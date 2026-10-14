using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace SakuraMod.SakuraModCode.Powers;

public sealed class ChoiceDrawDebtPower : SakuraPowerModel
{
    protected override string IconFileName => "advance.svg";
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player || AmountOnTurnStart <= 0)
            return;

        var amount = AmountOnTurnStart;
        await PowerCmd.ModifyAmount(choiceContext, this, -amount, Owner, null);
        var selected = (await CardSelectCmd.FromHandForDiscard(
            choiceContext,
            player,
            new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, amount),
            null,
            this)).ToList();
        if (selected.Count > 0)
            await CardCmd.Discard(choiceContext, selected);
    }
}
