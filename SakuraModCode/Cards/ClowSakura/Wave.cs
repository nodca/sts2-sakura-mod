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

public class ClowWave() : ClowCard(1, CardType.Power, CardRarity.Uncommon, TargetType.None)
{
    public override SakuraElementSet Elements => SakuraElementSet.Water;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Magic", 2)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await ApplyPower<ClassicWavePower>(choiceContext, Owner.Creature, ReleasedMagic());

    protected override void OnUpgrade() => DynamicVars["Magic"].UpgradeValueBy(1);
}

public class SakuraWave() : SakuraFormCard(1, CardType.Skill, TargetType.None)
{
    public override SakuraElementSet Elements => SakuraElementSet.Water;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await ApplyPower<ClassicEarthyPower>(choiceContext, Owner.Creature, 1);
        await ApplyPower<ClassicFireyPower>(choiceContext, Owner.Creature, 1);
        await ApplyPower<ClassicWateryPower>(choiceContext, Owner.Creature, 1);
        await ApplyPower<ClassicWindyPower>(choiceContext, Owner.Creature, 1);
    }
}

