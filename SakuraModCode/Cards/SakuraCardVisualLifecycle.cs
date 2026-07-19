using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using SakuraMod.SakuraModCode.Cards;

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

    public static void AfterCardReload(NCard card)
    {
        _ = SakuraCardVisualInfrastructure.TrySynchronizeCurrentModelPortraits(card);
    }

    public static void IsolateCardVisuals(NCard card)
    {
        RestoreStaleCardVisuals(card, SakuraCardVisualFamilies.Layout(card));
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

        var renderer = RendererId(layout);
        var ledger = SakuraCardMutationLedgers.For(holder);
        ledger.Begin(renderer);
        mutation = new SakuraHolderVisualMutation(ledger, renderer);
        return true;
    }

    public static void ApplyHolderVisuals(
        NCardHolder holder,
        SakuraCardMutationLedger ledger)
    {
        ClearCardLayout.ApplyHolderVisuals(holder, ledger);
        ClassicCardLayout.ApplyHolderVisuals(holder, ledger);
    }

    public static void ApplyDescriptionVisibility(NCard? card, bool visible)
    {
        ClearCardLayout.ApplyDescriptionVisibility(card, visible);
        ClassicCardLayout.ApplyDescriptionVisibility(card, visible);
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
        if (source == nameof(NCard._EnterTree) || layout == SakuraCardVisualLayout.None)
            return true;

        return layout switch
        {
            SakuraCardVisualLayout.Clear => ClearCardLayout.TryRestoreOwnedTexturesForRecovery(card),
            SakuraCardVisualLayout.Classic => ClassicCardLayout.TryRestoreOwnedTexturesForRecovery(card),
            _ => false,
        };
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

    private static void RestoreStaleCardVisuals(NCard card, SakuraCardVisualLayout currentLayout)
    {
        if (currentLayout != SakuraCardVisualLayout.Clear)
            ClearCardLayout.RestoreCardIfTracked(card);
        if (currentLayout != SakuraCardVisualLayout.Classic)
            ClassicCardLayout.RestoreCardIfTracked(card);
    }

    private static void RestoreAllCardVisualsIfTracked(NCard card)
    {
        ClearCardLayout.RestoreCardIfTracked(card);
        ClassicCardLayout.RestoreCardIfTracked(card);
    }

    private static void ApplyCardVisuals(NCard card)
    {
        ClearCardLayout.Apply(card);
        ClassicCardLayout.Apply(card);
    }

    public static void BeforeCardUpdateVisuals(NCard card)
    {
        RestoreAllCardVisualsIfTracked(card);
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

    private static SakuraCardRendererId RendererId(SakuraCardVisualLayout layout) =>
        layout switch
        {
            SakuraCardVisualLayout.Clear => SakuraCardRendererId.Clear,
            SakuraCardVisualLayout.Classic => SakuraCardRendererId.Classic,
            _ => throw new ArgumentOutOfRangeException(nameof(layout), layout, "Layout has no Sakura renderer.")
        };
}

internal readonly record struct SakuraHolderVisualMutation(
    SakuraCardMutationLedger Ledger,
    SakuraCardRendererId Renderer);
