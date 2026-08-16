using MegaCrit.Sts2.Core;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.Extensions;
using STS2RitsuLib.Combat.HandSize;

namespace SakuraMod.SakuraModCode.Cards;

public class Reversal() : TransparentExtraEffectCard(0, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Wind];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new ReversalDamageVar(0, ValueProp.Move),
        new DynamicVar("PileCardsPerDamage", 3),
        new DynamicVar("PileDamage", 1)
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        var drawCards = CardPile.Get(PileType.Draw, Owner)!.Cards.ToList();
        var discardCards = CardPile.Get(PileType.Discard, Owner)!.Cards.ToList();
        var exchangedCards = drawCards.Count + discardCards.Count;
        PileExchangeVfx.Play(drawCards.Count, discardCards.Count);

        foreach (var card in drawCards)
            await SakuraActions.MoveExistingCardToPileWithoutVisuals(this, card, PileType.Discard, CardPilePosition.Bottom);
        foreach (var card in discardCards)
            await SakuraActions.MoveExistingCardToPileWithoutVisuals(this, card, PileType.Draw, CardPilePosition.Bottom);

        var damage = ReversalRules.TotalDamage(
            DynamicVars.Damage.IntValue,
            exchangedCards,
            DynamicVars["PileCardsPerDamage"].IntValue,
            DynamicVars["PileDamage"].IntValue);
        await SakuraActions.Attack(choiceContext, this, RequiredTarget(play), damage);
        if (activation.IsActive)
            await ApplyExtraEffect(choiceContext, play);
    }

    private async Task ApplyExtraEffect(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var handSize = CardPile.Get(PileType.Hand, Owner)!.Cards.Count;
        var cardsToDraw = MaxHandSizeCalculator.Calculate(Owner) - handSize;
        if (cardsToDraw > 0)
            await CardPileCmd.Draw(choiceContext, cardsToDraw, Owner, false);
    }

    protected override void OnUpgrade() => DynamicVars["PileCardsPerDamage"].UpgradeValueBy(-1);
}

internal static class ReversalRules
{
    internal static int TotalDamage(int baseDamage, int exchangedCards, int cardsPerDamage, int pileDamage) =>
        baseDamage + Math.Max(0, exchangedCards) / Math.Max(1, cardsPerDamage) * Math.Max(0, pileDamage);

    internal static int ExchangedCardCount(CardModel card) =>
        card.IsMutable && card.CombatState is not null
            ? (CardPile.Get(PileType.Draw, card.Owner)?.Cards.Count ?? 0)
                + (CardPile.Get(PileType.Discard, card.Owner)?.Cards.Count ?? 0)
            : 0;
}

internal sealed class ReversalDamageVar(decimal damage, ValueProp props) : DamageVar(damage, props)
{
    public override void UpdateCardPreview(CardModel card, CardPreviewMode previewMode, Creature? target, bool runGlobalHooks)
    {
        var baseValue = ReversalRules.TotalDamage(
            (int)BaseValue,
            ReversalRules.ExchangedCardCount(card),
            card.DynamicVars["PileCardsPerDamage"].IntValue,
            card.DynamicVars["PileDamage"].IntValue);
        decimal preview = baseValue;
        if (card.Enchantment is not null)
        {
            preview += card.Enchantment.EnchantDamageAdditive(preview, Props);
            preview *= card.Enchantment.EnchantDamageMultiplicative(preview, Props);
            if (!card.IsEnchantmentPreview)
                EnchantedValue = preview;
        }

        if (runGlobalHooks)
            preview = Hook.ModifyDamage(card.Owner.RunState, card.CombatState, target, card.Owner.Creature, baseValue, Props, card, ModifyDamageHookType.All, previewMode, out _);

        PreviewValue = preview;
    }
}
