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

public class ClowShot() : ClowExtraEffectCard(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    private const int Hits = 2;
    private const int ExtraVulnerable = 3;

    public override SakuraElementSet Elements => SakuraElementSet.Fire;
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new SakuraSourceDamageVar(4, ValueProp.Move),
        new DynamicVar("Hits", Hits),
        new PowerVar<VigorPower>(2)
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await DealDamage(choiceContext, RequiredTarget(play), ReleasedDamage(), hitCount: Hits);
        await ApplyPower<VigorPower>(choiceContext, Owner.Creature, ReleasedValue("VigorPower"));
    }

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PlayCard(choiceContext, play);
        await ApplyPower<VulnerablePower>(choiceContext, RequiredTarget(play), ExtraVulnerable);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(1);
    }
}

public class SakuraShot() : SakuraFormCard(1, CardType.Attack, TargetType.AnyEnemy)
{
    public override SakuraElementSet Elements => SakuraElementSet.Fire;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new SakuraSourceDamageVar(1, ValueProp.Move), new DynamicVar("Magic", 12)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = RequiredTarget(play);
        await DealDamage(choiceContext, target, ReleasedDamage(), hitCount: ReleasedMagic());
    }
}

