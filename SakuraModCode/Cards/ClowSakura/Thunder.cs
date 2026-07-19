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

public class ClowThunder() : ClowExtraEffectCard(3, CardType.Attack, CardRarity.Rare, TargetType.None)
{
    public override SakuraElementSet Elements => SakuraElementSet.Earth;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain];
    protected override bool IsPlayable => SakuraMagicCharge.CanSpendMagic(Owner);
    protected override IEnumerable<DynamicVar> CanonicalVars => [new SakuraSourceDamageVar(15, ValueProp.Move), new DynamicVar("Magic", 4)];

    protected override Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        Task.CompletedTask;

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await DealDamageToRandomEnemies(choiceContext, ReleasedDamage(), ReleasedMagic());
        await SakuraMagicCharge.AddVoidToDiscardPile(choiceContext, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3);
        DynamicVars["Magic"].UpgradeValueBy(1);
    }
}

public class SakuraThunder() : SakuraFormCard(0, CardType.Attack, TargetType.None)
{
    private const int ResourceDivisor = 2;

    public override SakuraElementSet Elements => SakuraElementSet.Earth;
    protected override bool HasEnergyCostX => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new SakuraSourceDamageVar(15, ValueProp.Move), new DynamicVar("Magic", ResourceDivisor)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var charge = await SakuraMagicCharge.SpendAllMagic(choiceContext, Owner);
        var count = ((int)play.Resources.EnergySpent * ResourceDivisor + charge) / ResourceDivisor;
        await DealDamageToRandomEnemies(choiceContext, ReleasedDamage(), count);
    }
}

