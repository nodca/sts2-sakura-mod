using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

// Through poisons every enemy when cards cross Memory or the Exhaust pile: Clow
// on entry, Sakura on entry or exit, one trigger per moved card.
internal static class ThroughPoisonScenario
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
                await CreatureCmd.Stun(enemy);
            }
        });

        var clow = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowThrough>(combat, player);
        await CombatScenarioContext.PlayCardAsync(clow);
        assertions.Equal("through_clow_power_amount", 2, player.Creature.GetPower<ClassicThroughPower>()?.Amount ?? 0);

        var exhausted = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowSword>(combat, player);
        await AssertAllEnemiesGainPoisonAsync("through_clow_exhaust_enters", 2, choice => CardCmd.Exhaust(choice, exhausted));

        var remembered = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowShield>(combat, player);
        await AssertAllEnemiesGainPoisonAsync("through_clow_memory_enters", 2, _ =>
            SakuraActions.MoveExistingCardToPileWithoutVisuals(null, remembered, memory.Type, CardPilePosition.Bottom));
        await AssertAllEnemiesGainPoisonAsync("through_clow_ignores_leaving_memory", 0, _ =>
            RecallAsync(remembered));

        var sakura = await CombatScenarioContext.AddGeneratedCardToHandAsync<SakuraThrough>(combat, player);
        await CombatScenarioContext.PlayCardAsync(sakura);
        assertions.Equal("through_sakura_power_amount", 3, player.Creature.GetPower<ClassicThroughSakuraPower>()?.Amount ?? 0);

        var recalled = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowShield>(combat, player);
        await AssertAllEnemiesGainPoisonAsync("through_both_memory_enters", 5, _ =>
            SakuraActions.MoveExistingCardToPileWithoutVisuals(null, recalled, memory.Type, CardPilePosition.Bottom));
        await AssertAllEnemiesGainPoisonAsync("through_sakura_memory_leaves", 3, _ =>
            RecallAsync(recalled));
        await AssertAllEnemiesGainPoisonAsync("through_exhaust_to_memory_is_one_trigger", 5, _ =>
            SakuraActions.MoveExistingCardToPileWithoutVisuals(null, exhausted, memory.Type, CardPilePosition.Bottom));

        RuntimeTestHost.WriteCheckpoint(
            request,
            "through_poison_verified",
            "Through poisoned every enemy once per card crossing Memory or the Exhaust pile.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["enemy_count"] = enemies.Count,
            ["clow_poison_per_card"] = 2,
            ["sakura_poison_per_card"] = 3
        };

        Task RecallAsync(MegaCrit.Sts2.Core.Models.CardModel card) =>
            SakuraMemoryPile.RecallToHand(player, [card]);

        Task ActAsync(Func<PlayerChoiceContext, Task> execute) =>
            CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, execute));

        async Task AssertAllEnemiesGainPoisonAsync(string name, int expected, Func<PlayerChoiceContext, Task> execute)
        {
            var poisonBefore = enemies.ToDictionary(static enemy => enemy, PoisonOf);
            await ActAsync(execute);
            foreach (var enemy in enemies)
                assertions.Equal($"{name}_{enemy.CombatId}", expected, PoisonOf(enemy) - poisonBefore[enemy]);
        }

        static int PoisonOf(MegaCrit.Sts2.Core.Entities.Creatures.Creature enemy) =>
            enemy.GetPower<PoisonPower>()?.Amount ?? 0;
    }
}
