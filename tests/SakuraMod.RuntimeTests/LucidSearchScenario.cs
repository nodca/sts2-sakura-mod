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

// Lucid looks at the top of the draw pile and puts 1 card (2 with Extra) into hand.
// It costs 0, Exhausts, and leaves the picked cards' costs alone.
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

        async Task<Lucid> PlayLucidAsync(params int[] picks)
        {
            var lucid = await CombatScenarioContext.AddGeneratedCardToHandAsync<Lucid>(combat, player);
            var selector = new TestCardSelector();
            foreach (var pick in picks)
                selector.PrepareToSelect([pick]);
            var energyBefore = playerCombat.Energy;
            using (CardSelectCmd.UseSelector(selector))
                await CombatScenarioContext.PlayCardAsync(lucid);
            assertions.Equal("lucid_costs_zero", energyBefore, playerCombat.Energy);
            assertions.True("lucid_exhausts", lucid.Pile?.Type == PileType.Exhaust);
            return lucid;
        }

        int Cost(CardModel card) => card.EnergyCost.GetWithModifiers(CostModifiers.All);

        var bludgeon = combat.CreateCard<Bludgeon>(player);
        var defend = combat.CreateCard<DefendIronclad>(player);
        var strike = combat.CreateCard<StrikeIronclad>(player);
        await PrepareAsync(0, bludgeon, defend, strike);
        await PlayLucidAsync(0);
        assertions.True("pick_moves_same_card_to_hand", bludgeon.Pile?.Type == PileType.Hand);
        assertions.Equal("pick_keeps_its_cost", 3, Cost(bludgeon));
        assertions.True("unpicked_stay_in_draw",
            defend.Pile?.Type == PileType.Draw && strike.Pile?.Type == PileType.Draw);
        assertions.True("unpicked_keep_cost", Cost(defend) == 1 && Cost(strike) == 1);

        var whirlwind = combat.CreateCard<Whirlwind>(player);
        var secondBludgeon = combat.CreateCard<Bludgeon>(player);
        var thirdCard = combat.CreateCard<DefendIronclad>(player);
        await PrepareAsync(10, whirlwind, secondBludgeon, thirdCard);
        await PlayLucidAsync(0, 0);
        assertions.True("extra_picks_two",
            whirlwind.Pile?.Type == PileType.Hand && secondBludgeon.Pile?.Type == PileType.Hand);
        assertions.True("extra_leaves_third_in_draw", thirdCard.Pile?.Type == PileType.Draw);
        assertions.Equal("extra_picks_keep_their_cost", 3, Cost(secondBludgeon));

        return new Dictionary<string, object?>
        {
            ["fixture"] = "Native top-of-draw cards; Lucid with and without 10 Magic Charge."
        };
    }
}
