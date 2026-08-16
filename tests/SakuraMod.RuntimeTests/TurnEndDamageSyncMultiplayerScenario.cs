using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.Relics;
using SakuraMod.TestProtocol;
using STS2RitsuLib.Networking.ManagedActions;

namespace SakuraMod.RuntimeTests;

internal static class TurnEndDamageSyncMultiplayerScenario
{
    private const int SiegeBlock = 5;

    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        SakuraRuntimeEnvironment environment,
        RuntimeAssertionCollector assertions)
    {
        await using var context = await MultiplayerScenarioContext.StartAsync(request);
        var combat = await context.EnterWeakSlimesCombatAsync();
        var players = (SakuraRunHooks.ActiveRunState ?? context.Run).Players
            .OrderBy(static player => player.NetId)
            .ToArray();
        var enemies = combat.Enemies
            .Where(static enemy => enemy.IsAlive && !enemy.IsSecondaryEnemy)
            .Take(3)
            .ToArray();
        var eraseEnemy = enemies[0];
        var siegeEnemy = enemies[1];
        var survivor = enemies[2];
        var turnsBeforeErase = players.ToDictionary(
            static player => player.NetId,
            static player => player.PlayerCombatState?.Hand.Cards.Count(static card => card is SpellTurn) ?? 0);
        foreach (var player in players)
        {
            var wand = player.Relics.OfType<ClassicSealedWandRelic>().Single();
            var threshold = ClassicSealedWandRelic.TriggerThresholdFor(
                SakuraSourceCardRules.ConvertedSakuraCount(player));
            if (!wand.ApplySynchronizedCharge(uint.MaxValue, threshold - 3))
                throw new InvalidOperationException($"Could not prepare Sealed Wand threshold for player {player.NetId}.");
        }
        var before = Charges(context);

        assertions.Equal("fixture_player_count", 2, players.Length);
        assertions.Equal("fixture_primary_enemy_count", 3, enemies.Length);

        await CreatureCmd.SetMaxAndCurrentHp(eraseEnemy, 1);
        await CreatureCmd.SetMaxAndCurrentHp(siegeEnemy, 1);
        await CreatureCmd.SetMaxAndCurrentHp(survivor, 100);
        foreach (var enemy in enemies)
            await CreatureCmd.Stun(enemy);
        await PowerCmd.Apply<SakuraErasePower>(
            new ThrowingPlayerChoiceContext(),
            eraseEnemy,
            33,
            context.ClientPlayer.Creature,
            null);
        assertions.Equal("erase_fixture_hp", 1, eraseEnemy.CurrentHp);
        assertions.Equal("erase_fixture_amount", 33, eraseEnemy.GetPowerAmount<SakuraErasePower>());
        await context.SignalAndWaitAsync("turn-end-sync-erase-prepared");

        var eraseChecksumBaseline = context.ChecksumCount;
        await context.EndLocalTurnAsync();
        await context.SignalAndWaitAsync("turn-end-sync-erase-turns-ended");
        await MultiplayerScenarioContext.WaitForStateAsync(
            () => !eraseEnemy.IsAlive,
            "Sakura Erase enemy death to resolve");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions().WaitAsync(TimeSpan.FromSeconds(30));
        await context.WaitForActionChecksumsAsync(
            eraseChecksumBaseline,
            "Sakura Erase turn-end death",
            "After enemy turn end",
            nameof(RitsuLibManagedGameAction));
        await MultiplayerScenarioContext.WaitForStateAsync(
            () => players.All(static player => player.PlayerCombatState?.Phase == PlayerTurnPhase.Play),
            "next multiplayer player turn after Sakura Erase");

        var afterErase = Charges(context);
        foreach (var player in players)
        {
            assertions.Equal(
                $"sealed_wand_erase_threshold_spent_{player.NetId}",
                0,
                afterErase[player.NetId]);
            assertions.Equal(
                $"sealed_wand_erase_generated_turn_{player.NetId}",
                turnsBeforeErase[player.NetId] + 1,
                player.PlayerCombatState?.Hand.Cards.Count(static card => card is SpellTurn) ?? 0);
        }
        assertions.True("erase_left_combat_running", CombatManager.Instance.IsInProgress);
        assertions.True("erase_left_primary_enemies", siegeEnemy.IsAlive && survivor.IsAlive);
        context.ThrowIfNetworkFailed();
        await context.SignalAndWaitAsync("turn-end-sync-erase-verified");

        var turnsBeforeSiege = players.ToDictionary(
            static player => player.NetId,
            static player => player.PlayerCombatState?.Hand.Cards.Count(static card => card is SpellTurn) ?? 0);
        foreach (var player in players)
        {
            var wand = player.Relics.OfType<ClassicSealedWandRelic>().Single();
            var threshold = ClassicSealedWandRelic.TriggerThresholdFor(
                SakuraSourceCardRules.ConvertedSakuraCount(player));
            if (!wand.ApplySynchronizedCharge(uint.MaxValue - 1, threshold))
                throw new InvalidOperationException($"Could not prepare Sealed Wand duplicate guard for player {player.NetId}.");
        }
        foreach (var enemy in new[] { siegeEnemy, survivor })
            await CreatureCmd.Stun(enemy);
        if (context.ClientPlayer.Creature.Block > 0)
            await CreatureCmd.LoseBlock(context.ClientPlayer.Creature, context.ClientPlayer.Creature.Block);
        await CreatureCmd.GainBlock(
            context.ClientPlayer.Creature,
            SiegeBlock,
            ValueProp.Unpowered,
            cardPlay: null,
            fast: false);
        var pending = await PowerCmd.Apply<SiegePendingPower>(
            new ThrowingPlayerChoiceContext(),
            context.ClientPlayer.Creature,
            1,
            context.ClientPlayer.Creature,
            null)
            ?? throw new InvalidOperationException("Siege pending Power was not applied.");
        pending.QueueEffect(extraEffect: true);
        var survivorHpBeforeSiege = survivor.CurrentHp;
        assertions.Equal("siege_fixture_target_hp", 1, siegeEnemy.CurrentHp);
        assertions.Equal("siege_fixture_block", SiegeBlock, context.ClientPlayer.Creature.Block);
        assertions.Equal("siege_fixture_pending", 1, pending.Amount);
        await context.SignalAndWaitAsync("turn-end-sync-siege-prepared");

        var siegeChecksumBaseline = context.ChecksumCount;
        await context.EndLocalTurnAsync();
        await context.SignalAndWaitAsync("turn-end-sync-siege-turns-ended");
        await MultiplayerScenarioContext.WaitForStateAsync(
            () => !siegeEnemy.IsAlive,
            "Siege enemy death to resolve");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions().WaitAsync(TimeSpan.FromSeconds(30));
        await context.WaitForActionChecksumsAsync(
            siegeChecksumBaseline,
            "Siege turn-end death",
            "After enemy turn end",
            nameof(RitsuLibManagedGameAction));

        var afterSiege = Charges(context);
        foreach (var player in players)
        {
            assertions.Equal(
                $"sealed_wand_siege_remaining_charge_{player.NetId}",
                3,
                afterSiege[player.NetId]);
            assertions.Equal(
                $"sealed_wand_siege_generated_one_turn_{player.NetId}",
                turnsBeforeSiege[player.NetId] + 1,
                player.PlayerCombatState?.Hand.Cards.Count(static card => card is SpellTurn) ?? 0);
        }
        assertions.Equal("siege_survivor_weak", 1, survivor.GetPowerAmount<WeakPower>());
        assertions.Equal("siege_survivor_damage", SiegeBlock, survivorHpBeforeSiege - survivor.CurrentHp);
        assertions.Equal(
            "siege_pending_removed",
            null,
            context.ClientPlayer.Creature.GetPower<SiegePendingPower>());
        assertions.True("siege_left_combat_running", CombatManager.Instance.IsInProgress && survivor.IsAlive);
        context.ThrowIfNetworkFailed();

        await CreatureCmd.SetMaxAndCurrentHp(survivor, 1);
        await CreatureCmd.Stun(survivor);
        await PowerCmd.Apply<SakuraErasePower>(
            new ThrowingPlayerChoiceContext(),
            survivor,
            33,
            context.ClientPlayer.Creature,
            null);
        var expectedFinal = afterSiege.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value + 3);
        await context.SignalAndWaitAsync("turn-end-sync-final-erase-prepared");

        await context.EndLocalTurnAsync();
        await context.SignalAndWaitAsync("turn-end-sync-final-erase-turns-ended");
        await MultiplayerScenarioContext.WaitForStateAsync(
            () => !survivor.IsAlive,
            "final Sakura Erase enemy death to resolve");
        await MultiplayerScenarioContext.WaitForStateAsync(
            () => (SakuraRunHooks.ActiveRunState ?? context.Run).Players.All(player =>
                player.Relics.OfType<ClassicSealedWandRelic>().Single().ChargeAmount
                >= expectedFinal[player.NetId]),
            "final off-action Sealed Wand charge application");

        var afterFinalErase = Charges(context);
        foreach (var player in players)
        {
            assertions.Equal(
                $"sealed_wand_final_erase_delta_{player.NetId}",
                3,
                afterFinalErase[player.NetId] - afterSiege[player.NetId]);
        }
        assertions.True("final_erase_ended_combat", !CombatManager.Instance.IsInProgress);
        await context.SignalAndWaitAsync("turn-end-sync-final-erase-verified");
        context.ThrowIfNetworkFailed();

        var digest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(string.Join(
                "|",
                players.Select(player =>
                    $"{player.NetId}:{before[player.NetId]}:{afterErase[player.NetId]}:{afterSiege[player.NetId]}:{afterFinalErase[player.NetId]}")))));
        await context.SignalAndWaitAsync("turn-end-damage-sync-verified");
        RuntimeTestHost.WriteCheckpoint(
            request,
            "turn_end_damage_sync_verified",
            "Non-final Sakura Erase/Siege deaths and a final off-action Erase death remained synchronized and charged both Sealed Wands exactly once.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["fixture"] = new
            {
                setup_mutations = new[]
                {
                    "Two Sakura players",
                    "Three stunned primary enemies with mirrored HP",
                    "Mirrored Sakura Erase and Siege pending Powers"
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
                final_digest = digest,
                divergence = false,
                erase_enemy_hp = eraseEnemy.CurrentHp,
                siege_enemy_hp = siegeEnemy.CurrentHp,
                survivor_hp = survivor.CurrentHp,
                survivor_weak = survivor.GetPowerAmount<WeakPower>(),
                charges = players.Select(player => new
                {
                    net_id = player.NetId,
                    before = before[player.NetId],
                    after_erase = afterErase[player.NetId],
                    after_siege = afterSiege[player.NetId],
                    after_final_erase = afterFinalErase[player.NetId]
                }).ToArray()
            }
        };
    }

    private static Dictionary<ulong, int> Charges(MultiplayerScenarioContext context) =>
        (SakuraRunHooks.ActiveRunState ?? context.Run).Players.ToDictionary(
            static player => player.NetId,
            static player => player.Relics.OfType<ClassicSealedWandRelic>().Single().ChargeAmount);
}
