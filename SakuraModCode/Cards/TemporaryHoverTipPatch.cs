using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace SakuraMod.SakuraModCode.Cards;

[HarmonyPatch(typeof(CardModel), nameof(CardModel.HoverTips), MethodType.Getter)]
public static class TemporaryHoverTipPatch
{
    private const string TemporaryTipKey = "SAKURAMOD-TEMPORARY";
    private const string ReleasedTipKey = "SAKURAMOD-RELEASED";
    private const string ReflectionTipKey = "SAKURAMOD-REFLECTION";
    private const string StrongReflectionTipKey = "SAKURAMOD-STRONG_REFLECTION";

    [HarmonyPostfix]
    public static void HoverTipsPostfix(CardModel __instance, ref IEnumerable<IHoverTip> __result)
    {
        if (__instance.IsTemporary())
            __result = AppendTip(__result, TemporaryTipKey);
        if (__instance.IsReleased())
            __result = AppendTip(__result, ReleasedTipKey);

        if (__instance is Reflect)
            __result = AppendTip(
                __result,
                __instance.CurrentUpgradeLevel > 0 ? StrongReflectionTipKey : ReflectionTipKey);
    }

    private static IEnumerable<IHoverTip> AppendTip(IEnumerable<IHoverTip> tips, string key) =>
        tips.Append(new HoverTip(
            new LocString("static_hover_tips", $"{key}.title"),
            new LocString("static_hover_tips", $"{key}.description")));
}
