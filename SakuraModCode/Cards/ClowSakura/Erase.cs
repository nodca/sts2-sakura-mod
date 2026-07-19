using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.Relics;
using SakuraMod.SakuraModCode.Extensions;
using STS2RitsuLib.Utils;

namespace SakuraMod.SakuraModCode.Cards;

public class ClowErase() : ClowExtraEffectCard(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    private const int NormalKillHpPercent = 33;
    private const int ExtraKillHpPercent = 66;

    public override SakuraElementSet Elements => SakuraElementSet.Wind;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new SakuraSourceDamageVar(9, ValueProp.Move), new DynamicVar("KillPercent", NormalKillHpPercent), new DynamicVar("ExtraKillPercent", ExtraKillHpPercent)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await KillOrDamage(choiceContext, RequiredTarget(play), NormalKillHpPercent);

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await KillOrDamage(choiceContext, RequiredTarget(play), ExtraKillHpPercent);

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(4);

    private async Task KillOrDamage(PlayerChoiceContext choiceContext, Creature target, int killHpPercent)
    {
        if (SakuraEnemyRules.IsMinion(target) || (!SakuraEnemyRules.IsEliteOrBossCombat(target) && target.CurrentHp * 100 <= target.MaxHp * killHpPercent))
        {
            await CreatureCmd.Kill(target, force: true);
            return;
        }

        await DealDamage(choiceContext, target, ReleasedDamage());
    }
}

public class SakuraErase() : SakuraFormCard(3, CardType.Attack, TargetType.AnyEnemy)
{
    private const int EliteBossEnergyRefund = 3;

    public override SakuraElementSet Elements => SakuraElementSet.Wind;

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = RequiredTarget(play);
        if (SakuraEnemyRules.IsEliteOrBossCombat(target))
        {
            await PlayerCmd.GainEnergy(EliteBossEnergyRefund, Owner);
            return;
        }

        foreach (var power in target.Powers.ToList())
            await PowerCmd.Remove(power);

        var hpLoss = Math.Max(0, target.CurrentHp - 1);
        if (hpLoss > 0)
            await CreatureCmd.Damage(choiceContext, target, hpLoss, ValueProp.Unblockable | ValueProp.Unpowered, Owner.Creature, this);
    }
}

