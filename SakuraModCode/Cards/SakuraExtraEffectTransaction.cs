using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.Relics;
using SakuraMod.SakuraModCode.Character;
using System.Runtime.CompilerServices;

namespace SakuraMod.SakuraModCode.Cards;

public readonly record struct SakuraExtraEffectActivation(bool IsActive);

internal enum SakuraExtraEffectActivationCost
{
    MagicCharge,
    LockSakura,
    RedCape
}

internal readonly record struct SakuraExtraEffectPostPlayPlan(
    bool ApplyExtraElementStates)
{
    internal static SakuraExtraEffectPostPlayPlan ForGameplay(
        SakuraExtraEffectActivation activation) =>
        new(ApplyExtraElementStates: activation.IsActive);
}

internal interface ISakuraExtraEffectCard
{
    Task PlayWithExtraEffect(
        PlayerChoiceContext choiceContext,
        CardPlay play,
        SakuraExtraEffectActivation activation);
}

internal static class SakuraExtraEffectTransaction
{
    private static readonly ConditionalWeakTable<CardPlay, ActivatedPlay> ActivatedPlays = new();
    private static readonly ConditionalWeakTable<CardModel, ActiveProjectionStack> ActiveProjections = new();

    internal static bool Supports(CardModel? card) => card is ISakuraExtraEffectCard;

    internal static bool CanActivate(Player? owner) =>
        CanActivate(
            owner?.Creature.GetPower<ClassicMagicChargePower>()?.Amount ?? 0,
            owner?.Creature.GetPower<ClassicLockPower>() is not null);

    internal static bool CanActivate(int magicCharge, bool isLocked) =>
        magicCharge >= SakuraMagicCharge.ExtraEffectCost && !isLocked;

    internal static SakuraExtraEffectActivationCost ActivationCost(bool hasLockSakura) =>
        hasLockSakura
            ? SakuraExtraEffectActivationCost.LockSakura
            : SakuraExtraEffectActivationCost.MagicCharge;

    internal static bool ShouldShowDescription(CardModel? card) =>
        ShouldShowDescription(card, card?.CombatState is not null);

    internal static bool ShouldShowDescription(CardModel? card, bool isInCombat) =>
        card is not null
        && Supports(card)
        && (!isInCombat || ShouldShowAsActive(card));

    internal static bool ShouldShowAsActive(CardModel? card) =>
        card is { IsMutable: true }
        && Supports(card)
        && (IsActivelyProjected(card)
            || card.Owner is { } owner
            && (CanActivate(owner)
                || owner.GetRelic<ClassicRedCapeRelic>()?.CanActivateFreeExtraEffect(card) == true));

    internal static bool IsActivelyProjected(CardModel card) =>
        ActiveProjections.TryGetValue(card, out var projections)
        && projections.Current?.Card == card;

    internal static SourceEraClass? MagicCircleEraFor(
        CardModel? card,
        SakuraExtraEffectActivation activation)
    {
        if (card is null
            || !SakuraCardCatalog.TryGetMetadata(card, out var metadata)
            || metadata.Era is not { } era)
        {
            return null;
        }

        return era == SourceEraClass.Sakura
            || card.Type == CardType.Power
            || activation.IsActive
                ? era
                : null;
    }

    internal static bool DidActivate(CardPlay play) => ActivatedPlays.TryGetValue(play, out _);

    internal static bool DidSpendMagicCharge(CardPlay play) =>
        ActivatedPlays.TryGetValue(play, out var activation)
        && activation.Cost == SakuraExtraEffectActivationCost.MagicCharge;

    internal static void TryShowMagicCircle(CardModel card, SakuraExtraEffectActivation activation)
    {
        if (MagicCircleEraFor(card, activation) is { } era)
            SakuraMagicCirclePresenter.TryShowOrRefresh(card.Owner?.Creature, era);
    }

    internal static async Task Execute(
        CardModel card,
        PlayerChoiceContext choiceContext,
        CardPlay play)
    {
        if (play.Card != card)
            throw new InvalidOperationException("Extra Effect transaction must execute for its own CardPlay.");
        if (card is not ISakuraExtraEffectCard extra)
            throw new InvalidOperationException("Extra Effect transaction only executes for Extra Effect cards.");

        var redCapeActivation = card.Owner.GetRelic<ClassicRedCapeRelic>()?.TryActivateFreeExtraEffect(card) == true;
        var activation = new SakuraExtraEffectActivation(redCapeActivation || CanActivate(card.Owner));
        var opportunity = SakuraMagicCharge.CaptureOpportunity(card.Owner);
        var lockSakura = activation.IsActive
            ? card.Owner.Creature.GetPower<ClassicLockSakuraPower>()
            : null;
        var activationCost = redCapeActivation
            ? SakuraExtraEffectActivationCost.RedCape
            : ActivationCost(lockSakura is not null);

        await ExecuteCore(
            card,
            play,
            activation,
            async () =>
            {
                switch (activationCost)
                {
                    case SakuraExtraEffectActivationCost.MagicCharge:
                        await SakuraMagicCharge.SpendMagic(
                            choiceContext,
                            card.Owner,
                            SakuraMagicCharge.ExtraEffectCost);
                        break;
                    case SakuraExtraEffectActivationCost.LockSakura:
                        await PowerCmd.Decrement(lockSakura!);
                        break;
                    case SakuraExtraEffectActivationCost.RedCape:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            },
            () => SakuraActions.RecordExtraEffectTriggeredThisTurn(choiceContext, play),
            () =>
            {
                TryShowMagicCircle(card, activation);
                return extra.PlayWithExtraEffect(choiceContext, play, activation);
            },
            () => ApplyGameplayPostEffects(card, choiceContext, activation, opportunity),
            activationCost);
    }

    private static async Task ApplyGameplayPostEffects(
        CardModel card,
        PlayerChoiceContext choiceContext,
        SakuraExtraEffectActivation activation,
        SakuraMagicChargeOpportunity? opportunity)
    {
        if (activation.IsActive)
        {
            await SakuraElementState.ApplyMissing(choiceContext, card);
            return;
        }

        await SakuraMagicCharge.TryApplyCapturedOpportunity(choiceContext, card, opportunity);
    }

    private sealed record ActivatedPlay(SakuraExtraEffectActivationCost Cost);

    private static async Task ExecuteCore(
        CardModel card,
        CardPlay play,
        SakuraExtraEffectActivation activation,
        Func<Task> spend,
        Func<Task> record,
        Func<Task> gameplay,
        Func<Task> postPlay,
        SakuraExtraEffectActivationCost activationCost = SakuraExtraEffectActivationCost.MagicCharge)
    {
        ActiveProjectionStack? projections = null;
        if (activation.IsActive)
        {
            projections = ActiveProjections.GetOrCreateValue(card);
            projections.Push(play);
        }

        try
        {
            if (activation.IsActive)
            {
                await spend();
                ActivatedPlays.Add(play, new ActivatedPlay(activationCost));
                await record();
            }

            await gameplay();
            await postPlay();
        }
        finally
        {
            if (activation.IsActive)
            {
                projections!.Pop();
                if (projections.Current is null)
                    ActiveProjections.Remove(card);
            }
        }
    }

    private sealed class ActiveProjectionStack
    {
        private readonly List<CardPlay> _plays = [];

        internal CardPlay? Current => _plays.Count == 0 ? null : _plays[^1];

        internal void Push(CardPlay play) => _plays.Add(play);

        internal void Pop() => _plays.RemoveAt(_plays.Count - 1);
    }
}
