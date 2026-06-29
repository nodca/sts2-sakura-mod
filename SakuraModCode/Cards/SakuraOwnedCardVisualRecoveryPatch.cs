using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace SakuraMod.SakuraModCode.Cards;

[HarmonyPatch(typeof(NCard))]
public static class SakuraOwnedCardVisualRecoveryPatch
{
    [HarmonyFinalizer]
    [HarmonyPatch(nameof(NCard.UpdateVisuals))]
    public static Exception? UpdateVisualsFinalizer(NCard __instance, Exception? __exception)
    {
        return SakuraCardVisualDispatcher.RecoverCardVisuals(
            __instance,
            __exception,
            nameof(NCard.UpdateVisuals));
    }

    [HarmonyFinalizer]
    [HarmonyPatch(nameof(NCard._EnterTree))]
    public static Exception? EnterTreeFinalizer(NCard __instance, Exception? __exception)
    {
        return SakuraCardVisualDispatcher.RecoverCardVisuals(
            __instance,
            __exception,
            nameof(NCard._EnterTree));
    }
}
