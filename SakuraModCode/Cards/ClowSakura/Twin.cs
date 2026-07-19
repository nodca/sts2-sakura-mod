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

public class ClowTwin() : ClowCard(3, CardType.Power, CardRarity.Rare, TargetType.None)
{
    public override SakuraElementSet Elements => SakuraElementSet.Fire | SakuraElementSet.Water;

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await ApplyPower<ClassicTwinPower>(choiceContext, Owner.Creature, 1);

    protected override void OnUpgrade() => AddKeywordIfMissing(CardKeyword.Retain);
}

public class SakuraTwin() : SakuraFormCard(1, CardType.Power, TargetType.None)
{
    private const int TwinAmount = 1;

    public override SakuraElementSet Elements => SakuraElementSet.Fire | SakuraElementSet.Water;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Amount", TwinAmount)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await ApplyPower<ClassicTwinSakuraPower>(choiceContext, Owner.Creature, DynamicVars["Amount"].IntValue);
}

