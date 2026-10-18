using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal static class SleepWakeScenario
{
    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request, RuntimeAssertionCollector assertions)
    {
        var context = await CombatScenarioContext.StartAsync(request);
        var combat = await context.EnterWeakCrawlerCombatAsync();
        var player = context.Player;
        var target = combat.HittableEnemies.First();
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choice =>
        {
            await PowerCmd.Apply<PoisonPower>(choice, target, 4, player.Creature, null);
        }));
        var sleep = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowSleep>(combat, player);
        await CombatScenarioContext.PlayCardAsync(sleep, target);
        assertions.Equal("sleep_card_applies_two_stacks", 2, target.GetPower<ClassicSleepPower>()?.Amount ?? 0);

        var hpBeforePoison = target.CurrentHp;
        await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
        assertions.Equal("poison_damages_sleeping_enemy", hpBeforePoison - 4, target.CurrentHp);
        assertions.Equal("poison_preserves_sleep_and_turn_spends_one_stack", 1,
            target.GetPower<ClassicSleepPower>()?.Amount ?? 0);

        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choice =>
        {
            var attack = combat.CreateCard<ClowSword>(player);
            await CreatureCmd.GainBlock(target, 5, ValueProp.Unpowered, null, false);
            await DamageCmd.Attack(1).FromCard(attack).Targeting(target).Execute(choice);
            assertions.Equal("fully_blocked_attack_does_not_wake", 1,
                target.GetPower<ClassicSleepPower>()?.Amount ?? 0);

            await CreatureCmd.LoseBlock(target, target.Block);
            await CreatureCmd.Damage(choice, target, 1, ValueProp.Unpowered, player.Creature, null);
            assertions.Equal("unpowered_damage_does_not_wake", 1,
                target.GetPower<ClassicSleepPower>()?.Amount ?? 0);

            var hpBeforeAttack = target.CurrentHp;
            await DamageCmd.Attack(1).FromCard(attack).Targeting(target).Execute(choice);
            assertions.Equal("attack_deals_unblocked_damage", hpBeforeAttack - 1, target.CurrentHp);
            assertions.True("unblocked_attack_wakes", target.GetPower<ClassicSleepPower>() is null);
        }));

        return new Dictionary<string, object?>
        {
            ["target_hp"] = target.CurrentHp,
            ["sleep_remaining"] = target.GetPower<ClassicSleepPower>()?.Amount ?? 0
        };
    }
}
