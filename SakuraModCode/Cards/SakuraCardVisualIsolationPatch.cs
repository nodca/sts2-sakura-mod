using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace SakuraMod.SakuraModCode.Cards;

[HarmonyPatch(typeof(NCard))]
internal static class SakuraCardVisualIsolationPatch
{
    [HarmonyPriority(Priority.First)]
    [HarmonyPrefix]
    [HarmonyPatch(nameof(NCard.UpdateVisuals))]
    public static void UpdateVisualsPrefix(NCard __instance)
    {
        SakuraCardVisualDispatcher.BeforeCardUpdateVisualsIsolation(__instance);
    }
}
