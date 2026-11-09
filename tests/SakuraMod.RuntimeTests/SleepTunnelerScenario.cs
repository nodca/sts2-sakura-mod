using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal static class SleepTunnelerScenario
{
    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request, RuntimeAssertionCollector assertions)
    {
        var cases = new[]
        {
            (Name: "sleep_then_exact_break", SleepFirst: true, WakeBeforeStun: false, WakeAfterStun: false),
            (Name: "sleep_then_overbreak", SleepFirst: true, WakeBeforeStun: true, WakeAfterStun: false),
            (Name: "break_then_sleep", SleepFirst: false, WakeBeforeStun: false, WakeAfterStun: true),
            (Name: "break_then_sleep_then_attack", SleepFirst: false, WakeBeforeStun: true, WakeAfterStun: false),
        };
        for (var i = 0; i < cases.Length; i++)
        {
            var (name, sleepFirst, wakeBeforeStun, wakeAfterStun) = cases[i];
            var context = await CombatScenarioContext.StartAsync(request);
            var combat = await context.EnterTunnelerCombatAsync();
            var player = context.Player;
            var target = combat.HittableEnemies.Single(enemy => enemy.Monster is Tunneler);
            var tunneler = (Tunneler)target.Monster!;
            await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choice =>
            {
                foreach (var other in combat.HittableEnemies.Where(enemy => enemy != target).ToArray())
                    await CreatureCmd.Kill(other);
            }));
            await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
            await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
            assertions.True(name + "_starts_burrowed", target.Block > 0 && target.HasPower<BurrowedPower>());

            async Task PlaySleep()
            {
                var sleep = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowSleep>(combat, player);
                await CombatScenarioContext.PlayCardAsync(sleep, target);
            }

            Task Attack(decimal damage) =>
                CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choice =>
                {
                    var attack = combat.CreateCard<ClowSword>(player);
                    await DamageCmd.Attack(damage).FromCard(attack).Targeting(target).Execute(choice);
                }));

            int SleepAmount() => target.GetPower<ClassicSleepPower>()?.Amount ?? 0;

            if (sleepFirst)
                await PlaySleep();

            var hpBeforeBreak = target.CurrentHp;
            await Attack(target.Block + (sleepFirst && wakeBeforeStun ? 1 : 0));
            assertions.True(name + "_break_removes_burrow", target.Block == 0 && !target.HasPower<BurrowedPower>());
            assertions.Equal(name + "_break_hp_loss", sleepFirst && wakeBeforeStun ? 1 : 0,
                hpBeforeBreak - target.CurrentHp);
            if (!sleepFirst)
            {
                await PlaySleep();
                if (wakeBeforeStun)
                    await Attack(1);
            }

            assertions.Equal(name + "_sleep_before_stun", wakeBeforeStun ? 0 : 2, SleepAmount());
            assertions.True(name + "_stun_remains_queued", target.IsStunned);
            var playerHp = player.Creature.CurrentHp;
            await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
            assertions.Equal(name + "_stun_deals_no_damage", playerHp, player.Creature.CurrentHp);
            assertions.True(name + "_native_stun_callback_ran", !tunneler.IsStunned);
            assertions.Equal(name + "_sleep_after_stun", wakeBeforeStun ? 0 : 1, SleepAmount());

            if (!wakeBeforeStun)
            {
                assertions.True(name + "_remaining_sleep_covers_bite", tunneler.NextMove.Intents is [SleepIntent]);
                if (wakeAfterStun)
                    await Attack(1);
                else
                {
                    await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
                    assertions.Equal(name + "_remaining_sleep_deals_no_damage", playerHp, player.Creature.CurrentHp);
                }
                assertions.Equal(name + "_sleep_ends", 0, SleepAmount());
            }

            assertions.Equal(name + "_returns_to_bite", Tunneler.biteMoveId, tunneler.NextMove.Id);
            if (i + 1 < cases.Length)
                await NGame.Instance!.ReturnToMainMenu();
        }

        return [];
    }
}
