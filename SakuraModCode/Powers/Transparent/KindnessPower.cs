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
    private sealed class Data
    {
        public int ExtraPendingCount;
    }

    protected override string IconFileName => "kindness.png";

    private CardModel? _targetCard;
    private bool _returnedWithExtraEffect;

    public override PowerType Type => PowerType.Buff;
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override object InitInternalData() => new Data();

    public void RegisterPendingEffect(bool extraEffect)
    {
        if (extraEffect)
            GetInternalData<Data>().ExtraPendingCount++;
    }

    public override (PileType, CardPilePosition) ModifyCardPlayResultPileTypeAndPosition(
        CardModel card,
        bool isAutoPlay,
        ResourceInfo resources,
        PileType pileType,
        CardPilePosition position)
    {
        if (Amount <= 0
            || _targetCard is not null
            || card.Owner?.Creature != Owner
            || !SakuraSourceCardRules.CanBeTargetedByClearCardEffects(card)
            || pileType != PileType.Exhaust)
            return (pileType, position);

        _targetCard = card;
        var data = GetInternalData<Data>();
        if (data.ExtraPendingCount > 0)
        {
            data.ExtraPendingCount--;
            _returnedWithExtraEffect = true;
        }
        else
        {
            _returnedWithExtraEffect = false;
        }

        return (PileType.Hand, CardPilePosition.Bottom);
    }

    public override Task AfterModifyingCardPlayResultPileOrPosition(
        CardModel card,
        PileType pileType,
        CardPilePosition position)
    {
        if (card != _targetCard || pileType == PileType.Hand)
            return Task.CompletedTask;

        if (_returnedWithExtraEffect)
            GetInternalData<Data>().ExtraPendingCount++;

        _targetCard = null;
        _returnedWithExtraEffect = false;
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (play.Card != _targetCard || play.PlayIndex < play.PlayCount - 1)
            return;

        var card = play.Card;
        if (_returnedWithExtraEffect)
        {
            card.EnergyCost.SetThisTurn(0, true);
            card.InvokeEnergyCostChanged();
        }

        _targetCard = null;
        _returnedWithExtraEffect = false;
        await PowerCmd.Decrement(this);
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        _targetCard = null;
        _returnedWithExtraEffect = false;
        return Task.CompletedTask;
    }
}
