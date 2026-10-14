using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.TestSupport;

namespace SakuraMod.SakuraModCode.FourthAct.Visuals.Presentation;

// Instantiates fourth-act effect scenes. A scene owns its own timeline (an autoplaying
// AnimationPlayer and one-shot particles) and declares metadata on its root:
//   contact_time — when the effect lands (a cast's wind-up, a projectile's arrival)
//   lifetime     — when it may be freed
//   travel_time  — optional: the root flies from the caster to the target over this time
//   arc_height   — optional: peak height of that flight
//   aimed        — optional: the root is rotated to point from the caster at the target
// Effects are authored travelling right-to-left (enemy towards player) and mirror for the
// other way.
internal static class FourthActFxPlayer
{
    internal const string ContactTimeMeta = "contact_time";
    internal const string LifetimeMeta = "lifetime";
    internal const string TravelTimeMeta = "travel_time";
    internal const string ArcHeightMeta = "arc_height";
    internal const string AimedMeta = "aimed";
    private const float FallbackLifetime = 1.5f;

    // For native AttackCommand.WithHitVfxNode: an unparented node the command adds to the
    // target's VFX container on every hit.
    internal static Node2D? CreateHit(FourthActFxId id, Creature attacker, Creature target, FourthActFxAnchor anchor)
    {
        if (TestMode.IsOn || NCombatRoom.Instance is not { } room || room.GetCreatureNode(target) is not { } targetNode)
            return null;

        var facing = room.GetCreatureNode(attacker) is { } attackerNode
                     && attackerNode.VfxSpawnPosition.X < targetNode.VfxSpawnPosition.X
            ? -1f
            : 1f;
        return Instantiate(id, AnchorOf(targetNode, anchor), facing, null);
    }

    // Plays an effect on the room's combat VFX layer and returns its contact time, or null
    // when nothing could be shown (so the caller adds no wait).
    internal static float? Spawn(FourthActFxId id, Vector2 globalPosition, float facing = 1f) =>
        Add(Instantiate(id, globalPosition, facing, null));

    internal static float? SpawnAt(FourthActFxId id, Creature creature, FourthActFxAnchor anchor = FourthActFxAnchor.Center) =>
        !TestMode.IsOn && NCombatRoom.Instance?.GetCreatureNode(creature) is { } node
            ? Spawn(id, AnchorOf(node, anchor))
            : null;

    // From one creature towards another: the scene either flies there (travel_time) or is
    // rotated to point there (aimed). Returns when it arrives.
    internal static float? SpawnToward(FourthActFxId id, Creature from, FourthActFxAnchor fromAnchor, Creature to,
        FourthActFxAnchor toAnchor = FourthActFxAnchor.Center)
    {
        if (TestMode.IsOn
            || NCombatRoom.Instance is not { } room
            || room.GetCreatureNode(from) is not { } fromNode
            || room.GetCreatureNode(to) is not { } toNode)
            return null;

        var start = AnchorOf(fromNode, fromAnchor);
        var end = AnchorOf(toNode, toAnchor);
        return Add(Instantiate(id, start, end.X > start.X ? -1f : 1f, end));
    }

    internal static float ContactTime(Node node) => Meta(node, ContactTimeMeta, 0f);

    internal static Vector2 AnchorOf(NCreature node, FourthActFxAnchor anchor) => anchor switch
    {
        FourthActFxAnchor.Head => node.GetTopOfHitbox(),
        FourthActFxAnchor.Feet => node.GetBottomOfHitbox(),
        _ => node.VfxSpawnPosition
    };

    internal static Node2D? Instantiate(FourthActFxId id) => Instantiate(id, Vector2.Zero, 1f, null, placed: false);

    private static float? Add(Node2D? node)
    {
        if (node is null || NCombatRoom.Instance is not { } room)
            return null;

        room.CombatVfxContainer.AddChildSafely(node);
        return ContactTime(node);
    }

    private static Node2D? Instantiate(FourthActFxId id, Vector2 globalPosition, float facing, Vector2? target, bool placed = true)
    {
        if (TestMode.IsOn)
            return null;

        try
        {
            var root = PreloadManager.Cache.GetScene(FourthActFxScenes.PathFor(id))
                .Instantiate<Node2D>(PackedScene.GenEditState.Disabled);
            if (placed)
                root.AddChild(new FourthActFxLifetime(globalPosition, facing, target, Meta(root, LifetimeMeta, FallbackLifetime)));
            return root;
        }
        catch (Exception exception)
        {
            MainFile.Logger.Warn($"Fourth-act fx {id} failed open: {exception.Message}");
            return null;
        }
    }

    internal static float Meta(Node node, string name, float fallback) =>
        node.HasMeta(name) ? node.GetMeta(name).AsSingle() : fallback;
}

// Places its effect root once it enters the tree, flies or aims it at a target when the scene
// asks for that, and frees it after the declared lifetime, at combat end, or with its
// container — whichever comes first.
internal sealed partial class FourthActFxLifetime : Node
{
    private readonly Vector2 _start;
    private readonly float _facing;
    private readonly Vector2? _target;
    private readonly float _lifetime;
    private Node2D? _root;
    private float _travel;
    private float _arc;
    private float _elapsed;

    internal FourthActFxLifetime(Vector2 start, float facing, Vector2? target, float lifetime)
    {
        _start = start;
        _facing = facing;
        _target = target;
        _lifetime = lifetime;
        Name = "FourthActFxLifetime";
    }

    public override void _Ready()
    {
        if (GetParent() is Node2D root)
        {
            _root = root;
            root.GlobalPosition = _start;
            if (_target is { } target && root.HasMeta(FourthActFxPlayer.AimedMeta))
            {
                // Authored pointing left; aim the -X axis at the target.
                root.GlobalRotation = (target - _start).Angle() - Mathf.Pi;
            }
            else if (_facing < 0f)
            {
                root.Scale = new Vector2(-root.Scale.X, root.Scale.Y);
            }

            if (_target is not null)
            {
                _travel = FourthActFxPlayer.Meta(root, FourthActFxPlayer.TravelTimeMeta, 0f);
                _arc = FourthActFxPlayer.Meta(root, FourthActFxPlayer.ArcHeightMeta, 0f);
            }
        }

        SetProcess(_travel > 0f);
        CombatManager.Instance.CombatEnded += OnCombatEnded;
        TreeExiting += OnTreeExiting;
        GetTree().CreateTimer(_lifetime).Timeout += FreeRoot;
    }

    public override void _Process(double delta)
    {
        if (_root is null || _target is not { } target)
            return;

        _elapsed += (float)delta;
        var t = Mathf.Min(1f, _elapsed / _travel);
        _root.GlobalPosition = _start.Lerp(target, t) + new Vector2(0f, -_arc * 4f * t * (1f - t));
        if (t >= 1f)
            SetProcess(false);
    }

    private void OnCombatEnded(CombatRoom _) => FreeRoot();

    private void OnTreeExiting()
    {
        TreeExiting -= OnTreeExiting;
        CombatManager.Instance.CombatEnded -= OnCombatEnded;
    }

    private void FreeRoot()
    {
        if (IsInstanceValid(this) && GetParent() is { } root && IsInstanceValid(root))
            root.QueueFreeSafely();
    }
}
