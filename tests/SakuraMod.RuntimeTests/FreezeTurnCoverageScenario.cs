using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.FourthAct.Water.Powers;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal static class FreezeTurnCoverageScenario
{
    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions)
    {
        var context = await CombatScenarioContext.StartAsync(request);
        foreach (var attackIntent in new[] { false, true })
        foreach (var stacks in new[] { 2, 3 })
        {
            var combat = await context.EnterWeakSlimesCombatAsync();
            var player = context.Player;
            var enemy = combat.HittableEnemies.First();
            var nonAttackMoves = 0;
            var attackMoves = 0;
            var setup = new RuntimeFixtureAction(player, async choiceContext =>
            {
                // Native state-machine fixtures make both non-attack and mixed
                // attack move bodies observable across real turn boundaries.
                foreach (var target in combat.Enemies)
                {
                    var idle = new MoveState("FREEZE_TEST_IDLE", _ => Task.CompletedTask, new BuffIntent())
                    {
                        FollowUpStateId = "FREEZE_TEST_IDLE"
                    };
                    target.Monster!.MoveStateMachine!.States[idle.Id] = idle;
                    target.Monster.SetMoveImmediate(idle, forceTransition: true);
                }
                var attack = new MoveState("FREEZE_TEST_ATTACK", async _ =>
                {
                    attackMoves++;
                    await CreatureCmd.GainBlock(enemy, 99, MegaCrit.Sts2.Core.ValueProps.ValueProp.Unpowered, null);
                }, new SingleAttackIntent(() => 1), new BuffIntent())
                {
                    FollowUpStateId = "FREEZE_TEST_ATTACK"
                };
                var nonAttack = new MoveState("FREEZE_TEST_NON_ATTACK", async _ =>
                {
                    nonAttackMoves++;
                    await PowerCmd.Apply<StrengthPower>(choiceContext, enemy, 1, enemy, null);
                }, new BuffIntent())
                {
                    FollowUpStateId = "FREEZE_TEST_NON_ATTACK"
                };
                enemy.Monster!.MoveStateMachine!.States[attack.Id] = attack;
                enemy.Monster.MoveStateMachine.States[nonAttack.Id] = nonAttack;
                var pendingMove = attackIntent ? attack : nonAttack;
                enemy.Monster.SetMoveImmediate(pendingMove, forceTransition: true);
                // Native Stun resumes the last logged move after the skipped turn.
                enemy.Monster.MoveStateMachine.StateLog.Add(pendingMove);
                await PowerCmd.Apply<ClassicFreezePower>(choiceContext, enemy, stacks, player.Creature, null);
            });
            await CombatScenarioContext.EnqueueAndWaitAsync(setup);
            var prefix = $"freeze_{(attackIntent ? "mixed" : "non_attack")}_{stacks}";
            assertions.True($"{prefix}_immediate_stun", enemy.IsStunned);
            assertions.Equal($"{prefix}_immediate_block", 10m, enemy.Block);

            // The same pending move must not receive another reward during
            // repeated side-start checks or an additional application.
            await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choiceContext =>
            {
                await PowerCmd.Apply<ClassicFreezePower>(choiceContext, enemy, 1, player.Creature, null);
                await PowerCmd.Decrement(enemy.GetPower<ClassicFreezePower>()!);
            }));
            assertions.Equal($"{prefix}_stacking_no_duplicate_block", 10m, enemy.Block);

            for (var remaining = stacks; remaining > 0; remaining--)
            {
                await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
                assertions.Equal($"{prefix}_non_attack_suppressed_at_{remaining}", 0, nonAttackMoves);
                assertions.Equal($"{prefix}_mixed_move_suppressed_at_{remaining}", 0, attackMoves);
                assertions.Equal($"{prefix}_no_strength_from_move_at_{remaining}", 0, enemy.GetPower<StrengthPower>()?.Amount ?? 0);
                assertions.Equal($"{prefix}_decay_at_{remaining}", remaining - 1, enemy.GetPower<ClassicFreezePower>()?.Amount ?? 0);
                assertions.Equal($"{prefix}_old_block_cleared_new_block_once_at_{remaining}", remaining > 1 ? 10m : 0m, enemy.Block);
                assertions.Equal($"{prefix}_stunned_at_{remaining}", remaining > 1, enemy.IsStunned);
            }
            await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
            assertions.Equal($"{prefix}_non_attack_resumes_after_expiry", attackIntent ? 0 : 1, nonAttackMoves);
            assertions.Equal($"{prefix}_attack_resumes_after_expiry", attackIntent ? 1 : 0, attackMoves);
            assertions.Equal($"{prefix}_move_block_resumes_after_expiry", attackIntent ? 99m : 0m, enemy.Block);
            assertions.Equal($"{prefix}_move_strength_resumes_after_expiry", attackIntent ? 0 : 1, enemy.GetPower<StrengthPower>()?.Amount ?? 0);
        }
        var blockBefore = context.Player.Creature.Block;
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(
            context.Player,
            async choiceContext => await PowerCmd.Apply<SakuraFrostbitePower>(
                choiceContext, context.Player.Creature, 12, context.Player.Creature, null)));
        assertions.Equal("player_conversion_still_flat_5_block", 5m, context.Player.Creature.Block - blockBefore);
        assertions.Equal("player_conversion_two_frozen_stacks", 2, context.Player.Creature.GetPower<WaterFrozenPower>()?.Amount ?? 0);
        return new Dictionary<string, object?> { ["freeze_stacks"] = new[] { 2, 3 } };
    }
}
