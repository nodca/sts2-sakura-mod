using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>Continuous snow across the enemy line, with frost and powder on contact.</summary>
internal sealed class SnowBlizzardVfx : CelVfxSession
{
    internal const string ScenePath =
        MainFile.ResPath + "/scenes/combat/card_vfx/snow_blizzard_vfx.tscn";
    internal const string TargetScenePath =
        MainFile.ResPath + "/scenes/combat/card_vfx/snow_crystal_target.tscn";
    internal const string ShaderPath =
        MainFile.ResPath + "/shaders/card_vfx/snow_blizzard.gdshader";
    internal static IReadOnlyList<string> AssetPaths { get; } = [ScenePath, TargetScenePath];

    internal const float RevealDuration = 0.18f;
    internal const float ImpactDuration = 0.42f;
    internal const float FadeDuration = 0.26f;
    internal const float InactiveHitAt = -10f;
    private const float FinaleStrength = 1.35f;
    private const float FieldHeadroom = 42f;
    private const float FieldSidePadding = 76f;
    private const float FieldGroundPadding = 8f;
    private const float FieldMinWidth = 300f;
    private const float FieldMaxWidth = 1180f;
    private const float FieldViewportWidthFraction = 0.70f;

    private static bool _loadFailureLogged;

    private readonly Node2D _backRoot;
    private readonly ShaderMaterial _frontMaterial;
    private readonly ShaderMaterial _backMaterial;
    private readonly ShaderMaterial[] _materials;
    private readonly Dictionary<Creature, FrostVisual> _targets = [];
    private float _lastHitAt = InactiveHitAt;
    private bool _faded;
    private bool _finale;
    private bool _backReleased;

    private SnowBlizzardVfx(
        Node2D root,
        Node2D backRoot,
        NCombatRoom room,
        PackedScene targetScene,
        IReadOnlyList<Creature> targets,
        Creature? caster)
        : base(root, room)
    {
        _backRoot = backRoot;
        var frontBody = root.GetNode<ColorRect>("%SnowfallBody");
        var backBody = backRoot.GetNode<ColorRect>("%SnowfallBody");
        _frontMaterial = CelVfxGeometry.DuplicateMaterial(frontBody, "near snow");
        _backMaterial = CelVfxGeometry.DuplicateMaterial(backBody, "far snow");

        var envelopes = new List<CelVfxGeometry.TargetGeometry>(targets.Count);
        for (var index = 0; index < targets.Count; index++)
        {
            var target = targets[index];
            if (_targets.ContainsKey(target))
                continue;
            var geometry = CelVfxGeometry.Resolve(room, target, index, Budget);
            envelopes.Add(geometry);
            _targets.Add(target, new FrostVisual(targetScene, root, geometry, index));
        }

        var field = LayoutField(room, envelopes);
        var casterX = caster is null ? field.Center.X - 1f : room.GetCreatureNode(caster)?.VfxSpawnPosition.X;
        var windSign = WindSign(casterX ?? field.Center.X - 1f, field.Center.X);
        var seed = (float)Random.Shared.NextDouble() * 6.1f;
        ConfigureField(root, frontBody, _frontMaterial, field, windSign, seed, 1f);
        ConfigureField(backRoot, backBody, _backMaterial, field, windSign, seed, 0f);
        foreach (var visual in _targets.Values)
        {
            // Moving the field root must not move its already positioned targets.
            visual.Root.GlobalPosition = visual.Center;
            visual.Material.SetShaderParameter("wind_sign", windSign);
            visual.Material.SetShaderParameter("impact_life", ImpactDuration);
        }
        _materials = [_frontMaterial, _backMaterial, .. _targets.Values.Select(static target => target.Material)];

        // The far layer is a sibling in the room, outside the base-owned root.
        CombatManager.Instance.CombatEnded += OnSnowCombatEnded;
        root.TreeExiting += ReleaseBackRoot;
        backRoot.TreeExiting += OnBackTreeExiting;
    }

    private static CelVfxGeometry.GeometryBudget Budget => new(
        HorizontalPadding: 22f,
        VerticalPadding: 18f,
        MinWidth: 160f,
        MinHeight: 170f,
        MaxWidth: 440f,
        MaxHeight: 470f,
        FallbackWidth: 240f,
        FallbackHeight: 260f,
        FloorClearance: 6f,
        MaxViewportWidthFraction: 0.30f,
        MaxViewportHeightFraction: 0.56f);

    protected override IEnumerable<ShaderMaterial> Materials => _materials;
    protected override float MaximumLifetime => 15f;

