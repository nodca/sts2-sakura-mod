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
        var poisonBefore = target.GetPower<PoisonPower>()?.Amount ?? 0;
        var hpBefore = target.CurrentHp;
        await CombatScenarioContext.PlayCardAsync(mist);

        var expectedPoison = poisonBefore + 10;
        assertions.Equal("mist_preserves_slippery", slipperyBefore, target.GetPower<SlipperyPower>()?.Amount ?? 0);
        assertions.Equal("mist_extra_deals_no_immediate_damage", hpBefore, target.CurrentHp);
        assertions.Equal("mist_extra_applies_ten_poison", expectedPoison, target.GetPower<PoisonPower>()?.Amount ?? 0);
        assertions.Equal(
            "mist_extra_effect_spends_then_regains_magic_charge",
            1,
            player.Creature.GetPower<ClassicMagicChargePower>()?.Amount ?? 0);
        assertions.Equal(
            "mist_extra_effect_triggered",
            triggerBefore + 1,
            SakuraActions.ExtraEffectTriggerCountThisTurn(player));
        assertions.Equal("mist_card_discarded", PileType.Discard, mist.Pile?.Type);

        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choiceContext =>
        {
            await PowerCmd.Apply<ClassicMagicChargePower>(choiceContext, player.Creature,
                FixtureMagicCharge - (player.Creature.GetPower<ClassicMagicChargePower>()?.Amount ?? 0),
                player.Creature, null, silent: true);
        }));
        var secondMist = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowMist>(combat, player);
        secondMist.UpgradeInternal();
        var hpBeforeSecondMist = target.CurrentHp;
        var expectedTotalPoison = expectedPoison + 12;
        await CombatScenarioContext.PlayCardAsync(secondMist);
        assertions.Equal("upgraded_mist_extra_deals_no_immediate_damage", hpBeforeSecondMist, target.CurrentHp);
        assertions.Equal("upgraded_mist_extra_applies_twelve_poison", expectedTotalPoison, target.GetPower<PoisonPower>()?.Amount ?? 0);

        RuntimeTestHost.WriteCheckpoint(
            request,
            "clow_mist_slippery_verified",
            "Clow Mist Extra applies 10 Poison, or 12 when upgraded, without immediate damage or removing Slippery.");

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
