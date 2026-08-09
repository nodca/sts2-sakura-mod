using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
/// Hail's ice crystals: one shader-driven crystal per target, driven in two
/// distinguishable hits.
/// </summary>
/// <remarks>
/// A single class deriving from <see cref="CelVfxSession"/> rather than Aqua's
/// outer-static-plus-nested-session pair. <c>TryPrepare</c> is protected, so an
/// outer static class cannot reach it and would have to restate the guard logic —
/// exactly the duplication the shared skeleton exists to prevent.
/// </remarks>
internal sealed class HailIceShardVfx : CelVfxSession
{
    internal const string ScenePath =
        MainFile.ResPath + "/scenes/combat/card_vfx/hail_ice_shard_vfx.tscn";
    internal const string TargetScenePath =
        MainFile.ResPath + "/scenes/combat/card_vfx/hail_ice_shard_target.tscn";
    internal const string ShaderPath =
        MainFile.ResPath + "/shaders/card_vfx/hail_ice_shard.gdshader";

    // Beats, in seconds. The crystal drives in, then the second hit splits it.
    private const float FallDuration = 0.18f;
    private const float CrackDuration = 0.10f;
    private const float ShatterDuration = 0.42f;
    private const float FadeDuration = 0.30f;

    /// <summary>
    /// A hold lasts this long, matching <see cref="CelVfxSession.BeginHold"/> at two
    /// stepped frames. Fracture tweens wait it out: <c>BeginHold</c> freezes shader
    /// time, not Godot's tween clock, so starting them during the hold would show a
    /// motionless crystal throwing moving fragments.
    /// </summary>
    private const float HoldDuration = 2f / 12f;

    // Height the crystal falls from, as a fraction of its own region.
    private const float FallHeightFraction = 1.35f;
    private const int FragmentCount = 6;
    private const float FragmentGravity = 980f;
    private const int VfxZIndex = 3000;

    private static PackedScene? _rootScene;
    private static PackedScene? _targetScene;
    private static bool _loadFailureLogged;

    private readonly Node2D _debris;
    private readonly Dictionary<Creature, ShardVisual> _shards = [];
    private bool _faded;

    private HailIceShardVfx(
        Node2D root,
        NCombatRoom room,
        PackedScene targetScene,
        IReadOnlyList<Creature> creatures)
        : base(root, room)
    {
        _debris = root.GetNode<Node2D>("%Debris");
        var shards = root.GetNode<Node2D>("%Shards");

        for (var index = 0; index < creatures.Count; index++)
        {
            var creature = creatures[index];
            if (_shards.ContainsKey(creature))
                continue;
            var geometry = CelVfxGeometry.Resolve(room, creature, index, Budget);
            _shards.Add(creature, new ShardVisual(targetScene, shards, geometry, index));
        }
    }

    /// <summary>
    /// Ice occupies a narrower, taller region than Aqua's water body: a falling
    /// shard reads as a shard because it is not as wide as the enemy it hits.
    /// </summary>
    private static CelVfxGeometry.GeometryBudget Budget => new(
        HorizontalPadding: 10f,
        VerticalPadding: 18f,
        MinWidth: 120f,
        MinHeight: 150f,
        MaxWidth: 300f,
        MaxHeight: 380f,
        FallbackWidth: 170f,
        FallbackHeight: 200f,
        FloorClearance: 8f,
        MaxViewportWidthFraction: 0.22f,
        MaxViewportHeightFraction: 0.46f);

    protected override IEnumerable<ShaderMaterial> Materials =>
        _shards.Values.Select(static shard => shard.Material);

    /// <summary>
    /// Safety net, not a timer. Worst case is five enemies at two hits each: about
    /// 0.87 s per target serially, plus the shared prelude and the fade. Sized well
    /// clear of that envelope, because a cap set tight becomes a truncation bug.
    /// </summary>
    protected override float MaximumLifetime => 9.0f;

