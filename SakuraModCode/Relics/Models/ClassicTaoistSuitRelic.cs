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

public class ClassicTaoistSuitRelic : SakuraRelicModel
{
    private const int Trigger = 4;
    private int _cardsPlayed;

    protected override string IconFileName => "taoist_suit.png";
    public override RelicRarity Rarity => RelicRarity.Uncommon;
    public override bool ShowCounter => true;
    public override int DisplayAmount => _cardsPlayed;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Trigger", Trigger),
        new CardsVar(1)
    ];

    public override Task BeforeCombatStart()
    {
        SetCardsPlayed(0);
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (play.Card?.Owner != Owner
            || play.Card is not SakuraSourceCard { IsClassicSourceCard: true })
            return;

        SetCardsPlayed(_cardsPlayed + 1);
        if (_cardsPlayed < DynamicVars["Trigger"].IntValue)
            return;

        SetCardsPlayed(_cardsPlayed - DynamicVars["Trigger"].IntValue);
        Flash();
        var combatState = Owner.Creature.CombatState
            ?? throw new InvalidOperationException("Taoist Suit generated Empty Spell requires an active combat.");
        for (var i = 0; i < DynamicVars.Cards.IntValue; i++)
        {
            var card = combatState.CreateCard<SpellEmptySpell>(Owner);
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner, CardPilePosition.Random);
        }
    }

    private void SetCardsPlayed(int amount)
    {
        _cardsPlayed = amount;
        InvokeDisplayAmountChanged();
    }
}

