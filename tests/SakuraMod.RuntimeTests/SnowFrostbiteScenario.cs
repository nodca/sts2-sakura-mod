using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal static class SnowFrostbiteScenario
{
    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions)
    {
        var context = await CombatScenarioContext.StartAsync(request);
        var combat = await context.EnterWeakSlimesCombatAsync();
        var player = context.Player;
        var enemies = combat.Enemies.ToArray();
        assertions.True("snow_fixture_multiple_enemies", enemies.Length >= 2);

        async Task ResetAsync(decimal block = 0, bool extra = false)
        {
            await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choiceContext =>
            {
                await PlayerCmd.GainEnergy(20, player);
                if (player.Creature.GetPower<ClassicMagicChargePower>() is { } charge)
                    await PowerCmd.Remove(charge);
                if (extra)
                    await PowerCmd.Apply<ClassicMagicChargePower>(choiceContext, player.Creature, 10, player.Creature, null);
                foreach (var enemy in enemies)
                {
                    foreach (var power in enemy.Powers.ToArray())
                        await PowerCmd.Remove(power);
                    await CreatureCmd.SetMaxAndCurrentHp(enemy, 1000);
                    await CreatureCmd.LoseBlock(enemy, enemy.Block);
                    if (block > 0)
                        await CreatureCmd.GainBlock(enemy, block, ValueProp.Unpowered, null, true);
                }
            }));
        }

        await ResetAsync();
        var zeroSnow = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowSnow>(combat, player);
        await CombatScenarioContext.PlayCardAsync(zeroSnow);
        foreach (var enemy in enemies)
        {
            assertions.Equal($"zero_hits_hp_{enemy.CombatId}", 1000, enemy.CurrentHp);
            assertions.Equal($"zero_hits_no_frostbite_{enemy.CombatId}", 0, enemy.GetPower<SakuraFrostbitePower>()?.Amount ?? 0);
        }

        // Prime the combat-long Water history through real native card plays.
        for (var i = 0; i < 4; i++)
        {
            var aqua = await CombatScenarioContext.AddGeneratedCardToHandAsync<Aqua>(combat, player);
            await CombatScenarioContext.PlayCardAsync(aqua);
        }

        var hits = 5;
        foreach (var mode in new[] { "clow", "clow-upgraded", "sakura", "sakura-blocked", "sakura-partial", "clow-extra" })
        {
            var block = mode == "sakura-blocked" ? 1000 : mode == "sakura-partial" ? 4 : 0;
            await ResetAsync(block, extra: mode == "clow-extra");
            CardModel snow = mode.StartsWith("sakura", StringComparison.Ordinal)
                ? await CombatScenarioContext.AddGeneratedCardToHandAsync<SakuraSnow>(combat, player)
                : await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowSnow>(combat, player);
            if (mode == "clow-upgraded")
                snow.UpgradeInternal();
            await CombatScenarioContext.PlayCardAsync(snow);

            var totalDamage = 0;
            foreach (var enemy in enemies)
            {
                var damage = 1000 - enemy.CurrentHp;
                totalDamage += damage;
                assertions.Equal($"{mode}_frostbite_once_{enemy.CombatId}", damage > 0 ? 2 : 0,
                    enemy.GetPower<SakuraFrostbitePower>()?.Amount ?? 0);
                assertions.Equal($"{mode}_no_freeze_{enemy.CombatId}", null, enemy.GetPower<ClassicFreezePower>());
                if (mode.StartsWith("sakura", StringComparison.Ordinal))
                    assertions.Equal($"{mode}_damage_before_frostbite_{enemy.CombatId}",
                        Math.Max(0, hits * 5 - block), damage);
            }
            if (mode.StartsWith("clow", StringComparison.Ordinal))
                assertions.Equal($"{mode}_damage_before_frostbite", hits * (mode == "clow-upgraded" ? 6 : 4)
                    + (mode == "clow-extra" ? 10 * enemies.Length : 0), totalDamage);
            hits++;
        }

        return new Dictionary<string, object?> { ["snow_frostbite_per_damaged_enemy"] = 2 };
    }
}
