using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using STS2RitsuLib.Patching;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace SakuraMod.SakuraModCode.Cards;

internal static class SakuraCardGeometryLifecycle
{
    private const SakuraControlProperty BoxProperties =
        SakuraControlProperty.Position
        | SakuraControlProperty.Size
        | SakuraControlProperty.CustomMinimumSize
        | SakuraControlProperty.Scale
        | SakuraControlProperty.PivotOffset;

    private static readonly FieldInfo? HolderHitboxField =
        PrivateAccess.DeclaredField(typeof(NCardHolder), "_hitbox");
    private static readonly FieldInfo? HandFlashField =
        PrivateAccess.DeclaredField(typeof(NHandCardHolder), "_flash");

    public static void BorrowCardGeometry(
        SakuraCardMutationLedger ledger,
        NCard card,
        IReadOnlyList<CanvasItem> hiddenNodes,
        SubViewport? transformVfxViewport)
    {
        ledger.Borrow(
            card,
            SakuraControlProperty.Size
            | SakuraControlProperty.CustomMinimumSize
            | SakuraControlProperty.PivotOffset);
        ledger.Borrow(card.Body, BoxProperties);
        ledger.Borrow(
            card.CardHighlight,
            BoxProperties
            | SakuraControlProperty.Anchors
            | SakuraControlProperty.ZIndex
            | SakuraControlProperty.MouseFilter
            | SakuraControlProperty.TextureExpandMode
            | SakuraControlProperty.TextureStretchMode);
        foreach (var hiddenNode in hiddenNodes)
        {
            ledger.Borrow(
                hiddenNode as Control,
                SakuraControlProperty.Position | SakuraControlProperty.Size);
        }

        ledger.BorrowViewportSize(transformVfxViewport);
    }

    public static void ApplyCardRoot(
        NCard card,
        Rect2 rootBox,
        Vector2 rootPivotOffset,
        SubViewport? transformVfxViewport)
    {
        if (!SakuraCardGeometry.TryProfile(SakuraCardVisualFamilies.Layout(card), out var profile))
            return;

        EnsureTransformVfxViewportFits(transformVfxViewport, profile.RootSize);
        SakuraCardVisualInfrastructure.ApplySize(card, profile.RootSize, rootPivotOffset);
        SakuraCardVisualInfrastructure.ApplyBox(card.Body, rootBox);
    }

    public static void ApplyCardHighlight(
        NCardHighlight highlight,
        Rect2 box,
        int zIndex)
    {
        SakuraCardVisualInfrastructure.ApplyTopLeftAnchors(highlight);
        if (highlight.ExpandMode != TextureRect.ExpandModeEnum.IgnoreSize)
            highlight.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        if (highlight.StretchMode != TextureRect.StretchModeEnum.Scale)
            highlight.StretchMode = TextureRect.StretchModeEnum.Scale;
        SakuraCardVisualInfrastructure.ApplyBox(highlight, box);
        if (highlight.ZIndex != zIndex)
            highlight.ZIndex = zIndex;
        if (highlight.MouseFilter != Control.MouseFilterEnum.Ignore)
            highlight.MouseFilter = Control.MouseFilterEnum.Ignore;
    }

    public static void OverrideCurrentSize(NCard card, ref Vector2 size)
    {
        if (SakuraCardGeometry.TryProfile(SakuraCardVisualFamilies.Layout(card), out var profile))
            size = profile.RootSize * card.Scale;
    }

    public static void ApplyHolder(
        NCardHolder holder,
        SakuraCardMutationLedger ledger)
    {
        var layout = SakuraCardVisualFamilies.Layout(holder.CardNode);
        if (!SakuraCardGeometry.TryProfile(layout, out var profile))
            return;

        ledger.Borrow(HolderHitbox(holder), BoxProperties);
        ledger.Borrow(
            HandFlash(holder),
            BoxProperties
            | SakuraControlProperty.MouseFilter
            | SakuraControlProperty.TextureExpandMode
            | SakuraControlProperty.TextureStretchMode);
        ledger.Borrow(holder.CardNode, SakuraControlProperty.Position);

        SakuraCardVisualInfrastructure.ApplyBox(HolderHitbox(holder), profile.CenteredRootBox);
        ApplyHandFlashGeometry(HandFlash(holder), profile.CenteredHighlightBox);
        if (holder.CardNode is { } card && card.Position != Vector2.Zero)
            card.Position = Vector2.Zero;
    }

