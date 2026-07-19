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

public class Spiral() : TransparentExtraEffectCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override bool GainsBlock => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Wind];
    internal override IEnumerable<string> ReferencedStaticHoverTipKeys =>
        [SakuraMemoryPile.PileId, SakuraCardHoverTips.TemporaryTipKey];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new SpiralDamageVar(5, ValueProp.Move),
        new SpiralBlockVar(5, ValueProp.Move),
        new DynamicVar("MemoryScale", 1),
        new CardsVar("NextTurnCopies", 1),
        new CardsVar("ExtraCopies", 3)
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        var target = RequiredTarget(play);
        var damage = SpiralRules.ScaledValue(this, DynamicVars.Damage);
        var block = SpiralRules.ScaledValue(this, DynamicVars.Block);
        await SakuraActions.Attack(choiceContext, this, target, damage);
        await CreatureCmd.GainBlock(Owner.Creature, block, ValueProp.Move, play, false);
        if (IsUpgraded)
            await ScheduleNextTurnCopies(choiceContext);
        if (activation.IsActive)
            await ApplyExtraEffect(choiceContext, play);
    }

    private async Task ScheduleNextTurnCopies(PlayerChoiceContext choiceContext)
    {
        var copies = DynamicVars["NextTurnCopies"].IntValue;
        if (copies <= 0)
            return;

        var power = await PowerCmd.Apply<SpiralNextTurnPower>(
            choiceContext,
            Owner.Creature,
            copies,
            Owner.Creature,
            this,
            false);
        for (var i = 0; i < copies; i++)
            power?.QueueCopy(this);
    }

    private async Task ApplyExtraEffect(PlayerChoiceContext choiceContext, CardPlay play)
    {
        for (var i = 0; i < DynamicVars["ExtraCopies"].IntValue; i++)
        {
            await SakuraGeneratedCardLifecycle.AddTemporaryCopyToHand(
                this,
                freeThisTurn: true,
                context: choiceContext);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(1);
        DynamicVars.Block.UpgradeValueBy(1);
    }
}

internal static class SpiralRules
{
    internal static int OutputWithMemory(int baseValue, int memoryCount, int memoryScale = 1) =>
        baseValue + Math.Max(0, memoryCount) * Math.Max(0, memoryScale);

    internal static int ScaledValue(CardModel card, DynamicVar variable)
    {
        var memoryScale = card.DynamicVars.TryGetValue("MemoryScale", out var scaleVar)
            ? (int)scaleVar.BaseValue
            : 0;
        return OutputWithMemory((int)variable.BaseValue, MemoryCount(card), memoryScale);
    }

    private static int MemoryCount(CardModel card) =>
        card.IsMutable && card.CombatState is not null
            ? SakuraMemoryPile.Count(card.Owner)
            : 0;
}

internal sealed class SpiralDamageVar(decimal damage, ValueProp props) : DamageVar(damage, props)
{
    public override void UpdateCardPreview(CardModel card, CardPreviewMode previewMode, Creature? target, bool runGlobalHooks)
    {
        decimal baseValue = SpiralRules.ScaledValue(card, this);
        var preview = baseValue;
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

internal sealed class SpiralBlockVar(decimal block, ValueProp props) : BlockVar(block, props)
{
    public override void UpdateCardPreview(CardModel card, CardPreviewMode previewMode, Creature? target, bool runGlobalHooks)
    {
        decimal baseValue = SpiralRules.ScaledValue(card, this);
        var preview = baseValue;
        if (card.Enchantment is not null)
        {
            preview += card.Enchantment.EnchantBlockAdditive(preview);
            preview *= card.Enchantment.EnchantBlockMultiplicative(preview);
            if (!card.IsEnchantmentPreview)
                EnchantedValue = preview;
        }

        if (runGlobalHooks && card.CombatState is not null)
            preview = Hook.ModifyBlock(card.CombatState, card.Owner.Creature, baseValue, Props, card, null, out _);

        PreviewValue = preview;
    }
}
