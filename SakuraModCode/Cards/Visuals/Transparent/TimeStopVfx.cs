using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using SakuraMod.SakuraModCode.Powers;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
/// The Clear card Time stops the world: a bezel front spreads out from Sakura and
/// greys everything it passes, a clock face stays behind her, its second hand
/// sweeps once and stops with a clack, petals hang mid-air. Only the Time
/// player's own creature and the bottom UI keep their colour. The freeze holds
/// through the turn switch and colour flows back out from Sakura when the extra
/// turn starts. With the extra effect, gold hands fly from the dial and stamp a
/// time seal onto every preserved thing (Block, energy, hand, element states).
/// </summary>
/// <remarks>
/// <para>
/// Presentation only. <c>Time.PlayCard</c> applies the power and preservation
/// first, awaits <see cref="Cues.StopAsync"/> (the clack), then ends the turn.
/// The cue is a no-op when presentation is off or failed, so gameplay and its
/// timing are then identical to having no effect.
/// </para>
/// <para>
/// The grey is a two-layer screen partition: the under-layer is the first child
/// of the Time owner's <c>NCreature</c> and grades only inside her rect (the
/// background behind her); the over-layer is the first child of
/// <c>CombatVfxContainer</c> and grades only outside it. Every pixel is graded
/// once, so her own pixels stay in colour with no halo. Seals live in an overlay
/// under <c>room.Ui</c>. No native node is reparented, reordered or tinted; every
/// target is read each frame.
/// </para>
/// <para>
/// Beat constants and geometry are kept in step with
/// <c>scripts/render_time_stop_vfx.gd</c>, the approved offline plate.
/// </para>
/// </remarks>
internal sealed class TimeStopVfx : CelVfxSession
{
    internal const string ScenePath =
        MainFile.ResPath + "/scenes/combat/card_vfx/time_stop_vfx.tscn";
    internal const string ShaderPath =
        MainFile.ResPath + "/shaders/card_vfx/time_stop.gdshader";
    internal static IReadOnlyList<string> AssetPaths { get; } = [ScenePath];

    internal const string UnderRootName = "SakuraTimeStopUnder";
    internal const string OverRootName = "SakuraTimeStopVfx";
    internal const string SealRootName = "SakuraTimeStopSeals";

    // --- Beats, in seconds after the card resolves (plate constants) ---------
    /// <summary>The shared standard prelude's blocking lead, before the body clock starts.</summary>
    internal const float SharedPreludeLead = 0.18f;
    internal const float WaveStart = 0.18f;
    internal const float WaveEnd = 0.42f;
    internal const float DialRevealStart = 0.20f;
    internal const float DialRevealEnd = 0.36f;
    internal const float HandGrow = 0.38f;
    internal const float HandSweepStart = 0.42f;
    /// <summary>The clack: <see cref="Cues.StopAsync"/> returns here and the turn ends.</summary>
    internal const float Clack = 0.60f;
    private const int FlashFrames = 1;
    private const int FocusFrames = 2;
    private const int SquashFrames = 1;
    private const float Burst = 0.26f;
    private const float Pulse = 0.28f;
    private const float Settle = 0.20f;
    private const float DialHoldOpacity = 0.90f;
    private const float HandHoldOpacity = 0.95f;
    internal const float ReleaseDuration = 0.35f;
    private const float PinStagger = 0.05f;
    private const float PinFlight = 0.16f;
    private const int SealStampFrames = 2;
    private const float SealRipple = 0.30f;
    private const float GlazeIn = 0.12f;
    private const float SheenCycle = 1.6f;
    private const float SheenCross = 0.6f;
    private const float PetalThaw = 0.10f;
    /// <summary>
    /// Body seconds the freeze may hold without a turn start (a long co-op wait)
    /// before it releases on its own.
    /// </summary>
    internal const float HoldCap = 10f;

    // --- Geometry (plate values; the dial scales with Sakura's body) ---------
    private const float WaveAspect = 0.80f;
    private const float CoverMargin = 90f;
    private const float DialRadius = 340f;
    private const float DialReferenceHeight = 360f;
    private const float DialAspect = 0.86f;
    private static readonly float HourAngle = Mathf.DegToRad(304f - 90f) - Mathf.Tau;
    private static readonly float MinuteAngle = Mathf.DegToRad(48f - 90f);
    private const float HandFrom = -Mathf.Pi * 0.5f;
    // Sixteen seconds past: just below three o'clock, toward enemies on the right;
    // forty-three past when she faces left.
    private static readonly float StopRight = Mathf.DegToRad(100f);
    private static readonly float StopLeft = Mathf.DegToRad(260f);
    private const float HandRecoil = 0.05f;
    private const float HandTick = Mathf.Tau / 60f;
    private const float SmearScale = 0.022f;
    private const float HolePad = 16f;
    private const float HoleCorner = 18f;
    private const int SealZIndex = 3000;
    private const float GlazePad = 24f;
    private const float PinTipOffset = 31f;
    private const float SealDesignRadius = 24f;
    private const float SealRectHalf = 64f;

    private static bool _loadFailureLogged;
    private static bool _missingTargetLogged;
    private static TimeStopVfx? _current;

