using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
/// The Clear card Aqua as its own water phoenix: a whirlpool opens on the floor in
/// front of Sakura, the bird rises out of it with its tail still coiled into the
/// vortex, holds the card-face pose, then sweeps low through every enemy in turn
/// with a splash crown on each hit. Every enemy still holding Frostbite after the
/// attacks freezes its splash into ice and sends the shards back into the vortex,
/// which then closes.
/// </summary>
/// <remarks>
/// Presentation only. The card owns the target snapshot, damage, the Frostbite read
/// and the draw/energy rewards; it awaits <see cref="Cues.ReachAsync"/> before each
/// attack and calls <see cref="Cues.Return"/> after the Frostbite read. Beat
/// constants, layout, the wavy tail and the crystal shape are kept in step with
/// <c>scripts/render_aqua_vfx.gd</c>, the approved offline plate.
/// </remarks>
internal sealed class AquaPhoenixVfx : CelVfxSession
{
    internal const string ScenePath =
        MainFile.ResPath + "/scenes/combat/card_vfx/aqua_phoenix_vfx.tscn";
    internal const string ShaderPath =
        MainFile.ResPath + "/shaders/card_vfx/aqua_phoenix.gdshader";
    internal static IReadOnlyList<string> AssetPaths { get; } = [ScenePath];

    // Beat timeline after the shared prelude, in body seconds (plate D5/D6).
    /// <summary>The shared standard prelude's blocking lead, before the body clock starts.</summary>
    internal const float SharedPreludeLead = 0.18f;
    internal const float VortexOpen = 0.10f;
    internal const float RiseStart = 0.03f;
    internal const float ApexAt = 0.18f;
    internal const float FirstReach = 0.40f;
    internal const float SweepSpacing = 0.075f;
    internal const float SweepSpanCap = 0.30f;
    internal const float TailDuration = 0.30f;
    internal const float SplashRise = 0.08f;
    internal const float SplashFall = 0.30f;
    // The plate's stand-in for the attack loop finishing after the last hit; live
    // play uses the moment gameplay actually calls Return.
    internal const float PlannedReturnAfterLast = 0.10f;
    internal const float FreezeDuration = 0.10f;
    internal const float FreezeHold = 0.04f;
    internal const float ShatterDuration = 0.20f;
    internal const int CrystalsPerEnemy = 4;
    internal const float CrystalFlight = 0.28f;
    internal const float CrystalStaggerMax = 0.05f;
    internal const float EnemyStagger = 0.03f;
    internal const float CloseAfterTail = 0.20f;
    internal const float CloseWithoutReturn = 0.20f;
    internal const float CloseAfterReturn = 0.15f;
    private const float FadeDuration = 0.06f;
    // The body clock runs as one tween; the lifetime cap ends it first.
    private const float BodyClockSpan = 7.8f;

    // Layout, in screen pixels (plate geometry).
    private const float VortexForward = 1.2f;
    private const float VortexDrop = 4f;
    private static readonly Vector2 ApexOffset = new(150f, -320f);
    private const float ApexMinTop = 220f;
    private const float HelixRadius = 92f;
    private const float HelixLift = 170f;
    private const float HelixSpin = 18f;
    private const float HelixStartAngle = -1.047f;
    private const float TailLength = 380f;
    private const float TailLengthAttached = 1050f;
    private const int TailPoints = 40;
    private const float TailWidth = 46f;
    private const float TailWave = 20f;
    private const float TailWavelength = 170f;
    private const float BirdScale = 1.12f;
    private const float SweepScale = 0.84f;
    private const float SweepSpread = 0.12f;
    private const float ExitDistance = 300f;
    private const float CollapseDrop = 130f;
    private const float SplashAnchorShare = 0.42f;
    private const float SplashCrownHeight = 118f;
    private const float IceCentreDesign = 48f;

    /// <summary>
    /// Returning ice shard size: this many times the 54x10 px base outline, about
    /// 70x13 px at launch scale 1.0 (plate <c>CRYSTAL_SCALE</c>).
    /// </summary>
    internal const float CrystalScale = 1.3f;
    private const float CrystalTrailWidth = 7f;

    private static readonly Color CrystalLight = new(0.90f, 0.99f, 1.0f);
    private static readonly Color CrystalShade = new(0.52f, 0.80f, 0.97f);
    private static readonly Color CrystalGlint = new(1.0f, 0.86f, 0.38f);
    private static readonly Color TrailTail = new(0.75f, 0.95f, 1.0f, 0f);
    private static readonly Color TrailHead = new(0.90f, 1.0f, 1.0f, 0.80f);

    private static bool _loadFailureLogged;

