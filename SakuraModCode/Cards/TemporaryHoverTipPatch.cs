using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Classic.Cards;

namespace SakuraMod.SakuraModCode.Cards;

[HarmonyPatch(typeof(CardModel), nameof(CardModel.HoverTips), MethodType.Getter)]
public static class TemporaryHoverTipPatch
{
    private const string TemporaryTipKey = "SAKURAMOD-TEMPORARY";
    private const string ReleasedTipKey = "SAKURAMOD-RELEASED";
    private const string ReflectionTipKey = "SAKURAMOD-REFLECTION";
    private const string StrongReflectionTipKey = "SAKURAMOD-STRONG_REFLECTION";
    private const string ClassicSpellTipKey = "SAKURAMOD-CLASSIC_SPELL_CARD";

    [HarmonyPostfix]
    public static void HoverTipsPostfix(CardModel __instance, ref IEnumerable<IHoverTip> __result)
    {
        __result = AppendElementTips(__instance, __result);
        __result = AppendClassicElementTips(__instance, __result);
        __result = AppendClassicSakuraCardTips(__instance, __result);
        __result = AppendReferencedKeywordTips(__instance, __result);

        if (__instance.IsTemporary() || ReferencesTemporaryTip(__instance))
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

    private static IEnumerable<IHoverTip> AppendClassicElementTips(CardModel card, IEnumerable<IHoverTip> tips)
    {
        if (card is not ClassicSakuraCard classicCard)
            return tips;

        if (classicCard.Family == ClassicSakuraCardFamily.Spell)
            tips = AppendTip(tips, ClassicSpellTipKey);

        foreach (var element in classicCard.Element.AsElements())
            tips = AppendTip(tips, ClassicElementTipKey(classicCard, element));

        foreach (var element in ClassicElementStatesReferencedBy(classicCard))
            tips = AppendTip(tips, ClassicElementStateTipKey(element));

        return tips;
    }

    private static IEnumerable<IHoverTip> AppendClassicSakuraCardTips(CardModel card, IEnumerable<IHoverTip> tips)
    {
        if (card is ClassicSakuraCard { ShowsSakuraCardVoidTip: true })
            tips = tips.Append(HoverTipFactory.FromKeyword(SakuraKeywords.SakuraCard));

        if (card is ClassicSakuraCard { Family: ClassicSakuraCardFamily.Clow, Identity: ClassicCardIdentity.Voice })
            tips = tips.Append(HoverTipFactory.FromKeyword(SakuraKeywords.Echo));

        if (card is ClassicSakuraCard { Identity: ClassicCardIdentity.Create } createCard)
        {
            tips = tips.Append(HoverTipFactory.FromKeyword(SakuraKeywords.Removable));
            if (createCard.Family == ClassicSakuraCardFamily.Clow)
            {
                tips = tips.Append(HoverTipFactory.FromKeyword(SakuraKeywords.CostDecreasing));
                tips = tips.Append(HoverTipFactory.FromKeyword(SakuraKeywords.EntityLimited));
            }
        }

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

    private static bool ReferencesTemporaryTip(CardModel card) =>
        card is CopiedSoul or MemoryFeather or DimensionalDrift;

    private static IEnumerable<ClassicElement> ClassicElementStatesReferencedBy(ClassicSakuraCard card)
    {
        if (card.Family == ClassicSakuraCardFamily.Sakura && card.Identity == ClassicCardIdentity.Wave)
            return (ClassicElement.Earthy | ClassicElement.Firey | ClassicElement.Watery | ClassicElement.Windy).AsElements();

        if (card.Family is not (ClassicSakuraCardFamily.Clow or ClassicSakuraCardFamily.Sakura))
            return [];

        return card.Identity is ClassicCardIdentity.Earthy
            or ClassicCardIdentity.Firey
            or ClassicCardIdentity.Watery
            or ClassicCardIdentity.Windy
            ? card.Element.AsElements()
            : [];
    }

    private static string ClassicElementTipKey(ClassicSakuraCard card, ClassicElement element) =>
        card.Family == ClassicSakuraCardFamily.Spell
            ? ClassicElementSpellTipKey(element)
            : ClassicElementCardTipKey(element);

    private static string ClassicElementCardTipKey(ClassicElement element) =>
        element switch
        {
            ClassicElement.Earthy => "SAKURAMOD-CLASSIC_EARTHY_CARD",
            ClassicElement.Firey => "SAKURAMOD-CLASSIC_FIREY_CARD",
            ClassicElement.Watery => "SAKURAMOD-CLASSIC_WATERY_CARD",
            ClassicElement.Windy => "SAKURAMOD-CLASSIC_WINDY_CARD",
            _ => throw new ArgumentOutOfRangeException(nameof(element), element, null)
        };

    private static string ClassicElementSpellTipKey(ClassicElement element) =>
        element switch
        {
            ClassicElement.Earthy => "SAKURAMOD-CLASSIC_EARTHY_SPELL_CARD",
            ClassicElement.Firey => "SAKURAMOD-CLASSIC_FIREY_SPELL_CARD",
            ClassicElement.Watery => "SAKURAMOD-CLASSIC_WATERY_SPELL_CARD",
            ClassicElement.Windy => "SAKURAMOD-CLASSIC_WINDY_SPELL_CARD",
            _ => throw new ArgumentOutOfRangeException(nameof(element), element, null)
        };

    private static string ClassicElementStateTipKey(ClassicElement element) =>
        element switch
        {
            ClassicElement.Earthy => "SAKURAMOD-CLASSIC_EARTHY_STATE",
            ClassicElement.Firey => "SAKURAMOD-CLASSIC_FIREY_STATE",
            ClassicElement.Watery => "SAKURAMOD-CLASSIC_WATERY_STATE",
            ClassicElement.Windy => "SAKURAMOD-CLASSIC_WINDY_STATE",
            _ => throw new ArgumentOutOfRangeException(nameof(element), element, null)
        };

    private static IEnumerable<IHoverTip> AppendTip(IEnumerable<IHoverTip> tips, string key) =>
        tips.Append(new HoverTip(
            new LocString("static_hover_tips", $"{key}.title"),
            new LocString("static_hover_tips", $"{key}.description")));
}
