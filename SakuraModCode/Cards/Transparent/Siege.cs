using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Extensions;
using SakuraMod.SakuraModCode.Powers;
using STS2RitsuLib.Cards.DynamicVars;

namespace SakuraMod.SakuraModCode.Cards;

public class Siege() : TransparentExtraEffectCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override bool GainsBlock => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Earth];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new SiegeBlockVar(SiegeRules.BaseBlock, ValueProp.Move),
        new PowerVar<WeakPower>(SiegeRules.WeakAmount)
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        var enemyCount = CombatState!.HittableEnemies.Count();
        var block = SiegeRules.BlockAmount(
            DynamicVars.Block.IntValue,
            SiegeGrowthPower.GrowthAmount(this),
            enemyCount);
        await CreatureCmd.GainBlock(Owner.Creature, block, ValueProp.Move, play, false);
        var pending = await PowerCmd.Apply<SiegePendingPower>(
            choiceContext,
            Owner.Creature,
            1,
            Owner.Creature,
            this,
            false);
        pending?.QueueEffect(activation.IsActive);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(2);
}

internal static class SiegeRules
{
    internal const int BaseBlock = 3;
    internal const int BlockPerEnemy = 2;
    internal const int GrowthPerTrigger = 2;
    internal const int WeakAmount = 1;

    internal static int BlockAmount(int baseBlock, int growth, int enemyCount) =>
        Math.Max(0, baseBlock)
        + Math.Max(0, growth)
        + BlockPerEnemy * Math.Max(0, enemyCount);

    internal static bool ShouldTrigger(int block) => block > 0;

    internal static int ExtraDamage(int block) => Math.Max(0, block);
}

internal sealed class SiegeBlockVar(decimal block, ValueProp props) : BlockVar(block, props)
{
    public override void UpdateCardPreview(CardModel card, CardPreviewMode previewMode, Creature? target, bool runGlobalHooks)
    {
        decimal baseValue = Math.Max(0m, BaseValue + SiegeGrowthPower.GrowthAmount(card));
        var preview = baseValue;
        if (card.Enchantment is not null)
        {
            preview += card.Enchantment.EnchantBlockAdditive(preview);
            preview *= card.Enchantment.EnchantBlockMultiplicative(preview);
            if (!card.IsEnchantmentPreview)
                EnchantedValue = preview;
        }

        if (runGlobalHooks && card.CombatState is not null)
            preview = Hook.ModifyBlock(card.CombatState, card.Owner.Creature, preview, Props, card, null, out _);

        PreviewValue = preview;
    }
}