    public static void ApplyHandSpacing(NPlayerHand hand)
    {
        SakuraHandLayout.Apply(hand);
    }

    public static void PrepareGrid(NCardGrid grid, IReadOnlyList<CardModel> cards)
    {
        SakuraCardVisualGrid.ApplyCardSize(grid, cards);
    }

    public static void CenterGrid(NCardGrid grid)
    {
        SakuraCardVisualGrid.CenterRows(grid);
    }

    public static void ApplySelectionHighlightLayer(NCardHighlight highlight, bool selected)
    {
        if (ParentCard(highlight) is not { } card
            || !SakuraCardGeometry.TryProfile(SakuraCardVisualFamilies.Layout(card), out var profile)
            || !HasAncestor<NCardGridSelectionScreen>(highlight))
        {
            return;
        }

        var targetZIndex = selected ? profile.SelectionHighlightZIndex : profile.HighlightZIndex;
        if (highlight.ZIndex != targetZIndex)
            highlight.ZIndex = targetZIndex;
    }

    public static void ResizeHoverTip(NHoverTipCardContainer container)
    {
        var tip = LastHoverTipControl(container);
        var card = tip?.GetNodeOrNull<NCard>("%Card");
        if (tip is null
            || card is null
            || SakuraCardVisualFamilies.Family(card) != SakuraCardVisualFamily.Kinomoto)
        {
            return;
        }

        var size = card.GetCurrentSize();
        if (size.X <= 0f || size.Y <= 0f)
            return;

        if (tip.Size != size)
            tip.Size = size;
        if (tip.CustomMinimumSize != size)
            tip.CustomMinimumSize = size;
        if (card.Position != Vector2.Zero)
            card.Position = Vector2.Zero;
    }

    private static void ApplyHandFlashGeometry(Control? flash, Rect2 box)
    {
        if (flash is TextureRect textureRect)
        {
            if (textureRect.ExpandMode != TextureRect.ExpandModeEnum.IgnoreSize)
                textureRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            if (textureRect.StretchMode != TextureRect.StretchModeEnum.Scale)
                textureRect.StretchMode = TextureRect.StretchModeEnum.Scale;
            if (textureRect.MouseFilter != Control.MouseFilterEnum.Ignore)
                textureRect.MouseFilter = Control.MouseFilterEnum.Ignore;
        }

        SakuraCardVisualInfrastructure.ApplyBox(flash, box);
    }

    private static void EnsureTransformVfxViewportFits(SubViewport? viewport, Vector2 rootSize)
    {
        if (viewport is null)
            return;

        var targetSize = new Vector2I(
            Mathf.CeilToInt(Mathf.Max(viewport.Size.X, NCard.defaultSize.X)),
            Mathf.CeilToInt(Mathf.Max(viewport.Size.Y, rootSize.Y)));
        if (viewport.Size != targetSize)
            viewport.Size = targetSize;
    }

    private static NClickableControl? HolderHitbox(NCardHolder holder) =>
        HolderHitboxField?.GetValue(holder) as NClickableControl;

    internal static Control? HandFlash(NCardHolder holder) =>
        holder is NHandCardHolder handHolder
            ? HandFlashField?.GetValue(handHolder) as Control
            : null;

    private static NCard? ParentCard(Node node)
    {
        for (var current = node.GetParent(); current is not null; current = current.GetParent())
        {
            if (current is NCard card)
                return card;
        }

        return null;
    }

    private static bool HasAncestor<T>(Node node)
        where T : Node
    {
        for (var current = node.GetParent(); current is not null; current = current.GetParent())
        {
            if (current is T)
                return true;
        }

        return false;
    }

    private static Control? LastHoverTipControl(NHoverTipCardContainer container)
    {
        for (var index = container.GetChildCount() - 1; index >= 0; index--)
        {
            if (container.GetChild(index) is Control control)
                return control;
        }

        return null;
    }
}

