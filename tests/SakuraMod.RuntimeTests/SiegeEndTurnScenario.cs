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
        await CombatScenarioContext.PlayCardAsync(siege);
        var expectedFirstGain = SiegeRules.BlockAmount(SiegeRules.BaseBlock, enemyCount);
        var blockAtEnemyTurnEnd = player.Creature.Block;
        var hpBeforeResolution = enemies.ToDictionary(static enemy => enemy, static enemy => enemy.CurrentHp);

        assertions.Equal("siege_initial_block_gain", expectedFirstGain, blockAtEnemyTurnEnd - blockBeforePlay);
        assertions.Equal("siege_pending_before_enemy_end", 1, player.Creature.GetPower<SiegePendingPower>()?.Amount ?? 0);

        await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);

        assertions.Equal("siege_growth_not_applied", null, player.Creature.GetPower<SiegeGrowthPower>());
        assertions.Equal("siege_pending_removed_after_resolution", null, player.Creature.GetPower<SiegePendingPower>());
        foreach (var enemy in enemies)
        {
            assertions.Equal(
                $"siege_weak_{enemy.CombatId}",
                SiegeRules.WeakAmount,
                enemy.GetPower<WeakPower>()?.Amount ?? 0);
            assertions.Equal(
                $"siege_extra_damage_{enemy.CombatId}",
                blockAtEnemyTurnEnd,
                hpBeforeResolution[enemy] - enemy.CurrentHp);
        }

        var secondSiege = await CombatScenarioContext.AddGeneratedCardToHandAsync<Siege>(combat, player);
        var blockBeforeSecondPlay = player.Creature.Block;
        await CombatScenarioContext.PlayCardAsync(secondSiege);
        var expectedSecondGain = SiegeRules.BlockAmount(SiegeRules.BaseBlock, enemyCount);
        assertions.Equal(
            "siege_second_copy_has_unchanged_block",
            expectedSecondGain,
            player.Creature.Block - blockBeforeSecondPlay);

        RuntimeTestHost.WriteCheckpoint(
            request,
            "siege_end_turn_verified",
            "Siege retained Block through a stunned enemy turn, then applied Weak and dealt current-Block damage without increasing another copy's Block.");

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
                block_used_for_damage = blockAtEnemyTurnEnd,
                second_block_gain = expectedSecondGain
            }
        };
    }
}
