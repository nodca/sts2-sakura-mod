using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

// Through pays out when cards cross Memory or the Exhaust pile: Clow on entry,
// Sakura on entry or exit, one trigger per moved card.
internal static class ThroughPiercingScenario
{
    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions)
    {
        var context = await CombatScenarioContext.StartAsync(request);
        var combat = await context.EnterWeakSlimesCombatAsync();
        var player = context.Player;
        var enemies = combat.Enemies.ToList();
        var memory = SakuraMemoryPile.Get(player)!;

        await ActAsync(async _ =>
        {
            foreach (var enemy in enemies)
            {
                await CreatureCmd.GainMaxHp(enemy, 200);
                await CreatureCmd.GainBlock(enemy, 100, ValueProp.Unpowered, null, true);
                await CreatureCmd.Stun(enemy);
            }
        });

        var clow = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowThrough>(combat, player);
        await CombatScenarioContext.PlayCardAsync(clow);
        assertions.Equal("through_clow_power_amount", 3, player.Creature.GetPower<ClassicThroughPower>()?.Amount ?? 0);

        var exhausted = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowSword>(combat, player);
        await AssertAllEnemiesLoseAsync("through_clow_exhaust_enters", 3, choice => CardCmd.Exhaust(choice, exhausted));

        var remembered = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowShield>(combat, player);
        await AssertAllEnemiesLoseAsync("through_clow_memory_enters", 3, _ =>
            SakuraActions.MoveExistingCardToPileWithoutVisuals(null, remembered, memory.Type, CardPilePosition.Bottom));
        await AssertAllEnemiesLoseAsync("through_clow_ignores_leaving_memory", 0, _ =>
            ConsumeAsync(remembered));

        var sakura = await CombatScenarioContext.AddGeneratedCardToHandAsync<SakuraThrough>(combat, player);
        await CombatScenarioContext.PlayCardAsync(sakura);
        assertions.Equal("through_sakura_power_amount", 4, player.Creature.GetPower<ClassicThroughSakuraPower>()?.Amount ?? 0);

        var recalled = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowShield>(combat, player);
        await AssertAllEnemiesLoseAsync("through_both_memory_enters", 7, _ =>
            SakuraActions.MoveExistingCardToPileWithoutVisuals(null, recalled, memory.Type, CardPilePosition.Bottom));
        await AssertAllEnemiesLoseAsync("through_sakura_memory_leaves", 4, _ =>
            ConsumeAsync(recalled));
        await AssertAllEnemiesLoseAsync("through_exhaust_to_memory_is_one_trigger", 7, _ =>
            SakuraActions.MoveExistingCardToPileWithoutVisuals(null, exhausted, memory.Type, CardPilePosition.Bottom));

        RuntimeTestHost.WriteCheckpoint(
            request,
            "through_piercing_verified",
            "Through dealt unblockable damage to every enemy once per card crossing Memory or the Exhaust pile.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["enemy_count"] = enemies.Count,
            ["clow_damage_per_card"] = 3,
            ["sakura_damage_per_card"] = 4
        };

        async Task ConsumeAsync(MegaCrit.Sts2.Core.Models.CardModel card) =>
            SakuraGeneratedCardLifecycle.RemoveDetachedGeneratedChoices(await SakuraMemoryPile.Consume(player, [card]));

        Task ActAsync(Func<PlayerChoiceContext, Task> execute) =>
            CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, execute));

        async Task AssertAllEnemiesLoseAsync(string name, int expected, Func<PlayerChoiceContext, Task> execute)
        {
            var hpBefore = enemies.ToDictionary(static enemy => enemy, static enemy => enemy.CurrentHp);
            var blockBefore = enemies.ToDictionary(static enemy => enemy, static enemy => enemy.Block);
            await ActAsync(execute);
            foreach (var enemy in enemies)
            {
                assertions.Equal($"{name}_{enemy.CombatId}", expected, hpBefore[enemy] - enemy.CurrentHp);
                assertions.Equal($"{name}_ignores_block_{enemy.CombatId}", blockBefore[enemy], enemy.Block);
            }
        }
    }
}
