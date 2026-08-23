using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Cards.FreePlay;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.Relics;

namespace SakuraMod.SakuraModCode.Cards;

internal static class SakuraMagicCharge
{
    public const int NormalMagicChargeGain = 1;
    public const int PowerMagicChargeGain = 2;
    public const int ElementOpportunityThreshold = 5;
    public const int ExtraEffectCost = 10;

    public static bool CanSpendMagic(Player? owner) =>
        owner?.Creature.GetPower<ClassicMagicChargePower>()?.Amount >= ExtraEffectCost;

    internal static SakuraMagicChargeBand BandFor(int amount) =>
        amount >= ExtraEffectCost
            ? SakuraMagicChargeBand.Full
            : amount >= ElementOpportunityThreshold
                ? SakuraMagicChargeBand.Resonant
                : SakuraMagicChargeBand.Low;

    internal static SakuraMagicChargeOpportunityTransition OpportunityTransition(int previousAmount, int currentAmount)
    {
        var previousBand = BandFor(previousAmount);
        var currentBand = BandFor(currentAmount);
        if (currentBand == SakuraMagicChargeBand.Resonant && previousBand != SakuraMagicChargeBand.Resonant)
            return SakuraMagicChargeOpportunityTransition.Arm;
        if (currentBand != SakuraMagicChargeBand.Resonant)
            return SakuraMagicChargeOpportunityTransition.Expire;
        return SakuraMagicChargeOpportunityTransition.Preserve;
    }

    internal static SakuraMagicChargeOpportunity? CaptureOpportunity(Player owner)
    {
        var power = owner.Creature.GetPower<ClassicMagicChargePower>();
        if (power is null || BandFor(power.Amount) != SakuraMagicChargeBand.Resonant)
            return null;

        var generation = power.ArmedOpportunityGeneration;
        return generation > 0
            ? new SakuraMagicChargeOpportunity(power, generation)
            : null;
    }

    internal static bool TryConsumeOpportunity(Player owner, SakuraMagicChargeOpportunity opportunity) =>
        ReferenceEquals(owner.Creature.GetPower<ClassicMagicChargePower>(), opportunity.Power)
        && opportunity.Power.TryConsumeOpportunity(opportunity.Generation);

    internal static async Task TryApplyCapturedOpportunity(
        PlayerChoiceContext choiceContext,
        CardModel card,
        SakuraMagicChargeOpportunity? opportunity)
    {
        if (opportunity is not { } captured)
            return;

        if (await SakuraElementState.ApplyMissing(choiceContext, card))
            TryConsumeOpportunity(card.Owner, captured);
    }

    internal static bool GainsMagicAfterPlay(CardModel card, bool hasSealedBook) =>
        card is SakuraCardModel && SakuraTransparentCardCatalog.IsTransparentCard(card)
        || card is SakuraSourceCard { GrantsMagicCharge: true } && hasSealedBook;

    internal static async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardModel card)
    {
        var hasSealedBook = card.Owner?.GetRelic<ClassicSealedBookRelic>() is not null;
        if (!GainsMagicAfterPlay(card, hasSealedBook))
            return;

        await GainMagic(choiceContext, card);
    }

    public static async Task SpendMagic(PlayerChoiceContext choiceContext, Player owner, int amount)
    {
        if (amount > 0)
            await ModifyMagic(choiceContext, owner, -amount, null, false);
    }

    public static async Task SpendUpToMagic(PlayerChoiceContext choiceContext, Player owner, int amount)
    {
        var current = owner.Creature.GetPower<ClassicMagicChargePower>()?.Amount ?? 0;
        if (current <= 0 || amount <= 0)
            return;

        await ModifyMagic(choiceContext, owner, -Math.Min(current, amount), null, false);
    }

    public static async Task<int> SpendAllMagic(PlayerChoiceContext choiceContext, Player owner)
    {
        var amount = owner.Creature.GetPower<ClassicMagicChargePower>()?.Amount ?? 0;
        if (amount <= 0)
            return 0;

        await ModifyMagic(choiceContext, owner, -amount, null, false);
        return amount;
    }

    public static async Task GainMagic(PlayerChoiceContext choiceContext, CardModel card)
    {
        var amount = card.Type == CardType.Power ? PowerMagicChargeGain : NormalMagicChargeGain;
        await GainMagic(choiceContext, card.Owner, amount, card);
    }

    public static async Task GainMagic(
        PlayerChoiceContext choiceContext,
        Player owner,
        int amount,
        CardModel? cardSource = null,
        bool fast = false)
    {
        if (amount > 0)
            await ModifyMagic(choiceContext, owner, amount, cardSource, fast);
    }

    private static async Task ModifyMagic(
        PlayerChoiceContext choiceContext,
        Player owner,
        int delta,
        CardModel? cardSource,
        bool fast)
    {
        if (delta == 0)
            return;

        var previousAmount = owner.Creature.GetPower<ClassicMagicChargePower>()?.Amount ?? 0;
        if (delta > 0)
        {
            await PowerCmd.Apply<ClassicMagicChargePower>(
                choiceContext,
                owner.Creature,
                delta,
                owner.Creature,
                cardSource,
                fast);
        }
        else if (owner.Creature.GetPower<ClassicMagicChargePower>() is { } power)
        {
            await PowerCmd.ModifyAmount(choiceContext, power, delta, owner.Creature, cardSource, fast);
        }

        var currentPower = owner.Creature.GetPower<ClassicMagicChargePower>();
        var currentAmount = currentPower?.Amount ?? 0;
        if (currentPower is not null)
        {
            switch (OpportunityTransition(previousAmount, currentAmount))
            {
                case SakuraMagicChargeOpportunityTransition.Arm:
                    currentPower.ArmNextOpportunity();
                    break;
                case SakuraMagicChargeOpportunityTransition.Expire:
                    currentPower.ExpireOpportunity();
                    break;
            }

            currentPower.NotifyProjectionChanged();
        }

        if (currentPower is not null && currentAmount > previousAmount)
            SakuraGlowVisual.NotifyMagicChargeGained(owner.Creature, currentAmount - previousAmount);
    }

    public static void SetFreeForRestOfTurn(CardModel card)
    {
        card.SetToFreeForRestOfTurn();
    }

    // Shared Void-card creation. Not a Magic Charge resource rule.
    public static async Task AddVoidToDrawPile(PlayerChoiceContext choiceContext, Player owner)
    {
        var combatState = owner.Creature.CombatState
            ?? throw new InvalidOperationException("Sakura generated Void requires an active combat.");
        var card = combatState.CreateCard<MegaCrit.Sts2.Core.Models.Cards.Void>(owner);
        CardCmd.PreviewCardPileAdd(await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombatWithResult(
            card,
            PileType.Draw,
            owner,
            CardPilePosition.Random));
    }

    public static async Task AddVoidToDiscardPile(PlayerChoiceContext choiceContext, Player owner)
    {
        var combatState = owner.Creature.CombatState
            ?? throw new InvalidOperationException("Sakura generated Void requires an active combat.");
        var card = combatState.CreateCard<MegaCrit.Sts2.Core.Models.Cards.Void>(owner);
        CardCmd.PreviewCardPileAdd(await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombatWithResult(
            card,
            PileType.Discard,
            owner,
            CardPilePosition.Bottom));
    }
}

internal enum SakuraMagicChargeBand
{
    Low,
    Resonant,
    Full
}

internal enum SakuraMagicChargeOpportunityTransition
{
    Preserve,
    Arm,
    Expire
}

internal readonly record struct SakuraMagicChargeOpportunity(
    ClassicMagicChargePower Power,
    int Generation);