    internal static HailIceShardVfx? TryCreate(IReadOnlyList<Creature> targets, Creature? caster = null)
    {
        ArgumentNullException.ThrowIfNull(targets);
        if (targets.Count == 0)
            return null;
        if (!TryPrepare(
                "Hail ice",
                LoadScenes,
                out var room,
                out _,
                out var scenes))
        {
            return null;
        }

        Node2D? root = null;
        try
        {
            root = scenes.Root.Instantiate<Node2D>();
            root.Name = "SakuraHailIceShardVfx";
            root.ZAsRelative = false;
            root.ZIndex = VfxZIndex;
            room.CombatVfxContainer.AddChildSafely(root);

            var session = new HailIceShardVfx(root, room, scenes.Target, targets);
            // Started after construction, never inside it: the base clock pulls
            // Materials, and during a base constructor the subclass field backing
            // it is still empty.
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

    /// <summary>Shared wand tap, magic circle, and speed lines, then the ice.</summary>
    internal async Task<bool> PlayPrelude(CardModel card, Creature? caster)
    {
        if (!await PlayCelPrelude(card, caster))
            return false;

        var tween = Track(Root.CreateTween().SetParallel());
        var index = 0;
        foreach (var shard in _shards.Values)
        {
            var target = shard;
            tween.TweenMethod(
                    Callable.From<float>(value => target.SetFall(value)),
                    0f,
                    1f,
                    FallDuration)
                .SetDelay(index * 0.04f)
                // Gravity, not a slide: quadratic In is the constant-acceleration
                // curve, so the crystal accelerates into the target.
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Quad);
            index++;
        }

        return await WaitActive(FallDuration + Math.Max(0, _shards.Count - 1) * 0.04f);
    }

    /// <summary>
    /// One hit against one target. The first drives the crystal in and leaves a
    /// fracture network; the second holds a beat, then splits the body along those
    /// same fractures into fragments that fall under gravity.
    /// </summary>
    /// <remarks>
    /// Hit counting is the session's own state. Reading the card's hit loop would
    /// put gameplay state behind a presentation decision.
    /// </remarks>
    internal void Impact(Creature target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (!IsActive() || !_shards.TryGetValue(target, out var shard))
            return;

        shard.Hits++;
        if (shard.Hits == 1)
        {
            Track(shard.CreateCrackTween());
            return;
        }

        if (shard.Hits > 2 || shard.HasShattered)
            return;

        // The hold is the shared signature element: drawn detail freezes for two
        // stepped frames while the speed lines burst, then motion continues from
        // where it stopped rather than jumping forward.
        BeginHold();
        shard.HasShattered = true;
        Track(shard.CreateShatterTween(_debris, HoldDuration));
    }

    /// <summary>
    /// Fades the ice out, then releases. This is the Release beat of the session
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
        var longest = _shards.Values.Any(static shard => shard.HasShattered)
            ? HoldDuration + ShatterDuration
            : FadeDuration;
        var fade = Track(Root.CreateTween());
        fade.TweenInterval(longest * 0.62f);
        fade.TweenProperty(Root, "modulate:a", 0f, longest * 0.38f);
        fade.TweenCallback(Callable.From(Dispose));
    }

    private static (PackedScene Root, PackedScene Target) LoadScenes()
    {
        _rootScene ??= ResourceLoader.Load<PackedScene>(ScenePath, null, ResourceLoader.CacheMode.Reuse)
            ?? throw new InvalidOperationException($"Could not load {ScenePath}.");
        _targetScene ??= ResourceLoader.Load<PackedScene>(TargetScenePath, null, ResourceLoader.CacheMode.Reuse)
            ?? throw new InvalidOperationException($"Could not load {TargetScenePath}.");
        return (_rootScene, _targetScene);
    }

    private static void LogLoadFailure(Exception exception)
    {
        if (_loadFailureLogged)
            return;

        _loadFailureLogged = true;
        MainFile.Logger.Error(
            $"Could not create Hail ice VFX from {ScenePath}, {TargetScenePath}, and {ShaderPath}: {exception}");
    }

    private sealed class ShardVisual
    {
        private readonly Node2D _root;
        private readonly Vector2 _center;
        private readonly Vector2 _size;
        private readonly float _fallHeight;
        private readonly int _index;

