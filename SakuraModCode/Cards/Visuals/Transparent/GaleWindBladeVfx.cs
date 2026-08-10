using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
/// Gale's wind blade: disconnected strokes flying from Sakura's hand through the
/// target.
/// </summary>
/// <remarks>
/// The first session in this family whose region travels. Nothing in the shared
/// geometry layer needed to grow for it: <c>ResolveCaster</c> already supplies the
/// launch end and <c>Resolve</c> the target end, and <c>region_size</c> is constant
/// throughout the flight because only the carrier's position and rotation change.
/// Rotation preserves length, so the derivative-width ink stays constant at every
/// flight angle with no compensation.
/// <para>
/// One scene, like Blaze and unlike Aqua's root-plus-target split. That split lets a
/// single <c>BackBufferCopy</c> serve N target copies; this card hits one enemy and
/// has no <c>BackBufferCopy</c> at all, so there is nothing to share.
/// </para>
/// </remarks>
internal sealed class GaleWindBladeVfx : CelVfxSession
{
    internal const string ScenePath =
        MainFile.ResPath + "/scenes/combat/card_vfx/gale_wind_blade_vfx.tscn";
    internal const string ShaderPath =
        MainFile.ResPath + "/shaders/card_vfx/gale_wind_blade.gdshader";
    internal static IReadOnlyList<string> AssetPaths { get; } = [ScenePath];

    /// <summary>
    /// Flight time, in seconds. Three stepped frames at 12 Hz.
    /// </summary>
    /// <remarks>
    /// A floor set by legibility, not by comfort. Travel shorter than about three
    /// stepped frames is sampled too coarsely to read as movement and lands as a
    /// jump instead — the mistake the sword card made at 0.16 s, which is 1.92
    /// frames. Beats on this program are counted in frames, then converted.
    /// </remarks>
    private const float FlightDuration = 3f / StepFrequency;

    private const float DissipateDuration = 0.30f;
    private const float FadeDuration = 0.18f;

    /// <summary>
    /// A hold lasts this long, matching <see cref="CelVfxSession.BeginHold"/> at two
    /// stepped frames. Motion tweens wait it out: <c>BeginHold</c> freezes shader
    /// time, not Godot's tween clock, so a blade that looks frozen would otherwise
    /// keep sliding and shed moving debris.
    /// </summary>
    private const float HoldDuration = 2f / StepFrequency;

    /// <summary>
    /// Region extent in pixels, fixed rather than derived from the target's hitbox.
    /// One wind blade does not get longer because the enemy is bigger; only where it
    /// flies to depends on the target.
    /// </summary>
    /// <remarks>
    /// Longer along travel than across it, because a spearhead is longer than it is
    /// wide. Two earlier revisions failed on this axis rather than on any parameter:
    /// several similar strokes lying along the flight axis read as a gust, and several
    /// similar arcs nested across it read as a sonar ping. The fix was to draw one body
    /// instead of a series, which the shader's own header records at length.
    /// </remarks>
    private static readonly Vector2 BladeRegion = new(300f, 160f);

    private const float BladeRegionHalfX = 150f;

    /// <summary>
    /// Distance from the region centre to the point of the spearhead, mirroring
    /// <c>TIP_FRAC</c> in the shader. The carrier stops this far short of the target so
    /// the point, not the region centre, is what arrives.
    /// </summary>
    private const float TipOffsetPx = BladeRegionHalfX * 0.92f;

    /// <summary>
    /// How far past the target the blade continues while coming apart. The air closes
    /// behind a blade that passed through; one that stops on the target reads as a
    /// blade that hit a wall.
    /// </summary>
    private const float OvershootPx = 96f;

    /// <summary>
    /// Height of the launch point between the caster's floor and hitbox centre.
    /// </summary>
    /// <remarks>
    /// Expressed as a bias upward from the floor, so a larger value is higher. The
    /// magic circle sits at 0.38 by this measure and a shield plate at 0.70; a hand
    /// throwing a blade is above both.
    /// </remarks>
    private const float LaunchBodyBias = 0.72f;

    private const int DebrisCount = 7;

    /// <summary>
    /// Petals and leaves fall far slower than the 980 default: they are light and
    /// present a large face to the air, so drag dominates. A parameter of the shared
    /// integrator, not a second integrator.
    /// </summary>
    private const float DebrisGravity = 240f;

    private const int VfxZIndex = 3000;

    private static bool _loadFailureLogged;

    private readonly ShaderMaterial _material;
    private readonly Node2D _carrier;
    private readonly Node2D _debris;
    private readonly Vector2 _launch;
    private readonly Vector2 _flightEnd;
    private readonly Vector2 _overshoot;
    private readonly Vector2 _direction;
    private bool _impacted;
    private bool _faded;

