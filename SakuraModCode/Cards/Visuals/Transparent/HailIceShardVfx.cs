using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>One heavy ice block per target; its first hit cracks and breaks it.</summary>
internal sealed class HailIceShardVfx : CelVfxSession
{
    internal const string ScenePath = MainFile.ResPath + "/scenes/combat/card_vfx/hail_ice_shard_vfx.tscn";
    internal const string TargetScenePath = MainFile.ResPath + "/scenes/combat/card_vfx/hail_ice_shard_target.tscn";
    internal const string ShaderPath = MainFile.ResPath + "/shaders/card_vfx/hail_ice_shard.gdshader";
    internal static IReadOnlyList<string> AssetPaths { get; } = [ScenePath, TargetScenePath];

    private const float FallDuration = 0.17f;
    private const float ContactDuration = 0.035f;
    private const float ShatterDuration = 0.40f;
    private const float FadeDuration = 0.08f;
    private const float FallHeightFraction = 1.10f;
    private const float FragmentGravity = 1600f;
    private const int GrainCount = 8;
    private static bool _loadFailureLogged;

    private readonly Node2D _debris;
    private readonly Dictionary<Creature, ShardVisual> _shards = [];
    private bool _faded;
    private float _lastHitAt = -10f;

    private HailIceShardVfx(Node2D root, NCombatRoom room, PackedScene targetScene, IReadOnlyList<Creature> creatures)
        : base(root, room)
    {
        _debris = root.GetNode<Node2D>("%Debris");
        var shards = root.GetNode<Node2D>("%Shards");
        for (var index = 0; index < creatures.Count; index++)
        {
            var creature = creatures[index];
            if (!_shards.ContainsKey(creature))
                _shards.Add(creature, new ShardVisual(targetScene, shards,
                    CelVfxGeometry.Resolve(room, creature, index, Budget), index));
        }
    }

    private static CelVfxGeometry.GeometryBudget Budget => new(
        HorizontalPadding: 10f, VerticalPadding: 18f,
        MinWidth: 120f, MinHeight: 150f, MaxWidth: 300f, MaxHeight: 380f,
        FallbackWidth: 170f, FallbackHeight: 200f, FloorClearance: 8f,
        MaxViewportWidthFraction: 0.22f, MaxViewportHeightFraction: 0.46f);

    protected override IEnumerable<ShaderMaterial> Materials => _shards.Values.Select(static shard => shard.Material);
    protected override float MaximumLifetime => 9f;
    private float Elapsed => _shards.Values.First().Material.GetShaderParameter("elapsed").AsSingle();

    internal static float ReleaseSeconds(float elapsed, float lastHitAt) =>
        Math.Clamp(ContactDuration + ShatterDuration - Math.Max(0f, elapsed - lastHitAt),
            0f, ContactDuration + ShatterDuration) + FadeDuration;

