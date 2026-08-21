using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal static class MagicChargeThresholdScenario
{
    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions)
    {
        var context = await CombatScenarioContext.StartAsync(request);
        var combat = await context.EnterWeakCrawlerCombatAsync();
        var player = context.Player;

        var initialGain = new RuntimeFixtureAction(
            player,
            choiceContext => SakuraMagicCharge.GainMagic(choiceContext, player, 5));
        await CombatScenarioContext.EnqueueAndWaitAsync(initialGain);
        var charge = player.Creature.GetPower<ClassicMagicChargePower>()
            ?? throw new InvalidOperationException("Magic Charge was not created at the midpoint threshold.");
        assertions.True("midpoint_fixture_choice_context", initialGain.ChoiceContext is GameActionPlayerChoiceContext);
        assertions.Equal("midpoint_charge_ready", 5, charge.Amount);
        assertions.True("midpoint_opportunity_ready", charge.ArmedOpportunityGeneration > 0);
        var generationBeforeTurnEnd = charge.ArmedOpportunityGeneration;

        var persistentLock = new RuntimeFixtureAction(
            player,
            choiceContext => PowerCmd.Apply<ClassicLockSakuraPower>(
                choiceContext,
                player.Creature,
                1,
                player.Creature,
                null,
                silent: true));
        await CombatScenarioContext.EnqueueAndWaitAsync(persistentLock);
        assertions.Equal(
            "lock_sakura_arms_next_trigger",
            1,
            player.Creature.GetPower<ClassicLockSakuraPower>()?.Amount ?? 0);

        await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
        assertions.Equal("midpoint_charge_survives_turn", 5, charge.Amount);
        assertions.Equal(
            "midpoint_opportunity_survives_turn",
            generationBeforeTurnEnd,
            charge.ArmedOpportunityGeneration);
        assertions.Equal(
            "lock_sakura_survives_turn_boundary",
            1,
            player.Creature.GetPower<ClassicLockSakuraPower>()?.Amount ?? 0);

        var clearPersistentLock = new RuntimeFixtureAction(
            player,
            async _ =>
            {
                if (player.Creature.GetPower<ClassicLockSakuraPower>() is { } lockPower)
                    await PowerCmd.Remove(lockPower);
            });
        await CombatScenarioContext.EnqueueAndWaitAsync(clearPersistentLock);

        var fireSpell = await CombatScenarioContext.AddGeneratedCardToHandAsync<SpellHuoShen>(combat, player);
        await CombatScenarioContext.PlayCardAsync(fireSpell, combat.HittableEnemies.First());
        assertions.True("midpoint_applies_fire_state", player.Creature.HasPower<ClassicFireyPower>());
        assertions.Equal("midpoint_does_not_spend_charge", 5, charge.Amount);
        assertions.Equal("midpoint_consumes_once", 0, charge.ArmedOpportunityGeneration);

        var waterWithoutOpportunity = await CombatScenarioContext.AddGeneratedCardToHandAsync<SpellShuiLong>(combat, player);
        await CombatScenarioContext.PlayCardAsync(waterWithoutOpportunity);
        assertions.Equal("spent_midpoint_does_not_apply_water", false, player.Creature.HasPower<ClassicWateryPower>());
        assertions.Equal("spent_midpoint_keeps_charge", 5, charge.Amount);

        var reentry = new RuntimeFixtureAction(
            player,
            async choiceContext =>
            {
                await SakuraMagicCharge.SpendMagic(choiceContext, player, 1);
                await SakuraMagicCharge.GainMagic(choiceContext, player, 1);
            });
        await CombatScenarioContext.EnqueueAndWaitAsync(reentry);
        var reentryGeneration = charge.ArmedOpportunityGeneration;
        assertions.True("midpoint_reentry_rearms", reentryGeneration > 0);

        var waterWithOpportunity = await CombatScenarioContext.AddGeneratedCardToHandAsync<SpellShuiLong>(combat, player);
        await CombatScenarioContext.PlayCardAsync(waterWithOpportunity);
        assertions.True("rearmed_midpoint_applies_water", player.Creature.HasPower<ClassicWateryPower>());
        assertions.Equal("rearmed_midpoint_consumes_once", 0, charge.ArmedOpportunityGeneration);

        var reachFull = new RuntimeFixtureAction(
            player,
            choiceContext => SakuraMagicCharge.GainMagic(choiceContext, player, 5));
        await CombatScenarioContext.EnqueueAndWaitAsync(reachFull);
        assertions.Equal("full_threshold_charge", 10, charge.Amount);
        var nonExtraFlower = await CombatScenarioContext.AddGeneratedCardToHandAsync<SakuraFlower>(combat, player);
        await CombatScenarioContext.PlayCardAsync(nonExtraFlower);
        assertions.Equal("flower_without_earthy_exhausts", PileType.Exhaust, nonExtraFlower.Pile?.Type);
        assertions.Equal("full_non_extra_only_gains_charge", 11, charge.Amount);
        assertions.Equal(
            "full_non_extra_does_not_apply_element",
            false,
            player.Creature.HasPower<ClassicEarthyPower>());
        var triggerBefore = SakuraActions.ExtraEffectTriggerCountThisTurn(player);

        var flower = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowFlower>(combat, player);
        await CombatScenarioContext.PlayCardAsync(flower);
        assertions.Equal(
            "flower_does_not_self_enable_non_exhaust",
            PileType.Exhaust,
            flower.Pile?.Type);
        var postFlowerCharge = player.Creature.GetPower<ClassicMagicChargePower>()
            ?? throw new InvalidOperationException("Clow Flower did not restore Magic Charge after Extra spend.");
        assertions.True("full_threshold_applies_earth_state", player.Creature.HasPower<ClassicEarthyPower>());
        assertions.Equal("full_threshold_spends_then_regains", 2, postFlowerCharge.Amount);
        assertions.Equal(
            "full_threshold_triggers_extra",
            triggerBefore + 1,
            SakuraActions.ExtraEffectTriggerCountThisTurn(player));

        var reachOverflow = new RuntimeFixtureAction(
            player,
            choiceContext => SakuraMagicCharge.GainMagic(choiceContext, player, 15));
        await CombatScenarioContext.EnqueueAndWaitAsync(reachOverflow);
        assertions.Equal("overflow_charge_before_extra", 17, postFlowerCharge.Amount);

        var voice = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowVoice>(combat, player);
        await CombatScenarioContext.PlayCardAsync(voice);
        assertions.Equal("overflow_spend_then_regain", 8, postFlowerCharge.Amount);
        assertions.True("overflow_arms_later_midpoint", postFlowerCharge.ArmedOpportunityGeneration > 0);
        assertions.True("overflow_extra_applies_wind_state", player.Creature.HasPower<ClassicWindyPower>());
        assertions.Equal(
            "overflow_triggers_second_extra",
            triggerBefore + 2,
            SakuraActions.ExtraEffectTriggerCountThisTurn(player));

        var clearFire = new RuntimeFixtureAction(
            player,
            async choiceContext =>
            {
                if (player.Creature.GetPower<ClassicFireyPower>() is { } firey)
                    await PowerCmd.Remove(firey);
                var hand = player.PlayerCombatState?.Hand.Cards.ToList() ?? [];
                if (hand.Count > 0)
                    await CardCmd.Discard(choiceContext, hand);
            });
        await CombatScenarioContext.EnqueueAndWaitAsync(clearFire);
        var overflowGeneration = postFlowerCharge.ArmedOpportunityGeneration;
        var fireAfterOverflow = await CombatScenarioContext.AddGeneratedCardToHandAsync<SpellHuoShen>(combat, player);
        await CombatScenarioContext.PlayCardAsync(fireAfterOverflow, combat.HittableEnemies.First());
        assertions.True("overflow_opportunity_applies_on_later_card", player.Creature.HasPower<ClassicFireyPower>());
        assertions.Equal("overflow_opportunity_consumed_on_later_card", 0, postFlowerCharge.ArmedOpportunityGeneration);
        assertions.Equal("overflow_opportunity_does_not_spend_charge", 8, postFlowerCharge.Amount);

        var lockCard = await CombatScenarioContext.AddGeneratedCardToHandAsync<SakuraLock>(combat, player);
        await CombatScenarioContext.PlayCardAsync(lockCard);
        assertions.Equal(
            "lock_sakura_rearms_next_trigger",
            1,
            player.Creature.GetPower<ClassicLockSakuraPower>()?.Amount ?? 0);

        var chargeForLock = new RuntimeFixtureAction(
            player,
            choiceContext => SakuraMagicCharge.GainMagic(choiceContext, player, 1));
        await CombatScenarioContext.EnqueueAndWaitAsync(chargeForLock);
        assertions.Equal("lock_sakura_charge_before_trigger", 10, postFlowerCharge.Amount);

        var freeFlower = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowFlower>(combat, player);
        await CombatScenarioContext.PlayCardAsync(freeFlower);
        assertions.Equal("earthy_clow_flower_enters_discard", PileType.Discard, freeFlower.Pile?.Type);
        assertions.Equal("lock_sakura_preserves_charge_then_card_gains", 11, postFlowerCharge.Amount);
        assertions.Equal(
            "lock_sakura_consumed_by_next_trigger",
            0,
            player.Creature.GetPower<ClassicLockSakuraPower>()?.Amount ?? 0);

        var paidFlower = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowFlower>(combat, player);
        await CombatScenarioContext.PlayCardAsync(paidFlower);
        assertions.Equal("earthy_second_clow_flower_enters_discard", PileType.Discard, paidFlower.Pile?.Type);
        assertions.Equal("lock_sakura_only_protects_one_trigger", 2, postFlowerCharge.Amount);

        var earthySakuraFlower = await CombatScenarioContext.AddGeneratedCardToHandAsync<SakuraFlower>(combat, player);
        await CombatScenarioContext.PlayCardAsync(earthySakuraFlower);
        assertions.Equal("earthy_sakura_flower_enters_discard", PileType.Discard, earthySakuraFlower.Pile?.Type);

        RuntimeTestHost.WriteCheckpoint(
            request,
            "magic_charge_thresholds_verified",
            "Native card actions proved midpoint use, Extra spend, overflow, and next-trigger Lock preservation.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["fixture"] = new
            {
                seed = request.Seed.ToString("X16"),
                setup_mutations = new[]
                {
                    "RuntimeFixtureAction -> SakuraMagicCharge.GainMagic(5)",
                    "RuntimeFixtureAction -> 5->4->5 reentry",
                    "RuntimeFixtureAction -> gain to 10",
                    "RuntimeFixtureAction -> gain to 17",
                    "RuntimeFixtureAction -> gain from 9 to 10 for Lock trigger"
                }
            },
            ["thresholds"] = new
            {
                midpoint_reentry_generation = reentryGeneration,
                overflow_generation = overflowGeneration,
                final_charge = postFlowerCharge.Amount,
                firey = player.Creature.HasPower<ClassicFireyPower>(),
                watery = player.Creature.HasPower<ClassicWateryPower>(),
                earthy = player.Creature.HasPower<ClassicEarthyPower>(),
                windy = player.Creature.HasPower<ClassicWindyPower>(),
                extra_trigger_delta = SakuraActions.ExtraEffectTriggerCountThisTurn(player) - triggerBefore
            }
        };
    }
}
