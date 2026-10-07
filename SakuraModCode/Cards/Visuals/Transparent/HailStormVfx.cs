using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
/// The Clear card Hail as a hailstorm: a card-face cumulus gathers above the
/// enemies, a volley of hailstones pelts them, and one larger spiked stone per
/// target lands on the damage frame and cracks apart from its impact point.
/// Spent magic charge thickens the volley, the cloud and the finishing stones.
/// </summary>
/// <remarks>
/// Presentation only. The card snapshots the targets, spends the charge and calls
/// <see cref="Cues.Impact"/> immediately before each attack. Every visual is a
/// pure function of body time (seconds after the shared prelude) and the hit
/// timestamps, evaluated in <see cref="OnFrame"/>; the same model and constants
/// drive <c>scripts/render_hail_vfx.gd</c>. No screen shake: native damage
/// already shakes by unblocked amount, which grows with the spent charge.
/// </remarks>
internal sealed class HailStormVfx : CelVfxSession
{
    internal const string ScenePath = MainFile.ResPath + "/scenes/combat/card_vfx/hail_storm_vfx.tscn";
    internal const string StoneScenePath = MainFile.ResPath + "/scenes/combat/card_vfx/hail_stone_target.tscn";
    internal static IReadOnlyList<string> AssetPaths { get; } = [ScenePath, StoneScenePath];

    // Body timeline after the shared prelude (design D5).
    internal const float CloudForm = 0.15f;
    internal const float VolleyStart = 0.10f;
    internal const float VolleyWindow = 0.27f;
    internal const float FinisherStart = 0.55f;
    internal const float FinisherFall = 0.18f;
    internal const float FinisherContact = FinisherStart + FinisherFall;
    internal const float ContactPause = 0.035f;
    internal const float ShatterDuration = 0.40f;
    internal const float DisperseDelay = 0.10f;
    internal const float CloudDisperse = 0.30f;
    internal const float FadeDuration = 0.08f;
    private const float SquashIn = 0.06f;
    private const float BounceLife = 0.24f;
    private const float ChipLife = 0.22f;
    private const float GrainLife = 0.27f;

    // Motion.
    private const float StoneV0 = 1100f;
    private const float StoneGravity = 3000f;
    private const float Slant = 0.10f;
    private const float FragmentGravity = 1600f;
    private const float FragmentSpeed = 210f;
    private const float FragmentLift = 110f;
    // Must match SITE_ANGLE_0 / SITE_ANGLE_STEP in hail_stone.gdshader.
    private const float SiteAngle0 = -1.2208f;
    private const float SiteAngleStep = 1.2566371f;
    private const int FragmentCount = 5;

    // Budgets and layout.
    internal const int MaxVolley = 60;
    internal const int MinPerTarget = 3;
    internal const int MaxPerTarget = 15;
    private const float QuadReach = 1.45f;
    private const float MinCloudWidth = 560f;
    private const float CloudMargin = 70f;
    private const float CloudAboveTargets = 110f;
    private const float CloudBaseFraction = 0.86f;

    private static readonly Color GrainColour = new(0.86f, 0.90f, 1f);
    private static bool _loadFailureLogged;

    private readonly ShaderMaterial _cloudMaterial;
    private readonly ShaderMaterial _volleyMaterial;
    private readonly MultiMesh _volley;
    private readonly Node2D _debris;
    private readonly float _strength;
    private readonly float _slantDirection;
    private readonly float _salt;
    private readonly Stone[] _stones;
    private readonly Dictionary<Creature, Finisher> _finishers = [];
    private float? _bodyStart;
    private float _lastHitAt = float.NegativeInfinity;
    private float? _disperseAt;
    private bool _faded;

    private readonly record struct Lane(float X, float Top, float Height, float Width, float Floor);

    private readonly record struct Stone(
        float Spawn, int Kind, float Radius, bool Body, Vector2 Contact, float StartY, float Fall, float Seed);

    private sealed class Finisher(Node2D node, ShaderMaterial material, Vector2 contact, float startY, float radius)
    {
        internal Node2D Node { get; } = node;
        internal ShaderMaterial Material { get; } = material;
        internal Vector2 Contact { get; } = contact;
        internal float StartY { get; } = startY;
        internal float Radius { get; } = radius;
        /// <summary>Body time of this stone's own hit; null until its cue arrives.</summary>
        internal float? HitAt { get; set; }
        internal List<(Polygon2D Piece, Vector2 Velocity)> Grains { get; } = [];
    }

