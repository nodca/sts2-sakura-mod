using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using STS2RitsuLib;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.Patching.Models;
using System.Threading.Tasks;

namespace SakuraMod.SakuraModCode.Cards;

internal static class SakuraCardVisualPatchRegistration
{
    public static void Register()
    {
        var patcher = RitsuLibFramework.CreatePatcher(MainFile.ModId, "card-visuals", "card visuals");

        patcher.RegisterPatch<SakuraCardReloadPortraitSynchronizationPatch>();
        patcher.RegisterPatch<SakuraCardUpdateVisualsPatch>();
        patcher.RegisterPatch<SakuraCardEnchantmentChangedGeometryPatch>();
        patcher.RegisterPatch<SakuraGeneratedTransparentCardUpdateVisualsPatch>();
        patcher.RegisterPatch<KinomotoSakuraCardRewardGlowPatch>();
        patcher.RegisterPatch<KinomotoSakuraCardCurrentSizePatch>();
        patcher.RegisterPatch<KinomotoSakuraCardHolderSetCardPatch>();
        patcher.RegisterPatch<KinomotoSakuraCardHolderReassignedPatch>();
        patcher.RegisterPatch<KinomotoSakuraPlayerHandRefreshLayoutPatch>();
        patcher.RegisterPatch<KinomotoSakuraHandHolderReadyPatch>();
        patcher.RegisterPatch<KinomotoSakuraHandHolderSetCardPatch>();
        patcher.RegisterPatch<KinomotoSakuraHandHolderUpdateCardPatch>();
        patcher.RegisterPatch<KinomotoSakuraHandHolderSetDefaultTargetsPatch>();
        patcher.RegisterPatch<KinomotoSakuraPreviewHolderReadyPatch>();
        patcher.RegisterPatch<KinomotoSakuraPreviewHolderSetCardScalePatch>();
        patcher.RegisterPatch<KinomotoSakuraCardGridSetCardsPatch>();
        patcher.RegisterPatch<KinomotoSakuraCardGridUpdatePositionsPatch>();
        patcher.RegisterPatch<KinomotoSakuraCardGridAllocateHoldersPatch>();
        patcher.RegisterPatch<KinomotoSakuraCardSelectionHighlightShowPatch>();
        patcher.RegisterPatch<KinomotoSakuraCardSelectionHighlightHidePatch>();
        patcher.RegisterPatch<KinomotoSakuraCardSelectionHighlightHideInstantlyPatch>();
        patcher.RegisterPatch<SakuraOwnedCardUpdateVisualsRecoveryPatch>();
        patcher.RegisterPatch<SakuraOwnedCardEnterTreeRecoveryPatch>();

        if (!RitsuLibFramework.ApplyRequiredPatcher(
                patcher,
                MarkRequiredPatcherFailed,
                "Required Sakura card visual patches failed. SakuraMod initialization will stop."))
        {
            throw new InvalidOperationException("Required Sakura card visual patches failed.");
        }
    }

    private static void MarkRequiredPatcherFailed()
    {
        MainFile.Logger.Error("Required Sakura card visual patcher failed.");
    }

    internal static ModPatchTarget NCardUpdateVisualsTarget() =>
        PatchTarget.Method<NCard>(
            nameof(NCard.UpdateVisuals),
            typeof(PileType),
            typeof(CardPreviewMode));

    internal static ModPatchTarget NCardReloadTarget() =>
        PatchTarget.Method<NCard>("Reload", Type.EmptyTypes);

    internal static ModPatchTarget NCardOnEnchantmentChangedTarget() =>
        PatchTarget.Method<NCard>("OnEnchantmentChanged", Type.EmptyTypes);
}

internal sealed class SakuraCardReloadPortraitSynchronizationPatch : IPatchMethod
{
    public static string PatchId => "sakura_card_reload_portrait_synchronization";
    public static string Description => "Synchronize both pooled NCard portrait slots after Reload";
    public static bool IsCritical => true;

    public static ModPatchTarget[] GetTargets() =>
    [
        SakuraCardVisualPatchRegistration.NCardReloadTarget()
    ];

