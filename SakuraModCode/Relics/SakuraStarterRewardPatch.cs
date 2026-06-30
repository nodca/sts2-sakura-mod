using BaseLib.Abstracts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Character;

namespace SakuraMod.SakuraModCode.Relics;

[HarmonyPatch]
internal static class SakuraStarterRewardPatch
{
    private const string NeowCursedDonePage = "NEOW.pages.DONE.CURSED.description";

    private static readonly Action<AncientEventModel, string?> SetAncientDonePage =
        (Action<AncientEventModel, string?>)Delegate.CreateDelegate(
            typeof(Action<AncientEventModel, string?>),
            AccessTools.PropertySetter(typeof(AncientEventModel), "CustomDonePage"));

    private static readonly Action<AncientEventModel> FinishAncient =
        (Action<AncientEventModel>)Delegate.CreateDelegate(
            typeof(Action<AncientEventModel>),
            AccessTools.Method(typeof(AncientEventModel), "Done"));

    [HarmonyPatch(typeof(Neow), "GenerateInitialOptions")]
    [HarmonyPostfix]
    private static void ReplaceSakuraNeowOptions(Neow __instance, ref IReadOnlyList<EventOption> __result)
    {
        __result = SakuraStarterCompatibility.ReplaceNeowRelicOptions(
            __instance,
            __result,
            SakuraNeowRelicOption);
    }

    [HarmonyPatch(typeof(PandorasBox), nameof(PandorasBox.AfterObtained))]
    [HarmonyPrefix]
    private static bool TransformSakuraBasicStrikeOrDefendCards(PandorasBox __instance, ref Task __result)
    {
        return SakuraStarterCompatibility.TryTransformSakuraBasicStrikeOrDefendCards(__instance, ref __result);
    }

    [HarmonyPatch(typeof(NeowsTalisman), nameof(NeowsTalisman.AfterObtained))]
    [HarmonyPrefix]
    private static bool UpgradeSakuraBasicStrikeAndDefendCards(NeowsTalisman __instance, ref Task __result)
    {
        return SakuraStarterCompatibility.TryUpgradeSakuraBasicStrikeAndDefendCards(__instance, ref __result);
    }

    [HarmonyPatch(typeof(LargeCapsule), nameof(LargeCapsule.AfterObtained))]
    [HarmonyPrefix]
    private static bool ApplyKeroSnackBoxForSakura(LargeCapsule __instance, ref Task __result)
    {
        return SakuraStarterCompatibility.TryApplyKeroSnackBoxForSakura(__instance, ref __result);
    }

    [HarmonyPatch(typeof(LeafyPoultice), nameof(LeafyPoultice.AfterObtained))]
    [HarmonyPrefix]
    private static bool ApplyBrokenClockGearForSakura(LeafyPoultice __instance, ref Task __result)
    {
        return SakuraStarterCompatibility.TryApplyBrokenClockGearForSakura(__instance, ref __result);
    }

    [HarmonyPatch(typeof(StrikeDummy), nameof(StrikeDummy.ModifyDamageAdditive))]
    [HarmonyPrefix]
    private static bool ApplyStrikeDummyDamageForSakura(
        StrikeDummy __instance,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        ref decimal __result)
    {
        return SakuraStarterCompatibility.TryApplyStrikeDummyDamageForSakura(
            __instance,
            props,
            dealer,
            cardSource,
            ref __result);
    }

    [HarmonyPatch(typeof(FakeStrikeDummy), nameof(FakeStrikeDummy.ModifyDamageAdditive))]
    [HarmonyPrefix]
    private static bool ApplyFakeStrikeDummyDamageForSakura(
        FakeStrikeDummy __instance,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        ref decimal __result)
    {
        return SakuraStarterCompatibility.TryApplyStrikeDummyDamageForSakura(
            __instance,
            props,
            dealer,
            cardSource,
            ref __result);
    }

    [HarmonyPatch(typeof(GhostSeed), nameof(GhostSeed.AfterCardEnteredCombat))]
    [HarmonyPrefix]
    private static bool ApplyGhostSeedAfterCardEnteredCombatForSakura(
        GhostSeed __instance,
        CardModel card,
        ref Task __result)
    {
        return SakuraStarterCompatibility.TryApplyGhostSeedAfterCardEnteredCombatForSakura(
            __instance,
            card,
            ref __result);
    }

    [HarmonyPatch(typeof(GhostSeed), nameof(GhostSeed.AfterRoomEntered))]
    [HarmonyPrefix]
    private static bool ApplyGhostSeedAfterRoomEnteredForSakura(
        GhostSeed __instance,
        AbstractRoom room,
        ref Task __result)
    {
        return SakuraStarterCompatibility.TryApplyGhostSeedAfterRoomEnteredForSakura(
            __instance,
            room,
            ref __result);
    }

    [HarmonyPatch(typeof(NutritiousSoup), nameof(NutritiousSoup.AfterObtained))]
    [HarmonyPrefix]
    private static bool ApplyTezcatarasEmberForSakura(NutritiousSoup __instance, ref Task __result)
    {
        return SakuraStarterCompatibility.TryApplyTezcatarasEmberForSakura(__instance, ref __result);
    }

    [HarmonyPatch(typeof(PaelsClaw), nameof(PaelsClaw.AfterObtained))]
    [HarmonyPrefix]
    private static bool ApplySakuraGoopyForSakura(PaelsClaw __instance, ref Task __result)
    {
        return SakuraStarterCompatibility.TryApplySakuraGoopyForSakura(__instance, ref __result);
    }

    [HarmonyPatch(typeof(ArchaicTooth), nameof(ArchaicTooth.SetupForPlayer))]
    [HarmonyPrefix]
    private static bool SetupArchaicToothForSakura(
        ArchaicTooth __instance,
        Player player,
        ref bool __result)
    {
        return SakuraStarterCompatibility.TrySetupArchaicToothForSakura(__instance, player, ref __result);
    }

    [HarmonyPatch(typeof(ArchaicTooth), nameof(ArchaicTooth.AfterObtained))]
    [HarmonyPrefix]
    private static bool ApplyArchaicToothForSakura(ArchaicTooth __instance, ref Task __result)
    {
        return SakuraStarterCompatibility.TryApplyArchaicToothForSakura(__instance, ref __result);
    }

    [HarmonyPatch(typeof(TouchOfOrobas), nameof(TouchOfOrobas.GetUpgradedStarterRelic))]
    [HarmonyPrefix]
    private static bool UseCustomStarterRelicUpgrade(RelicModel starterRelic, ref RelicModel __result)
    {
        return SakuraStarterCompatibility.TryUseCustomStarterRelicUpgrade(starterRelic, ref __result);
    }

    private static EventOption SakuraNeowRelicOption(Neow neow, RelicModel relicTemplate)
    {
        var relic = relicTemplate.ToMutable();
        relic.Owner = neow.Owner!;
        var textKey = $"{relic.Id.Entry}.neowOption";

        async Task OnChosen() =>
            await SakuraStarterCompatibility.ObtainNeowReplacementRelic(
                neow,
                relic,
                SetAncientDonePage,
                FinishAncient);

        return EventOption.FromRelic(relic, neow, OnChosen, textKey);
    }
}
