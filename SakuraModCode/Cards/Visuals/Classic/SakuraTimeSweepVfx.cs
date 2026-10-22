using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
/// Sakura Time's on-play sweep: a pink star forms in front of Sakura and crosses
/// the enemy line trailing gold sand. Each enemy's Time Stop lands as the star
/// passes it, and <see cref="TimeFreezeVisual"/> takes over from there.
/// </summary>
internal sealed class SakuraTimeSweepVfx : CelVfxSession
{
    internal const string ShaderPath = MainFile.ResPath + "/shaders/card_vfx/sakura_time_sweep.gdshader";
    internal static IReadOnlyList<string> AssetPaths { get; } = [ShaderPath];

    private const int VfxZIndex = 1;
    private const float FallbackCasterOffset = 0.25f;
    private const float FadeSeconds = 0.30f;

    private static bool _loadFailureLogged;

    private readonly IReadOnlyList<Creature> _targets;
    private readonly Timeline _timeline;
    private readonly ShaderMaterial _material;

    private float _t;
    private bool _bodyStarted;
    private bool _faded;

    private SakuraTimeSweepVfx(Node2D root, NCombatRoom room, IReadOnlyList<Creature> targets,
        Timeline timeline, ShaderMaterial material)
        : base(root, room)
    {
        _targets = targets;
        _timeline = timeline;
        _material = material;
    }

    protected override IEnumerable<ShaderMaterial> Materials => [_material];

    // Prelude, the sweep, slow gameplay between applications, and the fade.
    protected override float MaximumLifetime => 5.0f;

    // --- Layout -----------------------------------------------------------------

    /// <summary>Where the star travels and when it passes each target.</summary>
    internal readonly record struct Timeline(
        Vector2 Start,
        Vector2 End,
        float Arc,
        float FormSeconds,
        float SweepSeconds,
        IReadOnlyList<float> Arrivals)
    {
        internal const float Form = 0.12f;
        internal const float BaseSweep = 0.45f;
        internal const float PerTarget = 0.12f;
        private const float Overshoot = 150f;
        private const float EdgeMargin = 40f;

        internal float End0 => FormSeconds + SweepSeconds;

        /// <param name="start">The star's birth point in front of the caster.</param>
        /// <param name="targets">Each target's centre, in the same order as gameplay applies them.</param>
        internal static Timeline For(Vector2 start, IReadOnlyList<Vector2> targets, float viewLeft, float viewRight)
        {
            if (targets.Count == 0)
            {
                var lone = start + new Vector2(Overshoot, 0f);
                return new Timeline(start, lone, 0f, Form, BaseSweep, []);
            }

            var facing = targets.Average(static t => t.X) >= start.X ? 1f : -1f;
            var farthest = facing > 0f ? targets.Max(static t => t.X) : targets.Min(static t => t.X);
            var endX = Math.Clamp(farthest + facing * Overshoot, viewLeft + EdgeMargin, viewRight - EdgeMargin);
            var end = new Vector2(endX, targets.Average(static t => t.Y));
            var span = endX - start.X;
            var sweep = BaseSweep + PerTarget * targets.Count;
            // The arc lifts the middle of the path over the enemies' heads a little.
            var arc = Math.Clamp(Math.Abs(span) * 0.06f, 20f, 70f);
            var arrivals = targets
                .Select(t => Math.Abs(span) < 1f ? 0f : Math.Clamp((t.X - start.X) / span, 0f, 1f))
                .Select(u => Form + sweep * u)
                .ToArray();
            return new Timeline(start, end, arc, Form, sweep, arrivals);
        }
    }

    // --- Orchestration ----------------------------------------------------------

    internal static Task PlayOrResolveAsync(
        CardModel card,
        Creature? caster,
        IReadOnlyList<Creature> targets,
        Func<Cues, Task> resolveGameplay)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(resolveGameplay);

