using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.TestSupport;

namespace SakuraMod.SakuraModCode.Cards;

internal static class AquaWaterSphereVfx
{
    internal const string ScenePath = MainFile.ResPath + "/scenes/combat/card_vfx/aqua_water_sphere_vfx.tscn";
    internal const string TargetScenePath = MainFile.ResPath + "/scenes/combat/card_vfx/aqua_water_sphere_target.tscn";
    internal const string ShaderPath = MainFile.ResPath + "/shaders/card_vfx/aqua_water_sphere.gdshader";

    // Timings are tuned for readability over brevity: the first pass was fast
    // enough that the crest, the band structure, and the freeze all blurred past.
    internal const float CrestDuration = 0.52f;
    internal const float FormationDuration = 0.46f;
    internal const float TargetStagger = 0.07f;
    internal const float ImpactRiseDuration = 0.07f;
    internal const float ImpactRecoverDuration = 0.30f;
    internal const float FreezeDuration = 0.26f;

    /// <summary>
    /// Beat where the fully frozen shell is held still so it reads before it
    /// breaks. Release() is invoked immediately after PlayFreeze(), so without
    /// this hold the shatter would start mid-transition.
    /// </summary>
    internal const float FreezeHold = 0.20f;

    internal const float ReleaseDuration = 0.42f;
    internal const float ShatterDuration = 0.46f;

    /// <summary>Full frozen release: transition, readable hold, then shatter.</summary>
    internal const float FrozenReleaseDuration = FreezeDuration + FreezeHold + ShatterDuration;

    // Safety net for stranded nodes, not a timing device. Five enemies with a
    // freeze beat already run about 3.5s at these slower timings, so the cap has
    // to sit well clear of the real envelope or it becomes a truncation bug.
    internal const float MaximumLifetime = 8.00f;

    internal const int DropletCount = 8;
    internal const int ShardCount = 7;

    // The enclosure starts before the crest finishes so the opening reads as one
    // event rather than two. Scaled with the slower crest: the water should begin
    // climbing while the wave is still passing over the target.
    private const float FormationLead = 0.16f;

    private const int VfxZIndex = 3000;
    private const float HorizontalPadding = 42f;
    private const float VerticalPadding = 32f;
    private const float MinWidth = 176f;
    private const float MinHeight = 190f;
    private const float MaxWidth = 470f;
    private const float MaxHeight = 500f;
    private const float FallbackWidth = 230f;
    private const float FallbackHeight = 260f;
    private const float FloorClearance = 10f;

    /// <summary>
    /// Crest height as a fraction of the tallest target. Generous on purpose: the
    /// wave has to stand up over the enemies and still leave room for the lip and
    /// the barrel carved under it.
    /// </summary>
    /// <summary>
    /// Crest draw height as a multiple of the tallest target. Above 1 on purpose:
    /// the shader's swell peaks at 0.90 of the region, so the wave has to be
    /// taller than the enemies to break over them rather than lap at their knees.
    /// </summary>
    private const float CrestHeightFraction = 1.45f;

    private const float CrestPadding = 120f;

    /// <summary>Region the target scene's floor ripple polyline is authored for.</summary>
    private const float AuthoredDiameter = 256f;

    private static PackedScene? _rootScene;
    private static PackedScene? _targetScene;
    private static Shader? _shader;
    private static bool _loadFailureLogged;

    internal static bool ResourcesArePreloaded =>
        _rootScene is not null && _targetScene is not null && _shader is not null;

    public static void PreloadResources()
    {
        if (!TestMode.IsOn)
            _ = TryGetResources(out _, out _);
    }

    /// <param name="caster">
    /// Creature the wave radiates from. Passing it lets the crest work in
    /// player-left, player-right, and player-centre arenas without branching on
    /// layout; a null caster falls back to sweeping from the near edge.
    /// </param>
    public static Session? TryCreate(IReadOnlyList<Creature> targets, Creature? caster = null)
    {
        if (TestMode.IsOn
            || targets.Count == 0
            || NCombatRoom.Instance is not { } room
            || room.CombatVfxContainer is not { } container)
        {
            return null;
        }
        if (!TryGetResources(out var rootScene, out var targetScene))
            return null;

        Node2D? root = null;
        try
        {
            root = rootScene.Instantiate<Node2D>();
            root.Name = "SakuraAquaWaterSphereVfx";
            root.ZAsRelative = false;
            root.ZIndex = VfxZIndex;
            container.AddChildSafely(root);

            return new Session(root, room, targetScene, targets, caster);
        }
        catch (Exception exception)
        {
            LogLoadFailure(exception);
            root?.QueueFreeSafely();
            return null;
        }
    }

