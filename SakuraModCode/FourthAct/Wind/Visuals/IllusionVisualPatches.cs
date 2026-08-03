using Godot;
using HarmonyLib;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using SakuraMod.SakuraModCode.FourthAct.Wind.Models;

namespace SakuraMod.SakuraModCode.FourthAct.Wind.Visuals;

internal static class IllusionVisualController
{
    private static readonly Color HiddenProjectionTint = new(0.55f, 0.55f, 0.55f, 1f);
    private static readonly ConditionalWeakTable<Creature, Dictionary<string, Vector2>> DeclaredPositions = new();

    internal static Creature? RealBody(Creature projection) =>
        projection.CombatState?.Enemies.FirstOrDefault(
            static creature => creature.IsAlive && creature.Monster is IllusionMonster);

    internal static void ExchangePositions(Creature realBody, Creature other)
    {
        CaptureDeclaredPositions(realBody);
        var realNode = NCombatRoom.Instance?.GetCreatureNode(realBody);
        var otherNode = NCombatRoom.Instance?.GetCreatureNode(other);
        if (realNode is null || otherNode is null)
            return;

        (realNode.Position, otherNode.Position) = (otherNode.Position, realNode.Position);
    }

    internal static void ResetDeclaredPositions(Creature realBody)
    {
        if (realBody.CombatState is not { } combatState || NCombatRoom.Instance is null)
            return;

        CaptureDeclaredPositions(realBody);
        if (!DeclaredPositions.TryGetValue(realBody, out var positions))
            return;

        foreach (var image in combatState.Enemies.Where(
                     static creature => creature.IsAlive && creature.Monster is IllusionMonster or IllusionProjectionMonster))
        {
            if (image.SlotName is { } slotName
                && positions.TryGetValue(slotName, out var position)
                && NCombatRoom.Instance.GetCreatureNode(image) is { } node)
            {
                node.GlobalPosition = position;
            }
        }
    }

    internal static void SetRealBodyRevealed(Creature realBody, bool revealed)
    {
        if (realBody.CombatState is not { } combatState || NCombatRoom.Instance is null)
            return;

        foreach (var image in combatState.Enemies.Where(
                     static creature => creature.IsAlive && creature.Monster is IllusionMonster or IllusionProjectionMonster))
        {
            if (NCombatRoom.Instance.GetCreatureNode(image) is { } node)
                node.Modulate = revealed && image != realBody ? HiddenProjectionTint : Colors.White;
        }
    }

    internal static async Task ReshuffleWithOcclusionAsync(Creature realBody)
    {
        var images = realBody.CombatState?.Enemies
            .Where(static creature => creature.IsAlive && creature.Monster is IllusionMonster or IllusionProjectionMonster)
            .ToList() ?? [];
        if (images.Count < 2)
        {
            SetRealBodyRevealed(realBody, revealed: false);
            return;
        }

        await WithGroupOcclusionAsync(realBody, () =>
        {
            var other = realBody.Monster!.Rng.NextItem(images.Where(creature => creature != realBody).ToList());
            if (other is not null)
                ExchangePositions(realBody, other);
            SetRealBodyRevealed(realBody, revealed: false);
            return Task.CompletedTask;
        });
    }

