using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.TestProtocol;
using System.Reflection;

namespace SakuraMod.RuntimeTests;

internal static class ExchangePileScenario
{
    private const int FixtureMagicCharge = 10;
    private static readonly FieldInfo CombatPileCurrentCountField =
        AccessTools.Field(typeof(NCombatCardPile), "_currentCount")
        ?? throw new MissingFieldException(typeof(NCombatCardPile).FullName, "_currentCount");

    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions)
    {
        var context = await CombatScenarioContext.StartAsync(request);
        var combat = await context.EnterWeakCrawlerCombatAsync();
        var player = context.Player;
        var playerCombat = player.PlayerCombatState
            ?? throw new InvalidOperationException("Player combat state is unavailable.");
        var memory = SakuraMemoryPile.Get(player)
            ?? throw new InvalidOperationException("Memory pile is unavailable.");

        var forgottenInMemory = await CombatScenarioContext.AddGeneratedCardToHandAsync<Gale>(combat, player);
        forgottenInMemory.MakeTemporary(returnsToMemory: false);
        var ordinaryInMemory = await CombatScenarioContext.AddGeneratedCardToHandAsync<Snooze>(combat, player);
        var drawMarker = await CombatScenarioContext.AddGeneratedCardToHandAsync<Transfer>(combat, player);
        var firstHandCard = await CombatScenarioContext.AddGeneratedCardToHandAsync<Gale>(combat, player);
        var secondHandCard = await CombatScenarioContext.AddGeneratedCardToHandAsync<Snooze>(combat, player);
        firstHandCard.MakeTemporary(returnsToMemory: false);

        var setupAction = new RuntimeFixtureAction(
            player,
            async choiceContext =>
            {
                await PowerCmd.Apply<ClassicMagicChargePower>(
                    choiceContext,
                    player.Creature,
                    FixtureMagicCharge,
                    player.Creature,
                    null,
                    silent: true);
                await SakuraActions.MoveExistingCardToPileWithoutVisuals(
                    null,
                    forgottenInMemory,
                    SakuraMemoryPile.PileType,
                    CardPilePosition.Bottom);
                await SakuraActions.MoveExistingCardToPileWithoutVisuals(
                    null,
                    ordinaryInMemory,
                    SakuraMemoryPile.PileType,
                    CardPilePosition.Bottom);
                await SakuraActions.MoveExistingCardToPileWithoutVisuals(
                    null,
                    drawMarker,
                    PileType.Draw,
                    CardPilePosition.Bottom);
            });
        await CombatScenarioContext.EnqueueAndWaitAsync(setupAction);
        await AwaitUiFramesAsync();

        var combatUi = NCombatRoom.Instance?.Ui
            ?? throw new InvalidOperationException("Combat UI is unavailable.");
        assertions.Equal(
            "draw_ui_count_synchronized_before_exchange",
            playerCombat.DrawPile.Cards.Count,
            DisplayedCount(combatUi.DrawPile));

        var memoryBefore = memory.Cards.ToArray();
        var drawBefore = playerCombat.DrawPile.Cards.ToArray();
        var temporaryBefore = memoryBefore
            .Concat(drawBefore)
            .ToDictionary(static card => card, static card => card.IsTemporary());
        var firstCostBefore = firstHandCard.EnergyCost.GetWithModifiers(CostModifiers.Local);
        var secondCostBefore = secondHandCard.EnergyCost.GetWithModifiers(CostModifiers.Local);
        var triggerBefore = SakuraActions.ExtraEffectTriggerCountThisTurn(player);

        var exchange = await CombatScenarioContext.AddGeneratedCardToHandAsync<Exchange>(combat, player);
        var handChoices = playerCombat.Hand.Cards
            .Where(card => card != exchange && SakuraActions.HasExchangeableEnergyCost(card))
            .ToList();
        var firstIndex = handChoices.IndexOf(firstHandCard);
        var secondIndex = handChoices.IndexOf(secondHandCard);
        if (firstIndex < 0 || secondIndex < 0)
            throw new InvalidOperationException("Exchange fixture cards were not available to the hand selector.");

        var selector = new TestCardSelector();
        selector.PrepareToSelect([firstIndex, secondIndex]);
        selector.PrepareToSelect([0, 2]);
        using (CardSelectCmd.UseSelector(selector))
        {
            await CombatScenarioContext.PlayCardAsync(exchange);
        }
        await AwaitUiFramesAsync();

        assertions.True("memory_received_draw_identity", memory.Cards.SequenceEqual(drawBefore));
        assertions.True("draw_received_memory_identity", playerCombat.DrawPile.Cards.SequenceEqual(memoryBefore));
        assertions.Equal("memory_count_swapped", drawBefore.Length, memory.Cards.Count);
        assertions.Equal("draw_count_swapped", memoryBefore.Length, playerCombat.DrawPile.Cards.Count);
        assertions.True("draw_marker_entered_memory", memory.Cards.Contains(drawMarker));
        assertions.True("forgotten_card_left_memory", playerCombat.DrawPile.Cards.Contains(forgottenInMemory));
        assertions.True("forgotten_state_preserved", forgottenInMemory.IsTemporary());
        assertions.Equal("ordinary_memory_state_preserved", false, ordinaryInMemory.IsTemporary());
        assertions.True(
            "all_exchanged_card_states_preserved",
            temporaryBefore.All(pair => pair.Key.IsTemporary() == pair.Value));
        assertions.True(
            "all_exchanged_card_identities_remain_in_combat",
            temporaryBefore.Keys.All(combat.ContainsCard));
        assertions.Equal(
            "base_effect_first_cost_exchanged",
            secondCostBefore,
            firstHandCard.EnergyCost.GetWithModifiers(CostModifiers.Local));
        assertions.Equal(
            "base_effect_second_cost_exchanged",
            firstCostBefore,
            secondHandCard.EnergyCost.GetWithModifiers(CostModifiers.Local));
        assertions.Equal("base_effect_first_forgotten_removed", false, firstHandCard.IsTemporary());
        assertions.True("base_effect_second_forgotten_added", secondHandCard.IsTemporary());
        assertions.Equal(
            "extra_effect_trigger_delta",
            1,
            SakuraActions.ExtraEffectTriggerCountThisTurn(player) - triggerBefore);
        assertions.True("exchange_result_pile", playerCombat.ExhaustPile.Cards.Contains(exchange));
        assertions.Equal("selector_released", null, CardSelectCmd.Selector);
        assertions.Equal(
            "draw_ui_count_synchronized_after_exchange",
            playerCombat.DrawPile.Cards.Count,
            DisplayedCount(combatUi.DrawPile));

        var reversalDrawBefore = playerCombat.DrawPile.Cards.ToArray();
        var reversalDiscardBefore = playerCombat.DiscardPile.Cards.ToArray();
        var reversal = await CombatScenarioContext.AddGeneratedCardToHandAsync<Reversal>(combat, player);
        await CombatScenarioContext.PlayCardAsync(reversal, combat.HittableEnemies.First());
        await AwaitUiFramesAsync();
        assertions.True(
            "reversal_draw_received_discard_identity",
            playerCombat.DrawPile.Cards.SequenceEqual(reversalDiscardBefore));
        assertions.True(
            "reversal_discard_received_draw_identity",
            playerCombat.DiscardPile.Cards
                .Where(card => card != reversal)
                .SequenceEqual(reversalDrawBefore));
        assertions.Equal(
            "draw_ui_count_synchronized_after_reversal",
            reversalDiscardBefore.Length,
            DisplayedCount(combatUi.DrawPile));
        assertions.Equal(
            "discard_ui_received_reversal_swapped_cards",
            reversalDrawBefore.Length,
            DisplayedCount(combatUi.DiscardPile));

        var directSource = await CombatScenarioContext.AddGeneratedCardToHandAsync<Exchange>(combat, player);
        var remainingPairs = new[]
        {
            (ExchangePileKind.Memory, ExchangePileKind.Exhaust),
            (ExchangePileKind.Memory, ExchangePileKind.Discard),
            (ExchangePileKind.Exhaust, ExchangePileKind.Draw),
            (ExchangePileKind.Exhaust, ExchangePileKind.Discard),
            (ExchangePileKind.Draw, ExchangePileKind.Discard)
        };
        foreach (var (firstKind, secondKind) in remainingPairs)
        {
            var firstPile = ResolvePile(firstKind);
            var secondPile = ResolvePile(secondKind);
            var firstBefore = firstPile.Cards.ToArray();
            var secondBefore = secondPile.Cards.ToArray();
            var pairAction = new RuntimeFixtureAction(
                player,
                async _ => await directSource.ExchangePiles(firstKind, secondKind));
            await CombatScenarioContext.EnqueueAndWaitAsync(pairAction);
            assertions.True(
                $"{firstKind}_{secondKind}_first_received_second_in_order",
                firstPile.Cards.SequenceEqual(secondBefore));
            assertions.True(
                $"{firstKind}_{secondKind}_second_received_first_in_order",
                secondPile.Cards.SequenceEqual(firstBefore));
        }

        var emptyMemory = ResolvePile(ExchangePileKind.Memory);
        var emptyExhaust = ResolvePile(ExchangePileKind.Exhaust);
        var emptySetupAction = new RuntimeFixtureAction(
            player,
            async _ =>
            {
                foreach (var card in emptyMemory.Cards.Concat(emptyExhaust.Cards).ToArray())
                {
                    await SakuraActions.MoveExistingCardToPileWithoutVisuals(
                        directSource,
                        card,
                        PileType.Discard,
                        CardPilePosition.Bottom);
                }
            });
        await CombatScenarioContext.EnqueueAndWaitAsync(emptySetupAction);
        var emptyExchangeAction = new RuntimeFixtureAction(
            player,
            async _ => await directSource.ExchangePiles(
                ExchangePileKind.Memory,
                ExchangePileKind.Exhaust));
        await CombatScenarioContext.EnqueueAndWaitAsync(emptyExchangeAction);
        assertions.Equal("both_empty_memory_remains_empty", 0, emptyMemory.Cards.Count);
        assertions.Equal("both_empty_exhaust_remains_empty", 0, emptyExhaust.Cards.Count);

        RuntimeTestHost.WriteCheckpoint(
            request,
            "exchange_four_pile_selection_verified",
            "Exchange resolved its base hand swap, then exchanged Memory and Draw with stable identity, order, and card state.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["fixture"] = new
            {
                seed = request.Seed.ToString("X16"),
                card = typeof(Exchange).FullName,
                selected_piles = new[] { "Memory", "Draw" }
            },
            ["before"] = new
            {
                memory_count = memoryBefore.Length,
                draw_count = drawBefore.Length,
                first_hand_cost = firstCostBefore,
                second_hand_cost = secondCostBefore
            },
            ["after"] = new
            {
                memory_count = memory.Cards.Count,
                draw_count = playerCombat.DrawPile.Cards.Count,
                forgotten_state = forgottenInMemory.IsTemporary(),
                result_pile = exchange.Pile?.Type
            }
        };

        CardPile ResolvePile(ExchangePileKind kind) =>
            kind switch
            {
                ExchangePileKind.Memory => memory,
                ExchangePileKind.Exhaust => playerCombat.ExhaustPile,
                ExchangePileKind.Draw => playerCombat.DrawPile,
                ExchangePileKind.Discard => playerCombat.DiscardPile,
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
            };
    }

    private static int DisplayedCount(NCombatCardPile pile) =>
        CombatPileCurrentCountField.GetValue(pile) is int count
            ? count
            : throw new InvalidOperationException("Combat pile UI count is unavailable.");

    private static async Task AwaitUiFramesAsync()
    {
        var game = NGame.Instance
            ?? throw new InvalidOperationException("NGame is unavailable while awaiting pile UI refresh.");
        await game.AwaitProcessFrame();
        await game.AwaitProcessFrame();
    }
}
