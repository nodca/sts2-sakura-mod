using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.TestSupport;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

// Lucid looks at the top 3 cards, puts 1 into hand and gives the rest Forgotten,
// so they move from the draw pile to Memory at turn end. Extra makes the pick free
// this turn; the upgrade only removes Exhaust.
internal static class LucidSearchScenario
{
    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request, RuntimeAssertionCollector assertions)
    {
        var context = await CombatScenarioContext.StartAsync(request);
        var combat = await context.EnterWeakSlimesCombatAsync();
        var player = context.Player;
        var playerCombat = player.PlayerCombatState!;

        async Task PrepareAsync(int magic, params CardModel[] topCards)
        {
            await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choice =>
            {
                await PlayerCmd.GainEnergy(10, player);
                if (player.Creature.GetPower<ClassicMagicChargePower>() is { } charge)
                    await PowerCmd.Remove(charge);
                if (magic > 0)
                    await PowerCmd.Apply<ClassicMagicChargePower>(choice, player.Creature, magic, player.Creature, null);
                // Added in reverse so topCards[0] ends up on top.
                foreach (var card in topCards.Reverse())
                    await CardPileCmd.Add(card, PileType.Draw, CardPilePosition.Top, null);
            }));
        }

        async Task<Lucid> PlayLucidAsync(bool upgraded = false)
        {
            var lucid = await CombatScenarioContext.AddGeneratedCardToHandAsync<Lucid>(combat, player);
            if (upgraded)
                lucid.UpgradeInternal();
            var selector = new TestCardSelector();
            selector.PrepareToSelect([0]);
            var energyBefore = playerCombat.Energy;
            using (RunManager.Instance.CardSelectorStack.UseSelector(player, selector))
                await CombatScenarioContext.PlayCardAsync(lucid);
            assertions.Equal("lucid_costs_zero", energyBefore, playerCombat.Energy);
            return lucid;
        }

        int Cost(CardModel card) => card.EnergyCost.GetWithModifiers(CostModifiers.All);

        var bludgeon = combat.CreateCard<Bludgeon>(player);
        var defend = combat.CreateCard<DefendIronclad>(player);
        var strike = combat.CreateCard<StrikeIronclad>(player);
        await PrepareAsync(0, bludgeon, defend, strike);
        var baseLucid = await PlayLucidAsync();
        assertions.True("lucid_exhausts", baseLucid.Pile?.Type == PileType.Exhaust);
        assertions.True("pick_moves_same_card_to_hand", bludgeon.Pile?.Type == PileType.Hand);
        assertions.Equal("pick_keeps_its_cost", 3, Cost(bludgeon));
        assertions.True("pick_stays_unforgotten", !bludgeon.IsTemporary());
        assertions.True("rest_stay_in_draw",
            defend.Pile?.Type == PileType.Draw && strike.Pile?.Type == PileType.Draw);
        assertions.True("rest_gain_forgotten", defend.IsTemporary() && strike.IsTemporary());

        var extraBludgeon = combat.CreateCard<Bludgeon>(player);
        var extraRest = combat.CreateCard<DefendIronclad>(player);
        var extraRestTwo = combat.CreateCard<StrikeIronclad>(player);
        await PrepareAsync(10, extraBludgeon, extraRest, extraRestTwo);
        await PlayLucidAsync();
        assertions.Equal("extra_pick_costs_zero_this_turn", 0, Cost(extraBludgeon));
        assertions.True("extra_rest_gain_forgotten", extraRest.IsTemporary() && extraRestTwo.IsTemporary());

        await PrepareAsync(0, combat.CreateCard<DefendIronclad>(player), combat.CreateCard<DefendIronclad>(player),
            combat.CreateCard<DefendIronclad>(player));
        var upgradedLucid = await PlayLucidAsync(upgraded: true);
        assertions.True("upgraded_lucid_does_not_exhaust", upgradedLucid.Pile?.Type != PileType.Exhaust);

        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async _ =>
        {
            foreach (var enemy in combat.Enemies.Where(static enemy => enemy.IsAlive))
                await CreatureCmd.Stun(enemy);
        }));
        await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
        var memory = SakuraMemoryPile.Get(player)
            ?? throw new InvalidOperationException("Memory pile is unavailable after Lucid's turn end.");
        assertions.True("forgotten_rest_move_to_memory",
            ReferenceEquals(defend.Pile, memory) && ReferenceEquals(strike.Pile, memory));
        assertions.Equal("extra_free_cost_expires_after_turn", 3, Cost(extraBludgeon));

        return new Dictionary<string, object?>
        {
            ["fixture"] = "Native top-of-draw cards; Lucid with and without 10 Magic Charge; upgraded Lucid; one stunned turn boundary."
        };
    }
}
