using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
/// Shade's dragon, seen only as its shadow: it spreads out of Sakura's own shadow,
/// opens its wings on the floor (Block), slithers under each enemy's feet (Weak),
/// then either turns and sinks back into her shadow (Extra) or breaks into ink.
/// </summary>
/// <remarks>
/// Every shape is built in the floor plane by <see cref="ShadeGroundDragonShape"/>,
/// drawn white into a transparent <see cref="SubViewport"/>, and composited by
/// <c>shade_ground_dragon.gdshader</c> into a soft ink body with a water-blue glow.
/// The whole effect sits in <c>BackCombatVfxContainer</c>, under the creatures,
/// because it is on the ground.
/// </remarks>
internal sealed class ShadeGroundDragonVfx : CelVfxSession
{
    internal const string ScenePath =
        MainFile.ResPath + "/scenes/combat/card_vfx/shade_ground_dragon_vfx.tscn";
    internal const string ShaderPath =
        MainFile.ResPath + "/shaders/card_vfx/shade_ground_dragon.gdshader";
    internal static IReadOnlyList<string> AssetPaths { get; } = [ScenePath];

    private const float BodyClockSpan = 5.6f;
    // Screen span the mask must cover around the floor line: the cast profile
    // reaches about 260 px up the floor (raised wing tips), and the homeward lane
    // with the hanging claws about 175 px below it.
    internal const float BandAboveFloor = 300f;
    internal const float BandHeight = 540f;
    private const float FallbackCasterOffset = 0.25f;

    private static bool _loadFailureLogged;

    private readonly IReadOnlyList<Creature> _targets;
    private readonly ShadeGroundDragonShape.Layout _layout;
    private readonly ShadeGroundDragonShape.Timeline _timeline;
    private readonly float[] _enemyXs;
    private readonly SubViewport _viewport;
    private readonly Sprite2D _composite;
    private readonly ShaderMaterial _material;
    private readonly ShadeGroundDragonMask _mask;
    private readonly ShadeGroundDragonRipples _ripples;
    private readonly float _seed;

    private float _t;
    private bool _bodyStarted;
    private bool _faded;

    private ShadeGroundDragonVfx(
        Node2D root,
        NCombatRoom room,
        IReadOnlyList<Creature> targets,
        float[] enemyXs,
        ShadeGroundDragonShape.Layout layout,
        float floorY,
        Rect2 band)
        : base(root, room)
    {
        _targets = targets;
        _enemyXs = enemyXs;
        _layout = layout;
        _timeline = ShadeGroundDragonShape.TimelineFor(layout);
        _seed = (float)Random.Shared.NextDouble() * 6.1f;

        _viewport = root.GetNode<SubViewport>("%MaskViewport");
        _viewport.Size = new Vector2I(Mathf.CeilToInt(band.Size.X), Mathf.CeilToInt(band.Size.Y));
        _mask = new ShadeGroundDragonMask
        {
            Name = "ShadeMask",
            // Floor plane → mask pixels: shift to the band, squash depth.
            Position = new Vector2(-band.Position.X, floorY - band.Position.Y),
            Scale = new Vector2(1f, ShadeGroundDragonShape.Projection)
        };
        _viewport.AddChild(_mask);

        _composite = root.GetNode<Sprite2D>("%Composite");
        _material = CelVfxGeometry.DuplicateMaterial(_composite, "Shade ground dragon");
        _material.SetShaderParameter("opacity", 0f);
        _composite.Texture = _viewport.GetTexture();
        _composite.GlobalPosition = band.Position;

        _ripples = new ShadeGroundDragonRipples
        {
            Name = "ShadeRipples",
            Position = new Vector2(0f, floorY),
            Scale = new Vector2(1f, ShadeGroundDragonShape.Projection)
        };
        root.AddChild(_ripples);
    }

    protected override IEnumerable<ShaderMaterial> Materials => [_material];

    // Prelude, the slither, slow gameplay between Weak applications, and the swim home.
    protected override float MaximumLifetime => 6.0f;

    // --- Orchestration --------------------------------------------------------

    internal static Task PlayOrResolveAsync(
        CardModel card,
        Creature? caster,
        IReadOnlyList<Creature> targets,
        bool extra,
        Func<Cues, Task> resolveGameplay)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(resolveGameplay);

