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

public class ClowWatery() : ClowCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.None)
{
    public override SakuraElementSet Elements => SakuraElementSet.Water;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Magic", 1)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await ApplyPower<ClassicWateryPower>(choiceContext, Owner.Creature, 1);
        await AddGeneratedSpells<SpellShuiLong>(choiceContext, ReleasedMagic());
    }

    protected override void OnUpgrade() => DynamicVars["Magic"].UpgradeValueBy(1);
}

public class SakuraWatery() : SakuraFormCard(1, CardType.Power, TargetType.None)
{
    public override SakuraElementSet Elements => SakuraElementSet.Water;

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await ApplyPower<ClassicWateryPermanentPower>(choiceContext, Owner.Creature, 1);
        await ApplyPower<ClassicWateryPower>(choiceContext, Owner.Creature, 1);
    }
}

