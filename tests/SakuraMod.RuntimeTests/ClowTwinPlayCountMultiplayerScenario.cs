using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Runs;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal static class ClowTwinPlayCountMultiplayerScenario
{
    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        SakuraRuntimeEnvironment environment,
        RuntimeAssertionCollector assertions)
    {
        await using var context = await MultiplayerScenarioContext.StartAsync(request);
        var combat = await context.EnterWeakCrawlerCombatAsync();
        var owner = context.ClientPlayer;
        var twin = combat.CreateCard<ClowTwin>(owner);
        var floating = combat.CreateCard<ClowFloat>(owner);
        var secondFloat = combat.CreateCard<ClowFloat>(owner);
        twin.EnergyCost.SetThisTurnOrUntilPlayed(0, reduceOnly: true);
        floating.EnergyCost.SetThisTurnOrUntilPlayed(0, reduceOnly: true);
        secondFloat.EnergyCost.SetThisTurnOrUntilPlayed(0, reduceOnly: true);
        await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
            twin, PileType.Hand, owner, CardPilePosition.Random);
        await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
            floating, PileType.Hand, owner, CardPilePosition.Random);
        await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
            secondFloat, PileType.Hand, owner, CardPilePosition.Random);
        await context.SignalAndWaitAsync("clow-twin-fixture-ready");

        var twinChecksumBaseline = context.ChecksumCount;
        if (context.LocalPlayer.NetId == owner.NetId)
            await context.PlayOwnedCardAsync(twin);
        await MultiplayerScenarioContext.WaitForStateAsync(
            () => owner.Creature.GetPower<ClassicTwinPower>()?.Amount == 1,
            "client-owned Twin to apply ClassicTwinPower(1)");
        await context.WaitForActionsAsync();
        await context.WaitForActionChecksumsAsync(
            twinChecksumBaseline,
            "client-owned Twin",
            nameof(PlayCardAction));
        await context.SignalAndWaitAsync("clow-twin-applied");

        var twinPower = owner.Creature.GetPower<ClassicTwinPower>()
            ?? throw new InvalidOperationException("ClassicTwinPower was not applied.");
        if (context.LocalPlayer.NetId == owner.NetId)
        {
            var queriedPlayCount = twinPower.ModifyCardPlayCount(floating, target: null, playCount: 1);
            assertions.Equal("client_preview_query_doubles_float", 2, queriedPlayCount);
        }
        await context.SignalAndWaitAsync("clow-twin-client-preview-probed");

        var floatChecksumBaseline = context.ChecksumCount;
        if (context.LocalPlayer.NetId == owner.NetId)
            await context.PlayOwnedCardAsync(floating);
        await context.SignalAndWaitAsync("clow-twin-float-requested");
        await context.WaitForActionsAsync();
        await context.WaitForActionChecksumsAsync(
            floatChecksumBaseline,
            "client-owned Float after an asymmetric play-count query",
            nameof(PlayCardAction));

        var floatAmount = owner.Creature.GetPower<ClassicFloatPower>()?.Amount ?? 0;
        assertions.Equal("float_executes_twice", 2, floatAmount);
        assertions.Equal("twin_consumed_once", 1, twinPower.CardsDoubledThisTurnCount);
        await context.SignalAndWaitAsync("clow-twin-first-float-verified");

        var secondFloatChecksumBaseline = context.ChecksumCount;
        if (context.LocalPlayer.NetId == owner.NetId)
            await context.PlayOwnedCardAsync(secondFloat);
        await context.SignalAndWaitAsync("clow-twin-second-float-requested");
        await context.WaitForActionsAsync();
        await context.WaitForActionChecksumsAsync(
            secondFloatChecksumBaseline,
            "second same-turn client-owned Float",
            nameof(PlayCardAction));
        assertions.Equal(
            "second_same_turn_float_executes_once",
            3,
            owner.Creature.GetPower<ClassicFloatPower>()?.Amount ?? 0);
        assertions.Equal("twin_same_turn_quota_stays_consumed", 1, twinPower.CardsDoubledThisTurnCount);

        foreach (var enemy in combat.Enemies.Where(static enemy => enemy.IsAlive))
            await CreatureCmd.Stun(enemy);
        var turnBeforeReset = owner.PlayerCombatState?.TurnNumber ?? 0;
        var turnChecksumBaseline = context.ChecksumCount;
        await context.SignalAndWaitAsync("clow-twin-turn-reset-prepared");
        await context.EndLocalTurnAsync();
        await context.SignalAndWaitAsync("clow-twin-turns-ended");
        await MultiplayerScenarioContext.WaitForStateAsync(
            () => context.OrderedPlayers.All(static player => player.PlayerCombatState?.Phase == PlayerTurnPhase.Play)
                && (owner.PlayerCombatState?.TurnNumber ?? 0) > turnBeforeReset,
            "next multiplayer player turn after Twin quota consumption");
        await context.WaitForActionsAsync();
        await context.WaitForActionChecksumsAsync(
            turnChecksumBaseline,
            "Twin per-turn reset",
            "After enemy turn end");
        assertions.Equal("twin_quota_resets_next_turn", 0, twinPower.CardsDoubledThisTurnCount);

        var thirdFloat = combat.CreateCard<ClowFloat>(owner);
        thirdFloat.EnergyCost.SetThisTurnOrUntilPlayed(0, reduceOnly: true);
        await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
            thirdFloat, PileType.Hand, owner, CardPilePosition.Random);
        await context.SignalAndWaitAsync("clow-twin-third-float-ready");
        var thirdFloatChecksumBaseline = context.ChecksumCount;
        if (context.LocalPlayer.NetId == owner.NetId)
            await context.PlayOwnedCardAsync(thirdFloat);
        await context.SignalAndWaitAsync("clow-twin-third-float-requested");
        await context.WaitForActionsAsync();
        await context.WaitForActionChecksumsAsync(
            thirdFloatChecksumBaseline,
            "next-turn client-owned Float",
            nameof(PlayCardAction));
        var finalFloatAmount = owner.Creature.GetPower<ClassicFloatPower>()?.Amount ?? 0;
        assertions.Equal("next_turn_float_executes_twice", 5, finalFloatAmount);
        assertions.Equal("twin_next_turn_consumed_once", 1, twinPower.CardsDoubledThisTurnCount);
        var finalMagicCharge = owner.Creature.GetPower<ClassicMagicChargePower>()?.Amount ?? 0;

        await context.SignalAndWaitAsync("clow-twin-float-verified");
        RuntimeTestHost.WriteCheckpoint(
            request,
            "clow_twin_play_count_verified",
            "An asymmetric client-side play-count query did not consume Twin or desynchronize Float.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["fixture"] = new
            {
                setup_mutations = new[]
                {
                    "Client-owned zero-cost ClowTwin and three ClowFloat cards",
                    "One extra ModifyCardPlayCount query on the owning client only"
                }
            },
            ["peer"] = new
            {
                role = request.Multiplayer!.Role,
                local_net_id = context.LocalPlayer.NetId,
                checksum_observations = context.ChecksumObservations.Select(static observation => new
                {
                    id = observation.Id,
                    context = observation.Context,
                    checksum = observation.Checksum
                }).ToArray()
            },
            ["comparison"] = new
            {
                versions = new { environment.GameVersion, environment.RitsuVersion, environment.SakuraVersion },
                divergence = false,
                owner_net_id = owner.NetId,
                twin_amount = twinPower.Amount,
                twin_consumed_this_turn = twinPower.CardsDoubledThisTurnCount,
                first_float_amount = floatAmount,
                final_float_amount = finalFloatAmount,
                final_magic_charge = finalMagicCharge
            }
        };
    }
}
