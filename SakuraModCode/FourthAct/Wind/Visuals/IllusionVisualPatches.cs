using Godot;
using HarmonyLib;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Creatures;
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
