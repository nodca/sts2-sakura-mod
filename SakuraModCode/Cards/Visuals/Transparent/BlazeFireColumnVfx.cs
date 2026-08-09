using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
/// Blaze's fire column: one shader-driven plume rising from the target's feet.
/// </summary>
/// <remarks>
/// A single class deriving from <see cref="CelVfxSession"/>, like Hail and unlike
/// Aqua's outer-static-plus-nested-session pair. <c>TryPrepare</c> is protected, so
/// an outer static class cannot reach it and would have to restate the guard logic.
/// <para>
/// One scene, not Aqua's root-plus-target split. That split exists so a single
/// <c>BackBufferCopy</c> can serve N target copies; Blaze hits one enemy, so N is
/// always one and splitting would only add boilerplate.
/// </para>
/// </remarks>
internal sealed class BlazeFireColumnVfx : CelVfxSession
{
    internal const string ScenePath =
        MainFile.ResPath + "/scenes/combat/card_vfx/blaze_fire_column_vfx.tscn";
    internal const string ShaderPath =
        MainFile.ResPath + "/shaders/card_vfx/blaze_fire_column.gdshader";

    // Beats, in seconds. Longer than Hail's single-target 0.87 s, which is the
    // point: this is a rare burst card and reads as one.
    private const float IgniteDuration = 0.20f;
    private const float RiseDuration = 0.36f;
    private const float BurnoutDuration = 0.44f;
    private const float FadeDuration = 0.18f;

    /// <summary>
    /// A hold lasts this long, matching <see cref="CelVfxSession.BeginHold"/> at two
    /// stepped frames. Motion tweens wait it out: <c>BeginHold</c> freezes shader
    /// time, not Godot's tween clock, so embers launched during the hold would leave
    /// a motionless column.
    /// </summary>
    private const float HoldDuration = 2f / 12f;

    private const int EmberCount = 9;

    /// <summary>
    /// Embers fall slower than debris. They are light and drag-dominated, so the
    /// arc is lazy rather than rock-like — a parameter of the shared integrator, not
    /// a second integrator.
    /// </summary>
    private const float EmberGravity = 420f;

    private const int VfxZIndex = 3000;

    private static PackedScene? _scene;
    private static bool _loadFailureLogged;

    private readonly ShaderMaterial _material;
    private readonly Node2D _embers;
    private readonly Vector2 _origin;
    private readonly Vector2 _size;
    private bool _impacted;
    private bool _faded;

    private BlazeFireColumnVfx(Node2D root, NCombatRoom room, CelVfxGeometry.TargetGeometry geometry)
        : base(root, room)
    {
        _embers = root.GetNode<Node2D>("%Embers");
        _size = geometry.Size;

        // The column grows upward from the target's feet, so the node origin sits on
        // the base of the region rather than at its centre.
        _origin = geometry.Center + Vector2.Down * geometry.Size.Y * 0.5f;
        root.GlobalPosition = _origin;

        var body = root.GetNode<ColorRect>("%ColumnBody");
        _material = CelVfxGeometry.DuplicateMaterial(body, "fire column");

        // The root never scales. Sizing travels to the shader as region_size, which
        // is what holds ink weight constant in screen pixels across every enemy size.
        root.Scale = Vector2.One;
        body.Size = geometry.Size;
        body.Position = new Vector2(-geometry.Size.X * 0.5f, -geometry.Size.Y);
        _material.SetShaderParameter("region_size", geometry.Size);
        _material.SetShaderParameter("seed", (float)Random.Shared.NextDouble() * 6.1f);
        _material.SetShaderParameter("ignite", 0f);
        _material.SetShaderParameter("rise", 0f);
        _material.SetShaderParameter("burnout", 0f);
    }

    /// <summary>
    /// A column: narrower than Aqua's water body and far taller than Hail's shard.
    /// The vertical fraction is the loosest of the three because a plume that does
    /// not clear the enemy's head does not read as a pillar of fire.
    /// </summary>
    private static CelVfxGeometry.GeometryBudget Budget => new(
        HorizontalPadding: 14f,
        VerticalPadding: 26f,
        MinWidth: 130f,
        MinHeight: 300f,
        MaxWidth: 260f,
        MaxHeight: 520f,
        FallbackWidth: 190f,
        FallbackHeight: 420f,
        FloorClearance: 6f,
        MaxViewportWidthFraction: 0.20f,
        MaxViewportHeightFraction: 0.62f);

    protected override IEnumerable<ShaderMaterial> Materials => [_material];

    /// <summary>
    /// Safety net, not a timer. One target, no hit loop: the worst case is the beat
    /// chain running end to end at about 1.95 s including the shared prelude. Sized
    /// well clear of that, because a cap set tight becomes a truncation bug.
    /// </summary>
    protected override float MaximumLifetime => 6.0f;

