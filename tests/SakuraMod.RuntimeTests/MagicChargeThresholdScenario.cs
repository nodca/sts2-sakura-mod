using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using System.Reflection;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.FourthAct.Fire.Powers;
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
            ?? throw new InvalidOperationException("Magic Charge was not created.");
        assertions.True("charge_fixture_choice_context", initialGain.ChoiceContext is GameActionPlayerChoiceContext);
        assertions.Equal("initial_charge", 5, charge.Amount);

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
        assertions.Equal("charge_survives_turn", 5, charge.Amount);
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
        assertions.Equal("fire_spell_at_five_does_not_enter_state", false, player.Creature.HasPower<ClassicFireyPower>());
        assertions.Equal("fire_spell_gains_charge", 6, charge.Amount);

        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(
            player, choiceContext => SakuraMagicCharge.GainMagic(choiceContext, player, 3)));
        assertions.Equal("charge_before_filling_card", 9, charge.Amount);
        var triggerBefore = SakuraActions.ExtraEffectTriggerCountThisTurn(player);

        var fillingFlower = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowFlower>(combat, player);
        await CombatScenarioContext.PlayCardAsync(fillingFlower);
        assertions.Equal("card_can_fill_charge_for_next_play", 10, charge.Amount);
        assertions.Equal("filling_card_does_not_enter_earth_state", false, player.Creature.HasPower<ClassicEarthyPower>());
        assertions.Equal("filling_card_does_not_trigger_extra", triggerBefore, SakuraActions.ExtraEffectTriggerCountThisTurn(player));

        var nonExtraFlower = await CombatScenarioContext.AddGeneratedCardToHandAsync<SakuraFlower>(combat, player);
        await CombatScenarioContext.PlayCardAsync(nonExtraFlower);
        assertions.Equal("flower_without_earthy_exhausts", PileType.Exhaust, nonExtraFlower.Pile?.Type);
        assertions.Equal("full_non_extra_only_gains_charge", 11, charge.Amount);
        assertions.Equal(
            "full_non_extra_does_not_apply_element",
            false,
            player.Creature.HasPower<ClassicEarthyPower>());

        var flower = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowFlower>(combat, player);
        var blockBeforeFlower = player.Creature.Block;
        await CombatScenarioContext.PlayCardAsync(flower);
        assertions.Equal(
            "flower_entering_play_does_not_trigger_earth_state",
            blockBeforeFlower,
            player.Creature.Block);
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
        assertions.True("overflow_extra_applies_wind_state", player.Creature.HasPower<ClassicWindyPower>());
        assertions.Equal(
            "overflow_triggers_second_extra",
            triggerBefore + 2,
            SakuraActions.ExtraEffectTriggerCountThisTurn(player));

        var fireAfterOverflow = await CombatScenarioContext.AddGeneratedCardToHandAsync<SpellHuoShen>(combat, player);
        await CombatScenarioContext.PlayCardAsync(fireAfterOverflow, combat.HittableEnemies.First());
        assertions.Equal("overflow_does_not_enable_free_state", false, player.Creature.HasPower<ClassicFireyPower>());
        assertions.Equal("fire_spell_adds_charge_after_overflow", 9, postFlowerCharge.Amount);

        var lockCard = await CombatScenarioContext.AddGeneratedCardToHandAsync<SakuraLock>(combat, player);
        await CombatScenarioContext.PlayCardAsync(lockCard);
        assertions.Equal(
            "lock_sakura_rearms_next_trigger",
            1,
            player.Creature.GetPower<ClassicLockSakuraPower>()?.Amount ?? 0);

        assertions.Equal("lock_card_does_not_enter_fire_state", false, player.Creature.HasPower<ClassicFireyPower>());
        assertions.Equal("lock_sakura_charge_before_trigger", 10, postFlowerCharge.Amount);

        // Resolve real overrides: equivalent C# syntax must not affect this contract.
        foreach (var card in new CardModel[]
                 { combat.CreateCard<ClowRain>(player), combat.CreateCard<ClowFlower>(player), combat.CreateCard<SakuraFlower>(player) })
        {
            var method = typeof(CardModel).GetMethod("GetResultLocationForCardPlay", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var location = (CardLocation)method.Invoke(card, null)!;
            assertions.True($"{card.GetType().Name}_keeps_result_context",
                location == new CardLocation(player, PileType.Discard, CardPilePosition.Bottom));
            if (card is ClowFlower or SakuraFlower)
            {
                location = (CardLocation)method.Invoke(card, null)!;
                assertions.True($"{card.GetType().Name}_second_play_exhausts_with_context",
                    location == new CardLocation(player, PileType.Exhaust, CardPilePosition.Bottom));
            }
        }

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

        // Check the pre-play state: entering Fire on the first play must not grant the bonus.
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choiceContext =>
        {
            if (player.Creature.GetPower<ClassicFireyPower>() is { } firey)
                await PowerCmd.Remove(firey);
            await PlayerCmd.GainEnergy(3, player);
        }));
        foreach (var expectedCount in new[] { 1, 2, 3 })
        {
            await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choiceContext =>
                await CardCmd.Discard(choiceContext, player.PlayerCombatState!.Hand.Cards.ToList())));
            var fireyCard = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowFirey>(combat, player);
            if (expectedCount == 3)
                await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, _ =>
                {
                    fireyCard.UpgradeInternal();
                    return Task.CompletedTask;
                }));
            await CombatScenarioContext.PlayCardAsync(fireyCard);
            assertions.Equal($"firey_generates_{expectedCount}_spells", expectedCount,
                player.PlayerCombatState!.Hand.Cards.OfType<SpellHuoShen>().Count());
        }

        var sovereign = combat.HittableEnemies.First();
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choiceContext =>
        {
            if (player.Creature.GetPower<ClassicEarthyPower>() is { } earthy)
                await PowerCmd.Remove(earthy);
            if (player.Creature.GetPower<ClassicFireyPower>() is { } firey)
                await PowerCmd.Remove(firey);
            await PowerCmd.Apply<LightSovereigntyPower>(choiceContext, sovereign, 1, sovereign, null, silent: true);
            await SakuraMagicCharge.SpendAllMagic(choiceContext, player);
            await SakuraMagicCharge.GainMagic(choiceContext, player, 10);
        }));

        var blockedFlower = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowFlower>(combat, player);
        var beforeBlockedExtra = SakuraActions.ExtraEffectTriggerCountThisTurn(player);
        await CombatScenarioContext.PlayCardAsync(blockedFlower);
        assertions.Equal("sovereignty_allows_extra", beforeBlockedExtra + 1, SakuraActions.ExtraEffectTriggerCountThisTurn(player));
        assertions.Equal("sovereignty_still_pays_extra_cost", 1, player.Creature.GetPower<ClassicMagicChargePower>()?.Amount ?? 0);
        assertions.Equal("sovereignty_blocks_extra_element", false, player.Creature.HasPower<ClassicEarthyPower>());
        assertions.Equal("blocked_extra_records_no_entry", false, SakuraElementState.ConsumeEnteredByPlay(blockedFlower, SakuraElement.Earth));

        var blockedFirey = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowFirey>(combat, player);
        await CombatScenarioContext.PlayCardAsync(blockedFirey);
        assertions.Equal("sovereignty_blocks_direct_element", false, player.Creature.HasPower<ClassicFireyPower>());
        assertions.Equal("blocked_direct_effect_records_no_entry", false, SakuraElementState.ConsumeEnteredByPlay(blockedFirey, SakuraElement.Fire));

        RuntimeTestHost.WriteCheckpoint(
            request,
            "magic_charge_thresholds_verified",
            "Native card actions proved the 10-Charge threshold, overflow, Lock Sakura, direct entry, and Sovereignty.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["thresholds"] = new
            {
                final_charge = player.Creature.GetPower<ClassicMagicChargePower>()?.Amount ?? 0,
                extra_trigger_delta = SakuraActions.ExtraEffectTriggerCountThisTurn(player) - triggerBefore
            }
        };
    }
}
