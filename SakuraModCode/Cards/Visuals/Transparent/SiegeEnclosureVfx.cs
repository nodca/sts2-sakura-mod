using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>The delayed Siege retaliation encloses the enemy group in one scroll-covered cube.</summary>
internal sealed class SiegeEnclosureVfx : CelVfxSession
{
    internal const string ScenePath = MainFile.ResPath + "/scenes/combat/card_vfx/siege_enclosure.tscn";
    internal static IReadOnlyList<string> AssetPaths { get; } = [ScenePath];
    private const float AssembleDuration = 0.38f;
    private const float HoldDuration = 0.22f;
    private const float ReleaseDuration = 0.48f;
    private readonly Node2D _backRoot;
    private readonly List<ShaderMaterial> _materials;

    private SiegeEnclosureVfx(Node2D root, Node2D backRoot, NCombatRoom room, List<ShaderMaterial> materials)
        : base(root, room)
    {
        _backRoot = backRoot;
        _materials = materials;
        root.TreeExiting += ReleaseBackRoot;
    }

    protected override IEnumerable<ShaderMaterial> Materials => _materials;
    protected override float MaximumLifetime => 6f;

    internal static Task PlayOrResolveAsync(IReadOnlyList<Creature> targets, Func<Task> resolveGameplay) =>
        CelVfxSession.PlayOrResolveAsync(
            "Siege enclosure",
            () => TryCreate(targets),
            session => session.Enclose(),
            _ => resolveGameplay(),
            session => session.Release(),
            session => session.Dispose());

    private static SiegeEnclosureVfx? TryCreate(IReadOnlyList<Creature> targets)
    {
        if (targets.Count == 0 || !TryPrepare("Siege enclosure",
                () => PreloadManager.Cache.GetScene(ScenePath), out var room, out _, out var scene))
            return null;

        Node2D? front = null;
        Node2D? back = null;
        try
        {
            Rect2? bounds = null;
            foreach (var target in targets)
            {
                if (!target.IsAlive || CelVfxGeometry.ResolveCaster(room.GetCreatureNode(target)) is not { } anchor)
                    continue;
                var body = new Rect2(anchor.BodyCenter - anchor.BodySize * 0.5f, anchor.BodySize);
                bounds = bounds is { } previous ? previous.Merge(body) : body;
            }
            if (bounds is not { } group)
                return null;

            var layout = Layout.ForGroup(group, room.CombatVfxContainer.GetViewportRect());
            front = new Node2D { Name = "SakuraSiegeEnclosure" };
            back = new Node2D { Name = "SakuraSiegeEnclosureBack" };
            room.CombatVfxContainer.AddChildSafely(front);
            room.BackCombatVfxContainer.AddChildSafely(back);
            var materials = new List<ShaderMaterial>();
            foreach (var (parent, layer) in new[] { (back, 0f), (front, 1f) })
            {
                var node = scene.Instantiate<Node2D>();
                parent.AddChildSafely(node);
                node.GlobalPosition = layout.Center;
                var body = node.GetNode<ColorRect>("%Cube");
                body.Size = layout.RegionSize;
                body.Position = -layout.RegionSize * 0.5f;
                var material = CelVfxGeometry.DuplicateMaterial(body, "Siege cube");
                material.SetShaderParameter("region_size", layout.RegionSize);
                material.SetShaderParameter("cube_width", layout.Width);
                material.SetShaderParameter("layer_mode", layer);
                materials.Add(material);
            }

            var session = new SiegeEnclosureVfx(front, back, room, materials);
            session.StartClock();
            return session;
        }
        catch
        {
            front?.QueueFreeSafely();
            back?.QueueFreeSafely();
            throw; // The shared session logs the presentation failure and resolves gameplay once.
        }
    }

    internal readonly record struct Layout(Vector2 Center, float Width)
    {
        internal Vector2 RegionSize => new(Width * 1.32f + 24f, Width * 1.18f + 24f);

        internal static Layout ForGroup(Rect2 group, Rect2 viewport)
        {
            // The card's diamond-top view: vertical edges are 0.70 of the projected
            // width, with 0.08 depth above/below. Its shared interior is 0.62 high.
            var width = Math.Max(group.Size.X + 64f, (group.Size.Y + 48f) / 0.62f);
            var center = new Vector2(group.GetCenter().X, group.End.Y + 24f - width * 0.35f);
            // Move within the spare enclosure margin to fit the screen; never shrink
            // the cube below the complete enemy group just to fit an unusual layout.
            var minX = Math.Max(viewport.Position.X + width * 0.5f + 12f, group.End.X - width * 0.5f + 16f);
            var maxX = Math.Min(viewport.End.X - width * 0.5f - 12f, group.Position.X + width * 0.5f - 16f);
            var minY = Math.Max(viewport.Position.Y + width * 0.43f + 12f, group.End.Y - width * 0.35f + 16f);
            var maxY = Math.Min(viewport.End.Y - width * 0.43f - 12f, group.Position.Y + width * 0.27f - 16f);
            if (minX <= maxX)
                center.X = Math.Clamp(center.X, minX, maxX);
            if (minY <= maxY)
                center.Y = Math.Clamp(center.Y, minY, maxY);
            return new Layout(center, width);
        }
    }

    private async Task<bool> Enclose()
    {
        // This is a Power's end-of-enemy-turn trigger, so there is no played card/wand prelude.
        Animate("assemble", 0f, 1f, AssembleDuration);
        return await WaitActive(AssembleDuration);
    }

    private void Release()
    {
        if (!IsActive())
        {
            Dispose();
            return;
        }

        Animate("pulse", 1f, 0f, HoldDuration);
        var tween = Track(Root.CreateTween());
        tween.TweenInterval(HoldDuration);
        tween.TweenMethod(Callable.From<float>(value => Set("release", value)), 0f, 1f, ReleaseDuration);
        tween.TweenCallback(Callable.From(Dispose));
    }

    private void Animate(string uniform, float from, float to, float duration)
    {
        Set(uniform, from);
        Track(Root.CreateTween()).TweenMethod(
            Callable.From<float>(value => Set(uniform, value)), from, to, duration)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
    }

    private void Set(string uniform, float value)
    {
        foreach (var material in _materials)
            material.SetShaderParameter(uniform, value);
    }

    private void ReleaseBackRoot()
    {
        Root.TreeExiting -= ReleaseBackRoot;
        if (GodotObject.IsInstanceValid(_backRoot) && !_backRoot.IsQueuedForDeletion())
            _backRoot.QueueFreeSafely();
    }
}
