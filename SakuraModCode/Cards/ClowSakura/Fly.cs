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

public class ClowFly() : ClowExtraEffectCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.None)
{
    private const int ExtraDraw = 2;

    public override SakuraElementSet Elements => SakuraElementSet.Wind;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Innate, CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(2), new DynamicVar("Magic", 1)];

    protected override Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        DrawAndGainMagic(choiceContext, 0);

    protected override Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        DrawAndGainMagic(choiceContext, ExtraDraw);

    private async Task DrawAndGainMagic(PlayerChoiceContext choiceContext, int extraDraw)
    {
        await CardPileCmd.Draw(choiceContext, ReleasedValue("Cards") + extraDraw, Owner, false);
        await SakuraMagicCharge.GainMagic(choiceContext, Owner, ReleasedMagic(), this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1);
        DynamicVars["Magic"].UpgradeValueBy(1);
    }
}

public class SakuraFly() : SakuraFormCard(1, CardType.Power, TargetType.None)
{
    public override SakuraElementSet Elements => SakuraElementSet.Wind;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(2)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await ApplyPower<ClassicFlyPower>(choiceContext, Owner.Creature, ReleasedValue("Cards"));
}
