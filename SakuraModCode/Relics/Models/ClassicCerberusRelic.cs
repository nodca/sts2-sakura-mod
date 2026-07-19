using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.Extensions;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;
using CoreVoid = MegaCrit.Sts2.Core.Models.Cards.Void;

namespace SakuraMod.SakuraModCode.Relics;

public class ClassicCerberusRelic : SakuraRelicModel
{
    private const int BattleStartMagicCharge = 6;
    private const int MarkAmount = 1;

    protected override string IconFileName => "cerberus.png";
    public override RelicRarity Rarity => RelicRarity.Rare;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("BattleStartMagicCharge", BattleStartMagicCharge),
        new DynamicVar("MarkDamageIncrease", 25)
    ];

    public override async Task AfterObtained() =>
        await SakuraUltimateWandRecipe.TryCreateUltimateWand(Owner);

    public override async Task BeforeCombatStart() =>
        await SakuraRelicCombatActions.ApplyMagicChargeIfSealedBook(
            this,
            new ThrowingPlayerChoiceContext(),
            DynamicVars["BattleStartMagicCharge"].IntValue);

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner)
            return;

        await SakuraRelicCombatActions.MarkRandomEnemy(this, choiceContext, MarkAmount);
    }
}