    private readonly IReadOnlyList<Creature> _targets;
    private readonly bool _empowered;
    private readonly float _forward;
    private readonly Vector2 _vortexCentre;
    private readonly Vector2 _apex;
    private readonly float[] _arrivals;
    private readonly float _lastArrival;
    private readonly Vector2[] _keyPoints;
    private readonly float[] _keyTimes;
    private readonly Vector2[] _icePoints;
    private readonly float[] _splashStart;
    private readonly float[] _iceStart;
    private readonly Node2D?[] _splashes;
    private readonly ShaderMaterial?[] _splashMaterials;
    private readonly List<Crystal> _crystals = [];

    private readonly Node2D _vortex;
    private readonly Line2D _tail;
    private readonly Node2D _bird;
    private readonly Node2D _ice;
    private readonly ShaderMaterial _vortexMaterial;
    private readonly ShaderMaterial _tailMaterial;
    private readonly ShaderMaterial _birdMaterial;

    private float _t;
    private bool _bodyStarted;
    private bool _faded;
    private float _closeAt = float.NaN;
    private float _closeLength = CloseWithoutReturn;

    private readonly record struct Crystal(Node2D Node, Line2D Trail, int Enemy, int Index, Line2D? Glint);

    private AquaPhoenixVfx(
        Node2D root,
        NCombatRoom room,
        IReadOnlyList<Creature> targets,
        bool empowered,
        Vector2 vortexCentre,
        float forward,
        float viewportTop,
        IReadOnlyList<CelVfxGeometry.TargetGeometry> geometries,
        IReadOnlyList<bool> hasNode)
        : base(root, room)
    {
        _targets = targets;
        _empowered = empowered;
        _forward = forward;
        _vortexCentre = vortexCentre;
        var apex = vortexCentre + new Vector2(ApexOffset.X * forward, ApexOffset.Y);
        _apex = new Vector2(apex.X, Math.Max(apex.Y, viewportTop + ApexMinTop));

        _vortex = root.GetNode<Node2D>("%Vortex");
        _tail = root.GetNode<Line2D>("%Tail");
        _bird = root.GetNode<Node2D>("%Bird");
        _ice = root.GetNode<Node2D>("%Ice");
        _vortexMaterial = CelVfxGeometry.DuplicateMaterial(root.GetNode<ColorRect>("%VortexBody"), "aqua vortex");
        _tailMaterial = CelVfxGeometry.DuplicateMaterial(_tail, "aqua tail");
        _birdMaterial = CelVfxGeometry.DuplicateMaterial(root.GetNode<ColorRect>("%BirdBody"), "aqua phoenix body");

        var count = targets.Count;
        _arrivals = new float[count];
        for (var i = 0; i < count; i++)
            _arrivals[i] = ArrivalTime(i, count);
        _lastArrival = _arrivals[count - 1];

        var hits = new Vector2[count];
        _icePoints = new Vector2[count];
        _splashStart = Enumerable.Repeat(float.NaN, count).ToArray();
        _iceStart = Enumerable.Repeat(float.NaN, count).ToArray();
        _splashes = new Node2D?[count];
        _splashMaterials = new ShaderMaterial?[count];

        var seed = (float)Random.Shared.NextDouble() * 6.1f;
        foreach (var material in new[] { _vortexMaterial, _tailMaterial, _birdMaterial })
        {
            material.SetShaderParameter("seed", seed);
            // The vortex flash is the Energy payoff; it is switched on only when ice returns.
            material.SetShaderParameter("empowered", 0f);
            material.SetShaderParameter("form", 0f);
            material.SetShaderParameter("dissolve", 0f);
            material.SetShaderParameter("opacity", 0f);
        }

        var template = root.GetNode<Node2D>("%SplashTemplate");
        var splashLayer = root.GetNode<Node2D>("%Splashes");
        for (var i = 0; i < count; i++)
        {
            var geometry = geometries[i];
            // The bird glides through the body middle, below the head and intent.
            hits[i] = geometry.Center;
            var floorY = geometry.Center.Y + geometry.Size.Y * 0.5f;
            var splashAnchor = new Vector2(geometry.Center.X, floorY - geometry.Size.Y * SplashAnchorShare);
            var splashScale = SplashScale(geometry.Size.Y);
            _icePoints[i] = splashAnchor + new Vector2(0f, -IceCentreDesign * splashScale);
            if (!hasNode[i])
                continue;

            var splash = (Node2D)template.Duplicate();
            splash.UniqueNameInOwner = false;
            splash.Name = $"AquaSplash{i}";
            splashLayer.AddChildSafely(splash);
            var material = CelVfxGeometry.DuplicateMaterial(splash.GetNode<ColorRect>("SplashBody"), "aqua splash");
            material.SetShaderParameter("seed", seed + i * 1.37f);
            material.SetShaderParameter("empowered", empowered ? 1f : 0f);
            material.SetShaderParameter("form", 0f);
            material.SetShaderParameter("dissolve", 0f);
            material.SetShaderParameter("freeze", 0f);
            material.SetShaderParameter("shatter", 0f);
            material.SetShaderParameter("opacity", 0f);
            splash.GlobalPosition = splashAnchor;
            splash.Scale = Vector2.One * splashScale;
            splash.Visible = false;
            _splashes[i] = splash;
            _splashMaterials[i] = material;
        }

        // Flight keys: rise out of the helix, the held card pose, every hit point,
        // then on past the last enemy.
        var points = new List<Vector2> { Helix(RiseStart), _apex };
        var times = new List<float> { RiseStart, ApexAt };
        for (var i = 0; i < count; i++)
        {
            points.Add(hits[i]);
            times.Add(_arrivals[i]);
        }
        var last = points[^1];
        var previous = points[^2];
        var heading = (last - previous).Normalized();
        heading = new Vector2(heading.X, Math.Min(heading.Y, 0.15f)).Normalized();
        points.Add(last + heading * ExitDistance + new Vector2(0f, -30f));
        times.Add(_lastArrival + TailDuration);
        _keyPoints = [.. points];
        _keyTimes = [.. times];

        _vortex.GlobalPosition = vortexCentre;
        // The tail's helix is mirrored with the arena, so the spiral it coils
        // into turns with it; the vortex shading has no screen-fixed light.
        _vortex.Scale = new Vector2(forward, 1f);
        _vortex.Visible = false;
        _bird.Visible = false;
        _tail.Visible = false;
    }