    private readonly Creature _caster;
    private readonly NCreature _casterNode;
    private readonly bool _empowered;
    private readonly Node2D _under;
    private readonly ColorRect _underRect;
    private readonly ColorRect _overRect;
    private readonly ColorRect _petals;
    private readonly Node2D _sealRoot;
    private readonly Node2D _template;
    private readonly ShaderMaterial _underMaterial;
    private readonly ShaderMaterial _overMaterial;
    private readonly ShaderMaterial _petalMaterial;
    private readonly List<CanvasItem> _standeeParts;
    private readonly List<Seal> _seals = [];

    private float _t = WaveStart;
    private bool _bodyStarted;
    private bool _sealsLaunched;
    private bool _releaseArmed;
    private float _releaseAt = float.NaN;
    private bool _extrasReleased;
    private float _handStop;
    private Vector2 _lastCentre;
    private float _lastDialRadius = DialRadius;

    private sealed class Seal
    {
        internal required string Kind { get; init; }
        internal required Node2D Pin { get; init; }
        internal required Node2D Hand { get; init; }
        internal required ShaderMaterial HandMaterial { get; init; }
        internal required Line2D Trail { get; init; }
        internal required ColorRect Body { get; init; }
        internal required ShaderMaterial SealMaterial { get; init; }
        internal required float Launch { get; init; }
        internal required List<Glaze> Glazes { get; init; }
        internal Control? Target { get; init; }
        internal Vector2 Centre { get; set; }
        internal bool Lost { get; set; }
    }

    private sealed record Glaze(Control Target, Node2D Anchor, ColorRect Rect, ShaderMaterial Material, int Index);

    private TimeStopVfx(
        Node2D overRoot,
        Node2D underRoot,
        Node2D sealRoot,
        NCombatRoom room,
        Creature caster,
        NCreature casterNode,
        bool empowered)
        : base(overRoot, room)
    {
        _caster = caster;
        _casterNode = casterNode;
        _empowered = empowered;
        _under = underRoot;
        _sealRoot = sealRoot;
        _underRect = underRoot.GetNode<ColorRect>("Grade");
        _overRect = overRoot.GetNode<ColorRect>("OverGrade/Grade");
        _petals = overRoot.GetNode<ColorRect>("Petals");
        _template = sealRoot.GetNode<Node2D>("Pins/PinTemplate");
        _underMaterial = CelVfxGeometry.DuplicateMaterial(_underRect, "time stop under-grade");
        _overMaterial = CelVfxGeometry.DuplicateMaterial(_overRect, "time stop over-grade");
        _petalMaterial = CelVfxGeometry.DuplicateMaterial(_petals, "time stop petals");
        _standeeParts = CollectStandeeParts(casterNode);

        var facing = CelVfxGeometry.ResolveCaster(casterNode)?.FacingSign ?? 1f;
        _handStop = HandFrom + Mathf.Tau + (facing >= 0f ? StopRight : StopLeft);
        foreach (var material in GradeMaterials)
        {
            material.SetShaderParameter("dial_aspect", DialAspect);
            material.SetShaderParameter("wave_aspect", WaveAspect);
            material.SetShaderParameter("hour_angle", HourAngle);
            material.SetShaderParameter("minute_angle", MinuteAngle);
            material.SetShaderParameter("hole_corner", HoleCorner);
            material.SetShaderParameter("opacity", 0f);
        }
        _petalMaterial.SetShaderParameter("seed", (float)Random.Shared.NextDouble() * 3f);
        _petalMaterial.SetShaderParameter("opacity", 0f);
        _under.Visible = false;
        overRoot.GetNode<Node2D>("OverGrade").Visible = false;
        _petals.Visible = false;
        _template.Visible = false;

        // The extra roots are not children of the session root; they leave with it.
        overRoot.TreeExiting += ReleaseExtras;
        underRoot.TreeExiting += OnUnderExiting;
    }

    private IEnumerable<ShaderMaterial> GradeMaterials => [_underMaterial, _overMaterial];

    protected override IEnumerable<ShaderMaterial> Materials => [_underMaterial, _overMaterial, _petalMaterial];

    // The hold cap plus the release; the base clock's hard cut is only a backstop
    // behind the session's own graceful release at HoldCap.
    protected override float MaximumLifetime => HoldCap + ReleaseDuration + 2f;

    // --- Orchestration --------------------------------------------------------

    internal static Task PlayOrResolveAsync(
        CardModel card,
        Creature caster,
        bool empowered,
        Func<Cues, Task> resolveGameplay)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(caster);
        ArgumentNullException.ThrowIfNull(resolveGameplay);

