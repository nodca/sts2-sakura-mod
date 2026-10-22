using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
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
        var triggerBefore = SakuraActions.ExtraEffectTriggerCountThisTurn(player);

        var exchange = await CombatScenarioContext.AddGeneratedCardToHandAsync<Exchange>(combat, player);
        var handChoices = playerCombat.Hand.Cards
            .Where(card => card != exchange && !card.IsTemporary())
            .ToList();
        var forgetIndex = handChoices.IndexOf(secondHandCard);
        if (forgetIndex < 0 || handChoices.Contains(firstHandCard))
            throw new InvalidOperationException("Exchange fixture cards were not filtered correctly for the hand selector.");

        var selector = new TestCardSelector();
        selector.PrepareToSelect([forgetIndex]);
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
        assertions.True("base_effect_already_forgotten_card_unchanged", firstHandCard.IsTemporary());
        assertions.True("base_effect_selected_card_forgotten", secondHandCard.IsTemporary());
        assertions.Equal(
            "base_effect_next_turn_remind_scheduled",
            1,
            player.Creature.GetPower<ExchangeRecallPower>()?.Amount ?? 0);
        assertions.Equal(
            "extra_effect_trigger_delta",
            1,
            SakuraActions.ExtraEffectTriggerCountThisTurn(player) - triggerBefore);
        assertions.True("exchange_result_pile", playerCombat.DiscardPile.Cards.Contains(exchange));
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
                async choice => await directSource.ExchangePiles(choice, firstKind, secondKind));
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
            async choice => await directSource.ExchangePiles(
                choice,
                ExchangePileKind.Memory,
                ExchangePileKind.Exhaust));
        await CombatScenarioContext.EnqueueAndWaitAsync(emptyExchangeAction);
        assertions.Equal("both_empty_memory_remains_empty", 0, emptyMemory.Cards.Count);
        assertions.Equal("both_empty_exhaust_remains_empty", 0, emptyExhaust.Cards.Count);

        await VerifyLoopCyclesAsync(context, combat, assertions);

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
                draw_count = drawBefore.Length
            },
            ["after"] = new
            {
                memory_count = memory.Cards.Count,
                draw_count = playerCombat.DrawPile.Cards.Count,
                forgotten_state = forgottenInMemory.IsTemporary(),
                selected_hand_card_forgotten = secondHandCard.IsTemporary(),
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

    private static async Task VerifyLoopCyclesAsync(
        CombatScenarioContext context,
        CombatState combat,
        RuntimeAssertionCollector assertions)
    {
        var player = context.Player;
        var piles = player.PlayerCombatState!;
        var memory = SakuraMemoryPile.Get(player)!;
        await ResetPilesAsync(8, 0, 0, 0);

        var clow = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowLoop>(combat, player);
        clow.UpgradeInternal();
        await CombatScenarioContext.PlayCardAsync(clow);
        assertions.Equal("clow_loop_applied_one_draw", 1, player.Creature.GetPower<ClassicLoopPower>()?.Amount ?? 0);
        await ActAsync(async choice => { await CardPileCmd.Draw(choice, 1, player); });
        assertions.Equal("ordinary_draw_does_not_trigger_loop", 1, piles.Hand.Cards.Count);
        await ActAsync(async choice => await CardCmd.Discard(choice, piles.Hand.Cards.ToArray()));
        assertions.Equal("ordinary_discard_does_not_trigger_clow_loop", 0, piles.Hand.Cards.Count);
        await ActAsync(async choice => await CardPileCmd.Shuffle(choice, player));
        assertions.Equal("shuffle_triggers_clow_once", 1, piles.Hand.Cards.Count);

        var sakura = await CombatScenarioContext.AddGeneratedCardToHandAsync<SakuraLoop>(combat, player);
        await CombatScenarioContext.PlayCardAsync(sakura);
        assertions.Equal("sakura_loop_applied_two_draws", 2, player.Creature.GetPower<ClassicLoopSakuraPower>()?.Amount ?? 0);
        assertions.Equal("clow_loop_stays_independent", 1, player.Creature.GetPower<ClassicLoopPower>()?.Amount ?? 0);
        var beforeDiscard = piles.Hand.Cards.Count;
        await ActAsync(async choice => await CardCmd.Discard(choice, piles.Hand.Cards.Take(1).ToArray()));
        assertions.Equal("discard_no_longer_triggers_sakura_loop", beforeDiscard - 1, piles.Hand.Cards.Count);

        await ResetPilesAsync(0, 8, 0, 0);
        await ActAsync(async choice => { await CardPileCmd.Draw(choice, 1, player); });
        assertions.Equal("automatic_shuffle_adds_both_loop_rewards_to_requested_draw", 4, piles.Hand.Cards.Count);

        await ResetPilesAsync(8, 0, 0, 0);
        var exchange = await CombatScenarioContext.AddGeneratedCardToHandAsync<Exchange>(combat, player);
        await ActAsync(async choice => await exchange.ExchangePiles(choice, ExchangePileKind.Memory, ExchangePileKind.Exhaust));
        assertions.Equal("empty_swap_does_not_trigger_loop", 1, piles.Hand.Cards.Count);

        await ActAsync(async _ =>
        {
            await CardPileCmd.AddGeneratedCardToCombat(combat.CreateCard<ClowSword>(player), memory.Type, player);
            await CardPileCmd.AddGeneratedCardToCombat(combat.CreateCard<ClowShield>(player), PileType.Exhaust, player);
        });
        var memoryBefore = memory.Cards.ToArray();
        var exhaustBefore = piles.ExhaustPile.Cards.ToArray();
        await ActAsync(async choice => await exchange.ExchangePiles(choice, ExchangePileKind.Memory, ExchangePileKind.Exhaust));
        assertions.Equal("first_counted_swap_only_advances_loop", 1, piles.Hand.Cards.Count);
        assertions.True("loop_reward_waits_for_complete_memory_swap", memory.Cards.SequenceEqual(exhaustBefore));
        assertions.True("loop_reward_waits_for_complete_exhaust_swap", piles.ExhaustPile.Cards.SequenceEqual(memoryBefore));

        await ResetPilesAsync(6, 6, 0, 0);
        var reversal = await CombatScenarioContext.AddGeneratedCardToHandAsync<Reversal>(combat, player);
        var swappedTopCards = piles.DiscardPile.Cards.Take(3).ToArray();
        await CombatScenarioContext.PlayCardAsync(reversal, combat.HittableEnemies.First());
        assertions.Equal("second_counted_swap_triggers_each_loop_once", 3, piles.Hand.Cards.Count);
        assertions.True("reversal_loop_draws_from_completed_swap", piles.Hand.Cards.SequenceEqual(swappedTopCards));

        await ResetPilesAsync(1, 5, 1, 1);
        exchange = await CombatScenarioContext.AddGeneratedCardToHandAsync<Exchange>(combat, player);
        await ActAsync(async choice => await exchange.ExchangePiles(choice, ExchangePileKind.Memory, ExchangePileKind.Exhaust));
        assertions.Equal("odd_counted_swap_draws_nothing", 1, piles.Hand.Cards.Count);
        await ActAsync(async choice => await exchange.ExchangePiles(choice, ExchangePileKind.Memory, ExchangePileKind.Exhaust));
        assertions.Equal("swap_reward_can_trigger_one_additional_shuffle_reward", 7, piles.Hand.Cards.Count);
        assertions.Equal("small_pile_cycle_finishes_without_phantom_cards", 0, piles.DrawPile.Cards.Count + piles.DiscardPile.Cards.Count);

        await ResetPilesAsync(12, 1, 0, 0);
        await ActAsync(async choice => { await CardPileCmd.Draw(choice, CardPile.MaxCardsInHand, player); });
        var fullHand = piles.Hand.Cards.ToArray();
        await ActAsync(async choice => await CardPileCmd.Shuffle(choice, player));
        assertions.True("loop_rewards_respect_full_hand", piles.Hand.Cards.SequenceEqual(fullHand));

        Task ActAsync(Func<PlayerChoiceContext, Task> execute) =>
            CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, execute));

        async Task ResetPilesAsync(int draw, int discard, int remembered, int exhausted)
        {
            await ActAsync(async choice =>
            {
                foreach (var card in piles.Hand.Cards.Concat(piles.DrawPile.Cards)
                    .Concat(piles.DiscardPile.Cards).Concat(piles.ExhaustPile.Cards).Concat(memory.Cards).ToArray())
                    await CardPileCmd.RemoveFromCombat(card);
                if (player.Creature.GetPower<ClassicMagicChargePower>() is { } charge)
                    await PowerCmd.Remove(charge);
                if (player.Creature.GetPower<ExchangeRecallPower>() is { } recall)
                    await PowerCmd.Remove(recall);

                foreach (var (pile, count) in new[]
                {
                    (PileType.Draw, draw), (PileType.Discard, discard),
                    (memory.Type, remembered), (PileType.Exhaust, exhausted)
                })
                    for (var index = 0; index < count; index++)
                        await CardPileCmd.AddGeneratedCardToCombat(combat.CreateCard<ClowSword>(player), pile, player);
            });
        }
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
