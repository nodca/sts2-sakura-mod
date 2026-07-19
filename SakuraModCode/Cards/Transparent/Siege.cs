using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;
using STS2RitsuLib.Cards.DynamicVars;

namespace SakuraMod.SakuraModCode.Cards;

public class Siege() : TransparentExtraEffectCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override bool GainsBlock => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Earth];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(SiegeRules.BaseBlock, ValueProp.Move),
        new PowerVar<WeakPower>(SiegeRules.WeakAmount)
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        var enemyCount = CombatState!.HittableEnemies.Count();
        var block = SiegeRules.BlockAmount(
            DynamicVars.Block.IntValue,
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
    internal const int WeakAmount = 1;

    internal static int BlockAmount(int baseBlock, int enemyCount) =>
        Math.Max(0, baseBlock)
        + BlockPerEnemy * Math.Max(0, enemyCount);

    internal static bool ShouldTrigger(int block) => block > 0;

    internal static int ExtraDamage(int block) => Math.Max(0, block);
}