    public static void Postfix(NCard __instance)
    {
        SakuraCardVisualLifecycle.AfterCardReload(__instance);
    }
}

internal sealed class SakuraCardUpdateVisualsPatch : IPatchMethod
{
    public static string PatchId => "sakura_card_visual_update_visuals";
    public static string Description => "Restore and apply Sakura card visuals around NCard.UpdateVisuals";
    public static bool IsCritical => true;

    public static ModPatchTarget[] GetTargets() =>
    [
        SakuraCardVisualPatchRegistration.NCardUpdateVisualsTarget()
    ];

    [HarmonyPriority(Priority.First)]
    public static void Prefix(NCard __instance)
    {
        SakuraCardLifecycle.BeforeCardUpdateVisuals(__instance);
    }

    public static void Postfix(NCard __instance)
    {
        SakuraCardLifecycle.AfterCardUpdateVisuals(__instance);
    }
}

internal sealed class SakuraCardEnchantmentChangedGeometryPatch : IPatchMethod
{
    public static string PatchId => "sakura_card_enchantment_changed_geometry";
    public static string Description => "Restore Sakura card enchantment tab geometry after a live enchantment change";
    public static bool IsCritical => true;

    public static ModPatchTarget[] GetTargets() =>
    [
        SakuraCardVisualPatchRegistration.NCardOnEnchantmentChangedTarget()
    ];

    public static void Postfix(NCard __instance)
    {
        SakuraCardGeometryLifecycle.AfterNativeEnchantmentChanged(__instance);
    }
}

internal sealed class SakuraGeneratedTransparentCardUpdateVisualsPatch : IPatchMethod
{
    public static string PatchId => "sakura_generated_transparent_card_update_visuals";
    public static string Description => "Reapply generated Transparent Card visuals after other UpdateVisuals postfixes";
    public static bool IsCritical => true;

    public static ModPatchTarget[] GetTargets() =>
    [
        SakuraCardVisualPatchRegistration.NCardUpdateVisualsTarget()
    ];

    [HarmonyPriority(Priority.Last)]
    public static void Postfix(NCard __instance)
    {
        SakuraCardLifecycle.AfterGeneratedTransparentCardUpdateVisuals(__instance);
    }
}

internal sealed class KinomotoSakuraCardRewardGlowPatch : IPatchMethod
{
    public static string PatchId => "sakura_card_visual_kinomoto_reward_glow";
    public static string Description => "Apply Kinomoto card visuals after reward glow activation";
    public static bool IsCritical => true;

    public static ModPatchTarget[] GetTargets() =>
    [
        PatchTarget.Method<NCard>(nameof(NCard.ActivateRewardScreenGlow), Type.EmptyTypes)
    ];

    public static void Postfix(NCard __instance)
    {
        SakuraCardLifecycle.AfterCardRewardGlow(__instance);
    }
}

internal sealed class KinomotoSakuraCardCurrentSizePatch : IPatchMethod
{
    public static string PatchId => "sakura_card_visual_kinomoto_current_size";
    public static string Description => "Override current card size for Kinomoto full-card layouts";
    public static bool IsCritical => true;

    public static ModPatchTarget[] GetTargets() =>
    [
        PatchTarget.Method<NCard>(nameof(NCard.GetCurrentSize), Type.EmptyTypes)
    ];

    public static void Postfix(NCard __instance, ref Vector2 __result)
    {
        SakuraCardGeometryLifecycle.OverrideCurrentSize(__instance, ref __result);
    }
}

internal sealed class KinomotoSakuraCardHolderSetCardPatch : IPatchMethod
{
    public static string PatchId => "sakura_card_visual_kinomoto_holder_set_card";
    public static string Description => "Restore and apply Kinomoto card holder visuals when SetCard runs";
    public static bool IsCritical => true;

    public static ModPatchTarget[] GetTargets() =>
    [
        PatchTarget.Method<NCardHolder>("SetCard", typeof(NCard))
    ];

    public static void Prefix(NCardHolder __instance)
    {
        SakuraCardVisualLifecycle.RestoreHolderVisuals(__instance);
    }

