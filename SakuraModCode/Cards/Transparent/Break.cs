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

public class Break() : TransparentExtraEffectCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Fire];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(7, ValueProp.Move), new PowerVar<VulnerablePower>(1)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        await SakuraThroughResolution.WithPropagationSuppressed(async () =>
        {
            foreach (var target in SakuraThroughResolution.TargetsFor(play))
            {
                var hadBlock = target.Block > 0;
                if (hadBlock)
                    await CreatureCmd.LoseBlock(target, target.Block);

                var damage = DynamicVars.Damage.IntValue * (hadBlock ? 2 : 1);
                await SakuraActions.Attack(choiceContext, this, target, damage);
                if (activation.IsActive)
                    await ApplyExtraEffect(choiceContext, target);
            }
        });
    }

    private async Task ApplyExtraEffect(PlayerChoiceContext choiceContext, Creature target)
    {
        if (target.HasPower<ArtifactPower>())
            await PowerCmd.Apply<ArtifactPower>(choiceContext, target, -1, Owner.Creature, this, false);
        await PowerCmd.Apply<VulnerablePower>(choiceContext, target, DynamicVars.Vulnerable.IntValue, Owner.Creature, this, false);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3);
        DynamicVars.Vulnerable.UpgradeValueBy(1);
    }
}
