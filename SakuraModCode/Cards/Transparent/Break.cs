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

public class Break() : TransparentExtraEffectCard(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Fire];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(8, ValueProp.Move), new PowerVar<VulnerablePower>(1), new DynamicVar("ExtraVulnerable", 2)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        foreach (var target in PlayTargets(play))
        {
            if (target.Block > 0)
                await CreatureCmd.LoseBlock(target, target.Block);

            await DealDamage(choiceContext, target, DynamicVars.Damage.IntValue, hitVfx: SakuraNativeHitFx.RockShatter, hitTmpSfx: SakuraNativeHitFx.BluntTmpSfx);
            var vulnerable = DynamicVars.Vulnerable.IntValue + (activation.IsActive ? DynamicVars["ExtraVulnerable"].IntValue : 0);
            await PowerCmd.Apply<VulnerablePower>(choiceContext, target, vulnerable, Owner.Creature, this, false);
        }
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(4);
}
