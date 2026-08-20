using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal static class ClowMistSlipperyScenario
{
    private const int FixtureMagicCharge = 10;

    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions)
    {
        var context = await CombatScenarioContext.StartAsync(request);
        var combat = await context.EnterVantomBossCombatAsync();
        var player = context.Player;
        var target = combat.Enemies.Single();
        var slipperyBefore = target.GetPower<SlipperyPower>()?.Amount ?? 0;

        assertions.True(
            "vantom_boss_starts_with_slippery",
            slipperyBefore > 0,
            "The first-act Vantom boss should apply SlipperyPower when it enters combat.");

        var fixtureAction = new RuntimeFixtureAction(
            player,
            choiceContext => PowerCmd.Apply<ClassicMagicChargePower>(
                choiceContext,
                player.Creature,
                FixtureMagicCharge,
                player.Creature,
                null,
                silent: true));
        await CombatScenarioContext.EnqueueAndWaitAsync(fixtureAction);
        assertions.Equal(
            "mist_extra_effect_fixture_charge",
            FixtureMagicCharge,
            player.Creature.GetPower<ClassicMagicChargePower>()?.Amount ?? 0);

        var triggerBefore = SakuraActions.ExtraEffectTriggerCountThisTurn(player);
        var mist = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowMist>(combat, player);
        await CombatScenarioContext.PlayCardAsync(mist);

        assertions.Equal("mist_removes_slippery", null, target.GetPower<SlipperyPower>());
        assertions.Equal(
            "mist_extra_effect_spends_then_regains_magic_charge",
            1,
            player.Creature.GetPower<ClassicMagicChargePower>()?.Amount ?? 0);
        assertions.Equal(
            "mist_extra_effect_triggered",
            triggerBefore + 1,
            SakuraActions.ExtraEffectTriggerCountThisTurn(player));
        assertions.Equal("mist_card_discarded", PileType.Discard, mist.Pile?.Type);

        RuntimeTestHost.WriteCheckpoint(
            request,
            "clow_mist_slippery_verified",
            "A native Clow Mist Extra Effect removed SlipperyPower from the first-act Vantom boss.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["fixture"] = new
            {
                seed = request.Seed.ToString("X16"),
                encounter = "VantomBoss",
                target = target.GetType().FullName,
                slippery_before = slipperyBefore,
                magic_charge = FixtureMagicCharge
            },
            ["result"] = new
            {
                slippery_after = target.GetPower<SlipperyPower>()?.Amount ?? 0,
                magic_charge_after = player.Creature.GetPower<ClassicMagicChargePower>()?.Amount ?? 0,
                extra_effect_triggers = SakuraActions.ExtraEffectTriggerCountThisTurn(player),
                pile = mist.Pile?.Type.ToString()
            }
        };
    }
}
