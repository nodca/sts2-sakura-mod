using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.TestSupport;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

// Repair heals the first time each turn a card returns from Memory to the hand,
// then rearms at the next turn.
internal static class RepairMemoryHealScenario
{
    private const int FixtureHp = 30;

    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions)
    {
        var context = await CombatScenarioContext.StartAsync(request);
        var combat = await context.EnterWeakSlimesCombatAsync();
        var player = context.Player;
        var memory = SakuraMemoryPile.Get(player)!;

        var remembered = new List<CardModel>();
        for (var i = 0; i < 3; i++)
            remembered.Add(await CombatScenarioContext.AddGeneratedCardToHandAsync<Gale>(combat, player));
        await ActAsync(async _ =>
        {
            await CreatureCmd.SetCurrentHp(player.Creature, FixtureHp);
            foreach (var card in remembered)
                await SakuraActions.MoveExistingCardToPileWithoutVisuals(null, card, memory.Type, CardPilePosition.Bottom);
        });

        var repair = await CombatScenarioContext.AddGeneratedCardToHandAsync<Repair>(combat, player);
        await CombatScenarioContext.PlayCardAsync(repair);
        var heal = repair.DynamicVars.Heal.IntValue;
        assertions.Equal("repair_power_amount", heal, player.Creature.GetPower<RepairPower>()?.Amount ?? 0);

        await RecallOneAsync();
        assertions.Equal("first_recall_heals", FixtureHp + heal, player.Creature.CurrentHp);
        await RecallOneAsync();
        assertions.Equal("second_recall_same_turn_does_not_heal", FixtureHp + heal, player.Creature.CurrentHp);

        foreach (var enemy in combat.Enemies.Where(static enemy => enemy.IsAlive))
            await CreatureCmd.Stun(enemy);
        await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
        var hpAtNextTurn = player.Creature.CurrentHp;
        await RecallOneAsync();
        assertions.Equal("next_turn_recall_heals_again", hpAtNextTurn + heal, player.Creature.CurrentHp);
        assertions.Equal("selector_released", null, RunManager.Instance.CardSelectorStack.GetSelector(player));

        RuntimeTestHost.WriteCheckpoint(
            request,
            "repair_memory_heal_verified",
            "Repair healed once per turn when a card returned from Memory, and rearmed at the next turn.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["heal"] = heal,
            ["hp_after"] = player.Creature.CurrentHp
        };

        async Task RecallOneAsync()
        {
            var selector = new TestCardSelector();
            selector.PrepareToSelect([0]);
            using (RunManager.Instance.CardSelectorStack.UseSelector(player, selector))
            {
                await ActAsync(async choiceContext =>
                    await Remind.RecallIntoHand(player, choiceContext, 1, upgradeCopies: false));
            }
        }

        Task ActAsync(Func<PlayerChoiceContext, Task> execute) =>
            CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, execute));
    }
}
