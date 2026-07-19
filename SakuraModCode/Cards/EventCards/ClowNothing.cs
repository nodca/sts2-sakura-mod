using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;

namespace SakuraMod.SakuraModCode.Cards;

public class ClowNothing() : ClowCard(2, CardType.Power, CardRarity.Event, TargetType.None)
{
    public override SakuraElementSet Elements => SakuraElementSet.None;
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new SakuraSourceDamageVar(4, ValueProp.Unblockable | ValueProp.Unpowered),
        new SakuraSourceBlockVar(3, ValueProp.Move)
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (Owner.Creature.GetPower<ClassicNothingPower>() is null)
            await ApplyPower<ClassicNothingPower>(choiceContext, Owner.Creature, 1);

        Owner.Creature.GetPower<ClassicNothingPower>()?.SetValues(ReleasedDamage(), ReleasedBlock(), IsUpgraded);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Damage"].UpgradeValueBy(2);
        DynamicVars["Block"].UpgradeValueBy(2);
    }
}