    private HailStormVfx(
        Node2D root,
        NCombatRoom room,
        PackedScene stoneScene,
        IReadOnlyList<(Creature Creature, Lane Lane)> targets,
        float strength,
        float slantDirection)
        : base(root, room)
    {
        _strength = strength;
        _slantDirection = slantDirection;
        _salt = (float)Random.Shared.NextDouble() * 97f;
        _debris = root.GetNode<Node2D>("%Debris");

        var lanes = targets.Select(static target => target.Lane).ToList();
        var cloud = root.GetNode<ColorRect>("%Cloud");
        _cloudMaterial = CelVfxGeometry.DuplicateMaterial(cloud, "hail cloud");
        var viewport = room.CombatVfxContainer.GetViewportRect();
        var cloudRect = CloudRect(lanes, strength, viewport);
        cloud.Size = cloudRect.Size;
        cloud.GlobalPosition = cloudRect.Position;
        _cloudMaterial.SetShaderParameter("region_size", cloudRect.Size);
        _cloudMaterial.SetShaderParameter("seed", _salt % 7f);
        _cloudMaterial.SetShaderParameter("strength", strength);
        _cloudMaterial.SetShaderParameter("formation", 0f);
        _cloudMaterial.SetShaderParameter("dispersal", 0f);
        var cloudBase = cloudRect.Position.Y + cloudRect.Size.Y * CloudBaseFraction;

        var volleyNode = root.GetNode<MultiMeshInstance2D>("%Volley");
        _volleyMaterial = CelVfxGeometry.DuplicateMaterial(volleyNode, "hail volley");
        _stones = BuildStones(lanes, strength, cloudBase);
        _volley = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform2D,
            UseCustomData = true,
            Mesh = UnitQuad(),
            InstanceCount = _stones.Length * 3
        };
        volleyNode.Multimesh = _volley;
        for (var i = 0; i < _volley.InstanceCount; i++)
            Hide(i);

