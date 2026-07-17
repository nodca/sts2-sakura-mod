using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Classic.Cards;
using SakuraMod.SakuraModCode.Classic.Powers;
using SakuraMod.SakuraModCode.Classic.Relics;
using SakuraMod.SakuraModCode.Character;
using System.Runtime.CompilerServices;

namespace SakuraMod.SakuraModCode.Cards;

public readonly record struct SakuraExtraEffectActivation(bool IsActive);

internal enum SakuraExtraEffectActivationCost
{
    MagicCharge,
    LockSakura
}

internal readonly record struct SakuraExtraEffectPostPlayPlan(
    bool ApplyExtraElementStates,
    bool AddSakuraVoid,
    bool GainTransparentMagic,
    bool MayGainClassicMagic)
{
    internal static SakuraExtraEffectPostPlayPlan ForGameplay(
        CardModel card,
        SakuraExtraEffectActivation activation) =>
        new(
            ApplyExtraElementStates: activation.IsActive,
            AddSakuraVoid: !activation.IsActive && card is ClassicSakuraCard { AddsVoidOnNormalSakuraPlay: true },
            GainTransparentMagic: false,
            MayGainClassicMagic: false);

    internal static SakuraExtraEffectPostPlayPlan ForAfterCardPlayed(CardModel card)
    {
        var isTransparent = card is SakuraModCard && SakuraTransparentCardCatalog.IsTransparentCard(card);
        return new(
            ApplyExtraElementStates: false,
            AddSakuraVoid: false,
            GainTransparentMagic: isTransparent,
            MayGainClassicMagic: card is ClassicSakuraCard { GrantsMagicCharge: true });
    }
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
        magicCharge >= ClassicSakuraMagic.ExtraEffectCost && !isLocked;

    internal static SakuraExtraEffectActivationCost ActivationCost(bool hasLockSakura) =>
        hasLockSakura
            ? SakuraExtraEffectActivationCost.LockSakura
            : SakuraExtraEffectActivationCost.MagicCharge;

    internal static bool ShouldShowAsActive(CardModel? card) =>
        card is { IsMutable: true, Owner: not null }
        && Supports(card)
        && (IsActivelyProjected(card) || CanActivate(card.Owner));

    internal static bool IsActivelyProjected(CardModel card) =>
        ActiveProjections.TryGetValue(card, out var projections)
        && projections.Current?.Card == card;

    internal static bool DidActivate(CardPlay play) => ActivatedPlays.TryGetValue(play, out _);

    internal static bool DidSpendMagicCharge(CardPlay play) =>
        ActivatedPlays.TryGetValue(play, out var activation)
        && activation.Cost == SakuraExtraEffectActivationCost.MagicCharge;

    internal static async Task Execute(
        CardModel card,
        PlayerChoiceContext choiceContext,
        CardPlay play,
        Func<PlayerChoiceContext, CardPlay, Task> playWithoutExtraEffect)
    {
        if (play.Card != card)
            throw new InvalidOperationException("Extra Effect transaction must execute for its own CardPlay.");

        var capability = card as ISakuraExtraEffectCard;
        var activation = new SakuraExtraEffectActivation(capability is not null && CanActivate(card.Owner));
        var opportunity = ClassicSakuraMagic.CaptureOpportunity(card.Owner);
        var lockSakura = activation.IsActive
            ? card.Owner.Creature.GetPower<ClassicLockSakuraPower>()
            : null;
        var activationCost = ActivationCost(lockSakura is not null);

        await ExecuteCore(
            card,
            play,
            activation,
            async () =>
            {
                switch (activationCost)
                {
                    case SakuraExtraEffectActivationCost.MagicCharge:
                        await ClassicSakuraMagic.SpendMagic(
                            choiceContext,
                            card.Owner,
                            ClassicSakuraMagic.ExtraEffectCost);
                        break;
                    case SakuraExtraEffectActivationCost.LockSakura:
                        await PowerCmd.Decrement(lockSakura!);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            },
            () => SakuraActions.RecordExtraEffectTriggeredThisTurn(choiceContext, play),
            () => capability is not null
                ? capability.PlayWithExtraEffect(choiceContext, play, activation)
                : playWithoutExtraEffect(choiceContext, play),
            () => ApplyGameplayPostEffects(card, choiceContext, activation, opportunity),
            activationCost);
    }

    internal static Task ExecuteCoreForTests(
        CardModel card,
        CardPlay play,
        SakuraExtraEffectActivation activation,
        Func<Task> spend,
        Func<Task> record,
        Func<Task> gameplay,
        Func<Task> postPlay,
        SakuraExtraEffectActivationCost activationCost = SakuraExtraEffectActivationCost.MagicCharge) =>
        ExecuteCore(card, play, activation, spend, record, gameplay, postPlay, activationCost);

    internal static async Task AfterCardPlayed(
        CardModel card,
        PlayerChoiceContext choiceContext,
        CardPlay play)
    {
        if (play.Card != card)
            return;

        var plan = SakuraExtraEffectPostPlayPlan.ForAfterCardPlayed(card);

        if (plan.GainTransparentMagic)
        {
            await ClassicSakuraMagic.GainMagic(choiceContext, card);
            return;
        }

        if (plan.MayGainClassicMagic
            && card.Owner.GetRelic<ClassicSealedBookRelic>() is not null)
        {
            await ClassicSakuraMagic.GainMagic(choiceContext, card);
        }
    }

    private static async Task ApplyGameplayPostEffects(
        CardModel card,
        PlayerChoiceContext choiceContext,
        SakuraExtraEffectActivation activation,
        ClassicMagicChargeOpportunity? opportunity)
    {
        var plan = SakuraExtraEffectPostPlayPlan.ForGameplay(card, activation);
        if (plan.ApplyExtraElementStates)
        {
            await SakuraActions.ApplyMissingClassicElementStates(choiceContext, card);
        }
        else if (opportunity is { } captured
                 && await SakuraActions.ApplyMissingClassicElementStates(choiceContext, card))
        {
            ClassicSakuraMagic.TryConsumeOpportunity(card.Owner, captured);
        }

        if (plan.AddSakuraVoid)
            await ClassicSakuraMagic.AddVoidToDrawPile(choiceContext, card.Owner);
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
        SakuraExtraEffectActivationCost activationCost)
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