    private static bool TryGetResources(out PackedScene rootScene, out PackedScene targetScene)
    {
        rootScene = null!;
        targetScene = null!;
        try
        {
            _rootScene ??= ResourceLoader.Load<PackedScene>(ScenePath, null, ResourceLoader.CacheMode.Reuse)
                ?? throw new InvalidOperationException($"Could not load {ScenePath}.");
            _targetScene ??= ResourceLoader.Load<PackedScene>(TargetScenePath, null, ResourceLoader.CacheMode.Reuse)
                ?? throw new InvalidOperationException($"Could not load {TargetScenePath}.");
            _shader ??= ResourceLoader.Load<Shader>(ShaderPath, null, ResourceLoader.CacheMode.Reuse)
                ?? throw new InvalidOperationException($"Could not load {ShaderPath}.");
            rootScene = _rootScene;
            targetScene = _targetScene;
            return true;
        }
        catch (Exception exception)
        {
            LogLoadFailure(exception);
            return false;
        }
    }

    private static void LogLoadFailure(Exception exception)
    {
        if (_loadFailureLogged)
            return;

        _loadFailureLogged = true;
        MainFile.Logger.Error(
            $"Could not create Aqua water VFX from {ScenePath}, {TargetScenePath}, and {ShaderPath}: {exception}");
    }

    internal sealed class Session : IDisposable
    {
        private readonly Node2D _root;
        private readonly Node2D _crest;
        private readonly ColorRect _crestBody;
        private readonly ShaderMaterial _crestMaterial;
        private readonly Node2D _debris;
        private readonly Dictionary<Creature, TargetVisual> _targets = [];
        private readonly List<Tween> _tweens = [];
        private bool _released;
        private bool _disposed;
        private float _elapsed;

        public Session(
            Node2D root,
            NCombatRoom room,
            PackedScene targetScene,
            IReadOnlyList<Creature> creatures,
            Creature? caster)
        {
            _root = root;
            _crest = root.GetNode<Node2D>("%Crest");
            _crestBody = root.GetNode<ColorRect>("%CrestBody");
            _debris = root.GetNode<Node2D>("%Debris");
            var spheres = root.GetNode<Node2D>("%Spheres");

            var geometries = creatures
                .Select((creature, index) => (Creature: creature, Geometry: ResolveGeometry(room, creature, index)))
                .ToList();
            foreach (var (creature, geometry) in geometries)
            {
                var visual = new TargetVisual(targetScene, spheres, _debris, geometry, _targets.Count);
                _targets.Add(creature, visual);
            }

            _crestMaterial = DuplicateMaterial(_crestBody, "crest");
            LayOutCrest(geometries.Select(static item => item.Geometry).ToList(), ResolveCasterX(room, caster));

            CombatManager.Instance.CombatEnded += OnCombatEnded;
            _root.TreeExiting += OnTreeExiting;
            TaskHelper.RunSafely(DriveElapsedTime());
        }

        public async Task PlayPrelude()
        {
            if (!IsActive())
                return;

            var tween = Track(_root.CreateTween().SetParallel());
            tween.TweenProperty(_crest, "modulate:a", 1f, CrestDuration * 0.14f);
            // Sine InOut carries the crest across the enemy line at a readable
            // speed. An EaseOut curve would spend most of the beat parked at the
            // far end, which is part of why the first pass was hard to see.
            tween.TweenMethod(
                    Callable.From<float>(SetCrestSweep),
                    0f,
                    1f,
                    CrestDuration)
                .SetEase(Tween.EaseType.InOut)
                .SetTrans(Tween.TransitionType.Sine);
            tween.TweenProperty(_crest, "modulate:a", 0f, CrestDuration * 0.26f)
                .SetDelay(CrestDuration * 0.74f);

            // Each enclosure forms on its own delay so several enemies read as one
            // sweep instead of a simultaneous pop.
            var formationStart = Math.Max(0f, CrestDuration - FormationLead);
            var index = 0;
            foreach (var visual in _targets.Values)
            {
                var visualRef = visual;
                tween.TweenMethod(
                        Callable.From<float>(value => visualRef.SetFormation(value)),
                        0f,
                        1f,
                        FormationDuration)
                    .SetDelay(formationStart + index * TargetStagger)
                    .SetEase(Tween.EaseType.Out)
                    .SetTrans(Tween.TransitionType.Cubic);
                index++;
            }

            var total = formationStart + Math.Max(0, _targets.Count - 1) * TargetStagger + FormationDuration;
            await WaitActive(total);
        }