        return CelVfxSession.PlayOrResolveAsync(
            "Time stop",
            () => TryCreate(caster, empowered),
            session => session.PlayPrelude(card, caster),
            scope => resolveGameplay(new Cues(scope)),
            session => session.ArmRelease(),
            session => session.Dispose());
    }

    internal sealed class Cues(CueScope<TimeStopVfx> scope)
    {
        /// <summary>
        /// Waits for the second hand's clack. Await immediately before ending the
        /// turn.
        /// </summary>
        /// <remarks>
        /// Returns at once when presentation is off, failed, or already past the
        /// clack, so the wait exists only while the clock is actually running.
        /// </remarks>
        internal Task StopAsync() => scope.InvokeAsync("stop", static session => session.StopAsync());
    }

    private static TimeStopVfx? TryCreate(Creature caster, bool empowered)
    {
        if (!TryPrepare("Time stop", LoadScene, out var room, out var container, out var scene)
            || room.Ui is not { } ui
            || room.GetCreatureNode(caster) is not { } casterNode
            || !GodotObject.IsInstanceValid(casterNode))
        {
            return null;
        }

        // One frozen world at a time: a second Time replaces the first.
        _current?.Dispose();

        Node2D? overRoot = null;
        Node2D? underRoot = null;
        Node2D? sealRoot = null;
        try
        {
            overRoot = scene.Instantiate<Node2D>();
            overRoot.Name = OverRootName;
            underRoot = overRoot.GetNode<Node2D>("UnderGrade");
            var pins = overRoot.GetNode<Node2D>("Pins");
            overRoot.RemoveChild(underRoot);
            overRoot.RemoveChild(pins);
            // These subtrees now live under native parents; they no longer
            // belong to the scene root that instantiated them.
            ClearOwner(underRoot);
            ClearOwner(pins);
            underRoot.Name = UnderRootName;

            // Over-layer: first in the combat VFX container, after every creature.
            overRoot.ZAsRelative = true;
            overRoot.ZIndex = 0;
            container.AddChildSafely(overRoot);
            container.MoveChildSafely(overRoot, 0);

            // Under-layer: first child of the Time owner's creature, before her body.
            underRoot.ZAsRelative = true;
            underRoot.ZIndex = 0;
            casterNode.AddChildSafely(underRoot);
            casterNode.MoveChildSafely(underRoot, 0);

            sealRoot = new Node2D
            {
                Name = SealRootName,
                ZAsRelative = false,
                ZIndex = SealZIndex
            };
            ui.AddChildSafely(sealRoot);
            sealRoot.AddChildSafely(pins);

            var session = new TimeStopVfx(overRoot, underRoot, sealRoot, room, caster, casterNode, empowered);
            _current = session;
            // Started after construction, never inside it: the base clock pulls
            // Materials, and during a base constructor the subclass fields are empty.
            session.StartClock();
            return session;
        }
        catch (Exception exception)
        {
            LogLoadFailure(exception);
            FreeSafely(underRoot);
            FreeSafely(sealRoot);
            FreeSafely(overRoot);
            return null;
        }
    }

    private async Task<bool> PlayPrelude(CardModel card, Creature caster)
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
        _under.Visible = true;
        Root.GetNode<Node2D>("OverGrade").Visible = true;
        _petals.Visible = true;
        Advance(WaveStart);
        // One tween owns body time for every layer; configured fully before any
        // yield. It outlasts the hold cap, which releases on its own.
        var span = HoldCap + ReleaseDuration + 1f;
        var body = Track(Root.CreateTween());
        body.TweenMethod(Callable.From<float>(Advance), WaveStart, WaveStart + span, span);
    }

    private async Task StopAsync()
    {
        if (!_bodyStarted || !float.IsNaN(_releaseAt) || !IsActive())
            return;

        var wait = Clack - _t;
        if (wait > 0f && !await WaitActive(wait))
            return;

        // The seals belong to the clack, before the turn ends. The frame wait can
        // resume before this frame's body tween reaches the clack, so launch them
        // here rather than leave them to the next Advance.
        if (_empowered && !_sealsLaunched && float.IsNaN(_releaseAt) && IsActive())
            LaunchSeals();
    }

    /// <summary>
    /// Gameplay has resolved: hold the freeze until the next turn starts (the
    /// extra turn), then release.
    /// </summary>
    private void ArmRelease()
    {
        if (!_bodyStarted || !IsActive())
        {
            Dispose();
            return;
        }

        if (_releaseArmed || !float.IsNaN(_releaseAt))
            return;

        _releaseArmed = true;
        CombatManager.Instance.TurnStarted += OnTurnStarted;
    }

    private void OnTurnStarted(CombatState _)
    {
        UnsubscribeTurn();
        BeginRelease();
    }

    private void BeginRelease()
    {
        if (!float.IsNaN(_releaseAt))
            return;

        _releaseAt = _t;
    }

    private void UnsubscribeTurn()
    {
        if (!_releaseArmed)
            return;

        _releaseArmed = false;
        CombatManager.Instance.TurnStarted -= OnTurnStarted;
    }

    // --- Frame ----------------------------------------------------------------

    private void Advance(float t)
    {
        _t = t;
        if (_extrasReleased)
            return;

        try
        {
            AdvanceFrame(t);
        }
        catch (Exception exception)
        {
            // A tween callback that throws would throw again every frame with the
            // world still grey; drop the presentation instead.
            MainFile.Logger.Error($"Time stop VFX frame failed and was disposed: {exception}");
            Dispose();
        }
    }

    private void AdvanceFrame(float t)
    {
        var casterLive = GodotObject.IsInstanceValid(_casterNode) && _casterNode.IsInsideTree();
        // Nothing may stay grey: a combat that is ending, a creature that has left,
        // or a hold that ran too long all release the world.
        if (CombatManager.Instance.IsEnding
            || !casterLive
            || t >= WaveStart + HoldCap)
        {
            BeginRelease();
        }

        var release = float.IsNaN(_releaseAt) ? 0f : Math.Clamp((t - _releaseAt) / ReleaseDuration, 0f, 1f);
        if (release >= 1f)
        {
            Dispose();
            return;
        }

        if (!_sealsLaunched && _empowered && t >= Clack && float.IsNaN(_releaseAt))
            LaunchSeals();

        var visible = VisibleRect();
        var origin = visible.Position;
        PlaceLayer(Root, visible);
        _overRect.Size = visible.Size;
        _petals.Size = visible.Size;
        if (GodotObject.IsInstanceValid(_under) && _under.IsInsideTree())
        {
            PlaceLayer(_under, visible);
            _underRect.Size = visible.Size;
        }

        var anchor = casterLive ? CelVfxGeometry.ResolveCaster(_casterNode) : null;
        if (anchor is { } resolved)
        {
            _lastCentre = resolved.BodyCenter;
            _lastDialRadius = DialRadius * Math.Clamp(resolved.BodySize.Y / DialReferenceHeight, 0.8f, 1.25f);
        }
        var centre = _lastCentre - origin;
        var hole = HoleRect();
        var cover = CoverRadius(centre, visible.Size);
        UpdateGrade(t, release, centre, cover, hole is { } rect ? new Rect2(rect.Position - origin, rect.Size) : null, visible.Size);
        UpdatePetals(t, release, centre, visible.Size);
        UpdateSeals(t, release);
    }

    private void UpdateGrade(float t, float release, Vector2 centre, float cover, Rect2? hole, Vector2 size)
    {
        var started = t >= WaveStart;
        var waveU = Math.Clamp((t - WaveStart) / (WaveEnd - WaveStart), 0f, 1f);
        var waveRadius = started ? cover * (1f - (1f - waveU) * (1f - waveU)) : 0f;
        var clackFrame = FramesSince(t, Clack);
        var flash = clackFrame is >= 0 and < FlashFrames ? 1f : 0f;
        var focus = clackFrame is >= 0 and < FocusFrames ? 1f : 0f;
        var squash = clackFrame is >= 0 and < SquashFrames ? 1f : 0f;
        var shudder = clackFrame switch
        {
            0 => 10f,
            1 => -4f,
            _ => 0f
        };
        var clack = 0f;
        var pulse = 0f;
        if (t >= Clack)
        {
            clack = 1f - Math.Clamp((t - Clack) / Burst, 0f, 1f);
            var u = Math.Clamp((t - Clack) / Pulse, 0f, 1f);
            pulse = u >= 1f ? 0f : Math.Max(u, 0.001f);
        }

        var reveal = EaseOut((t - DialRevealStart) / (DialRevealEnd - DialRevealStart));
        var settle = Math.Clamp((t - Clack - FlashFrames / StepFrequency) / Settle, 0f, 1f);
        var fadeOut = 1f - EaseInOut(release);
        var dial = (t >= DialRevealStart ? 1f : 0f) * Mathf.Lerp(1f, DialHoldOpacity, settle) * fadeOut;
        var handGrow = EaseOut((t - HandGrow) / (HandSweepStart - HandGrow + 0.02f));
        var handOpacity = (t >= HandGrow ? 1f : 0f) * Mathf.Lerp(1f, HandHoldOpacity, settle) * fadeOut;
        var angularSpeed = 0f;
        if (t >= HandSweepStart && t < Clack)
        {
            var u = (t - HandSweepStart) / (Clack - HandSweepStart);
            angularSpeed = (_handStop - HandFrom) * (0.35f + 1.3f * u) / (Clack - HandSweepStart);
        }

        var releasing = !float.IsNaN(_releaseAt);
        var holeRect = hole is { } rect
            ? new Vector4(rect.Position.X, rect.Position.Y, rect.Size.X, rect.Size.Y)
            : Vector4.Zero;
        foreach (var material in GradeMaterials)
        {
            material.SetShaderParameter("region_size", size);
            material.SetShaderParameter("centre", centre);
            material.SetShaderParameter("hole_rect", holeRect);
            material.SetShaderParameter("dial_radius", _lastDialRadius);
            material.SetShaderParameter("opacity", 1f);
            material.SetShaderParameter("wave_radius", waveRadius);
            material.SetShaderParameter("front", started && waveU < 1f ? 1f : 0f);
            material.SetShaderParameter("flash", flash);
            material.SetShaderParameter("focus", focus);
            material.SetShaderParameter("release_radius", releasing ? cover * EaseInOut(release) : 0f);
            material.SetShaderParameter("release_front", releasing && release < 1f ? 1f : 0f);
            material.SetShaderParameter("dial_opacity", dial);
            material.SetShaderParameter("dial_reveal", reveal);
            material.SetShaderParameter("dial_shudder", shudder);
            material.SetShaderParameter("dial_pulse", pulse);
            material.SetShaderParameter("hand_angle", HandAngle(t));
            material.SetShaderParameter("hand_grow", handGrow);
            material.SetShaderParameter("hand_opacity", handOpacity);
            material.SetShaderParameter("hand_smear", Math.Clamp(angularSpeed * SmearScale, 0f, 0.85f));
            material.SetShaderParameter("clack", clack);
            material.SetShaderParameter("hand_squash", squash);
        }
    }

    private void UpdatePetals(float t, float release, Vector2 centre, Vector2 size)
    {
        // Petals ride the wave out, stop dead at the clack, fall again at release.
        var travel = Math.Clamp(t - WaveStart, 0f, Clack - WaveStart);
        var fall = 0f;
        var freeze = t >= Clack ? 1f : 0f;
        if (!float.IsNaN(_releaseAt))
        {
            fall = Math.Max(0f, t - _releaseAt);
            travel += fall;
            freeze *= 1f - Math.Clamp(fall / PetalThaw, 0f, 1f);
        }
        _petalMaterial.SetShaderParameter("region_size", size);
        _petalMaterial.SetShaderParameter("centre", centre);
        _petalMaterial.SetShaderParameter("petal_time", travel);
        _petalMaterial.SetShaderParameter("petal_fall", fall);
        _petalMaterial.SetShaderParameter("petal_freeze", freeze);
        _petalMaterial.SetShaderParameter("opacity", 1f - EaseInOut(release));
    }

    private float HandAngle(float t)
    {
        if (t < HandSweepStart)
            return HandFrom;
        if (t < Clack)
        {
            var u = (t - HandSweepStart) / (Clack - HandSweepStart);
            // Accelerates into the stop, so it lands as a hit rather than settling.
            return HandFrom + (_handStop - HandFrom) * (0.35f * u + 0.65f * u * u);
        }

        var angle = _handStop;
        if (FramesSince(t, Clack) < SquashFrames)
            angle -= HandRecoil;
        if (!float.IsNaN(_releaseAt))
            angle += HandTick;
        return angle;
    }

    // --- Geometry -------------------------------------------------------------

    /// <summary>The visible viewport in the combat canvas's global coordinates.</summary>
    private Rect2 VisibleRect()
    {
        var viewport = Root.GetViewport();
        return viewport.GetCanvasTransform().AffineInverse() * viewport.GetVisibleRect();
    }

    private static void PlaceLayer(Node2D layer, Rect2 visible)
    {
        // Unrotated and unscaled whatever the parent, so one local pixel is one
        // canvas pixel and both grade layers agree on every coordinate.
        layer.GlobalTransform = new Transform2D(0f, visible.Position);
    }

    private static float CoverRadius(Vector2 centre, Vector2 size)
    {
        var best = 0f;
        foreach (var corner in new[] { Vector2.Zero, new Vector2(size.X, 0f), new Vector2(0f, size.Y), size })
        {
            var d = corner - centre;
            best = Math.Max(best, new Vector2(d.X, d.Y / WaveAspect).Length());
        }
        return best + CoverMargin;
    }

    /// <summary>
    /// Sakura's own pixels: the standee's visible parts (taller than the hitbox),
    /// her hitbox, health bar, Block and power row, padded.
    /// </summary>
    private Rect2? HoleRect()
    {
        if (!GodotObject.IsInstanceValid(_casterNode) || !_casterNode.IsInsideTree())
            return null;

        Rect2? union = null;
        void Add(Rect2 rect)
        {
            if (rect.Size.X <= 0f || rect.Size.Y <= 0f)
                return;
            union = union is { } current ? current.Merge(rect) : rect;
        }

        Rect2? hitbox = null;
        if (_casterNode.Hitbox is { } box && GodotObject.IsInstanceValid(box))
        {
            hitbox = box.GetGlobalRect();
            Add(hitbox.Value);
        }

        // A standee part can never widen the hole beyond a body around her.
        var limit = hitbox is { } h ? h.Grow(Math.Max(h.Size.X, h.Size.Y) * 0.75f) : (Rect2?)null;
        foreach (var part in _standeeParts)
        {
            if (!GodotObject.IsInstanceValid(part) || !part.IsVisibleInTree())
                continue;
            if (PartRect(part) is not { } rect)
                continue;
            Add(limit is { } bound ? rect.Intersection(bound) : rect);
        }

        if (StateDisplay() is { } display)
        {
            if (display.IsVisibleInTree())
                Add(display.GetGlobalRect());
            if (display.GetNodeOrNull<NHealthBar>("%HealthBar") is { } bar && bar.IsVisibleInTree())
            {
                Add(bar.GetGlobalRect());
                if (bar.GetNodeOrNull<Control>("%BlockContainer") is { } block && block.IsVisibleInTree())
                    Add(block.GetGlobalRect());
            }
            if (display.GetNodeOrNull<Control>("%PowerContainer") is { } powers)
            {
                foreach (var power in powers.GetChildren().OfType<NPower>())
                {
                    if (power.IsVisibleInTree())
                        Add(power.GetGlobalRect());
                }
            }
        }

        return union?.Grow(HolePad);
    }

    private NCreatureStateDisplay? StateDisplay() =>
        GodotObject.IsInstanceValid(_casterNode)
            ? _casterNode.GetNodeOrNull<NCreatureStateDisplay>("%HealthBar")
            : null;

    private static List<CanvasItem> CollectStandeeParts(NCreature casterNode)
    {
        var parts = new List<CanvasItem>();
        if (casterNode.Visuals is not { } visuals || !GodotObject.IsInstanceValid(visuals))
            return parts;

        foreach (var node in visuals.FindChildren("*", "", true, false))
        {
            if (node is Sprite2D or Polygon2D or MeshInstance2D or TextureRect)
                parts.Add((CanvasItem)node);
        }
        return parts;
    }

    private static Rect2? PartRect(CanvasItem part)
    {
        switch (part)
        {
            case Sprite2D sprite when sprite.Texture is not null:
                return sprite.GetGlobalTransform() * sprite.GetRect();
            case Polygon2D polygon when polygon.Polygon.Length > 0:
            {
                var transform = polygon.GetGlobalTransform();
                Rect2? rect = null;
                foreach (var point in polygon.Polygon)
                {
                    var global = transform * (point + polygon.Offset);
                    rect = rect is { } current ? current.Expand(global) : new Rect2(global, Vector2.Zero);
                }
                return rect;
            }
            case MeshInstance2D mesh when mesh.Mesh is not null:
            {
                var aabb = mesh.Mesh.GetAabb();
                var local = new Rect2(aabb.Position.X, aabb.Position.Y, aabb.Size.X, aabb.Size.Y);
                return mesh.GetGlobalTransform() * local;
            }
            case TextureRect texture:
                return texture.GetGlobalRect();
            default:
                return null;
        }
    }

    // --- Seals (extra effect) -------------------------------------------------

    private void LaunchSeals()
    {
        _sealsLaunched = true;
        var index = 0;
        // Energy and hand are the local player's UI: seal them only for her own play.
        var local = LocalContext.IsMe(_caster.Player);

        if (_caster.Block > 0
            && StateDisplay()?.GetNodeOrNull<NHealthBar>("%HealthBar")?.GetNodeOrNull<Control>("%BlockContainer") is { } block
            && block.IsVisibleInTree())
        {
            AddSeal("block", block, [block], ref index);
        }
        else if (_caster.Block > 0)
        {
            LogMissingTarget("Block");
        }

        if (local)
        {
            if (Room.Ui is { } ui && ui.EnergyCounterContainer is { } container && container.IsVisibleInTree())
            {
                var energy = container.GetChildren().OfType<NEnergyCounter>().FirstOrDefault() ?? (Control)container;
                AddSeal("energy", energy, [energy], ref index);
            }
            else
            {
                LogMissingTarget("energy");
            }

            var cards = NPlayerHand.Instance is { } hand
                ? hand.ActiveHolders
                    .Select(static holder => holder.CardNode)
                    .OfType<NCard>()
                    .Where(static card => GodotObject.IsInstanceValid(card) && card.IsVisibleInTree())
                    .Select(static card => GodotObject.IsInstanceValid(card.Body) ? card.Body : (Control)card)
                    .ToList()
                : [];
            if (cards.Count > 0)
                AddSeal("hand", null, cards, ref index);
        }

        if (StateDisplay()?.GetNodeOrNull<Control>("%PowerContainer") is { } powers)
        {
            foreach (var power in powers.GetChildren().OfType<NPower>())
            {
                if (power.IsVisibleInTree() && IsElementState(power))
                    AddSeal("element", power, [power], ref index);
            }
        }
    }

    private static bool IsElementState(NPower power)
    {
        try
        {
            return power.Model is SakuraElementStatePower;
        }
        catch (InvalidOperationException)
        {
            // A power node whose model is not bound yet is not a preserved state.
            return false;
        }
    }

    private void AddSeal(string kind, Control? target, IReadOnlyList<Control> glazed, ref int index)
    {
        var pin = (Node2D)_template.Duplicate();
        pin.Name = $"TimeSeal{index}";
        pin.Visible = false;
        _template.GetParent().AddChildSafely(pin);

        var prototype = pin.GetNode<ColorRect>("Glaze");
        prototype.Visible = false;
        var glazes = new List<Glaze>();
        for (var k = 0; k < glazed.Count; k++)
        {
            var anchor = new Node2D { Name = $"GlazeAnchor{k}" };
            pin.AddChildSafely(anchor);
            var rect = (ColorRect)prototype.Duplicate();
            rect.Visible = true;
            anchor.AddChildSafely(rect);
            var material = CelVfxGeometry.DuplicateMaterial(rect, "time seal glaze");
            material.SetShaderParameter("seed", index * 1.37f + k * 0.61f + 0.2f);
            material.SetShaderParameter("rim_round", kind switch
            {
                "energy" or "element" or "block" => 1f,
                _ => 0.08f
            });
            material.SetShaderParameter("glaze", 0f);
            material.SetShaderParameter("sheen", -1f);
            material.SetShaderParameter("opacity", 0f);
            glazes.Add(new Glaze(glazed[k], anchor, rect, material, k));
        }

        var body = pin.GetNode<ColorRect>("Hand/Body");
        var handMaterial = CelVfxGeometry.DuplicateMaterial(body, "time seal hand");
        var sealBody = pin.GetNode<ColorRect>("Seal");
        var sealMaterial = CelVfxGeometry.DuplicateMaterial(sealBody, "time seal");
        sealMaterial.SetShaderParameter("hour_angle", HourAngle);
        sealMaterial.SetShaderParameter("minute_angle", MinuteAngle);
        sealMaterial.SetShaderParameter("hand_angle", _handStop);
        sealMaterial.SetShaderParameter("stamp", 0f);
        sealMaterial.SetShaderParameter("ripple", 0f);
        sealMaterial.SetShaderParameter("opacity", 0f);
        // The seal draws above every glaze layer.
        pin.MoveChildSafely(sealBody, pin.GetChildCount() - 1);

        _seals.Add(new Seal
        {
            Kind = kind,
            Pin = pin,
            Hand = pin.GetNode<Node2D>("Hand"),
            HandMaterial = handMaterial,
            Trail = pin.GetNode<Line2D>("Trail"),
            Body = sealBody,
            SealMaterial = sealMaterial,
            Launch = Clack + index * PinStagger,
            Glazes = glazes,
            Target = target
        });
        index++;
    }

    private void UpdateSeals(float t, float release)
    {
        if (_seals.Count == 0)
            return;

        var fadeOut = 1f - EaseInOut(release);
        foreach (var seal in _seals)
        {
            if (seal.Lost || !GodotObject.IsInstanceValid(seal.Pin))
                continue;

            if (!UpdateGlazes(seal, t, fadeOut) || SealPlacement(seal) is not { } placement)
            {
                // Its element is gone (a card left the hand, a power was removed).
                seal.Lost = true;
                seal.Pin.QueueFreeSafely();
                continue;
            }

            var (centre, radius) = placement;
            seal.Centre = centre;
            var u = (t - seal.Launch) / PinFlight;
            seal.Pin.Visible = u >= 0f;
            if (u < 0f)
                continue;

            var landedAt = seal.Launch + PinFlight;
            // The flying hand leaves the dial ring on the side facing its target.
            var toTarget = (centre - _lastCentre).Normalized();
            var reach = _lastDialRadius / MathF.Sqrt(toTarget.X * toTarget.X
                + toTarget.Y * toTarget.Y / (DialAspect * DialAspect));
            var start = _lastCentre + toTarget * reach;
            var into = new Vector2(1f, 1f).Normalized();
            var p1 = start + new Vector2(0f, -220f);
            var p2 = centre - into * 180f;
            var e = EaseOut(u);
            var position = Bezier(start, p1, p2, centre, e);
            var tangent = Bezier(start, p1, p2, centre, Math.Min(e + 0.02f, 1f))
                - Bezier(start, p1, p2, centre, Math.Max(e - 0.02f, 0f));
            var angle = tangent.Angle();
            seal.Hand.GlobalRotation = angle;
            seal.Hand.GlobalPosition = position - Vector2.FromAngle(angle) * PinTipOffset;
            seal.HandMaterial.SetShaderParameter("opacity", u < 1f ? fadeOut : 0f);

            seal.Trail.Visible = u < 1f;
            if (u < 1f)
            {
                var inverse = seal.Trail.GetGlobalTransform().AffineInverse();
                var points = new Vector2[10];
                for (var k = 0; k < points.Length; k++)
                {
                    var te = EaseOut(Math.Clamp(u - 0.30f * (1f - k / 9f), 0f, 1f));
                    points[k] = inverse * Bezier(start, p1, p2, centre, te);
                }
                seal.Trail.Points = points;
            }

            seal.Body.Scale = Vector2.One * radius / SealDesignRadius;
            seal.Body.GlobalPosition = centre - Vector2.One * SealRectHalf;
            var stampFrame = FramesSince(t, landedAt);
            var stamp = stampFrame >= 0 ? Math.Min((stampFrame + 1f) / SealStampFrames, 1f) : 0f;
            seal.SealMaterial.SetShaderParameter("stamp", stamp);
            var ripple = t >= landedAt ? Math.Clamp((t - landedAt) / SealRipple, 0f, 1f) : 0f;
            seal.SealMaterial.SetShaderParameter("ripple", t < landedAt || ripple >= 1f ? 0f : Math.Max(ripple, 0.001f));
            seal.SealMaterial.SetShaderParameter("opacity", fadeOut);
        }
    }

    /// <summary>
    /// Re-anchors each glaze on its element; false when every element of the
    /// seal has gone. Cards that leave the hand drop their own glaze.
    /// </summary>
    private bool UpdateGlazes(Seal seal, float t, float fadeOut)
    {
        var landedAt = seal.Launch + PinFlight;
        var any = false;
        foreach (var glaze in seal.Glazes)
        {
            if (!GodotObject.IsInstanceValid(glaze.Anchor))
                continue;
            if (!IsLiveTarget(seal.Kind, glaze.Target))
            {
                glaze.Anchor.QueueFreeSafely();
                continue;
            }

            any = true;
            glaze.Anchor.GlobalTransform = glaze.Target.GetGlobalTransform();
            var size = glaze.Target.Size;
            glaze.Rect.Position = -Vector2.One * GlazePad;
            glaze.Rect.Size = size + Vector2.One * GlazePad * 2f;
            glaze.Material.SetShaderParameter("region_size", glaze.Rect.Size);
            glaze.Material.SetShaderParameter("rim_size", size);
            glaze.Material.SetShaderParameter("glaze", t >= landedAt ? EaseOut((t - landedAt) / GlazeIn) : 0f);
            glaze.Material.SetShaderParameter("opacity", fadeOut);
            var sheen = -1f;
            if (t >= landedAt + GlazeIn)
            {
                // Stepped: crosses in SheenCross, rests for the rest of the cycle;
                // cards in the hand take it in turn.
                var stepped = MathF.Floor((t - landedAt - GlazeIn) * StepFrequency) / StepFrequency;
                var phase = (stepped + seal.Launch * 5f + glaze.Index * 0.12f) % SheenCycle;
                sheen = phase <= SheenCross ? phase / SheenCross : -1f;
            }
            glaze.Material.SetShaderParameter("sheen", sheen);
        }
        return any;
    }

    private static bool IsLiveTarget(string kind, Control target)
    {
        if (!GodotObject.IsInstanceValid(target) || !target.IsInsideTree() || !target.IsVisibleInTree())
            return false;
        if (kind != "hand")
            return true;

        // Still a card in the hand (not played, discarded or exhausted).
        return NPlayerHand.Instance is { } hand
            && GodotObject.IsInstanceValid(hand)
            && hand.CardHolderContainer.IsAncestorOf(target);
    }

    /// <summary>Where a seal stamps and how large: by its element's live rect.</summary>
    private (Vector2 Centre, float Radius)? SealPlacement(Seal seal)
    {
        if (seal.Kind == "hand")
        {
            Rect2? union = null;
            foreach (var glaze in seal.Glazes)
            {
                // Same frame as UpdateGlazes: a glaze dropped there is queued, and
                // its card may already be outside the tree.
                if (!GodotObject.IsInstanceValid(glaze.Anchor)
                    || glaze.Anchor.IsQueuedForDeletion()
                    || !IsLiveTarget(seal.Kind, glaze.Target))
                    continue;
                var rect = glaze.Target.GetGlobalRect();
                union = union is { } current ? current.Merge(rect) : rect;
            }
            return union is { } hand
                ? (new Vector2(hand.GetCenter().X, hand.Position.Y + 4f), 26f)
                : null;
        }

        if (seal.Target is not { } target || !GodotObject.IsInstanceValid(target))
            return null;

        var bounds = target.GetGlobalRect();
        var c = bounds.GetCenter();
        var w = bounds.Size.X;
        var h = bounds.Size.Y;
        var small = Math.Min(w, h);
        return seal.Kind switch
        {
            "block" => (c + new Vector2(0.375f * w, -0.33f * h), Math.Clamp(small * 0.33f, 10f, 16f)),
            "energy" => (c + new Vector2(0.30f * w, -0.29f * h), Math.Clamp(small * 0.15f, 14f, 24f)),
            _ => (c + new Vector2(0.375f * w, 0.34f * h), Math.Clamp(small * 0.34f, 9f, 14f))
        };
    }

    private static void LogMissingTarget(string what)
    {
        if (_missingTargetLogged)
            return;

        _missingTargetLogged = true;
        MainFile.Logger.Debug($"Time stop seal skipped: no live {what} display.");
    }

    // --- Cleanup --------------------------------------------------------------

    /// <summary>
    /// Frees the under-layer and the seal overlay with the session root. Runs on
    /// every exit path: normal release, combat end, tree exit, exceptions and the
    /// lifetime cap all end by freeing the root.
    /// </summary>
    private void ReleaseExtras()
    {
        if (_extrasReleased)
            return;

        _extrasReleased = true;
        if (GodotObject.IsInstanceValid(Root))
            Root.TreeExiting -= ReleaseExtras;
        UnsubscribeTurn();
        if (GodotObject.IsInstanceValid(_under))
            _under.TreeExiting -= OnUnderExiting;
        FreeSafely(_under);
        FreeSafely(_sealRoot);
        if (ReferenceEquals(_current, this))
            _current = null;
    }

    // The creature left (death, room teardown): its child layer goes with it.
    private void OnUnderExiting() => BeginRelease();

    private static void ClearOwner(Node node)
    {
        node.Owner = null;
        foreach (var child in node.FindChildren("*", "", true, false))
            child.Owner = null;
    }

    private static void FreeSafely(Node? node)
    {
        if (node is not null && GodotObject.IsInstanceValid(node) && !node.IsQueuedForDeletion())
            node.QueueFreeSafely();
    }

    private static PackedScene LoadScene() =>
        PreloadManager.Cache.GetScene(ScenePath);

    private static void LogLoadFailure(Exception exception)
    {
        if (_loadFailureLogged)
            return;

        _loadFailureLogged = true;
        MainFile.Logger.Error($"Could not create Time stop VFX from {ScenePath} and {ShaderPath}: {exception}");
    }

    // --- Curves ---------------------------------------------------------------

    /// <summary>Whole 12 Hz frames since an instant, or -1 before it.</summary>
    private static int FramesSince(float t, float at) =>
        t < at ? -1 : (int)MathF.Floor((t - at) * StepFrequency);

    private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
    {
        var s = 1f - t;
        return s * s * s * a + 3f * s * s * t * b + 3f * s * t * t * c + t * t * t * d;
    }

    private static float EaseOut(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return 1f - (1f - t) * (1f - t) * (1f - t);
    }

    private static float EaseInOut(float t) => -(MathF.Cos(MathF.PI * Math.Clamp(t, 0f, 1f)) - 1f) * 0.5f;
}
