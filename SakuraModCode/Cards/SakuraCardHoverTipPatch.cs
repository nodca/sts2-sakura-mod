using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace SakuraMod.SakuraModCode.Cards;

[HarmonyPatch(typeof(NHoverTipCardContainer), nameof(NHoverTipCardContainer.Add), [typeof(CardHoverTip)])]
public static class SakuraCardHoverTipPatch
{
    [HarmonyPostfix]
    public static void AddPostfix(NHoverTipCardContainer __instance)
    {
        SakuraCardGeometryLifecycle.ResizeHoverTip(__instance);
    }
}