    public static void Postfix(NCardHolder __instance)
    {
        SakuraCardLifecycle.AfterHolderCardChanged(__instance);
    }
}

internal sealed class KinomotoSakuraCardHolderReassignedPatch : IPatchMethod
{
    public static string PatchId => "sakura_card_visual_kinomoto_holder_reassigned";
    public static string Description => "Restore and apply Kinomoto card holder visuals when a card is reassigned";
    public static bool IsCritical => true;

    public static ModPatchTarget[] GetTargets() =>
    [
        PatchTarget.Method<NCardHolder>("OnCardReassigned", Type.EmptyTypes)
    ];

    public static void Prefix(NCardHolder __instance)
    {
        SakuraCardVisualLifecycle.RestoreHolderVisuals(__instance);
    }

    public static void Postfix(NCardHolder __instance)
    {
        SakuraCardLifecycle.AfterHolderCardChanged(__instance);
    }
}

internal sealed class KinomotoSakuraPlayerHandRefreshLayoutPatch : IPatchMethod
{
    public static string PatchId => "sakura_card_visual_kinomoto_hand_refresh_layout";
    public static string Description => "Apply Kinomoto hand holder visuals around hand layout refresh";
    public static bool IsCritical => true;

    public static ModPatchTarget[] GetTargets() =>
    [
        PatchTarget.Method<NPlayerHand>("RefreshLayout", Type.EmptyTypes)
    ];

    public static void Prefix(NPlayerHand __instance)
    {
        SakuraCardLifecycle.BeforeHandRefreshLayout(__instance);
    }

    public static void Postfix(NPlayerHand __instance)
    {
        SakuraCardLifecycle.AfterHandRefreshLayout(__instance);
    }
}

internal sealed class KinomotoSakuraHandHolderReadyPatch : IPatchMethod
{
    public static string PatchId => "sakura_card_visual_kinomoto_hand_holder_ready";
    public static string Description => "Apply Kinomoto hand holder visuals after hand holder readiness";
    public static bool IsCritical => true;

    public static ModPatchTarget[] GetTargets() =>
    [
        PatchTarget.Method<NHandCardHolder>(nameof(NHandCardHolder._Ready), Type.EmptyTypes)
    ];

    public static void Postfix(NHandCardHolder __instance)
    {
        SakuraCardLifecycle.AfterHolderGeometryChanged(__instance);
    }
}

internal sealed class KinomotoSakuraHandHolderSetCardPatch : IPatchMethod
{
    public static string PatchId => "sakura_card_visual_kinomoto_hand_holder_set_card";
    public static string Description => "Prepare generated Transparent hand holder visuals before SetCard";
    public static bool IsCritical => true;

    public static ModPatchTarget[] GetTargets() =>
    [
        PatchTarget.Method<NHandCardHolder>("SetCard", typeof(NCard))
    ];

    public static void Prefix(NCard node)
    {
        SakuraGeneratedCardLifecycle.BeforeGeneratedTransparentHandHolderSetCard(node);
    }
}

internal sealed class KinomotoSakuraHandHolderUpdateCardPatch : IPatchMethod
{
    public static string PatchId => "sakura_card_visual_kinomoto_hand_holder_update_card";
    public static string Description => "Apply Kinomoto hand holder visuals after UpdateCard";
    public static bool IsCritical => true;

    public static ModPatchTarget[] GetTargets() =>
    [
        PatchTarget.Method<NHandCardHolder>(nameof(NHandCardHolder.UpdateCard), Type.EmptyTypes)
    ];

    public static void Postfix(NHandCardHolder __instance)
    {
        SakuraCardLifecycle.AfterHolderGeometryChanged(__instance);
    }
}

internal sealed class KinomotoSakuraHandHolderSetDefaultTargetsPatch : IPatchMethod
{
    public static string PatchId => "sakura_card_visual_kinomoto_hand_holder_set_default_targets";
    public static string Description => "Apply Kinomoto hand holder visuals after default targets are set";
    public static bool IsCritical => true;

