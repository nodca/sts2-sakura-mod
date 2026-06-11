using HarmonyLib;
using MegaCrit.Sts2.Core.Runs;
using SakuraMod.SakuraModCode.Character;
using VanillaEvents = MegaCrit.Sts2.Core.Models.Events;

namespace SakuraMod.SakuraModCode.Events;

[HarmonyPatch]
internal static class SakuraStarterEventPoolPatch
{
    [HarmonyPatch(typeof(VanillaEvents.Amalgamator), nameof(VanillaEvents.Amalgamator.IsAllowed))]
    [HarmonyPrefix]
    private static bool HideVanillaAmalgamatorForSakura(IRunState runState, ref bool __result) =>
        HideVanillaStarterEventForSakura(runState, ref __result);

    [HarmonyPatch(typeof(VanillaEvents.WoodCarvings), nameof(VanillaEvents.WoodCarvings.IsAllowed))]
    [HarmonyPrefix]
    private static bool HideVanillaWoodCarvingsForSakura(IRunState runState, ref bool __result) =>
        HideVanillaStarterEventForSakura(runState, ref __result);

    [HarmonyPatch(typeof(VanillaEvents.SpiralingWhirlpool), nameof(VanillaEvents.SpiralingWhirlpool.IsAllowed))]
    [HarmonyPrefix]
    private static bool HideVanillaSpiralingWhirlpoolForSakura(IRunState runState, ref bool __result) =>
        HideVanillaStarterEventForSakura(runState, ref __result);

    private static bool HideVanillaStarterEventForSakura(IRunState runState, ref bool __result)
    {
        if (!SakuraStarterCards.IsSakuraRun(runState))
            return true;

        __result = false;
        return false;
    }
}
