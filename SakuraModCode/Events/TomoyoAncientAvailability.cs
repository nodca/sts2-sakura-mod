using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Events.Models;

namespace SakuraMod.SakuraModCode.Events;

internal static class TomoyoAncientAvailability
{
    internal static IEnumerable<AncientEventModel> FilterForRun(
        IEnumerable<AncientEventModel> ancients,
        IRunState runState) =>
        SakuraStarterCompatibility.IsKinomotoSakuraRun(runState)
            ? ancients
            : ancients.Where(static ancient => ancient is not ClassicTomoyoAncientCostumes);
}

[HarmonyPatch(typeof(Hive), nameof(Hive.GetUnlockedAncients), [typeof(UnlockState)])]
internal static class TomoyoAncientAvailabilityPatch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(ref IEnumerable<AncientEventModel> __result)
    {
        var runState = RunManager.Instance.DebugOnlyGetState();
        if (runState is not null)
            __result = TomoyoAncientAvailability.FilterForRun(__result, runState);
    }
}
