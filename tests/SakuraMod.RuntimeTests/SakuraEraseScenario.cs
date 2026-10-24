using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models.Powers;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal static class SakuraEraseScenario
{
    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions)
    {
        var context = await CombatScenarioContext.StartAsync(request);
        var combat = await context.EnterWeakSlimesCombatAsync();
        var player = context.Player;
        var normalTarget = combat.Enemies[0];

        var normalSetup = new RuntimeFixtureAction(
            player,
            async choiceContext =>
            {
                await PlayerCmd.GainEnergy(10, player);
                foreach (var enemy in combat.Enemies)
                {
                    await CreatureCmd.SetMaxAndCurrentHp(enemy, 100);
                    await CreatureCmd.Stun(enemy);
                }
                await PowerCmd.Apply<ArtifactPower>(
                    choiceContext,
                    normalTarget,
                    1,
                    player.Creature,
                    null);
            });
        await CombatScenarioContext.EnqueueAndWaitAsync(normalSetup);

        var blockedErase = await CombatScenarioContext.AddGeneratedCardToHandAsync<SakuraErase>(combat, player);
        await CombatScenarioContext.PlayCardAsync(blockedErase, normalTarget);
        assertions.Equal("erase_artifact_consumed", 0, normalTarget.GetPowerAmount<ArtifactPower>());
        assertions.Equal("erase_artifact_blocks_debuff", 0, normalTarget.GetPowerAmount<SakuraErasePower>());

        var appliedErase = await CombatScenarioContext.AddGeneratedCardToHandAsync<SakuraErase>(combat, player);
        await CombatScenarioContext.PlayCardAsync(appliedErase, normalTarget);
        assertions.Equal("erase_debuff_applied", 33, normalTarget.GetPowerAmount<SakuraErasePower>());
        assertions.Equal("erase_no_immediate_hp_loss", 100, normalTarget.CurrentHp);

        var repeatedErase = await CombatScenarioContext.AddGeneratedCardToHandAsync<SakuraErase>(combat, player);
        await CombatScenarioContext.PlayCardAsync(repeatedErase, normalTarget);
        assertions.Equal("erase_debuff_does_not_stack", 33, normalTarget.GetPowerAmount<SakuraErasePower>());

        await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
        assertions.Equal("erase_turn_end_hp_loss", 67, normalTarget.CurrentHp);
        assertions.Equal("erase_persists_after_trigger", 33, normalTarget.GetPowerAmount<SakuraErasePower>());

        var eliteCombat = await context.EnterKnightsEliteCombatAsync();
        var eliteEnemies = eliteCombat.Enemies.ToList();
        assertions.True("erase_elite_has_minion_and_elite", eliteEnemies.Count >= 2);
        var secondaryTarget = eliteEnemies[0];

        var eliteSetup = new RuntimeFixtureAction(
            player,
            async choiceContext =>
            {
                await PlayerCmd.GainEnergy(10, player);
                foreach (var enemy in eliteEnemies)
                {
                    await CreatureCmd.SetMaxAndCurrentHp(enemy, 100);
                    await CreatureCmd.Stun(enemy);
                }
                await PowerCmd.Apply<MinionPower>(
                    choiceContext,
                    secondaryTarget,
                    1,
                    secondaryTarget,
                    null);
            });
        await CombatScenarioContext.EnqueueAndWaitAsync(eliteSetup);
        assertions.True("erase_first_elite_enemy_is_secondary", secondaryTarget.IsSecondaryEnemy);
        assertions.True("erase_later_elite_enemy_is_primary", eliteEnemies.Skip(1).Any(static enemy => enemy.IsPrimaryEnemy));

        var minionErase = await CombatScenarioContext.AddGeneratedCardToHandAsync<SakuraErase>(eliteCombat, player);
        await CombatScenarioContext.PlayCardAsync(minionErase, secondaryTarget);

        assertions.Equal("erase_secondary_keeps_max_hp", 100, secondaryTarget.MaxHp);
        assertions.Equal("erase_secondary_receives_debuff", 33, secondaryTarget.GetPowerAmount<SakuraErasePower>());

        var elite = eliteEnemies.Skip(1).First(static enemy => enemy.IsPrimaryEnemy);
        var eliteErase = await CombatScenarioContext.AddGeneratedCardToHandAsync<SakuraErase>(eliteCombat, player);
        await CombatScenarioContext.PlayCardAsync(eliteErase, elite);
        assertions.Equal("erase_elite_loses_max_hp", 67, elite.MaxHp);
        assertions.Equal("erase_elite_current_hp_clamped", 67, elite.CurrentHp);
        assertions.Equal("erase_elite_has_no_debuff", 0, elite.GetPowerAmount<SakuraErasePower>());

        RuntimeTestHost.WriteCheckpoint(
            request,
            "sakura_erase_verified",
            "Sakura Erase respected Artifact, did not stack, triggered after the target side turn, and classified a minion and an elite independently in an elite encounter.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["normal_target"] = new
            {
                max_hp = normalTarget.MaxHp,
                hp_after_trigger = normalTarget.CurrentHp,
                erase_amount = normalTarget.GetPowerAmount<SakuraErasePower>()
            },
            ["elite_targets"] = eliteEnemies.Select(static enemy => new
            {
                enemy.CombatId,
                secondary = enemy.IsSecondaryEnemy,
                enemy.MaxHp,
                enemy.CurrentHp,
                erase_amount = enemy.GetPowerAmount<SakuraErasePower>()
            }).ToArray()
        };
    }
}
