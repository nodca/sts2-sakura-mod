using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using SakuraMod.SakuraModCode.Cards;
using System.Runtime.CompilerServices;

namespace SakuraMod.SakuraModCode.Cards;

internal static class SakuraCardLifecycle
{
    public static void BeforeCardUpdateVisuals(NCard card)
    {
        SakuraCardVisualLifecycle.IsolateCardVisuals(card);
        SakuraCardVisualLifecycle.BeforeCardUpdateVisuals(card);
    }

    public static void AfterCardUpdateVisuals(NCard card)
    {
        SakuraCardVisualLifecycle.AfterCardUpdateVisuals(card);
        SakuraForgottenOverlay.Sync(card);
        ApplyParentHolder(card);
    }

    public static void AfterCardRewardGlow(NCard card)
    {
        SakuraCardVisualLifecycle.AfterCardRewardGlow(card);
        ApplyParentHolder(card);
    }

    public static void AfterGeneratedTransparentCardUpdateVisuals(NCard card)
    {
        if (SakuraGeneratedCardLifecycle.IsGeneratedTransparentHandVisualCard(card.Model))
            SakuraCardVisualLifecycle.AfterCardUpdateVisuals(card);
    }

    public static void AfterHolderCardChanged(NCardHolder holder) =>
        ApplyHolder(holder);

    public static void BeforeHandRefreshLayout(NPlayerHand hand) =>
        ApplyHandHolders(hand);

    public static void AfterHandRefreshLayout(NPlayerHand hand)
    {
        ApplyHandHolders(hand);
        SakuraCardGeometryLifecycle.ApplyHandSpacing(hand);
    }

    public static void AfterHolderGeometryChanged(NCardHolder holder) =>
        ApplyHolder(holder);

    public static void AfterCardReturnedFromPool(NCard card) =>
        SakuraForgottenOverlay.ResetForReuse(card);

    // Rides the native end-turn hover flash: every Forgotten hand card warns that it is about to leave.
    public static void AfterHandFlashPlayableHolders(NPlayerHand hand)
    {
        foreach (var holder in hand.ActiveHolders)
            SakuraForgottenOverlay.Pulse(holder.CardNode);
    }

    private static void ApplyParentHolder(NCard card)
    {
        if (card.GetParent() is NCardHolder holder)
            ApplyHolder(holder);
    }

    private static void ApplyHolders(IEnumerable<NHandCardHolder> holders)
    {
        foreach (var holder in holders)
            ApplyHolder(holder);
    }

    private static void ApplyHandHolders(NPlayerHand hand)
    {
        ApplyHolders(hand.ActiveHolders);
        if (DetachedCurrentCardPlayHolder(hand) is { } currentHolder)
            ApplyHandDescriptionVisibility(currentHolder);
    }

    private static void ApplyHolder(NCardHolder holder)
    {
        if (!SakuraCardVisualLifecycle.TryBeginHolderVisuals(holder, out var mutation))
            return;

        SakuraCardGeometryLifecycle.ApplyHolder(holder, mutation.Ledger);
        SakuraCardVisualLifecycle.ApplyHolderVisuals(holder, mutation.Ledger);
        SakuraCardVisualLifecycle.CompleteHolderVisuals(mutation);
        ApplyHandDescriptionVisibility(holder);
    }

    private static void ApplyHandDescriptionVisibility(NCardHolder holder)
    {
        if (holder is not NHandCardHolder handHolder
            || NPlayerHand.Instance is not { } hand)
        {
            return;
        }

        var isActiveHandHolder =
            ReferenceEquals(handHolder.GetParent(), hand.CardHolderContainer)
            && handHolder.IsVisibleInTree();
        var isCurrentCardPlay =
            ReferenceEquals(DetachedCurrentCardPlayHolder(hand), handHolder);
        if (!isActiveHandHolder && !isCurrentCardPlay)
            return;

        var isFocused = ReferenceEquals(hand.FocusedHolder, handHolder);
        SakuraCardVisualLifecycle.ApplyDescriptionVisibility(
            holder.CardNode,
            SakuraDescriptionRegion.ShouldShow(
                isInCombatHand: true,
                isFocused,
                isCurrentCardPlay));
    }

    private static NHandCardHolder? DetachedCurrentCardPlayHolder(NPlayerHand hand)
    {
        var holder = hand.GetChildren().OfType<NCardPlay>().FirstOrDefault()?.Holder;
        return ReferenceEquals(holder?.GetParent(), hand) ? holder : null;
    }
}

internal static class SakuraCardVisualLifecycle
{
    private const string RecoverableObjectName = "Godot.CompressedTexture2D";
    private static readonly object RecoveryLogLock = new();
    private static readonly HashSet<string> RecoveryLogKeys = [];
    private static readonly ConditionalWeakTable<NCard, SakuraCardFaceState> FaceStates = new();

    internal static ISakuraCardLayoutAdapter? AdapterFor(SakuraCardVisualLayout layout) =>
        layout switch
        {
            SakuraCardVisualLayout.Clear => ClearCardLayout.Instance,
            SakuraCardVisualLayout.Classic => ClassicCardLayout.Instance,
            _ => null
        };

    public static void AfterCardReload(NCard card)
    {
        _ = SakuraCardVisualInfrastructure.TrySynchronizeCurrentModelPortraits(card);
    }

    public static void IsolateCardVisuals(NCard card)
    {
        if (FaceStates.TryGetValue(card, out var state)
            && !ReferenceEquals(state.Adapter, AdapterFor(SakuraCardVisualFamilies.Layout(card))))
        {
            state.Restore(card);
        }
    }

