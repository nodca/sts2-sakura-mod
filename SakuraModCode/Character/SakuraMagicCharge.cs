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
    public const int ExtraEffectCost = 10;

    public static bool CanSpendMagic(Player? owner) =>
        owner?.Creature.GetPower<ClassicMagicChargePower>()?.Amount >= ExtraEffectCost;

    // Sealed Book owns every after-play Charge grant; without it no card charges.
    internal static bool GainsMagicAfterPlay(CardModel card, bool hasSealedBook) =>
        hasSealedBook
        && (SakuraTransparentCardCatalog.IsTransparentCard(card)
            || card is SakuraSourceCard { GrantsMagicCharge: true });

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

        var currentAmount = owner.Creature.GetPower<ClassicMagicChargePower>()?.Amount ?? 0;
        if (currentAmount > previousAmount)
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
