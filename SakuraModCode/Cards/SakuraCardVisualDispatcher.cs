using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using SakuraMod.SakuraModCode.Classic.Cards;

namespace SakuraMod.SakuraModCode.Cards;

internal static class SakuraCardVisualDispatcher
{
    public static void BeforeCardUpdateVisualsIsolation(NCard card)
    {
        SakuraCardVisualLifecycle.BeforeCardUpdateVisualsIsolation(card);
    }

    public static Exception? RecoverCardVisuals(NCard card, Exception? exception, string source)
    {
        return SakuraCardVisualLifecycle.RecoverCardVisuals(card, exception, source);
    }

    public static void BeforeClearCardUpdateVisuals(NCard card)
    {
        SakuraCardVisualLifecycle.BeforeCardUpdateVisuals(card, SakuraCardVisualFamily.Clear);
    }

    public static void AfterClearCardUpdateVisuals(NCard card)
    {
        SakuraCardVisualLifecycle.AfterCardUpdateVisuals(card, SakuraCardVisualFamily.Clear);
    }

    public static void AfterClearCardRewardGlow(NCard card)
    {
        SakuraCardVisualLifecycle.AfterCardRewardGlow(card, SakuraCardVisualFamily.Clear);
    }

    public static void OverrideClearCurrentSize(NCard card, ref Vector2 size)
    {
        SakuraCardVisualLifecycle.OverrideCurrentSize(card, SakuraCardVisualFamily.Clear, ref size);
    }

    public static void BeforeClearHolderCardChanged(NCardHolder holder)
    {
        SakuraCardVisualLifecycle.BeforeHolderCardChanged(holder, SakuraCardVisualFamily.Clear);
    }

    public static void AfterClearHolderCardChanged(NCardHolder holder)
    {
        SakuraCardVisualLifecycle.AfterHolderCardChanged(holder, SakuraCardVisualFamily.Clear);
    }

    public static void BeforeClearHandRefreshLayout(NPlayerHand hand)
    {
        SakuraCardVisualLifecycle.BeforeHandRefreshLayout(hand, SakuraCardVisualFamily.Clear);
    }

    public static void AfterClearHandRefreshLayout(NPlayerHand hand)
    {
        SakuraCardVisualLifecycle.AfterHandRefreshLayout(hand, SakuraCardVisualFamily.Clear);
    }

    public static void AfterClearHandHolderUpdated(NHandCardHolder holder)
    {
        SakuraCardVisualLifecycle.AfterHandHolderUpdated(holder, SakuraCardVisualFamily.Clear);
    }

    public static void AfterClearPreviewHolderUpdated(NPreviewCardHolder holder)
    {
        SakuraCardVisualLifecycle.AfterPreviewHolderUpdated(holder, SakuraCardVisualFamily.Clear);
    }

    public static void BeforeClearGridSetCards(NCardGrid grid, IReadOnlyList<CardModel> cards)
    {
        SakuraCardVisualLifecycle.BeforeGridSetCards(grid, cards, SakuraCardVisualFamily.Clear);
    }

    public static void AfterClearGridPositionsUpdated(NCardGrid grid)
    {
        SakuraCardVisualLifecycle.AfterGridPositionsUpdated(grid, SakuraCardVisualFamily.Clear);
    }

    public static void AfterClearSelectionHighlightChanged(NCardHighlight highlight, bool selected)
    {
        SakuraCardVisualLifecycle.AfterSelectionHighlightChanged(highlight, selected, SakuraCardVisualFamily.Clear);
    }

    public static void BeforeClassicCardUpdateVisuals(NCard card)
    {
        SakuraCardVisualLifecycle.BeforeCardUpdateVisuals(card, SakuraCardVisualFamily.Classic);
    }

    public static void AfterClassicCardUpdateVisuals(NCard card)
    {
        SakuraCardVisualLifecycle.AfterCardUpdateVisuals(card, SakuraCardVisualFamily.Classic);
    }

    public static void AfterClassicCardRewardGlow(NCard card)
    {
        SakuraCardVisualLifecycle.AfterCardRewardGlow(card, SakuraCardVisualFamily.Classic);
    }

    public static void OverrideClassicCurrentSize(NCard card, ref Vector2 size)
    {
        SakuraCardVisualLifecycle.OverrideCurrentSize(card, SakuraCardVisualFamily.Classic, ref size);
    }

    public static void BeforeClassicHolderCardChanged(NCardHolder holder)
    {
        SakuraCardVisualLifecycle.BeforeHolderCardChanged(holder, SakuraCardVisualFamily.Classic);
    }

    public static void AfterClassicHolderCardChanged(NCardHolder holder)
    {
        SakuraCardVisualLifecycle.AfterHolderCardChanged(holder, SakuraCardVisualFamily.Classic);
    }

    public static void BeforeClassicHandRefreshLayout(NPlayerHand hand)
    {
        SakuraCardVisualLifecycle.BeforeHandRefreshLayout(hand, SakuraCardVisualFamily.Classic);
    }

