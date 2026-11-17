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

public class Reversal() : TransparentExtraEffectCard(0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Wind];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new ReversalBlockVar(0, ValueProp.Move),
        new ReversalDamageVar(0, ValueProp.Move),
        new DynamicVar("PileCards", 3),
        new DynamicVar("PileBonus", 1)
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        var drawCards = CardPile.Get(PileType.Draw, Owner)!.Cards.ToList();
        var discardCards = CardPile.Get(PileType.Discard, Owner)!.Cards.ToList();
        var exchangedCards = drawCards.Count + discardCards.Count;
        PileExchangeVfx.Play(
            Owner,
            PileType.Draw,
            PileType.Discard,
            drawCards.Count,
            discardCards.Count,
            PileExchangeMotif.Reversal);

        foreach (var card in drawCards)
            await SakuraActions.MoveExistingCardToPileWithoutVisuals(this, card, PileType.Discard, CardPilePosition.Bottom);
        foreach (var card in discardCards)
            await SakuraActions.MoveExistingCardToPileWithoutVisuals(this, card, PileType.Draw, CardPilePosition.Bottom);

        await ClassicLoopPower.AfterPileExchange(choiceContext, Owner, exchangedCards);

        var block = ReversalRules.ScaledValue(DynamicVars.Block.IntValue, exchangedCards, PileCards, PileBonus);
        if (block > 0)
            await CreatureCmd.GainBlock(Owner.Creature, block, ValueProp.Move, play, false);

        var damage = ReversalRules.ScaledValue(DynamicVars.Damage.IntValue, exchangedCards, PileCards, PileBonus);
        await DealDamage(choiceContext, play, RequiredTarget(play), damage, hitVfx: SakuraNativeHitFx.Slash);
        if (activation.IsActive)
            await ApplyExtraEffect(choiceContext, play);
    }

    private async Task ApplyExtraEffect(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var handSize = CardPile.Get(PileType.Hand, Owner)!.Cards.Count;
        var cardsToDraw = ReversalRules.ExtraDrawTarget(MaxHandSizeCalculator.Calculate(Owner)) - handSize;
        if (cardsToDraw > 0)
            await CardPileCmd.Draw(choiceContext, cardsToDraw, Owner, false);
    }

    private int PileCards => DynamicVars["PileCards"].IntValue;
    private int PileBonus => DynamicVars["PileBonus"].IntValue;

    protected override void OnUpgrade() => DynamicVars["PileCards"].UpgradeValueBy(-1);
}

internal static class ReversalRules
{
    internal static int ScaledValue(int baseValue, int exchangedCards, int pileCards, int pileBonus) =>
        baseValue + Math.Max(0, exchangedCards) / Math.Max(1, pileCards) * Math.Max(0, pileBonus);

    internal static int ExtraDrawTarget(int maxHandSize) => Math.Max(0, maxHandSize) / 2;

    internal static int PreviewValue(CardModel card, decimal baseValue) =>
        ScaledValue(
            (int)baseValue,
            ExchangedCardCount(card),
            card.DynamicVars["PileCards"].IntValue,
            card.DynamicVars["PileBonus"].IntValue);

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
        var baseValue = ReversalRules.PreviewValue(card, BaseValue);
        decimal preview = baseValue;
        if (card.Enchantment is not null)
        {
            preview += card.Enchantment.EnchantDamageAdditive(preview, Props);
            preview *= card.Enchantment.EnchantDamageMultiplicative(preview, Props);
            if (!card.IsEnchantmentPreview)
                EnchantedValue = preview;
        }

        if (runGlobalHooks)
            preview = Hook.ModifyDamage(card.Owner.RunState, card.CombatState, target, card.Owner.Creature, baseValue, Props, card, null, ModifyDamageHookType.All, previewMode, out _);

        PreviewValue = preview;
    }
}

internal sealed class ReversalBlockVar(decimal block, ValueProp props) : BlockVar(block, props)
{
    public override void UpdateCardPreview(CardModel card, CardPreviewMode previewMode, Creature? target, bool runGlobalHooks)
    {
        var baseValue = ReversalRules.PreviewValue(card, BaseValue);
        decimal preview = baseValue;
        if (card.Enchantment is not null)
        {
            preview += card.Enchantment.EnchantBlockAdditive(preview);
            preview *= card.Enchantment.EnchantBlockMultiplicative(preview);
            if (!card.IsEnchantmentPreview)
                EnchantedValue = preview;
        }

        if (runGlobalHooks && card.CombatState is { } combatState)
            preview = Hook.ModifyBlock(combatState, card.Owner.Creature, baseValue, Props, card, null, out _);

        PreviewValue = preview;
    }
}
