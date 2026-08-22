using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Afflictions;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Pooling;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.FourthAct.Dark;
using SakuraMod.SakuraModCode.FourthAct.Dark.Afflictions;
using SakuraMod.SakuraModCode.FourthAct.Dark.Cards;
using SakuraMod.SakuraModCode.FourthAct.Dark.Models;
using SakuraMod.SakuraModCode.FourthAct.Dark.Powers;
using SakuraMod.SakuraModCode.FourthAct.Wind.CardState;
using SakuraMod.TestProtocol;
using STS2RitsuLib.Combat.HandSize;
using System.Reflection;

namespace SakuraMod.RuntimeTests;

internal static class DarkEndpointScenario
{
    private static readonly FieldInfo CardOverlayField = typeof(NCard).GetField(
        "_cardOverlay",
        BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(typeof(NCard).FullName, "_cardOverlay");

    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions)
    {
        var context = await CombatScenarioContext.StartAsync(request);
        var combat = await context.EnterDarkCombatAsync();
        var player = context.Player;
        var dark = combat.Enemies.Single();
        var model = dark.Monster as DarkMonster
            ?? throw new InvalidOperationException("The Dark encounter did not create DarkMonster.");
        var openingLights = Hand(player).OfType<MicroLight>().ToList();

        assertions.Equal("dark_base_hp", DarkEnemyRules.BaseHp, dark.MaxHp);
        assertions.Equal("dark_opening_phase", DarkPhase.Veiled, model.Phase);
        assertions.Equal("dark_opening_micro_lights", DarkEnemyRules.MicroLightsPerDraw, openingLights.Count);
        assertions.Equal("dark_opening_veil_layers", DarkEnemyRules.InitialVeilLayers,
            dark.GetPower<DarkVeilPower>()?.Amount ?? -1);
        assertions.True("dark_sovereignty", dark.GetPower<DarkSovereigntyPower>() is not null);
        assertions.True("dark_battle_controller", dark.GetPower<DarkBattlePower>() is not null);
        assertions.Equal("dark_player_has_no_micro_light_marker", null, player.Creature.GetPower<DarkLightPower>());
        assertions.Equal("dark_has_no_phase_one_micro_light_power", null, dark.GetPower<DarkLightPower>());
        assertions.Equal("dark_opening_max_hand_size", 8, MaxHandSizeCalculator.Calculate(player));

        var microLight = combat.CreateCard<MicroLight>(player);
        var temporaryGrant = new RuntimeFixtureAction(
            player,
            choiceContext => AssertMicroLightForgottenImmunity(choiceContext, microLight, assertions));
        await CombatScenarioContext.EnqueueAndWaitAsync(temporaryGrant);
        var generatedImmuneLight = combat.CreateCard<MicroLight>(player);
        var generatedTemporaryGrant = new RuntimeFixtureAction(
            player,
            choiceContext => SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
                generatedImmuneLight,
                new GeneratedCardOptions
                {
                    Pile = PileType.Exhaust,
                    AddTemporary = true
                },
                choiceContext));
        await CombatScenarioContext.EnqueueAndWaitAsync(generatedTemporaryGrant);
        assertions.Equal("dark_generated_micro_light_rejects_forgotten", false, generatedImmuneLight.IsTemporary());
        await AssertSleepingLifecycle(player, assertions);
        await InspectAfflictionVisuals(combat, player, assertions);

        for (var index = 0; index < DarkEnemyRules.InitialVeilLayers; index++)
        {
            var light = openingLights[index];
            await CombatScenarioContext.PlayCardAsync(light);
            if (index < DarkEnemyRules.InitialVeilLayers - 1)
            {
                assertions.Equal($"dark_veil_after_micro_light_{index + 1}",
                    DarkEnemyRules.InitialVeilLayers - index - 1,
                    dark.GetPower<DarkVeilPower>()?.Amount ?? -1);
            }
        }