    internal static async Task WithGroupOcclusionAsync(Creature realBody, Func<Task> hiddenAction)
    {
        if (MegaCrit.Sts2.Core.TestSupport.TestMode.IsOn || NCombatRoom.Instance is not { } room)
        {
            await hiddenAction();
            return;
        }

        CaptureDeclaredPositions(realBody);
        var points = realBody.CombatState?.Enemies
            .Where(static creature => creature.IsAlive && creature.Monster is IllusionMonster or IllusionProjectionMonster)
            .Select(room.GetCreatureNode)
            .OfType<NCreature>()
            .Select(static node => node.VfxSpawnPosition)
            .ToList() ?? [];
        if (room.GetCreatureNode(realBody) is { } realNode
            && DeclaredPositions.TryGetValue(realBody, out var declaredPositions))
        {
            var vfxOffset = realNode.VfxSpawnPosition - realNode.GlobalPosition;
            points.AddRange(declaredPositions.Values.Select(position => position + vfxOffset));
        }
        if (points.Count == 0)
        {
            await hiddenAction();
            return;
        }

        var left = points.Min(static point => point.X) - 170f;
        var right = points.Max(static point => point.X) + 170f;
        var top = points.Min(static point => point.Y) - 190f;
        var bottom = points.Max(static point => point.Y) + 190f;
        var root = new Node2D
        {
            Name = "SakuraIllusionGroupOcclusion",
            ZIndex = 120,
            ZAsRelative = false,
            Modulate = Colors.Transparent
        };
        var veil = new Polygon2D
        {
            Color = new Color(0.08f, 0.07f, 0.16f, 0.98f),
            Polygon = [new(left, top), new(right, top), new(right, bottom), new(left, bottom)]
        };
        root.AddChild(veil);
        for (var index = 0; index < 7; index++)
        {
            var y = Mathf.Lerp(top, bottom, (index + 1f) / 8f);
            root.AddChild(new Line2D
            {
                Width = 3f,
                DefaultColor = new Color(0.42f, 0.76f, 0.88f, 0.34f),
                Antialiased = true,
                Points = [new(left, y), new((left + right) * 0.5f, y + (index % 2 == 0 ? 16f : -16f)), new(right, y)]
            });
        }
        room.CombatVfxContainer.AddChildSafely(root);

        try
        {
            var close = root.CreateTween();
            close.TweenProperty(root, "modulate", Colors.White, 0.15f)
                .SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Cubic);
            await root.ToSignal(close, Tween.SignalName.Finished);
            await hiddenAction();
            var open = root.CreateTween().SetParallel();
            open.TweenProperty(root, "modulate:a", 0f, 0.18f)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
            open.TweenProperty(root, "scale:y", 0.92f, 0.18f)
                .SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
            await root.ToSignal(open, Tween.SignalName.Finished);
        }
        finally
        {
            root.QueueFreeSafely();
        }
    }

    internal static async Task RevealRealBodyAsync(Creature realBody)
    {
        await PlayGroupRippleAsync(realBody);
        SetRealBodyRevealed(realBody, revealed: true);
    }

    internal static async Task DissolveProjectionAsync(Creature projection, Color anticipationColor)
    {
        if (MegaCrit.Sts2.Core.TestSupport.TestMode.IsOn
            || NCombatRoom.Instance?.GetCreatureNode(projection) is not { } node)
        {
            return;
        }

        var anchor = node.Visuals.VfxSpawnPosition;
        var originalModulate = node.Modulate;
        var anticipate = node.CreateTween();
        anticipate.TweenProperty(node, "modulate", anticipationColor, 0.09f);
        await node.ToSignal(anticipate, Tween.SignalName.Finished);

        var root = new Node2D { Name = "SakuraIllusionProjectionDissolve", ZIndex = 18 };
        anchor.AddChildSafely(root);
        var ripple = new Line2D
        {
            Width = 5f,
            DefaultColor = new Color(0.68f, 0.94f, 1f, 0.9f),
            Antialiased = true,
            Closed = true,
            Points = CirclePoints(34f, 24),
            Scale = Vector2.One * 0.45f
        };
        root.AddChild(ripple);
        for (var index = 0; index < 9; index++)
        {
            var angle = Mathf.Tau * index / 9f;
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var shard = new Polygon2D
            {
                Color = index % 3 == 0
                    ? new Color(0.62f, 0.52f, 0.88f, 0.78f)
                    : new Color(0.52f, 0.86f, 0.94f, 0.72f),
                Polygon = [new(0f, -10f), new(6f, 3f), new(-4f, 8f)]
            };
            root.AddChild(shard);
            var shardTween = shard.CreateTween().SetParallel();
            shardTween.TweenProperty(shard, "position", direction * (58f + index % 3 * 16f), 0.22f)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
            shardTween.TweenProperty(shard, "rotation", angle + 1.4f, 0.22f);
            shardTween.TweenProperty(shard, "modulate:a", 0f, 0.12f).SetDelay(0.1f);
        }
        var dissolve = root.CreateTween().SetParallel();
        dissolve.TweenProperty(ripple, "scale", Vector2.One * 1.55f, 0.2f)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        dissolve.TweenProperty(ripple, "modulate:a", 0f, 0.15f).SetDelay(0.05f);
        dissolve.TweenProperty(node, "modulate:a", 0f, 0.18f);
        await root.ToSignal(dissolve, Tween.SignalName.Finished);
        node.Modulate = new Color(originalModulate, 0f);
        root.QueueFreeSafely();
    }

    internal static Color StatusColor(PowerModel power) => power.Type switch
    {
        MegaCrit.Sts2.Core.Entities.Powers.PowerType.Debuff => new Color(0.86f, 0.32f, 0.42f, 0.92f),
        MegaCrit.Sts2.Core.Entities.Powers.PowerType.Buff => new Color(0.42f, 0.82f, 0.66f, 0.92f),
        _ => new Color(0.62f, 0.54f, 0.86f, 0.92f)
    };

    private static async Task PlayGroupRippleAsync(Creature realBody)
    {
        if (MegaCrit.Sts2.Core.TestSupport.TestMode.IsOn || NCombatRoom.Instance is not { } room)
            return;
        var roots = new List<Node2D>();
        foreach (var image in realBody.CombatState?.Enemies.Where(
                     static creature => creature.IsAlive && creature.Monster is IllusionMonster or IllusionProjectionMonster) ?? [])
        {
            if (room.GetCreatureNode(image) is not { } node)
                continue;
            var ripple = new Line2D
            {
                Name = "SakuraIllusionRevealRipple",
                Width = 4f,
                DefaultColor = new Color(0.58f, 0.88f, 0.98f, 0.76f),
                Antialiased = true,
                Closed = true,
                Points = CirclePoints(42f, 24),
                Scale = Vector2.One * 0.45f
            };
            node.Visuals.VfxSpawnPosition.AddChildSafely(ripple);
            roots.Add(ripple);
            var tween = ripple.CreateTween().SetParallel();
            tween.TweenProperty(ripple, "scale", Vector2.One * 1.45f, 0.16f);
            tween.TweenProperty(ripple, "modulate:a", 0f, 0.12f).SetDelay(0.04f);
        }
        if (roots.Count == 0)
            return;
        await roots[0].ToSignal(roots[0].GetTree().CreateTween().TweenInterval(0.16f), Tween.SignalName.Finished);
        foreach (var root in roots)
            root.QueueFreeSafely();
    }

    private static Vector2[] CirclePoints(float radius, int count) =>
        Enumerable.Range(0, count)
            .Select(index => Mathf.Tau * index / (float)(count - 1))
            .Select(angle => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius)
            .ToArray();

    private static void CaptureDeclaredPositions(Creature realBody)
    {
        if (DeclaredPositions.TryGetValue(realBody, out _)
            || realBody.CombatState is not { } combatState
            || NCombatRoom.Instance is null)
        {
            return;
        }

        var positions = new Dictionary<string, Vector2>(StringComparer.Ordinal);
        foreach (var image in combatState.Enemies.Where(
                     static creature => creature.IsAlive && creature.Monster is IllusionMonster or IllusionProjectionMonster))
        {
            if (image.SlotName is { } slotName && NCombatRoom.Instance.GetCreatureNode(image) is { } node)
                positions[slotName] = node.GlobalPosition;
        }

        if (positions.Count > 0)
            DeclaredPositions.Add(realBody, positions);
    }
}