internal readonly record struct SakuraCardLayoutGeometry(
    Vector2 RootSize,
    Vector2 HighlightMargin,
    float GridPadding,
    float GridVerticalOffset,
    int HighlightZIndex,
    int SelectionHighlightZIndex)
{
    public Rect2 CenteredRootBox => new(RootSize * -0.5f, RootSize);
    public Rect2 HighlightBox => new(-HighlightMargin, RootSize + HighlightMargin * 2f);
    public Rect2 CenteredHighlightBox =>
        new(CenteredRootBox.Position - HighlightMargin, HighlightBox.Size);
}

internal readonly record struct SakuraHandLayoutProfile(
    Vector2 CardSize,
    float SameLayoutAdjustment,
    float MinimumAdjacentGap);

internal readonly record struct SakuraHandCardGeometry(
    SakuraCardVisualLayout Layout,
    float TargetAngleDegrees,
    Vector2 TargetScale);

internal static class SakuraCardGeometry
{
    private const float ClearSizeScale = 1.05f;

    public static readonly Vector2 VanillaLayoutSize = new(300f, 422f);

    public static readonly Vector2 ClearLayoutSize = new(216.3f, 472.5f);
    public const float ClearSameLayoutAdjustment = -31.5f;
    public const float ClearMinimumAdjacentGap = 100.8f;

    public static readonly Vector2 ClassicLayoutSize = new(221f, 491f);
    public const float ClassicSameLayoutAdjustment = -54f;
    public const float ClassicMinimumAdjacentGap = 100f;

    public static readonly SakuraCardLayoutGeometry ClearLayout = new(
        ClearLayoutSize,
        new Vector2(32f, 36f) * ClearSizeScale,
        40f * ClearSizeScale,
        -36f * ClearSizeScale,
        -1,
        1);

    public static readonly SakuraCardLayoutGeometry ClassicLayout = new(
        ClassicLayoutSize,
        new Vector2(34f, 38f),
        44f,
        -34f,
        -1,
        1);

    public static SakuraHandLayoutProfile ClearHandLayoutProfile => new(
        ClearLayoutSize,
        ClearSameLayoutAdjustment,
        ClearMinimumAdjacentGap);

    public static SakuraHandLayoutProfile ClassicHandLayoutProfile => new(
        ClassicLayoutSize,
        ClassicSameLayoutAdjustment,
        ClassicMinimumAdjacentGap);

    public static bool TryProfile(
        SakuraCardVisualLayout layout,
        out SakuraCardLayoutGeometry profile)
    {
        switch (layout)
        {
            case SakuraCardVisualLayout.Clear:
                profile = ClearLayout;
                return true;
            case SakuraCardVisualLayout.Classic:
                profile = ClassicLayout;
                return true;
            default:
                profile = default;
                return false;
        }
    }

    public static Vector2 GridCellSize(SakuraCardVisualLayout layout)
    {
        if (!TryProfile(layout, out var profile))
            return NCard.defaultSize * NCardHolder.smallScale;

        var defaultSize = NCard.defaultSize * NCardHolder.smallScale;
        var layoutSize = profile.RootSize * NCardHolder.smallScale;
        return new Vector2(
            Mathf.Max(defaultSize.X, layoutSize.X),
            Mathf.Max(defaultSize.Y, layoutSize.Y));
    }
}

internal static class SakuraHandLayout
{
    private const int StackAllocationLimit = 32;

    public static void Apply(NPlayerHand hand)
    {
        var holders = hand.ActiveHolders;
        if (holders.Count <= 1 || hand.FocusedHolder is not null)
            return;

        Span<float> originalXs = holders.Count <= StackAllocationLimit
            ? stackalloc float[holders.Count]
            : new float[holders.Count];
        Span<SakuraHandCardGeometry> geometries = holders.Count <= StackAllocationLimit
            ? stackalloc SakuraHandCardGeometry[holders.Count]
            : new SakuraHandCardGeometry[holders.Count];
        Span<float> adjustedXs = holders.Count <= StackAllocationLimit
            ? stackalloc float[holders.Count]
            : new float[holders.Count];

        var targetScale = HandPosHelper.GetScale(holders.Count);
        for (var index = 0; index < holders.Count; index++)
        {
            var holder = holders[index];
            originalXs[index] = holder.TargetPosition.X;
            geometries[index] = new(
                SakuraCardVisualFamilies.Layout(holder.CardNode),
                holder.TargetAngle,
                targetScale);
        }

        if (!TryCalculateTargetXs(originalXs, geometries, adjustedXs))
            return;

        for (var index = 0; index < holders.Count; index++)
        {
            var originalPosition = holders[index].TargetPosition;
            holders[index].SetTargetPosition(new Vector2(adjustedXs[index], originalPosition.Y));
        }

    }