    protected override IEnumerable<ShaderMaterial> Materials =>
        new[] { _vortexMaterial, _tailMaterial, _birdMaterial }
            .Concat(_splashMaterials.OfType<ShaderMaterial>());

    // Covers the prelude, the sweep, slow gameplay between hits, and the ice return.
    protected override float MaximumLifetime => 8.0f;

    // --- Pure timing ----------------------------------------------------------

    /// <summary>Gap between consecutive hits: the whole sweep stays within <see cref="SweepSpanCap"/>.</summary>
    internal static float SweepSpacingFor(int count) =>
        count <= 1 ? SweepSpacing : Math.Min(SweepSpacing, SweepSpanCap / (count - 1));

    /// <summary>Body time at which the bird reaches enemy <paramref name="index"/> of <paramref name="count"/>.</summary>
    internal static float ArrivalTime(int index, int count) => FirstReach + index * SweepSpacingFor(count);

    /// <summary>Body time from the first hit to the last.</summary>
    internal static float SweepSpan(int count) =>
        count <= 1 ? 0f : ArrivalTime(count - 1, count) - ArrivalTime(0, count);

    /// <summary>Gap between the shards one enemy sends back.</summary>
    internal static float CrystalStagger => Math.Min(CrystalStaggerMax, 0.15f / CrystalsPerEnemy);

    /// <summary>Body time the last shard lands, for ice that starts returning at <paramref name="returnAt"/>.</summary>
    internal static float LandedAt(float returnAt, int frostbiteCount) =>
        returnAt
        + (Math.Max(frostbiteCount, 1) - 1) * EnemyStagger
        + FreezeDuration + FreezeHold
        + (CrystalsPerEnemy - 1) * CrystalStagger
        + CrystalFlight;

    /// <summary>
    /// Planned body length when gameplay keeps pace (Return at the plate's stand-in
    /// time): the later of the bird's tail and the vortex closing.
    /// </summary>
    internal static float PlannedBodySeconds(int count, int frostbiteCount)
    {
        var last = ArrivalTime(count - 1, count);
        var tailEnd = last + TailDuration;
        var closeEnd = frostbiteCount > 0
            ? LandedAt(last + PlannedReturnAfterLast, frostbiteCount) + CloseAfterReturn
            : last + CloseAfterTail + CloseWithoutReturn;
        return Math.Max(tailEnd, closeEnd);
    }

    /// <summary>Only the unplayed part of the tail after the last hit survives gameplay resolution.</summary>
    internal static float ReleaseSeconds(float elapsed, float lastArrival) => Math.Clamp(
        TailDuration - Math.Max(0f, elapsed - lastArrival),
        0f,
        TailDuration);

    internal static float SplashScale(float bodyHeight) =>
        Math.Clamp(bodyHeight * 0.33f / SplashCrownHeight, 0.6f, 1.2f);

    // --- Orchestration --------------------------------------------------------