[HarmonyPatch(typeof(NCreatureStateDisplay), nameof(NCreatureStateDisplay.SetCreature))]
internal static class IllusionStateDisplayPatch
{
    [HarmonyPrefix]
    private static void UseRealBodyForProjection(ref Creature creature)
    {
        if (creature.Monster is IllusionProjectionMonster
            && IllusionVisualController.RealBody(creature) is { } realBody)
        {
            creature = realBody;
        }
    }
}

[HarmonyPatch(typeof(NCreature), nameof(NCreature.UpdateIntent))]
internal static class IllusionIntentDisplayPatch
{
    [HarmonyPrefix]
    private static bool CopyRealIntent(NCreature __instance, IEnumerable<Creature> targets, ref Task __result)
    {
        if (__instance.Entity.Monster is not IllusionProjectionMonster
            || IllusionVisualController.RealBody(__instance.Entity) is not { } realBody)
        {
            return true;
        }

        __result = UpdateIntent(__instance, realBody, targets);
        return false;
    }

    private static Task UpdateIntent(NCreature projectionNode, Creature realBody, IEnumerable<Creature> targets)
    {
        IReadOnlyList<AbstractIntent> intents = realBody.Monster!.NextMove.Intents;
        var container = projectionNode.IntentContainer;
        var index = 0;
        for (; index < intents.Count && index < container.GetChildCount(); index++)
        {
            var intentNode = container.GetChild<NIntent>(index);
            intentNode.SetFrozen(false);
            intentNode.UpdateIntent(intents[index], targets, realBody);
        }

        var offset = projectionNode.GetHashCode() * 0.01f;
        for (; index < intents.Count; index++)
        {
            var intentNode = NIntent.Create(offset + index * 0.3f);
            container.AddChild(intentNode);
            intentNode.UpdateIntent(intents[index], targets, realBody);
        }

        while (container.GetChildCount() > intents.Count)
            container.GetChild(container.GetChildCount() - 1).QueueFree();

        return Task.CompletedTask;
    }
}
