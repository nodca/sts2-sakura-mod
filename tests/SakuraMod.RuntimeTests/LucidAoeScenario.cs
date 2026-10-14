using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal static class LucidAoeScenario
{
    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request, RuntimeAssertionCollector assertions)
    {
        var context = await CombatScenarioContext.StartAsync(request);
        var combat = await context.EnterWeakSlimesCombatAsync();
        var player = context.Player;
        var enemies = combat.Enemies.ToList();
        assertions.True("multiple_enemies", enemies.Count >= 2);

        async Task PrepareAsync(int magic)
        {
            await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choice =>
            {
                await PlayerCmd.GainEnergy(30, player);
                if (player.Creature.GetPower<ClassicMagicChargePower>() is { } charge)
                    await PowerCmd.Remove(charge);
                if (magic > 0)
                    await PowerCmd.Apply<ClassicMagicChargePower>(choice, player.Creature, magic, player.Creature, null);
                foreach (var enemy in enemies.Where(enemy => enemy.IsAlive))
                {
                    await CreatureCmd.SetMaxAndCurrentHp(enemy, 1000);
                    await CreatureCmd.LoseBlock(enemy, enemy.Block);
                    await CreatureCmd.Stun(enemy);
                }
            }));
        }

        async Task SearchAsync(CardModel chosen)
        {
            await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async _ =>
            {
                await CardPileCmd.Add(chosen, PileType.Draw, CardPilePosition.Top, null);
            }));
            var lucid = await CombatScenarioContext.AddGeneratedCardToHandAsync<Lucid>(combat, player);
            var selector = new TestCardSelector();
            selector.PrepareToSelect([0]);
            var energyBefore = player.PlayerCombatState!.Energy;
            using (CardSelectCmd.UseSelector(selector))
                await CombatScenarioContext.PlayCardAsync(lucid);
            assertions.Equal("lucid_costs_zero_energy", energyBefore, player.PlayerCombatState.Energy);
            assertions.True("search_moves_same_card_to_hand", chosen.Pile?.Type == PileType.Hand);
            assertions.True("lucid_exhausts", lucid.Pile?.Type == PileType.Exhaust);
        }

        await PrepareAsync(0);
        var selected = await CombatScenarioContext.AddGeneratedCardToHandAsync<Neutralize>(combat, player);
        await SearchAsync(selected);
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async _ =>
        {
            foreach (var enemy in enemies)
                await CreatureCmd.GainBlock(enemy, 1, ValueProp.Unpowered, null, true);
        }));
        var before = enemies.Select(enemy => enemy.CurrentHp).ToArray();
        await CombatScenarioContext.PlayCardAsync(selected, enemies[0]);
        for (var i = 0; i < enemies.Count; i++)
        {
            assertions.Equal($"selected_attack_hits_all_with_block_{i}", 2, before[i] - enemies[i].CurrentHp);
            assertions.Equal($"weak_stays_on_original_target_{i}", i == 0 ? 1 : 0, enemies[i].GetPowerAmount<WeakPower>());
        }
        assertions.True("selected_mark_persists_after_play", player.Creature.GetPower<LucidPiercePower>() is not null);

        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async _ =>
        {
            await CardPileCmd.Add(selected, PileType.Hand, CardPilePosition.Bottom, null);
        }));
        before = enemies.Select(enemy => enemy.CurrentHp).ToArray();
        await CombatScenarioContext.PlayCardAsync(selected, enemies[0]);
        for (var i = 0; i < enemies.Count; i++)
            assertions.Equal($"recovered_attack_still_spreads_{i}", 3, before[i] - enemies[i].CurrentHp);

        var unselected = await CombatScenarioContext.AddGeneratedCardToHandAsync<Neutralize>(combat, player);
        before = enemies.Select(enemy => enemy.CurrentHp).ToArray();
        await CombatScenarioContext.PlayCardAsync(unselected, enemies[0]);
        for (var i = 1; i < enemies.Count; i++)
            assertions.Equal($"unselected_attack_does_not_spread_{i}", before[i], enemies[i].CurrentHp);

        var random = await CombatScenarioContext.AddGeneratedCardToHandAsync<SwordBoomerang>(combat, player);
        await SearchAsync(random);
        assertions.Equal("selected_attack_cost_is_not_reduced", 1, random.EnergyCost.GetWithModifiers(CostModifiers.All));
        before = enemies.Select(enemy => enemy.CurrentHp).ToArray();
        await CombatScenarioContext.PlayCardAsync(random);
        for (var i = 0; i < enemies.Count; i++)
            assertions.Equal($"selected_random_attack_hits_all_{i}", 9, before[i] - enemies[i].CurrentHp);

        var unusedPower = await CombatScenarioContext.AddGeneratedCardToHandAsync<Footwork>(combat, player);
        await SearchAsync(unusedPower);
        assertions.Equal("selected_power_cost_not_reduced", 1, unusedPower.EnergyCost.GetWithModifiers(CostModifiers.All));
        await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
        assertions.True("all_selected_marks_expire", player.Creature.GetPower<LucidPiercePower>() is null);

        await PrepareAsync(10);
        var skill = await CombatScenarioContext.AddGeneratedCardToHandAsync<DefendIronclad>(combat, player);
        await SearchAsync(skill);
        assertions.True("skill_selection_does_not_mark", player.Creature.GetPower<LucidPiercePower>() is null);
        assertions.True("extra_active_without_attack_selection", player.Creature.GetPower<LucidPierceAllPower>() is not null);
        assertions.Equal("selected_skill_cost_not_reduced", 1, skill.EnergyCost.GetWithModifiers(CostModifiers.All));
        var energyBeforeSkill = player.PlayerCombatState!.Energy;
        await CombatScenarioContext.PlayCardAsync(skill);
        assertions.Equal("selected_skill_pays_full_cost", energyBeforeSkill - 1, player.PlayerCombatState.Energy);

        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choice =>
        {
            await PowerCmd.Apply<VigorPower>(choice, player.Creature, 6, player.Creature, null);
        }));
        var aoe = await CombatScenarioContext.AddGeneratedCardToHandAsync<Aqua>(combat, player);
        before = enemies.Select(enemy => enemy.CurrentHp).ToArray();
        await CombatScenarioContext.PlayCardAsync(aoe);
        for (var i = 0; i < enemies.Count; i++)
            assertions.Equal($"aqua_vigor_hits_each_enemy_once_{i}", 10, before[i] - enemies[i].CurrentHp);
        assertions.True("aqua_consumes_vigor_after_complete_sweep", player.Creature.GetPower<VigorPower>() is null);

        var storm = await CombatScenarioContext.AddGeneratedCardToHandAsync<SakuraStorm>(combat, player);
        before = enemies.Select(enemy => enemy.CurrentHp).ToArray();
        await CombatScenarioContext.PlayCardAsync(storm);
        var stormDamage = before[0] - enemies[0].CurrentHp;
        assertions.True("manual_random_multi_hit_damage_range", stormDamage >= 28 && stormDamage <= 63);
        for (var i = 1; i < enemies.Count; i++)
            assertions.Equal($"manual_random_multi_hit_hits_all_{i}", stormDamage, before[i] - enemies[i].CurrentHp);

        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async _ =>
        {
            await CreatureCmd.SetCurrentHp(enemies[0], 5);
        }));
        var twin = await CombatScenarioContext.AddGeneratedCardToHandAsync<TwinStrike>(combat, player);
        before = enemies.Select(enemy => enemy.CurrentHp).ToArray();
        await CombatScenarioContext.PlayCardAsync(twin, enemies[0]);
        assertions.True("primary_dies_on_first_hit", enemies[0].IsDead);
        for (var i = 1; i < enemies.Count; i++)
            assertions.Equal($"later_hits_continue_after_primary_dies_{i}", 10, before[i] - enemies[i].CurrentHp);

        await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
        assertions.True("extra_expires_at_turn_end", player.Creature.GetPower<LucidPierceAllPower>() is null);
        var ordinary = await CombatScenarioContext.AddGeneratedCardToHandAsync<Neutralize>(combat, player);
        before = enemies.Select(enemy => enemy.CurrentHp).ToArray();
        await CombatScenarioContext.PlayCardAsync(ordinary, enemies[1]);
        assertions.Equal("next_turn_attack_normal", 3, before[1] - enemies[1].CurrentHp);

        return new Dictionary<string, object?> { ["enemyCount"] = enemies.Count, ["stormDamage"] = stormDamage };
    }
}
