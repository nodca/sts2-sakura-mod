using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.Relics;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal static class TurnEndDamageSyncMultiplayerScenario
{
    private const int SiegeBlock = 5;

    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        SakuraRuntimeEnvironment environment,
        RuntimeAssertionCollector assertions)
    {
        await using var context = await MultiplayerScenarioContext.StartAsync(request, shouldSave: true);
        var combat = await context.EnterWeakSlimesCombatAsync();
        var players = context.Run.Players
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
            if (!wand.ApplyDeathCharge(uint.MaxValue, threshold - 3))
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
            "After enemy turn end");
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
            if (!wand.ApplyDeathCharge(uint.MaxValue - 1, threshold))
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
            "After enemy turn end");
        await MultiplayerScenarioContext.WaitForStateAsync(
            () => players.All(static player => player.PlayerCombatState?.Phase == PlayerTurnPhase.Play)
                && context.ClientPlayer.Creature.GetPower<SiegePendingPower>() is null
                && survivor.CurrentHp == survivorHpBeforeSiege - SiegeBlock,
            "next multiplayer player turn after Siege");

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
        await PowerCmd.Apply<PoisonPower>(
            new ThrowingPlayerChoiceContext(),
            survivor,
            12,
            context.ClientPlayer.Creature,
            null);
        var expectedFinal = afterSiege.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value + 3);
        assertions.Equal("poison_fixture_hp", 1, survivor.CurrentHp);
        assertions.Equal("poison_fixture_amount", 12, survivor.GetPowerAmount<PoisonPower>());
        await context.SignalAndWaitAsync("turn-end-sync-final-poison-prepared");

        var poisonChecksumBaseline = context.ChecksumCount;
        await context.EndLocalTurnAsync();
        await context.SignalAndWaitAsync("turn-end-sync-final-poison-turns-ended");
        await MultiplayerScenarioContext.WaitForStateAsync(
            () => !survivor.IsAlive,
            "final Poison enemy death to resolve");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions().WaitAsync(TimeSpan.FromSeconds(30));
        await context.WaitForActionChecksumsAsync(
            poisonChecksumBaseline,
            "final Poison turn-start death",
            "After enemy turn start");
        await MultiplayerScenarioContext.WaitForStateAsync(
            () => context.Run.Players.All(player =>
                player.Relics.OfType<ClassicSealedWandRelic>().Single().ChargeAmount
                >= expectedFinal[player.NetId]),
            "final Poison Sealed Wand charge application");

        var afterFinalPoison = Charges(context);
        foreach (var player in players)
        {
            assertions.Equal(
                $"sealed_wand_final_poison_delta_{player.NetId}",
                3,
                afterFinalPoison[player.NetId] - afterSiege[player.NetId]);
        }
        assertions.True("final_poison_ended_combat", !CombatManager.Instance.IsInProgress);
        assertions.True(
            "sealed_wand_used_no_managed_action",
            context.ChecksumObservations.All(static observation =>
                !observation.Context.Contains("RitsuLibManagedGameAction", StringComparison.Ordinal)));
        await context.SignalAndWaitAsync("turn-end-sync-final-poison-verified");
        context.ThrowIfNetworkFailed();

        if (context.IsHost)
        {
            await SaveManager.Instance.SaveRun(preFinishedRoom: null, saveProgress: false);
            var read = SaveManager.Instance.LoadAndCanonicalizeMultiplayerRunSave(context.LocalPlayer.NetId);
            assertions.True("final_poison_save_readable", read.Success && read.SaveData is not null);
            var savedRun = RunState.FromSerializable(
                read.SaveData ?? throw new InvalidDataException("Final Poison multiplayer save contained no run data."));
            var savedCharges = savedRun.Players.ToDictionary(
                static player => player.NetId,
                static player => player.Relics.OfType<ClassicSealedWandRelic>().Single().ChargeAmount);
            foreach (var player in players)
            {
                assertions.Equal(
                    $"final_poison_saved_charge_{player.NetId}",
                    afterFinalPoison[player.NetId],
                    savedCharges[player.NetId]);
            }
        }
        await context.SignalAndWaitAsync("turn-end-sync-final-poison-save-verified");

        var digest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(string.Join(
                "|",
                players.Select(player =>
                    $"{player.NetId}:{before[player.NetId]}:{afterErase[player.NetId]}:{afterSiege[player.NetId]}:{afterFinalPoison[player.NetId]}")))));
        await context.SignalAndWaitAsync("turn-end-damage-sync-verified");
        RuntimeTestHost.WriteCheckpoint(
            request,
            "turn_end_damage_sync_verified",
            "Non-final Sakura Erase/Siege deaths and a final vanilla Poison death remained synchronized and charged both Sealed Wands exactly once.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["fixture"] = new
            {
                setup_mutations = new[]
                {
                    "Two Sakura players",
                    "Three stunned primary enemies with mirrored HP",
                    "Mirrored Sakura Erase, Siege pending, and vanilla Poison Powers"
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
                    after_final_poison = afterFinalPoison[player.NetId]
                }).ToArray()
            }
        };
    }

    private static Dictionary<ulong, int> Charges(MultiplayerScenarioContext context) =>
        context.Run.Players.ToDictionary(
            static player => player.NetId,
            static player => player.Relics.OfType<ClassicSealedWandRelic>().Single().ChargeAmount);
}
