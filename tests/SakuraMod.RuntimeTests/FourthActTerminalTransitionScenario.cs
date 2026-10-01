using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using SakuraMod.SakuraModCode.FourthAct.Dark.Encounters;
using SakuraMod.SakuraModCode.FourthAct.Routing;
using SakuraMod.SakuraModCode.FourthAct.Wind.Encounters;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal static class FourthActTerminalTransitionScenario
{
    private static ActModel[] Acts() => [.. ActModel.GetDefaultList(), ModelDb.Act<SakuraFourthAct>()];

    public static async Task<Dictionary<string, object?>> ExecuteLiveAsync(
        SakuraTestRequest request, RuntimeAssertionCollector assertions)
    {
        var context = await CombatScenarioContext.StartAsync(request, acts: Acts());
        var run = context.Run;
        var routes = FourthActRouteCatalog.Resolve().CompleteRoutes;
        // Verify intermediate branches before ending this run. Restarting an act
        // during the Architect's asynchronous entrance races native fades.
        for (var routeIndex = 0; routeIndex < routes.Count; routeIndex++)
        {
            var route = routes[routeIndex];
            await RunManager.Instance.EnterAct(FourthActEntryRegistration.FourthActSlotIndex, doTransition: false);
            var map = (SakuraFourthActMap)run.Map;
            var boss = map.MerchantMapPoint.Children.Single(point => point.coord.col == routeIndex * 2)
                .Children.Single();
            await EnterAndWinAsync(run, context.Player, boss.coord, route.ElementalBoss!.EncounterType);
            await VerifyRewardReturnAndRestAsync(run, boss, assertions, $"fourth_act_{route.Element}");
            RuntimeTestHost.WriteCheckpoint(request, $"fourth_act_{route.Element}_intermediate_verified",
                $"{route.Element} boss rewards returned to the map and the rest-site character initialized.");
        }

        var endpointType = routes[^1].Endpoint.EncounterType!;
        await EnterAndWinAsync(run, context.Player, run.Map.BossMapPoint.coord, endpointType);
        await VerifyArchitectAsync(run, assertions);
        return new() { ["entry"] = "live_victory", ["elemental_bosses"] = routes.Count,
            ["endpoint"] = endpointType.Name };
    }

    public static Task<Dictionary<string, object?>> ExecuteFinishedCombatAsync(
        SakuraTestRequest request, RuntimeAssertionCollector assertions) => request.Phase switch
        {
            "write" => WriteFinishedBossAsync(request, assertions),
            "read" => ReadFinishedBossAsync(request, assertions),
            _ => throw new InvalidDataException("Finished-boss restoration requires write/read phases.")
        };

    private static async Task<Dictionary<string, object?>> WriteFinishedBossAsync(
        SakuraTestRequest request, RuntimeAssertionCollector assertions)
    {
        var context = await CombatScenarioContext.StartAsync(request, shouldSave: true, acts: Acts());
        await RunManager.Instance.EnterAct(FourthActEntryRegistration.FourthActSlotIndex, doTransition: false);
        var map = (SakuraFourthActMap)context.Run.Map;
        var boss = map.MerchantMapPoint.Children.Single(point => point.coord.col == 0).Children.Single();
        await EnterAndWinAsync(context.Run, context.Player, boss.coord, typeof(WindyEncounter));
        await CombatScenarioContext.WaitUntilAsync(() => NOverlayStack.Instance?.Peek() is NRewardsScreen,
            "Wind boss card rewards before save");
        var room = context.Run.CurrentRoom as CombatRoom
            ?? throw new InvalidOperationException("No finished Wind combat room to save.");
        assertions.True("finished_boss_write_pre_finished", room.IsPreFinished);
        await SaveManager.Instance.SaveRun(preFinishedRoom: room, saveProgress: false);
        var read = SaveManager.Instance.LoadRunSave();
        assertions.True("finished_boss_write_readable", read.Success && read.SaveData?.PreFinishedRoom is not null);
        var snapshot = new FinishedBossSnapshot(boss.coord.col, boss.coord.row, room.Encounter.Id.ToString());
        SakuraTestProtocol.WriteAtomic(request.PriorSnapshotPath!, snapshot);
        return new() { ["saved_boss"] = snapshot };
    }

    private static async Task<Dictionary<string, object?>> ReadFinishedBossAsync(
        SakuraTestRequest request, RuntimeAssertionCollector assertions)
    {
        var expected = SakuraTestProtocol.Read<FinishedBossSnapshot>(request.PriorSnapshotPath!);
        var read = SaveManager.Instance.LoadRunSave();
        var save = read.SaveData ?? throw new InvalidDataException($"Could not load finished Boss: {read.Status}.");
        var run = RunState.FromSerializable(save);
        await RunManager.Instance.SetUpSavedSingleplayer(run, save);
        var game = NGame.Instance!;
        game.ReactionContainer.InitializeNetworking(new NetSingleplayerGameService());
        await game.LoadRun(run, save.PreFinishedRoom);
        RunManager.Instance.CombatReplayWriter.IsEnabled = false;
        assertions.True("finished_boss_read_saved_map", run.Map is SavedActMap);
        assertions.True("finished_boss_read_room_mode", NCombatRoom.Instance?.Mode == CombatRoomMode.FinishedCombat);
        assertions.Equal("finished_boss_read_encounter", expected.EncounterId,
            (run.CurrentRoom as CombatRoom)?.Encounter.Id.ToString());
        var boss = run.Map.GetPoint(new MapCoord { col = expected.Col, row = expected.Row })
            ?? throw new InvalidOperationException("The saved elemental Boss coordinate is missing.");
        await VerifyRewardReturnAndRestAsync(run, boss, assertions, "fourth_act_restored_Wind");

        // Preserve the original rewardless FinishedCombat endpoint regression.
        if (!run.AddVisitedMapCoord(run.Map.BossMapPoint.coord))
            throw new InvalidOperationException("Could not mark the restored Dark endpoint as visited.");
        var endpoint = new CombatRoom(ModelDb.Encounter<DarkEncounter>().ToMutable(), run);
        endpoint.MarkPreFinished();
        await RunManager.Instance.LoadIntoLatestMapCoord(endpoint);
        await VerifyArchitectAsync(run, assertions);
        return new() { ["entry"] = "fresh_process_finished_combat", ["boss"] = expected,
            ["map_type"] = run.Map.GetType().Name, ["endpoint"] = nameof(DarkEncounter) };
    }

    private static async Task VerifyRewardReturnAndRestAsync(RunState run, MapPoint boss,
        RuntimeAssertionCollector assertions, string prefix)
    {
        await CombatScenarioContext.WaitUntilAsync(() => NOverlayStack.Instance?.Peek() is NRewardsScreen,
            $"{prefix} card reward screen");
        var screen = (NRewardsScreen)NOverlayStack.Instance!.Peek()!;
        var rewards = (RewardsSet)AccessTools.Field(typeof(NRewardsScreen), "_rewardsSet").GetValue(screen)!;
        assertions.Equal($"{prefix}_card_reward_count", 1, rewards.Rewards.OfType<CardReward>().Count());
        var proceed = screen.GetNode<NProceedButton>("ProceedButton");
        proceed.EmitSignal(NClickableControl.SignalName.Released, proceed);
        await CombatScenarioContext.WaitUntilAsync(
            () => NMapScreen.Instance?.Visible == true || run.CurrentRoom?.IsVictoryRoom == true,
            $"map after {prefix} rewards");
        assertions.True($"{prefix}_does_not_enter_architect", run.CurrentRoom?.IsVictoryRoom == false);
        assertions.True($"{prefix}_returns_to_map", NMapScreen.Instance?.Visible == true);
        assertions.Equal($"{prefix}_index_after_rewards", FourthActEntryRegistration.FourthActSlotIndex,
            run.CurrentActIndex);
        if (run.CurrentRoom?.IsVictoryRoom == true)
            throw new InvalidOperationException($"{prefix} rewards prematurely entered The Architect.");

        await NMapScreen.Instance!.TravelToMapCoord(boss.Children.Single().coord);
        var rest = NRestSiteRoom.Instance
            ?? throw new InvalidOperationException("The fourth-act rest site has no native room node.");
        // Native _Ready connects focus after its act animation switch; an
        // unsupported act index throws before the connection is made.
        var ready = rest.Characters.Single().Hitbox.GetSignalConnectionList(Control.SignalName.FocusEntered).Count > 0;
        assertions.True($"{prefix}_rest_character_ready", ready);
        assertions.True($"{prefix}_rest_options_available", rest.Options.Count > 0);
        assertions.Equal($"{prefix}_index_after_rest", FourthActEntryRegistration.FourthActSlotIndex,
            run.CurrentActIndex);
        if (!ready)
            throw new InvalidOperationException("Fourth-act rest-site character did not finish native initialization.");
    }

    private static async Task VerifyArchitectAsync(RunState run, RuntimeAssertionCollector assertions)
    {
        await CombatScenarioContext.WaitUntilAsync(() => run.CurrentRoom?.IsVictoryRoom == true,
            "Architect after final Light/Dark endpoint");
        assertions.True("fourth_act_terminal_entered_native_architect",
            run.CurrentRoom is EventRoom { CanonicalEvent: TheArchitect });
        assertions.Equal("fourth_act_terminal_index_preserved", FourthActEntryRegistration.FourthActSlotIndex,
            run.CurrentActIndex);
        assertions.True("fourth_act_terminal_left_combat", !CombatManager.Instance.IsInProgress);
    }

    private static async Task EnterAndWinAsync(RunState run, Player player, MapCoord coord, Type encounterType)
    {
        await RunManager.Instance.EnterMapCoord(coord);
        await CombatScenarioContext.WaitUntilAsync(
            () => CombatManager.Instance.IsInProgress && player.PlayerCombatState?.Phase == PlayerTurnPhase.Play,
            $"{encounterType.Name} opening hand");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        var combat = CombatManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException("The entered combat has no state.");
        if (combat.Encounter?.GetType() != encounterType)
            throw new InvalidOperationException($"Expected {encounterType.Name}, got {combat.Encounter?.GetType().Name}.");
        foreach (var enemy in combat.Enemies.Where(static enemy => enemy.IsAlive).ToList())
            await CreatureCmd.Kill(enemy);
        await CombatManager.Instance.CheckWinCondition();
    }

    private sealed record FinishedBossSnapshot(int Col, int Row, string EncounterId);
}
