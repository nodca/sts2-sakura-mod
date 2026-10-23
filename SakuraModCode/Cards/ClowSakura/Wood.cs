using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
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

public class ClowWood() : ClowCard(1, CardType.Power, CardRarity.Uncommon, TargetType.None)
{
    private const int PoisonPerTrigger = 1;

    public override SakuraElementSet Elements => SakuraElementSet.Earth;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<PoisonPower>(PoisonPerTrigger)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await ApplyPower<ClassicWoodPower>(choiceContext, Owner.Creature, DynamicVars["PoisonPower"].IntValue);

    protected override void OnUpgrade() => DynamicVars["PoisonPower"].UpgradeValueBy(1);
}

public class SakuraWood() : SakuraFormCard(1, CardType.Power, TargetType.None)
{
    private const int PoisonPerEarthTrigger = 3;

    public override SakuraElementSet Elements => SakuraElementSet.Earth;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<PoisonPower>(PoisonPerEarthTrigger)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await ApplyPower<ClassicSakuraWoodPower>(choiceContext, Owner.Creature, DynamicVars["PoisonPower"].IntValue);
}