    internal static BlazeFireColumnVfx? TryCreate(Creature target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (!TryPrepare("Blaze fire", LoadScene, out var room, out _, out var scene))
            return null;

        Node2D? root = null;
        try
        {
            root = scene.Instantiate<Node2D>();
            root.Name = "SakuraBlazeFireColumnVfx";
            root.ZAsRelative = false;
            root.ZIndex = VfxZIndex;
            room.CombatVfxContainer.AddChildSafely(root);

            var geometry = CelVfxGeometry.Resolve(room, target, 0, Budget);
            var session = new BlazeFireColumnVfx(root, room, geometry);
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
    /// Shared wand tap, magic circle, and speed lines, then the column ignites and
    /// climbs.
    /// </summary>
    /// <remarks>
    /// Each beat gets its own tracked Tween. Everything after an await that yields to
    /// the scene tree is a new phase, and a Tween that has already started rejects
    /// further tweeners — appending would throw inside the awaited card action.
    /// </remarks>
    internal async Task<bool> PlayPrelude(CardModel card, Creature? caster)
    {
        if (!await PlayCelPrelude(card, caster))
            return false;

        var ignite = Track(Root.CreateTween());
        ignite.TweenMethod(
                Callable.From<float>(value => _material.SetShaderParameter("ignite", value)),
                0f,
                1f,
                IgniteDuration)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Quad);
        if (!await WaitActive(IgniteDuration))
            return false;

        var rise = Track(Root.CreateTween());
        rise.TweenMethod(
                Callable.From<float>(value => _material.SetShaderParameter("rise", value)),
                0f,
                1f,
                RiseDuration)
            // Buoyant acceleration: the plume starts slow and gains speed as it
            // climbs, which quadratic In is the constant-acceleration curve for.
            .SetEase(Tween.EaseType.In)
            .SetTrans(Tween.TransitionType.Quad);
        return await WaitActive(RiseDuration);
    }

    /// <summary>
    /// The hit beat: hold two stepped frames while the speed lines burst, then throw
    /// embers and let the column burn out.
    /// </summary>
    /// <remarks>
    /// No <c>Creature</c> parameter. Blaze targets one enemy, so an argument could
    /// only ever equal the target this session was built around, and taking it would
    /// invite a "what if they disagree" branch that has no answer.
    /// </remarks>
    internal void Impact()
    {
        if (_impacted || !IsActive())
            return;

        _impacted = true;

        // The hold is the shared signature element: drawn detail freezes for two
        // stepped frames, then motion continues from where it stopped.
        BeginHold();

        var tween = Track(Root.CreateTween().SetParallel());
        tween.TweenMethod(
                Callable.From<float>(value => _material.SetShaderParameter("burnout", value)),
                0f,
                1f,
                BurnoutDuration)
            .SetDelay(HoldDuration)
            .SetEase(Tween.EaseType.In)
            .SetTrans(Tween.TransitionType.Quad);

        for (var i = 0; i < EmberCount; i++)
        {
            var spread = -0.5f + 1f * i / Math.Max(1, EmberCount - 1);
            // Upward initial velocity against positive gravity: the ember rises,
            // slows, turns over, and falls. Rock-like straight throws are what a
            // downward-only initial velocity would give.
            var velocity = new Vector2(spread * 170f, -300f - i % 3 * 70f);
            var origin = new Vector2(
                _origin.X + spread * _size.X * 0.30f,
                _origin.Y - _size.Y * (0.20f + i % 4 * 0.14f));
            CelVfxGeometry.AddBallisticDebris(
                tween,
                _embers,
                EmberPoints(3.4f + i % 3 * 1.1f),
                i % 3 == 0 ? new Color(1f, 0.94f, 0.60f) : new Color(0.97f, 0.53f, 0.15f),
                origin,
                velocity,
                BurnoutDuration,
                HoldDuration,
                EmberGravity,
                2.6f + i * 0.2f,
                "BlazeEmber");
        }
    }

    /// <summary>
    /// Fades the fire out, then releases. This is the Release beat of the session
    /// contract; the base <c>Dispose</c> it ends in is idempotent and also covers
    /// combat end, tree exit, exceptions, and the lifetime cap.
    /// </summary>
    internal void FadeAndDispose()
    {
        if (_faded || !IsActive())
        {
            Dispose();
            return;
        }

        _faded = true;
        // Wait out the burnout the impact beat started, so the column dies by
        // collapsing rather than by being cut off mid-rise.
        var settle = _impacted ? HoldDuration + BurnoutDuration : 0f;
        var fade = Track(Root.CreateTween());
        fade.TweenInterval(settle);
        fade.TweenProperty(Root, "modulate:a", 0f, FadeDuration);
        fade.TweenCallback(Callable.From(Dispose));
    }

    private static PackedScene LoadScene() =>
        _scene ??= ResourceLoader.Load<PackedScene>(ScenePath, null, ResourceLoader.CacheMode.Reuse)
            ?? throw new InvalidOperationException($"Could not load {ScenePath}.");

    private static void LogLoadFailure(Exception exception)
    {
        if (_loadFailureLogged)
            return;

        _loadFailureLogged = true;
        MainFile.Logger.Error(
            $"Could not create Blaze fire VFX from {ScenePath} and {ShaderPath}: {exception}");
    }

    /// <summary>
    /// Ember outline: a small tapered flake, not the angular shard Hail throws. Even
    /// at far-field size the debris silhouette should not read as ice.
    /// </summary>
    private static Vector2[] EmberPoints(float radius) =>
    [
        new(0f, -radius * 1.8f),
        new(radius * 0.78f, 0f),
        new(0f, radius * 1.15f),
        new(-radius * 0.78f, 0f)
    ];
}
