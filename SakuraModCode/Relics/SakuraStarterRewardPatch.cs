using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using SakuraMod.SakuraModCode.Character;

namespace SakuraMod.SakuraModCode.Relics;

[HarmonyPatch]
internal static class SakuraStarterRewardPatch
{
    [HarmonyPatch(typeof(TouchOfOrobas), nameof(TouchOfOrobas.SetupForPlayer))]
    [HarmonyPrefix]
    private static bool SetupClassicSealedWandStarterUpgrade(
        TouchOfOrobas __instance,
        Player player,
        ref bool __result)
    {
        return SakuraStarterCompatibility.TrySetupClassicSealedWandStarterUpgrade(__instance, player, ref __result);
    }

    [HarmonyPatch(typeof(TouchOfOrobas), nameof(TouchOfOrobas.AfterObtained))]
    [HarmonyPrefix]
    private static bool ApplyClassicSealedWandStarterUpgrade(TouchOfOrobas __instance, ref Task __result)
    {
        return SakuraStarterCompatibility.TryApplyClassicSealedWandStarterUpgrade(__instance, ref __result);
    }

    [HarmonyPatch(typeof(TouchOfOrobas), nameof(TouchOfOrobas.GetUpgradedStarterRelic))]
    [HarmonyPrefix]
    private static bool UseCustomStarterRelicUpgrade(RelicModel starterRelic, ref RelicModel __result)
    {
        return SakuraStarterCompatibility.TryUseCustomStarterRelicUpgrade(starterRelic, ref __result);
    }
}
