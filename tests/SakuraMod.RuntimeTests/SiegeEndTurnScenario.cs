using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models.Powers;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal static class SiegeEndTurnScenario
{
    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions)
    {
        var context = await CombatScenarioContext.StartAsync(request);
        var combat = await context.EnterWeakSlimesCombatAsync();
        var player = context.Player;
        var enemies = combat.HittableEnemies.ToList();
        var enemyCount = enemies.Count;

        var setup = new RuntimeFixtureAction(
            player,
            async choiceContext =>
            {
                await SakuraMagicCharge.GainMagic(choiceContext, player, 10);
                foreach (var enemy in enemies)
                    await CreatureCmd.Stun(enemy);
            });
        await CombatScenarioContext.EnqueueAndWaitAsync(setup);

        var siege = await CombatScenarioContext.AddGeneratedCardToHandAsync<Siege>(combat, player);
        var blockBeforePlay = player.Creature.Block;
        var hpBeforePlay = enemies.ToDictionary(static enemy => enemy, static enemy => enemy.CurrentHp);
        await CombatScenarioContext.PlayCardAsync(siege);
        var expectedFirstGain = SiegeRules.BlockAmount(SiegeRules.BlockPerEnemy, enemyCount);
        var blockAfterPlay = player.Creature.Block;

        assertions.Equal("siege_initial_block_gain", expectedFirstGain, blockAfterPlay - blockBeforePlay);
        assertions.Equal("siege_pending_before_enemy_end", 1, player.Creature.GetPower<SiegePendingPower>()?.Amount ?? 0);
        foreach (var enemy in enemies)
        {
            assertions.Equal(
                $"siege_extra_weak_on_play_{enemy.CombatId}",
                SiegeRules.ExtraWeak,
                enemy.GetPower<WeakPower>()?.Amount ?? 0);
            assertions.Equal(
                $"siege_extra_deals_no_damage_{enemy.CombatId}",
                0,
                hpBeforePlay[enemy] - enemy.CurrentHp);
        }
        var hpBeforeResolution = enemies.ToDictionary(static enemy => enemy, static enemy => enemy.CurrentHp);

        await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);

        assertions.Equal("siege_growth_not_applied", null, player.Creature.GetPower<SiegeGrowthPower>());
        assertions.Equal("siege_pending_removed_after_resolution", null, player.Creature.GetPower<SiegePendingPower>());
        foreach (var enemy in enemies)
        {
            assertions.Equal(
                // Extra Weak ticks down once at the enemy turn end, then Siege adds its own Weak.
                $"siege_weak_{enemy.CombatId}",
                SiegeRules.ExtraWeak - 1 + SiegeRules.WeakAmount,
                enemy.GetPower<WeakPower>()?.Amount ?? 0);
            assertions.Equal(
                $"siege_no_turn_end_damage_{enemy.CombatId}",
                0,
                hpBeforeResolution[enemy] - enemy.CurrentHp);
        }

        var secondSiege = await CombatScenarioContext.AddGeneratedCardToHandAsync<Siege>(combat, player);
        var blockBeforeSecondPlay = player.Creature.Block;
        var secondEnemyCount = combat.HittableEnemies.Count();
        await CombatScenarioContext.PlayCardAsync(secondSiege);
        var expectedSecondGain = SiegeRules.BlockAmount(SiegeRules.BlockPerEnemy, secondEnemyCount);
        assertions.Equal(
            "siege_second_copy_has_unchanged_block",
            expectedSecondGain,
            player.Creature.Block - blockBeforeSecondPlay);

        RuntimeTestHost.WriteCheckpoint(
            request,
            "siege_end_turn_verified",
            "Siege Extra applied 2 Weak on play, then the retained Block applied Weak at enemy turn end without damage or growing another copy's Block.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["fixture"] = new
            {
                seed = request.Seed.ToString("X16"),
                enemies = enemyCount,
                extra_effect = true,
                enemy_actions = "stunned"
            },
            ["resolution"] = new
            {
                first_block_gain = expectedFirstGain,
                block_after_play = blockAfterPlay,
                second_block_gain = expectedSecondGain
            }
        };
    }
}
