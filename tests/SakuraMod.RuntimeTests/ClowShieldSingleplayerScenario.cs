using MegaCrit.Sts2.Core.Entities.Cards;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal static class ClowShieldSingleplayerScenario
{
    private const int FixtureMagicCharge = 20;

    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions)
    {
        var context = await CombatScenarioContext.StartAsync(request);
        var combat = await context.EnterWeakCrawlerCombatAsync();
        var player = context.Player;

        var setup = new RuntimeFixtureAction(
            player,
            choiceContext => SakuraMagicCharge.GainMagic(
                choiceContext,
                player,
                FixtureMagicCharge));
        await CombatScenarioContext.EnqueueAndWaitAsync(setup);

        var shield = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowShield>(combat, player);
        var blockBefore = player.Creature.Block;
        await CombatScenarioContext.PlayCardAsync(shield);

        assertions.True("shield_singleplayer_immediate_block", player.Creature.Block > blockBefore);
        assertions.Equal(
            "shield_singleplayer_ward_applied",
            3,
            player.Creature.GetPower<ClassicShieldWardPower>()?.Amount ?? 0);
        assertions.Equal("shield_singleplayer_card_discarded", PileType.Discard, shield.Pile?.Type);
        RuntimeTestHost.WriteCheckpoint(
            request,
            "shield_singleplayer_verified",
            "A native singleplayer Shield play granted immediate Block and ClassicShieldWardPower(3).");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["fixture"] = new
            {
                seed = request.Seed.ToString("X16"),
                player_count = context.Run.Players.Count,
                magic_charge = FixtureMagicCharge
            },
            ["result"] = new
            {
                block_before = blockBefore,
                block_after = player.Creature.Block,
                ward = player.Creature.GetPower<ClassicShieldWardPower>()?.Amount ?? 0,
                pile = shield.Pile?.Type.ToString()
            }
        };
    }
}
