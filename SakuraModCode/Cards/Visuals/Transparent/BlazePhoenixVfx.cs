using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
/// The Clear card Blaze as its own firebird: the exhausted cards fly out of the
/// exhaust pile as fuel, the bird forms above Sakura with a strength that tracks
/// the pile, dives onto the target, flings its wings open on the hit, and the
/// exhaust pile burns to ash where its button was when it leaves combat.
/// </summary>
/// <remarks>
/// Presentation only. The card owns selection, Exhaust, damage and the removal
/// from combat; it passes the fuel in and calls <see cref="Cues.Strike"/> right
/// before the attack and <see cref="Cues.AshAsync"/> around the removal. Timing
/// and layout are kept in step with <c>scripts/render_blaze_vfx.gd</c>.
/// </remarks>
internal sealed class BlazePhoenixVfx : CelVfxSession
{
    internal const string ScenePath =
        MainFile.ResPath + "/scenes/combat/card_vfx/blaze_phoenix_vfx.tscn";
    internal const string ShaderPath =
        MainFile.ResPath + "/shaders/card_vfx/blaze_phoenix.gdshader";
    internal static IReadOnlyList<string> AssetPaths { get; } = [ScenePath];

    /// <summary>Vanilla's shared fire hit (<c>FmodSfx.fire</c>), as Fiend Fire plays per hit.</summary>
    internal const string StrikeSfx = "event:/sfx/characters/attack_fire";

    // Beat timeline after the shared prelude (design D3/D4).
    internal const float FuelDuration = 0.35f;
    internal const float FormDuration = 0.25f;
    internal const float DiveDuration = 0.25f;
    internal const float TailDuration = 0.45f;
    internal const float AshDuration = 0.75f;
    private const float StreamFlight = 0.22f;
    private const float StreamFadeShare = 0.25f;
    private const float StreamTrailShare = 0.32f;
    private const float MaxStreamStagger = 0.05f;
    private const float BirdBurnDuration = 0.25f;
    private const float BirdHideAfter = 0.28f;
    private const float FlareFormIn = 0.06f;
    private const float FlareOpen = 0.18f;
    private const float FlareBurn = 0.40f;
    private const float FlareLift = 80f;
    private const float FadeDuration = 0.06f;
    internal const int MaxFuelStreams = 6;
    internal const int StrengthSaturation = 10;
    internal const float MinBirdScale = 0.85f;
    internal const float MaxBirdScale = 1.20f;
    private const int EmberCount = 8;
    private const float EmberGravity = 700f;
    private const int AshCount = 16;
    private const int UiLayerZIndex = 3000;

    // Layout, in screen pixels.
    private const float GatherAboveHead = 110f;
    private const float GatherForward = 50f;
    private const float GatherMinTop = 200f;
    private const float DiveArcHeight = 170f;
    private const float FlareRaise = 0.10f;
    private const float RestPitch = -0.12f;

    private static readonly Color StreamHot = new(1f, 0.85f, 0.45f, 1f);
    private static readonly Color StreamMid = new(1f, 0.40f, 0.06f, 0.7f);
    private static readonly Color StreamCool = new(0.85f, 0.12f, 0.04f, 0f);
    private static readonly Color CardFace = new(1f, 0.93f, 0.74f);
    private static readonly Color CardRim = new(1f, 0.45f, 0.08f);
    private static readonly Vector2 CardHalfSize = new(15f, 21f);

    private static bool _loadFailureLogged;

    private readonly ShaderMaterial _birdMaterial;
    private readonly ShaderMaterial _strikeMaterial;
    private readonly Node2D _bird;
    private readonly Node2D _strike;
    private readonly Node2D _strikeEmbers;
    private readonly Node2D _fuel;
    private readonly Node2D _ash;
    private readonly Vector2 _gather;
    private readonly Vector2 _target;
    private readonly Vector2 _diveControl;
    private readonly Vector2? _pileAnchor;
    private readonly float _birdScale;
    private readonly float _facing;
    private readonly List<(Line2D Ribbon, Node2D Head)> _streams = [];
    private Sprite2D? _gatherGlow;
    private bool _struck;
    private bool _faded;
    private float _strikeAt;
    private float _ashEndsAt = float.NegativeInfinity;

    internal readonly record struct Fuel(int ExhaustCount, bool Empowered);