    public static void AfterClassicHandRefreshLayout(NPlayerHand hand)
    {
        SakuraCardVisualLifecycle.AfterHandRefreshLayout(hand, SakuraCardVisualFamily.Classic);
    }

    public static void AfterClassicHandHolderUpdated(NHandCardHolder holder)
    {
        SakuraCardVisualLifecycle.AfterHandHolderUpdated(holder, SakuraCardVisualFamily.Classic);
    }

    public static void AfterClassicPreviewHolderUpdated(NPreviewCardHolder holder)
    {
        SakuraCardVisualLifecycle.AfterPreviewHolderUpdated(holder, SakuraCardVisualFamily.Classic);
    }

    public static void BeforeClassicGridSetCards(NCardGrid grid, IReadOnlyList<CardModel> cards)
    {
        SakuraCardVisualLifecycle.BeforeGridSetCards(grid, cards, SakuraCardVisualFamily.Classic);
    }

    public static void AfterClassicGridPositionsUpdated(NCardGrid grid)
    {
        SakuraCardVisualLifecycle.AfterGridPositionsUpdated(grid, SakuraCardVisualFamily.Classic);
    }

    public static void AfterClassicSelectionHighlightChanged(NCardHighlight highlight, bool selected)
    {
        SakuraCardVisualLifecycle.AfterSelectionHighlightChanged(highlight, selected, SakuraCardVisualFamily.Classic);
    }
}

internal static class SakuraCardVisualLifecycle
{
    private const string RecoverableObjectName = "Godot.CompressedTexture2D";

    public static void BeforeCardUpdateVisualsIsolation(NCard card)
    {
        RestoreStaleCardVisuals(card, SakuraCardVisualFamilies.Family(card));
    }

    public static Exception? RecoverCardVisuals(NCard card, Exception? exception, string source)
    {
        var family = SakuraCardVisualFamilies.Family(card);
        if (exception is null
            || !IsRecoverableGodotObjectLifetimeException(exception)
            || family == SakuraCardVisualFamily.Vanilla)
            return exception;

        try
        {
            ApplyCardVisualsForRecovery(card, family);
            MainFile.Logger.Warn(
                $"Recovered disposed card texture during {source} for {card.Model?.Id} ({family}).");
            return null;
        }
        catch (Exception recoveryException)
        {
            MainFile.Logger.Error(
                $"Failed to recover disposed card texture during {source} for {card.Model?.Id} ({family}): {recoveryException}");
            return exception;
        }
    }

    private static void RestoreStaleCardVisuals(NCard card, SakuraCardVisualFamily currentFamily)
    {
        if (currentFamily != SakuraCardVisualFamily.Clear)
            RestoreCardIfTracked(card, SakuraCardVisualFamily.Clear);
        if (currentFamily != SakuraCardVisualFamily.Classic)
            RestoreCardIfTracked(card, SakuraCardVisualFamily.Classic);

        if (currentFamily is not SakuraCardVisualFamily.Clear and not SakuraCardVisualFamily.Classic)
            SakuraNonClearFrameApplier.RestoreTrackedAndCurrentModelVisuals(card);
    }

    private static void RestoreCardIfTracked(NCard card, SakuraCardVisualFamily family)
    {
        switch (family)
        {
            case SakuraCardVisualFamily.Clear:
                ClearCardLayout.RestoreCardIfTracked(card);
                break;
            case SakuraCardVisualFamily.Classic:
                ClassicSakuraCardLayout.RestoreCardIfTracked(card);
                break;
        }
    }

    private static void RestoreHolderIfTracked(NCardHolder holder, SakuraCardVisualFamily family)
    {
        switch (family)
        {
            case SakuraCardVisualFamily.Clear:
                ClearCardLayout.RestoreHolderIfTracked(holder);
                break;
            case SakuraCardVisualFamily.Classic:
                ClassicSakuraCardLayout.RestoreHolderIfTracked(holder);
                break;
        }
    }

    private static void ApplyCardVisuals(NCard card, SakuraCardVisualFamily family)
    {
        switch (family)
        {
            case SakuraCardVisualFamily.Clear:
                ClearCardLayout.Apply(card);
                break;
            case SakuraCardVisualFamily.Classic:
                ClassicSakuraCardLayout.Apply(card);
                break;
        }
    }

    private static void ApplyCardVisualsForRecovery(NCard card, SakuraCardVisualFamily family)
    {
        switch (family)
        {
            case SakuraCardVisualFamily.Clear:
                ClearCardLayout.Apply(card);
                break;
            case SakuraCardVisualFamily.Kinomoto:
                SakuraNonClearFrameApplier.ApplyTextureRecovery(card);
                break;
            case SakuraCardVisualFamily.Classic:
                ClassicSakuraCardLayout.Apply(card);
                break;
        }
    }

    public static void BeforeCardUpdateVisuals(NCard card, SakuraCardVisualFamily family)
    {
        RestoreCardIfTracked(card, family);
    }

    public static void AfterCardUpdateVisuals(NCard card, SakuraCardVisualFamily family)
    {
        ApplyCardVisuals(card, family);
    }

