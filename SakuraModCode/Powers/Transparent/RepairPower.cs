using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Utils;

namespace SakuraMod.SakuraModCode.Powers;

public class RepairPower : SakuraPowerModel
{
    // Gates a heal, so the once-per-turn mark must be checksummed.
    private static readonly SavedAttachedState<RepairPower, bool> HealedThisTurn =
        new("SakuraMod_RepairPowerHealedThisTurn", () => false);

    protected override string IconFileName => "repair.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (participants.Contains(Owner))
            HealedThisTurn[this] = false;
        return Task.CompletedTask;
    }

    // Every way a card leaves Memory for the hand counts, so Dreaming does too.
    internal static async Task AfterRecalledFromMemory(Player player)
    {
        if (player.Creature.GetPower<RepairPower>() is not { } power || HealedThisTurn[power])
            return;

        HealedThisTurn[power] = true;
        power.Flash();
        await CreatureCmd.Heal(power.Owner, power.Amount);
    }
}