    internal static float[] CalculateTargetXs(
        float[] originalXs,
        SakuraCardVisualLayout[] layouts)
    {
        ArgumentNullException.ThrowIfNull(originalXs);
        ArgumentNullException.ThrowIfNull(layouts);
        if (originalXs.Length != layouts.Length)
            throw new ArgumentException("Hand positions and layouts must have the same length.", nameof(layouts));

        var geometries = new SakuraHandCardGeometry[layouts.Length];
        for (var index = 0; index < layouts.Length; index++)
            geometries[index] = new SakuraHandCardGeometry(layouts[index], 0f, Vector2.One);

        return CalculateTargetXs(originalXs, geometries);
    }

    internal static float[] CalculateTargetXs(
        float[] originalXs,
        SakuraHandCardGeometry[] geometries)
    {
        ArgumentNullException.ThrowIfNull(originalXs);
        ArgumentNullException.ThrowIfNull(geometries);
        if (originalXs.Length != geometries.Length)
            throw new ArgumentException("Hand positions and geometries must have the same length.", nameof(geometries));

        var adjustedXs = originalXs.ToArray();
        if (!TryCalculateTargetXs(originalXs, geometries, adjustedXs))
            return originalXs.ToArray();

        return adjustedXs;
    }

    private static bool TryCalculateTargetXs(
        ReadOnlySpan<float> originalXs,
        ReadOnlySpan<SakuraHandCardGeometry> geometries,
        Span<float> adjustedXs)
    {
        if (originalXs.Length <= 1 || !ContainsSakuraLayout(geometries))
            return false;

        adjustedXs[0] = 0f;
        var previousOriginalX = originalXs[0];
        for (var index = 1; index < originalXs.Length; index++)
        {
            var originalX = originalXs[index];
            var originalGap = originalX - previousOriginalX;
            if (originalGap <= 0f)
                return false;

            var adjustedGap = originalGap;
            if (TryCalculatePairGap(originalGap, geometries[index - 1], geometries[index], out var targetGap))
                adjustedGap = targetGap;

            adjustedXs[index] = adjustedXs[index - 1] + adjustedGap;
            previousOriginalX = originalX;
        }

        var originalCenter = (originalXs[0] + originalXs[^1]) * 0.5f;
        var adjustedCenter = (adjustedXs[0] + adjustedXs[^1]) * 0.5f;
        var xOffset = originalCenter - adjustedCenter;
        for (var index = 0; index < adjustedXs.Length; index++)
            adjustedXs[index] += xOffset;

        return true;
    }

    private static bool ContainsSakuraLayout(ReadOnlySpan<SakuraHandCardGeometry> geometries)
    {
        for (var index = 0; index < geometries.Length; index++)
        {
            if (IsSakuraLayout(geometries[index].Layout))
                return true;
        }

        return false;
    }

    private static bool TryCalculatePairGap(
        float originalGap,
        SakuraHandCardGeometry left,
        SakuraHandCardGeometry right,
        out float targetGap)
    {
        if (!IsSakuraLayout(left.Layout) && !IsSakuraLayout(right.Layout))
        {
            targetGap = 0f;
            return false;
        }

        var leftProfile = ProfileFor(left.Layout);
        var rightProfile = ProfileFor(right.Layout);
        var leftExtent = HorizontalHalfExtent(leftProfile, left);
        var rightExtent = HorizontalHalfExtent(rightProfile, right);
        var leftClearance = SameLayoutClearance(originalGap, leftProfile, left, right);
        var rightClearance = SameLayoutClearance(originalGap, rightProfile, left, right);
        var desiredClearance = BlendClearance(leftClearance, leftExtent, rightClearance, rightExtent);
        var minimumGap = Mathf.Max(leftProfile.MinimumAdjacentGap, rightProfile.MinimumAdjacentGap);

        targetGap = Mathf.Max(minimumGap, leftExtent + rightExtent + desiredClearance);
        return true;
    }

