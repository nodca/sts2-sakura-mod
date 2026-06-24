using BaseLib.Abstracts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens;
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
        if (__instance.Owner is null || !SakuraStarterCards.IsSakura(__instance.Owner))
            return;

        var options = __result.ToList();
        for (var i = 0; i < options.Count; i++)
        {
            options[i] = options[i].Relic switch
            {
                LargeCapsule => SakuraNeowRelicOption<KeroSnackBox>(__instance),
                LeafyPoultice => SakuraNeowRelicOption<BrokenClockGear>(__instance),
                _ => options[i]
            };
        }

        __result = options;
    }

    [HarmonyPatch(typeof(PandorasBox), nameof(PandorasBox.AfterObtained))]
    [HarmonyPrefix]
    private static bool TransformSakuraStarterCards(PandorasBox __instance, ref Task __result)
    {
        if (!SakuraStarterCards.IsSakura(__instance.Owner))
            return true;

        __result = TransformSakuraStarterCards(__instance.Owner);
        return false;
    }

    [HarmonyPatch(typeof(LargeCapsule), nameof(LargeCapsule.AfterObtained))]
    [HarmonyPrefix]
    private static bool ApplyKeroSnackBoxForSakura(LargeCapsule __instance, ref Task __result)
    {
        if (!SakuraStarterCards.IsSakura(__instance.Owner))
            return true;

        __result = SakuraStarterRelicEffects.ApplyKeroSnackBox(
            __instance.Owner,
            __instance.DynamicVars["Relics"].IntValue);
        return false;
    }

    [HarmonyPatch(typeof(LeafyPoultice), nameof(LeafyPoultice.AfterObtained))]
    [HarmonyPrefix]
    private static bool ApplyBrokenClockGearForSakura(LeafyPoultice __instance, ref Task __result)
    {
        if (!SakuraStarterCards.IsSakura(__instance.Owner))
            return true;

        __result = SakuraStarterRelicEffects.ApplyBrokenClockGear(
            __instance.Owner,
            __instance.DynamicVars.MaxHp.BaseValue);
        return false;
    }

    [HarmonyPatch(typeof(TouchOfOrobas), nameof(TouchOfOrobas.GetUpgradedStarterRelic))]
    [HarmonyPrefix]
    private static bool UseCustomStarterRelicUpgrade(RelicModel starterRelic, ref RelicModel __result)
    {
        if (starterRelic is not CustomRelicModel customStarter)
            return true;

        var replacement = customStarter.GetUpgradeReplacement();
        if (replacement is null || replacement.Id == starterRelic.Id)
            return true;

        __result = replacement;
        return false;
    }

    private static EventOption SakuraNeowRelicOption<T>(Neow neow)
        where T : RelicModel
    {
        var relic = ModelDb.Relic<T>().ToMutable();
        relic.Owner = neow.Owner!;
        var textKey = $"{relic.Id.Entry}.neowOption";

        async Task OnChosen()
        {
            await RelicCmd.Obtain(relic, neow.Owner!);
            SetAncientDonePage(neow, NeowCursedDonePage);
            FinishAncient(neow);
        }

        return EventOption.FromRelic(relic, neow, OnChosen, textKey);
    }

    private static async Task TransformSakuraStarterCards(Player owner)
    {
        var transformations = owner.Deck.Cards
            .Where(card => SakuraStarterCards.IsStarterCard(card) && card.IsTransformable)
            .Select(card => SakuraStarterRelicEffects.CreateSupportTransformation(owner, card))
            .ToList();

        var results = (await CardCmd.Transform(transformations, null, CardPreviewStyle.None)).ToList();
        if (results.Count > 0 && LocalContext.IsMe(owner))
            NSimpleCardsViewScreen.ShowScreen(results, new LocString("relics", "PANDORAS_BOX.infoText"));
    }
}