        assertions.Equal("dark_phase_one_never_installs_micro_light_power", null, dark.GetPower<DarkLightPower>());
        assertions.Equal("dark_veil_broken", null, dark.GetPower<DarkVeilPower>());
        assertions.Equal("dark_break_window_sides", DarkEnemyRules.VeilBreakPlayerSides, model.VeilBreakSidesRemaining);
        assertions.Equal("dark_break_vulnerable", 1, dark.GetPower<VulnerablePower>()?.Amount ?? 0);

        await GivePlayerSurvivalBuffer(player);
        var firstConfinementSelector = new TestCardSelector();
        firstConfinementSelector.PrepareToSelect([0]);
        using (CardSelectCmd.UseSelector(firstConfinementSelector))
            await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);

        var confinementTarget = Hand(player)
            .SingleOrDefault(static card => card.Affliction is DarkConfinementAffliction)
            ?? throw new InvalidOperationException("The first Confinement did not mark one post-draw hand card.");
        assertions.True("dark_confinement_is_temporary", confinementTarget.IsTemporary());
        var exposedWindowLights = Hand(player).OfType<MicroLight>().ToList();
        assertions.Equal("dark_break_keeps_micro_light_pressure", DarkEnemyRules.MicroLightsPerDraw, exposedWindowLights.Count);
        assertions.Equal("dark_break_window_after_first_side", 1, model.VeilBreakSidesRemaining);

        CardCmd.ApplyKeyword(exposedWindowLights[0], CardKeyword.Retain);

        await GivePlayerSurvivalBuffer(player);
        await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
        assertions.True("dark_fade_removes_previous_micro_lights", exposedWindowLights.All(static card => !IsInCombatPile(card)));
        assertions.True("dark_fade_does_not_exhaust", exposedWindowLights.All(static card => card.Pile?.Type != PileType.Exhaust));
        assertions.True("dark_confined_card_enters_memory", ReferenceEquals(confinementTarget.Pile, SakuraMemoryPile.Get(player)));
        assertions.Equal("dark_confined_card_forgotten_cleared", false, confinementTarget.IsTemporary());
        assertions.Equal("dark_confined_card_affliction_cleared", null, confinementTarget.Affliction);
        assertions.Equal("dark_veil_restored", DarkEnemyRules.InitialVeilLayers,
            dark.GetPower<DarkVeilPower>()?.Amount ?? -1);
        assertions.Equal("dark_break_window_finished", 0, model.VeilBreakSidesRemaining);
        assertions.Equal("dark_generation_resumes", DarkEnemyRules.MicroLightsPerDraw, Hand(player).Count(static card => card is MicroLight));

        var hpBeforeTransitionDamage = dark.CurrentHp;
        var transitionDamage = new RuntimeFixtureAction(
            player,
            choiceContext => CreatureCmd.Damage(
                choiceContext,
                dark,
                1100,
                ValueProp.Unpowered,
                player.Creature,
                null));
        await CombatScenarioContext.EnqueueAndWaitAsync(transitionDamage);
        assertions.True("dark_veil_reduces_transition_hit", hpBeforeTransitionDamage - dark.CurrentHp < 1100);
        assertions.Equal("dark_transition_pending", DarkPhase.TransitionPending, model.Phase);
        assertions.True("dark_transition_keeps_veil_until_move", dark.GetPower<DarkVeilPower>() is not null);

        await GivePlayerSurvivalBuffer(player);
        await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
        assertions.Equal("dark_phase_two_started", DarkPhase.EternalNight, model.Phase);
        assertions.Equal("dark_phase_two_night_one", 1, model.Night);
        assertions.Equal("dark_transition_removes_veil", null, dark.GetPower<DarkVeilPower>());
        assertions.Equal("dark_phase_two_installs_zero_micro_light", 0,
            dark.GetPower<DarkLightPower>()?.Amount ?? -1);
        assertions.Equal("dark_phase_two_first_move", DarkMonster.PhaseTwoNonConfinementId, model.NextMove.Id);

        await GivePlayerSurvivalBuffer(player);
        await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
        assertions.Equal("dark_night_two", 2, model.Night);
        assertions.Equal("dark_night_two_next_move", "P2_CONFINEMENT", model.NextMove.Id);
        assertions.Equal("dark_night_one_block", DarkEnemyRules.Block(1), dark.Block);

        await GivePlayerSurvivalBuffer(player);
        await EndConfinementTurn(player);
        assertions.Equal("dark_night_three", 3, model.Night);
        assertions.Equal("dark_night_three_next_move", DarkMonster.PhaseTwoNonConfinementId, model.NextMove.Id);
        assertions.True("dark_night_two_weak", player.Creature.GetPower<WeakPower>() is not null);

        await GivePlayerSurvivalBuffer(player);
        await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
        assertions.Equal("dark_night_four", 4, model.Night);
        assertions.Equal("dark_night_four_next_move", "P2_CONFINEMENT", model.NextMove.Id);
        assertions.Equal("dark_night_three_block", DarkEnemyRules.Block(3), dark.Block);

        await GivePlayerSurvivalBuffer(player);
        await EndConfinementTurn(player);
        assertions.Equal("dark_night_five", DarkEnemyRules.MaximumNight, model.Night);
        assertions.Equal("dark_ultimate_armed", "P2_ULTIMATE", model.NextMove.Id);
        assertions.True("dark_night_four_frail", player.Creature.GetPower<FrailPower>() is not null);

        var voidBeforeUltimate = CountVoids(player);
        await GivePlayerSurvivalBuffer(player);
        await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
        assertions.Equal("dark_ultimate_resets_to_night_three", 3, model.Night);
        assertions.Equal("dark_ultimate_preserves_regular_cadence", DarkMonster.PhaseTwoNonConfinementId, model.NextMove.Id);
        assertions.Equal("dark_ultimate_adds_one_void", voidBeforeUltimate + 1, CountVoids(player));

        var reduceNight = new RuntimeFixtureAction(
            player,
            choiceContext => DarkMicroLightCoordinator.ApplyMicroLight(choiceContext, player, 6));
        await CombatScenarioContext.EnqueueAndWaitAsync(reduceNight);
        assertions.Equal("dark_repeated_thresholds_reduce_night", 1, model.Night);
        assertions.Equal("dark_repeated_thresholds_consume_micro_light", 0, dark.GetPower<DarkLightPower>()?.Amount ?? -1);

        var bankAtNightOne = new RuntimeFixtureAction(
            player,
            choiceContext => DarkMicroLightCoordinator.ApplyMicroLight(choiceContext, player, 3));
        await CombatScenarioContext.EnqueueAndWaitAsync(bankAtNightOne);
        assertions.Equal("dark_night_one_banks_micro_light", 3, dark.GetPower<DarkLightPower>()?.Amount ?? -1);
        await GivePlayerSurvivalBuffer(player);
        await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
        assertions.Equal("dark_natural_night_increase_is_cancelled", 1, model.Night);
        assertions.Equal("dark_banked_micro_light_consumed", 0, dark.GetPower<DarkLightPower>()?.Amount ?? -1);

        RuntimeTestHost.WriteCheckpoint(
            request,
            "dark_endpoint_verified",
            "The Dark crossed native draw, Confinement, Memory cleanup, veil, transition, Eternal Night, ultimate, and Affliction-overlay lifecycles.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["fixture"] = new
            {
                seed = request.Seed.ToString("X16"),
                encounter = combat.Encounter?.Id.ToString(),
                threshold = DarkEnemyRules.MicroLightThreshold
            },
            ["resolution"] = new
            {
                phase = model.Phase.ToString(),
                night = model.Night,
                micro_light = dark.GetPower<DarkLightPower>()?.Amount,
                next_move = model.NextMove.Id,
                memory_count = SakuraMemoryPile.Count(player)
            }
        };
    }

    private static async Task AssertMicroLightForgottenImmunity(
        MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext choiceContext,
        MicroLight microLight,
        RuntimeAssertionCollector assertions)
    {
        var granted = await SakuraForgotten.GrantTemporary(choiceContext, microLight);
        assertions.Equal("dark_micro_light_rejects_forgotten_grant", false, granted);
        assertions.Equal("dark_micro_light_has_no_forgotten_state", false, microLight.IsTemporary());
    }

    private static async Task AssertSleepingLifecycle(
        Player player,
        RuntimeAssertionCollector assertions)
    {
        var sleepingCard = Hand(player).First(static card => card.CanPlay());
        await ApplyAffliction<SleepingAffliction>(player, sleepingCard);
        var energyBefore = player.PlayerCombatState?.Energy ?? -1;
        var pileBefore = sleepingCard.Pile;
        var canPlay = sleepingCard.CanPlay(out var reason, out var preventer);
        var queued = sleepingCard.TryManualPlay(null);

        assertions.Equal("sleeping_manual_play_is_rejected", false, canPlay);
        assertions.Equal("sleeping_manual_play_does_not_enqueue", false, queued);
        assertions.True("sleeping_rejection_reports_hook", reason.HasFlag(UnplayableReason.BlockedByHook));
        assertions.True("sleeping_affliction_is_preventer", preventer is SleepingAffliction);
        assertions.Equal("sleeping_rejection_preserves_energy", energyBefore, player.PlayerCombatState?.Energy ?? -1);
        assertions.True("sleeping_rejection_preserves_hand", ReferenceEquals(pileBefore, sleepingCard.Pile)
            && sleepingCard.Pile?.Type == PileType.Hand);
        assertions.True("sleeping_rejection_preserves_affliction", sleepingCard.Affliction is SleepingAffliction);

        WindSleepingCards.Wake(sleepingCard);
        assertions.Equal("sleeping_wake_clears_affliction", null, sleepingCard.Affliction);
        assertions.True("sleeping_wake_restores_playability", sleepingCard.CanPlay());
    }

    private static async Task InspectAfflictionVisuals(
        CombatState combat,
        Player player,
        RuntimeAssertionCollector assertions)
    {
        NCard? vanillaNode = null;
        NCard? classicNode = null;
        NCard? clearNode = null;
        NCard? reusedNode = null;
        try
        {
            var vanilla = combat.CreateCard<Bash>(player);
            var classic = combat.CreateCard<ClowSword>(player);
            var clear = combat.CreateCard<Gale>(player);
            vanillaNode = CreateAttachedCard(vanilla);
            classicNode = CreateAttachedCard(classic);
            clearNode = CreateAttachedCard(clear);

            await ApplyAffliction<Bound>(player, vanilla);
            await ApplyAffliction<Bound>(player, classic);
            await ApplyAffliction<Bound>(player, clear);
            var nativePosition = RequireOverlay(vanillaNode).Position;
            var expectedClassicPosition = SakuraCardGeometry.ClassicLayoutSize * 0.5f;
            var expectedClearPosition = SakuraCardGeometry.ClearLayoutSize * 0.5f;
            assertions.Equal("dark_affliction_vanilla_position", nativePosition, RequireOverlay(vanillaNode).Position);
            assertions.Equal("dark_affliction_classic_position", expectedClassicPosition, RequireOverlay(classicNode).Position);
            assertions.Equal("dark_affliction_clear_position", expectedClearPosition, RequireOverlay(clearNode).Position);

            var oldClassicOverlay = RequireOverlay(classicNode);
            CardCmd.ClearAffliction(classic);
            assertions.Equal("dark_affliction_classic_removes_live", null, Overlay(classicNode));
            await ApplyAffliction<DarkConfinementAffliction>(player, classic);
            assertions.True(
                "dark_affliction_classic_replaces_live",
                !ReferenceEquals(oldClassicOverlay, RequireOverlay(classicNode)));
            assertions.Equal("dark_affliction_classic_replacement_position", Vector2.Zero, RequireOverlay(classicNode).Position);

            CardCmd.ClearAffliction(clear);
            assertions.Equal("dark_affliction_clear_removes_live", null, Overlay(clearNode));
            await ApplyAffliction<DarkConfinementAffliction>(player, clear);
            assertions.Equal("dark_affliction_clear_replacement_position", Vector2.Zero, RequireOverlay(clearNode).Position);

            CardCmd.ClearAffliction(clear);
            await ApplyAffliction<SleepingAffliction>(player, clear);
            var sleepingOverlay = RequireOverlay(clearNode);
            assertions.Equal("sleeping_overlay_root_name", "SleepingAffliction", sleepingOverlay.Name.ToString());
            assertions.True("sleeping_overlay_has_eyelids", sleepingOverlay.HasNode("MotifGroup/LeftEyelid")
                && sleepingOverlay.HasNode("MotifGroup/RightEyelid"));
            assertions.True("sleeping_overlay_has_breathing_animation", sleepingOverlay.HasNode("AnimationPlayer"));
            CardCmd.ClearAffliction(clear);

            ReleaseCard(classicNode);
            var releasedClassicNode = classicNode;
            classicNode = null;
            var pooledVanilla = combat.CreateCard<Bash>(player);
            await ApplyAffliction<Bound>(player, pooledVanilla);
            reusedNode = CreateAttachedCard(pooledVanilla);
            assertions.True(
                "dark_affliction_pool_reuses_classic_node",
                ReferenceEquals(releasedClassicNode, reusedNode));
            assertions.Equal("dark_affliction_pool_restores_vanilla_position", nativePosition, RequireOverlay(reusedNode).Position);
        }
        finally
        {
            ReleaseCard(reusedNode);
            ReleaseCard(clearNode);
            ReleaseCard(classicNode);
            ReleaseCard(vanillaNode);
        }
    }

    private static Task ApplyAffliction<TAffliction>(Player player, CardModel card)
        where TAffliction : MegaCrit.Sts2.Core.Models.AfflictionModel =>
        CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(
            player,
            async choiceContext =>
            {
                _ = choiceContext;
                await CardCmd.Afflict<TAffliction>(card, 1);
            }));

    private static NCard CreateAttachedCard(CardModel model)
    {
        var card = NCard.Create(model) ?? throw new InvalidOperationException($"Could not create NCard for {model.Id}.");
        if (Engine.GetMainLoop() is not SceneTree tree)
            throw new InvalidOperationException("Godot main loop is not a SceneTree.");

        tree.Root.AddChild(card);
        card.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
        return card;
    }

    private static Control? Overlay(NCard card) => CardOverlayField.GetValue(card) as Control;

    private static Control RequireOverlay(NCard card) =>
        Overlay(card) ?? throw new InvalidOperationException($"{card.Model?.Id} did not create an Affliction overlay.");

    private static void ReleaseCard(NCard? card)
    {
        if (card is null)
            return;

        card.GetParent()?.RemoveChild(card);
        NodePool.Free(card);
    }

    private static IReadOnlyList<CardModel> Hand(Player player) =>
        player.PlayerCombatState?.Hand.Cards
        ?? throw new InvalidOperationException("The player has no combat hand.");

    private static async Task GivePlayerSurvivalBuffer(Player player)
    {
        var action = new RuntimeFixtureAction(
            player,
            async _ =>
            {
                await CreatureCmd.GainMaxHp(player.Creature, 100);
                await CreatureCmd.GainBlock(player.Creature, 200, ValueProp.Unpowered, null, true);
            });
        await CombatScenarioContext.EnqueueAndWaitAsync(action);
    }

    private static async Task EndConfinementTurn(Player player)
    {
        var selector = new TestCardSelector();
        selector.PrepareToSelect([0]);
        using (CardSelectCmd.UseSelector(selector))
            await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
    }

    private static int CountVoids(Player player)
    {
        var piles = new[] { PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust, PileType.Play };
        return piles.Sum(pile => CardPile.Get(pile, player)?.Cards.Count(
            static card => card is MegaCrit.Sts2.Core.Models.Cards.Void) ?? 0);
    }

    private static bool IsInCombatPile(CardModel card) =>
        card.Pile?.IsCombatPile == true;
}
