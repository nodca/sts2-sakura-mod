using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
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
    private const uint NoPendingTarget = uint.MaxValue;

    private sealed class Data
    {
        public int ExtraPendingCount;
        public uint PendingTargetCombatCardIndex = NoPendingTarget;
        public bool ReturnedWithExtraEffect;
    }

    protected override string IconFileName => "kindness.png";

    public override PowerType Type => PowerType.Buff;
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override object InitInternalData() => new Data();

    private Data State => GetInternalData<Data>();

    public void RegisterPendingEffect(bool extraEffect)
    {
        if (extraEffect)
            State.ExtraPendingCount++;
    }

    public override (PileType, CardPilePosition) ModifyCardPlayResultPileTypeAndPosition(
        CardModel card,
        bool isAutoPlay,
        ResourceInfo resources,
        PileType pileType,
        CardPilePosition position)
    {
        if (!IsEligibleIntercept(card, pileType)
            || State.PendingTargetCombatCardIndex != NoPendingTarget)
            return (pileType, position);

        return (PileType.Hand, CardPilePosition.Bottom);
    }

    public override Task AfterModifyingCardPlayResultPileOrPosition(
        CardModel card,
        PileType pileType,
        CardPilePosition position)
    {
        if (pileType == PileType.Hand
            && IsEligibleIntercept(card, PileType.Exhaust)
            && State.PendingTargetCombatCardIndex == NoPendingTarget)
        {
            State.PendingTargetCombatCardIndex = NetCombatCard.FromModel(card).CombatCardIndex;
            if (State.ExtraPendingCount > 0)
            {
                State.ExtraPendingCount--;
                State.ReturnedWithExtraEffect = true;
            }
            else
            {
                State.ReturnedWithExtraEffect = false;
            }

            return Task.CompletedTask;
        }

        if (State.PendingTargetCombatCardIndex != NoPendingTarget
            && ReferenceEquals(card, ResolvePendingTargetCard())
            && pileType != PileType.Hand)
        {
            if (State.ReturnedWithExtraEffect)
                State.ExtraPendingCount++;

            ClearPendingTarget();
        }

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (play.Card != ResolvePendingTargetCard() || play.PlayIndex < play.PlayCount - 1)
            return;

        var card = play.Card;
        if (State.ReturnedWithExtraEffect)
        {
            card.EnergyCost.SetThisTurn(0, true);
            card.InvokeEnergyCostChanged();
        }

        ClearPendingTarget();
        await PowerCmd.Decrement(this);
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        ClearPendingTarget();
        return Task.CompletedTask;
    }

    private bool IsEligibleIntercept(CardModel card, PileType pileType) =>
        Amount > 0
        && card.Owner?.Creature == Owner
        && SakuraSourceCardRules.CanBeTargetedByClearCardEffects(card)
        && pileType == PileType.Exhaust;

    private CardModel? ResolvePendingTargetCard()
    {
        if (State.PendingTargetCombatCardIndex == NoPendingTarget
            || Owner.Player?.PlayerCombatState is not { } playerCombat)
            return null;

        return playerCombat.AllCards.FirstOrDefault(card =>
            NetCombatCard.FromModel(card).CombatCardIndex == State.PendingTargetCombatCardIndex);
    }

    private void ClearPendingTarget()
    {
        State.PendingTargetCombatCardIndex = NoPendingTarget;
        State.ReturnedWithExtraEffect = false;
    }
}