        public void Impact(Creature target)
        {
            if (!IsActive() || !_targets.TryGetValue(target, out var visual))
                return;

            var tween = Track(visual.Root.CreateTween());
            tween.TweenMethod(Callable.From<float>(visual.SetImpact), 0f, 1f, ImpactRiseDuration)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Cubic);
            // Back easing overshoots past rest before settling, which is what
            // makes the compression read as fluid rather than as a scale pop.
            tween.TweenMethod(Callable.From<float>(visual.SetImpact), 1f, 0f, ImpactRecoverDuration)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Back);
        }

        /// <summary>
        /// Locks the target's water into ice. Presentation only: the caller has
        /// already resolved which creature holds the highest Frostbite.
        /// </summary>
        public void PlayFreeze(Creature target)
        {
            if (!IsActive() || !_targets.TryGetValue(target, out var visual) || visual.IsFrozen)
                return;

            visual.IsFrozen = true;
            var tween = Track(visual.Root.CreateTween());
            tween.TweenMethod(Callable.From<float>(visual.SetFreeze), 0f, 1f, FreezeDuration)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Quint);
        }

        public void Release()
        {
            if (_released || !IsActive())
                return;

            _released = true;
            var longest = ReleaseDuration;
            foreach (var visual in _targets.Values)
            {
                Track(visual.CreateReleaseTween());
                if (visual.IsFrozen)
                    longest = Math.Max(longest, FrozenReleaseDuration);
            }

            var fade = Track(_root.CreateTween());
            fade.TweenInterval(longest * 0.68f);
            fade.TweenProperty(_root, "modulate:a", 0f, longest * 0.32f);
            fade.TweenCallback(Callable.From(Dispose));
        }

        private void SetCrestSweep(float progress)
        {
            // formation is the wave-front position. The shader turns it into a
            // per-column rise-and-collapse phase, so the water swells in place
            // instead of the node sliding a mass across the enemy line.
            _crestMaterial.SetShaderParameter("formation", progress);
        }

        private void LayOutCrest(IReadOnlyList<TargetGeometry> geometries, float? casterX)
        {
            // The region has to span the caster as well as every target, because
            // the swell propagates outward from the caster's own column.
            var left = geometries.Min(static item => item.Center.X - item.Size.X * 0.5f);
            var right = geometries.Max(static item => item.Center.X + item.Size.X * 0.5f);
            if (casterX is { } origin)
            {
                left = Math.Min(left, origin);
                right = Math.Max(right, origin);
            }
            left -= CrestPadding;
            right += CrestPadding;

            // Anchored to the floor line, tall enough to break over the enemies.
            var floorY = geometries.Max(static item => item.Center.Y + item.Size.Y * 0.5f);
            var tallest = geometries.Max(static item => item.Size.Y);
            var size = new Vector2(right - left, tallest * CrestHeightFraction);

            _crestBody.Size = size;
            _crestBody.Position = -size * 0.5f;
            // The crest does not travel: the wave front moves through it. Placing
            // it once, with its bottom edge on the floor, is what stops it reading
            // as a flat mass flying in over the enemies' heads.
            var center = new Vector2((left + right) * 0.5f, floorY - size.Y * 0.5f);
            _crest.GlobalPosition = center;

            var localOrigin = (casterX ?? left) - center.X;
            _crestMaterial.SetShaderParameter("region_size", size);
            _crestMaterial.SetShaderParameter("shape_mode", 1f);
            _crestMaterial.SetShaderParameter("seed", 0.41f);
            _crestMaterial.SetShaderParameter("crest_origin_x", localOrigin);
        }

        private async Task DriveElapsedTime()
        {
            try
            {
                while (IsActive() && _elapsed < MaximumLifetime)
                {
                    _crestMaterial.SetShaderParameter("elapsed", _elapsed);
                    foreach (var visual in _targets.Values)
                        visual.SetElapsed(_elapsed);
                    _elapsed += await _root.AwaitProcessFrame();
                }

                if (IsActive())
                    Dispose();
            }
            catch (OperationCanceledException) when (!IsActive())
            {
                // Node frame waits are canceled when the VFX root leaves the tree.
            }
        }

        private async Task<bool> WaitActive(float seconds)
        {
            try
            {
                var waited = 0f;
                while (waited < seconds)
                {
                    if (!IsActive())
                        return false;
                    waited += await _root.AwaitProcessFrame();
                }
                return IsActive();
            }
            catch (OperationCanceledException) when (!IsActive())
            {
                return false;
            }
        }

        private Tween Track(Tween tween)
        {
            _tweens.Add(tween);
            return tween;
        }

        private bool IsActive() =>
            !_disposed
            && !CombatManager.Instance.IsEnding
            && GodotObject.IsInstanceValid(_root)
            && _root.IsInsideTree()
            && !_root.IsQueuedForDeletion();

        private void OnCombatEnded(CombatRoom _) => Dispose();

        private void OnTreeExiting()
        {
            if (!_disposed)
                Dispose(queueFree: false);
        }

        public void Dispose() => Dispose(queueFree: true);

        private void Dispose(bool queueFree)
        {
            if (_disposed)
                return;

            _disposed = true;
            CombatManager.Instance.CombatEnded -= OnCombatEnded;
            if (GodotObject.IsInstanceValid(_root))
                _root.TreeExiting -= OnTreeExiting;
            foreach (var tween in _tweens)
            {
                if (GodotObject.IsInstanceValid(tween) && tween.IsValid())
                    tween.Kill();
            }
            _tweens.Clear();
            if (queueFree && GodotObject.IsInstanceValid(_root) && !_root.IsQueuedForDeletion())
                _root.QueueFreeSafely();
        }
    }

    private sealed class TargetVisual
    {
        private readonly ShaderMaterial _material;
        private readonly ColorRect _body;
        private readonly Node2D _droplets;
        private readonly Node2D _shards;
        private readonly Line2D _ripple;
        private readonly Vector2 _center;
        private readonly Vector2 _size;
        private readonly float _formationRise;
        private readonly float _rippleScale;
        private readonly int _index;

        public TargetVisual(
            PackedScene scene,
            Node2D parent,
            Node2D debrisParent,
            TargetGeometry geometry,
            int index)
        {
            Root = scene.Instantiate<Node2D>();
            Root.Name = $"AquaWater{index + 1}";
            parent.AddChildSafely(Root);
            // Droplets pinch off this target's own body, so they stay target-local.
            // Shards fly across the field toward the player, so they live on the
            // shared container and must not inherit this target's fade.
            _droplets = Root.GetNode<Node2D>("%Droplets");
            _shards = debrisParent;
            _center = geometry.Center;
            _size = geometry.Size;
            _formationRise = geometry.Size.Y * 0.46f;
            _index = index;

            _body = Root.GetNode<ColorRect>("%WaterBody");
            _material = DuplicateMaterial(_body, $"target {index}");

            // The root keeps a uniform scale forever. Sizing travels to the
            // shader as region_size, which is what keeps ink weight constant in
            // screen pixels across every enemy size.
            Root.Scale = Vector2.One;
            _body.Size = geometry.Size;
            _body.Position = -geometry.Size * 0.5f;
            _material.SetShaderParameter("region_size", geometry.Size);
            _material.SetShaderParameter("shape_mode", 0f);
            _material.SetShaderParameter("seed", index * 0.271f + 0.13f);

            // The ripple polyline is authored around a 256 px region. Since the
            // root no longer scales, it carries its own uniform scale so the
            // ground contact matches the enclosure without stretching the ink.
            _ripple = Root.GetNode<Line2D>("%FloorRipple");
            _rippleScale = geometry.Size.X / AuthoredDiameter;
            _ripple.Position = new Vector2(0f, geometry.Size.Y * 0.47f);
            _ripple.Scale = new Vector2(_rippleScale, _rippleScale);
            SetFormation(0f);
        }

        public Node2D Root { get; }
        public bool IsFrozen { get; set; }

        public void SetFormation(float progress)
        {
            progress = Mathf.Clamp(progress, 0f, 1f);
            var eased = progress * progress * (3f - 2f * progress);
            _material.SetShaderParameter("formation", progress);
            Root.GlobalPosition = _center + Vector2.Down * Mathf.Lerp(_formationRise, 0f, eased);
        }

        public void SetImpact(float progress) =>
            _material.SetShaderParameter("impact", progress);

        public void SetFreeze(float progress) =>
            _material.SetShaderParameter("freeze", progress);

        public void SetElapsed(float elapsed) =>
            _material.SetShaderParameter("elapsed", elapsed);

        public Tween CreateReleaseTween() =>
            IsFrozen ? CreateShatterTween() : CreateBreakupTween();

        private Tween CreateBreakupTween()
        {
            var tween = Root.CreateTween().SetParallel();
            // Driving the union blend radius to zero is what separates the mass;
            // the droplets below are the pieces it pinched off.
            tween.TweenMethod(
                    Callable.From<float>(value => _material.SetShaderParameter("breakup", value)),
                    0f,
                    1f,
                    ReleaseDuration)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Cubic);
            tween.TweenProperty(_ripple, "modulate:a", 0.72f, ReleaseDuration * 0.22f);
            tween.TweenProperty(_ripple, "scale", new Vector2(1.38f, 1.22f) * _rippleScale, ReleaseDuration)
                .From(new Vector2(0.72f, 0.72f) * _rippleScale)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Cubic);

            for (var i = 0; i < DropletCount; i++)
            {
                var angle = -Mathf.Pi * 0.92f + Mathf.Pi * 1.84f * i / Math.Max(1, DropletCount - 1);
                var speed = 150f + i % 3 * 44f;
                var velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed;
                AddBallisticDebris(
                    tween,
                    _droplets,
                    DropletPoints(4.4f + i % 3, 10f + i % 2 * 3f),
                    i % 2 == 0 ? new Color(0.43f, 0.88f, 0.96f) : new Color(0.78f, 0.98f, 1f),
                    velocity,
                    ReleaseDuration);
            }
            return tween;
        }

        private Tween CreateShatterTween()
        {
            var tween = Root.CreateTween().SetParallel();
            // A frozen shell shatters rather than dissolving into droplets, so the
            // release constraint and the art direction agree. The shatter waits
            // out the freeze transition and its hold so the ice reads first.
            var shatterStart = FreezeDuration + FreezeHold;
            tween.TweenMethod(
                    Callable.From<float>(value => _material.SetShaderParameter("breakup", value)),
                    0f,
                    1f,
                    ShatterDuration)
                .SetDelay(shatterStart)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Expo);

            // Shards travel toward the player side, handing the eye off to the
            // energy and draw feedback the card resolves next.
            var playerDirection = _center.X >= 0f ? -1f : 1f;
            for (var i = 0; i < ShardCount; i++)
            {
                var spread = -0.55f + 1.10f * i / Math.Max(1, ShardCount - 1);
                var velocity = new Vector2(playerDirection * (210f + i % 3 * 52f), spread * 190f - 90f);
                AddBallisticDebris(
                    tween,
                    _shards,
                    ShardPoints(5.2f + i % 3 * 1.6f, 13f + i % 2 * 5f),
                    i % 2 == 0 ? new Color(0.82f, 0.97f, 1f) : new Color(0.56f, 0.83f, 0.98f),
                    velocity,
                    ShatterDuration,
                    shatterStart);
            }
            return tween;
        }

        /// <summary>
        /// Spawns one far-field debris shape and integrates a parabolic arc, so
        /// separated pieces fall under gravity instead of sliding outward. Far-field
        /// shapes use flat fills with no ink outline, matching the animation
        /// convention for sub-12px elements.
        /// </summary>
        private void AddBallisticDebris(
            Tween tween,
            Node2D parent,
            Vector2[] points,
            Color color,
            Vector2 velocity,
            float duration,
            float delay = 0f)
        {
            const float gravity = 980f;
            var origin = _center + new Vector2(
                (velocity.X > 0f ? 1f : -1f) * _size.X * 0.16f,
                -_size.Y * 0.06f);
            var piece = new Polygon2D
            {
                Name = "AquaDebris",
                Color = color,
                Polygon = points,
                Modulate = new Color(1f, 1f, 1f, 0f),
                ZAsRelative = false,
                ZIndex = VfxZIndex + 1
            };
            parent.AddChildSafely(piece);
            piece.GlobalPosition = origin;

            tween.TweenMethod(
                    Callable.From<float>(time =>
                    {
                        if (!GodotObject.IsInstanceValid(piece))
                            return;
                        piece.GlobalPosition = origin
                            + velocity * time
                            + new Vector2(0f, 0.5f * gravity * time * time);
                        piece.Rotation = time * (1.8f + _index * 0.3f);
                    }),
                    0f,
                    duration,
                    duration)
                .SetDelay(delay);
            tween.TweenProperty(piece, "modulate:a", 0.9f, duration * 0.14f)
                .SetDelay(delay);
            tween.TweenProperty(piece, "modulate:a", 0f, duration * 0.38f)
                .SetDelay(delay + duration * 0.62f);
        }
    }

    private static ShaderMaterial DuplicateMaterial(ColorRect body, string label)
    {
        var source = body.Material as ShaderMaterial
            ?? throw new InvalidOperationException($"Aqua {label} rect requires a ShaderMaterial.");
        var material = source.Duplicate() as ShaderMaterial
            ?? throw new InvalidOperationException($"Could not duplicate Aqua {label} material.");
        body.Material = material;
        return material;
    }

    private static Vector2[] DropletPoints(float radius, float height) =>
    [
        new(0f, -height),
        new(radius, -radius * 0.15f),
        new(radius * 0.72f, radius * 0.78f),
        new(0f, radius),
        new(-radius * 0.72f, radius * 0.78f),
        new(-radius, -radius * 0.15f)
    ];

    private static Vector2[] ShardPoints(float radius, float height) =>
    [
        new(0f, -height),
        new(radius * 0.86f, -height * 0.18f),
        new(radius * 0.34f, height * 0.72f),
        new(-radius * 0.52f, height * 0.46f),
        new(-radius * 0.78f, -height * 0.30f)
    ];

    private static float? ResolveCasterX(NCombatRoom room, Creature? caster)
    {
        if (caster is null || room.GetCreatureNode(caster) is not { } node || !GodotObject.IsInstanceValid(node))
            return null;
        if (node.Hitbox is { } hitbox && GodotObject.IsInstanceValid(hitbox))
        {
            var rect = hitbox.GetGlobalRect();
            if (IsUsable(rect.Size))
                return rect.Position.X + rect.Size.X * 0.5f;
        }
        return node.VfxSpawnPosition.X;
    }

    private static TargetGeometry ResolveGeometry(NCombatRoom room, Creature creature, int fallbackIndex)
    {
        var viewportRect = room.CombatVfxContainer.GetViewportRect();
        var viewportSize = viewportRect.Size;
        var node = room.GetCreatureNode(creature);
        if (node is not null
            && GodotObject.IsInstanceValid(node)
            && node.Hitbox is { } hitbox
            && GodotObject.IsInstanceValid(hitbox))
        {
            var rect = hitbox.GetGlobalRect();
            if (IsUsable(rect.Size))
            {
                var maxWidth = ViewportBound(MinWidth, MaxWidth, viewportSize.X, 0.34f);
                var maxHeight = ViewportBound(MinHeight, MaxHeight, viewportSize.Y, 0.58f);
                var size = new Vector2(
                    Math.Clamp(rect.Size.X + HorizontalPadding * 2f, MinWidth, maxWidth),
                    Math.Clamp(rect.Size.Y + VerticalPadding * 2f, MinHeight, maxHeight));
                var floor = node.GetBottomOfHitbox();
                var center = new Vector2(
                    rect.Position.X + rect.Size.X * 0.5f,
                    floor.Y - size.Y * 0.5f + FloorClearance);
                return new TargetGeometry(center, size);
            }
        }

        var fallbackCenter = node?.VfxSpawnPosition
            ?? viewportRect.GetCenter() + new Vector2((fallbackIndex - 1) * 96f, 24f);
        return new TargetGeometry(fallbackCenter, new Vector2(FallbackWidth, FallbackHeight));
    }

    private static float ViewportBound(
        float configuredMinimum,
        float configuredMaximum,
        float viewportAxis,
        float fraction)
    {
        if (!float.IsFinite(viewportAxis) || viewportAxis <= 0f)
            return configuredMaximum;
        return Math.Max(configuredMinimum, Math.Min(configuredMaximum, viewportAxis * fraction));
    }

    private static bool IsUsable(Vector2 size) =>
        float.IsFinite(size.X)
        && float.IsFinite(size.Y)
        && size.X > 1f
        && size.Y > 1f;

    private readonly record struct TargetGeometry(Vector2 Center, Vector2 Size);
}
