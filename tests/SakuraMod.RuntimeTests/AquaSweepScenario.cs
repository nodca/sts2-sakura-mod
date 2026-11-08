using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models.Powers;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

// Aqua hits every enemy once through one native attack context, adds 4 per
// Frostbite stack on each target, and its Extra gains 2 Energy.
internal static class AquaSweepScenario
{
    private const int FixtureVigor = 6;
    private const int FixtureFrostbite = 2;

    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request, RuntimeAssertionCollector assertions)
    {
        var context = await CombatScenarioContext.StartAsync(request);
        var combat = await context.EnterWeakSlimesCombatAsync();
        var player = context.Player;
        var enemies = combat.Enemies.ToList();
        assertions.True("multiple_enemies", enemies.Count >= 2);

        async Task PrepareAsync(int magic, int vigor, int frostbiteOnFirst)
        {
            await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choice =>
            {
                await PlayerCmd.GainEnergy(10, player);
                if (player.Creature.GetPower<ClassicMagicChargePower>() is { } charge)
                    await PowerCmd.Remove(charge);
                if (magic > 0)
                    await PowerCmd.Apply<ClassicMagicChargePower>(choice, player.Creature, magic, player.Creature, null);
                if (vigor > 0)
                    await PowerCmd.Apply<VigorPower>(choice, player.Creature, vigor, player.Creature, null);
                foreach (var enemy in enemies.Where(static enemy => enemy.IsAlive))
                {
                    await CreatureCmd.SetMaxAndCurrentHp(enemy, 1000);
                    await CreatureCmd.LoseBlock(enemy, enemy.Block);
                    if (enemy.GetPower<SakuraFrostbitePower>() is { } frostbite)
                        await PowerCmd.Remove(frostbite);
                }
                if (frostbiteOnFirst > 0)
                    await PowerCmd.Apply<SakuraFrostbitePower>(choice, enemies[0], frostbiteOnFirst, player.Creature, null);
            }));
        }

        // Vigor applies to the whole sweep once and is consumed after it.
        await PrepareAsync(magic: 0, vigor: FixtureVigor, frostbiteOnFirst: 0);
        var vigorAqua = await CombatScenarioContext.AddGeneratedCardToHandAsync<Aqua>(combat, player);
        var before = enemies.Select(static enemy => enemy.CurrentHp).ToArray();
        var energyBefore = player.PlayerCombatState!.Energy;
        await CombatScenarioContext.PlayCardAsync(vigorAqua);
        assertions.Equal("aqua_costs_one", energyBefore - 1, player.PlayerCombatState.Energy);
        for (var i = 0; i < enemies.Count; i++)
            assertions.Equal($"aqua_vigor_hits_each_enemy_once_{i}", 6 + FixtureVigor, before[i] - enemies[i].CurrentHp);
        assertions.True("aqua_consumes_vigor_after_complete_sweep", player.Creature.GetPower<VigorPower>() is null);

        // Frostbite adds 4 per stack to its holder only, and still amplifies the hit by 10% per stack.
        await PrepareAsync(magic: 10, vigor: 0, frostbiteOnFirst: FixtureFrostbite);
        var frostAqua = await CombatScenarioContext.AddGeneratedCardToHandAsync<Aqua>(combat, player);
        before = enemies.Select(static enemy => enemy.CurrentHp).ToArray();
        energyBefore = player.PlayerCombatState.Energy;
        await CombatScenarioContext.PlayCardAsync(frostAqua);
        var expectedFrostbitten = (int)Math.Floor((6 + 4 * FixtureFrostbite) * (1m + FixtureFrostbite / 10m));
        assertions.Equal("aqua_frostbite_bonus_on_holder", expectedFrostbitten, before[0] - enemies[0].CurrentHp);
        for (var i = 1; i < enemies.Count; i++)
            assertions.Equal($"aqua_no_bonus_without_frostbite_{i}", 6, before[i] - enemies[i].CurrentHp);
        assertions.Equal("aqua_extra_gains_two_energy", energyBefore - 1 + 2, player.PlayerCombatState.Energy);

        return new Dictionary<string, object?>
        {
            ["fixture"] = "Weak slimes at 1000 HP; 6 Vigor sweep; 2 Frostbite on the first enemy with 10 Magic Charge."
        };
    }
}
