using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal static class CleansingStatsScenario
{
    public static async Task<Dictionary<string, object?>> ExecuteAsync(SakuraTestRequest request, RuntimeAssertionCollector assertions)
    {
        var context = await CombatScenarioContext.StartAsync(request);
        var combat = await context.EnterWeakSlimesCombatAsync();
        var player = context.Player;
        var creature = player.Creature;
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choice =>
        {
            await PlayerCmd.GainEnergy(30, player);
            await CreatureCmd.GainMaxHp(creature, 200);
            await PowerCmd.Apply<StrengthPower>(choice, creature, 1, creature, null);
            await PowerCmd.Apply<StrengthPower>(choice, creature, -2, creature, null);
        }));
        assertions.Equal("permanent_loss_net_negative", -1, creature.GetPowerAmount<StrengthPower>());
        var jump = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowJump>(combat, player);
        await CombatScenarioContext.PlayCardAsync(jump);
        assertions.Equal("jump_clears_current_negative", 0, creature.GetPowerAmount<StrengthPower>());
        await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
        assertions.Equal("permanent_loss_no_end_turn_refund", 0, creature.GetPowerAmount<StrengthPower>());

        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choice =>
        {
            await PlayerCmd.GainEnergy(30, player);
            await PowerCmd.Apply<TenderPower>(choice, creature, 1, combat.Enemies.First(), null);
            await PowerCmd.Apply<ClassicMagicChargePower>(choice, creature, 20, creature, null);
        }));
        for (var i = 0; i < 3; i++)
        {
            var defend = await CombatScenarioContext.AddGeneratedCardToHandAsync<MegaCrit.Sts2.Core.Models.Cards.DefendIronclad>(combat, player);
            await CombatScenarioContext.PlayCardAsync(defend);
        }
        assertions.Equal("tender_three_plays_strength", -3, creature.GetPowerAmount<StrengthPower>());
        assertions.Equal("tender_three_plays_dexterity", -3, creature.GetPowerAmount<DexterityPower>());
        var blank = await CombatScenarioContext.AddGeneratedCardToHandAsync<Blank>(combat, player);
        await CombatScenarioContext.PlayCardAsync(blank);
        assertions.True("blank_preserves_tender", creature.HasPower<TenderPower>());
        assertions.Equal("blank_clears_then_tender_reapplies_strength", -1, creature.GetPowerAmount<StrengthPower>());
        assertions.Equal("blank_clears_then_tender_reapplies_dexterity", -1, creature.GetPowerAmount<DexterityPower>());
        await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
        assertions.Equal("tender_native_refund_strength", 3, creature.GetPowerAmount<StrengthPower>());
        assertions.Equal("tender_native_refund_dexterity", 3, creature.GetPowerAmount<DexterityPower>());
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choice =>
        {
            await SakuraActions.CleanseDebuffs(player);
            await SakuraActions.CleanseDebuffs(player);
        }));
        assertions.Equal("repeat_cleanse_preserves_positive_strength", 3, creature.GetPowerAmount<StrengthPower>());
        assertions.Equal("repeat_cleanse_preserves_positive_dexterity", 3, creature.GetPowerAmount<DexterityPower>());
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choice =>
        {
            await PowerCmd.Remove(creature.GetPower<TenderPower>());
            await PowerCmd.Remove(creature.GetPower<StrengthPower>());
            await PowerCmd.Apply<ClassicTemporaryStrengthPower>(choice, creature, 3, creature, null);
            await PowerCmd.Apply<StrengthPower>(choice, creature, -5, creature, null);
            assertions.Equal("temporary_gain_with_permanent_loss", -2, creature.GetPowerAmount<StrengthPower>());
            await SakuraActions.CleanseDebuffs(player);
            assertions.Equal("mixed_strength_cleared", 0, creature.GetPowerAmount<StrengthPower>());
            assertions.True("temporary_gain_settlement_preserved", creature.HasPower<ClassicTemporaryStrengthPower>());
        }));
        await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
        assertions.Equal("temporary_gain_expires_after_cleanse", -3, creature.GetPowerAmount<StrengthPower>());
        return new() { ["fixture"] = "Native permanent +1/-2 Strength; three native Defends under Tender; activated Blank; native turn boundaries." };
    }
}
