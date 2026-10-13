using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;

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

    // Intent previews follow native AttackIntent: show the local player's value.
    internal static Player? PreviewPlayer(ICombatState combatState)
    {
        if (!combatState.Players.Any())
            return null;
        return LocalContext.GetMe(combatState)
            ?? combatState.Players.FirstOrDefault(static player => player.Creature.IsAlive);
    }
}
