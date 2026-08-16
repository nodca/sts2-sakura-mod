using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models.Powers;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal static class RepairJumpRegenerationScenario
{
    private const int FixtureMagicCharge = 20;

    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions)
    {
        var context = await CombatScenarioContext.StartAsync(request);
        var combat = await context.EnterWeakCrawlerCombatAsync();
        var player = context.Player;

        var fixtureAction = new RuntimeFixtureAction(
            player,
            async choiceContext =>
            {
                await PowerCmd.Apply<ClassicMagicChargePower>(
                    choiceContext,
                    player.Creature,
                    FixtureMagicCharge,
                    player.Creature,
                    null,
                    silent: true);
                await PowerCmd.Apply<VulnerablePower>(
                    choiceContext,
                    player.Creature,
                    2,
                    player.Creature,
                    null,
                    silent: true);
            });
        await CombatScenarioContext.EnqueueAndWaitAsync(fixtureAction);

        var repair = await CombatScenarioContext.AddGeneratedCardToHandAsync<Repair>(combat, player);
        await CombatScenarioContext.PlayCardAsync(repair);

        var regenBeforeJump = player.Creature.GetPower<RegenPower>();
        var protectionBeforeJump = player.Creature.GetPower<RepairRegenerationPower>();
        assertions.Equal("repair_regen_amount_before_jump", 3, regenBeforeJump?.Amount ?? 0);
        assertions.True("repair_protection_before_jump", protectionBeforeJump is not null);
        assertions.Equal("repair_regen_type_before_jump", PowerType.Buff, regenBeforeJump?.TypeForCurrentAmount);
        assertions.Equal("repair_protection_type_before_jump", PowerType.Buff, protectionBeforeJump?.TypeForCurrentAmount);
        assertions.True("vulnerable_present_before_jump", player.Creature.HasPower<VulnerablePower>());

        var jump = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowJump>(combat, player);
        await CombatScenarioContext.PlayCardAsync(jump);

        var regenAfterJump = player.Creature.GetPower<RegenPower>();
        var protectionAfterJump = player.Creature.GetPower<RepairRegenerationPower>();
        assertions.Equal("repair_regen_amount_after_activated_jump", 3, regenAfterJump?.Amount ?? 0);
        assertions.True("repair_protection_after_activated_jump", protectionAfterJump is not null);
        assertions.Equal("vulnerable_removed_by_activated_jump", false, player.Creature.HasPower<VulnerablePower>());

        await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
        assertions.Equal(
            "repair_regen_retained_after_turn_boundary",
            3,
            player.Creature.GetPower<RegenPower>()?.Amount ?? 0);

        RuntimeTestHost.WriteCheckpoint(
            request,
            "repair_jump_regeneration_verified",
            "Activated Clow Jump did not remove Repair regeneration or its protection power.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["fixture"] = new
            {
                seed = request.Seed.ToString("X16"),
                magic_charge = FixtureMagicCharge,
                vulnerable = 2,
                cards = new[] { typeof(Repair).FullName, typeof(ClowJump).FullName },
                setup_mutations = new[]
                {
                    $"RuntimeFixtureAction -> PowerCmd.Apply<{nameof(ClassicMagicChargePower)}>({FixtureMagicCharge})",
                    $"Generated {nameof(Repair)} -> hand -> played with extra effect",
                    $"Generated {nameof(ClowJump)} -> hand -> played with extra effect"
                }
            },
            ["after_jump"] = new
            {
                regen_amount = regenAfterJump?.Amount ?? 0,
                regen_type = regenAfterJump?.TypeForCurrentAmount.ToString(),
                protection_present = protectionAfterJump is not null,
                protection_type = protectionAfterJump?.TypeForCurrentAmount.ToString(),
                vulnerable_present = player.Creature.HasPower<VulnerablePower>(),
                regen_after_turn = player.Creature.GetPower<RegenPower>()?.Amount ?? 0
            }
        };
    }
}
