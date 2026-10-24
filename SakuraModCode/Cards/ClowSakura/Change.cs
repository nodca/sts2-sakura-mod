using MegaCrit.Sts2.Core.Localization;
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

public class ClowChange() : ClowExtraEffectCard(1, CardType.Skill, CardRarity.Common, TargetType.None)
{
    private static LocString HandSwapPrompt => CardLoc<ClowChange>("handPrompt");
    private static LocString DiscardSwapPrompt => CardLoc<ClowChange>("discardPrompt");

    public override SakuraElementSet Elements => SakuraElementSet.Earth;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1), new DynamicVar("ExtraCards", 2)];

    // Draw after the swap: drawing first could shuffle the Discard Pile away
    // and leave nothing to swap with.
    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await SwapWithDiscard(choiceContext);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner, false);
    }

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PlayCard(choiceContext, play);
        await CardPileCmd.Draw(choiceContext, ReleasedValue("ExtraCards"), Owner, false);
    }

    private async Task SwapWithDiscard(PlayerChoiceContext choiceContext)
    {
        var handCards = CardPile.GetCards(Owner, PileType.Hand).Where(card => card != this).ToList();
        var discardCards = CardPile.GetCards(Owner, PileType.Discard).ToList();
        if (handCards.Count == 0 || discardCards.Count == 0)
            return;

        var outgoing = (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(HandSwapPrompt, 1) { Cancelable = false },
            card => handCards.Contains(card),
            this)).FirstOrDefault();
        if (outgoing is null)
            return;

        var incoming = (await CardSelectCmd.FromSimpleGrid(
            choiceContext,
            discardCards,
            Owner,
            new CardSelectorPrefs(DiscardSwapPrompt, 1) { Cancelable = false })).FirstOrDefault();
        if (incoming is null)
            return;

        ChangeRules.SwapCostsThisTurn(outgoing, incoming);
        await CardPileCmd.Add(outgoing, PileType.Discard, CardPilePosition.Top, this);
        await CardPileCmd.Add(incoming, PileType.Hand, CardPilePosition.Bottom, this);
    }

    protected override void OnUpgrade() => AddKeywordIfMissing(CardKeyword.Retain);
}

public class SakuraChange() : SakuraFormCard(0, CardType.Skill, TargetType.None)
{
    private static LocString CostSwapPrompt => CardLoc<SakuraChange>("selectionPrompt");

    public override SakuraElementSet Elements => SakuraElementSet.Earth;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(5)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var hand = CardPile.GetCards(Owner, PileType.Hand).Where(card => card != this).ToList();
        if (hand.Count > 0)
            await CardCmd.Discard(choiceContext, hand);
        var drawn = await CardPileCmd.Draw(choiceContext, ReleasedValue("Cards"), Owner, false);

        var candidates = drawn.Where(card => card.Pile?.Type == PileType.Hand && ChangeRules.HasSwappableCost(card)).ToList();
        if (candidates.Count < 2)
            return;

        var selected = (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(CostSwapPrompt, 0, 2) { Cancelable = true },
            card => candidates.Contains(card),
            this)).ToList();
        if (selected.Count == 2)
            ChangeRules.SwapCostsThisTurn(selected[0], selected[1]);
    }
}

internal static class ChangeRules
{
    // X-cost and unplayable cards keep their own cost; only fixed costs trade.
    internal static bool HasSwappableCost(CardModel card) =>
        !card.EnergyCost.CostsX && card.EnergyCost.GetWithModifiers(CostModifiers.None) >= 0;

    internal static void SwapCostsThisTurn(CardModel first, CardModel second)
    {
        if (!HasSwappableCost(first) || !HasSwappableCost(second))
            return;

        var firstCost = first.EnergyCost.GetWithModifiers(CostModifiers.All);
        var secondCost = second.EnergyCost.GetWithModifiers(CostModifiers.All);
        first.EnergyCost.SetThisTurn(secondCost);
        second.EnergyCost.SetThisTurn(firstCost);
    }
}
