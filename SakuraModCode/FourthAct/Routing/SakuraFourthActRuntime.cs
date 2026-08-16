using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.Rooms;
using SakuraMod.SakuraModCode.FourthAct.Dark.Encounters;

namespace SakuraMod.SakuraModCode.FourthAct.Routing;

internal static class SakuraFourthActMapFactory
{
    internal static bool TryCreate(ActModel act, out ActMap? map)
    {
        if (act is not SakuraFourthAct fourthAct)
        {
            map = null;
            return false;
        }

        if (fourthAct.IsMutable)
            fourthAct.ConfigureRouteBosses();
        map = new SakuraFourthActMap(FourthActRouteCatalog.Resolve());
        return true;
    }
}

internal static class SakuraFourthActSaveCompatibility
{
    internal static void NormalizeRoomCollections(SerializableRoomSet rooms)
    {
        rooms.EventIds ??= [];
        rooms.NormalEncounterIds ??= [];
        rooms.EliteEncounterIds ??= [];
    }

    internal static void NormalizeFourthAct(SerializableActModel save)
    {
        if (save.Id != ModelDb.GetId<SakuraFourthAct>()
            || save.SerializableRooms is not { } rooms)
        {
            return;
        }

        NormalizeRoomCollections(rooms);
    }
}

[HarmonyPatch(typeof(ActModel), nameof(ActModel.FromSave))]
internal static class SakuraFourthActSaveCompatibilityPatch
{
    [HarmonyPrefix]
    private static void Prefix(SerializableActModel save) =>
        SakuraFourthActSaveCompatibility.NormalizeFourthAct(save);
}

[HarmonyPatch(typeof(ActModel), nameof(ActModel.CreateMap))]
internal static class SakuraFourthActMapFactoryPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ActModel __instance, ref ActMap __result)
    {
        if (!SakuraFourthActMapFactory.TryCreate(__instance, out var map))
            return true;

        __result = map!;
        return false;
    }
}

internal static class SakuraFourthActRunTransition
{
    // STS2 v0.107.1 exposes no supported way to append an act after run creation.
    private static readonly MethodInfo RunStateActsSetter =
        typeof(RunState).GetProperty(nameof(RunState.Acts), BindingFlags.Instance | BindingFlags.Public)!
            .GetSetMethod(nonPublic: true)!;

    internal static bool ShouldAppendSlot(
        bool hasCompleteRoute,
        bool canEnterFourthAct,
        int currentActIndex,
        int actCount) =>
        hasCompleteRoute
        && canEnterFourthAct
        && currentActIndex == FourthActEntryRegistration.FourthActSlotIndex - 1
        && actCount == FourthActEntryRegistration.FourthActSlotIndex;

    internal static void TryAppendSlot(RunState runState)
    {
        if (!ShouldAppendSlot(
                FourthActEntryRegistration.CanRegister(FourthActRouteCatalog.Resolve()),
                FourthActEntryRegistration.CanEnter(runState),
                runState.CurrentActIndex,
                runState.Acts.Count))
        {
            return;
        }

        var acts = runState.Acts.Append(ModelDb.Act<SakuraFourthAct>()).ToList();
        RunStateActsSetter.Invoke(runState, [acts]);
    }
}

internal static class SakuraFourthActTerminalTransition
{
    internal static bool ShouldRouteToArchitect(
        ActModel act,
        EncounterModel? encounter,
        bool shouldGiveRewards,
        RoomType roomType,
        int currentActIndex,
        int actCount,
        MapCoord? currentMapCoord,
        MapCoord bossMapCoord) =>
        act is SakuraFourthAct
        && encounter is DarkEncounter
        && !shouldGiveRewards
        && roomType == RoomType.Boss
        && currentActIndex == actCount - 1
        && currentMapCoord == bossMapCoord;

    internal static bool ShouldRouteToArchitect(IRunState runState)
    {
        if (runState.CurrentRoom is not CombatRoom room)
            return false;

        return ShouldRouteToArchitect(
            runState.Act,
            room.Encounter,
            room.Encounter.ShouldGiveRewards,
            room.RoomType,
            runState.CurrentActIndex,
            runState.Acts.Count,
            runState.CurrentMapCoord,
            runState.Map.BossMapPoint.coord);
    }

    internal static async Task ProceedAsync(RunManager runManager, CancellationToken cancellationToken)
    {
        await MegaCrit.Sts2.Core.Commands.Cmd.Wait(1f, cancellationToken);

        var runState = runManager.DebugOnlyGetState();
        if (runState is not null && ShouldRouteToArchitect(runState))
            runManager.ActChangeSynchronizer.SetLocalPlayerReady();
    }

    internal static async Task ProceedAfterLoadAsync(Task loadTask, RunManager runManager)
    {
        await loadTask;

        var runState = runManager.DebugOnlyGetState();
        if (runState is not null && ShouldRouteToArchitect(runState))
            runManager.ActChangeSynchronizer.SetLocalPlayerReady();
    }
}

[HarmonyPatch(typeof(NCombatUi), nameof(NCombatUi.ProceedWithoutRewards))]
internal static class SakuraFourthActTerminalTransitionPatch
{
    [HarmonyPrefix]
    private static bool Prefix(
        ref Task __result,
        CancellationTokenSource ____cts)
    {
        var runManager = RunManager.Instance;
        var runState = runManager.DebugOnlyGetState();
        if (runState is null || !SakuraFourthActTerminalTransition.ShouldRouteToArchitect(runState))
            return true;

        // A restored FinishedCombat is still inside LoadIntoLatestMapCoord here. Let that
        // loading operation finish before starting the Architect room transition.
        if (NCombatRoom.Instance?.Mode == CombatRoomMode.FinishedCombat)
            return true;

        __result = SakuraFourthActTerminalTransition.ProceedAsync(runManager, ____cts.Token);
        return false;
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.LoadIntoLatestMapCoord))]
internal static class SakuraFourthActRestoredTerminalTransitionPatch
{
    [HarmonyPostfix]
    private static void Postfix(
        RunManager __instance,
        AbstractRoom? preFinishedRoom,
        ref Task __result)
    {
        if (preFinishedRoom is not CombatRoom
            {
                IsPreFinished: true,
                Encounter: DarkEncounter
            })
        {
            return;
        }

        __result = SakuraFourthActTerminalTransition.ProceedAfterLoadAsync(__result, __instance);
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.EnterNextAct))]
internal static class SakuraFourthActRunTransitionPatch
{
    [HarmonyPrefix]
    private static void Prefix(RunManager __instance)
    {
        var runState = __instance.DebugOnlyGetState();
        if (runState is not null)
            SakuraFourthActRunTransition.TryAppendSlot(runState);
    }
}
