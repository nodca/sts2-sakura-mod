using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.TestSupport;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.TestProtocol;
using SharpEnchantment = MegaCrit.Sts2.Core.Models.Enchantments.Sharp;

namespace SakuraMod.RuntimeTests;

internal static class GeneratedPileMemoryScenario
{
    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions)
    {
        var context = await CombatScenarioContext.StartAsync(request);
        var combat = await context.EnterWeakCrawlerCombatAsync();
        var player = context.Player;
        var playerCombat = player.PlayerCombatState
            ?? throw new InvalidOperationException("Player combat state is unavailable.");
        var transitions = new List<Dictionary<string, object?>>();

        var temporary = combat.CreateCard<Spiral>(player);
        temporary.UpgradeInternal();
        CardCmd.Enchant(ModelDb.Enchantment<SharpEnchantment>().ToMutable(), temporary, 2m);
        temporary.EnergyCost.SetThisTurnOrUntilPlayed(0, reduceOnly: true);
        await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
            temporary,
            new GeneratedCardOptions
            {
                Pile = PileType.Draw,
                Position = CardPilePosition.Bottom,
                AddTemporary = true
            });
        Record("temporary_generated_to_draw", temporary);

        var temporaryTime = combat.CreateCard<Time>(player);
        temporaryTime.EnergyCost.SetThisCombat(1, reduceOnly: true);
        await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
            temporaryTime,
            new GeneratedCardOptions
            {
                Pile = PileType.Draw,
                Position = CardPilePosition.Bottom,
                AddTemporary = true
            });
        Record("temporary_time_generated_to_draw", temporaryTime);

        var ordinary = combat.CreateCard<Gale>(player);
        await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
            ordinary,
            PileType.Hand,
            player,
            CardPilePosition.Random);
        Record("ordinary_generated_to_hand", ordinary);

        var temporaryExhausted = combat.CreateCard<Gale>(player);
        await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
            temporaryExhausted,
            new GeneratedCardOptions
            {
                Pile = PileType.Hand,
                Position = CardPilePosition.Random,
                AddTemporary = true
            });
        Record("temporary_generated_to_hand_for_exhaust", temporaryExhausted);

        var movementAction = new RuntimeFixtureAction(
            player,
            async choiceContext =>
            {
                await SakuraActions.MoveExistingCardToHand(null, temporary);
                Record("temporary_draw_to_hand", temporary);
                await SakuraActions.MoveExistingCardToPileWithoutVisuals(
                    null,
                    temporary,
                    PileType.Discard,
                    CardPilePosition.Bottom);
                Record("temporary_hand_to_discard", temporary);

                await SakuraActions.MoveExistingCardToPileWithoutVisuals(
                    null,
                    ordinary,
                    PileType.Draw,
                    CardPilePosition.Bottom);
                Record("ordinary_hand_to_draw", ordinary);
                await SakuraActions.MoveExistingCardToPileWithoutVisuals(
                    null,
                    ordinary,
                    PileType.Discard,
                    CardPilePosition.Bottom);
                Record("ordinary_draw_to_discard", ordinary);
                await CardCmd.Exhaust(choiceContext, ordinary, skipVisuals: true);
                Record("ordinary_discard_to_exhaust", ordinary);
                await CardCmd.Exhaust(choiceContext, temporaryExhausted, skipVisuals: true);
                Record("temporary_hand_to_exhaust", temporaryExhausted);
            });
        await CombatScenarioContext.EnqueueAndWaitAsync(movementAction);

        assertions.True(
            "movement_choice_context",
            movementAction.ChoiceContext is GameActionPlayerChoiceContext);
        assertions.True("temporary_in_discard_before_turn_end", playerCombat.DiscardPile.Cards.Contains(temporary));
        assertions.True("temporary_state_before_turn_end", temporary.IsTemporary());
        assertions.Equal(
            "temporary_turn_cost_before_turn_end",
            0,
            temporary.EnergyCost.GetWithModifiers(CostModifiers.Local));
        assertions.Equal(
            "temporary_combat_cost_before_turn_end",
            1,
            temporaryTime.EnergyCost.GetWithModifiers(CostModifiers.Local));
        assertions.True("ordinary_in_exhaust_before_turn_end", playerCombat.ExhaustPile.Cards.Contains(ordinary));
        assertions.True("temporary_in_exhaust_before_turn_end", playerCombat.ExhaustPile.Cards.Contains(temporaryExhausted));
        assertions.True("exhausted_temporary_state_before_turn_end", temporaryExhausted.IsTemporary());
        assertions.True("ordinary_owner_before_turn_end", ReferenceEquals(ordinary.Owner, player));
        assertions.Equal(
            "temporary_memory_empty_before_turn_end",
            0,
            SakuraMemoryPile.Count(player));

        var temporaryPower = await CombatScenarioContext.AddGeneratedCardToHandAsync<AnotherMe>(combat, player);
        temporaryPower.MakeTemporary();
        await CombatScenarioContext.PlayCardAsync(temporaryPower);
        assertions.True("played_temporary_power_removed_immediately", temporaryPower.HasBeenRemovedFromState);
        assertions.True("temporary_power_applied_before_turn_end", player.Creature.HasPower<AnotherMePower>());

        var selfContinuingSpiral = await CombatScenarioContext.AddGeneratedCardToHandAsync<Spiral>(combat, player);
        selfContinuingSpiral.UpgradeInternal();
        CardCmd.Enchant(ModelDb.Enchantment<SharpEnchantment>().ToMutable(), selfContinuingSpiral, 2m);
        await CombatScenarioContext.PlayCardAsync(selfContinuingSpiral, combat.HittableEnemies.First());

        var dreamingSetupAction = new RuntimeFixtureAction(
            player,
            choiceContext => PowerCmd.Apply<DreamingPower>(
                choiceContext,
                player.Creature,
                1,
                player.Creature,
                null,
                silent: true));
        await CombatScenarioContext.EnqueueAndWaitAsync(dreamingSetupAction);
        var dreamingSelector = new TestCardSelector();
        dreamingSelector.PrepareToSelect([0]);
        using (CardSelectCmd.UseSelector(dreamingSelector))
        {
            await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
        }
        assertions.Equal("dreaming_selector_released", null, CardSelectCmd.Selector);
        var memory = SakuraMemoryPile.Get(player)
            ?? throw new InvalidOperationException("Memory pile is unavailable after Temporary cleanup.");
        assertions.True("temporary_moved_into_memory", ReferenceEquals(temporary.Pile, memory));
        assertions.Equal("temporary_not_removed_from_state", false, temporary.HasBeenRemovedFromState);
        assertions.True("temporary_identity_retained_by_combat_scope", combat.ContainsCard(temporary));
        assertions.Equal("temporary_memory_count", 2, memory.Cards.Count);
        assertions.Equal("played_temporary_power_not_in_memory", false, memory.Cards.Contains(temporaryPower));
        assertions.True("played_temporary_power_effect_retained", player.Creature.HasPower<AnotherMePower>());
        assertions.True(
            "temporary_memory_types",
            memory.Cards.Any(card => card is Spiral)
            && memory.Cards.Any(card => card is Time));
        assertions.True("temporary_memory_owner", memory.Cards.All(card => ReferenceEquals(card.Owner, player)));
        assertions.Equal("temporary_memory_is_stable", false, memory.Cards.Any(card => card.IsTemporary()));
        assertions.Equal("temporary_memory_upgrade_retained", 1, temporary.CurrentUpgradeLevel);
        assertions.True(
            "temporary_memory_enchantment_retained",
            temporary.Enchantment is SharpEnchantment { Amount: 2 });
        assertions.Equal(
            "temporary_turn_cost_expired_in_memory",
            1,
            temporary.EnergyCost.GetWithModifiers(CostModifiers.Local));
        assertions.Equal(
            "temporary_combat_cost_retained_in_memory",
            1,
            temporaryTime.EnergyCost.GetWithModifiers(CostModifiers.Local));
        assertions.True("ordinary_remains_in_exhaust", playerCombat.ExhaustPile.Cards.Contains(ordinary));
        assertions.True("temporary_remains_in_exhaust", playerCombat.ExhaustPile.Cards.Contains(temporaryExhausted));
        assertions.True("exhausted_temporary_state_retained", temporaryExhausted.IsTemporary());
        assertions.True("exhausted_temporary_remains_in_combat_scope", combat.ContainsCard(temporaryExhausted));
        assertions.True("ordinary_remains_in_combat_scope", combat.ContainsCard(ordinary));
        assertions.True("ordinary_owner_after_turn_end", ReferenceEquals(ordinary.Owner, player));
        assertions.Equal(
            "upgraded_spiral_uses_a_normal_draw_slot",
            CombatManager.baseHandDrawCount,
            playerCombat.Hand.Cards.Count);
        var continuedSpiral = playerCombat.Hand.Cards
            .OfType<Spiral>()
            .Single(card => card.IsTemporary());
        assertions.True(
            "upgraded_spiral_self_continues_as_fresh_base_card",
            continuedSpiral.CurrentUpgradeLevel == 0
            && continuedSpiral.Enchantment is null
            && continuedSpiral.EnergyCost.GetWithModifiers(CostModifiers.Local) == 0);

        var remind = await CombatScenarioContext.AddGeneratedCardToHandAsync<Remind>(combat, player);
        remind.UpgradeInternal();
        await CombatScenarioContext.PlayCardAsync(remind);
        var recalled = playerCombat.Hand.Cards
            .Where(card => card.IsTemporary() && !card.ReturnsToMemoryAfterTemporary())
            .ToList();
        assertions.Equal("temporary_memory_consumed", 0, SakuraMemoryPile.Count(player));
        assertions.True("remind_enters_discard", playerCombat.DiscardPile.Cards.Contains(remind));
        assertions.Equal("remind_two_recalls_in_one_choice", 2, recalled.Count);
        assertions.True("recalled_copy_types", recalled.Any(card => card is Spiral) && recalled.Any(card => card is Time));
        assertions.True(
            "recalled_copy_inherits_enchantment",
            recalled.OfType<Spiral>().Single().Enchantment is SharpEnchantment { Amount: 2 });
        assertions.True("recalled_copies_are_temporary", recalled.All(card => card.IsTemporary()));
        assertions.Equal("recalled_copies_return_to_memory", false, recalled.Any(card => card.ReturnsToMemoryAfterTemporary()));
        assertions.True(
            "recalled_copy_is_free_this_turn",
            recalled.All(card => card.EnergyCost.GetWithModifiers(CostModifiers.Local) == 0));

        var recalledCleanupAction = new RuntimeFixtureAction(
            player,
            async choiceContext =>
            {
                foreach (var card in recalled)
                    await TemporaryModifier.RemoveTemporaryFromCombat(choiceContext, card);
                continuedSpiral.MakeTemporary(returnsToMemory: false);
                await TemporaryModifier.RemoveTemporaryFromCombat(choiceContext, continuedSpiral);
            });
        await CombatScenarioContext.EnqueueAndWaitAsync(recalledCleanupAction);
        assertions.True(
            "recalled_cleanup_choice_context",
            recalledCleanupAction.ChoiceContext is GameActionPlayerChoiceContext);
        assertions.True("recalled_copies_removed_from_state", recalled.All(card => card.HasBeenRemovedFromState));
        assertions.True("recalled_copies_have_no_pile", recalled.All(card => card.Pile is null));
        assertions.Equal("recalled_copy_does_not_reenter_memory", 0, SakuraMemoryPile.Count(player));

        var firstMemorySeed = combat.CreateCard<Gale>(player);
        var secondMemorySeed = combat.CreateCard<Siege>(player);
        await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
            firstMemorySeed,
            new GeneratedCardOptions { Pile = PileType.Discard, AddTemporary = true });
        await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
            secondMemorySeed,
            new GeneratedCardOptions { Pile = PileType.Discard, AddTemporary = true });
        var spiralWithExtra = await CombatScenarioContext.AddGeneratedCardToHandAsync<Spiral>(combat, player);
        spiralWithExtra.UpgradeInternal();
        var extraSetupAction = new RuntimeFixtureAction(
            player,
            async choiceContext =>
            {
                await TemporaryModifier.RemoveTemporaryFromCombat(choiceContext, firstMemorySeed);
                await TemporaryModifier.RemoveTemporaryFromCombat(choiceContext, secondMemorySeed);
                await PowerCmd.Apply<ClassicMagicChargePower>(
                    choiceContext,
                    player.Creature,
                    10,
                    player.Creature,
                    null,
                    silent: true);
            });
        await CombatScenarioContext.EnqueueAndWaitAsync(extraSetupAction);
        assertions.Equal("spiral_extra_memory_count", 2, SakuraMemoryPile.Count(player));

        var blockBeforeSpiral = player.Creature.Block;
        await CombatScenarioContext.PlayCardAsync(spiralWithExtra, combat.HittableEnemies.First());
        assertions.Equal("spiral_scales_from_memory", 5, player.Creature.Block - blockBeforeSpiral);
        var extraSpirals = playerCombat.Hand.Cards
            .OfType<Spiral>()
            .Where(card => card.IsTemporary() && card.ReturnsToMemoryAfterTemporary())
            .ToList();
        assertions.Equal("spiral_extra_generates_three_copies", 3, extraSpirals.Count);
        assertions.True(
            "spiral_extra_copies_are_free",
            extraSpirals.All(card =>
                card.CurrentUpgradeLevel == 0
                && card.EnergyCost.GetWithModifiers(CostModifiers.Local) == 0));

        var blockBeforeExtraSpiral = player.Creature.Block;
        await CombatScenarioContext.PlayCardAsync(extraSpirals[0], combat.HittableEnemies.First());
        assertions.Equal(
            "temporary_spiral_copy_scales_from_memory",
            5,
            player.Creature.Block - blockBeforeExtraSpiral);
        assertions.Equal("pile_memory_selector_released", null, CardSelectCmd.Selector);
        RuntimeTestHost.WriteCheckpoint(
            request,
            "generated_pile_memory_verified",
            "Temporary cards entered the real Memory pile and Remind consumed one without returning its recalled copy.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["fixture"] = new
            {
                seed = request.Seed.ToString("X16"),
                temporary_card = typeof(Spiral).FullName,
                temporary_time_card = typeof(Time).FullName,
                temporary_power_card = typeof(AnotherMe).FullName,
                ordinary_card = typeof(Gale).FullName,
                setup_mutations = new[]
                {
                    "Generated Temporary Spiral -> draw",
                    "Generated Temporary Time -> draw",
                    "Generated ordinary Gale -> hand"
                }
            },
            ["pile_transitions"] = transitions,
            ["after_turn"] = new
            {
                player_turn = playerCombat.TurnNumber,
                temporary_consumed_from_memory = temporary.HasBeenRemovedFromState,
                temporary_time_consumed_from_memory = temporaryTime.HasBeenRemovedFromState,
                recalled_removed_from_state = recalled.All(card => card.HasBeenRemovedFromState),
                ordinary_pile = ordinary.Pile?.Type,
                memory_count_after_recalled_cleanup = SakuraMemoryPile.Count(player)
            }
        };

        void Record(string operation, MegaCrit.Sts2.Core.Models.CardModel card) =>
            transitions.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["operation"] = operation,
                ["card"] = card.GetType().Name,
                ["pile"] = card.Pile?.Type,
                ["owner"] = card.Owner?.NetId,
                ["temporary"] = card.IsTemporary()
            });
    }
}