    internal static Task PlayOrResolveAsync(
        CardModel card,
        Creature? caster,
        IReadOnlyList<Creature> targets,
        bool empowered,
        Func<Cues, Task> resolveGameplay)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(resolveGameplay);

        return CelVfxSession.PlayOrResolveAsync(
            "Aqua phoenix",
            () => TryCreate(caster, targets, empowered),
            session => session.PlayPrelude(card, caster),
            scope => resolveGameplay(new Cues(scope)),
            session => session.FadeAndDispose(),
            session => session.Dispose());
    }

    internal sealed class Cues(CueScope<AquaPhoenixVfx> scope)
    {
        /// <summary>
        /// Waits until the bird reaches <paramref name="enemy"/>, then throws its
        /// splash. Await immediately before that enemy's attack.
        /// </summary>
        /// <remarks>
        /// Returns at once when presentation is off, failed, or already past that
        /// enemy, so the wait exists only while the bird is actually flying.
        /// </remarks>
        internal Task ReachAsync(Creature enemy)
        {
            ArgumentNullException.ThrowIfNull(enemy);
            return scope.InvokeAsync("reach", session => session.ReachAsync(enemy));
        }

        /// <summary>
        /// Every listed enemy freezes its splash and sends ice back to the vortex.
        /// Call once, after the Frostbite read; an empty list lets the vortex close.
        /// </summary>
        internal void Return(IReadOnlyList<Creature> frostbiteEnemies)
        {
            ArgumentNullException.ThrowIfNull(frostbiteEnemies);
            scope.Invoke("return", session => session.Return(frostbiteEnemies));
        }
    }

    private static AquaPhoenixVfx? TryCreate(Creature? caster, IReadOnlyList<Creature> targets, bool empowered)
    {
        if (targets.Count == 0
            || !TryPrepare("Aqua phoenix", LoadScene, out var room, out _, out var scene))
        {
            return null;
        }

        Node2D? root = null;
        try
        {
            var viewport = room.CombatVfxContainer.GetViewportRect();
            var geometries = targets
                .Select((target, index) => CelVfxGeometry.Resolve(room, target, index, Budget))
                .ToArray();
            var hasNode = targets
                .Select(target => room.GetCreatureNode(target) is { } node && GodotObject.IsInstanceValid(node))
                .ToArray();
            var centroidX = geometries.Average(static geometry => geometry.Center.X);

            float casterX;
            float casterFloorY;
            float casterWidth;
            if (caster is not null
                && CelVfxGeometry.ResolveCaster(room.GetCreatureNode(caster)) is { } anchor)
            {
                casterX = anchor.BodyCenter.X;
                casterFloorY = anchor.Floor.Y;
                casterWidth = anchor.BodySize.X;
            }
            else
            {
                // No caster node: a stand-in on the far side of the arena from the enemies.
                casterX = centroidX >= viewport.GetCenter().X
                    ? viewport.Position.X + viewport.Size.X * 0.25f
                    : viewport.End.X - viewport.Size.X * 0.25f;
                casterFloorY = geometries.Max(static geometry => geometry.Center.Y + geometry.Size.Y * 0.5f);
                casterWidth = FallbackCasterWidth;
            }

            var forward = centroidX >= casterX ? 1f : -1f;
            var vortexCentre = new Vector2(casterX + forward * VortexForward * casterWidth, casterFloorY + VortexDrop);

            root = scene.Instantiate<Node2D>();
            root.Name = "SakuraAquaPhoenixVfx";
            root.ZAsRelative = true;
            root.ZIndex = 0;
            room.CombatVfxContainer.AddChildSafely(root);
            root.GlobalPosition = Vector2.Zero;

            var session = new AquaPhoenixVfx(
                root, room, targets, empowered, vortexCentre, forward, viewport.Position.Y, geometries, hasNode);
            // Started after construction, never inside it: the base clock pulls
            // Materials, and during a base constructor the subclass fields are empty.
            session.StartClock();
            return session;
        }
        catch (Exception exception)
        {
            LogLoadFailure(exception);
            root?.QueueFreeSafely();
            return null;
        }
    }

    private const float FallbackCasterWidth = 240f;

    private static CelVfxGeometry.GeometryBudget Budget => new(
        HorizontalPadding: 0f,
        VerticalPadding: 0f,
        MinWidth: 80f,
        MinHeight: 120f,
        MaxWidth: 420f,
        MaxHeight: 520f,
        FallbackWidth: 200f,
        FallbackHeight: 300f,
        FloorClearance: 0f);

    /// <summary>
    /// Shared wand prelude, then the body clock starts. Gameplay is not held here:
    /// each <see cref="Cues.ReachAsync"/> waits for its own enemy instead.
    /// </summary>
    private async Task<bool> PlayPrelude(CardModel card, Creature? caster)
    {
        if (!await PlayCelPrelude(card, caster))
            return false;

        StartBody();
        return IsActive();
    }

    private void StartBody()
    {
        if (_bodyStarted || !IsActive())
            return;

        _bodyStarted = true;
        Advance(0f);
        // One tween owns body time for every layer; configured fully before any yield.
        var body = Track(Root.CreateTween());
        body.TweenMethod(Callable.From<float>(Advance), 0f, BodyClockSpan, BodyClockSpan);
    }

    // --- Cues -----------------------------------------------------------------

    private async Task ReachAsync(Creature enemy)
    {
        var index = IndexOf(enemy);
        if (index < 0 || !float.IsNaN(_splashStart[index]) || !_bodyStarted || _faded || !IsActive())
            return;

        var wait = _arrivals[index] - _t;
        if (wait > 0f && !await WaitActive(wait))
            return;

        _splashStart[index] = _t;
    }

    private void Return(IReadOnlyList<Creature> frostbiteEnemies)
    {
        if (!_bodyStarted || _faded || !IsActive() || !float.IsNaN(_closeAt))
            return;

        var returnAt = _t;
        var order = 0;
        foreach (var enemy in frostbiteEnemies)
        {
            var index = IndexOf(enemy);
            if (index < 0 || !float.IsNaN(_iceStart[index]))
                continue;

            _iceStart[index] = returnAt + order * EnemyStagger;
            for (var c = 0; c < CrystalsPerEnemy; c++)
                _crystals.Add(BuildCrystal(index, c));
            order++;
        }

        if (order == 0)
        {
            _closeAt = Math.Max(_lastArrival + CloseAfterTail, _t);
            _closeLength = CloseWithoutReturn;
            return;
        }

        _closeAt = LandedAt(returnAt, order);
        _closeLength = CloseAfterReturn;
        _vortexMaterial.SetShaderParameter("empowered", _empowered ? 1f : 0f);
    }

    private int IndexOf(Creature enemy)
    {
        for (var i = 0; i < _targets.Count; i++)
        {
            if (ReferenceEquals(_targets[i], enemy))
                return i;
        }
        return -1;
    }

    // --- Frame ----------------------------------------------------------------

    private void Advance(float t)
    {
        _t = t;
        UpdateVortex(t);
        var grow = UpdateBird(t, out var collapse, out var dive);
        UpdateTail(t, grow, collapse, dive);
        UpdateSplashes(t);
        UpdateCrystals(t);
    }

    private void UpdateVortex(float t)
    {
        // Held open until gameplay says whether ice is coming back.
        var close = float.IsNaN(_closeAt) ? 0f : Math.Clamp((t - _closeAt) / _closeLength, 0f, 1f);
        _vortex.Visible = close < 1f;
        _vortexMaterial.SetShaderParameter("form", Math.Clamp(t / VortexOpen, 0f, 1f));
        _vortexMaterial.SetShaderParameter("dissolve", close);
        _vortexMaterial.SetShaderParameter("opacity", 1f);
    }

    private float UpdateBird(float t, out float collapse, out float dive)
    {
        var rise = Math.Clamp((t - RiseStart) / (ApexAt - RiseStart), 0f, 1f);
        collapse = Math.Clamp((t - _lastArrival) / TailDuration, 0f, 1f);
        _bird.Visible = t >= RiseStart && collapse < 1f;

        var position = Path(t);
        var velocity = (Path(t + 0.01f) - position) / 0.01f;
        // Collapse: the body is lowered and tipped toward the floor.
        _bird.GlobalPosition = position + new Vector2(0f, CollapseDrop * collapse * collapse);
        // Heading in the bird's own (unmirrored) frame, damped toward level so it
        // climbs and dives without pointing straight up or down.
        var heading = new Vector2(velocity.X * _forward, velocity.Y).Angle();
        var pitch = Math.Clamp(heading, -1.05f, 0.55f) * 0.75f;
        if (t < ApexAt)
            pitch = Mathf.Lerp(-0.80f, -0.30f, EaseInOut(rise));
        _bird.Rotation = _forward * (pitch + 0.45f * EaseInOut(collapse));

        var speed = Math.Clamp(velocity.Length() / 6000f, 0f, 1f);
        var grow = Mathf.Lerp(0.55f, 1f, EaseOut(rise));
        dive = EaseInOut(Math.Clamp((t - ApexAt - 0.03f) / (FirstReach - ApexAt - 0.03f), 0f, 1f));
        var size = Mathf.Lerp(BirdScale, SweepScale, dive) * grow;
        _bird.Scale = new Vector2(_forward * size * (1f + 0.12f * speed), size * (1f - 0.06f * speed));

        _birdMaterial.SetShaderParameter("form", EaseOut(rise * 1.6f));
        // Wings stay folded until the bird has risen clear of Sakura; full spread
        // only in the held pose, folding back into the dive.
        var spread = EaseBack(Mathf.SmoothStep(0.35f, 1f, rise));
        if (t > ApexAt)
            spread = Mathf.Lerp(1f, SweepSpread, dive);
        _birdMaterial.SetShaderParameter("spread", spread);
        _birdMaterial.SetShaderParameter("dissolve", collapse);
        _birdMaterial.SetShaderParameter("opacity", 1f);
        return grow;
    }

    private void UpdateTail(float t, float grow, float collapse, float dive)
    {
        _tail.Visible = _bird.Visible;
        if (!_tail.Visible)
            return;

        _tail.Points = TailPath(t);
        _tail.Width = TailWidth * grow * Mathf.Lerp(1f, 0.9f, dive);
        _tailMaterial.SetShaderParameter("form", Math.Clamp((t - RiseStart) / (ApexAt - RiseStart) * 2f, 0f, 1f));
        _tailMaterial.SetShaderParameter("dissolve", collapse * collapse);
        _tailMaterial.SetShaderParameter("opacity", 1f - Mathf.SmoothStep(0.6f, 1f, collapse));
    }

    private void UpdateSplashes(float t)
    {
        for (var i = 0; i < _targets.Count; i++)
        {
            if (_splashes[i] is not { } splash || _splashMaterials[i] is not { } material)
                continue;
            if (!GodotObject.IsInstanceValid(splash))
                continue;

            material.SetShaderParameter("opacity", 1f);
            var iceStart = _iceStart[i];
            if (!float.IsNaN(iceStart) && t >= iceStart)
            {
                // A Frostbite enemy re-forms its splash as ice, holds, then shatters.
                var age = t - iceStart;
                splash.Visible = age < FreezeDuration + FreezeHold + ShatterDuration;
                material.SetShaderParameter("form", EaseOut(age / 0.05f));
                material.SetShaderParameter("dissolve", 0f);
                material.SetShaderParameter("freeze", Math.Clamp(age / FreezeDuration, 0f, 1f));
                material.SetShaderParameter(
                    "shatter",
                    Math.Clamp((age - FreezeDuration - FreezeHold) / ShatterDuration, 0f, 1f));
                continue;
            }

            var start = _splashStart[i];
            var splashAge = float.IsNaN(start) ? -1f : t - start;
            splash.Visible = splashAge >= 0f && splashAge < SplashRise + SplashFall;
            material.SetShaderParameter("form", EaseOut(splashAge / SplashRise));
            material.SetShaderParameter("dissolve", Math.Clamp((splashAge - SplashRise) / SplashFall, 0f, 1f));
            material.SetShaderParameter("freeze", 0f);
            material.SetShaderParameter("shatter", 0f);
        }
    }

    private void UpdateCrystals(float t)
    {
        var end = _vortexCentre + new Vector2(0f, -6f);
        foreach (var crystal in _crystals)
        {
            if (!GodotObject.IsInstanceValid(crystal.Node) || !GodotObject.IsInstanceValid(crystal.Trail))
                continue;

            var launch = _iceStart[crystal.Enemy] + FreezeDuration + FreezeHold + crystal.Index * CrystalStagger;
            var u = (t - launch) / CrystalFlight;
            var visible = u >= 0f && u < 1f;
            crystal.Node.Visible = visible;
            crystal.Trail.Visible = visible;
            if (!visible)
                continue;

            var start = _icePoints[crystal.Enemy]
                + new Vector2(-24f + 16f * crystal.Index, -12f * (crystal.Index % 2));
            var control = (start + end) * 0.5f + new Vector2(0f, -220f - 40f * crystal.Index);
            var e = EaseInOut(u);
            crystal.Node.GlobalPosition = Bezier(start, control, end, e);
            crystal.Node.Rotation = BezierTangent(start, control, end, e).Angle();
            // Mirrored arenas flip the shard across its long axis too, so the lit
            // facet and glint keep the side the plate shows instead of inverting
            // when the flight heads the other way.
            var size = Mathf.Lerp(1.15f, 0.65f, e);
            crystal.Node.Scale = new Vector2(size, size * _forward);

            var trail = new Vector2[8];
            for (var k = 0; k < trail.Length; k++)
            {
                var te = EaseInOut(Math.Clamp(u - 0.22f * (1f - k / 7f), 0f, 1f));
                trail[k] = Bezier(start, control, end, te);
            }
            crystal.Trail.Points = trail;

            if (crystal.Glint is { } glint && GodotObject.IsInstanceValid(glint))
            {
                // The glint flickers along the edge as the shard turns.
                glint.Modulate = new Color(1f, 1f, 1f, 0.55f + 0.45f * Math.Abs(MathF.Sin(t * 24f + crystal.Index * 1.3f)));
            }
        }
    }

    // --- Flight path ----------------------------------------------------------

    /// <summary>The tail coils down into the vortex: before the rise the path is a sinking helix.</summary>
    private Vector2 Helix(float time)
    {
        var s = Math.Max(RiseStart - time, 0f);
        var theta = HelixStartAngle - s * HelixSpin;
        var r = HelixRadius * MathF.Exp(-s * 1.6f);
        var lift = HelixLift * MathF.Exp(-s * 2.2f);
        return _vortexCentre + new Vector2(MathF.Cos(theta) * r * _forward, -lift + MathF.Sin(theta) * r * 0.34f);
    }

    private Vector2 Path(float time)
    {
        if (time <= _keyTimes[0])
            return Helix(time);

        var n = _keyPoints.Length;
        var segment = n - 2;
        for (var i = 0; i < n - 1; i++)
        {
            if (time < _keyTimes[i + 1])
            {
                segment = i;
                break;
            }
        }

        var u = Math.Clamp(
            (time - _keyTimes[segment]) / Math.Max(_keyTimes[segment + 1] - _keyTimes[segment], 0.0001f),
            0f,
            1f);
        if (segment == 0)
            u = 1f - (1f - u) * (1f - u); // Decelerate into the held pose.
        else if (segment == 1)
            u *= 0.6f + 0.4f * u; // Ease into the dive without arriving at a blur.

        var p0 = segment == 0 ? Helix(_keyTimes[0] - 0.03f) : _keyPoints[segment - 1];
        var p1 = _keyPoints[segment];
        var p2 = _keyPoints[segment + 1];
        var p3 = segment + 2 < n ? _keyPoints[segment + 2] : p2 * 2f - p1;
        return CatmullRom(p0, p1, p2, p3, u);
    }

    /// <summary>
    /// The tail walks back along the flight by arc length, so it keeps a fixed
    /// length whatever the speed: coiled into the vortex while rising, trailing
    /// the bird as a wavy water ribbon once it sweeps.
    /// </summary>
    private Vector2[] TailPath(float time)
    {
        var detach = Mathf.SmoothStep(ApexAt, FirstReach, time);
        var length = Mathf.Lerp(TailLengthAttached, TailLength, detach);
        // The tail drains into the collapsing body rather than lingering.
        length *= 1f - 0.85f * Mathf.SmoothStep(_lastArrival, _lastArrival + TailDuration * 0.8f, time);
        var step = length / (TailPoints - 1);

        var points = new List<Vector2>(TailPoints);
        var previous = Path(time);
        points.Add(previous);
        var tau = time;
        var travelled = 0f;
        var nextMark = step;
        while (points.Count < TailPoints && tau > time - 3f)
        {
            tau -= 0.002f;
            var current = Path(tau);
            travelled += current.DistanceTo(previous);
            previous = current;
            if (travelled >= nextMark)
            {
                points.Add(current);
                nextMark += step;
            }
        }

        var wavy = new Vector2[points.Count];
        for (var i = 0; i < points.Count; i++)
        {
            var a = points[Math.Max(i - 1, 0)];
            var b = points[Math.Min(i + 1, points.Count - 1)];
            var normal = (b - a).Normalized().Orthogonal();
            var s = i * step;
            var envelope = Mathf.SmoothStep(0f, 120f, s) * detach;
            var wave = MathF.Sin(s / TailWavelength * Mathf.Tau - time * 22f) * TailWave * envelope;
            wavy[i] = points[i] + normal * wave;
        }
        return wavy;
    }

    // --- Ice ------------------------------------------------------------------

    /// <summary>
    /// A slim faceted ice shard: two flat facets split along its long axis and a
    /// short cut facet at the point, no ink. Empowered adds a gold glint on the
    /// upper edge only.
    /// </summary>
    private Crystal BuildCrystal(int enemy, int index)
    {
        var trailGradient = new Gradient();
        trailGradient.SetColor(0, TrailTail);
        trailGradient.SetColor(1, TrailHead);
        var trail = new Line2D
        {
            Name = $"AquaIceTrail{enemy}_{index}",
            Width = CrystalTrailWidth,
            Gradient = trailGradient,
            BeginCapMode = Line2D.LineCapMode.Round,
            EndCapMode = Line2D.LineCapMode.Round,
            Visible = false
        };
        _ice.AddChildSafely(trail);

        var node = new Node2D { Name = $"AquaIce{enemy}_{index}", Visible = false };
        node.AddChildSafely(new Polygon2D
        {
            Polygon = CrystalPoints(new(30, 0), new(14, -5), new(-16, -3), new(-24, 0)),
            Color = CrystalLight
        });
        node.AddChildSafely(new Polygon2D
        {
            Polygon = CrystalPoints(new(30, 0), new(-24, 0), new(-14, 4), new(12, 5)),
            Color = CrystalShade
        });
        node.AddChildSafely(new Polygon2D
        {
            Polygon = CrystalPoints(new(30, 0), new(14, -5), new(18, 0)),
            Color = Colors.White
        });

        Line2D? glint = null;
        if (_empowered)
        {
            glint = new Line2D
            {
                Name = "Glint",
                Points = CrystalPoints(new(4, -4.4f), new(24, -1.6f)),
                Width = 2.2f * CrystalScale,
                DefaultColor = CrystalGlint,
                BeginCapMode = Line2D.LineCapMode.Round,
                EndCapMode = Line2D.LineCapMode.Round
            };
            node.AddChildSafely(glint);
        }
        _ice.AddChildSafely(node);
        return new Crystal(node, trail, enemy, index, glint);
    }

    private static Vector2[] CrystalPoints(params Vector2[] points) =>
        points.Select(static point => point * CrystalScale).ToArray();

    // --- Release --------------------------------------------------------------

    /// <summary>
    /// Fades out once the bird's tail, every splash, the returning ice and the
    /// vortex close have played. This is the Release beat; the base
    /// <c>Dispose</c> it ends in is idempotent and also covers combat end, tree
    /// exit, exceptions, and the lifetime cap.
    /// </summary>
    private void FadeAndDispose()
    {
        if (_faded || !_bodyStarted || !IsActive())
        {
            Dispose();
            return;
        }

        _faded = true;
        var t = _t;
        if (float.IsNaN(_closeAt))
        {
            _closeAt = Math.Max(_lastArrival + CloseAfterTail, t);
            _closeLength = CloseWithoutReturn;
        }

        var end = _closeAt + _closeLength;
        for (var i = 0; i < _targets.Count; i++)
        {
            if (!float.IsNaN(_splashStart[i]))
                end = Math.Max(end, _splashStart[i] + SplashRise + SplashFall);
            if (!float.IsNaN(_iceStart[i]))
                end = Math.Max(end, _iceStart[i] + FreezeDuration + FreezeHold + ShatterDuration);
        }

        var remaining = Math.Max(ReleaseSeconds(t, _lastArrival), end - t);
        if (remaining <= 0f)
        {
            Dispose();
            return;
        }

        var fadeSeconds = Math.Min(FadeDuration, remaining);
        var fade = Track(Root.CreateTween());
        if (remaining > fadeSeconds)
            fade.TweenInterval(remaining - fadeSeconds);
        fade.TweenProperty(Root, "modulate:a", 0f, fadeSeconds);
        fade.TweenCallback(Callable.From(Dispose));
    }

    private static PackedScene LoadScene() =>
        PreloadManager.Cache.GetScene(ScenePath);

    private static void LogLoadFailure(Exception exception)
    {
        if (_loadFailureLogged)
            return;

        _loadFailureLogged = true;
        MainFile.Logger.Error(
            $"Could not create Aqua phoenix VFX from {ScenePath} and {ShaderPath}: {exception}");
    }

    // --- Curves ---------------------------------------------------------------

    private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float u)
    {
        var u2 = u * u;
        var u3 = u2 * u;
        return 0.5f * (2f * p1 + (-p0 + p2) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2
            + (-p0 + 3f * p1 - 3f * p2 + p3) * u3);
    }

    private static Vector2 Bezier(Vector2 a, Vector2 control, Vector2 b, float t) =>
        a.Lerp(control, t).Lerp(control.Lerp(b, t), t);

    private static Vector2 BezierTangent(Vector2 a, Vector2 control, Vector2 b, float t) =>
        2f * (1f - t) * (control - a) + 2f * t * (b - control);

    private static float EaseOut(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return 1f - (1f - t) * (1f - t) * (1f - t);
    }

    private static float EaseInOut(float t) => -(MathF.Cos(MathF.PI * Math.Clamp(t, 0f, 1f)) - 1f) * 0.5f;

    private static float EaseBack(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        const float c = 1.70158f;
        return 1f + (c + 1f) * MathF.Pow(t - 1f, 3f) + c * MathF.Pow(t - 1f, 2f);
    }
}