    internal static Task PlayOrResolveAsync(CardModel card, Creature? caster, IReadOnlyList<Creature> targets,
        Func<Cues, Task> resolveGameplay)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(resolveGameplay);
        return CelVfxSession.PlayOrResolveAsync(
            "Hail ice", () => TryCreate(targets), session => session.PlayPrelude(card, caster),
            scope => resolveGameplay(new Cues(scope)), session => session.FadeAndDispose(), session => session.Dispose());
    }

    internal sealed class Cues(CueScope<HailIceShardVfx> scope)
    {
        internal void Impact(Creature target)
        {
            ArgumentNullException.ThrowIfNull(target);
            scope.Invoke("impact", session => session.Impact(target));
        }
    }

    private static HailIceShardVfx? TryCreate(IReadOnlyList<Creature> targets)
    {
        if (targets.Count == 0 || !TryPrepare("Hail ice", LoadScenes, out var room, out _, out var scenes))
            return null;
        Node2D? root = null;
        try
        {
            root = scenes.Root.Instantiate<Node2D>();
            root.Name = "SakuraHailIceShardVfx";
            root.ZAsRelative = true;
            root.ZIndex = 0;
            room.CombatVfxContainer.AddChildSafely(root);
            var session = new HailIceShardVfx(root, room, scenes.Target, targets);
            session.StartClock();
            return session;
        }
        catch (Exception exception)
        {
            if (!_loadFailureLogged)
            {
                _loadFailureLogged = true;
                MainFile.Logger.Error($"Could not create Hail ice VFX from {ScenePath}: {exception}");
            }
            root?.QueueFreeSafely();
            return null;
        }
    }

    private async Task<bool> PlayPrelude(CardModel card, Creature? caster)
    {
        if (!await PlayCelPrelude(card, caster))
            return false;
        var tween = Track(Root.CreateTween().SetParallel());
        var index = 0;
        foreach (var shard in _shards.Values)
        {
            var target = shard;
            // The shared parabola already supplies acceleration; a quadratic
            // tween here would apply it twice and turn the descent into t^4.
            tween.TweenMethod(Callable.From<float>(target.SetFall), 0f, 1f, FallDuration)
                .SetDelay(Math.Min(index++, 4) * 0.025f)
                .SetTrans(Tween.TransitionType.Linear);
        }
        return await WaitActive(FallDuration + Math.Min(_shards.Count - 1, 4) * 0.025f);
    }

    private void Impact(Creature target)
    {
        if (_faded || !IsActive() || !_shards.TryGetValue(target, out var shard) || shard.HasShattered)
            return;
        _lastHitAt = Elapsed;
        shard.HasShattered = true;
        shard.SetFall(1f);
        shard.Material.SetShaderParameter("crack", 1f);
        Track(shard.CreateShatterTween(_debris));
    }

    private void FadeAndDispose()
    {
        if (_faded || !IsActive())
        {
            Dispose();
            return;
        }
        _faded = true;
        var settle = ReleaseSeconds(Elapsed, _lastHitAt) - FadeDuration;
        var fade = Track(Root.CreateTween());
        if (settle > 0f)
            fade.TweenInterval(settle);
        fade.TweenProperty(Root, "modulate:a", 0f, FadeDuration);
        fade.TweenCallback(Callable.From(Dispose));
    }

    private static (PackedScene Root, PackedScene Target) LoadScenes() =>
        (PreloadManager.Cache.GetScene(ScenePath), PreloadManager.Cache.GetScene(TargetScenePath));

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
            // Extra canvas admits separated pieces while region_size keeps the
            // original block's size. All four pieces retain that same ice field.
            var drawSize = geometry.Size + new Vector2(200f, 240f);
            body.Size = drawSize;
            body.Position = -drawSize * 0.5f;
            Material.SetShaderParameter("region_size", geometry.Size);
            Material.SetShaderParameter("draw_size", drawSize);
            Material.SetShaderParameter("ground_y", geometry.Size.Y * 0.5f);
            Material.SetShaderParameter("seed", index * 0.317f + 0.19f);
            SetFall(0f);
        }

        internal ShaderMaterial Material { get; }
        internal bool HasShattered { get; set; }

        internal void SetFall(float progress)
        {
            progress = Mathf.Clamp(progress, 0f, 1f);
            Material.SetShaderParameter("formation", Mathf.Min(1f, progress * 3f));
            var gravity = 2f * _fallHeight / (FallDuration * FallDuration);
            _root.GlobalPosition = _center + Vector2.Up * _fallHeight
                + CelVfxGeometry.BallisticOffset(Vector2.Zero, gravity, progress * FallDuration);
        }

        internal Tween CreateShatterTween(Node2D debrisParent)
        {
            var tween = _root.CreateTween().SetParallel();
            // Crack on the contact frame, then move the actual four fracture
            // regions after the brief local compression. No second hit is needed.
            tween.TweenMethod(Callable.From<float>(time =>
            {
                Material.SetShaderParameter("shatter", time / ShatterDuration);
                Material.SetShaderParameter("split", 1f);
                for (var i = 0; i < 4; i++)
                {
                    var velocity = i switch
                    {
                        0 => new Vector2(-150f, -115f),
                        1 => new Vector2(135f, -140f),
                        2 => new Vector2(-90f, 20f),
                        _ => new Vector2(150f, 35f)
                    };
                    var offset = CelVfxGeometry.BallisticOffset(velocity, FragmentGravity, time);
                    var angle = time * ((i % 2 == 0 ? -1f : 1f) * (1.1f + i * 0.2f));
                    Material.SetShaderParameter($"fragment_{i}", new Vector3(offset.X, offset.Y, angle));
                }
            }), 0f, ShatterDuration, ShatterDuration)
                .SetDelay(ContactDuration).SetTrans(Tween.TransitionType.Linear);

            for (var i = 0; i < GrainCount; i++)
            {
                var spread = -0.7f + 1.4f * i / (GrainCount - 1);
                var radius = 1.8f + i % 3 * 0.7f;
                var grain = CelVfxGeometry.AddBallisticDebris(
                    tween, debrisParent,
                    [new Vector2(-radius, -radius * 0.6f), new Vector2(radius * 0.8f, -radius),
                        new Vector2(radius, radius * 0.55f), new Vector2(-radius * 0.5f, radius)],
                    new Color(0.83f, 0.94f, 0.96f), _center + new Vector2(spread * _size.X * 0.18f, 0f),
                    new Vector2(spread * 230f, -100f - i % 3 * 36f), 0.27f,
                    ContactDuration, FragmentGravity, 2.4f + _index * 0.3f, "HailGrain", zIndex: 1);
                grain.ZAsRelative = true;
            }
            return tween;
        }
    }
}
