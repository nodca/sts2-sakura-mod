using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using SakuraMod.SakuraModCode.Powers;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
/// The live UI of what Sakura's time magic keeps across a turn end: Block,
/// energy, the hand and element states. Transparent Time stamps seals on these;
/// Clow Time's Stasis scatters sand over them.
/// </summary>
internal static class SakuraPreservedStateTargets
{
    /// <param name="Kind">"block", "energy", "hand" or "element".</param>
    /// <param name="Anchor">The single control the group sits on; null for the hand, which spans its cards.</param>
    /// <param name="Nodes">Every control the group covers (one, or each hand card's body).</param>
    internal readonly record struct Group(string Kind, Control? Anchor, IReadOnlyList<Control> Nodes);

    /// <summary>
    /// Resolves the groups in a fixed order: Block, energy, hand, element states.
    /// Missing displays are skipped; <paramref name="missing"/> hears about a
    /// display that should exist (Block above 0, the local energy counter) but does not.
    /// </summary>
    internal static IReadOnlyList<Group> Resolve(
        NCombatRoom room, NCreature? ownerNode, Creature owner, Action<string>? missing = null)
    {
        var groups = new List<Group>();
        var display = GodotObject.IsInstanceValid(ownerNode)
            ? ownerNode!.GetNodeOrNull<NCreatureStateDisplay>("%HealthBar")
            : null;

        if (owner.Block > 0
            && display?.GetNodeOrNull<NHealthBar>("%HealthBar")?.GetNodeOrNull<Control>("%BlockContainer") is { } block
            && block.IsVisibleInTree())
        {
            groups.Add(new Group("block", block, [block]));
        }
        else if (owner.Block > 0)
        {
            missing?.Invoke("Block");
        }

        // Energy and hand are the local player's UI.
        if (LocalContext.IsMe(owner.Player))
        {
            if (room.Ui is { } ui && ui.EnergyCounterContainer is { } container && container.IsVisibleInTree())
            {
                var energy = container.GetChildren().OfType<NEnergyCounter>().FirstOrDefault() ?? (Control)container;
                groups.Add(new Group("energy", energy, [energy]));
            }
            else
            {
                missing?.Invoke("energy");
            }

            var cards = NPlayerHand.Instance is { } hand
                ? hand.ActiveHolders
                    .Select(static holder => holder.CardNode)
                    .OfType<NCard>()
                    .Where(static card => GodotObject.IsInstanceValid(card) && card.IsVisibleInTree())
                    .Select(static card => GodotObject.IsInstanceValid(card.Body) ? card.Body : (Control)card)
                    .ToList()
                : [];
            if (cards.Count > 0)
                groups.Add(new Group("hand", null, cards));
        }

        if (display?.GetNodeOrNull<Control>("%PowerContainer") is { } powers)
        {
            foreach (var power in powers.GetChildren().OfType<NPower>())
            {
                if (power.IsVisibleInTree() && IsElementState(power))
                    groups.Add(new Group("element", power, [power]));
            }
        }

        return groups;
    }

    private static bool IsElementState(NPower power)
    {
        try
        {
            return power.Model is SakuraElementStatePower;
        }
        catch (InvalidOperationException)
        {
            // A power node whose model is not bound yet is not a preserved state.
            return false;
        }
    }
}
