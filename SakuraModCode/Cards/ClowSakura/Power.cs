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

public class ClowPower() : ClowExtraEffectCard(2, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    private const int ExtraHpLoss = 15;

    public override SakuraElementSet Elements => SakuraElementSet.Fire;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new SakuraSourceDamageVar(26, ValueProp.Move)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await DealDamage(choiceContext, RequiredTarget(play), ReleasedDamage());
        await ApplyPower<StrengthPower>(choiceContext, Owner.Creature, -1);
    }

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PlayCard(choiceContext, play);
        await DealDamage(choiceContext, RequiredTarget(play), ExtraHpLoss, ValueProp.Unblockable);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(8);
}

public class SakuraPower() : SakuraFormCard(2, CardType.Power, TargetType.None)
{
    public override SakuraElementSet Elements => SakuraElementSet.Fire;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<StrengthPower>(8)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await ApplyPower<StrengthPower>(choiceContext, Owner.Creature, ReleasedValue("StrengthPower"));
}

