using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Classic.Powers;
using StsVoid = MegaCrit.Sts2.Core.Models.Cards.Void;

namespace SakuraMod.SakuraModCode.Classic.Cards;

[HarmonyPatch(typeof(StsVoid), nameof(StsVoid.AfterCardDrawn))]
internal static class ClassicLightVoidPatch
{
    [HarmonyPrefix]
    private static bool SkipVoidEnergyLossWhenLightIsActive(StsVoid __instance, CardModel card, ref Task __result)
    {
        if (card != __instance || __instance.Owner?.Creature.GetPower<ClassicLightPower>() is null)
            return true;

        __result = Task.CompletedTask;
        return false;
    }
}