    private BlazePhoenixVfx(
        Node2D root,
        NCombatRoom room,
        Fuel fuel,
        Vector2 gather,
        Vector2 target,
        Vector2? pileAnchor)
        : base(root, room)
    {
        _bird = root.GetNode<Node2D>("%Bird");
        _strike = root.GetNode<Node2D>("%Strike");
        _strikeEmbers = root.GetNode<Node2D>("%StrikeEmbers");
        _fuel = root.GetNode<Node2D>("%Fuel");
        _ash = root.GetNode<Node2D>("%Ash");
        _birdMaterial = CelVfxGeometry.DuplicateMaterial(root.GetNode<ColorRect>("%BirdBody"), "phoenix body");
        _strikeMaterial = CelVfxGeometry.DuplicateMaterial(root.GetNode<ColorRect>("%StrikeFlare"), "phoenix strike");

        _gather = gather;
        _target = target;
        _diveControl = new Vector2((gather.X + target.X) * 0.5f, Math.Min(gather.Y, target.Y) - DiveArcHeight);
        _pileAnchor = pileAnchor;
        _birdScale = BirdScale(fuel.ExhaustCount);
        _facing = target.X >= gather.X ? 1f : -1f;

        var seed = (float)Random.Shared.NextDouble() * 6.1f;
        foreach (var material in new[] { _birdMaterial, _strikeMaterial })
        {
            material.SetShaderParameter("seed", seed);
            material.SetShaderParameter("strength", Strength(fuel.ExhaustCount));
            material.SetShaderParameter("empowered", fuel.Empowered ? 1f : 0f);
            material.SetShaderParameter("form", 0f);
            material.SetShaderParameter("spread", 0f);
            material.SetShaderParameter("dissolve", 0f);
            material.SetShaderParameter("opacity", 0f);
        }

        // The pile-anchored beats sit in UI space and must draw above the combat UI.
        foreach (var layer in new[] { _fuel, _ash })
        {
            layer.ZAsRelative = false;
            layer.ZIndex = UiLayerZIndex;
        }

        if (pileAnchor is not null)
            BuildFuel(FuelStreamCount(fuel.ExhaustCount));
    }

    protected override IEnumerable<ShaderMaterial> Materials => [_birdMaterial, _strikeMaterial];

    // Covers the prelude, the fuel/form/dive chain, slow gameplay and the ash tail.
    protected override float MaximumLifetime => 8.0f;

    private float Elapsed => _birdMaterial.GetShaderParameter("elapsed").AsSingle();

    // --- Pure timing and strength ---------------------------------------------

    /// <summary>Fuel streams drawn for an exhaust count: one per card, capped.</summary>
    internal static int FuelStreamCount(int exhaustCount) => Math.Clamp(exhaustCount, 0, MaxFuelStreams);

    /// <summary>Fuel strength in [0, 1], easing out and saturating at <see cref="StrengthSaturation"/>.</summary>
    internal static float Strength(int exhaustCount)
    {
        var t = Math.Clamp(exhaustCount / (float)StrengthSaturation, 0f, 1f);
        return 1f - (1f - t) * (1f - t);
    }

    internal static float BirdScale(int exhaustCount) =>
        MinBirdScale + (MaxBirdScale - MinBirdScale) * Strength(exhaustCount);

    /// <summary>The fuel beat is skipped outright when there is nothing to burn.</summary>
    internal static float FuelSeconds(int exhaustCount) => exhaustCount > 0 ? FuelDuration : 0f;

    /// <summary>Body length after the shared prelude, excluding the ash that overlaps the tail.</summary>
    internal static float BodySeconds(int exhaustCount) =>
        FuelSeconds(exhaustCount) + FormDuration + DiveDuration + TailDuration;

    /// <summary>When stream <paramref name="index"/> leaves the pile, inside the fixed fuel window.</summary>
    internal static float StreamStart(int index, int streams) =>
        streams <= 1 ? 0f : index * Math.Min(MaxStreamStagger, (FuelDuration - StreamFlight) / (streams - 1));

    /// <summary>Only the unplayed part of the strike's tail survives gameplay resolution.</summary>
    internal static float ReleaseSeconds(float elapsed, float strikeAt) => Math.Clamp(
        TailDuration - Math.Max(0f, elapsed - strikeAt),
        0f,
        TailDuration);

