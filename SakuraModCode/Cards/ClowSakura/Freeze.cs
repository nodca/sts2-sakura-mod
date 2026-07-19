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

public class ClowFreeze() : ClowExtraEffectCard(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override bool GainsBlock => true;
    public override SakuraElementSet Elements => SakuraElementSet.Water;
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new SakuraSourceDamageVar(14, ValueProp.Move),
        new SakuraSourceBlockVar(6, ValueProp.Move),
        new PowerVar<SakuraFrostbitePower>(2)
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await GainBlock(play, ReleasedBlock());
        await SakuraThroughResolution.WithPropagationSuppressed(async () =>
        {
            foreach (var target in SakuraThroughResolution.TargetsFor(play))
            {
                await DealDamage(choiceContext, target, ReleasedDamage());
                await ApplyPower<SakuraFrostbitePower>(choiceContext, target, ReleasedValue("SakuraFrostbitePower"));
            }
        });
    }

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await GainBlock(play, ReleasedBlock());
        await SakuraThroughResolution.WithPropagationSuppressed(async () =>
        {
            foreach (var target in SakuraThroughResolution.TargetsFor(play))
            {
                await DealDamage(choiceContext, target, ReleasedDamage());
                var frostbite = ReleasedValue("SakuraFrostbitePower");
                var existingFrostbite = target.GetPower<SakuraFrostbitePower>()?.Amount ?? 0;
                await ApplyPower<SakuraFrostbitePower>(
                    choiceContext,
                    target,
                    SakuraFreezeRules.DoubledApplicationAmount(existingFrostbite, frostbite));
            }
        });
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4);
        DynamicVars.Block.UpgradeValueBy(2);
    }
}

public class SakuraFreeze() : SakuraFormCard(2, CardType.Attack, TargetType.AnyEnemy)
{
    public override bool GainsBlock => true;
    public override SakuraElementSet Elements => SakuraElementSet.Water;
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new SakuraSourceDamageVar(22, ValueProp.Move),
        new SakuraSourceBlockVar(10, ValueProp.Move),
        new PowerVar<SakuraFrostbitePower>(3),
        new DynamicVar("ExtraFrostbite", 3)
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await GainBlock(play, ReleasedBlock());
        await SakuraThroughResolution.WithPropagationSuppressed(async () =>
        {
            foreach (var target in SakuraThroughResolution.TargetsFor(play))
            {
                await DealDamage(choiceContext, target, ReleasedDamage());
                var isEliteOrBossTarget = SakuraEnemyRules.IsEliteOrBossTarget(target);
                var frostbite = SakuraFreezeRules.FrostbiteAmount(
                    ReleasedValue("SakuraFrostbitePower"),
                    ReleasedValue("ExtraFrostbite"),
                    isEliteOrBossTarget);
                await ApplyPower<SakuraFrostbitePower>(choiceContext, target, frostbite);
            }
        });
    }
}
