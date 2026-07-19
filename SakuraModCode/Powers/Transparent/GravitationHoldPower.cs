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
    private readonly HashSet<CardModel> _excludedSources = [];
    private readonly HashSet<CardModel> _pendingReturns = [];
    private readonly Dictionary<CardModel, int> _returnCounts = [];

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public void ExcludeSource(CardModel card) => _excludedSources.Add(card);

    public override (PileType, CardPilePosition) ModifyCardPlayResultPileTypeAndPosition(
        CardModel card,
        bool isAutoPlay,
        ResourceInfo resources,
        PileType pileType,
        CardPilePosition position)
    {
        if (Amount <= 0
            || card.Owner?.Creature != Owner
            || _excludedSources.Contains(card)
            || pileType != PileType.Discard)
            return (pileType, position);

        _pendingReturns.Add(card);
        return (PileType.Hand, CardPilePosition.Bottom);
    }

    public override Task AfterModifyingCardPlayResultPileOrPosition(
        CardModel card,
        PileType pileType,
        CardPilePosition position)
    {
        if (!_pendingReturns.Remove(card) || pileType != PileType.Hand)
            return Task.CompletedTask;

        _returnCounts[card] = _returnCounts.GetValueOrDefault(card) + 1;
        card.InvokeEnergyCostChanged();
        return Task.CompletedTask;
    }

    public override bool TryModifyEnergyCostInCombat(
        CardModel card,
        decimal currentCost,
        out decimal newCost) =>
        TryIncreaseReturnedCardCost(card, _returnCounts.GetValueOrDefault(card), currentCost, out newCost);

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

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == Owner.Side && participants.Contains(Owner))
            await PowerCmd.Remove(this);
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        foreach (var card in _returnCounts.Keys)
            card.InvokeEnergyCostChanged();

        _excludedSources.Clear();
        _pendingReturns.Clear();
        _returnCounts.Clear();
        return Task.CompletedTask;
    }
}

