using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace SakuraMod.SakuraModCode.FourthAct;

internal static class FourthActCombatRules
{
    internal static bool IsCompletePlayerSide(
        ICombatState combatState,
        IEnumerable<Creature> participants)
    {
        var participantSet = participants.ToHashSet();
        return combatState.Players
            .Where(static player => player.Creature.IsAlive)
            .All(player => participantSet.Contains(player.Creature));
    }
}
