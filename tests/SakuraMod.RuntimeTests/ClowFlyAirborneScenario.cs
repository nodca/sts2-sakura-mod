using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal static class ClowFlyAirborneScenario
{
    private const int FixtureDamage = 10;
    private const int ExtraMagicCharge = 10;

    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions)
    {
        var context = await CombatScenarioContext.StartAsync(request);
        var combat = await context.EnterWeakCrawlerCombatAsync();
        var player = context.Player;
        var playerCombat = player.PlayerCombatState
            ?? throw new InvalidOperationException("Player combat state is unavailable.");
        var enemy = combat.Enemies.First(static enemy => enemy.IsAlive);

        // Baseline: the same enemy-sourced powered attack without Airborne.
        var baselineAttackLoss = await DealDamageAsync(player, enemy, ValueProp.Move);
        assertions.Equal("baseline_attack_is_unmodified", FixtureDamage, baselineAttackLoss);

        // Turn 1: unupgraded Clow Fly without Extra -> Airborne 1, no draw.
        await SetMagicChargeAsync(player, 0);
        var fly = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowFly>(combat, player);
        var handBeforeFly = playerCombat.Hand.Cards.Count;
        var extraBefore = SakuraActions.ExtraEffectTriggerCountThisTurn(player);
        await CombatScenarioContext.PlayCardAsync(fly);
        assertions.Equal(
            "fly_without_extra_does_not_trigger_extra",
            extraBefore,
            SakuraActions.ExtraEffectTriggerCountThisTurn(player));
        assertions.Equal("fly_applies_airborne_1", 1, AirborneAmount(player));
        assertions.Equal("fly_unupgraded_draws_nothing", handBeforeFly - 1, playerCombat.Hand.Cards.Count);
        assertions.Equal("fly_exhausts", PileType.Exhaust, fly.Pile?.Type);

        var airborneAttackLoss = await DealDamageAsync(player, enemy, ValueProp.Move);
        assertions.Equal("airborne_halves_enemy_attack", FixtureDamage / 2, airborneAttackLoss);
        var airborneUnpoweredLoss = await DealDamageAsync(player, enemy, ValueProp.Unpowered);
        assertions.Equal("airborne_ignores_unpowered_damage", FixtureDamage, airborneUnpoweredLoss);
        var airborneHpLoss = await DealDamageAsync(player, enemy, ValueProp.Unblockable | ValueProp.Unpowered);
        assertions.Equal("airborne_ignores_hp_loss", FixtureDamage, airborneHpLoss);
        assertions.Equal("airborne_not_consumed_by_hits", 1, AirborneAmount(player));
        RuntimeTestHost.WriteCheckpoint(
            request,
            "clow_fly_airborne_damage_verified",
            "Clow Fly applied Airborne 1, which halved an enemy powered attack and ignored non-attack damage.");

        var hpBeforeEnemyTurn1 = player.Creature.CurrentHp;
        await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
        assertions.Equal("airborne_1_removed_at_next_turn_start", 0, AirborneAmount(player));
        assertions.Equal("airborne_1_power_absent", null, player.Creature.GetPower<AirbornePower>());
        RuntimeTestHost.WriteCheckpoint(
            request,
            "clow_fly_airborne_1_expired",
            "Airborne 1 survived the enemy turn and was removed at the player's next turn start.");

        // Turn 2: Clow Fly with Extra -> Airborne 2, lasts two enemy turns.
        await SetMagicChargeAsync(player, ExtraMagicCharge);
        var extraFly = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowFly>(combat, player);
        var extraTriggerBefore = SakuraActions.ExtraEffectTriggerCountThisTurn(player);
        await CombatScenarioContext.PlayCardAsync(extraFly);
        assertions.Equal(
            "extra_fly_triggers_extra",
            1,
            SakuraActions.ExtraEffectTriggerCountThisTurn(player) - extraTriggerBefore);
        assertions.Equal("extra_fly_applies_airborne_2", 2, AirborneAmount(player));

        await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
        assertions.Equal("airborne_2_decrements_to_1", 1, AirborneAmount(player));
        await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
        assertions.Equal("airborne_2_removed_after_second_turn", 0, AirborneAmount(player));
        RuntimeTestHost.WriteCheckpoint(
            request,
            "clow_fly_airborne_2_expired",
            "Extra Clow Fly applied Airborne 2, which survived two enemy turns.");

        // Turn 4: upgraded Clow Fly without Extra -> Airborne 1 and draw exactly 1.
        await SetMagicChargeAsync(player, 0);
        var upgradedFly = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowFly>(combat, player);
        upgradedFly.UpgradeInternal();
        var handBeforeUpgraded = playerCombat.Hand.Cards.Count;
        await CombatScenarioContext.PlayCardAsync(upgradedFly);
        assertions.Equal("upgraded_fly_applies_airborne_1", 1, AirborneAmount(player));
        assertions.Equal("upgraded_fly_draws_one", handBeforeUpgraded, playerCombat.Hand.Cards.Count);
        RuntimeTestHost.WriteCheckpoint(
            request,
            "clow_fly_upgraded_draw_verified",
            "Upgraded Clow Fly applied Airborne 1 and drew exactly one card.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["fixture"] = new
            {
                seed = request.Seed.ToString("X16"),
                enemy = enemy.Monster?.Id.Entry,
                fixture_damage = FixtureDamage
            },
            ["damage"] = new
            {
                baseline_attack = baselineAttackLoss,
                airborne_attack = airborneAttackLoss,
                airborne_unpowered = airborneUnpoweredLoss,
                airborne_hp_loss = airborneHpLoss,
                hp_before_enemy_turn_1 = hpBeforeEnemyTurn1
            },
            ["upgraded"] = new
            {
                hand_before = handBeforeUpgraded,
                hand_after = playerCombat.Hand.Cards.Count
            }
        };
    }

    private static int AirborneAmount(Player player) =>
        player.Creature.GetPower<AirbornePower>()?.Amount ?? 0;

    private static async Task<int> DealDamageAsync(Player player, Creature enemy, ValueProp props)
    {
        var before = player.Creature.CurrentHp + player.Creature.Block;
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(
            player,
            choiceContext => CreatureCmd.Damage(
                choiceContext,
                player.Creature,
                FixtureDamage,
                props,
                enemy,
                null)));
        return before - (player.Creature.CurrentHp + player.Creature.Block);
    }

    private static Task SetMagicChargeAsync(Player player, int amount) =>
        CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(
            player,
            async choiceContext =>
            {
                await SakuraMagicCharge.SpendAllMagic(choiceContext, player);
                if (amount > 0)
                    await SakuraMagicCharge.GainMagic(choiceContext, player, amount);
            }));
}