    // --- Orchestration --------------------------------------------------------

    internal static Task PlayOrResolveAsync(
        CardModel card,
        Creature? caster,
        Creature target,
        Fuel fuel,
        Func<Cues, Task> resolveGameplay)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(resolveGameplay);

        return CelVfxSession.PlayOrResolveAsync(
            "Blaze phoenix",
            () => TryCreate(card, caster, target, fuel),
            session => session.PlayPrelude(card, caster),
            scope => resolveGameplay(new Cues(scope)),
            session => session.FadeAndDispose(),
            session => session.Dispose());
    }

    internal sealed class Cues(CueScope<BlazePhoenixVfx> scope)
    {
        /// <summary>The bird lands on the target. Call immediately before the attack.</summary>
        internal void Strike() => scope.Invoke("strike", static session => session.Strike());

        /// <summary>
        /// Runs the authoritative removal exactly once, live presentation or not,
        /// and burns the exhaust pile to ash where its button was.
        /// </summary>
        /// <remarks>
        /// The button position is read before the removal: the empty-pile patch
        /// hides the button as soon as the pile empties. Removal is awaited outside
        /// the cue scope, so a dead or disabled session can never skip or repeat it,
        /// and its exceptions stay gameplay exceptions.
        /// </remarks>
        internal async Task AshAsync(Func<Task> removeFromCombat)
        {
            ArgumentNullException.ThrowIfNull(removeFromCombat);
            Vector2? anchor = null;
            scope.Invoke("ash anchor", session => anchor = session.LivePileAnchor());
            await removeFromCombat();
            if (anchor is { } position)
                scope.Invoke("ash", session => session.PlayAsh(position));
        }
    }

    private static BlazePhoenixVfx? TryCreate(CardModel card, Creature? caster, Creature target, Fuel fuel)
    {
        if (caster is null
            || !TryPrepare("Blaze phoenix", LoadScene, out var room, out _, out var scene))
        {
            return null;
        }

        Node2D? root = null;
        try
        {
            if (CelVfxGeometry.ResolveCaster(room.GetCreatureNode(caster)) is not { } casterAnchor)
                return null;

            var viewport = room.CombatVfxContainer.GetViewportRect();
            var targetGeometry = CelVfxGeometry.Resolve(room, target, 0, Budget);
            var targetPoint = targetGeometry.Center - new Vector2(0f, targetGeometry.Size.Y * FlareRaise);
            var forward = targetPoint.X >= casterAnchor.BodyCenter.X ? 1f : -1f;
            var headTop = casterAnchor.BodyCenter.Y - casterAnchor.BodySize.Y * 0.5f;
            var gather = new Vector2(
                casterAnchor.BodyCenter.X + forward * GatherForward,
                Math.Max(viewport.Position.Y + GatherMinTop, headTop - GatherAboveHead));

            // Pile-anchored beats belong to the local player's own exhaust pile.
            Vector2? pileAnchor = null;
            if (fuel.ExhaustCount > 0
                && card.Owner is { } owner
                && LocalContext.IsMe(owner)
                && PileExchangeVfx.TryGetPileCenter(room, PileType.Exhaust, out var pileCenter))
            {
                pileAnchor = pileCenter;
            }

            root = scene.Instantiate<Node2D>();
            root.Name = "SakuraBlazePhoenixVfx";
            root.ZAsRelative = true;
            root.ZIndex = 0;
            room.CombatVfxContainer.AddChildSafely(root);
            root.GlobalPosition = Vector2.Zero;

            var session = new BlazePhoenixVfx(root, room, fuel, gather, targetPoint, pileAnchor);
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

    private static CelVfxGeometry.GeometryBudget Budget => new(
        HorizontalPadding: 0f,
        VerticalPadding: 0f,
        MinWidth: 100f,
        MinHeight: 140f,
        MaxWidth: 360f,
        MaxHeight: 480f,
        FallbackWidth: 200f,
        FallbackHeight: 300f,
        FloorClearance: 0f);

    /// <summary>
    /// Shared wand prelude, then fuel, form and dive. Each beat is its own tracked
    /// Tween, fully configured before the await that lets it start; appending to a
    /// Tween after it has run would throw inside the awaited card action.
    /// </summary>
    private async Task<bool> PlayPrelude(CardModel card, Creature? caster)
    {
        if (!await PlayCelPrelude(card, caster))
            return false;

        if (_streams.Count > 0)
        {
            var fuelTween = Track(Root.CreateTween());
            fuelTween.TweenMethod(Callable.From<float>(UpdateFuel), 0f, FuelDuration, FuelDuration);
            if (!await WaitActive(FuelDuration))
                return false;
        }

        ShowBird();
        var form = Track(Root.CreateTween());
        form.TweenMethod(Callable.From<float>(UpdateForm), 0f, 1f, FormDuration);
        if (!await WaitActive(FormDuration))
            return false;
        ReleaseFuel();

        var dive = Track(Root.CreateTween());
        dive.TweenMethod(Callable.From<float>(UpdateDive), 0f, 1f, DiveDuration);
        return await WaitActive(DiveDuration);
    }

    // --- Fuel -----------------------------------------------------------------

    private void BuildFuel(int count)
    {
        var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
        var glowTexture = RadialTexture(new Color(1f, 0.62f, 0.20f, 0.9f), new Color(1f, 0.25f, 0.02f, 0f));
        for (var index = 0; index < count; index++)
        {
            var ribbon = new Line2D
            {
                Name = $"FuelRibbon{index}",
                Width = 16f,
                WidthCurve = RibbonWidth(),
                Gradient = RibbonGradient(),
                JointMode = Line2D.LineJointMode.Round,
                BeginCapMode = Line2D.LineCapMode.Round,
                EndCapMode = Line2D.LineCapMode.Round,
                Antialiased = true,
                Material = additive,
                Visible = false
            };
            _fuel.AddChildSafely(ribbon);

            var head = new Node2D { Name = $"FuelCard{index}", Visible = false };
            head.AddChildSafely(new Sprite2D { Texture = glowTexture, Material = additive, Scale = Vector2.One * 0.85f });
            head.AddChildSafely(new Polygon2D { Polygon = CardCorners(), Color = CardFace });
            var rim = new Line2D { Width = 5f, DefaultColor = CardRim, Closed = true };
            rim.Points = CardCorners();
            head.AddChildSafely(rim);
            _fuel.AddChildSafely(head);

            _streams.Add((ribbon, head));
        }

        _gatherGlow = new Sprite2D
        {
            Name = "FuelGather",
            Texture = RadialTexture(new Color(1f, 0.85f, 0.5f, 1f), new Color(1f, 0.35f, 0.05f, 0f)),
            Material = additive,
            Visible = false
        };
        _fuel.AddChildSafely(_gatherGlow);
        _gatherGlow.GlobalPosition = _gather;
    }

    /// <summary>A gentle bow straight from the pile toward the gather point.</summary>
    private static Vector2 StreamControl(Vector2 from, Vector2 to, int index)
    {
        var normal = (to - from).Normalized().Orthogonal();
        if (normal.Y > 0f)
            normal = -normal;
        return (from + to) * 0.5f + normal * (70f + index % 3 * 45f);
    }

    /// <summary>
    /// The exhaust pile button's current centre, or the create-time capture when
    /// it cannot be resolved. Null when the pile-anchored beats are skipped.
    /// </summary>
    /// <remarks>
    /// Re-read rather than cached: when Blaze exhausts into an empty pile, the
    /// native button is still sliding in (150px, 0.5s) when the session is created.
    /// </remarks>
    private Vector2? LivePileAnchor()
    {
        if (_pileAnchor is not { } captured)
            return null;
        return PileExchangeVfx.TryGetPileCenter(Room, PileType.Exhaust, out var center) ? center : captured;
    }

    private void UpdateFuel(float time)
    {
        if (LivePileAnchor() is not { } anchor)
            return;

        var arrived = 0;
        for (var index = 0; index < _streams.Count; index++)
        {
            var (ribbon, head) = _streams[index];
            var control = StreamControl(anchor, _gather, index);
            var u = (time - StreamStart(index, _streams.Count)) / StreamFlight;
            if (u >= 1f)
                arrived++;
            if (u <= 0f || u >= 1f + StreamFadeShare)
            {
                ribbon.Visible = false;
                head.Visible = false;
                continue;
            }

            var headU = Math.Min(u, 1f);
            var tailU = Math.Max(0f, u - StreamTrailShare);
            var points = new Vector2[12];
            for (var k = 0; k < points.Length; k++)
            {
                var s = Mathf.Lerp(tailU, headU, k / (float)(points.Length - 1));
                points[k] = Bezier(anchor, control, _gather, EaseInOut(s));
            }
            ribbon.Points = points;
            ribbon.Visible = true;
            ribbon.Modulate = new Color(1f, 1f, 1f, 1f - Math.Clamp((u - 1f) / StreamFadeShare, 0f, 1f));

            head.Visible = u < 1f;
            head.GlobalPosition = Bezier(anchor, control, _gather, EaseInOut(headU));
            head.Rotation = BezierTangent(anchor, control, _gather, EaseInOut(headU)).Angle() + Mathf.Pi * 0.5f;
            head.Scale = Vector2.One * Mathf.Lerp(1f, 0.35f, headU);
            head.Modulate = Colors.White.Lerp(new Color(1f, 0.55f, 0.15f, 0.6f), Mathf.SmoothStep(0.4f, 1f, headU));
        }

        if (_gatherGlow is { } glow)
        {
            var arrivals = arrived / (float)Math.Max(1, _streams.Count);
            glow.Visible = true;
            glow.Scale = Vector2.One * Mathf.Lerp(0.6f, 1.8f, arrivals);
            glow.Modulate = new Color(1f, 1f, 1f, 0.6f + 0.4f * arrivals);
        }
    }

    private void ReleaseFuel()
    {
        foreach (var child in _fuel.GetChildren())
        {
            if (child is Node node && GodotObject.IsInstanceValid(node))
                node.QueueFreeSafely();
        }
        _streams.Clear();
        _gatherGlow = null;
    }

    // --- Form and dive --------------------------------------------------------

    private void ShowBird()
    {
        _bird.GlobalPosition = _gather;
        _bird.Rotation = RestPitch * _facing;
        _bird.Scale = new Vector2(_facing, 1f) * _birdScale * 0.6f;
        _birdMaterial.SetShaderParameter("opacity", 1f);
        _bird.Visible = true;
    }

    private void UpdateForm(float u)
    {
        var form = EaseOut(u);
        _birdMaterial.SetShaderParameter("form", form);
        _birdMaterial.SetShaderParameter("spread", EaseBack(u));
        _bird.GlobalPosition = _gather + new Vector2(0f, -8f * form);
        _bird.Scale = new Vector2(_facing, 1f) * _birdScale * Mathf.Lerp(0.6f, 1f, form);
        // The gathered fuel swells into the forming body and fades under it.
        if (_gatherGlow is { } glow && GodotObject.IsInstanceValid(glow))
        {
            glow.Scale = Vector2.One * 1.8f * (1f + 0.6f * u);
            glow.Modulate = new Color(1f, 1f, 1f, 1f - 0.9f * u);
        }
    }

    private void UpdateDive(float u)
    {
        var path = EaseInOut(u);
        _birdMaterial.SetShaderParameter("spread", Mathf.Lerp(1f, 0.55f, path));
        _bird.GlobalPosition = Bezier(_gather, _diveControl, _target, path);
        var tangent = BezierTangent(_gather, _diveControl, _target, path);
        // With a mirrored body, local +X points along -X, so the heading turns by pi.
        var heading = tangent.Angle() - (_facing < 0f ? Mathf.Pi : 0f);
        _bird.Rotation = Mathf.LerpAngle(RestPitch * _facing, heading, Mathf.SmoothStep(0f, 0.35f, u));
        var speed = Mathf.Sin(Mathf.Pi * u);
        _bird.Scale = new Vector2(_facing * (1f + 0.16f * speed), 1f - 0.10f * speed) * _birdScale;
    }

    // --- Strike ---------------------------------------------------------------

    /// <summary>
    /// The bird burns away where it hit and a front-on flare flings its wings open
    /// over the target. Damage lands on this frame.
    /// </summary>
    /// <remarks>
    /// No <c>Creature</c> parameter: Blaze has one target, already resolved into
    /// this session's geometry, so an argument could only disagree with it.
    /// </remarks>
    private void Strike()
    {
        if (_struck || _faded || !IsActive())
            return;

        _struck = true;
        _strikeAt = Elapsed;
        SfxCmd.Play(StrikeSfx);
        _bird.GlobalPosition = _target;
        _strike.GlobalPosition = _target;
        _strike.Scale = Vector2.One * _birdScale * 0.75f;
        _strikeMaterial.SetShaderParameter("opacity", 1f);
        _strike.Visible = true;

        var tween = Track(Root.CreateTween().SetParallel());
        tween.TweenMethod(Callable.From<float>(UpdateStrike), 0f, TailDuration, TailDuration);
        for (var i = 0; i < EmberCount; i++)
        {
            var side = -1f + 2f * i / (EmberCount - 1);
            var velocity = new Vector2(side * 330f, -260f - i % 3 * 70f);
            var ember = CelVfxGeometry.AddBallisticDebris(
                tween,
                _strikeEmbers,
                EmberPoints(2.4f + i % 3 * 0.8f),
                i % 3 == 0 ? new Color(1f, 0.92f, 0.6f) : new Color(1f, 0.5f, 0.12f),
                _target,
                velocity,
                TailDuration,
                0f,
                EmberGravity,
                -0.6f + i * 0.2f,
                "BlazeEmber",
                zIndex: 1);
            ember.ZAsRelative = true;
        }
    }

    private void UpdateStrike(float age)
    {
        _birdMaterial.SetShaderParameter("dissolve", Math.Clamp(age / BirdBurnDuration, 0f, 1f));
        _bird.Visible = age < BirdHideAfter;

        var open = Math.Clamp(age / FlareOpen, 0f, 1f);
        var burn = Math.Clamp((age - FlareFormIn) / FlareBurn, 0f, 1f);
        _strikeMaterial.SetShaderParameter("form", EaseOut(Math.Clamp(age / FlareFormIn, 0f, 1f)));
        _strikeMaterial.SetShaderParameter("spread", EaseBack(open));
        _strikeMaterial.SetShaderParameter("dissolve", burn);
        // The flare lifts away as it burns: fire rises.
        _strike.GlobalPosition = _target + new Vector2(0f, -FlareLift * burn);
        _strike.Scale = Vector2.One * _birdScale * Mathf.Lerp(0.75f, 1f, EaseOut(open));
    }

    // --- Ash ------------------------------------------------------------------

    private void PlayAsh(Vector2 anchor)
    {
        if (_faded || !IsActive())
            return;

        _ashEndsAt = Elapsed + AshDuration;
        var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
        var flash = new Sprite2D
        {
            Name = "AshFlash",
            Texture = RadialTexture(new Color(1f, 0.72f, 0.35f, 1f), new Color(1f, 0.30f, 0.04f, 0f)),
            Material = additive
        };
        _ash.AddChildSafely(flash);
        flash.GlobalPosition = anchor;

        var flakes = new Polygon2D[AshCount];
        for (var i = 0; i < AshCount; i++)
        {
            flakes[i] = new Polygon2D { Name = $"Ash{i}", Polygon = FlakePoints(8f + i % 4 * 2f) };
            _ash.AddChildSafely(flakes[i]);
            flakes[i].GlobalPosition = anchor;
        }

        var tween = Track(Root.CreateTween());
        tween.TweenMethod(
            Callable.From<float>(age => UpdateAsh(anchor, flash, flakes, age)),
            0f,
            AshDuration,
            AshDuration);
    }

    private static void UpdateAsh(Vector2 anchor, Sprite2D flash, Polygon2D[] flakes, float age)
    {
        if (GodotObject.IsInstanceValid(flash))
        {
            var flashU = Math.Clamp(age / 0.30f, 0f, 1f);
            flash.Visible = flashU < 1f;
            flash.Scale = Vector2.One * Mathf.Lerp(0.5f, 1.3f, EaseOut(flashU));
            flash.Modulate = new Color(1f, 1f, 1f, 1f - flashU);
        }

        var u = Math.Clamp(age / AshDuration, 0f, 1f);
        for (var i = 0; i < flakes.Length; i++)
        {
            var flake = flakes[i];
            if (!GodotObject.IsInstanceValid(flake))
                continue;

            // Burst out of the button, then drift upward and sway as they cool.
            var angle = -Mathf.Pi * 0.55f + (i / (float)(flakes.Length - 1) - 0.5f) * 1.6f;
            var speed = 110f + i % 4 * 30f;
            var burst = 1f - Mathf.Pow(1f - u, 3f);
            var drift = Mathf.Sin(u * 6f + i * 1.7f) * 16f * u;
            flake.GlobalPosition = anchor + Vector2.FromAngle(angle) * speed * burst + new Vector2(drift, -90f * u);
            flake.Rotation = u * (2f + i % 3) * (i % 2 == 0 ? 1f : -1f);
            // Half stay glowing embers, half cool into grey ash.
            var ember = i % 2 == 0 ? new Color(1f, 0.80f, 0.40f) : new Color(1f, 0.50f, 0.15f);
            var ash = i % 2 == 0 ? new Color(1f, 0.45f, 0.12f) : new Color(0.58f, 0.54f, 0.52f);
            var color = ember.Lerp(ash, Mathf.SmoothStep(0.1f, 0.6f, u));
            color.A = 1f - Mathf.SmoothStep(0.45f, 1f, u);
            flake.Color = color;
        }
    }

    // --- Release --------------------------------------------------------------

    /// <summary>
    /// Fades out once the strike tail and any ash have finished. This is the
    /// Release beat; the base <c>Dispose</c> it ends in is idempotent and also
    /// covers combat end, tree exit, exceptions, and the lifetime cap.
    /// </summary>
    private void FadeAndDispose()
    {
        if (_faded || !IsActive())
        {
            Dispose();
            return;
        }

        _faded = true;
        var elapsed = Elapsed;
        var remaining = _struck ? ReleaseSeconds(elapsed, _strikeAt) : FadeDuration;
        remaining = Math.Max(remaining, _ashEndsAt - elapsed);
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
            $"Could not create Blaze phoenix VFX from {ScenePath} and {ShaderPath}: {exception}");
    }

    // --- Shapes and curves ----------------------------------------------------

    private static Vector2 Bezier(Vector2 a, Vector2 control, Vector2 b, float t) =>
        a.Lerp(control, t).Lerp(control.Lerp(b, t), t);

    private static Vector2 BezierTangent(Vector2 a, Vector2 control, Vector2 b, float t) =>
        2f * (1f - t) * (control - a) + 2f * t * (b - control);

    private static float EaseOut(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return 1f - (1f - t) * (1f - t) * (1f - t);
    }

    private static float EaseInOut(float t) => -(Mathf.Cos(Mathf.Pi * Math.Clamp(t, 0f, 1f)) - 1f) * 0.5f;

    private static float EaseBack(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        const float c = 1.70158f;
        return 1f + (c + 1f) * Mathf.Pow(t - 1f, 3f) + c * Mathf.Pow(t - 1f, 2f);
    }

    private static GradientTexture2D RadialTexture(Color inner, Color outer)
    {
        var gradient = new Gradient();
        gradient.SetColor(0, inner);
        gradient.SetColor(1, outer);
        return new GradientTexture2D
        {
            Gradient = gradient,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(0.5f, 0f),
            Width = 128,
            Height = 128
        };
    }

    private static Curve RibbonWidth()
    {
        var curve = new Curve();
        curve.AddPoint(new Vector2(0f, 0f));
        curve.AddPoint(new Vector2(1f, 1f));
        return curve;
    }

    private static Gradient RibbonGradient()
    {
        var gradient = new Gradient();
        gradient.SetColor(0, StreamCool);
        gradient.SetColor(1, StreamHot);
        gradient.AddPoint(0.6f, StreamMid);
        return gradient;
    }

    private static Vector2[] CardCorners() =>
    [
        new(-CardHalfSize.X, -CardHalfSize.Y),
        new(CardHalfSize.X, -CardHalfSize.Y),
        new(CardHalfSize.X, CardHalfSize.Y),
        new(-CardHalfSize.X, CardHalfSize.Y)
    ];

    /// <summary>A small curled feather-ash flake.</summary>
    private static Vector2[] FlakePoints(float size) =>
    [
        new(-size, -size * 0.3f),
        new(0f, -size * 0.55f),
        new(size, -size * 0.1f),
        new(size * 0.4f, size * 0.5f),
        new(-size * 0.6f, size * 0.35f)
    ];

    /// <summary>A tapered ember flake, not the angular shard Hail throws.</summary>
    private static Vector2[] EmberPoints(float radius) =>
    [
        new(0f, -radius * 1.8f),
        new(radius * 0.8f, 0f),
        new(0f, radius * 1.1f),
        new(-radius * 0.8f, 0f)
    ];
}