        var stonesParent = root.GetNode<Node2D>("%Stones");
        for (var index = 0; index < targets.Count; index++)
        {
            var (creature, lane) = targets[index];
            if (_finishers.ContainsKey(creature))
                continue;
            _finishers.Add(creature, CreateFinisher(stoneScene, stonesParent, lane, index, cloudBase));
        }
    }

    protected override IEnumerable<ShaderMaterial> Materials =>
        _finishers.Values.Select(static finisher => finisher.Material).Append(_cloudMaterial).Append(_volleyMaterial);

    protected override float MaximumLifetime => 9f;

    private float Elapsed => _cloudMaterial.GetShaderParameter("elapsed").AsSingle();

    // --- Pure model -----------------------------------------------------------

    /// <summary>Spent charge as a share of the card's maximum spend, in [0, 1].</summary>
    internal static float Strength(int spent, int maxSpend) =>
        maxSpend <= 0 ? 0f : Math.Clamp(spent / (float)maxSpend, 0f, 1f);

    /// <summary>Volley stones per target: 6 → 15 with strength, inside the total cap.</summary>
    internal static int VolleyPerTarget(float strength, int targetCount)
    {
        var wanted = (int)MathF.Round(6f + 9f * Math.Clamp(strength, 0f, 1f));
        return Math.Clamp(Math.Min(wanted, MaxVolley / Math.Max(targetCount, 1)), MinPerTarget, MaxPerTarget);
    }

    internal static float FinisherScale(float strength) => 0.85f + 0.40f * Math.Clamp(strength, 0f, 1f);

    internal static float FinisherRadius(float laneWidth, float laneHeight, float strength) =>
        Math.Clamp(0.20f * Math.Min(laneWidth, laneHeight), 36f, 85f) * FinisherScale(strength);

    internal static float CloudHeight(float strength) => 150f + 40f * Math.Clamp(strength, 0f, 1f);

    /// <summary>Seconds a volley stone takes to fall <paramref name="distance"/> pixels.</summary>
    internal static float FallTime(float distance) =>
        (-StoneV0 + MathF.Sqrt(StoneV0 * StoneV0 + 2f * StoneGravity * Math.Max(distance, 0f))) / StoneGravity;

    /// <summary>Launch direction of fracture piece <paramref name="index"/>, along its own wedge.</summary>
    internal static Vector2 FragmentDirection(int index) =>
        Vector2.FromAngle(SiteAngle0 + index * SiteAngleStep);

    /// <summary>
    /// Seconds the outro waits: whichever is later of the newest fracture finishing
    /// and the cloud dispersing (which starts no earlier than the outro itself),
    /// then the root fade. Never re-waits beats that already played.
    /// </summary>
    internal static float ReleaseSeconds(float elapsed, float lastHitAt)
    {
        var fracture = lastHitAt + ContactPause + ShatterDuration - elapsed;
        var cloud = Math.Max(elapsed, lastHitAt + DisperseDelay) + CloudDisperse - elapsed;
        return Math.Clamp(Math.Max(fracture, cloud), 0f, ContactPause + ShatterDuration + DisperseDelay + CloudDisperse)
            + FadeDuration;
    }

    internal static float Hash(int index, int salt, float sessionSalt = 0f)
    {
        var v = MathF.Sin(index * 12.9898f + salt * 78.233f + sessionSalt) * 43758.5453f;
        return v - MathF.Floor(v);
    }

    // --- Orchestration --------------------------------------------------------

    internal static Task PlayOrResolveAsync(
        CardModel card,
        Creature? caster,
        IReadOnlyList<Creature> targets,
        int spentCharge,
        int maxSpend,
        Func<Cues, Task> resolveGameplay)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(resolveGameplay);
        return CelVfxSession.PlayOrResolveAsync(
            "Hail storm",
            () => TryCreate(caster, targets, Strength(spentCharge, maxSpend)),
            session => session.PlayPrelude(card, caster),
            scope => resolveGameplay(new Cues(scope)),
            session => session.FadeAndDispose(),
            session => session.Dispose());
    }

    internal sealed class Cues(CueScope<HailStormVfx> scope)
    {
        /// <summary>This target's finishing stone cracks apart. Call immediately before its attack.</summary>
        internal void Impact(Creature target)
        {
            ArgumentNullException.ThrowIfNull(target);
            scope.Invoke("impact", session => session.Impact(target));
        }
    }

    private static HailStormVfx? TryCreate(Creature? caster, IReadOnlyList<Creature> targets, float strength)
    {
        if (targets.Count == 0 || !TryPrepare("Hail storm", LoadScenes, out var room, out _, out var scenes))
            return null;

        Node2D? root = null;
        try
        {
            var resolved = new List<(Creature, Lane)>(targets.Count);
            for (var index = 0; index < targets.Count; index++)
            {
                var geometry = CelVfxGeometry.Resolve(room, targets[index], index, Budget);
                var floor = geometry.Center.Y + geometry.Size.Y * 0.5f;
                resolved.Add((targets[index],
                    new Lane(geometry.Center.X, floor - geometry.Size.Y, geometry.Size.Y, geometry.Size.X, floor)));
            }

            // Hail slants away from the caster, like the wind behind her cast.
            var targetsX = resolved.Average(static target => target.Item2.X);
            var casterX = caster is not null && CelVfxGeometry.ResolveCaster(room.GetCreatureNode(caster)) is { } anchor
                ? anchor.BodyCenter.X
                : targetsX - 1f;
            var slant = targetsX >= casterX ? 1f : -1f;

            root = scenes.Root.Instantiate<Node2D>();
            root.Name = "SakuraHailStormVfx";
            root.ZAsRelative = true;
            root.ZIndex = 0;
            room.CombatVfxContainer.AddChildSafely(root);
            root.GlobalPosition = Vector2.Zero;

            var session = new HailStormVfx(root, room, scenes.Stone, resolved, strength, slant);
            // Started after construction: the base clock pulls Materials.
            session.StartClock();
            return session;
        }
        catch (Exception exception)
        {
            if (!_loadFailureLogged)
            {
                _loadFailureLogged = true;
                MainFile.Logger.Error($"Could not create Hail storm VFX from {ScenePath}: {exception}");
            }
            root?.QueueFreeSafely();
            return null;
        }
    }

    private static CelVfxGeometry.GeometryBudget Budget => new(
        HorizontalPadding: 0f, VerticalPadding: 0f,
        MinWidth: 90f, MinHeight: 130f, MaxWidth: 460f, MaxHeight: 560f,
        FallbackWidth: 180f, FallbackHeight: 260f, FloorClearance: 0f);

    private static (PackedScene Root, PackedScene Stone) LoadScenes() =>
        (PreloadManager.Cache.GetScene(ScenePath), PreloadManager.Cache.GetScene(StoneScenePath));

    /// <summary>Shared wand prelude, then the body clock runs until the finishing stones land.</summary>
    private async Task<bool> PlayPrelude(CardModel card, Creature? caster)
    {
        if (!await PlayCelPrelude(card, caster))
            return false;
        _bodyStart = Elapsed;
        return await WaitActive(FinisherContact);
    }

    private void Impact(Creature target)
    {
        if (_faded || !IsActive() || _bodyStart is null
            || !_finishers.TryGetValue(target, out var finisher) || finisher.HitAt is not null)
        {
            return;
        }
        Shatter(finisher, BodyTime);
    }

    private void Shatter(Finisher finisher, float bodyTime)
    {
        // A hit that arrives before the stone has landed still breaks it at contact.
        var hitAt = Math.Max(bodyTime, FinisherContact);
        finisher.HitAt = hitAt;
        _lastHitAt = Math.Max(_lastHitAt, _bodyStart!.Value + hitAt);
        var grains = 8 + (int)MathF.Round(6f * _strength);
        for (var g = 0; g < grains; g++)
        {
            var r = 2.2f + g % 3 * 0.9f;
            var piece = new Polygon2D
            {
                Name = "HailGrain",
                Color = GrainColour,
                Polygon = [new(-r, -r * 0.6f), new(r * 0.8f, -r), new(r, r * 0.55f), new(-r * 0.5f, r)],
                Modulate = new Color(1f, 1f, 1f, 0f)
            };
            _debris.AddChildSafely(piece);
            piece.GlobalPosition = finisher.Contact;
            var spread = -0.8f + 1.6f * g / Math.Max(1, grains - 1);
            finisher.Grains.Add((piece, new Vector2(
                spread * (230f + 90f * _strength),
                -110f - g % 3 * 40f - 50f * _strength)));
        }
        UpdateFinisher(finisher, BodyTime);
    }

    private void FadeAndDispose()
    {
        if (_faded || !IsActive() || _bodyStart is null)
        {
            Dispose();
            return;
        }
        _faded = true;
        var now = BodyTime;
        // Targets whose cue never came (already dead, skipped) still break apart.
        foreach (var finisher in _finishers.Values)
        {
            if (finisher.HitAt is null)
                Shatter(finisher, now);
        }
        var lastHitBody = _lastHitAt - _bodyStart.Value;
        _disperseAt = Math.Max(now, lastHitBody + DisperseDelay);

        var release = ReleaseSeconds(Elapsed, _lastHitAt) - FadeDuration;
        var fade = Track(Root.CreateTween());
        if (release > 0f)
            fade.TweenInterval(release);
        fade.TweenProperty(Root, "modulate:a", 0f, FadeDuration);
        fade.TweenCallback(Callable.From(Dispose));
    }

    private float BodyTime => _bodyStart is { } start ? Elapsed - start : 0f;

    // --- Per-frame model --------------------------------------------------------

    protected override void OnFrame(float delta)
    {
        if (_bodyStart is null)
            return;
        var t = BodyTime;
        _cloudMaterial.SetShaderParameter("formation", Math.Clamp(t / CloudForm, 0f, 1f));
        if (_disperseAt is { } disperseAt)
            _cloudMaterial.SetShaderParameter("dispersal", Math.Clamp((t - disperseAt) / CloudDisperse, 0f, 1f));
        UpdateVolley(t);
        foreach (var finisher in _finishers.Values)
            UpdateFinisher(finisher, t);
    }

    private void UpdateVolley(float t)
    {
        var count = _stones.Length;
        for (var i = 0; i < count; i++)
        {
            var stone = _stones[i];
            var local = t - stone.Spawn;
            if (local >= 0f && local < stone.Fall)
            {
                var distance = stone.Contact.Y - stone.StartY;
                var y = stone.StartY + CelVfxGeometry.BallisticOffset(new Vector2(0f, StoneV0), StoneGravity, local).Y;
                var x = stone.Contact.X - _slantDirection * Slant * distance * (1f - local / stone.Fall);
                var stretch = 1f + Math.Clamp((StoneV0 + StoneGravity * local) / 2000f, 0f, 1f) * 0.22f;
                var size = 2f * QuadReach * stone.Radius;
                Show(i, new Transform2D(0f, new Vector2(size / MathF.Sqrt(stretch), size * stretch), 0f, new Vector2(x, y)),
                    stone.Seed, stone.Kind, 1f);
            }
            else if (local >= stone.Fall && local - stone.Fall < BounceLife)
            {
                var age = local - stone.Fall;
                var velocity = new Vector2(
                    (Hash(i, 7, _salt) - 0.5f) * 2f * (stone.Body ? 200f : 110f),
                    -(stone.Body ? 170f + 130f * Hash(i, 8, _salt) : 90f + 60f * Hash(i, 8, _salt)));
                var position = stone.Contact + CelVfxGeometry.BallisticOffset(velocity, FragmentGravity, age);
                var size = 2f * QuadReach * stone.Radius * (1f - 0.3f * age / BounceLife);
                Show(i, new Transform2D((Hash(i, 9, _salt) - 0.5f) * 14f * age, new Vector2(size, size), 0f, position),
                    stone.Seed, stone.Kind, 1f - SmoothStep(0.08f, BounceLife, age));
            }
            else
            {
                Hide(i);
            }

            for (var c = 0; c < 2; c++)
            {
                var chip = count + i * 2 + c;
                var age = local - stone.Fall;
                if (!stone.Body || age < 0f || age >= ChipLife)
                {
                    Hide(chip);
                    continue;
                }
                var side = c == 0 ? -1f : 1f;
                var velocity = new Vector2(side * (140f + 100f * Hash(i, 11 + c, _salt)), -(130f + 110f * Hash(i, 13 + c, _salt)));
                var size = 2f * QuadReach * (2.6f + 1.4f * Hash(i, 15 + c, _salt));
                Show(chip, new Transform2D(age * 12f * side, new Vector2(size, size), 0f,
                        stone.Contact + CelVfxGeometry.BallisticOffset(velocity, FragmentGravity, age)),
                    Hash(i, 17 + c, _salt) * 5f, 3, 1f - SmoothStep(0.06f, ChipLife, age));
            }
        }
    }

    private void UpdateFinisher(Finisher finisher, float t)
    {
        var material = finisher.Material;
        var fall = Math.Clamp((t - FinisherStart) / FinisherFall, 0f, 1f);
        var time = fall * FinisherFall;
        var distance = finisher.Contact.Y - finisher.StartY;
        var v0 = distance / FinisherFall * 0.5f;
        var gravity = 2f * (distance - v0 * FinisherFall) / (FinisherFall * FinisherFall);
        finisher.Node.GlobalPosition = new Vector2(finisher.Contact.X,
            finisher.StartY + CelVfxGeometry.BallisticOffset(new Vector2(0f, v0), gravity, time).Y);
        material.SetShaderParameter("formation", t < FinisherStart ? 0f : Math.Min(1f, fall * 3f));
        var landed = t - FinisherContact;
        material.SetShaderParameter("squash", landed >= 0f ? Math.Clamp(landed / SquashIn, 0f, 1f) : 0f);

        if (finisher.HitAt is not { } hitAt)
            return;
        var age = t - hitAt - ContactPause;
        var shatter = Math.Clamp(age, 0f, ShatterDuration);
        material.SetShaderParameter("crack", 1f);
        material.SetShaderParameter("split", age >= 0f ? 1f : 0f);
        material.SetShaderParameter("shatter", shatter / ShatterDuration);
        var boost = 0.85f + 0.45f * _strength;
        for (var i = 0; i < FragmentCount; i++)
        {
            var direction = FragmentDirection(i);
            var velocity = (direction * FragmentSpeed + new Vector2(0f, -FragmentLift)) * boost;
            var offset = CelVfxGeometry.BallisticOffset(velocity, FragmentGravity, shatter);
            var angle = shatter * MathF.Sign(direction.X) * (1.4f + i * 0.25f);
            material.SetShaderParameter($"fragment_{i}", new Vector3(offset.X, offset.Y, angle));
        }

        var grainAge = Math.Clamp(age, 0f, GrainLife);
        foreach (var (piece, velocity) in finisher.Grains)
        {
            if (!GodotObject.IsInstanceValid(piece))
                continue;
            piece.GlobalPosition = finisher.Contact + CelVfxGeometry.BallisticOffset(velocity, FragmentGravity, grainAge);
            piece.Rotation = grainAge * 2.4f;
            piece.Modulate = new Color(1f, 1f, 1f,
                age < 0f || age > GrainLife ? 0f : (1f - SmoothStep(0.12f, GrainLife, age)) * 0.95f);
        }
    }

    // --- Construction helpers -----------------------------------------------------

    private static Rect2 CloudRect(IReadOnlyList<Lane> lanes, float strength, Rect2 viewport)
    {
        var left = lanes.Min(static lane => lane.X - lane.Width * 0.5f);
        var right = lanes.Max(static lane => lane.X + lane.Width * 0.5f);
        var top = lanes.Min(static lane => lane.Top);
        var widen = Math.Max(CloudMargin, (MinCloudWidth - (right - left)) * 0.5f);
        left = Math.Max(viewport.Position.X, left - widen);
        right = Math.Min(viewport.End.X, right + widen);
        var height = CloudHeight(strength);
        var cloudBase = Math.Max(top - CloudAboveTargets, viewport.Position.Y + height * 0.75f);
        return new Rect2(left, cloudBase - height * CloudBaseFraction, right - left, height);
    }

    private Stone[] BuildStones(IReadOnlyList<Lane> lanes, float strength, float cloudBase)
    {
        var perTarget = VolleyPerTarget(strength, lanes.Count);
        var stones = new Stone[perTarget * lanes.Count];
        for (var i = 0; i < stones.Length; i++)
        {
            var lane = lanes[i % lanes.Count];
            var slot = i / lanes.Count;
            var roll = Hash(i, 2, _salt);
            var kind = roll < 0.5f ? 0 : roll < 0.82f ? 1 : 2;
            var sizeRoll = Hash(i, 10, _salt);
            var radius = kind switch
            {
                0 => Mathf.Lerp(6f, 9f, sizeRoll),
                1 => Mathf.Lerp(9f, 13f, sizeRoll),
                _ => Mathf.Lerp(12f, 17f, sizeRoll)
            } * (0.9f + 0.3f * strength);
            var body = Hash(i, 4, _salt) < 0.6f;
            var contact = new Vector2(
                lane.X + (Hash(i, 3, _salt) - 0.5f) * lane.Width * 1.1f,
                body ? lane.Top + lane.Height * (0.15f + 0.55f * Hash(i, 5, _salt)) : lane.Floor - radius * 0.6f);
            var startY = cloudBase - 12f;
            stones[i] = new Stone(
                VolleyStart + VolleyWindow * (slot + Hash(i, 1, _salt) * 0.9f) / perTarget,
                kind, radius, body, contact, startY, FallTime(contact.Y - startY), Hash(i, 6, _salt) * 7f);
        }
        return stones;
    }

    private Finisher CreateFinisher(PackedScene scene, Node2D parent, Lane lane, int index, float cloudBase)
    {
        var node = scene.Instantiate<Node2D>();
        node.Name = $"HailStone{index + 1}";
        parent.AddChildSafely(node);
        var body = node.GetNode<ColorRect>("%StoneBody");
        var material = CelVfxGeometry.DuplicateMaterial(body, $"hail stone {index}");
        var radius = FinisherRadius(lane.Width, lane.Height, _strength);
        // Extra canvas lets the five pieces travel; the stone itself is radius-sized.
        var drawSize = new Vector2(radius * 2f + 420f, radius * 2f + 480f);
        body.Size = drawSize;
        body.Position = -drawSize * 0.5f;
        var contact = new Vector2(lane.X, lane.Top + lane.Height * 0.35f);
        material.SetShaderParameter("radius", radius);
        material.SetShaderParameter("draw_size", drawSize);
        material.SetShaderParameter("floor_y", lane.Floor - contact.Y);
        material.SetShaderParameter("seed", index * 0.317f + 0.19f + _salt % 3f);
        var finisher = new Finisher(node, material, contact, cloudBase - radius * 0.3f, radius);
        UpdateFinisher(finisher, 0f);
        return finisher;
    }

    private static ArrayMesh UnitQuad()
    {
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = new Vector2[] { new(-0.5f, -0.5f), new(0.5f, -0.5f), new(0.5f, 0.5f), new(-0.5f, 0.5f) };
        arrays[(int)Mesh.ArrayType.TexUV] = new Vector2[] { new(0f, 0f), new(1f, 0f), new(1f, 1f), new(0f, 1f) };
        arrays[(int)Mesh.ArrayType.Index] = new[] { 0, 1, 2, 0, 2, 3 };
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    private void Show(int index, Transform2D transform, float seed, int kind, float alpha)
    {
        _volley.SetInstanceTransform2D(index, transform);
        _volley.SetInstanceCustomData(index, new Color(seed, kind, alpha, 0f));
    }

    private void Hide(int index)
    {
        _volley.SetInstanceTransform2D(index, new Transform2D(0f, Vector2.Zero, 0f, Vector2.Zero));
        _volley.SetInstanceCustomData(index, new Color(0f, 0f, 0f, 0f));
    }

    private static float SmoothStep(float from, float to, float x)
    {
        var t = Math.Clamp((x - from) / (to - from), 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}
