using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Relics;
using SakuraMod.TestProtocol;
using STS2RitsuLib.Networking.ManagedActions;

namespace SakuraMod.RuntimeTests;

internal static class SealedWandChargeMultiplayerScenario
{
    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        SakuraRuntimeEnvironment environment,
        RuntimeAssertionCollector assertions)
    {
        await using var context = await MultiplayerScenarioContext.StartAsync(request);
        var combat = await context.EnterWeakSlimesCombatAsync();
        var runState = SakuraRunHooks.ActiveRunState ?? context.Run;
        var players = runState.Players.OrderBy(static player => player.NetId).ToArray();
        var wands = players.ToDictionary(
            static player => player.NetId,
            static player => player.Relics.OfType<ClassicSealedWandRelic>().Single());
        var before = wands.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.ChargeAmount);

        assertions.Equal("fixture_player_count", 2, players.Length);
        assertions.Equal("fixture_player_ids", "1,2", string.Join(',', players.Select(static player => player.NetId)));
        assertions.True(
            "fixture_both_players_are_sakura",
            players.All(static player => player.Character is SakuraMod.SakuraModCode.Character.ClassicSakura));
        assertions.True(
            "fixture_each_player_owns_sealed_wand",
            wands.All(pair => pair.Value.Owner == context.Player(pair.Key)));
        await context.SignalAndWaitAsync("sealed-wand-fixture-ready");

        var enemies = combat.Enemies
            .Where(static enemy => enemy.IsAlive && !enemy.IsSecondaryEnemy)
            .Take(3)
            .ToArray();
        assertions.Equal("fixture_live_primary_enemy_count", 3, enemies.Length);
        var ordinaryEnemy = enemies[0];
        var sealEnemy = enemies[1];
        var finalEnemy = enemies[2];
        var finisher = combat.CreateCard<ClowSword>(context.ClientPlayer);
        var seal = combat.CreateCard<SpellSeal>(context.ClientPlayer);
        var finalizer = combat.CreateCard<ClowSand>(context.ClientPlayer);
        await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
            finisher,
            PileType.Hand,
            context.ClientPlayer,
            CardPilePosition.Random);
        await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
            seal,
            PileType.Hand,
            context.ClientPlayer,
            CardPilePosition.Random);
        await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
            finalizer,
            PileType.Hand,
            context.ClientPlayer,
            CardPilePosition.Random);
        await CreatureCmd.Damage(
            new ThrowingPlayerChoiceContext(),
            ordinaryEnemy,
            Math.Max(0, ordinaryEnemy.CurrentHp - 1),
            ValueProp.Unblockable | ValueProp.Unpowered,
            context.LocalPlayer.Creature,
            null);
        await CreatureCmd.Damage(
            new ThrowingPlayerChoiceContext(),
            sealEnemy,
            Math.Max(0, sealEnemy.CurrentHp - 14),
            ValueProp.Unblockable | ValueProp.Unpowered,
            context.LocalPlayer.Creature,
            null);
        await CreatureCmd.Damage(
            new ThrowingPlayerChoiceContext(),
            finalEnemy,
            Math.Max(0, finalEnemy.CurrentHp - 1),
            ValueProp.Unblockable | ValueProp.Unpowered,
            context.LocalPlayer.Creature,
            null);
        await context.SignalAndWaitAsync("sealed-wand-enemy-prepared");
        var ordinaryChecksumBaseline = context.ChecksumCount;
        if (context.LocalPlayer.NetId == context.ClientPlayer.NetId)
            await context.PlayOwnedCardAsync(finisher, ordinaryEnemy);
        await context.SignalAndWaitAsync("sealed-wand-ordinary-killed");
        await MultiplayerScenarioContext.WaitForStateAsync(
            () => !ordinaryEnemy.IsAlive,
            "ordinary shared enemy death to resolve");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions().WaitAsync(TimeSpan.FromSeconds(30));
        await context.WaitForActionChecksumsAsync(
            ordinaryChecksumBaseline,
            "ordinary Sealed Wand charge",
            nameof(PlayCardAction),
            nameof(RitsuLibManagedGameAction));

        var afterOrdinary = (SakuraRunHooks.ActiveRunState ?? context.Run).Players
            .ToDictionary(
                static player => player.NetId,
                static player => player.Relics.OfType<ClassicSealedWandRelic>().Single().ChargeAmount);
        foreach (var player in players)
        {
            assertions.Equal(
                $"sealed_wand_ordinary_delta_{player.NetId}",
                3,
                afterOrdinary[player.NetId] - before[player.NetId]);
        }

        await context.SignalAndWaitAsync("sealed-wand-ordinary-verified");
        var sealChecksumBaseline = context.ChecksumCount;
        if (context.LocalPlayer.NetId == context.ClientPlayer.NetId)
            await context.PlayOwnedCardAsync(seal, sealEnemy);
        await context.SignalAndWaitAsync("sealed-wand-seal-killed");
        await MultiplayerScenarioContext.WaitForStateAsync(
            () => !sealEnemy.IsAlive,
            "Seal shared enemy death to resolve");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions().WaitAsync(TimeSpan.FromSeconds(30));
        await context.WaitForActionChecksumsAsync(
            sealChecksumBaseline,
            "Seal Sealed Wand charge",
            nameof(PlayCardAction),
            nameof(RitsuLibManagedGameAction));

        var after = (SakuraRunHooks.ActiveRunState ?? context.Run).Players
            .ToDictionary(
                static player => player.NetId,
                static player => player.Relics.OfType<ClassicSealedWandRelic>().Single().ChargeAmount);
        foreach (var player in players)
        {
            assertions.Equal(
                $"sealed_wand_seal_delta_{player.NetId}",
                5,
                after[player.NetId] - afterOrdinary[player.NetId]);
        }

        await context.SignalAndWaitAsync("sealed-wand-final-enemy-prepared");
        var finalChecksumBaseline = context.ChecksumCount;
        var expectedFinal = after.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value + 3);
        if (context.LocalPlayer.NetId == context.ClientPlayer.NetId)
            await context.PlayOwnedCardAsync(finalizer, finalEnemy);
        await context.SignalAndWaitAsync("sealed-wand-final-enemy-killed");
        await MultiplayerScenarioContext.WaitForStateAsync(
            () => !finalEnemy.IsAlive,
            "final shared enemy death to resolve");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions().WaitAsync(TimeSpan.FromSeconds(30));
        await context.WaitForActionChecksumsAsync(
            finalChecksumBaseline,
            "final Sealed Wand charge",
            nameof(PlayCardAction));
        await MultiplayerScenarioContext.WaitForStateAsync(
            () => (SakuraRunHooks.ActiveRunState ?? context.Run).Players.All(player =>
                player.Relics.OfType<ClassicSealedWandRelic>().Single().ChargeAmount >= expectedFinal[player.NetId]),
            "final Sealed Wand charge application");

        var afterFinal = (SakuraRunHooks.ActiveRunState ?? context.Run).Players
            .ToDictionary(
                static player => player.NetId,
                static player => player.Relics.OfType<ClassicSealedWandRelic>().Single().ChargeAmount);
        foreach (var player in players)
        {
            assertions.Equal(
                $"sealed_wand_final_delta_{player.NetId}",
                3,
                afterFinal[player.NetId] - after[player.NetId]);
        }

        var digest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(string.Join(
                "|",
                players.Select(player =>
                    $"{player.NetId}:{before[player.NetId]}:{afterOrdinary[player.NetId]}:{after[player.NetId]}:{afterFinal[player.NetId]}")))));
        await context.SignalAndWaitAsync("sealed-wand-charge-verified");
        RuntimeTestHost.WriteCheckpoint(
            request,
            "sealed_wand_charge_verified",
            "Ordinary, Seal, and final shared enemy deaths were observed by both Sakura players' Sealed Wands.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["fixture"] = new
            {
                setup_mutations = new[] { "Two Sakura players", "Mirrored HP setup", "Client-owned ClowSword, SpellSeal, and ClowSand lethal actions" }
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
                final_digest = digest,
                divergence = false,
                charges = players.Select(player => new
                {
                    net_id = player.NetId,
                    before = before[player.NetId],
                    after = after[player.NetId],
                    after_final = afterFinal[player.NetId],
                    delta = afterFinal[player.NetId] - before[player.NetId]
                }).ToArray()
            }
        };
    }
}
