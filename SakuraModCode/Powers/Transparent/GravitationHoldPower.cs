using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.Extensions;
using STS2RitsuLib.Combat.HandSize;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace SakuraMod.SakuraModCode.Powers;

public class GravitationHoldPower : SakuraPowerModel
{
    protected override string IconFileName => "gravitation_hold.png";

    private readonly HashSet<CardModel> _pendingReturns = [];
    private readonly Dictionary<CardModel, int> _returnCounts = [];
    private readonly HashSet<CardModel> _pendingReturnDamage = [];
    private readonly HashSet<CardModel> _leftHandForDiscard = [];

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
        if (!CanReturnPlayedCard(card) || cardLocation.pileType != PileType.Discard)
            return cardLocation;

        _pendingReturns.Add(card);
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
        if (!_pendingReturns.Remove(card) || cardLocation.pileType != PileType.Hand)
            return Task.CompletedTask;

        PreservePlayedLocalCost(card);
        _returnCounts[card] = _returnCounts.GetValueOrDefault(card) + 1;
        _pendingReturnDamage.Add(card);
        card.InvokeEnergyCostChanged();
        GravitationHoldVisual.NotifyReturned(Owner, card);
        return Task.CompletedTask;
    }

    public override bool TryModifyEnergyCostInCombat(
        CardModel card,
        decimal currentCost,
        out decimal newCost) =>
        TryIncreaseReturnedCardCost(card, _returnCounts.GetValueOrDefault(card), currentCost, out newCost);

    // Native play cleanup drops "this turn or until played" discounts after the card
    // lands in Hand, so pin the local cost it was played at for the rest of the turn.
    internal static void PreservePlayedLocalCost(CardModel card)
    {
        var playedCost = card.EnergyCost.GetWithModifiers(CostModifiers.Local);
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

    // Discard follows native Sly: only CardCmd.Discard counts (AfterCardDiscarded), never the
    // end-of-turn hand flush, and only for cards that left the Hand (not Sakura Arrow's Draw Pile).
    public override Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? clonedBy)
    {
        // Only the latest move counts: a hand-to-discard move that was not a discard (Clow Change's
        // swap, the end-of-turn flush) is forgotten once the card moves again, e.g. shuffled into
        // the Draw Pile and later discarded from there by Sakura Arrow.
        if (oldPileType == PileType.Hand && card.Pile?.Type == PileType.Discard && CanReturnPlayedCard(card))
            _leftHandForDiscard.Add(card);
        else
            _leftHandForDiscard.Remove(card);
        return Task.CompletedTask;
    }

    // Sly cards are skipped: native Sly autoplays them, and they then return as played cards.
    public override async Task AfterCardDiscarded(PlayerChoiceContext choiceContext, CardModel card)
    {
        if (!_leftHandForDiscard.Remove(card)
            || card.IsSlyThisTurn
            || !CanReturnPlayedCard(card)
            || card.Pile?.Type != PileType.Discard)
            return;

        await CardPileCmd.Add(card, PileType.Hand);
        if (card.Pile?.Type != PileType.Hand)
            return;

        _returnCounts[card] = _returnCounts.GetValueOrDefault(card) + 1;
        card.InvokeEnergyCostChanged();
        GravitationHoldVisual.NotifyReturned(Owner, card);
        await DealReturnDamage(choiceContext);
    }

    // The return is decided before the card resolves, so hit once after its last
    // play (Replay included), like Panache's Unpowered damage to all enemies.
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!IsLastPlay(cardPlay.PlayIndex, cardPlay.PlayCount) || !_pendingReturnDamage.Remove(cardPlay.Card))
            return;

        await DealReturnDamage(choiceContext);
    }

    private async Task DealReturnDamage(PlayerChoiceContext choiceContext)
    {
        var damage = DynamicVars[ReturnDamageVar].IntValue;
        if (damage <= 0 || CombatState is not { } combatState)
            return;

        Flash();
        await CreatureCmd.Damage(choiceContext, combatState.HittableEnemies, damage, SakuraPowerValueProps.Damage, Owner);
    }

    internal static bool IsLastPlay(int playIndex, int playCount) => playIndex >= playCount - 1;

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == Owner.Side && participants.Contains(Owner))
            await PowerCmd.Remove(this);
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        foreach (var card in _returnCounts.Keys)
            card.InvokeEnergyCostChanged();

        _pendingReturns.Clear();
        _returnCounts.Clear();
        _pendingReturnDamage.Clear();
        _leftHandForDiscard.Clear();
        GravitationHoldVisual.NotifyRemoved(oldOwner);
        return Task.CompletedTask;
    }
}