        return CelVfxSession.PlayOrResolveAsync(
            "Sakura Time sweep",
            () => TryCreate(caster, targets),
            session => session.PlayPrelude(card, caster),
            scope => resolveGameplay(new Cues(scope)),
            session => session.FadeAndDispose(),
            session => session.Dispose());
    }

    internal sealed class Cues(CueScope<SakuraTimeSweepVfx> scope)
    {
        /// <summary>
        /// Waits until the star passes <paramref name="enemy"/>. Await immediately
        /// before that enemy's Time Stop.
        /// </summary>
        /// <remarks>
        /// Returns at once when presentation is off, failed, or already past that
        /// enemy, so the wait exists only while the star is actually moving.
        /// </remarks>
        internal Task ReachAsync(Creature enemy)
        {
            ArgumentNullException.ThrowIfNull(enemy);
            return scope.InvokeAsync("reach", session => session.ReachAsync(enemy));
        }
    }

    private static SakuraTimeSweepVfx? TryCreate(Creature? caster, IReadOnlyList<Creature> targets)
    {
        if (!TryPrepare("Sakura Time sweep", LoadShader, out var room, out _, out var shader))
            return null;

        Node2D? root = null;
        try
        {
            var view = room.CombatVfxContainer.GetViewportRect();
            var centers = targets
                .Select((target, index) => CelVfxGeometry.Resolve(room, target, index, Budget).Center)
                .ToArray();

            Vector2 start;
            if (caster is not null
                && CelVfxGeometry.ResolveCaster(room.GetCreatureNode(caster)) is { } anchor)
            {
                // In front of her chest, toward the enemies.
                start = anchor.BodyCenter + new Vector2(anchor.FacingSign * anchor.BodySize.X * 0.55f,
                    -anchor.BodySize.Y * 0.12f);
            }
            else
            {
                var enemiesRight = centers.Length == 0 || centers.Average(static c => c.X) >= view.GetCenter().X;
                start = new Vector2(
                    enemiesRight
                        ? view.Position.X + view.Size.X * FallbackCasterOffset
                        : view.End.X - view.Size.X * FallbackCasterOffset,
                    view.Position.Y + view.Size.Y * 0.55f);
            }

            var timeline = Timeline.For(start, centers, view.Position.X, view.End.X);

            root = new Node2D
            {
                Name = "SakuraTimeSweepVfx",
                ZAsRelative = false,
                ZIndex = VfxZIndex
            };
            room.CombatVfxContainer.AddChildSafely(root);
            root.GlobalPosition = Vector2.Zero;
            var material = new ShaderMaterial { Shader = shader };
            var rect = new ColorRect
            {
                Name = "Sweep",
                Color = Colors.White,
                Material = material,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Visible = false,
                Position = view.Position,
                Size = view.Size
            };
            root.AddChild(rect);
            material.SetShaderParameter("region_size", view.Size);
            material.SetShaderParameter("path_start", timeline.Start - view.Position);
            material.SetShaderParameter("path_end", timeline.End - view.Position);
            material.SetShaderParameter("arc", timeline.Arc);
            material.SetShaderParameter("head", 0f);
            material.SetShaderParameter("form", 0f);
            material.SetShaderParameter("fade", 0f);

            var session = new SakuraTimeSweepVfx(root, room, targets, timeline, material);
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
        Root.GetNode<ColorRect>("Sweep").Visible = true;
        Advance(0f);
        // One tween owns body time; configured fully before any yield.
        var body = Track(Root.CreateTween());
        body.TweenMethod(Callable.From<float>(Advance), 0f, _timeline.End0, _timeline.End0);
    }

    // --- Cues -------------------------------------------------------------------

    private async Task ReachAsync(Creature enemy)
    {
        var index = IndexOf(enemy);
        if (index < 0 || !_bodyStarted || _faded || !IsActive())
            return;

        var wait = _timeline.Arrivals[index] - _t;
        if (wait > 0f)
            await WaitActive(wait);
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

    // --- Frame ------------------------------------------------------------------

    private void Advance(float t)
    {
        _t = t;
        _material.SetShaderParameter("form", Math.Clamp(t / _timeline.FormSeconds, 0f, 1f));
        _material.SetShaderParameter("head",
            Math.Clamp((t - _timeline.FormSeconds) / _timeline.SweepSeconds, 0f, 1f));
    }

    /// <summary>
    /// Gameplay is done. The star finishes its pass to the far side of the enemy
    /// line, then the trail and star fade together.
    /// </summary>
    private void FadeAndDispose()
    {
        if (_faded || !_bodyStarted || !IsActive())
        {
            Dispose();
            return;
        }

        _faded = true;
        var fade = Track(Root.CreateTween());
        var remaining = _timeline.End0 - _t;
        if (remaining > 0f)
            fade.TweenInterval(remaining);
        // TweenMethod with explicit ends, never TweenProperty on a shader
        // uniform (Silent's Nil-uniform rule).
        fade.TweenMethod(Callable.From<float>(value => _material.SetShaderParameter("fade", value)),
            0f, 1f, FadeSeconds);
        fade.TweenCallback(Callable.From(Dispose));
    }

    private static Shader LoadShader() =>
        PreloadManager.Cache.GetAsset<Shader>(ShaderPath);

    private static void LogLoadFailure(Exception exception)
    {
        if (_loadFailureLogged)
            return;

        _loadFailureLogged = true;
        MainFile.Logger.Error($"Could not create Sakura Time sweep VFX from {ShaderPath}: {exception}");
    }
}
