using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

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
        map = new SakuraFourthActMap(FourthActRouteCatalog.CompleteRoutes);
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
                FourthActEntryRegistration.CanRegister(FourthActRouteCatalog.DraftRoutes),
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
