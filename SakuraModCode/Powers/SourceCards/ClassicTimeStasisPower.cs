using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace SakuraMod.SakuraModCode.Powers;

// Clow Time's stasis. Amount is the number of the owner's turn ends it still
// covers, and stacks like other duration buffs. Resolved through the native
// end-of-turn hooks, so hand, Block and Energy are read when each turn actually
// ends, not snapshotted on play.
public class ClassicTimeStasisPower : SakuraPowerModel
{
    // Set in the synced BeforeSideTurnEnd; an auto-play during the next turn's
    // start must not count its own turn start as an elapsed turn.
    private bool _turnEnded;

    protected override string IconFileName => "time_stasis.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool ShouldFlush(Player player) =>
        player != Owner.Player;

    public override bool ShouldClearBlock(Creature creature) =>
        creature != Owner;

    public override bool ShouldPlayerResetEnergy(Player player) =>
        player != Owner.Player;

    // Enemy debuffs decay through PowerCmd.ModifyAmount with no applier
    // (TickDownDuration, Decrement); player-driven removal carries an applier
    // or goes through PowerCmd.Remove and stays untouched.
    public override bool TryModifyPowerAmountReceived(
        PowerModel canonicalPower,
        Creature target,
        decimal amount,
        Creature? applier,
        out decimal modifiedAmount)
    {
        modifiedAmount = amount;
        if (applier is not null
            || amount >= 0m
            || target.Side == Owner.Side
            || canonicalPower.AllowNegative
            || canonicalPower.Type != PowerType.Debuff
            || !canonicalPower.IsVisible)
            return false;

        modifiedAmount = 0m;
        return true;
    }

    public override Task AfterModifyingPowerAmountReceived(PowerModel power)
    {
        Flash();
        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (Owner.Side == side && participants.Contains(Owner))
        {
            _turnEnded = true;
            SakuraElementStatePower.PreserveAllForNextTurn(Owner);
        }

        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStartLate(PlayerChoiceContext choiceContext, Player player)
    {
        if (!_turnEnded || player.Creature != Owner)
            return;

        _turnEnded = false;
        await PowerCmd.Decrement(this);
    }
}
