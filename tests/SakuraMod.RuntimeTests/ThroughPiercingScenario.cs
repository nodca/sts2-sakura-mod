using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.TestProtocol;
using Break = SakuraMod.SakuraModCode.Cards.Break;

namespace SakuraMod.RuntimeTests;

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
        assertions.True("through_has_multiple_ordered_enemies", enemies.Count >= 2);

        var setup = new RuntimeFixtureAction(
            player,
            async choiceContext =>
            {
                await SakuraMagicCharge.GainMagic(choiceContext, player, 6);
                foreach (var enemy in enemies)
                {
                    await CreatureCmd.GainMaxHp(enemy, 100);
                    await CreatureCmd.GainBlock(enemy, 100, ValueProp.Unpowered, null, true);
                    await CreatureCmd.Stun(enemy);
                }
            });
        await CombatScenarioContext.EnqueueAndWaitAsync(setup);

        var through = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowThrough>(combat, player);
        await CombatScenarioContext.PlayCardAsync(through);
        assertions.Equal("through_clow_power_amount", 1, player.Creature.GetPower<ClassicThroughPower>()?.Amount ?? 0);
        assertions.Equal("through_charge_after_power", 8, player.Creature.GetPower<ClassicMagicChargePower>()?.Amount ?? 0);

        var sword = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowSword>(combat, player);
        var hpBeforeSword = enemies.ToDictionary(static enemy => enemy, static enemy => enemy.CurrentHp);
        var blockBeforeSword = enemies.ToDictionary(static enemy => enemy, static enemy => enemy.Block);
        await CombatScenarioContext.PlayCardAsync(sword, enemies[0]);
        foreach (var enemy in enemies)
        {
            assertions.Equal(
                $"through_clow_sword_unblocked_bonus_{enemy.CombatId}",
                4,
                hpBeforeSword[enemy] - enemy.CurrentHp);
            assertions.Equal(
                $"through_clow_sword_original_blocked_{enemy.CombatId}",
                6,
                blockBeforeSword[enemy] - enemy.Block);
        }

        var clearLastTargetBlock = new RuntimeFixtureAction(
            player,
            _ => CreatureCmd.LoseBlock(enemies[^1], enemies[^1].Block));
        await CombatScenarioContext.EnqueueAndWaitAsync(clearLastTargetBlock);
        var neutralize = await CombatScenarioContext.AddGeneratedCardToHandAsync<Neutralize>(combat, player);
        var beforeSecondCard = enemies.ToDictionary(static enemy => enemy, static enemy => enemy.CurrentHp);
        await CombatScenarioContext.PlayCardAsync(neutralize, enemies[^1]);
        for (var i = 0; i < enemies.Count - 1; i++)
            assertions.Equal($"through_once_per_turn_{enemies[i].CombatId}", beforeSecondCard[enemies[i]], enemies[i].CurrentHp);
        assertions.Equal("through_second_card_hits_only_target", 3, beforeSecondCard[enemies[^1]] - enemies[^1].CurrentHp);

        await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
        var artifactSetup = new RuntimeFixtureAction(
            player,
            async choiceContext =>
            {
                foreach (var enemy in enemies.Where(static enemy => enemy.IsAlive))
                {
                    await CreatureCmd.Stun(enemy);
                    await PowerCmd.Apply<StrengthPower>(choiceContext, enemy, 5, enemy, null);
                }
                await PowerCmd.Apply<ArtifactPower>(choiceContext, enemies[0], 1, player.Creature, null);
            });
        await CombatScenarioContext.EnqueueAndWaitAsync(artifactSetup);

        var originalCard = await CombatScenarioContext.AddGeneratedCardToHandAsync<Neutralize>(combat, player);
        await CombatScenarioContext.PlayCardAsync(originalCard, enemies[^1]);

        var transfer = await CombatScenarioContext.AddGeneratedCardToHandAsync<Transfer>(combat, player);
        var strengthBefore = player.Creature.GetPowerAmount<StrengthPower>();
        var dexterityBefore = player.Creature.GetPowerAmount<DexterityPower>();
        await CombatScenarioContext.PlayCardAsync(transfer, enemies[0]);
        assertions.Equal("through_primary_artifact_blocks_strength_loss", 5, enemies[0].GetPowerAmount<StrengthPower>());
        foreach (var enemy in enemies.Skip(1))
            assertions.Equal($"through_secondary_strength_loss_{enemy.CombatId}", 3, enemy.GetPowerAmount<StrengthPower>());
        assertions.Equal("through_transfer_strength_per_target", enemies.Count, player.Creature.GetPowerAmount<StrengthPower>() - strengthBefore);
        assertions.Equal("through_transfer_dexterity_per_target", enemies.Count, player.Creature.GetPowerAmount<DexterityPower>() - dexterityBefore);

        foreach (var cardCase in new[] { "break", "bubbles", "clow-erase" })
        {
            await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
            var targetSetup = new RuntimeFixtureAction(
                player,
                async choiceContext =>
                {
                    await PlayerCmd.GainEnergy(10, player);
                    if (player.Creature.GetPower<StrengthPower>() is { } strength)
                        await PowerCmd.Remove(strength);
                    if (player.Creature.GetPower<ClassicMagicChargePower>() is { } charge)
                        await PowerCmd.Remove(charge);
                    foreach (var enemy in enemies)
                    {
                        await CreatureCmd.SetMaxAndCurrentHp(enemy, 100);
                        await CreatureCmd.LoseBlock(enemy, enemy.Block);
                        await CreatureCmd.Stun(enemy);
                    }
                    if (cardCase == "break")
                        await CreatureCmd.GainBlock(enemies[0], 10, ValueProp.Unpowered, null, true);
                    if (cardCase == "bubbles")
                    {
                        foreach (var enemy in enemies)
                        {
                            if (enemy.GetPower<StrengthPower>() is { } buff)
                                await PowerCmd.Remove(buff);
                            await PowerCmd.Apply<StrengthPower>(choiceContext, enemy, 5, enemy, null);
                        }
                    }
                    if (cardCase == "clow-erase")
                        await PowerCmd.Apply<MinionPower>(choiceContext, enemies[0], 1, enemies[0], null);
                });
            await CombatScenarioContext.EnqueueAndWaitAsync(targetSetup);

            var card = cardCase switch
            {
                "break" => (MegaCrit.Sts2.Core.Models.CardModel)await CombatScenarioContext.AddGeneratedCardToHandAsync<Break>(combat, player),
                "bubbles" => await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowBubbles>(combat, player),
                _ => await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowErase>(combat, player)
            };
            await CombatScenarioContext.PlayCardAsync(card, enemies[0]);

            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (cardCase == "clow-erase" && i == 0)
                {
                    assertions.True("through_clow_erase_kills_primary_minion", !enemy.IsAlive);
                    continue;
                }
                var expectedDamage = cardCase switch
                {
                    "break" => i == 0 ? 14 : 7,
                    "bubbles" => 5,
                    _ => 9
                };
                assertions.Equal($"through_{cardCase}_damage_once_{enemy.CombatId}", expectedDamage, 100 - enemy.CurrentHp);
                if (cardCase == "break")
                    assertions.Equal($"through_break_removes_block_{enemy.CombatId}", 0, enemy.Block);
                if (cardCase == "bubbles")
                    assertions.Equal($"through_bubbles_removes_buff_{enemy.CombatId}", 0, enemy.GetPowerAmount<StrengthPower>());
            }
        }

        RuntimeTestHost.WriteCheckpoint(
            request,
            "through_piercing_verified",
            "Through propagated automatic commands and manual Transfer, Break, Bubbles, and Erase target transactions without duplicate damage, including continuation after primary minion death.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["enemy_order"] = enemies.Select(static enemy => new { enemy.CombatId, model = enemy.Monster?.Id.Entry }).ToArray(),
            ["clow_snapshot"] = 8,
            ["clow_bonus_per_segment"] = 4,
            ["transfer_target_count"] = enemies.Count
        };
    }
}
