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

public class KindnessPower : SakuraPowerModel
{
    private readonly Queue<bool> _pendingEffects = [];
    private readonly HashSet<CardModel> _zeroCostCards = [];
    private CardModel? _targetCard;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public void QueueEffect(bool extraEffect) =>
        _pendingEffects.Enqueue(extraEffect);

    public override (PileType, CardPilePosition) ModifyCardPlayResultPileTypeAndPosition(
        CardModel card,
        bool isAutoPlay,
        ResourceInfo resources,
        PileType pileType,
        CardPilePosition position)
    {
        if (Amount <= 0
            || _targetCard is not null
            || _pendingEffects.Count == 0
            || card.Owner?.Creature != Owner
            || !SakuraSourceCardRules.CanBeTargetedByClearCardEffects(card)
            || pileType != PileType.Exhaust)
            return (pileType, position);

        _targetCard = card;
        if (_pendingEffects.Dequeue())
            _zeroCostCards.Add(card);

        return (PileType.Hand, CardPilePosition.Bottom);
    }

    public override Task AfterModifyingCardPlayResultPileOrPosition(
        CardModel card,
        PileType pileType,
        CardPilePosition position)
    {
        if (card == _targetCard && pileType != PileType.Hand)
            _zeroCostCards.Remove(card);

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (play.Card != _targetCard || play.PlayIndex < play.PlayCount - 1)
            return;

        var card = play.Card;
        if (_zeroCostCards.Remove(card))
        {
            card.EnergyCost.SetThisTurn(0, true);
            card.InvokeEnergyCostChanged();
        }

        _targetCard = null;
        await PowerCmd.Decrement(this);
    }
}