    private static bool IsSakuraLayout(SakuraCardVisualLayout layout) =>
        layout is SakuraCardVisualLayout.Clear or SakuraCardVisualLayout.Classic;

    private static SakuraHandLayoutProfile ProfileFor(SakuraCardVisualLayout layout) =>
        layout switch
        {
            SakuraCardVisualLayout.Clear => SakuraCardGeometry.ClearHandLayoutProfile,
            SakuraCardVisualLayout.Classic => SakuraCardGeometry.ClassicHandLayoutProfile,
            SakuraCardVisualLayout.None => new SakuraHandLayoutProfile(SakuraCardGeometry.VanillaLayoutSize, 0f, 0f),
            _ => throw new ArgumentOutOfRangeException(nameof(layout), layout, "Unknown Sakura hand layout.")
        };

    private static float SameLayoutClearance(
        float originalGap,
        SakuraHandLayoutProfile profile,
        SakuraHandCardGeometry left,
        SakuraHandCardGeometry right)
    {
        var sameLayoutGap = Mathf.Max(
            profile.MinimumAdjacentGap,
            originalGap + profile.SameLayoutAdjustment);
        return sameLayoutGap
               - HorizontalHalfExtent(profile, left)
               - HorizontalHalfExtent(profile, right);
    }

    private static float HorizontalHalfExtent(
        SakuraHandLayoutProfile profile,
        SakuraHandCardGeometry geometry)
    {
        var radians = Mathf.DegToRad(geometry.TargetAngleDegrees);
        var width = profile.CardSize.X * geometry.TargetScale.X;
        var height = profile.CardSize.Y * geometry.TargetScale.Y;
        return 0.5f * (Mathf.Abs(Mathf.Cos(radians)) * width + Mathf.Abs(Mathf.Sin(radians)) * height);
    }

    private static float BlendClearance(
        float leftClearance,
        float leftExtent,
        float rightClearance,
        float rightExtent)
    {
        var totalExtent = leftExtent + rightExtent;
        if (totalExtent <= 0f)
            throw new InvalidOperationException("Sakura hand layout profiles must expose positive projected extents.");

        return (leftClearance * leftExtent + rightClearance * rightExtent) / totalExtent;
    }

}

internal static class SakuraCardVisualGrid
{
    private const int MaxDeferredGridCenterAttempts = 4;

    private static readonly FieldInfo? GridCardSizeField = PrivateAccess.DeclaredField(typeof(NCardGrid), "_cardSize");
    private static readonly FieldInfo? GridCardRowsField = PrivateAccess.DeclaredField(typeof(NCardGrid), "_cardRows");
    private static readonly FieldInfo? GridScrollContainerField = PrivateAccess.DeclaredField(typeof(NCardGrid), "_scrollContainer");
    private static readonly ConditionalWeakTable<NCardGrid, KinomotoGridState> GridStates = new();

    public static Vector2 CardSizeFor(IReadOnlyList<CardModel> cards, Vector2 defaultSize)
    {
        var cardSize = defaultSize;

        if (ContainsLayout(cards, SakuraCardVisualLayout.Clear))
            cardSize = Max(cardSize, SakuraCardGeometry.GridCellSize(SakuraCardVisualLayout.Clear));
        if (ContainsLayout(cards, SakuraCardVisualLayout.Classic))
            cardSize = Max(cardSize, SakuraCardGeometry.GridCellSize(SakuraCardVisualLayout.Classic));

        return cardSize;
    }

    public static void ApplyCardSize(NCardGrid grid, IReadOnlyList<CardModel> cards)
    {
        var state = GridStates.GetOrCreateValue(grid);
        state.CenterMode = CenterModeFor(cards);
        state.NeedsDeferredCenter = state.CenterMode != KinomotoGridCenterMode.None;
        state.DeferredCenterAttempts = 0;
        state.DeferredCenterQueued = false;

        GridCardSizeField?.SetValue(grid, CardSizeFor(cards, DefaultGridCellSize()));
    }

