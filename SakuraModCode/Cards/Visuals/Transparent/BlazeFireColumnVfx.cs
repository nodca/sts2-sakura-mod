using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
/// Blaze gathers at the target's feet, erupts, then lifts away as curling fire.
/// </summary>
internal sealed class BlazeFireColumnVfx : CelVfxSession
{
    internal const string ScenePath =
        MainFile.ResPath + "/scenes/combat/card_vfx/blaze_fire_column_vfx.tscn";
    internal const string ShaderPath =
        MainFile.ResPath + "/shaders/card_vfx/blaze_fire_column.gdshader";
    internal static IReadOnlyList<string> AssetPaths { get; } = [ScenePath];

    private const float IgniteDuration = 0.15f;
    private const float RiseDuration = 0.20f;
    private const float PeakDuration = 0.05f;
    private const float BurnoutDuration = 0.44f;
    private const float FadeDuration = 0.06f;
    private const int EmberCount = 7;
    private const float EmberGravity = 90f;

    private static bool _loadFailureLogged;

    private readonly ShaderMaterial _material;
    private readonly Node2D _embers;
    private readonly Vector2 _origin;
    private readonly Vector2 _size;
    private bool _impacted;
    private bool _faded;
    private float _impactAt;

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

        // Size the draw region directly, preserving the screen-pixel heat budget.
        root.Scale = Vector2.One;
        body.Size = geometry.Size;
        body.Position = new Vector2(-geometry.Size.X * 0.5f, -geometry.Size.Y);
        _material.SetShaderParameter("region_size", geometry.Size);
        _material.SetShaderParameter("seed", (float)Random.Shared.NextDouble() * 6.1f);
        _material.SetShaderParameter("ignite", 0f);
        _material.SetShaderParameter("rise", 0f);
        _material.SetShaderParameter("burnout", 0f);
        _material.SetShaderParameter("impact_at", -10f);
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

    // The cap also covers gameplay taking longer than the visual beat chain.
    protected override float MaximumLifetime => 6.0f;

    private float Elapsed => _material.GetShaderParameter("elapsed").AsSingle();

    /// <summary>Only the unplayed part of the hit's tail survives gameplay resolution.</summary>
    internal static float ReleaseSeconds(float elapsed, float impactAt) => Math.Clamp(
        PeakDuration + BurnoutDuration + FadeDuration - Math.Max(0f, elapsed - impactAt),
        0f,
        PeakDuration + BurnoutDuration + FadeDuration);

    internal static Task PlayOrResolveAsync(
        CardModel card,
        Creature? caster,
        Creature target,
        Func<Cues, Task> resolveGameplay)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(resolveGameplay);

        return CelVfxSession.PlayOrResolveAsync(
            "Blaze fire",
            () => TryCreate(target),
            session => session.PlayPrelude(card, caster),
            scope => resolveGameplay(new Cues(scope)),
            session => session.FadeAndDispose(),
            session => session.Dispose());
    }

    internal sealed class Cues(CueScope<BlazeFireColumnVfx> scope)
    {
        internal void Impact() => scope.Invoke("impact", static session => session.Impact());
    }

    private static BlazeFireColumnVfx? TryCreate(Creature target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (!TryPrepare("Blaze fire", LoadScene, out var room, out _, out var scene))
            return null;

        Node2D? root = null;
        try
        {
            root = scene.Instantiate<Node2D>();
            root.Name = "SakuraBlazeFireColumnVfx";
            root.ZAsRelative = true;
            root.ZIndex = 0;
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
    private async Task<bool> PlayPrelude(CardModel card, Creature? caster)
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
            // The shader opens the core faster than the outer tongues from this
            // continuous phase. One linear input keeps those two curves aligned.
            .SetTrans(Tween.TransitionType.Linear);
        return await WaitActive(RiseDuration);
    }

    /// <summary>
    /// Damage and the local brightness accent start together; live fire keeps moving.
    /// </summary>
    /// <remarks>
    /// No <c>Creature</c> parameter. Blaze targets one enemy, so an argument could
    /// only ever equal the target this session was built around, and taking it would
    /// invite a "what if they disagree" branch that has no answer.
    /// </remarks>
    private void Impact()
    {
        if (_impacted || _faded || !IsActive())
            return;

        _impacted = true;
        _impactAt = Elapsed;
        _material.SetShaderParameter("impact_at", _impactAt);
        _material.SetShaderParameter("ignite", 1f);
        _material.SetShaderParameter("rise", 1f);

        var tween = Track(Root.CreateTween().SetParallel());
        tween.TweenMethod(
                Callable.From<float>(value => _material.SetShaderParameter("burnout", value)),
                0f,
                1f,
                BurnoutDuration)
            .SetDelay(PeakDuration)
            .SetTrans(Tween.TransitionType.Linear);

        for (var i = 0; i < EmberCount; i++)
        {
            var spread = -0.5f + 1f * i / Math.Max(1, EmberCount - 1);
            // These expire while still rising. The shared integrator handles the
            // small deceleration without adding a second particle system.
            var velocity = new Vector2(spread * 150f, -125f - i % 3 * 35f);
            var origin = new Vector2(
                _origin.X + spread * _size.X * 0.30f,
                _origin.Y - _size.Y * (0.20f + i % 4 * 0.14f));
            var ember = CelVfxGeometry.AddBallisticDebris(
                tween,
                _embers,
                EmberPoints(2.1f + i % 3 * 0.65f),
                i % 3 == 0 ? new Color(1f, 0.95f, 0.69f) : new Color(1f, 0.48f, 0.12f),
                origin,
                velocity,
                BurnoutDuration,
                PeakDuration,
                EmberGravity,
                -0.6f + i * 0.2f,
                "BlazeEmber",
                zIndex: 1);
            ember.ZAsRelative = true;
        }
    }

    /// <summary>
    /// Fades the fire out, then releases. This is the Release beat of the session
    /// contract; the base <c>Dispose</c> it ends in is idempotent and also covers
    /// combat end, tree exit, exceptions, and the lifetime cap.
    /// </summary>
    private void FadeAndDispose()
    {
        if (_faded || !IsActive())
        {
            Dispose();
            return;
        }

        _faded = true;
        var remaining = _impacted ? ReleaseSeconds(Elapsed, _impactAt) : FadeDuration;
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