    internal static float WindSign(float casterX, float fieldCenterX) =>
        fieldCenterX >= casterX ? 1f : -1f;

    /// <summary>Weather fades immediately; disposal preserves the last contact's tail.</summary>
    internal static float ReleaseSeconds(float elapsed, float lastHitAt) =>
        Math.Max(FadeDuration, Math.Clamp(ImpactDuration - Math.Max(0f, elapsed - lastHitAt), 0f, ImpactDuration));

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
            "Snow blizzard",
            () => TryCreate(targets, caster),
            session => session.PlayPrelude(card, caster),
            scope => resolveGameplay(new Cues(scope)),
            session => session.FadeAndDispose(),
            session => session.DisposePresentation());
    }

    internal sealed class Cues(CueScope<SnowBlizzardVfx> scope)
    {
        internal void Impact(Creature target)
        {
            ArgumentNullException.ThrowIfNull(target);
            scope.Invoke("impact", session => session.Impact(target));
        }

        internal void Finale() => scope.Invoke("finale", static session => session.Finale());
    }

    private static SnowBlizzardVfx? TryCreate(IReadOnlyList<Creature> targets, Creature? caster)
    {
        if (targets.Count == 0
            || !TryPrepare("Snow blizzard", LoadScenes, out var room, out _, out var scenes))
        {
            return null;
        }

        Node2D? root = null;
        Node2D? backRoot = null;
        try
        {
            root = scenes.Field.Instantiate<Node2D>();
            root.Name = "SakuraSnowBlizzardVfx";
            room.CombatVfxContainer.AddChildSafely(root);

            backRoot = scenes.Field.Instantiate<Node2D>();
            backRoot.Name = "SakuraSnowBlizzardBackVfx";
            room.BackCombatVfxContainer.AddChildSafely(backRoot);

            var session = new SnowBlizzardVfx(root, backRoot, room, scenes.Target, targets, caster);
            session.StartClock();
            return session;
        }
        catch (Exception exception)
        {
            LogLoadFailure(exception);
            root?.QueueFreeSafely();
            backRoot?.QueueFreeSafely();
            return null;
        }
    }

    private async Task<bool> PlayPrelude(CardModel card, Creature? caster)
    {
        if (!await PlayCelPrelude(card, caster))
            return false;

        var reveal = Track(Root.CreateTween());
        reveal.TweenMethod(Callable.From<float>(value =>
        {
            _frontMaterial.SetShaderParameter("curtain", value);
            _backMaterial.SetShaderParameter("curtain", value);
        }), 0f, 1f, RevealDuration).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
        return await WaitActive(RevealDuration);
    }

    private float Elapsed => _frontMaterial.GetShaderParameter("elapsed").AsSingle();

    private void Impact(Creature target)
    {
        if (!IsActive() || _faded || !target.IsAlive || !_targets.TryGetValue(target, out var visual))
            return;

        _lastHitAt = Elapsed;
        visual.Hit(_lastHitAt, 1f);
    }

    private void Finale()
    {
        if (_finale || _faded || !IsActive())
            return;

        _finale = true;
        var now = Elapsed;
        foreach (var (target, visual) in _targets)
        {
            if (!target.IsAlive)
                continue;
            _lastHitAt = now;
            visual.Hit(now, FinaleStrength);
        }
        _frontMaterial.SetShaderParameter("gust_at", now);
        _backMaterial.SetShaderParameter("gust_at", now);
    }

    private void FadeAndDispose()
    {
        if (_faded || !IsActive())
        {
            DisposePresentation();
            return;
        }

        _faded = true;
        var releaseSeconds = ReleaseSeconds(Elapsed, _lastHitAt);
        var fade = Track(Root.CreateTween());
        // Only weather opacity fades here. Each target's shader retains the
        // complete contact and powder envelope, including the preceding hit.
        fade.TweenMethod(Callable.From<float>(value =>
        {
            _frontMaterial.SetShaderParameter("opacity", value);
            _backMaterial.SetShaderParameter("opacity", value);
        }), 1f, 0f, FadeDuration);
        if (releaseSeconds > FadeDuration)
            fade.TweenInterval(releaseSeconds - FadeDuration);
        fade.TweenCallback(Callable.From(DisposePresentation));
    }

    private static CelVfxGeometry.TargetGeometry LayoutField(
        NCombatRoom room,
        IReadOnlyList<CelVfxGeometry.TargetGeometry> targets)
    {
        var left = targets.Min(static target => target.Center.X - target.Size.X * 0.5f);
        var right = targets.Max(static target => target.Center.X + target.Size.X * 0.5f);
        var top = targets.Min(static target => target.Center.Y - target.Size.Y * 0.5f) - FieldHeadroom;
        var bottom = targets.Max(static target => target.Center.Y + target.Size.Y * 0.5f) + FieldGroundPadding;
        var viewport = room.CombatVfxContainer.GetViewportRect();
        var widthCap = Math.Max(FieldMinWidth, Math.Min(FieldMaxWidth, viewport.Size.X * FieldViewportWidthFraction));
        var width = Math.Clamp(right - left + FieldSidePadding * 2f, FieldMinWidth, widthCap);
        var center = new Vector2((left + right) * 0.5f, (top + bottom) * 0.5f);
        var height = Math.Max(220f, bottom - top);
        return new CelVfxGeometry.TargetGeometry(center, new Vector2(width, height));
    }

    private static void ConfigureField(
        Node2D root,
        ColorRect body,
        ShaderMaterial material,
        CelVfxGeometry.TargetGeometry field,
        float windSign,
        float seed,
        float depth)
    {
        root.GlobalPosition = field.Center;
        root.Scale = Vector2.One;
        root.ZAsRelative = true;
        root.ZIndex = 0;
        body.Size = field.Size;
        body.Position = -field.Size * 0.5f;
        material.SetShaderParameter("region_size", field.Size);
        material.SetShaderParameter("wind_sign", windSign);
        material.SetShaderParameter("seed", seed);
        material.SetShaderParameter("depth_layer", depth);
        material.SetShaderParameter("curtain", 0f);
    }

    private void DisposePresentation()
    {
        ReleaseBackRoot();
        Dispose();
    }

    private void OnSnowCombatEnded(CombatRoom _) => ReleaseBackRoot();
    private void OnBackTreeExiting() => DisposePresentation();

    private void ReleaseBackRoot()
    {
        if (_backReleased)
            return;

        _backReleased = true;
        CombatManager.Instance.CombatEnded -= OnSnowCombatEnded;
        if (GodotObject.IsInstanceValid(Root))
            Root.TreeExiting -= ReleaseBackRoot;
        if (GodotObject.IsInstanceValid(_backRoot))
        {
            _backRoot.TreeExiting -= OnBackTreeExiting;
            if (!_backRoot.IsQueuedForDeletion())
                _backRoot.QueueFreeSafely();
        }
    }

    private static (PackedScene Field, PackedScene Target) LoadScenes() =>
        (PreloadManager.Cache.GetScene(ScenePath), PreloadManager.Cache.GetScene(TargetScenePath));

    private static void LogLoadFailure(Exception exception)
    {
        if (_loadFailureLogged)
            return;
        _loadFailureLogged = true;
        MainFile.Logger.Error($"Could not create Snow VFX from {ScenePath} and {TargetScenePath}: {exception}");
    }

    private sealed class FrostVisual
    {
        private readonly float _seed;
        private int _hits;

        internal FrostVisual(PackedScene scene, Node2D parent, CelVfxGeometry.TargetGeometry geometry, int index)
        {
            Root = scene.Instantiate<Node2D>();
            Root.Name = $"SnowContact{index + 1}";
            Root.ZIndex = 1;
            parent.AddChildSafely(Root);
            Center = geometry.Center;
            Root.GlobalPosition = Center;
            var body = Root.GetNode<ColorRect>("%CrystalBody");
            Material = CelVfxGeometry.DuplicateMaterial(body, $"snow contact {index}");
            body.Size = geometry.Size;
            body.Position = -geometry.Size * 0.5f;
            Material.SetShaderParameter("region_size", geometry.Size);
            _seed = index * 0.317f + 0.19f;
        }

        internal Node2D Root { get; }
        internal Vector2 Center { get; }
        internal ShaderMaterial Material { get; }

        internal void Hit(float now, float strength)
        {
            // Two fixed slots retain the previous powder tail without allocating
            // nodes, queueing animations, or delaying the new contact.
            Material.SetShaderParameter("previous_hit_at", Material.GetShaderParameter("hit_at"));
            Material.SetShaderParameter("previous_hit_seed", Material.GetShaderParameter("hit_seed"));
            Material.SetShaderParameter("previous_hit_strength", Material.GetShaderParameter("hit_strength"));
            Material.SetShaderParameter("hit_at", now);
            Material.SetShaderParameter("hit_seed", _seed + ++_hits * 0.731f);
            Material.SetShaderParameter("hit_strength", strength);
        }
    }
}