        return CelVfxSession.PlayOrResolveAsync(
            "Shade ground dragon",
            () => TryCreate(caster, targets, extra),
            session => session.PlayPrelude(card, caster),
            scope => resolveGameplay(new Cues(scope)),
            session => session.FadeAndDispose(),
            session => session.Dispose());
    }

    internal sealed class Cues(CueScope<ShadeGroundDragonVfx> scope)
    {
        /// <summary>
        /// Waits until the wings are open on the floor. Await immediately before Block.
        /// </summary>
        internal Task SpreadAsync() =>
            scope.InvokeAsync("spread", session => session.WaitUntilAsync(ShadeGroundDragonShape.SpreadCue));

        /// <summary>
        /// Waits until the head passes under <paramref name="enemy"/>. Await
        /// immediately before that enemy's Weak.
        /// </summary>
        /// <remarks>
        /// Returns at once when presentation is off, failed, or already past that
        /// enemy, so the wait exists only while the shadow is actually moving.
        /// </remarks>
        internal Task ReachAsync(Creature enemy)
        {
            ArgumentNullException.ThrowIfNull(enemy);
            return scope.InvokeAsync("reach", session => session.ReachAsync(enemy));
        }
    }

    private static ShadeGroundDragonVfx? TryCreate(Creature? caster, IReadOnlyList<Creature> targets, bool extra)
    {
        if (!TryPrepare("Shade ground dragon", LoadScene, out var room, out _, out var scene))
            return null;

        Node2D? root = null;
        try
        {
            var viewport = room.CombatVfxContainer.GetViewportRect();
            var enemyXs = targets
                .Select((target, index) => CelVfxGeometry.Resolve(room, target, index, Budget).Center.X)
                .ToArray();

            float casterX;
            float floorY;
            if (caster is not null
                && CelVfxGeometry.ResolveCaster(room.GetCreatureNode(caster)) is { } anchor)
            {
                casterX = anchor.Floor.X;
                floorY = anchor.Floor.Y;
            }
            else
            {
                // No caster node: a stand-in on the far side of the arena from the enemies.
                var enemiesRight = enemyXs.Length == 0 || enemyXs.Average() >= viewport.GetCenter().X;
                casterX = enemiesRight
                    ? viewport.Position.X + viewport.Size.X * FallbackCasterOffset
                    : viewport.End.X - viewport.Size.X * FallbackCasterOffset;
                floorY = viewport.Position.Y + viewport.Size.Y * 0.74f;
            }

            var facing = enemyXs.Length == 0 || enemyXs.Average() >= casterX ? 1f : -1f;
            var layout = ShadeGroundDragonShape.For(casterX, facing, enemyXs, viewport.Position.X, viewport.End.X, extra);
            var band = new Rect2(viewport.Position.X, floorY - BandAboveFloor, viewport.Size.X, BandHeight);

            root = scene.Instantiate<Node2D>();
            root.Name = "SakuraShadeGroundDragonVfx";
            root.ZAsRelative = true;
            root.ZIndex = 0;
            room.BackCombatVfxContainer.AddChildSafely(root);
            root.GlobalPosition = Vector2.Zero;

            var session = new ShadeGroundDragonVfx(root, room, targets, enemyXs, layout, floorY, band);
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
        MinWidth: 80f,
        MinHeight: 120f,
        MaxWidth: 420f,
        MaxHeight: 520f,
        FallbackWidth: 200f,
        FallbackHeight: 300f,
        FloorClearance: 0f);

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
        _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
        _composite.Visible = true;
        _material.SetShaderParameter("opacity", 1f);
        Advance(0f);
        // One tween owns body time for every layer; configured fully before any yield.
        var body = Track(Root.CreateTween());
        body.TweenMethod(Callable.From<float>(Advance), 0f, BodyClockSpan, BodyClockSpan);
    }

    // --- Cues -----------------------------------------------------------------

    private async Task WaitUntilAsync(float at)
    {
        if (!_bodyStarted || _faded || !IsActive())
            return;

        var wait = at - _t;
        if (wait > 0f)
            await WaitActive(wait);
    }

    private Task ReachAsync(Creature enemy)
    {
        var index = IndexOf(enemy);
        return index < 0 ? Task.CompletedTask : WaitUntilAsync(_timeline.Arrivals[index]);
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
        _mask.Show(t, _layout, _timeline, _seed);
        _ripples.Show(t, _enemyXs, _timeline);
    }

    /// <summary>
    /// Gameplay is done. The shadow is never held by it, so only the unplayed part
    /// of the timeline (the swim home or the ink scatter) is left to run.
    /// </summary>
    private void FadeAndDispose()
    {
        if (_faded || !_bodyStarted || !IsActive())
        {
            Dispose();
            return;
        }

        _faded = true;
        var remaining = _timeline.End - _t;
        if (remaining <= 0f)
        {
            Dispose();
            return;
        }

        var fadeSeconds = Math.Min(ShadeGroundDragonShape.FadeDuration, remaining);
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
            $"Could not create Shade ground dragon VFX from {ScenePath} and {ShaderPath}: {exception}");
    }
}