    private GaleWindBladeVfx(
        Node2D root,
        NCombatRoom room,
        CelVfxGeometry.CasterAnchor caster,
        CelVfxGeometry.TargetGeometry target)
        : base(root, room)
    {
        _carrier = root.GetNode<Node2D>("%BladeCarrier");
        _debris = root.GetNode<Node2D>("%Debris");

        // The root sits at the container origin and never moves; all travel belongs to
        // the carrier. That keeps the shader's local coordinates independent of where
        // the blade currently is, and keeps Debris — a sibling of the carrier — from
        // being dragged along after the fragments have been thrown.
        root.GlobalPosition = Vector2.Zero;
        root.Scale = Vector2.One;

        var launchY = caster.Floor.Y + (caster.BodyCenter.Y - caster.Floor.Y) * LaunchBodyBias;
        _launch = new Vector2(
            // FacingSign is used here and nowhere else. The blade's own orientation
            // comes from the two endpoints, which already carry left versus right; a
            // caster-side effect with no second endpoint, like the shield, is the case
            // that has to rely on the sign alone.
            caster.BodyCenter.X + caster.FacingSign * caster.BodySize.X * 0.5f,
            launchY);

        var toTarget = target.Center - _launch;
        _direction = toTarget.LengthSquared() > 1f ? toTarget.Normalized() : Vector2.Right;
        _flightEnd = target.Center - _direction * TipOffsetPx;
        _overshoot = _flightEnd + _direction * OvershootPx;

        _carrier.GlobalPosition = _launch;
        _carrier.Rotation = _direction.Angle();

        var body = root.GetNode<ColorRect>("%BladeBody");
        _material = CelVfxGeometry.DuplicateMaterial(body, "wind blade");

        // Sizing travels to the shader as region_size rather than as a node scale,
        // which is what holds ink weight constant in screen pixels.
        body.Size = BladeRegion;
        body.Position = -BladeRegion * 0.5f;
        _material.SetShaderParameter("region_size", BladeRegion);
        _material.SetShaderParameter("seed", (float)Random.Shared.NextDouble() * 6.1f);
        _material.SetShaderParameter("fray", 0f);
        _material.SetShaderParameter("sever", 0f);
    }

    /// <summary>
    /// Sizes the target anchor only. The blade's own extent is fixed, so this budget
    /// decides where the flight ends and nothing about how long the blade is.
    /// </summary>
    private static CelVfxGeometry.GeometryBudget Budget => new(
        HorizontalPadding: 0f,
        VerticalPadding: 0f,
        MinWidth: 80f,
        MinHeight: 90f,
        MaxWidth: 300f,
        MaxHeight: 340f,
        FallbackWidth: 150f,
        FallbackHeight: 180f,
        FloorClearance: 0f,
        MaxViewportWidthFraction: 0.22f,
        MaxViewportHeightFraction: 0.40f);

    protected override IEnumerable<ShaderMaterial> Materials => [_material];

    /// <summary>
    /// Safety net, not a timer. The worst chain — chibi prelude, flight, hold,
    /// dissipation, fade — runs about 1.22 s. Sized well clear of it, because a cap
    /// set tight becomes a truncation bug.
    /// </summary>
    protected override float MaximumLifetime => 5.0f;

    /// <summary>
    /// Builds the session from both ends of the flight, or returns null when either
    /// end cannot be resolved.
    /// </summary>
    /// <remarks>
    /// The first two-ended session in the family. A missing attacker node is as fatal
    /// as a missing target: a blade with no launch point would have to fall back to
    /// some invented origin, and an invented origin on a card whose whole read is
    /// "thrown from her hand" is worse than showing nothing.
    /// </remarks>
    internal static GaleWindBladeVfx? TryCreate(Creature attacker, Creature target)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(target);
        if (!TryPrepare("Gale wind", LoadScene, out var room, out _, out var scene))
            return null;

