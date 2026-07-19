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

public class ClassicYueRelic : SakuraRelicModel
{
    private const int Trigger = 3;
    private const int MagicCharge = 1;
    private const int Upgrades = 1;
    private int _elementCardsPlayed;

    protected override string IconFileName => "yue.png";
    public override RelicRarity Rarity => RelicRarity.Rare;
    public override bool ShowCounter => true;
    public override int DisplayAmount => _elementCardsPlayed;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Trigger", Trigger),
        new DynamicVar("MagicCharge", MagicCharge),
        new CardsVar(Upgrades)
    ];

    public override async Task AfterObtained() =>
        await SakuraUltimateWandRecipe.TryCreateUltimateWand(Owner);

    public override Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        if (player == Owner)
            SetElementCardsPlayed(0);

        return Task.CompletedTask;
    }

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner)
            SakuraRelicCombatActions.UpgradeRandomHandCards(this, DynamicVars.Cards.IntValue);

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (play.Card?.Owner != Owner
            || play.Card is not SakuraSourceCard card
            || card.Elements == SakuraElementSet.None)
            return;

        SetElementCardsPlayed(_elementCardsPlayed + 1);
        if (_elementCardsPlayed < DynamicVars["Trigger"].IntValue)
            return;

        SetElementCardsPlayed(_elementCardsPlayed - DynamicVars["Trigger"].IntValue);
        await SakuraRelicCombatActions.ApplyMagicChargeIfSealedBook(this, choiceContext, DynamicVars["MagicCharge"].IntValue);
    }

    private void SetElementCardsPlayed(int amount)
    {
        _elementCardsPlayed = amount;
        InvokeDisplayAmountChanged();
    }
}