    public static void AfterCardRewardGlow(NCard card, SakuraCardVisualFamily family)
    {
        ApplyCardVisuals(card, family);
    }

    public static void OverrideCurrentSize(NCard card, SakuraCardVisualFamily family, ref Vector2 size)
    {
        switch (family)
        {
            case SakuraCardVisualFamily.Clear when SakuraCardVisualFamilies.IsClear(card):
                size = ClearCardLayout.CurrentSize(card);
                break;
            case SakuraCardVisualFamily.Classic when SakuraCardVisualFamilies.IsClassic(card):
                size = ClassicSakuraCardLayout.CurrentSize(card);
                break;
        }
    }

    public static void BeforeHolderCardChanged(NCardHolder holder, SakuraCardVisualFamily family)
    {
        RestoreHolderIfTracked(holder, family);
    }

    public static void AfterHolderCardChanged(NCardHolder holder, SakuraCardVisualFamily family)
    {
        ApplyHolderVisuals(holder, family);
    }

    public static void BeforeHandRefreshLayout(NPlayerHand hand, SakuraCardVisualFamily family)
    {
        ApplyHandHolderVisuals(hand.ActiveHolders, family);
    }

    public static void AfterHandRefreshLayout(NPlayerHand hand, SakuraCardVisualFamily family)
    {
        ApplyHandHolderVisuals(hand.ActiveHolders, family);
        ApplyHandSpacing(hand, family);
    }

    public static void AfterHandHolderUpdated(NHandCardHolder holder, SakuraCardVisualFamily family)
    {
        ApplyHolderVisuals(holder, family);
    }

    public static void AfterPreviewHolderUpdated(NPreviewCardHolder holder, SakuraCardVisualFamily family)
    {
        ApplyHolderVisuals(holder, family);
    }

    public static void BeforeGridSetCards(NCardGrid grid, IReadOnlyList<CardModel> cards, SakuraCardVisualFamily family)
    {
        ApplyGridCardSize(grid, cards, family);
    }

    public static void AfterGridPositionsUpdated(NCardGrid grid, SakuraCardVisualFamily family)
    {
        CenterGridRows(grid, family);
    }

    public static void AfterSelectionHighlightChanged(
        NCardHighlight highlight,
        bool selected,
        SakuraCardVisualFamily family)
    {
        ApplySelectionHighlightLayer(highlight, selected, family);
    }

    private static void ApplyHolderVisuals(NCardHolder holder, SakuraCardVisualFamily family)
    {
        switch (family)
        {
            case SakuraCardVisualFamily.Clear:
                ClearCardLayout.Apply(holder);
                break;
            case SakuraCardVisualFamily.Classic:
                ClassicSakuraCardLayout.Apply(holder);
                break;
        }
    }

    private static void ApplyHandHolderVisuals(IEnumerable<NHandCardHolder> holders, SakuraCardVisualFamily family)
    {
        switch (family)
        {
            case SakuraCardVisualFamily.Clear:
                ClearCardLayout.Apply(holders);
                break;
            case SakuraCardVisualFamily.Classic:
                ClassicSakuraCardLayout.Apply(holders);
                break;
        }
    }

    private static void ApplyGridCardSize(
        NCardGrid grid,
        IReadOnlyList<CardModel> cards,
        SakuraCardVisualFamily family)
    {
        switch (family)
        {
            case SakuraCardVisualFamily.Clear:
                ClearCardLayout.ApplyGridCardSize(grid, cards);
                break;
            case SakuraCardVisualFamily.Classic:
                ClassicSakuraCardLayout.ApplyGridCardSize(grid, cards);
                break;
        }
    }

    private static void CenterGridRows(NCardGrid grid, SakuraCardVisualFamily family)
    {
        switch (family)
        {
            case SakuraCardVisualFamily.Clear:
                ClearCardLayout.CenterGridRows(grid);
                break;
            case SakuraCardVisualFamily.Classic:
                ClassicSakuraCardLayout.CenterGridRows(grid);
                break;
        }
    }

    private static void ApplyHandSpacing(NPlayerHand hand, SakuraCardVisualFamily family)
    {
        switch (family)
        {
            case SakuraCardVisualFamily.Clear:
                ClearCardLayout.ApplyHandSpacing(hand);
                break;
            case SakuraCardVisualFamily.Classic:
                ClassicSakuraCardLayout.ApplyHandSpacing(hand);
                break;
        }
    }

    private static void ApplySelectionHighlightLayer(
        NCardHighlight highlight,
        bool selected,
        SakuraCardVisualFamily family)
    {
        switch (family)
        {
            case SakuraCardVisualFamily.Clear:
                ClearCardLayout.ApplySelectionHighlightLayer(highlight, selected);
                break;
            case SakuraCardVisualFamily.Classic:
                ClassicSakuraCardLayout.ApplySelectionHighlightLayer(highlight, selected);
                break;
        }
    }

    private static bool IsRecoverableGodotObjectLifetimeException(Exception exception) =>
        exception is ObjectDisposedException { ObjectName: RecoverableObjectName }
        || exception.InnerException is not null && IsRecoverableGodotObjectLifetimeException(exception.InnerException);
}
