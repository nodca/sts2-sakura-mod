using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;

namespace SakuraMod.SakuraModCode.Cards;

public class ClowThrough() : ClowCard(2, CardType.Power, CardRarity.Rare, TargetType.None)
{
    public override SakuraElementSet Elements => SakuraElementSet.Earth;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new PowerVar<ClassicThroughPower>(1), new DynamicVar("Rate", 50)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var power = await PowerCmd.Apply<ClassicThroughPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["ClassicThroughPower"].IntValue,
            Owner.Creature,
            this);
        power?.RegisterClowSource(IsUpgraded);
    }

    protected override void OnUpgrade() => DynamicVars["Rate"].UpgradeValueBy(50);
}

public class SakuraThrough() : SakuraFormCard(1, CardType.Power, TargetType.None)
{
    private const int BonusDamage = 10;

    public override SakuraElementSet Elements => SakuraElementSet.Earth;
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new PowerVar<ClassicThroughPower>(1), new DynamicVar("Damage", BonusDamage)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var power = await PowerCmd.Apply<ClassicThroughPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["ClassicThroughPower"].IntValue,
            Owner.Creature,
            this);
        power?.RegisterSakuraSource();
    }
}
