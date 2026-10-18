using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using SakuraMod.SakuraModCode.Cards;

namespace SakuraMod.SakuraModCode.Powers;

// Exchange's delayed Remind: after next turn's draw, Remind Amount.
public sealed class ExchangeRecallPower : SakuraPowerModel
{
    protected override string IconFileName => "exchange_recall.svg";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player || AmountOnTurnStart <= 0)
            return;

        var amount = AmountOnTurnStart;
        await PowerCmd.ModifyAmount(choiceContext, this, -amount, Owner, null);
        await Remind.RecallIntoHand(player, choiceContext, amount, upgradeCopies: false);
    }
}
