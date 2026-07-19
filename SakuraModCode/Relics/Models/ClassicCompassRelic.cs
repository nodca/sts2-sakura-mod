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

public class ClassicCompassRelic : SakuraRelicModel
{
    private static readonly LocString Prompt = new("cards", "SAKURAMOD-COMPASS.selectionPrompt");
    private const int ChoiceCount = 3;
    private const int VoidCount = 3;
    private bool _usedThisCombat;

    protected override string IconFileName => "compass.png";
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(ChoiceCount),
        new DynamicVar("ChoiceLimit", 1),
        new DynamicVar("CombatCost", 0),
        new DynamicVar("VoidCount", VoidCount)
    ];

    public override Task BeforeCombatStart()
    {
        _usedThisCombat = false;
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (_usedThisCombat || player != Owner)
            return;

        _usedThisCombat = true;
        var combatState = Owner.Creature.CombatState
            ?? throw new InvalidOperationException("Compass card choice requires an active combat.");
        var templates = SakuraSourceCardRules.RewardableClowTemplates().ToList();
        Owner.RunState.Rng.CombatCardSelection.Shuffle(templates);
        var choices = templates
            .Take(DynamicVars.Cards.IntValue)
            .Select(template => combatState.CreateCard(template, Owner))
            .ToList();
        if (choices.Count == 0)
            return;

        CardModel? selected = null;
        try
        {
            selected = choices.Count == 1
                ? choices[0]
                : (await CardSelectCmd.FromSimpleGrid(
                    choiceContext,
                    choices,
                    Owner,
                    new CardSelectorPrefs(Prompt, DynamicVars["ChoiceLimit"].IntValue)
                    {
                        Cancelable = true,
                        RequireManualConfirmation = false
                    })).FirstOrDefault();

            if (selected is null)
                return;

            Flash();
            selected.EnergyCost.SetThisTurnOrUntilPlayed(DynamicVars["CombatCost"].IntValue, true);
            await CardPileCmd.AddGeneratedCardToCombat(selected, PileType.Hand, Owner, CardPilePosition.Random);

            var deckCard = Owner.RunState.CreateCard(ModelDb.GetById<CardModel>(ModelDb.GetId(selected.GetType())), Owner);
            await CardPileCmd.Add(deckCard, PileType.Deck, CardPilePosition.Bottom, this, skipVisuals: true);

            for (var i = 0; i < DynamicVars["VoidCount"].IntValue; i++)
                await SakuraMagicCharge.AddVoidToDiscardPile(choiceContext, Owner);
        }
        finally
        {
            foreach (var choice in choices)
            {
                if (choice != selected && choice.Pile is null)
                    choice.CardScope?.RemoveCard(choice);
            }
        }
    }
}

