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

public class ClowShadow() : ClowExtraEffectCard(1, CardType.Skill, CardRarity.Common, TargetType.None)
{
    private const int ExtraBlur = 2;

    public override bool GainsBlock => true;
    public override SakuraElementSet Elements => SakuraElementSet.Earth;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new SakuraSourceBlockVar(5, ValueProp.Move), new PowerVar<BlurPower>(1)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await GainBlock(play, ReleasedBlock());
        await ApplyPower<BlurPower>(choiceContext, Owner.Creature, ReleasedValue("BlurPower"));
    }

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PlayCard(choiceContext, play);
        await ApplyPower<BlurPower>(choiceContext, Owner.Creature, ExtraBlur);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3);
}

public class SakuraShadow() : SakuraFormCard(1, CardType.Skill, TargetType.None)
{
    public override bool GainsBlock => true;
    public override SakuraElementSet Elements => SakuraElementSet.Earth;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust, CardKeyword.Retain];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new SakuraSourceBlockVar(8, ValueProp.Move), new PowerVar<IntangiblePower>(2)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await GainBlock(play, ReleasedBlock());
        await ApplyPower<IntangiblePower>(choiceContext, Owner.Creature, ReleasedValue("IntangiblePower"));
    }
}
