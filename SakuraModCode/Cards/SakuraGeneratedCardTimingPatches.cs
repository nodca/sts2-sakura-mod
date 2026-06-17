using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace SakuraMod.SakuraModCode.Cards;

[HarmonyPatch(typeof(NCard))]
internal static class SakuraGeneratedCardNodeTimingPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(NCard.Create))]
    public static void CreatePrefix(
        CardModel card,
        out SakuraGeneratedCardDiagnostics.DetailTiming? __state)
    {
        __state = SakuraGeneratedCardDiagnostics.StartDetail(card, "n-card-create");
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NCard.Create))]
    public static void CreatePostfix(
        NCard? __result,
        SakuraGeneratedCardDiagnostics.DetailTiming? __state)
    {
        __state?.Finish(__result is null ? "result=null" : "result=node");
    }

    [HarmonyPriority(Priority.First)]
    [HarmonyPrefix]
    [HarmonyPatch(nameof(NCard.UpdateVisuals))]
    public static void UpdateVisualsPrefix(
        NCard __instance,
        PileType pileType,
        CardPreviewMode previewMode,
        out SakuraGeneratedCardDiagnostics.DetailTiming? __state)
    {
        __state = SakuraGeneratedCardDiagnostics.StartDetail(__instance.Model, "n-card-update-visuals");
    }

    [HarmonyPriority(Priority.Last)]
    [HarmonyPostfix]
    [HarmonyPatch(nameof(NCard.UpdateVisuals))]
    public static void UpdateVisualsPostfix(
        PileType pileType,
        CardPreviewMode previewMode,
        SakuraGeneratedCardDiagnostics.DetailTiming? __state)
    {
        __state?.Finish($"pile={pileType} preview={previewMode}");
    }
}

[HarmonyPatch(typeof(NPlayerHand))]
internal static class SakuraGeneratedCardHandTimingPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(NPlayerHand.Add))]
    public static void AddPrefix(
        NCard card,
        out SakuraGeneratedCardDiagnostics.DetailTiming? __state)
    {
        __state = SakuraGeneratedCardDiagnostics.StartDetail(card.Model, "n-player-hand-add");
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NPlayerHand.Add))]
    public static void AddPostfix(SakuraGeneratedCardDiagnostics.DetailTiming? __state)
    {
        __state?.Finish();
    }

    [HarmonyPrefix]
    [HarmonyPatch("RefreshLayout")]
    public static void RefreshLayoutPrefix(
        NPlayerHand __instance,
        out SakuraGeneratedCardDiagnostics.DetailTiming? __state)
    {
        __state = StartHandDetail(__instance, "n-player-hand-refresh-layout");
    }

    [HarmonyPostfix]
    [HarmonyPatch("RefreshLayout")]
    public static void RefreshLayoutPostfix(SakuraGeneratedCardDiagnostics.DetailTiming? __state)
    {
        __state?.Finish();
    }

    private static SakuraGeneratedCardDiagnostics.DetailTiming? StartHandDetail(
        NPlayerHand hand,
        string stage)
    {
        if (!SakuraGeneratedCardDiagnostics.Enabled)
            return null;

        foreach (var holder in hand.ActiveHolders)
        {
            var model = holder.CardNode?.Model;
            if (SakuraGeneratedCardDiagnostics.IsTracked(model))
                return SakuraGeneratedCardDiagnostics.StartDetail(model, stage);
        }

        return null;
    }
}

[HarmonyPatch(typeof(NHandCardHolder))]
internal static class SakuraGeneratedCardHandHolderTimingPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(NHandCardHolder.Create))]
    public static void CreatePrefix(
        NCard card,
        out SakuraGeneratedCardDiagnostics.DetailTiming? __state)
    {
        __state = SakuraGeneratedCardDiagnostics.StartDetail(card.Model, "n-hand-card-holder-create");
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NHandCardHolder.Create))]
    public static void CreatePostfix(SakuraGeneratedCardDiagnostics.DetailTiming? __state)
    {
        __state?.Finish();
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(NHandCardHolder.UpdateCard))]
    public static void UpdateCardPrefix(
        NHandCardHolder __instance,
        out SakuraGeneratedCardDiagnostics.DetailTiming? __state)
    {
        __state = SakuraGeneratedCardDiagnostics.StartDetail(
            __instance.CardNode?.Model,
            "n-hand-card-holder-update-card");
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NHandCardHolder.UpdateCard))]
    public static void UpdateCardPostfix(SakuraGeneratedCardDiagnostics.DetailTiming? __state)
    {
        __state?.Finish();
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(NHandCardHolder.SetDefaultTargets))]
    public static void SetDefaultTargetsPrefix(
        NHandCardHolder __instance,
        out SakuraGeneratedCardDiagnostics.DetailTiming? __state)
    {
        __state = SakuraGeneratedCardDiagnostics.StartDetail(
            __instance.CardNode?.Model,
            "n-hand-card-holder-set-default-targets");
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NHandCardHolder.SetDefaultTargets))]
    public static void SetDefaultTargetsPostfix(SakuraGeneratedCardDiagnostics.DetailTiming? __state)
    {
        __state?.Finish();
    }
}