        Node2D? root = null;
        try
        {
            if (CelVfxGeometry.ResolveCaster(room.GetCreatureNode(attacker)) is not { } caster)
                return null;

            root = scene.Instantiate<Node2D>();
            root.Name = "SakuraGaleWindBladeVfx";
            root.ZAsRelative = false;
            root.ZIndex = VfxZIndex;
            room.CombatVfxContainer.AddChildSafely(root);

            var geometry = CelVfxGeometry.Resolve(room, target, 0, Budget);
            var session = new GaleWindBladeVfx(root, room, caster, geometry);
            // Started after construction, never inside it: the base clock pulls
            // Materials, and during a base constructor the subclass field backing it
            // is still empty.
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

    /// <summary>
    /// Shared wand tap and speed lines, then the blade flies to the target.
    /// </summary>
    /// <remarks>
    /// Flight belongs to the prelude rather than to a third public beat. The shared
    /// prelude ends on the instant the wand touches the card, which is the instant the
    /// blade is released; awaiting this therefore returns with the blade already on
    /// the target, so <see cref="Impact"/> and the damage number land together. A
    /// separate <c>Fly</c> method would only add an ordering the caller has to
    /// remember for no independent use.
    /// </remarks>
    internal async Task<bool> PlayPrelude(CardModel card, Creature? caster)
    {
        if (!await PlayCelPrelude(card, caster))
            return false;

        var flight = Track(Root.CreateTween().SetParallel());
        // Constant velocity, no easing. An air blade has near-zero mass and is driven
        // the whole way, so the ballistic drop over a quarter second is far below a
        // pixel — which is also why CelVfxGeometry.BallisticOffset is not used here.
        // That integrator is for objects thrown and then left to gravity.
        flight.TweenProperty(_carrier, "global_position", _flightEnd, FlightDuration)
            .SetTrans(Tween.TransitionType.Linear);
        // Instability accumulates as the sheet travels, so fray is a function of
        // flight time as well as of distance back from the core toward the tail.
        flight.TweenMethod(
                Callable.From<float>(value => _material.SetShaderParameter("fray", value)),
                0f,
                1f,
                FlightDuration)
            .SetTrans(Tween.TransitionType.Linear);
        return await WaitActive(FlightDuration);
    }

    /// <summary>
    /// The hit beat: hold two stepped frames, then let the blade continue through the
    /// target while its wake is consumed from the tail forward.
    /// </summary>
    /// <remarks>
    /// A pass-through, not a burst. The blade keeps its heading and the air closes
    /// behind it, which is what a shear does; an outward explosion would be the
    /// signature of something that stopped.
    /// </remarks>
    internal void Impact()
    {
        if (_impacted || !IsActive())
            return;

        _impacted = true;

        // The hold is the shared signature element: drawn detail freezes for two
        // stepped frames, then motion continues from where it stopped.
        BeginHold();

        var pass = Track(Root.CreateTween().SetParallel());
        // Every tweener here is delayed past the hold. BeginHold stops shader time
        // only; without the delay the silhouette would sit frozen while the node it
        // is drawn on slides out from under it.
        pass.TweenProperty(_carrier, "global_position", _overshoot, DissipateDuration)
            .SetDelay(HoldDuration)
            .SetTrans(Tween.TransitionType.Linear);
        pass.TweenMethod(
                Callable.From<float>(value => _material.SetShaderParameter("sever", value)),
                0f,
                1f,
                DissipateDuration)
            .SetDelay(HoldDuration)
            .SetEase(Tween.EaseType.In)
            .SetTrans(Tween.TransitionType.Quad);

        // Debris is thrown from the impact point and parented to the root, not to the
        // carrier: once torn loose these belong to the room, and riding the carrier
        // would read as the blade carrying them away.
        var perpendicular = new Vector2(-_direction.Y, _direction.X);
        for (var i = 0; i < DebrisCount; i++)
        {
            var spread = -0.5f + (float)i / Math.Max(1, DebrisCount - 1);
            var velocity = _direction * (150f + i % 3 * 55f)
                + perpendicular * spread * 190f
                // A small upward bias: what the passing blade lifts rises before it
                // falls, which is what makes the arc read as air rather than as a throw.
                + Vector2.Up * (60f + i % 2 * 40f);
            var origin = _flightEnd + _direction * TipOffsetPx + perpendicular * spread * 26f;
            CelVfxGeometry.AddBallisticDebris(
                pass,
                _debris,
                DebrisPoints(2.8f + i % 3 * 0.9f),
                i % 3 == 0 ? new Color(0.94f, 0.99f, 0.97f) : new Color(0.62f, 0.88f, 0.84f),
                origin,
                velocity,
                DissipateDuration,
                HoldDuration,
                DebrisGravity,
                2.2f + i * 0.24f,
                "GaleDebris");
        }
    }

    /// <summary>
    /// Fades the blade out, then releases. The base <c>Dispose</c> it ends in is
    /// idempotent and also covers combat end, tree exit, exceptions, and the lifetime
    /// cap.
    /// </summary>
    internal void FadeAndDispose()
    {
        if (_faded || !IsActive())
        {
            Dispose();
            return;
        }

        _faded = true;
        // Wait out the dissipation the impact beat started, so the blade dies by
        // breaking up rather than by being cut off mid-pass.
        var settle = _impacted ? HoldDuration + DissipateDuration : 0f;
        var fade = Track(Root.CreateTween());
        fade.TweenInterval(settle);
        fade.TweenProperty(Root, "modulate:a", 0f, FadeDuration);
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
            $"Could not create Gale wind VFX from {ScenePath} and {ShaderPath}: {exception}");
    }

    /// <summary>
    /// Debris outline: a slender leaf, pointed at both ends like the strokes that threw
    /// it. Neither Hail's angular shard nor Blaze's flake, so the fragments still read
    /// as wind at far-field size.
    /// </summary>
    private static Vector2[] DebrisPoints(float radius) =>
    [
        new(radius * 2.1f, 0f),
        new(0f, radius * 0.62f),
        new(-radius * 2.1f, 0f),
        new(0f, -radius * 0.62f)
    ];
}
