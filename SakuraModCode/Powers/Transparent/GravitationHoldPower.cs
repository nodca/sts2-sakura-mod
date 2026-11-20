using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Cards;

namespace SakuraMod.SakuraModCode.Powers;

public class GravitationHoldPower : SakuraPowerModel
{
    protected override string IconFileName => "gravitation_hold.png";

    private sealed class Data
    {
        public readonly Dictionary<uint, PendingPlayReturn> PendingPlays = [];
        public readonly Dictionary<uint, int> ReturnCounts = [];
        public readonly HashSet<uint> LeftHandForDiscard = [];
    }

    private readonly record struct PendingPlayReturn(
        int PlayedLocalCost, PlayerChoiceContext? ChoiceContext = null);

    protected override object InitInternalData() => new Data();

    private Data State => GetInternalData<Data>();

    internal const string ReturnDamageVar = "ReturnDamage";

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar(ReturnDamageVar, 0)];

    // Each Gravitation played this turn adds its damage.
    public void AddReturnDamage(int damage)
    {
        if (damage <= 0)
            return;

        DynamicVars[ReturnDamageVar].BaseValue += damage;
        InvokeDisplayAmountChanged();
    }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    internal bool CanReturnPlayedCard(CardModel card) =>
        Amount > 0 && card.Owner?.Creature == Owner && !card.EnergyCost.CostsX;

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        GravitationHoldVisual.Mount(Owner);
        return Task.CompletedTask;
    }

    public override CardLocation ModifyCardPlayResultLocation(
        CardModel card,
        bool isAutoPlay,
        ResourceInfo resources,
        CardLocation cardLocation)
    {
        if (cardLocation.pileType != PileType.Discard
            || cardLocation.player != card.Owner
            || !CanReturnPlayedCard(card))
            return cardLocation;

        return cardLocation with
        {
            pileType = PileType.Hand,
            position = CardPilePosition.Bottom
        };
    }

    public override Task AfterModifyingCardPlayResultLocation(
        CardModel card,
        CardLocation cardLocation)
    {
        if (cardLocation.pileType == PileType.Hand
            && cardLocation.player == card.Owner
            && CanReturnPlayedCard(card)
            && NetCombatCardDb.Instance.TryGetCardId(card, out var id))
        {
            State.PendingPlays[id] = new PendingPlayReturn(card.EnergyCost.GetWithModifiers(CostModifiers.Local));
        }
        return Task.CompletedTask;
    }

    public override bool TryModifyEnergyCostInCombat(
        CardModel card,
        decimal currentCost,
        out decimal newCost)
    {
        if (card.IsMutable && card.Owner?.Creature == Owner
            && NetCombatCardDb.Instance.TryGetCardId(card, out var id))
        {
            return TryIncreaseReturnedCardCost(card, State.ReturnCounts.GetValueOrDefault(id), currentCost, out newCost);
        }

        newCost = currentCost;
        return false;
    }

    // Native play cleanup drops "this turn or until played" discounts after the card
    // lands in Hand, so pin the local cost it was played at for the rest of the turn.
    internal static void PreservePlayedLocalCost(CardModel card, int playedCost)
    {
        if (playedCost >= 0)
            card.EnergyCost.SetThisTurn(playedCost, reduceOnly: true);
    }

    internal static bool TryIncreaseReturnedCardCost(
        CardModel card,
        int returnCount,
        decimal currentCost,
        out decimal newCost)
    {
        if (card.EnergyCost.CostsX || currentCost < 0 || returnCount <= 0)
        {
            newCost = currentCost;
            return false;
        }

        newCost = currentCost + returnCount;
        return true;
    }

    public override Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? clonedBy)
    {
        if (card.Owner?.Creature != Owner || !NetCombatCardDb.Instance.TryGetCardId(card, out var id))
            return Task.CompletedTask;

        // Only the latest move can pair with AfterCardDiscarded. Swaps and the hand flush
        // have no discard hook, and shuffling the card away invalidates the record.
        if (oldPileType == PileType.Hand && card.Pile?.Type == PileType.Discard && CanReturnPlayedCard(card))
            State.LeftHandForDiscard.Add(id);
        else
            State.LeftHandForDiscard.Remove(id);

        // The result location is only a request: native Add sends the card to Discard
        // if its effect filled the Hand. Charge and damage only after a real return.
        if (oldPileType == PileType.Play
            && State.PendingPlays.Remove(id, out var pending)
            && pending.ChoiceContext is { } choiceContext
            && card.Pile?.Type == PileType.Hand)
        {
            PreservePlayedLocalCost(card, pending.PlayedLocalCost);
            CountReturn(id, card);
            return DealPlayedReturnDamage(choiceContext);
        }

        return Task.CompletedTask;
    }

    // Sly cards are skipped: native Sly autoplays them, and they then return as played cards.
    public override async Task AfterCardDiscarded(PlayerChoiceContext choiceContext, CardModel card)
    {
        if (card.Owner?.Creature != Owner
            || !NetCombatCardDb.Instance.TryGetCardId(card, out var id)
            || !State.LeftHandForDiscard.Remove(id)
            || card.IsSlyThisTurn
            || !CanReturnPlayedCard(card)
            || card.Pile?.Type != PileType.Discard)
            return;

        await CardPileCmd.Add(card, PileType.Hand);
        if (card.Pile?.Type != PileType.Hand)
            return;

        CountReturn(id, card);
        await DealReturnDamage(choiceContext);
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // The pile-change hook has no choice context. Carry the one from the
        // last play in the series to the native move that follows it.
        if (cardPlay.IsLastInSeries && cardPlay.ResultPile == PileType.Hand
            && cardPlay.Card.Owner?.Creature == Owner
            && NetCombatCardDb.Instance.TryGetCardId(cardPlay.Card, out var id)
            && State.PendingPlays.TryGetValue(id, out var pending))
        {
            State.PendingPlays[id] = pending with { ChoiceContext = choiceContext };
        }

        return Task.CompletedTask;
    }

    private void CountReturn(uint id, CardModel card)
    {
        State.ReturnCounts[id] = State.ReturnCounts.GetValueOrDefault(id) + 1;
        card.InvokeEnergyCostChanged();
        GravitationHoldVisual.NotifyReturned(Owner, card);
    }

    private async Task DealPlayedReturnDamage(PlayerChoiceContext choiceContext)
    {
        choiceContext.PushModel(this);
        try
        {
            await DealReturnDamage(choiceContext);
        }
        finally
        {
            choiceContext.PopModel(this);
        }
    }

    private async Task DealReturnDamage(PlayerChoiceContext choiceContext)
    {
        var damage = DynamicVars[ReturnDamageVar].IntValue;
        if (damage <= 0 || CombatState is not { } combatState)
            return;

        Flash();
        await CreatureCmd.Damage(choiceContext, combatState.HittableEnemies, damage, SakuraPowerValueProps.Damage, Owner);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == Owner.Side && participants.Contains(Owner))
            await PowerCmd.Remove(this);
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        foreach (var id in State.ReturnCounts.Keys)
            if (NetCombatCardDb.Instance.TryGetCard(id, out var card))
                card!.InvokeEnergyCostChanged();

        State.PendingPlays.Clear();
        State.ReturnCounts.Clear();
        State.LeftHandForDiscard.Clear();
        GravitationHoldVisual.NotifyRemoved(oldOwner);
        return Task.CompletedTask;
    }
}
