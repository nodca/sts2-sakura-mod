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

public class Siege() : TransparentExtraEffectCard(0, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    public override bool GainsBlock => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Earth];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(SiegeRules.BlockPerEnemy, ValueProp.Move),
        new PowerVar<WeakPower>(SiegeRules.WeakAmount)
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        var enemyCount = CombatState!.HittableEnemies.Count();
        var block = SiegeRules.BlockAmount(
            DynamicVars.Block.IntValue,
            enemyCount);
        await CreatureCmd.GainBlock(Owner.Creature, block, ValueProp.Move, play, false);
        if (activation.IsActive)
            await ApplyExtraWeak(choiceContext);
        var pending = await PowerCmd.Apply<SiegePendingPower>(
            choiceContext,
            Owner.Creature,
            1,
            Owner.Creature,
            this,
            false);
        pending?.QueueEffect(DynamicVars.Weak.IntValue);
    }

    private async Task ApplyExtraWeak(PlayerChoiceContext choiceContext)
    {
        var enemies = CombatState!.HittableEnemies.ToList();
        if (enemies.Count == 0)
            return;

        await PowerCmd.Apply<WeakPower>(
            choiceContext,
            enemies,
            SiegeRules.ExtraWeak,
            Owner.Creature,
            this,
            false);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(1);
        DynamicVars.Weak.UpgradeValueBy(1);
    }
}

internal static class SiegeRules
{
    internal const int BlockPerEnemy = 3;
    internal const int WeakAmount = 1;
    internal const int ExtraWeak = 2;

    internal static int BlockAmount(int blockPerEnemy, int enemyCount) =>
        Math.Max(0, blockPerEnemy) * Math.Max(0, enemyCount);

    internal static bool ShouldTrigger(int block) => block > 0;
}
