using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.TestProtocol;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace SakuraMod.RuntimeTests;

internal static class DarkSelectionCombatReentryScenario
{
    private static readonly TimeSpan ReentryTimeout = TimeSpan.FromSeconds(5);

    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions)
    {
        var context = await CombatScenarioContext.StartAsync(request);
        var firstCombat = await context.EnterWeakCrawlerCombatAsync();
        var player = context.Player;
        var firstPlayerCombat = player.PlayerCombatState
            ?? throw new InvalidOperationException("First player combat state is unavailable.");

        var setup = new RuntimeFixtureAction(
            player,
            choiceContext => PowerCmd.Apply<ClassicDarkPower>(
                choiceContext,
                player.Creature,
                1,
                player.Creature,
                null,
                silent: true));
        await CombatScenarioContext.EnqueueAndWaitAsync(setup);

        var eligibleHandCount = CardPile.GetCards(player, PileType.Hand).Count();
        assertions.True(
            "dark_selection_has_eligible_hand_card",
            eligibleHandCount > 0,
            "Dark needs a live hand card in order to open its end-of-turn selector.");
        assertions.Equal("dark_selector_override_absent", null, CardSelectCmd.Selector);

        var endTurnAction = new EndPlayerTurnAction(player, firstPlayerCombat.TurnNumber);
        RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(endTurnAction);
        await CombatScenarioContext.WaitUntilAsync(
            () => NPlayerHand.Instance is
            {
                IsInCardSelection: true,
                CurrentMode: NPlayerHand.Mode.SimpleSelect
            },
            "Dark end-of-turn hand selection");

        var hand = NPlayerHand.Instance
            ?? throw new InvalidOperationException("NPlayerHand disappeared before combat teardown.");
        var preTeardownActionState = endTurnAction.State;
        var preTeardownActionCompleted = endTurnAction.CompletionTask.IsCompleted;
        assertions.True("dark_native_hand_selection_open", hand.IsInCardSelection);
        assertions.Equal(
            "dark_native_hand_selection_mode",
            NPlayerHand.Mode.SimpleSelect,
            hand.CurrentMode);
        RuntimeTestHost.WriteCheckpoint(
            request,
            "dark_selection_pending",
            $"Dark's native SimpleSelect remains unresolved before teardown; " +
            $"end-turn action state={preTeardownActionState}, completed={preTeardownActionCompleted}.");

        var transitionStopwatch = Stopwatch.StartNew();
        RuntimeTestHost.WriteCheckpoint(
            request,
            "combat_reentry_requested",
            "Requested a second native combat while Dark's first-combat selection remains unresolved.");
        var transitionTask = RunManager.Instance.EnterRoomDebug(
            RoomType.Monster,
            model: ModelDb.Encounter<SlimesWeak>().ToMutable(),
            showTransition: false);
        await transitionTask.WaitAsync(ReentryTimeout);

        var secondCombat = await WaitForSecondCombatReadyAsync(firstCombat, player, transitionStopwatch);
        transitionStopwatch.Stop();
        var secondPlayerCombat = player.PlayerCombatState
            ?? throw new InvalidOperationException("Second player combat state is unavailable.");
        var openingHandCount = CardPile.GetCards(player, PileType.Hand).Count();

        assertions.Equal("combat_state_replaced", false, ReferenceEquals(firstCombat, secondCombat));
        assertions.Equal(
            "player_combat_state_replaced",
            false,
            ReferenceEquals(firstPlayerCombat, secondPlayerCombat));
        assertions.Equal("first_combat_phase_ended", PlayerTurnPhase.None, firstPlayerCombat.Phase);
        assertions.Equal("second_combat_play_phase", PlayerTurnPhase.Play, secondPlayerCombat.Phase);
        assertions.True(
            "second_combat_opening_draw_completed",
            openingHandCount > 0,
            "The replacement combat reached Play without a non-empty opening hand.");
        assertions.True(
            "second_combat_ready_before_stale_loop_fallback",
            transitionStopwatch.Elapsed < ReentryTimeout,
            $"Replacement combat took {transitionStopwatch.Elapsed.TotalMilliseconds:F0} ms.");
        RuntimeTestHost.WriteCheckpoint(
            request,
            "second_combat_ready",
            "The replacement combat reached Play phase with a non-empty opening hand before the stale-loop fallback.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["fixture"] = new
            {
                seed = request.Seed.ToString("X16"),
                dark_amount = 1,
                eligible_hand_count = eligibleHandCount,
                selector_override = CardSelectCmd.Selector?.GetType().FullName,
                setup_mutation = $"RuntimeFixtureAction -> PowerCmd.Apply<{nameof(ClassicDarkPower)}>(1)"
            },
            ["transition"] = new
            {
                timeout_ms = (long)ReentryTimeout.TotalMilliseconds,
                elapsed_ms = transitionStopwatch.ElapsedMilliseconds,
                first_combat_hash = RuntimeHelpers.GetHashCode(firstCombat),
                second_combat_hash = RuntimeHelpers.GetHashCode(secondCombat),
                first_player_combat_hash = RuntimeHelpers.GetHashCode(firstPlayerCombat),
                second_player_combat_hash = RuntimeHelpers.GetHashCode(secondPlayerCombat),
                first_phase = firstPlayerCombat.Phase,
                second_phase = secondPlayerCombat.Phase,
                opening_hand_count = openingHandCount,
                pre_teardown_end_turn_state = preTeardownActionState,
                pre_teardown_end_turn_completed = preTeardownActionCompleted,
                old_end_turn_state = endTurnAction.State,
                old_end_turn_completed = endTurnAction.CompletionTask.IsCompleted,
                old_end_turn_exception = endTurnAction.Exception?.GetBaseException().Message
            }
        };
    }

    private static async Task<CombatState> WaitForSecondCombatReadyAsync(
        CombatState firstCombat,
        MegaCrit.Sts2.Core.Entities.Players.Player player,
        Stopwatch transitionStopwatch)
    {
        var game = MegaCrit.Sts2.Core.Nodes.NGame.Instance
            ?? throw new InvalidOperationException("NGame became unavailable during combat re-entry.");
        while (transitionStopwatch.Elapsed < ReentryTimeout)
        {
            var combat = CombatManager.Instance.DebugOnlyGetState();
            if (combat is not null
                && !ReferenceEquals(combat, firstCombat)
                && player.PlayerCombatState?.Phase == PlayerTurnPhase.Play
                && CardPile.GetCards(player, PileType.Hand).Any())
            {
                return combat;
            }

            await game.AwaitProcessFrame();
        }

        throw new TimeoutException(
            $"Replacement combat did not reach Play with an opening hand within {ReentryTimeout.TotalSeconds:F0} seconds.");
    }
}
