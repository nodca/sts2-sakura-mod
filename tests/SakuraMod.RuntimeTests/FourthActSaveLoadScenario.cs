using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using SakuraMod.SakuraModCode.FourthAct.Routing;
using SakuraMod.SakuraModCode.FourthAct.Wind.Encounters;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal static class FourthActSaveLoadScenario
{
    public static Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions) =>
        request.Phase switch
        {
            "write" => ExecuteWriteAsync(request, assertions),
            "read" => ExecuteReadAsync(request, assertions),
            _ => throw new InvalidDataException(
                $"Fourth-act save/load phase must be 'write' or 'read', found '{request.Phase}'.")
        };

    private static async Task<Dictionary<string, object?>> ExecuteWriteAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions)
    {
        var snapshotPath = RequireSnapshotPath(request);
        ActModel[] acts = [.. ActModel.GetDefaultList(), ModelDb.Act<SakuraFourthAct>()];
        var context = await CombatScenarioContext.StartAsync(
            request,
            shouldSave: true,
            acts: acts);
        var run = context.Run;

        await RunManager.Instance.EnterAct(
            FourthActEntryRegistration.FourthActSlotIndex,
            doTransition: false);
        assertions.Equal(
            "fourth_act_write_index",
            FourthActEntryRegistration.FourthActSlotIndex,
            run.CurrentActIndex);
        assertions.True("fourth_act_write_identity", run.Act is SakuraFourthAct);
        assertions.True("fourth_act_write_map_type", run.Map is SakuraFourthActMap);

        var map = (SakuraFourthActMap)run.Map;
        await RunManager.Instance.EnterMapCoord(map.MerchantMapPoint.coord);
        await CombatScenarioContext.WaitUntilAsync(
            () => run.CurrentRoom?.RoomType == RoomType.Shop,
            "fourth-act merchant room");

        var elite = AssertSingle(map.MerchantMapPoint.Children, "fourth-act Wind elite route");
        await RunManager.Instance.EnterMapCoord(elite.coord);
        await CombatScenarioContext.WaitUntilAsync(
            () => CombatManager.Instance.IsInProgress
                && context.Player.PlayerCombatState?.Phase == PlayerTurnPhase.Play
                && run.CurrentRoom?.RoomType == RoomType.Elite,
            "fourth-act Wind elite opening hand");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        var combat = CombatManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException("Fourth-act elite combat has no state after startup.");
        var playerCombat = context.Player.PlayerCombatState
            ?? throw new InvalidOperationException("Fourth-act elite combat has no player state.");
        assertions.True(
            "fourth_act_write_wind_encounter",
            combat.Encounter is FlyEncounter or IllusionEncounter,
            combat.Encounter?.GetType().FullName ?? "No fourth-act encounter.");
        assertions.Equal(
            "fourth_act_write_opening_hand",
            CombatManager.baseHandDrawCount,
            playerCombat.Hand.Cards.Count);
        await SaveManager.Instance.SaveRun(preFinishedRoom: null, saveProgress: false);

        var readable = SaveManager.Instance.LoadRunSave();
        assertions.True(
            "fourth_act_write_save_readable",
            readable.Success && readable.SaveData is not null,
            $"{readable.Status}: {readable.ErrorMessage}");
        var save = readable.SaveData
            ?? throw new InvalidDataException("Fourth-act write process loaded no save data.");
        assertions.Equal(
            "fourth_act_write_saved_index",
            FourthActEntryRegistration.FourthActSlotIndex,
            save.CurrentActIndex);
        assertions.True(
            "fourth_act_write_empty_normal_list_was_omitted",
            save.Acts[FourthActEntryRegistration.FourthActSlotIndex]
                .SerializableRooms.NormalEncounterIds is null);

        var restoredInWriter = RunState.FromSerializable(save);
        assertions.True(
            "fourth_act_write_native_deserialization",
            restoredInWriter.Act is SakuraFourthAct);

        var snapshot = new FourthActSaveSnapshot(
            run.Act.Id.ToString(),
            run.CurrentActIndex,
            map.MerchantMapPoint.coord.col,
            map.MerchantMapPoint.coord.row,
            elite.coord.col,
            elite.coord.row,
            run.VisitedMapCoords.Count,
            map.MerchantMapPoint.Children.Count);
        SakuraTestProtocol.WriteAtomic(snapshotPath, snapshot);
        RuntimeTestHost.WriteCheckpoint(
            request,
            "fourth_act_save_write_verified",
            "A fresh isolated fourth-act Wind elite drew its opening hand, saved in combat, and deserialized in-process.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["saved"] = snapshot,
            ["save_status"] = readable.Status.ToString(),
            ["normal_encounter_ids_omitted"] = true,
            ["snapshot_path"] = snapshotPath
        };
    }

    private static async Task<Dictionary<string, object?>> ExecuteReadAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions)
    {
        var snapshotPath = RequireSnapshotPath(request);
        var expected = SakuraTestProtocol.Read<FourthActSaveSnapshot>(snapshotPath);
        var read = SaveManager.Instance.LoadRunSave();
        assertions.True("fourth_act_read_save_success", read.Success);
        assertions.Equal("fourth_act_read_save_status", ReadSaveStatus.Success, read.Status);
        var save = read.SaveData
            ?? throw new InvalidDataException(
                $"LoadRunSave returned no fourth-act data: {read.Status} {read.ErrorMessage}");
        var run = RunState.FromSerializable(save);

        await RunManager.Instance.SetUpSavedSingleplayer(run, save);
        var game = NGame.Instance
            ?? throw new InvalidOperationException("NGame is unavailable while loading the fourth-act save.");
        game.ReactionContainer.InitializeNetworking(new NetSingleplayerGameService());
        await game.LoadRun(run, save.PreFinishedRoom);
        RunManager.Instance.CombatReplayWriter.IsEnabled = false;
        await CombatScenarioContext.WaitUntilAsync(
            () => CombatManager.Instance.IsInProgress
                && run.Players.Single().PlayerCombatState?.Phase == PlayerTurnPhase.Play
                && run.CurrentRoom?.RoomType == RoomType.Elite,
            "restored fourth-act Wind elite opening hand");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();

        var merchantCoord = new MapCoord(expected.MerchantCol, expected.MerchantRow);
        var eliteCoord = new MapCoord(expected.EliteCol, expected.EliteRow);
        var merchant = run.Map.GetPoint(merchantCoord);
        var restoredCombat = CombatManager.Instance.DebugOnlyGetState();
        var restoredPlayerCombat = run.Players.Single().PlayerCombatState;
        assertions.Equal("fourth_act_read_index", expected.CurrentActIndex, run.CurrentActIndex);
        assertions.Equal("fourth_act_read_identity", expected.ActId, run.Act.Id.ToString());
        assertions.True("fourth_act_read_model_type", run.Act is SakuraFourthAct);
        assertions.True("fourth_act_read_saved_map_type", run.Map is SavedActMap);
        assertions.Equal("fourth_act_read_visited_count", expected.VisitedCount, run.VisitedMapCoords.Count);
        assertions.True("fourth_act_read_merchant_visited", run.VisitedMapCoords.Contains(merchantCoord));
        assertions.True("fourth_act_read_elite_visited", run.VisitedMapCoords.Contains(eliteCoord));
        assertions.True("fourth_act_read_merchant_present", merchant is not null);
        assertions.Equal("fourth_act_read_merchant_type", MapPointType.Shop, merchant?.PointType);
        assertions.Equal(
            "fourth_act_read_route_count",
            expected.RouteCount,
            merchant?.Children.Count ?? -1);
        assertions.Equal("fourth_act_read_room", RoomType.Elite, run.CurrentRoom?.RoomType);
        assertions.True(
            "fourth_act_read_wind_encounter",
            restoredCombat?.Encounter is FlyEncounter or IllusionEncounter,
            restoredCombat?.Encounter?.GetType().FullName ?? "No restored combat state.");
        assertions.Equal(
            "fourth_act_read_opening_hand",
            CombatManager.baseHandDrawCount,
            restoredPlayerCombat?.Hand.Cards.Count ?? -1);
        assertions.True(
            "fourth_act_read_normal_list_normalized",
            save.Acts[FourthActEntryRegistration.FourthActSlotIndex]
                .SerializableRooms.NormalEncounterIds is not null);
        RuntimeTestHost.WriteCheckpoint(
            request,
            "fourth_act_save_read_verified",
            "A fresh process restored fourth-act identity, map topology, Wind elite combat, and its opening hand.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["expected"] = expected,
            ["restored"] = new
            {
                act_id = run.Act.Id.ToString(),
                act_index = run.CurrentActIndex,
                map_type = run.Map.GetType().FullName,
                room_type = run.CurrentRoom?.RoomType.ToString(),
                visited = run.VisitedMapCoords.Select(static coord => new { coord.col, coord.row }).ToArray(),
                route_count = merchant?.Children.Count
            },
            ["load_status"] = read.Status.ToString(),
            ["snapshot_path"] = snapshotPath
        };
    }

    private static string RequireSnapshotPath(SakuraTestRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PriorSnapshotPath)
            || !Path.IsPathFullyQualified(request.PriorSnapshotPath))
        {
            throw new InvalidDataException(
                "Fourth-act save/load requires an absolute snapshot path.");
        }

        return request.PriorSnapshotPath;
    }

    private static T AssertSingle<T>(IReadOnlyCollection<T> values, string description) =>
        values.Count == 1
            ? values.Single()
            : throw new InvalidOperationException(
                $"Expected one {description}, found {values.Count}.");
}

internal sealed record FourthActSaveSnapshot(
    string ActId,
    int CurrentActIndex,
    int MerchantCol,
    int MerchantRow,
    int EliteCol,
    int EliteRow,
    int VisitedCount,
    int RouteCount);