    public static void RestoreHolderVisuals(NCardHolder holder)
    {
        var ledger = SakuraCardMutationLedgers.For(holder);
        ledger.Restore(SakuraCardRendererId.Clear);
        ledger.Restore(SakuraCardRendererId.Classic);
    }

    public static bool TryBeginHolderVisuals(
        NCardHolder holder,
        out SakuraHolderVisualMutation mutation)
    {
        mutation = default;
        if (!holder.IsNodeReady())
            return false;

        var layout = SakuraCardVisualFamilies.Layout(holder.CardNode);
        if (!SakuraCardGeometry.TryProfile(layout, out _))
        {
            RestoreHolderVisuals(holder);
            return false;
        }

        var renderer = AdapterFor(layout)!.Id;
        var ledger = SakuraCardMutationLedgers.For(holder);
        ledger.Begin(renderer);
        mutation = new SakuraHolderVisualMutation(ledger, renderer);
        return true;
    }

    public static void ApplyHolderVisuals(
        NCardHolder holder,
        SakuraCardMutationLedger ledger)
    {
        if (AdapterFor(SakuraCardVisualFamilies.Layout(holder.CardNode)) is { } adapter)
            SakuraCardFace.ApplyHolder(holder, ledger, adapter);
    }

    public static void ApplyDescriptionVisibility(NCard? card, bool visible)
    {
        if (card is not null && FaceStates.TryGetValue(card, out var state))
            SakuraCardFace.ApplyDescriptionVisibility(card, state, visible);
    }

    public static void CompleteHolderVisuals(SakuraHolderVisualMutation mutation)
    {
        mutation.Ledger.MarkApplied(mutation.Renderer);
    }

    public static Exception? RecoverCardVisuals(
        NCard card,
        Exception? exception,
        string source,
        bool originalCompleted)
    {
        if (!originalCompleted
            || exception is null
            || !IsRecoverableGodotObjectLifetimeException(exception))
        {
            return exception;
        }

        try
        {
            if (!TryRestoreStaticCardVisuals(card, source))
                return exception;

            LogRecoveryOnce(card, source, "recovered", null);
            return null;
        }
        catch (Exception recoveryException)
        {
            LogRecoveryOnce(card, source, "failed", recoveryException);
            return exception;
        }
    }

    private static bool TryRestoreStaticCardVisuals(NCard card, string source)
    {
        if (!SakuraCardVisualInfrastructure.TrySynchronizeCurrentModelPortraits(card))
            return false;

        var owner = SakuraCardVisualFamilies.ContentOwner(card);
        var layout = SakuraCardVisualFamilies.Layout(card);
        if (owner == SakuraCardContentOwner.Vanilla)
            return layout == SakuraCardVisualLayout.None;
        if (layout == SakuraCardVisualLayout.None)
            return true;

        return FaceStates.TryGetValue(card, out var state)
            && ReferenceEquals(state.Adapter, AdapterFor(layout))
            && SakuraCardFace.TryRestoreOwnedTexturesForRecovery(card, state);
    }

    private static void LogRecoveryOnce(
        NCard card,
        string source,
        string outcome,
        Exception? recoveryException)
    {
        var owner = SakuraCardVisualFamilies.ContentOwner(card);
        var layout = SakuraCardVisualFamilies.Layout(card);
        var modelId = card.Model?.Id.ToString() ?? "null";
        var key = $"{outcome}|{source}|{modelId}|{owner}|{layout}";
        lock (RecoveryLogLock)
        {
            if (!RecoveryLogKeys.Add(key))
                return;
        }

        if (recoveryException is null)
        {
            MainFile.Logger.Warn(
                $"Recovered disposed card texture during {source} for {modelId} "
                + $"(owner={owner}, layout={layout}).");
            return;
        }

        MainFile.Logger.Error(
            $"Failed to recover disposed card texture during {source} for {modelId} "
            + $"(owner={owner}, layout={layout}): {recoveryException}");
    }

    private static void RestoreCardVisualsIfTracked(NCard card)
    {
        if (FaceStates.TryGetValue(card, out var state))
            state.Restore(card);
    }

    private static void ApplyCardVisuals(NCard card)
    {
        if (AdapterFor(SakuraCardVisualFamilies.Layout(card)) is not { } adapter)
        {
            RestoreCardVisualsIfTracked(card);
            return;
        }

        var state = FaceStates.GetOrCreateValue(card);
        state.Capture(card, adapter);
        SakuraCardFace.Apply(card, state);
        state.MarkApplied();
    }

    public static void BeforeCardUpdateVisuals(NCard card)
    {
        RestoreCardVisualsIfTracked(card);
        SakuraVanillaCardVisualRestorer.RestoreCurrentModelCostIfVanillaRoute(card);
    }

    public static void AfterCardUpdateVisuals(NCard card)
    {
        ApplyCardVisuals(card);
    }

    public static void AfterCardRewardGlow(NCard card)
    {
        ApplyCardVisuals(card);
    }

    private static bool IsRecoverableGodotObjectLifetimeException(Exception exception) =>
        exception is ObjectDisposedException { ObjectName: RecoverableObjectName }
        || exception.InnerException is not null && IsRecoverableGodotObjectLifetimeException(exception.InnerException);
}

internal readonly record struct SakuraHolderVisualMutation(
    SakuraCardMutationLedger Ledger,
    SakuraCardRendererId Renderer);
