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

public class ClassicUltimateWandRelic : ClassicSealedWandRelic
{
    private const string BattleStartMagicChargeVar = "BattleStartMagicCharge";
    private const string ElementTriggerVar = "ElementTrigger";
    private const string ElementMagicChargeVar = "ElementMagicCharge";
    private const string MarkDamageIncreaseVar = "MarkDamageIncrease";
    private const string HandUpgradesVar = "HandUpgrades";

    private const int BattleStartMagicCharge = 6;
    private const int ElementTrigger = 2;
    private const int ElementMagicCharge = 1;
    private const int MarkAmount = 2;
    private const int MarkDamageIncreasePercent = 50;
    private const int HandUpgrades = 2;

    private int _elementCardsPlayed;

    protected override string IconFileName => "ultimate_wand.png";
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override bool IsAllowedInShops => false;
    protected override int BaseTriggerAmount => 35;
    protected override int BaseChargeGainAmount => 5;
    protected override int EliteBossExtraGainAmount => 5;
    protected override string GeneratedTurnSourceName => "Ultimate Wand";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ..base.CanonicalVars,
        new DynamicVar(BattleStartMagicChargeVar, BattleStartMagicCharge),
        new DynamicVar(ElementTriggerVar, ElementTrigger),
        new DynamicVar(ElementMagicChargeVar, ElementMagicCharge),
        new DynamicVar(MarkDamageIncreaseVar, MarkDamageIncreasePercent),
        new DynamicVar(HandUpgradesVar, HandUpgrades)
    ];

    public override bool IsAllowed(IRunState runState) =>
        false;

    public override async Task BeforeCombatStart()
    {
        await base.BeforeCombatStart();
        await SakuraRelicCombatActions.ApplyMagicChargeIfSealedBook(
            this,
            new ThrowingPlayerChoiceContext(),
            DynamicVars[BattleStartMagicChargeVar].IntValue);
    }

    public override Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        if (player == Owner)
            _elementCardsPlayed = 0;

        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner)
            return;

        SakuraRelicCombatActions.UpgradeRandomHandCards(this, DynamicVars[HandUpgradesVar].IntValue);
        await SakuraRelicCombatActions.MarkRandomEnemy(this, choiceContext, MarkAmount);
        await base.AfterPlayerTurnStart(choiceContext, player);
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (play.Card?.Owner != Owner
            || play.Card is not SakuraSourceCard card
            || card.Elements == SakuraElementSet.None)
            return;

        _elementCardsPlayed++;
        if (_elementCardsPlayed < DynamicVars[ElementTriggerVar].IntValue)
            return;

        _elementCardsPlayed -= DynamicVars[ElementTriggerVar].IntValue;
        await SakuraRelicCombatActions.ApplyMagicChargeIfSealedBook(
            this,
            choiceContext,
            DynamicVars[ElementMagicChargeVar].IntValue);
    }
}

