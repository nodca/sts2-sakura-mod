using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.TestSupport;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Relics;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal static class WandTurnPersistenceScenario
{
    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions)
    {
        var context = await CombatScenarioContext.StartAsync(request);
        await context.EnterWeakCrawlerCombatAsync();
        var player = context.Player;
        var wand = player.Relics.OfType<ClassicSealedWandRelic>().Single();

        assertions.Equal("fixture_wand_charge_before_trigger", 0, wand.ChargeAmount);
        wand.AddReturnRecharge();
        wand.AddReturnRecharge();
        await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);

        var deckTurns = player.Deck.Cards.OfType<SpellTurn>().ToArray();
        var firstCombat = player.PlayerCombatState
            ?? throw new InvalidOperationException("Player combat state is unavailable.");
        var handTurns = firstCombat.Hand.Cards.OfType<SpellTurn>().ToArray();

        assertions.Equal("deck_turn_count_after_trigger", 1, deckTurns.Length);
        assertions.Equal("hand_turn_count_after_trigger", 1, handTurns.Length);
        assertions.Equal("combat_turn_instance_count", 1, firstCombat.AllCards.OfType<SpellTurn>().Count());
        assertions.True(
            "hand_turn_links_to_deck_body",
            ReferenceEquals(handTurns[0].DeckVersion, deckTurns[0]));
        assertions.Equal("deck_body_pile", PileType.Deck, deckTurns[0].Pile?.Type);
        RuntimeTestHost.WriteCheckpoint(
            request,
            "wand_granted_one_linked_turn",
            "One wand trigger produced a single Turn: a deck body plus the hand copy carrying it as DeckVersion.");

        await context.EnterWeakCrawlerCombatAsync();
        var secondCombat = player.PlayerCombatState
            ?? throw new InvalidOperationException("Player combat state is unavailable.");
        var survivingBody = player.Deck.Cards.OfType<SpellTurn>().ToArray();

        assertions.Equal("deck_turn_survives_next_combat", 1, survivingBody.Length);
        assertions.Equal("second_combat_turn_instance_count", 1, secondCombat.AllCards.OfType<SpellTurn>().Count());
        var drawnTurn = secondCombat.AllCards.OfType<SpellTurn>().Single();
        assertions.True(
            "later_combat_turn_links_to_same_body",
            ReferenceEquals(drawnTurn.DeckVersion, survivingBody[0]));
        RuntimeTestHost.WriteCheckpoint(
            request,
            "unplayed_turn_survived_into_next_combat",
            "The unplayed Turn's deck body stayed in the deck and was cloned into the next combat.");

        var selectedClow = secondCombat.AllCards.OfType<ClowSword>().FirstOrDefault()
            ?? throw new InvalidOperationException("Fixture combat did not contain ClowSword.");
        var moveAction = new RuntimeFixtureAction(
            player,
            async _ =>
            {
                if (drawnTurn.Pile?.Type != PileType.Hand)
                {
                    await SakuraActions.MoveExistingCardToHand(null, drawnTurn);
                }

                if (selectedClow.Pile?.Type != PileType.Hand)
                {
                    await SakuraActions.MoveExistingCardToHand(null, selectedClow);
                }
            });
        await CombatScenarioContext.EnqueueAndWaitAsync(moveAction);

        var eligible = secondCombat.Hand.Cards
            .Where(SakuraSourceCardRules.IsEligibleClowForTurn)
            .ToList();
        var selectedIndex = eligible.IndexOf(selectedClow);
        if (selectedIndex < 0)
        {
            throw new InvalidOperationException("Fixture ClowSword was not eligible for Turn.");
        }

        var turnToPlay = secondCombat.Hand.Cards.OfType<SpellTurn>().Single();
        assertions.True("turn_is_playable_in_later_combat", turnToPlay.CanPlay());
        var selector = new TestCardSelector();
        selector.PrepareToSelect([selectedIndex]);
        using (CardSelectCmd.UseSelector(selector))
        {
            await CombatScenarioContext.PlayCardAsync(turnToPlay);
        }

        assertions.Equal("deck_body_removed_after_play", 0, player.Deck.Cards.OfType<SpellTurn>().Count());
        assertions.Equal("combat_turn_removed_after_play", 0, secondCombat.AllCards.OfType<SpellTurn>().Count());
        RuntimeTestHost.WriteCheckpoint(
            request,
            "playing_the_turn_removed_its_deck_body",
            "Playing the hand Turn removed the deck body it was linked to.");

        var moonBell = (ClassicMoonBellRelic)ModelDb.Relic<ClassicMoonBellRelic>().ToMutable();
        await RelicCmd.Obtain(moonBell, player);
        await moonBell.AfterPreventingDeath(player.Creature);

        var bellDeckTurns = player.Deck.Cards.OfType<SpellTurn>().ToArray();
        var bellHandTurns = secondCombat.Hand.Cards.OfType<SpellTurn>().ToArray();

        assertions.Equal("moon_bell_deck_turn_count", 1, bellDeckTurns.Length);
        assertions.Equal("moon_bell_hand_turn_count", 1, bellHandTurns.Length);
        assertions.Equal("moon_bell_combat_turn_instance_count", 1, secondCombat.AllCards.OfType<SpellTurn>().Count());
        assertions.True(
            "moon_bell_turn_links_to_deck_body",
            ReferenceEquals(bellHandTurns[0].DeckVersion, bellDeckTurns[0]));
        RuntimeTestHost.WriteCheckpoint(
            request,
            "moon_bell_granted_one_linked_turn",
            "Moon Bell granted a single Turn with the same deck body and hand copy linkage as the wands.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["fixture"] = new
            {
                seed = request.Seed.ToString("X16"),
                wand = typeof(ClassicSealedWandRelic).FullName,
                moon_bell = typeof(ClassicMoonBellRelic).FullName,
                setup_mutations = new[]
                {
                    "Added return recharge twice so the Sealed Wand crossed its 40 charge threshold on the next turn start",
                    "Entered a second combat so the unplayed Turn had to be cloned from the deck again",
                    "Obtained Moon Bell mid-combat and invoked its death-prevention hook directly"
                }
            },
            ["after"] = new
            {
                deck_turns_after_moon_bell = bellDeckTurns.Length,
                hand_turns_after_moon_bell = bellHandTurns.Length,
                linked = ReferenceEquals(bellHandTurns[0].DeckVersion, bellDeckTurns[0])
            }
        };
    }
}
