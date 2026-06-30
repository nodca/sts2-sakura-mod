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
        __result = AppendElementTips(__instance, __result);
        __result = AppendReferencedKeywordTips(__instance, __result);

        if (__instance.IsTemporary())
            __result = AppendTip(__result, TemporaryTipKey);
        if (__instance.IsReleased())
            __result = AppendTip(__result, ReleasedTipKey);

        if (__instance is Reflect)
            __result = AppendTip(
                __result,
                __instance.CurrentUpgradeLevel > 0 ? StrongReflectionTipKey : ReflectionTipKey);

        __result = __result.Distinct();
    }

    private static IEnumerable<IHoverTip> AppendElementTips(CardModel card, IEnumerable<IHoverTip> tips)
    {
        var elements = SakuraActions.ElementSetOf(card) | SakuraActions.StaticElementSetOf(card);
        foreach (var element in elements.AsElements())
            tips = tips.Append(HoverTipFactory.FromKeyword(SakuraActions.KeywordFor(element)));

        return tips;
    }

    private static IEnumerable<IHoverTip> AppendReferencedKeywordTips(CardModel card, IEnumerable<IHoverTip> tips)
    {
        if (card is not SakuraModCard sakuraCard)
            return tips;

        foreach (var keyword in sakuraCard.ReferencedKeywords)
            tips = tips.Append(HoverTipFactory.FromKeyword(keyword));

        return tips;
    }

    private static IEnumerable<IHoverTip> AppendTip(IEnumerable<IHoverTip> tips, string key) =>
        tips.Append(new HoverTip(
            new LocString("static_hover_tips", $"{key}.title"),
            new LocString("static_hover_tips", $"{key}.description")));
}