        internal ShardVisual(PackedScene scene, Node2D parent, CelVfxGeometry.TargetGeometry geometry, int index)
        {
            _root = scene.Instantiate<Node2D>();
            _root.Name = $"HailIce{index + 1}";
            parent.AddChildSafely(_root);

            _center = geometry.Center;
            _size = geometry.Size;
            _fallHeight = geometry.Size.Y * FallHeightFraction;
            _index = index;

            var body = _root.GetNode<ColorRect>("%ShardBody");
            Material = CelVfxGeometry.DuplicateMaterial(body, $"target {index}");
            Fragments = _root.GetNode<Node2D>("%Fragments");

            // The root never scales. Sizing travels to the shader as region_size,
            // which is what holds ink weight constant in screen pixels across every
            // enemy size.
            _root.Scale = Vector2.One;
            body.Size = geometry.Size;
            body.Position = -geometry.Size * 0.5f;
            Material.SetShaderParameter("region_size", geometry.Size);
            Material.SetShaderParameter("seed", index * 0.317f + 0.19f);
            SetFall(0f);
        }

        internal ShaderMaterial Material { get; }
        internal Node2D Fragments { get; }
        internal int Hits { get; set; }
        internal bool HasShattered { get; set; }

        /// <summary>Descent under gravity, reusing the shared parabola.</summary>
        internal void SetFall(float progress)
        {
            progress = Mathf.Clamp(progress, 0f, 1f);
            Material.SetShaderParameter("formation", Mathf.Min(1f, progress * 2.4f));
            // Solved so the crystal covers exactly _fallHeight over the beat: the
            // shape of the arc is the shared integrator's, the scale is this card's.
            var gravity = 2f * _fallHeight / (FallDuration * FallDuration);
            var offset = CelVfxGeometry.BallisticOffset(Vector2.Zero, gravity, progress * FallDuration);
            _root.GlobalPosition = _center + Vector2.Up * _fallHeight + offset;
        }

        internal Tween CreateCrackTween()
        {
            var tween = _root.CreateTween();
            tween.TweenMethod(
                    Callable.From<float>(value => Material.SetShaderParameter("crack", value)),
                    0f,
                    1f,
                    CrackDuration)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Quad);
            return tween;
        }

        internal Tween CreateShatterTween(Node2D debrisParent, float holdDuration)
        {
            var tween = _root.CreateTween().SetParallel();
            // Widening the same fracture slabs is what separates the body; the
            // fragments below are the pieces it split into. Delayed past the hold so
            // the crystal is moving again before they leave it.
            tween.TweenMethod(
                    Callable.From<float>(value => Material.SetShaderParameter("shatter", value)),
                    0f,
                    1f,
                    ShatterDuration)
                .SetDelay(holdDuration)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Expo);

            for (var i = 0; i < FragmentCount; i++)
            {
                var spread = -0.62f + 1.24f * i / Math.Max(1, FragmentCount - 1);
                var velocity = new Vector2(spread * 210f, -140f - i % 3 * 46f);
                var origin = _center + new Vector2(spread * _size.X * 0.22f, -_size.Y * 0.08f);
                CelVfxGeometry.AddBallisticDebris(
                    tween,
                    debrisParent,
                    FragmentPoints(5.0f + i % 3 * 1.5f, 12f + i % 2 * 4f),
                    i % 2 == 0 ? new Color(0.88f, 0.98f, 1f) : new Color(0.58f, 0.84f, 0.97f),
                    origin,
                    velocity,
                    ShatterDuration,
                    holdDuration,
                    FragmentGravity,
                    1.9f + _index * 0.3f,
                    "HailFragment");
            }
            return tween;
        }

        /// <summary>
        /// Angular fragment outline. Straight edges meeting at sharp corners, so a
        /// piece of ice still reads as ice at far-field size.
        /// </summary>
        private static Vector2[] FragmentPoints(float radius, float height) =>
        [
            new(0f, -height),
            new(radius * 0.92f, -height * 0.22f),
            new(radius * 0.40f, height * 0.78f),
            new(-radius * 0.58f, height * 0.50f),
            new(-radius * 0.84f, -height * 0.34f)
        ];
    }
}
