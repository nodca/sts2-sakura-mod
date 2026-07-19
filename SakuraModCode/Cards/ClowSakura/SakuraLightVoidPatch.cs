using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Powers;
using StsVoid = MegaCrit.Sts2.Core.Models.Cards.Void;

namespace SakuraMod.SakuraModCode.Cards;

[HarmonyPatch(typeof(StsVoid), nameof(StsVoid.AfterCardDrawn))]
internal static class SakuraLightVoidPatch
{
    [HarmonyPrefix]
    private static bool SkipVoidEnergyLossWhenLightIsActive(StsVoid __instance, CardModel card, ref Task __result)
    {
        if (card != __instance || !SakuraLightPowerBase.IsActive(__instance.Owner?.Creature))
            return true;

        __result = Task.CompletedTask;
        return false;
    }
}
