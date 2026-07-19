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

public class AnotherMe() :
    SakuraAncientCard(2, CardType.Power, TargetType.None)
{
    internal const int MagicChargeAmount = 5;

    protected override string AncientPortraitFileName => "another_me.png";
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("Magic", MagicChargeAmount)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await SakuraMagicCharge.GainMagic(choiceContext, Owner, ReleasedMagic(), this);
        await ApplyPower<AnotherMePower>(choiceContext, Owner.Creature, ReleasedMagic());
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
