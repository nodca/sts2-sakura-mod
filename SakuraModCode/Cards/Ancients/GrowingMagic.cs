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

public class GrowingMagic() :
    SakuraAncientCard(1, CardType.Attack, TargetType.AnyEnemy)
{
    internal const int MagicChargeOnKill = 5;

    protected override string AncientPortraitFileName => "growing_magic.png";
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new SakuraSourceDamageVar(18, ValueProp.Move),
        new DynamicVar("Magic", MagicChargeOnKill)
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        SakuraVoicePlayback.TryPlay(this);
        await DealDamage(choiceContext, RequiredTarget(play), ReleasedDamage());
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(6);
}

