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

public class ClowMaze() : ClowExtraEffectCard(2, CardType.Skill, CardRarity.Common, TargetType.None)
{
    private const int ExtraBlock = 14;

    public override bool GainsBlock => true;
    public override SakuraElementSet Elements => SakuraElementSet.Earth;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new SakuraSourceBlockVar(17, ValueProp.Move)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await GainBlock(play, ReleasedBlock());

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PlayCard(choiceContext, play);
        await GainBlock(play, ExtraBlock);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(6);
}

public class SakuraMaze() : SakuraFormCard(1, CardType.Skill, TargetType.None)
{
    public override bool GainsBlock => true;
    public override SakuraElementSet Elements => SakuraElementSet.Earth;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new SakuraSourceBlockVar(8, ValueProp.Move), new DynamicVar("Magic", 3)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        for (var i = 0; i < ReleasedMagic(); i++)
            await GainBlock(play, ReleasedBlock());
    }
}
