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

public class ClowSand() : ClowExtraEffectCard(0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    private const int ExtraPoison = 7;

    public override SakuraElementSet Elements => SakuraElementSet.Earth;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new SakuraSourceDamageVar(3, ValueProp.Move), new PowerVar<PoisonPower>(2)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = RequiredTarget(play);
        await DealDamage(choiceContext, target, ReleasedDamage());
        await ApplyPower<PoisonPower>(choiceContext, target, ReleasedValue("PoisonPower"));
    }

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PlayCard(choiceContext, play);
        await ApplyPower<PoisonPower>(choiceContext, RequiredTarget(play), ExtraPoison);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2);
        DynamicVars["PoisonPower"].UpgradeValueBy(1);
    }
}

public class SakuraSand() : SakuraFormCard(1, CardType.Skill, TargetType.None)
{
    private const int PoisonTriggerCount = 2;
    private const int PoisonPerApplication = 1;
    private const int PoisonApplicationCount = 9;

    public override SakuraElementSet Elements => SakuraElementSet.Earth;
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Triggers", PoisonTriggerCount),
        new PowerVar<PoisonPower>(PoisonPerApplication),
        new DynamicVar("Applications", PoisonApplicationCount)
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await TriggerCurrentPoison(choiceContext, CombatState!.HittableEnemies, DynamicVars["Triggers"].IntValue);
        for (var i = 0; i < DynamicVars["Applications"].IntValue; i++)
            await ApplyPowerToEnemies<PoisonPower>(choiceContext, DynamicVars["PoisonPower"].IntValue);
    }
}