    public static void CenterRows(NCardGrid grid)
    {
        if (!GridStates.TryGetValue(grid, out var state)
            || state.CenterMode == KinomotoGridCenterMode.None)
            return;

        if (!TryCenterRows(grid, state.CenterMode, out var shouldRetry))
        {
            if (shouldRetry)
                ScheduleDeferredCenter(grid, state);
            return;
        }

        if (state.NeedsDeferredCenter)
        {
            ScheduleDeferredCenter(grid, state);
            return;
        }

        state.DeferredCenterAttempts = 0;
    }

    private static bool ContainsLayout(IReadOnlyList<CardModel> cards, SakuraCardVisualLayout layout)
    {
        for (var i = 0; i < cards.Count; i++)
        {
            if (SakuraCardVisualFamilies.Layout(cards[i]) == layout)
                return true;
        }

        return false;
    }

    private static Vector2 Max(Vector2 left, Vector2 right) =>
        new(Mathf.Max(left.X, right.X), Mathf.Max(left.Y, right.Y));

    private static Vector2 DefaultGridCellSize() =>
        NCard.defaultSize * NCardHolder.smallScale;

    private static KinomotoGridCenterMode CenterModeFor(IReadOnlyList<CardModel> cards)
    {
        if (cards.Count == 0)
            return KinomotoGridCenterMode.None;

        var hasClear = false;
        var hasClassic = false;
        for (var i = 0; i < cards.Count; i++)
        {
            switch (SakuraCardVisualFamilies.Layout(cards[i]))
            {
                case SakuraCardVisualLayout.Clear:
                    hasClear = true;
                    break;
                case SakuraCardVisualLayout.Classic:
                    hasClassic = true;
                    break;
            }
        }

        return (hasClear, hasClassic) switch
        {
            (true, false) => KinomotoGridCenterMode.Clear,
            (false, true) => KinomotoGridCenterMode.Classic,
            (true, true) => KinomotoGridCenterMode.Mixed,
            _ => KinomotoGridCenterMode.None
        };
    }

    private static bool TryCenterRows(
        NCardGrid grid,
        KinomotoGridCenterMode centerMode,
        out bool shouldRetry)
    {
        shouldRetry = false;
        if (!SakuraCardVisualInfrastructure.IsGodotInstanceUsable(grid) || !grid.IsInsideTree())
            return true;
        if (GridCardRowsField?.GetValue(grid) is not List<List<NGridCardHolder>> cardRows)
            return true;
        if (GridScrollContainerField?.GetValue(grid) is not Control scrollContainer)
            return true;
        if (GridCardSizeField?.GetValue(grid) is not Vector2 cardSize)
            return true;
        if (scrollContainer.Size.X <= 0f || grid.Size.Y <= 0f)
        {
            shouldRetry = true;
            return false;
        }

        Span<int> visibleHolderCounts = cardRows.Count <= 128
            ? stackalloc int[cardRows.Count]
            : new int[cardRows.Count];
        var visibleRowCount = 0;
        for (var rowIndex = 0; rowIndex < cardRows.Count; rowIndex++)
        {
            var visibleHolderCount = GridRowHolderCount(cardRows[rowIndex], centerMode);
            if (visibleHolderCount < 0)
                return true;
            visibleHolderCounts[rowIndex] = visibleHolderCount;
            if (visibleHolderCount > 0)
                visibleRowCount++;
        }

        if (visibleRowCount == 0)
            return true;

        var metrics = MetricsFor(centerMode);
        var contentHeight = visibleRowCount * cardSize.Y + (visibleRowCount - 1) * metrics.Padding;
        var shouldCenterVertically = ShouldCenterVertically(grid, contentHeight);
        var startY = shouldCenterVertically
            ? (grid.Size.Y - contentHeight) * 0.5f + cardSize.Y * 0.5f - scrollContainer.Position.Y + metrics.VerticalOffset
            : 0f;

        var visibleRowIndex = 0;
        for (var rowIndex = 0; rowIndex < cardRows.Count; rowIndex++)
        {
            var row = cardRows[rowIndex];
            var holdersInRow = visibleHolderCounts[rowIndex];
            if (holdersInRow <= 0)
                continue;

            var rowWidth = holdersInRow * cardSize.X + (holdersInRow - 1) * metrics.Padding;
            var startX = (scrollContainer.Size.X - rowWidth) * 0.5f + cardSize.X * 0.5f;
            var stepX = cardSize.X + metrics.Padding;
            var y = shouldCenterVertically
                ? startY + visibleRowIndex * (cardSize.Y + metrics.Padding)
                : FirstVisibleGridHolderY(row);

            var visibleHolderIndex = 0;
            foreach (var holder in row)
            {
                if (!IsVisibleGridCardHolder(holder))
                    continue;

                holder.Position = new Vector2(startX + visibleHolderIndex * stepX, y);
                visibleHolderIndex++;
            }

            visibleRowIndex++;
        }

        return true;
    }

