using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using SakuraMod.SakuraModCode.FourthAct.Dark.Encounters;
using SakuraMod.SakuraModCode.FourthAct.Routing;
using SakuraMod.SakuraModCode.FourthAct.Wind.Encounters;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal static class FourthActTerminalTransitionScenario
{
    public static Task<Dictionary<string, object?>> ExecuteLiveAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions) =>
        ExecuteAsync(request, assertions, restoreFinishedCombat: false);

    public static Task<Dictionary<string, object?>> ExecuteFinishedCombatAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions) =>
        ExecuteAsync(request, assertions, restoreFinishedCombat: true);

    private static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions,
        bool restoreFinishedCombat)
    {
        ActModel[] acts = [.. ActModel.GetDefaultList(), ModelDb.Act<SakuraFourthAct>()];
        var context = await CombatScenarioContext.StartAsync(request, acts: acts);
        var run = context.Run;

        await RunManager.Instance.EnterAct(
            FourthActEntryRegistration.FourthActSlotIndex,
            doTransition: false);
        var map = run.Map as SakuraFourthActMap
            ?? throw new InvalidOperationException(
                $"Expected SakuraFourthActMap, found {run.Map.GetType().FullName}.");

        assertions.True(
            "fourth_act_terminal_dark_is_rewardless",
            !ModelDb.Encounter<DarkEncounter>().ShouldGiveRewards);
        if (restoreFinishedCombat)
        {
            if (!run.AddVisitedMapCoord(map.BossMapPoint.coord))
                throw new InvalidOperationException("Could not mark the fourth-act endpoint as visited.");

            var room = new CombatRoom(ModelDb.Encounter<DarkEncounter>().ToMutable(), run);
            room.MarkPreFinished();
            await RunManager.Instance.LoadIntoLatestMapCoord(room);
        }
        else
        {
            var elementalBoss = run.Act.PullNextEncounter(RoomType.Boss);
            assertions.True(
                "fourth_act_terminal_windy_consumed_before_dark",
                elementalBoss is WindyEncounter,
                elementalBoss.GetType().FullName ?? "No elemental boss encounter.");
            run.Act.MarkRoomVisited(RoomType.Boss);
            await RunManager.Instance.EnterMapCoord(map.BossMapPoint.coord);
            await CombatScenarioContext.WaitUntilAsync(
                () => CombatManager.Instance.IsInProgress
                    && CombatManager.Instance.DebugOnlyGetState()?.Encounter is DarkEncounter,
                "live Dark endpoint combat");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();

            var combat = CombatManager.Instance.DebugOnlyGetState()
                ?? throw new InvalidOperationException("Dark endpoint combat has no state.");
            foreach (var enemy in combat.Enemies.Where(static enemy => enemy.IsAlive).ToList())
                await CreatureCmd.Kill(enemy);
            await CombatManager.Instance.CheckWinCondition();
        }

        await CombatScenarioContext.WaitUntilAsync(
            () => run.CurrentRoom?.IsVictoryRoom == true,
            restoreFinishedCombat
                ? "Architect after restored FinishedCombat"
                : "Architect after live Dark victory");

        assertions.True("fourth_act_terminal_entered_architect", run.CurrentRoom?.IsVictoryRoom == true);
        assertions.True(
            "fourth_act_terminal_entered_native_architect_event",
            run.CurrentRoom is EventRoom { CanonicalEvent: TheArchitect });
        assertions.Equal(
            "fourth_act_terminal_act_index_preserved",
            FourthActEntryRegistration.FourthActSlotIndex,
            run.CurrentActIndex);
        assertions.True(
            "fourth_act_terminal_left_combat",
            run.CurrentRoom is EventRoom && !CombatManager.Instance.IsInProgress);
        RuntimeTestHost.WriteCheckpoint(
            request,
            restoreFinishedCombat
                ? "fourth_act_finished_combat_entered_architect"
                : "fourth_act_live_victory_entered_architect",
            restoreFinishedCombat
                ? "A restored rewardless Dark FinishedCombat entered The Architect."
                : "A live rewardless Dark victory entered The Architect.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["entry"] = restoreFinishedCombat ? "finished_combat" : "live_victory",
            ["act_index"] = run.CurrentActIndex,
            ["room_type"] = run.CurrentRoom?.RoomType.ToString(),
            ["is_victory_room"] = run.CurrentRoom?.IsVictoryRoom
        };
    }
}
