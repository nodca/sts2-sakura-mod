using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using STS2RitsuLib;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Relics;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal static class SaveLoadRestorationScenario
{
    private static readonly TimeSpan LifecycleTimeout = TimeSpan.FromSeconds(20);

    public static Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions) =>
        request.Phase switch
        {
            "write" => ExecuteWriteAsync(request, assertions),
            "read" => ExecuteReadAsync(request, assertions),
            _ => throw new InvalidDataException(
                $"Save/load scenario phase must be 'write' or 'read', found '{request.Phase}'.")
        };

    private static async Task<Dictionary<string, object?>> ExecuteWriteAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions)
    {
        var snapshotPath = RequireSnapshotPath(request);
        var context = await CombatScenarioContext.StartAsync(request, shouldSave: true);
        var player = context.Player;
        var create = context.Run.CreateCard<ClowCreate>(player);
        await CardPileCmd.Add(
            create,
            PileType.Deck,
            CardPilePosition.Bottom,
            clonedBy: null,
            skipVisuals: true);
        var moonBell = await RelicCmd.Obtain<ClassicMoonBellRelic>(player);
        await context.EnterWeakCrawlerCombatAsync();

        assertions.Equal("create_reduced_by_combat_start", 4, create.EnergyCost.GetResolved());
        var wand = player.Relics.OfType<ClassicSealedWandRelic>().Single();
        var fixtureAction = new RuntimeFixtureAction(
            player,
            async _ =>
            {
                wand.AddReturnRecharge();
                await moonBell.AfterPreventingDeath(player.Creature);
            });
        await CombatScenarioContext.EnqueueAndWaitAsync(fixtureAction);

        var snapshot = Capture(player);
        assertions.True("wand_charge_before_save", snapshot.WandCharge > 0);
        assertions.Equal("create_cost_before_save", 4, snapshot.CreateCost);
        assertions.Equal("moon_bell_status_before_save", "Disabled", snapshot.MoonBellStatus);
        assertions.True("moon_bell_used_before_save", snapshot.MoonBellShouldDie);
        SakuraTestProtocol.WriteAtomic(snapshotPath, snapshot);

        var savedEventSource = new TaskCompletionSource<RunSavedEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = RitsuLibFramework.SubscribeLifecycle<RunSavedEvent>(
            evt => savedEventSource.TrySetResult(evt),
            replayCurrentState: false);
        await SaveManager.Instance.SaveRun(preFinishedRoom: null, saveProgress: false);
        var savedEvent = await savedEventSource.Task.WaitAsync(LifecycleTimeout);
        var readable = SaveManager.Instance.LoadRunSave();
        assertions.True("run_saved_event", ReferenceEquals(savedEvent.SaveManager, SaveManager.Instance));
        assertions.Equal("run_saved_without_progress", false, savedEvent.SaveProgress);
        assertions.True("saved_run_readable_in_write_process", readable.Success && readable.SaveData is not null);
        RuntimeTestHost.WriteCheckpoint(
            request,
            "save_write_verified",
            "Generated state was saved, RunSavedEvent fired, and the save was readable before process exit.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["fixture"] = new
            {
                seed = request.Seed.ToString("X16"),
                saved_owners = new[]
                {
                    typeof(ClassicSealedWandRelic).FullName,
                    typeof(ClowCreate).FullName,
                    typeof(ClassicMoonBellRelic).FullName
                },
                setup_mutations = new[]
                {
                    "RunState.CreateCard<ClowCreate> -> deck",
                    "RelicCmd.Obtain<ClassicMoonBellRelic>",
                    "CombatStartingEvent -> ClowCreate.ReduceCostAtCombatStart",
                    "RuntimeFixtureAction -> ClassicSealedWandRelic.AddReturnRecharge",
                    "RuntimeFixtureAction -> ClassicMoonBellRelic.AfterPreventingDeath"
                }
            },
            ["before_save"] = snapshot,
            ["save_read_status"] = readable.Status.ToString(),
            ["snapshot_path"] = snapshotPath
        };
    }

    private static async Task<Dictionary<string, object?>> ExecuteReadAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions)
    {
        var snapshotPath = RequireSnapshotPath(request);
        var expected = SakuraTestProtocol.Read<SaveRestorationSnapshot>(snapshotPath);
        var read = SaveManager.Instance.LoadRunSave();
        assertions.True("load_run_save_success", read.Success);
        assertions.Equal("load_run_save_status", ReadSaveStatus.Success, read.Status);
        var save = read.SaveData
            ?? throw new InvalidDataException(
                $"LoadRunSave returned no data: {read.Status} {read.ErrorMessage}");
        var run = RunState.FromSerializable(save);

        var loadedEventSource = new TaskCompletionSource<RunLoadedEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = RitsuLibFramework.SubscribeLifecycle<RunLoadedEvent>(
            evt => loadedEventSource.TrySetResult(evt),
            replayCurrentState: false);
        await RunManager.Instance.SetUpSavedSingleplayer(run, save);
        var loadedEvent = await loadedEventSource.Task.WaitAsync(LifecycleTimeout);
        var game = NGame.Instance
            ?? throw new InvalidOperationException("NGame is unavailable while loading the saved run.");
        game.ReactionContainer.InitializeNetworking(new NetSingleplayerGameService());
        await game.LoadRun(run, save.PreFinishedRoom);
        RunManager.Instance.CombatReplayWriter.IsEnabled = false;

        var player = run.Players.Single();
        var actual = Capture(player);
        assertions.True("run_loaded_event", ReferenceEquals(loadedEvent.RunState, run));
        assertions.Equal("run_loaded_singleplayer", false, loadedEvent.IsMultiplayer);
        assertions.Equal("restored_wand_charge", expected.WandCharge, actual.WandCharge);
        assertions.Equal("restored_create_cost", expected.CreateCost, actual.CreateCost);
        assertions.Equal("restored_moon_bell_status", expected.MoonBellStatus, actual.MoonBellStatus);
        assertions.Equal("restored_moon_bell_used", expected.MoonBellShouldDie, actual.MoonBellShouldDie);
        assertions.Equal("restored_owner_id", expected.OwnerNetId, actual.OwnerNetId);
        assertions.Equal("restored_character_id", expected.CharacterId, actual.CharacterId);
        assertions.True(
            "restored_relic_ownership",
            player.Relics.OfType<ClassicSealedWandRelic>().Single().Owner == player
            && player.Relics.OfType<ClassicMoonBellRelic>().Single().Owner == player);
        assertions.True(
            "restored_card_ownership",
            ReferenceEquals(player.Deck.Cards.OfType<ClowCreate>().Single().Owner, player));
        RuntimeTestHost.WriteCheckpoint(
            request,
            "save_read_verified",
            "Fresh process loaded the generated save and matched all saved Sakura state.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["expected"] = expected,
            ["restored"] = actual,
            ["load_status"] = read.Status.ToString(),
            ["loaded_room"] = run.CurrentRoom?.RoomType.ToString(),
            ["snapshot_path"] = snapshotPath
        };
    }

    private static SaveRestorationSnapshot Capture(MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        var wand = player.Relics.OfType<ClassicSealedWandRelic>().Single();
        var moonBell = player.Relics.OfType<ClassicMoonBellRelic>().Single();
        var create = player.Deck.Cards.OfType<ClowCreate>().Single();
        return new SaveRestorationSnapshot(
            wand.ChargeAmount,
            create.EnergyCost.GetResolved(),
            moonBell.Status.ToString(),
            moonBell.ShouldDie(player.Creature),
            moonBell.CustomIconPath,
            player.NetId,
            player.Character.Id.ToString());
    }

    private static string RequireSnapshotPath(SakuraTestRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PriorSnapshotPath)
            || !Path.IsPathFullyQualified(request.PriorSnapshotPath))
        {
            throw new InvalidDataException(
                "Save/load scenario requires an absolute snapshot path.");
        }

        return request.PriorSnapshotPath;
    }
}

internal sealed record SaveRestorationSnapshot(
    int WandCharge,
    int CreateCost,
    string MoonBellStatus,
    bool MoonBellShouldDie,
    string MoonBellIconPath,
    ulong OwnerNetId,
    string CharacterId);
