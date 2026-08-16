using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal static class CombatTransitionCleanupScenario
{
    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions)
    {
        var context = await CombatScenarioContext.StartAsync(request);
        var firstCombat = await context.EnterWeakCrawlerCombatAsync();
        var player = context.Player;
        var firstPlayerCombat = player.PlayerCombatState
            ?? throw new InvalidOperationException("First player combat state is unavailable.");

        var generatedTemporary = firstCombat.CreateCard<Spiral>(player);
        await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
            generatedTemporary,
            new GeneratedCardOptions
            {
                Pile = PileType.Hand,
                AddTemporary = true
            });
        var captureCandidate = firstCombat.CreateCard<Gale>(player);
        await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
            captureCandidate,
            new GeneratedCardOptions
            {
                Pile = PileType.Hand,
                AddTemporary = true,
                AddManifestAtlasOrigin = true
            });
        var memorySource = firstCombat.CreateCard<Siege>(player);
        await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
            memorySource,
            new GeneratedCardOptions
            {
                Pile = PileType.Discard,
                AddTemporary = true
            });
        var fixtureAction = new RuntimeFixtureAction(
            player,
            async choiceContext =>
            {
                await captureCandidate.Stabilize(choiceContext);
                await TemporaryModifier.RemoveTemporaryFromCombat(choiceContext, memorySource);
                await PowerCmd.Apply<ClassicMagicChargePower>(
                    choiceContext,
                    player.Creature,
                    7,
                    player.Creature,
                    null,
                    silent: true);
                await PowerCmd.Apply<ClassicFireyPower>(
                    choiceContext,
                    player.Creature,
                    1,
                    player.Creature,
                    null,
                    silent: true);
            });
        await CombatScenarioContext.EnqueueAndWaitAsync(fixtureAction);
        assertions.Equal(
            "first_capture_candidate_count",
            1,
            SakuraManifestLoop.CaptureCandidateTypes(firstCombat, player).Count);
        assertions.Equal(
            "first_temporary_memory_count",
            1,
            SakuraMemoryPile.Count(player));
        var firstMemoryPile = SakuraMemoryPile.Get(player)
            ?? throw new InvalidOperationException("First combat Memory pile is unavailable.");
        assertions.Equal("first_magic_charge", 7, player.Creature.GetPower<ClassicMagicChargePower>()?.Amount ?? 0);
        assertions.True("first_firey_present", player.Creature.HasPower<ClassicFireyPower>());
        assertions.Equal("selector_clear_before_transition", null, CardSelectCmd.Selector);
        RuntimeTestHost.WriteCheckpoint(
            request,
            "first_combat_state_seeded",
            "First combat contains generated, weak-table, power, and Temporary-memory state.");

        var secondCombat = await context.EnterWeakCrawlerCombatAsync();
        var secondPlayerCombat = player.PlayerCombatState
            ?? throw new InvalidOperationException("Second player combat state is unavailable.");
        assertions.Equal("combat_state_replaced", false, ReferenceEquals(firstCombat, secondCombat));
        assertions.Equal(
            "player_combat_state_replaced",
            false,
            ReferenceEquals(firstPlayerCombat, secondPlayerCombat));
        assertions.Equal("first_combat_phase_ended", PlayerTurnPhase.None, firstPlayerCombat.Phase);
        assertions.True(
            "old_generated_cards_absent_from_second_combat",
            secondPlayerCombat.AllCards.All(card =>
                !ReferenceEquals(card, generatedTemporary)
                && !ReferenceEquals(card, captureCandidate)
                && !ReferenceEquals(card, memorySource)));
        assertions.Equal("second_magic_charge_clear", 0, player.Creature.GetPower<ClassicMagicChargePower>()?.Amount ?? 0);
        assertions.Equal("second_firey_clear", 0, player.Creature.GetPower<ClassicFireyPower>()?.Amount ?? 0);
        assertions.Equal("second_capture_candidates_clear", 0, SakuraManifestLoop.CaptureCandidateTypes(player).Count);
        assertions.Equal(
            "second_temporary_memory_clear",
            0,
            SakuraMemoryPile.Count(player));
        var emptySecondMemoryPile = SakuraMemoryPile.Get(player)
            ?? throw new InvalidOperationException("Second combat Memory pile is unavailable before cleanup.");
        assertions.Equal(
            "memory_pile_instance_replaced_with_combat",
            false,
            ReferenceEquals(firstMemoryPile, emptySecondMemoryPile));
        assertions.Equal(
            "second_generated_history_clear",
            0,
            CombatManager.Instance.History.Entries.OfType<CardGeneratedEntry>().Count());
        assertions.Equal("selector_clear_after_transition", null, CardSelectCmd.Selector);

        var secondTemporary = secondCombat.CreateCard<Spiral>(player);
        await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
            secondTemporary,
            new GeneratedCardOptions
            {
                Pile = PileType.Hand,
                AddTemporary = true
            });
        await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
        var secondMemory = SakuraMemoryPile.Get(player)
            ?? throw new InvalidOperationException("Second combat Memory pile is unavailable.");
        assertions.Equal("second_cleanup_runs_once", 1, secondMemory.Cards.Count);
        assertions.True("second_cleanup_memory_type", secondMemory.Cards.Count == 1 && secondMemory.Cards[0] is Spiral);
        assertions.Equal(
            "second_memory_stays_isolated_from_first",
            false,
            ReferenceEquals(firstMemoryPile, secondMemory));
        RuntimeTestHost.WriteCheckpoint(
            request,
            "combat_transition_cleanup_verified",
            "Second combat started clean and its Temporary cleanup ran once in its own scope.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["fixture"] = new
            {
                seed = request.Seed.ToString("X16"),
                generated_temporary = generatedTemporary.GetType().FullName,
                capture_candidate = captureCandidate.GetType().FullName,
                memory_source = memorySource.GetType().FullName,
                setup_mutations = new[]
                {
                    "Generated Temporary Spiral -> first hand",
                    "Generated Manifest-origin Temporary Gale -> first hand, then Stabilize",
                    "Generated Temporary Siege -> first discard, then move into the real Memory pile",
                    $"RuntimeFixtureAction -> {nameof(ClassicMagicChargePower)}(7)",
                    $"RuntimeFixtureAction -> {nameof(ClassicFireyPower)}(1)"
                }
            },
            ["transition"] = new
            {
                first_combat_hash = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(firstCombat),
                second_combat_hash = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(secondCombat),
                second_turn = secondPlayerCombat.TurnNumber,
                second_capture_candidates = SakuraManifestLoop.CaptureCandidateTypes(player).Count,
                second_memory_count = secondMemory.Cards.Count,
                memory_instance_replaced = !ReferenceEquals(firstMemoryPile, secondMemory)
            }
        };
    }
}