    private static int GridRowHolderCount(
        IReadOnlyList<NGridCardHolder> row,
        KinomotoGridCenterMode centerMode)
    {
        var count = 0;
        for (var i = 0; i < row.Count; i++)
        {
            var holder = row[i];
            if (!IsVisibleGridCardHolder(holder))
                continue;

            var layout = SakuraCardVisualFamilies.Layout(holder.CardModel);
            if (!LayoutMatchesCenterMode(layout, centerMode))
                return -1;

            count++;
        }

        return count;
    }

    private static bool LayoutMatchesCenterMode(
        SakuraCardVisualLayout layout,
        KinomotoGridCenterMode centerMode) =>
        centerMode switch
        {
            KinomotoGridCenterMode.Clear => layout == SakuraCardVisualLayout.Clear,
            KinomotoGridCenterMode.Classic => layout == SakuraCardVisualLayout.Classic,
            KinomotoGridCenterMode.Mixed => layout is SakuraCardVisualLayout.Clear or SakuraCardVisualLayout.Classic,
            _ => false
        };

    private static bool IsVisibleGridCardHolder(NGridCardHolder holder) =>
        holder.Visible && holder.CardModel is not null;

    private static float FirstVisibleGridHolderY(IReadOnlyList<NGridCardHolder> row)
    {
        for (var i = 0; i < row.Count; i++)
        {
            var holder = row[i];
            if (IsVisibleGridCardHolder(holder))
                return holder.Position.Y;
        }

        return 0f;
    }

    private static GridMetrics MetricsFor(KinomotoGridCenterMode centerMode)
    {
        var clear = SakuraCardGeometry.ClearLayout;
        var classic = SakuraCardGeometry.ClassicLayout;
        return centerMode switch
        {
            KinomotoGridCenterMode.Clear => new GridMetrics(clear.GridPadding, clear.GridVerticalOffset),
            KinomotoGridCenterMode.Classic => new GridMetrics(classic.GridPadding, classic.GridVerticalOffset),
            KinomotoGridCenterMode.Mixed => new GridMetrics(
                Mathf.Max(clear.GridPadding, classic.GridPadding),
                Mathf.Min(clear.GridVerticalOffset, classic.GridVerticalOffset)),
            _ => new GridMetrics(0f, 0f)
        };
    }

    private static bool ShouldCenterVertically(NCardGrid grid, float contentHeight) =>
        grid is not NCardLibraryGrid && grid.Size.Y > contentHeight;

    private static void ScheduleDeferredCenter(NCardGrid grid, KinomotoGridState state)
    {
        if (state.DeferredCenterQueued || state.DeferredCenterAttempts >= MaxDeferredGridCenterAttempts)
            return;

        state.NeedsDeferredCenter = false;
        state.DeferredCenterQueued = true;
        state.DeferredCenterAttempts++;
        Callable.From(() =>
        {
            state.DeferredCenterQueued = false;
            CenterRows(grid);
        }).CallDeferred();
    }

    private enum KinomotoGridCenterMode
    {
        None,
        Clear,
        Classic,
        Mixed
    }

    private sealed class KinomotoGridState
    {
        public KinomotoGridCenterMode CenterMode { get; set; }
        public bool NeedsDeferredCenter { get; set; }
        public int DeferredCenterAttempts { get; set; }
        public bool DeferredCenterQueued { get; set; }
    }
}

internal readonly record struct GridMetrics(float Padding, float VerticalOffset)
{
}