    public static ModPatchTarget[] GetTargets() =>
    [
        PatchTarget.Method<NHandCardHolder>(nameof(NHandCardHolder.SetDefaultTargets), Type.EmptyTypes)
    ];

    public static void Postfix(NHandCardHolder __instance)
    {
        SakuraCardLifecycle.AfterHolderGeometryChanged(__instance);
    }
}

internal sealed class KinomotoSakuraPreviewHolderReadyPatch : IPatchMethod
{
    public static string PatchId => "sakura_card_visual_kinomoto_preview_holder_ready";
    public static string Description => "Apply Kinomoto preview holder visuals after preview holder readiness";
    public static bool IsCritical => true;

    public static ModPatchTarget[] GetTargets() =>
    [
        PatchTarget.Method<NPreviewCardHolder>(nameof(NPreviewCardHolder._Ready), Type.EmptyTypes)
    ];

    public static void Postfix(NPreviewCardHolder __instance)
    {
        SakuraCardLifecycle.AfterHolderGeometryChanged(__instance);
    }
}

internal sealed class KinomotoSakuraPreviewHolderSetCardScalePatch : IPatchMethod
{
    public static string PatchId => "sakura_card_visual_kinomoto_preview_holder_set_card_scale";
    public static string Description => "Apply Kinomoto preview holder visuals after card scale changes";
    public static bool IsCritical => true;

    public static ModPatchTarget[] GetTargets() =>
    [
        PatchTarget.Method<NPreviewCardHolder>(nameof(NPreviewCardHolder.SetCardScale), typeof(Vector2))
    ];

    public static void Postfix(NPreviewCardHolder __instance)
    {
        SakuraCardLifecycle.AfterHolderGeometryChanged(__instance);
    }
}

internal sealed class KinomotoSakuraCardGridSetCardsPatch : IPatchMethod
{
    public static string PatchId => "sakura_card_visual_kinomoto_grid_set_cards";
    public static string Description => "Apply Kinomoto grid card size before grid cards are set";
    public static bool IsCritical => true;

    public static ModPatchTarget[] GetTargets() =>
    [
        PatchTarget.Method<NCardGrid>(
            nameof(NCardGrid.SetCards),
            typeof(IReadOnlyList<CardModel>),
            typeof(PileType),
            typeof(List<SortingOrders>),
            typeof(Task))
    ];

    public static void Prefix(NCardGrid __instance, IReadOnlyList<CardModel> cardsToDisplay)
    {
        SakuraCardGeometryLifecycle.PrepareGrid(__instance, cardsToDisplay);
    }
}

internal sealed class KinomotoSakuraCardGridUpdatePositionsPatch : IPatchMethod
{
    public static string PatchId => "sakura_card_visual_kinomoto_grid_update_positions";
    public static string Description => "Center Kinomoto card grid rows after grid positions update";
    public static bool IsCritical => true;

    public static ModPatchTarget[] GetTargets() =>
    [
        PatchTarget.Method<NCardGrid>("UpdateGridPositions", typeof(int))
    ];

    public static void Postfix(NCardGrid __instance)
    {
        SakuraCardGeometryLifecycle.CenterGrid(__instance);
    }
}

internal sealed class KinomotoSakuraCardGridAllocateHoldersPatch : IPatchMethod
{
    public static string PatchId => "sakura_card_visual_kinomoto_grid_allocate_holders";
    public static string Description => "Reapply Kinomoto card grid rows after native grid holder reuse";
    public static bool IsCritical => true;

    public static ModPatchTarget[] GetTargets() =>
    [
        PatchTarget.Method<NCardGrid>("AllocateCardHolders", Type.EmptyTypes)
    ];

    public static void Postfix(NCardGrid __instance)
    {
        SakuraCardGeometryLifecycle.CenterGrid(__instance);
    }
}

internal sealed class KinomotoSakuraCardSelectionHighlightShowPatch : IPatchMethod
{
    public static string PatchId => "sakura_card_visual_kinomoto_highlight_show";
    public static string Description => "Apply Kinomoto selection highlight layer after show";
    public static bool IsCritical => true;

