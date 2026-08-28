using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;

namespace SakuraMod.SakuraModCode.Cards;

public class ClowChange() : ClowCard(1, CardType.Skill, CardRarity.Common, TargetType.None)
{
    public override SakuraElementSet Elements => SakuraElementSet.Earth;
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(2),
        new DynamicVar("Magic", 2)
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var opportunity = SakuraMagicCharge.CaptureOpportunity(Owner);
        await SakuraMagicCharge.TryApplyCapturedOpportunity(choiceContext, this, opportunity);

        var discarded = await TryDiscardOne(choiceContext);

        var maxSpend = DynamicVars["Magic"].IntValue;
        var spent = ChangeRules.SpendableMagic(this);
        if (spent > 0)
            await SakuraMagicCharge.SpendUpToMagic(choiceContext, Owner, maxSpend);

        var drawCount = ChangeRules.DrawCount(discarded, DynamicVars.Cards.IntValue, spent);
        if (drawCount > 0)
            await CardPileCmd.Draw(choiceContext, drawCount, Owner, false);
    }

    protected override void OnUpgrade() => AddKeywordIfMissing(CardKeyword.Retain);

    private async Task<bool> TryDiscardOne(PlayerChoiceContext choiceContext)
    {
        var candidates = CardPile.GetCards(Owner, PileType.Hand).Where(card => card != this).ToList();
        if (candidates.Count == 0)
            return false;

        var selected = await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(HandPrompt, 0, 1)
            {
                Cancelable = true,
                RequireManualConfirmation = false
            },
            card => candidates.Contains(card),
            this);

        var cards = selected.ToList();
        if (cards.Count == 0)
            return false;

        await CardCmd.Discard(choiceContext, cards);
        return true;
    }
}

public class SakuraChange() : SakuraFormCard(0, CardType.Skill, TargetType.None)
{
    public override SakuraElementSet Elements => SakuraElementSet.Earth;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(5)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var hand = CardPile.GetCards(Owner, PileType.Hand).Where(card => card != this).ToList();
        if (hand.Count > 0)
            await CardCmd.Discard(choiceContext, hand);
        await CardPileCmd.Draw(choiceContext, ReleasedValue("Cards"), Owner, false);
    }
}

internal static class ChangeRules
{
    internal static int DrawCount(bool discarded, int baseDraw, int spentMagic) =>
        (discarded ? baseDraw : 0) + spentMagic;

    internal static int SpendableMagic(CardModel card)
    {
        if (!card.IsMutable || card.Owner is not { } owner)
            return 0;

        var maxSpend = card.DynamicVars["Magic"].IntValue;
        var current = owner.Creature.GetPower<ClassicMagicChargePower>()?.Amount ?? 0;
        return Math.Min(current, maxSpend);
    }
}