    public static ModPatchTarget[] GetTargets() =>
    [
        PatchTarget.Method<NCardHighlight>(nameof(NCardHighlight.AnimShow), Type.EmptyTypes)
    ];

    public static void Postfix(NCardHighlight __instance)
    {
        SakuraCardGeometryLifecycle.ApplySelectionHighlightLayer(__instance, selected: true);
    }
}

internal sealed class KinomotoSakuraCardSelectionHighlightHidePatch : IPatchMethod
{
    public static string PatchId => "sakura_card_visual_kinomoto_highlight_hide";
    public static string Description => "Restore Kinomoto selection highlight layer after hide";
    public static bool IsCritical => true;

    public static ModPatchTarget[] GetTargets() =>
    [
        PatchTarget.Method<NCardHighlight>(nameof(NCardHighlight.AnimHide), Type.EmptyTypes)
    ];

    public static void Postfix(NCardHighlight __instance)
    {
        SakuraCardGeometryLifecycle.ApplySelectionHighlightLayer(__instance, selected: false);
    }
}

internal sealed class KinomotoSakuraCardSelectionHighlightHideInstantlyPatch : IPatchMethod
{
    public static string PatchId => "sakura_card_visual_kinomoto_highlight_hide_instantly";
    public static string Description => "Restore Kinomoto selection highlight layer after instant hide";
    public static bool IsCritical => true;

    public static ModPatchTarget[] GetTargets() =>
    [
        PatchTarget.Method<NCardHighlight>(nameof(NCardHighlight.AnimHideInstantly), Type.EmptyTypes)
    ];

    public static void Postfix(NCardHighlight __instance)
    {
        SakuraCardGeometryLifecycle.ApplySelectionHighlightLayer(__instance, selected: false);
    }
}

internal sealed class SakuraOwnedCardUpdateVisualsRecoveryPatch : IPatchMethod
{
    public static string PatchId => "sakura_card_visual_update_visuals_recovery";
    public static string Description => "Recover disposed Sakura card visual resources during NCard.UpdateVisuals";
    public static bool IsCritical => true;

    public static ModPatchTarget[] GetTargets() =>
    [
        SakuraCardVisualPatchRegistration.NCardUpdateVisualsTarget()
    ];

    [HarmonyPriority(Priority.First)]
    public static void Prefix(out SakuraCardVisualRecoveryState __state)
    {
        __state = default;
    }

    [HarmonyPriority(Priority.First)]
    public static void Postfix(ref SakuraCardVisualRecoveryState __state)
    {
        __state.OriginalCompleted = true;
    }

    public static Exception? Finalizer(
        NCard __instance,
        Exception? __exception,
        SakuraCardVisualRecoveryState __state)
    {
        return SakuraCardVisualLifecycle.RecoverCardVisuals(
            __instance,
            __exception,
            nameof(NCard.UpdateVisuals),
            __state.OriginalCompleted);
    }
}

internal sealed class SakuraOwnedCardEnterTreeRecoveryPatch : IPatchMethod
{
    public static string PatchId => "sakura_card_visual_enter_tree_recovery";
    public static string Description => "Recover disposed Sakura card visual resources during NCard._EnterTree";
    public static bool IsCritical => true;

    public static ModPatchTarget[] GetTargets() =>
    [
        PatchTarget.Method<NCard>(nameof(NCard._EnterTree), Type.EmptyTypes)
    ];

    [HarmonyPriority(Priority.First)]
    public static void Prefix(out SakuraCardVisualRecoveryState __state)
    {
        __state = default;
    }

    [HarmonyPriority(Priority.First)]
    public static void Postfix(ref SakuraCardVisualRecoveryState __state)
    {
        __state.OriginalCompleted = true;
    }

    public static Exception? Finalizer(
        NCard __instance,
        Exception? __exception,
        SakuraCardVisualRecoveryState __state)
    {
        return SakuraCardVisualLifecycle.RecoverCardVisuals(
            __instance,
            __exception,
            nameof(NCard._EnterTree),
            __state.OriginalCompleted);
    }
}

internal struct SakuraCardVisualRecoveryState
{
    public bool OriginalCompleted { get; set; }
}
