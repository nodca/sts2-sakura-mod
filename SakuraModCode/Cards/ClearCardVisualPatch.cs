using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.addons.mega_text;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Extensions;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace SakuraMod.SakuraModCode.Cards;

[HarmonyPatch(typeof(NCard))]
public static class ClearCardVisualPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(NCard.UpdateVisuals))]
    public static void UpdateVisualsPrefix(NCard __instance)
    {
        SakuraCardVisualDispatcher.BeforeClearCardUpdateVisuals(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NCard.UpdateVisuals))]
    public static void UpdateVisualsPostfix(NCard __instance)
    {
        SakuraCardVisualDispatcher.AfterClearCardUpdateVisuals(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NCard.ActivateRewardScreenGlow))]
    public static void ActivateRewardScreenGlowPostfix(NCard __instance)
    {
        SakuraCardVisualDispatcher.AfterClearCardRewardGlow(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NCard.GetCurrentSize))]
    public static void GetCurrentSizePostfix(NCard __instance, ref Vector2 __result)
    {
        SakuraCardVisualDispatcher.OverrideClearCurrentSize(__instance, ref __result);
    }
}

[HarmonyPatch(typeof(NCardHolder))]
public static class ClearCardHolderPatch
{
    [HarmonyPrefix]
    [HarmonyPatch("SetCard")]
    public static void SetCardPrefix(NCardHolder __instance)
    {
        SakuraCardVisualDispatcher.BeforeClearHolderCardChanged(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch("SetCard")]
    public static void SetCardPostfix(NCardHolder __instance)
    {
        SakuraCardVisualDispatcher.AfterClearHolderCardChanged(__instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch("OnCardReassigned")]
    public static void OnCardReassignedPrefix(NCardHolder __instance)
    {
        SakuraCardVisualDispatcher.BeforeClearHolderCardChanged(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch("OnCardReassigned")]
    public static void OnCardReassignedPostfix(NCardHolder __instance)
    {
        SakuraCardVisualDispatcher.AfterClearHolderCardChanged(__instance);
    }
}

[HarmonyPatch(typeof(NPlayerHand))]
public static class ClearCardPlayerHandPatch
{
    [HarmonyPrefix]
    [HarmonyPatch("RefreshLayout")]
    public static void RefreshLayoutPrefix(NPlayerHand __instance)
    {
        SakuraCardVisualDispatcher.BeforeClearHandRefreshLayout(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch("RefreshLayout")]
    public static void RefreshLayoutPostfix(NPlayerHand __instance)
    {
        SakuraCardVisualDispatcher.AfterClearHandRefreshLayout(__instance);
    }
}

[HarmonyPatch(typeof(NHandCardHolder))]
public static class ClearCardHandHolderPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(NHandCardHolder._Ready))]
    public static void ReadyPostfix(NHandCardHolder __instance)
    {
        SakuraCardVisualDispatcher.AfterClearHandHolderUpdated(__instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch("SetCard")]
    public static void SetCardPrefix(NCard node)
    {
        SakuraCardVisualDispatcher.BeforeGeneratedTransparentHandHolderSetCard(node);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NHandCardHolder.UpdateCard))]
    public static void UpdateCardPostfix(NHandCardHolder __instance)
    {
        SakuraCardVisualDispatcher.AfterClearHandHolderUpdated(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NHandCardHolder.SetDefaultTargets))]
    public static void SetDefaultTargetsPostfix(NHandCardHolder __instance)
    {
        SakuraCardVisualDispatcher.AfterClearHandHolderUpdated(__instance);
    }
}

[HarmonyPatch(typeof(NPreviewCardHolder))]
public static class ClearCardPreviewHolderPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(NPreviewCardHolder._Ready))]
    public static void ReadyPostfix(NPreviewCardHolder __instance)
    {
        SakuraCardVisualDispatcher.AfterClearPreviewHolderUpdated(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NPreviewCardHolder.SetCardScale))]
    public static void SetCardScalePostfix(NPreviewCardHolder __instance)
    {
        SakuraCardVisualDispatcher.AfterClearPreviewHolderUpdated(__instance);
    }
}

[HarmonyPatch(typeof(NCardGrid))]
public static class ClearCardGridPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(NCardGrid.SetCards))]
    public static void SetCardsPrefix(NCardGrid __instance, IReadOnlyList<CardModel> cardsToDisplay)
    {
        SakuraCardVisualDispatcher.BeforeClearGridSetCards(__instance, cardsToDisplay);
    }

    [HarmonyPostfix]
    [HarmonyPatch("UpdateGridPositions")]
    public static void UpdateGridPositionsPostfix(NCardGrid __instance)
    {
        SakuraCardVisualDispatcher.AfterClearGridPositionsUpdated(__instance);
    }
}

[HarmonyPatch(typeof(NCardHighlight))]
public static class ClearCardSelectionHighlightPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(NCardHighlight.AnimShow))]
    public static void AnimShowPostfix(NCardHighlight __instance)
    {
        SakuraCardVisualDispatcher.AfterClearSelectionHighlightChanged(__instance, selected: true);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NCardHighlight.AnimHide))]
    public static void AnimHidePostfix(NCardHighlight __instance)
    {
        SakuraCardVisualDispatcher.AfterClearSelectionHighlightChanged(__instance, selected: false);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NCardHighlight.AnimHideInstantly))]
    public static void AnimHideInstantlyPostfix(NCardHighlight __instance)
    {
        SakuraCardVisualDispatcher.AfterClearSelectionHighlightChanged(__instance, selected: false);
    }
}

internal static class ClearCardLayout
{
    private static readonly ClearCardLayoutSpec Spec = new();
    private static readonly string[] KnownVisibleStatusLabels =
        SakuraStateText.KnownStatusLabels
            .Select(RemoveRichTextTags)
            .ToArray();

    private static readonly string[] HiddenCardNodeFieldNames =
    [
        "_ancientBanner",
        "_ancientBorder",
        "_ancientHighlight",
        "_ancientPortrait",
        "_ancientTextBg",
        "_banner",
        "_cardPortraitShadow",
        "_cardOverlay",
        "_frame",
        "_lock",
        "_overlayContainer",
        "_portrait",
        "_portraitBorder",
        "_portraitCanvasGroup",
        "_rareGlow",
        "_shadow",
        "_typeLabel",
        "_typePlaque",
        "_uncommonGlow",
    ];

    private static readonly List<FieldInfo> HiddenCardNodeFields =
        HiddenCardNodeFieldNames
            .Select(name => AccessTools.Field(typeof(NCard), name))
            .OfType<FieldInfo>()
            .ToList();

    private static readonly FieldInfo? TitleLabelField = AccessTools.Field(typeof(NCard), "_titleLabel");
    private static readonly FieldInfo? DescriptionLabelField = AccessTools.Field(typeof(NCard), "_descriptionLabel");
    private static readonly FieldInfo? FrameField = AccessTools.Field(typeof(NCard), "_frame");
    private static readonly FieldInfo? PortraitField = AccessTools.Field(typeof(NCard), "_portrait");
    private static readonly FieldInfo? BannerField = AccessTools.Field(typeof(NCard), "_banner");
    private static readonly FieldInfo? EnergyIconField = AccessTools.Field(typeof(NCard), "_energyIcon");
    private static readonly FieldInfo? EnergyLabelField = AccessTools.Field(typeof(NCard), "_energyLabel");
    private static readonly FieldInfo? UnplayableEnergyIconField = AccessTools.Field(typeof(NCard), "_unplayableEnergyIcon");
    private static readonly FieldInfo? StarIconField = AccessTools.Field(typeof(NCard), "_starIcon");
    private static readonly FieldInfo? StarLabelField = AccessTools.Field(typeof(NCard), "_starLabel");
    private static readonly FieldInfo? UnplayableStarIconField = AccessTools.Field(typeof(NCard), "_unplayableStarIcon");
    private static readonly FieldInfo? RareGlowField = AccessTools.Field(typeof(NCard), "_rareGlow");
    private static readonly FieldInfo? UncommonGlowField = AccessTools.Field(typeof(NCard), "_uncommonGlow");
    private static readonly FieldInfo? HolderHitboxField = AccessTools.Field(typeof(NCardHolder), "_hitbox");
    private static readonly FieldInfo? HandFlashField = AccessTools.Field(typeof(NHandCardHolder), "_flash");
    private static readonly FieldInfo? GridCardSizeField = AccessTools.Field(typeof(NCardGrid), "_cardSize");
    private static readonly FieldInfo? GridCardRowsField = AccessTools.Field(typeof(NCardGrid), "_cardRows");
    private static readonly FieldInfo? GridScrollContainerField = AccessTools.Field(typeof(NCardGrid), "_scrollContainer");
    private static readonly FieldInfo? HighlightCurrentTweenField = AccessTools.Field(typeof(NCardHighlight), "_curTween");
    private static readonly StringName HighlightWidthParameterName = new("width");
    private static readonly StringName FontColorName = new("font_color");
    private static readonly StringName FontOutlineColorName = new("font_outline_color");
    private static readonly StringName FontSizeName = new("font_size");
    private static readonly StringName OutlineSizeName = new("outline_size");
    private static readonly StringName FontShadowColorName = new("font_shadow_color");
    private static readonly StringName ShadowOffsetXName = new("shadow_offset_x");
    private static readonly StringName ShadowOffsetYName = new("shadow_offset_y");
    private static readonly StringName ShadowOutlineSizeName = new("shadow_outline_size");
    private const string HeaderPartSeparator = "  ";
    private const float HeaderPartSeparatorUnits = 1f;
    private const int MaxDeferredGridCenterAttempts = 4;

    private static readonly Color TemporaryHighlightColor = new(0.65f, 0.9f, 1f, 1f);
    private static readonly Color ReleasedHighlightColor = new(1f, 0.88f, 0.58f, 1f);
    private const string InactiveReleaseTextColor = "#b8b0a3";

    private static readonly Dictionary<Type, Texture2D?> ClearCardArtCache = [];
    private static readonly Dictionary<Type, string> ClearCardEnglishNameCache = [];
    private static readonly Dictionary<(string Language, SakuraElement Element), string> ElementTitleCache = [];
    private static readonly Dictionary<string, (string Released, string Temporary)> ClearCardStatusTextCache = [];
    private static Texture2D? ClearCardHighlightTextureCache;
    private static Texture2D? DefaultHighlightTextureCache;
    private static readonly ConditionalWeakTable<NCard, ClearCardState> CardStates = new();
    private static readonly ConditionalWeakTable<NCardHolder, ClearCardHolderState> HolderStates = new();
    private static readonly ConditionalWeakTable<NCardGrid, ClearCardGridState> GridStates = new();

    public static bool IsClearCard(NCard? card) =>
        SakuraCardVisualFamilies.IsClear(card);

    private static bool IsSakuraNonClearVisualCard(NCard? card) =>
        SakuraCardVisualFamilies.IsKinomoto(card);

    public static Vector2 CurrentSize(NCard card) =>
        Spec.LayoutSize * card.Scale;

    public static Vector2 GridCellSize => Spec.GridCellSize;

    public static string DescribeCardForDiagnostics(NCard card)
    {
        var stateTracked = CardStates.TryGetValue(card, out var state);
        var art = state?.Art;
        var expectedArt = card.Model is null ? null : ClearCardTexture(card.Model.GetType());
        var artMatchesExpected = art is not null
                                 && IsGodotInstanceUsable(art)
                                 && expectedArt is not null
                                 && HasTexture(art, expectedArt);

        return "visual={"
               + $"family={SakuraCardVisualFamilies.Family(card)},"
               + $"clear={IsClearCard(card)},"
               + $"stateTracked={stateTracked},"
               + $"stateApplied={(stateTracked && state is not null && state.IsApplied)},"
               + $"displayingPile={card.DisplayingPile},"
               + $"cardVisible={card.Visible},"
               + $"cardPos={FormatVector(card.Position)},"
               + $"cardGlobal={FormatVector(card.GlobalPosition)},"
               + $"cardScale={FormatVector(card.Scale)},"
               + $"cardSize={FormatVector(card.Size)},"
               + $"body={DescribeControl(card.Body)},"
               + $"bodySelfAlpha={AlphaOf(card.Body)},"
               + $"art={DescribeTextureRect(art)},"
               + $"artExpected={artMatchesExpected},"
               + $"frame={DescribeCanvasItem(FieldValue<CanvasItem>(FrameField, card))},"
               + $"portrait={DescribeCanvasItem(FieldValue<CanvasItem>(PortraitField, card))},"
               + $"banner={DescribeCanvasItem(FieldValue<CanvasItem>(BannerField, card))}"
               + "}";
    }

    public static string DescribeHolderForDiagnostics(NCardHolder holder)
    {
        var hitbox = HolderHitbox(holder);
        var flash = HandFlash(holder);
        var targetPosition = holder is NHandCardHolder handHolder
            ? FormatVector(handHolder.TargetPosition)
            : "n/a";

        return "holder={"
               + $"type={holder.GetType().Name},"
               + $"ready={holder.IsNodeReady()},"
               + $"insideTree={holder.IsInsideTree()},"
               + $"visible={holder.Visible},"
               + $"pos={FormatVector(holder.Position)},"
               + $"global={FormatVector(holder.GlobalPosition)},"
               + $"scale={FormatVector(holder.Scale)},"
               + $"rot={FormatFloat(holder.RotationDegrees)},"
               + $"target={targetPosition},"
               + $"targetAngle={(holder is NHandCardHolder angleHolder ? FormatFloat(angleHolder.TargetAngle) : "n/a")},"
               + $"hitbox={DescribeControl(hitbox)},"
               + $"flash={DescribeControl(flash)}"
               + "}";
    }

    public static void PreloadVisualResources()
    {
        foreach (var cardType in SakuraCardCatalog.TransparentCardTypes)
            _ = ClearCardTexture(cardType);
        _ = ClearCardHighlightTexture();
    }

    public static string CardArtPath(Type cardType) =>
        ClearCardArtFileName(cardType).ClearCardAssetPath();

    public static void RestoreCardIfTracked(NCard card)
    {
        if (CardStates.TryGetValue(card, out var existingState))
            existingState.Restore(card);
        SakuraNonClearFrameApplier.RestoreIfTracked(card);
    }

    public static void RestoreHolderIfTracked(NCardHolder holder)
    {
        if (HolderStates.TryGetValue(holder, out var existingState))
            existingState.Restore(holder);
    }

    public static void ApplyGridCardSize(NCardGrid grid, IReadOnlyList<CardModel> cards)
    {
        if (GridCardSizeField is null)
            return;

        var allCardsAreClearCards = AllCardsAreClearCards(cards);
        var state = GridStates.GetOrCreateValue(grid);
        state.AllCardsAreClearCards = allCardsAreClearCards;
        state.NeedsDeferredCenter = allCardsAreClearCards;
        state.DeferredCenterAttempts = 0;
        state.DeferredCenterQueued = false;
        GridCardSizeField.SetValue(grid, SakuraCardVisualGrid.CardSizeFor(cards, Spec.DefaultGridCellSize));
    }

    public static void CenterGridRows(NCardGrid grid)
    {
        if (!GridStates.TryGetValue(grid, out var state) || !state.AllCardsAreClearCards)
            return;

        if (!TryCenterGridRows(grid, out var shouldRetry))
        {
            if (shouldRetry)
                ScheduleDeferredGridCenter(grid, state);
            return;
        }

        if (state.NeedsDeferredCenter)
        {
            ScheduleDeferredGridCenter(grid, state);
            return;
        }

        state.DeferredCenterAttempts = 0;
    }

    private static bool TryCenterGridRows(NCardGrid grid, out bool shouldRetry)
    {
        shouldRetry = false;
        if (!IsGodotInstanceUsable(grid) || !grid.IsInsideTree())
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
            var visibleHolderCount = ClearCardGridRowHolderCount(cardRows[rowIndex]);
            if (visibleHolderCount < 0)
                return true;
            visibleHolderCounts[rowIndex] = visibleHolderCount;
            if (visibleHolderCount > 0)
                visibleRowCount++;
        }

        if (visibleRowCount == 0)
            return true;

        var contentHeight = visibleRowCount * cardSize.Y + (visibleRowCount - 1) * Spec.GridCardPadding;
        var shouldCenterVertically = grid.Size.Y > contentHeight;
        var startY = shouldCenterVertically
            ? (grid.Size.Y - contentHeight) * 0.5f + cardSize.Y * 0.5f - scrollContainer.Position.Y + Spec.ClearCardGridVerticalOffset
            : 0f;

        var visibleRowIndex = 0;
        for (var rowIndex = 0; rowIndex < cardRows.Count; rowIndex++)
        {
            var row = cardRows[rowIndex];
            var holdersInRow = visibleHolderCounts[rowIndex];
            if (holdersInRow <= 0)
                continue;

            var rowWidth = holdersInRow * cardSize.X + (holdersInRow - 1) * Spec.GridCardPadding;
            var startX = (scrollContainer.Size.X - rowWidth) * 0.5f + cardSize.X * 0.5f;
            var stepX = cardSize.X + Spec.GridCardPadding;
            var y = shouldCenterVertically
                ? startY + visibleRowIndex * (cardSize.Y + Spec.GridCardPadding)
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

    private static void ScheduleDeferredGridCenter(NCardGrid grid, ClearCardGridState state)
    {
        if (state.DeferredCenterQueued || state.DeferredCenterAttempts >= MaxDeferredGridCenterAttempts)
            return;

        state.NeedsDeferredCenter = false;
        state.DeferredCenterQueued = true;
        state.DeferredCenterAttempts++;
        Callable.From(() =>
        {
            state.DeferredCenterQueued = false;
            CenterGridRows(grid);
        }).CallDeferred();
    }

    public static void Apply(NCard card)
    {
        if (!IsClearCard(card))
        {
            if (CardStates.TryGetValue(card, out var existingState))
            {
                existingState.Restore(card);
                ApplyParentHolder(card);
            }

            if (IsSakuraNonClearVisualCard(card))
                SakuraNonClearFrameApplier.Apply(card);

            return;
        }

        SakuraNonClearFrameApplier.RestoreIfTracked(card);
        var state = CardStates.GetOrCreateValue(card);
        state.Capture(card);
        ApplyCardLayout(card, state);
        state.MarkApplied();
        ApplyParentHolder(card);
    }

    public static void Apply(NCardHolder holder)
    {
        if (!holder.IsNodeReady())
            return;

        if (!IsClearCard(holder.CardNode))
        {
            if (HolderStates.TryGetValue(holder, out var existingState))
                existingState.Restore(holder);
            return;
        }

        var state = HolderStates.GetOrCreateValue(holder);
        state.Capture(holder);
        ApplyHolderLayout(holder);
        state.MarkApplied();
    }

    public static void Apply(IEnumerable<NHandCardHolder> holders)
    {
        foreach (var holder in holders)
            Apply(holder);
    }

    public static void ApplyHandSpacing(NPlayerHand hand)
    {
        var holders = hand.ActiveHolders;
        if (holders.Count <= 1 || hand.FocusedHolder is not null)
            return;

        Span<float> adjustedXs = holders.Count <= 32
            ? stackalloc float[holders.Count]
            : new float[holders.Count];

        var firstPosition = holders[0].TargetPosition;
        var previousOriginalX = firstPosition.X;
        var previousIsClearCard = IsClearCard(holders[0].CardNode);
        var hasClearCard = previousIsClearCard;

        for (var i = 1; i < holders.Count; i++)
        {
            var holder = holders[i];
            var originalPosition = holder.TargetPosition;
            var currentIsClearCard = IsClearCard(holder.CardNode);
            hasClearCard |= currentIsClearCard;

            var originalGap = originalPosition.X - previousOriginalX;
            if (originalGap <= 0f)
                return;

            var adjustedGap = Mathf.Max(
                Spec.HandMinimumAdjacentGap,
                originalGap + HandPairGapAdjustment(previousIsClearCard, currentIsClearCard));
            adjustedXs[i] = adjustedXs[i - 1] + adjustedGap;
            previousOriginalX = originalPosition.X;
            previousIsClearCard = currentIsClearCard;
        }

        if (!hasClearCard)
            return;

        var originalCenter = (firstPosition.X + previousOriginalX) * 0.5f;
        var adjustedCenter = (adjustedXs[0] + adjustedXs[holders.Count - 1]) * 0.5f;
        var xOffset = originalCenter - adjustedCenter;
        for (var i = 0; i < holders.Count; i++)
        {
            var originalPosition = holders[i].TargetPosition;
            holders[i].SetTargetPosition(new Vector2(adjustedXs[i] + xOffset, originalPosition.Y));
        }
    }

    private static float HandPairGapAdjustment(NHandCardHolder left, NHandCardHolder right)
    {
        var leftIsClearCard = IsClearCard(left.CardNode);
        var rightIsClearCard = IsClearCard(right.CardNode);
        return HandPairGapAdjustment(leftIsClearCard, rightIsClearCard);
    }

    private static float HandPairGapAdjustment(bool leftIsClearCard, bool rightIsClearCard)
    {
        return (leftIsClearCard, rightIsClearCard) switch
        {
            (true, true) => Spec.HandClearPairGapAdjustment,
            (true, false) or (false, true) => Spec.HandMixedPairGapAdjustment,
            _ => 0f
        };
    }

    private static int ClearCardGridRowHolderCount(IReadOnlyList<NGridCardHolder> row)
    {
        var count = 0;
        for (var i = 0; i < row.Count; i++)
        {
            var holder = row[i];
            if (!IsVisibleGridCardHolder(holder))
                continue;
            if (!SakuraCardVisualFamilies.IsClear(holder.CardModel))
                return -1;

            count++;
        }

        return count;
    }

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

    private static bool AllCardsAreClearCards(IReadOnlyList<CardModel> cards)
    {
        if (cards.Count == 0)
            return false;

        for (var i = 0; i < cards.Count; i++)
        {
            if (!SakuraCardVisualFamilies.IsClear(cards[i]))
                return false;
        }

        return true;
    }

    private static void ApplyCardLayout(NCard card, ClearCardState state)
    {
        var model = card.Model;
        if (model is null)
            return;
        if (!IsGodotInstanceUsable(card.Body))
            return;

        var highlight = card.CardHighlight;
        if (highlight is null)
            return;

        var nodes = state.GetOrCreateNodes(card);

        var layout = ClearCardLayoutContext.For(card);
        EnsureTransformVfxViewportFits(layout.TransformVfxViewport);

        ApplySize(card, Spec.RootSize, layout.RootPivotOffset);
        ApplyBox(card.Body, layout.RootBox);
        card.Body.SelfModulate = new Color(1f, 1f, 1f, 0f);

        ApplyArtLayout(state.GetOrCreateArt(card), card);
        ApplyHighlightLayout(highlight, Spec.HighlightBox);
        ApplyPanelLayout(state.GetOrCreateDescriptionPanel(card), Spec.DescriptionPanelBox, Spec.DescriptionPanelZIndex);

        ApplyTitleLayout(card, nodes.TitleLabel, model);
        ApplyEnglishNameLayout(state.GetOrCreateEnglishNameLabel(card), model);
        ApplyDescriptionLayout(nodes.DescriptionLabel, model, state);
        ApplyCostLayout(
            nodes.EnergyIcon,
            nodes.EnergyLabel,
            nodes.UnplayableEnergyIcon,
            Spec.EnergyCostBox,
            Spec.EnergyCostLabelBox,
            model.EnergyIcon);
        ApplyCostLayout(
            nodes.StarIcon,
            nodes.StarLabel,
            nodes.UnplayableStarIcon,
            Spec.StarCostBox);

        HideVanillaBodyVisuals(card, state);
        HideLateRewardGlows(card);
        foreach (var hiddenNode in nodes.HiddenNodes)
            Hide(hiddenNode);
    }

    private static void ApplyArtLayout(TextureRect art, NCard card)
    {
        if (art.ExpandMode != TextureRect.ExpandModeEnum.IgnoreSize)
            art.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        if (art.StretchMode != TextureRect.StretchModeEnum.KeepAspectCentered)
            art.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        ApplyBox(art, Spec.ArtBox);
        var texture = ClearCardTexture(card);
        SetTextureIfDifferent(art, texture);
        var visible = art.Texture is not null;
        if (art.Visible != visible)
            art.Visible = visible;
        if (art.ZIndex != Spec.ArtZIndex)
            art.ZIndex = Spec.ArtZIndex;
    }

    private static void ApplyHighlightLayout(NCardHighlight highlight, Rect2 box)
    {
        ApplyTopLeftAnchors(highlight);
        if (highlight.ExpandMode != TextureRect.ExpandModeEnum.IgnoreSize)
            highlight.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        if (highlight.StretchMode != TextureRect.StretchModeEnum.Scale)
            highlight.StretchMode = TextureRect.StretchModeEnum.Scale;
        var texture = ClearCardHighlightTexture();
        SetTextureIfDifferent(highlight, texture);
        ApplyBox(highlight, box);
        if (highlight.ZIndex != Spec.HighlightZIndex)
            highlight.ZIndex = Spec.HighlightZIndex;
        if (highlight.MouseFilter != Control.MouseFilterEnum.Ignore)
            highlight.MouseFilter = Control.MouseFilterEnum.Ignore;
    }

    private static void ApplyTopLeftAnchors(Control? control)
    {
        SakuraCardVisualInfrastructure.ApplyTopLeftAnchors(control);
    }

    private static void ApplyCenteredAnchors(Control? control)
    {
        if (control is null)
            return;

        if (control.AnchorLeft != 0.5f)
            control.AnchorLeft = 0.5f;
        if (control.AnchorTop != 0.5f)
            control.AnchorTop = 0.5f;
        if (control.AnchorRight != 0.5f)
            control.AnchorRight = 0.5f;
        if (control.AnchorBottom != 0.5f)
            control.AnchorBottom = 0.5f;
    }

    private static void ApplyThemeColorOverride(Control control, StringName name, Color color)
    {
        SakuraCardVisualInfrastructure.ApplyThemeColorOverride(control, name, color);
    }

    private static void ApplyThemeConstantOverride(Control control, StringName name, int value)
    {
        SakuraCardVisualInfrastructure.ApplyThemeConstantOverride(control, name, value);
    }

    private static void ApplyThemeFontSizeOverride(Control control, StringName name, int value)
    {
        SakuraCardVisualInfrastructure.ApplyThemeFontSizeOverride(control, name, value);
    }

    private static void ApplyPanelLayout(Panel panel, Rect2 box, int zIndex)
    {
        ApplyBox(panel, box);
        if (!panel.Visible)
            panel.Visible = true;
        if (panel.ZIndex != zIndex)
            panel.ZIndex = zIndex;
        if (panel.MouseFilter != Control.MouseFilterEnum.Ignore)
            panel.MouseFilter = Control.MouseFilterEnum.Ignore;
    }

    private static void ApplyHolderLayout(NCardHolder holder)
    {
        ApplyBox(HolderHitbox(holder), Spec.CenteredRootBox);
        ApplyHandStateHighlightColor(holder);
        ApplyHandFlashLayout(holder);
        if (holder.CardNode is not null && holder.CardNode.Position != Vector2.Zero)
            holder.CardNode.Position = Vector2.Zero;
    }

    private static void ApplyHandFlashLayout(NCardHolder holder)
    {
        var flash = HandFlash(holder);
        if (flash is TextureRect textureRect)
        {
            if (textureRect.ExpandMode != TextureRect.ExpandModeEnum.IgnoreSize)
                textureRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            if (textureRect.StretchMode != TextureRect.StretchModeEnum.Scale)
                textureRect.StretchMode = TextureRect.StretchModeEnum.Scale;
            if (textureRect.MouseFilter != Control.MouseFilterEnum.Ignore)
                textureRect.MouseFilter = Control.MouseFilterEnum.Ignore;
        }

        ApplyBox(flash, Spec.CenteredHighlightBox);
        ApplyHandFlashColor(holder, flash);
    }

    private static void ApplyHandFlashColor(NCardHolder holder, Control? flash)
    {
        var highlight = holder.CardNode?.CardHighlight;
        if (flash is null || highlight is null)
            return;

        var highlightColor = highlight.Modulate;
        var flashColor = flash.Modulate;
        var targetColor = new Color(highlightColor.R, highlightColor.G, highlightColor.B, flashColor.A);
        if (flashColor != targetColor)
            flash.Modulate = targetColor;
    }

    private static void ApplyHandStateHighlightColor(NCardHolder holder)
    {
        if (holder is not NHandCardHolder || holder.CardNode is not { } card || card.CardHighlight is not { } highlight)
            return;

        var color = StateHighlightColor(card.Model, highlight.Modulate);
        if (color is null)
            return;

        var shouldAnimateWidth = ShaderWidthValue(highlight) <= 0.001f;
        highlight.Modulate = color.Value;
        if (shouldAnimateWidth)
            AnimateStateHighlightWidth(highlight);
        else
            SetShaderWidth(highlight, Spec.StateHighlightWidth);
    }

    private static Color? StateHighlightColor(CardModel? model, Color currentColor)
    {
        if (model is null || !Approximately(currentColor, NCardHighlight.playableColor))
            return null;

        if (model.IsReleased())
            return ReleasedHighlightColor;

        if (model.IsTemporary())
            return TemporaryHighlightColor;

        return null;
    }

    private static void ApplyParentHolder(NCard card)
    {
        if (card.GetParent() is NCardHolder holder)
            Apply(holder);
    }

    private static void EnsureTransformVfxViewportFits(SubViewport? viewport)
    {
        if (viewport is null)
            return;

        var targetSize = new Vector2I(
            Mathf.CeilToInt(Mathf.Max(viewport.Size.X, Spec.DefaultCardSize.X)),
            Mathf.CeilToInt(Mathf.Max(viewport.Size.Y, Spec.RootSize.Y)));
        if (viewport.Size != targetSize)
            viewport.Size = targetSize;
    }

    private static void ApplyTitleLayout(NCard card, MegaLabel? title, CardModel model)
    {
        ApplyBox(title, Spec.TitleBox);
        if (title is null)
            return;

        if (!title.Visible)
            title.Visible = true;
        var color = NameTextColor(model);
        ApplyTextCanvasColor(title);
        ApplyThemeColorOverride(title, FontColorName, color);
        ApplyThemeColorOverride(title, FontOutlineColorName, Spec.NameTextOutlineColor);
        ApplyThemeColorOverride(title, FontShadowColorName, Spec.TitleTextShadowColor);
        ApplyThemeConstantOverride(title, OutlineSizeName, Spec.TitleTextOutlineSize);
        ApplyThemeConstantOverride(title, ShadowOffsetXName, Spec.TitleTextShadowOffset);
        ApplyThemeConstantOverride(title, ShadowOffsetYName, Spec.TitleTextShadowOffset);
        ApplyThemeConstantOverride(title, ShadowOutlineSizeName, Spec.TitleTextShadowOutlineSize);
        ApplyThemeFontSizeOverride(title, FontSizeName, Spec.TitleTextFontSize);
        if (title.ZIndex != Spec.TextZIndex)
            title.ZIndex = Spec.TextZIndex;
        if (title.HorizontalAlignment != HorizontalAlignment.Center)
            title.HorizontalAlignment = HorizontalAlignment.Center;
        if (title.VerticalAlignment != VerticalAlignment.Center)
            title.VerticalAlignment = VerticalAlignment.Center;
        if (title.AutowrapMode != TextServer.AutowrapMode.Off)
            title.AutowrapMode = TextServer.AutowrapMode.Off;
        if (title.MinFontSize != 16)
            title.MinFontSize = 16;
        if (title.MaxFontSize != 26)
            title.MaxFontSize = 26;
    }

    private static void ApplyEnglishNameLayout(Label label, CardModel model)
    {
        ApplyBox(label, Spec.EnglishNameBox);
        var text = ClearCardEnglishName(model.GetType());
        if (label.Text != text)
            label.Text = text;
        if (!label.Visible)
            label.Visible = true;
        if (label.ZIndex != Spec.TextZIndex)
            label.ZIndex = Spec.TextZIndex;
        if (label.HorizontalAlignment != HorizontalAlignment.Center)
            label.HorizontalAlignment = HorizontalAlignment.Center;
        if (label.VerticalAlignment != VerticalAlignment.Center)
            label.VerticalAlignment = VerticalAlignment.Center;
        if (label.AutowrapMode != TextServer.AutowrapMode.Off)
            label.AutowrapMode = TextServer.AutowrapMode.Off;
        ApplyTextCanvasColor(label);
        ApplyThemeColorOverride(label, FontColorName, NameTextColor(model));
        ApplyThemeColorOverride(label, FontOutlineColorName, Spec.NameTextOutlineColor);
        ApplyThemeFontSizeOverride(label, FontSizeName, Spec.EnglishNameFontSize);
        ApplyThemeConstantOverride(label, OutlineSizeName, Spec.NameTextOutlineSize);
    }

    private static void ApplyTextCanvasColor(CanvasItem item)
    {
        if (item.Modulate != Colors.White)
            item.Modulate = Colors.White;
        if (item.SelfModulate != Colors.White)
            item.SelfModulate = Colors.White;
    }

    private static void ApplyDescriptionLayout(MegaRichTextLabel? description, CardModel model, ClearCardState state)
    {
        ApplyBox(description, Spec.DescriptionBox);
        if (description is null)
            return;

        var text = state.ClearCardDescriptionText(model, description.Text);
        if (description.Text != text)
            description.SetTextAutoSize(text);
        if (!description.Visible)
            description.Visible = true;
        if (description.ScrollActive)
            description.ScrollActive = false;
        if (description.FitContent)
            description.FitContent = false;
        if (!description.IsHorizontallyBound)
            description.IsHorizontallyBound = true;
        if (!description.IsVerticallyBound)
            description.IsVerticallyBound = true;
        if (description.MinFontSize != 12)
            description.MinFontSize = 12;
        if (description.MaxFontSize != 18)
            description.MaxFontSize = 18;
    }

    private static string ClearCardDescriptionText(CardModel model, string currentText, string? synchronizedLine = null)
    {
        var body = ClearCardDescriptionBody(model, currentText);
        synchronizedLine ??= SakuraStateText.SynchronizedLine(model);
        if (!string.IsNullOrEmpty(synchronizedLine))
            body = AppendDescriptionBodyTextLine(body, synchronizedLine.TrimStart('\r', '\n'));

        var header = ClearCardHeaderText(model);

        if (header.Length == 0)
            return CenterText(body);
        if (body.Length == 0)
            return CenterText(header);

        return CenterText($"{header}\n{body}");
    }

    private static string AppendDescriptionBodyTextLine(string body, string line) =>
        body.Length == 0
            ? line
            : $"{body}\n{line}";

    private static string ClearCardDescriptionBody(CardModel model, string currentText)
    {
        var text = RemoveCenterTags(currentText);
        var builder = new StringBuilder(text.Length);
        var lineStart = 0;
        for (var i = 0; i <= text.Length; i++)
        {
            if (i < text.Length && text[i] is not '\r' and not '\n')
                continue;

            AppendDescriptionBodyLine(builder, model, text, lineStart, i);
            if (i < text.Length && text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                i++;

            lineStart = i + 1;
        }

        return builder.ToString();
    }

    private static void AppendDescriptionBodyLine(StringBuilder builder, CardModel model, string text, int start, int end)
    {
        while (start < end && char.IsWhiteSpace(text[start]))
            start++;
        while (end > start && char.IsWhiteSpace(text[end - 1]))
            end--;

        if (start >= end
            || IsClearCardHeaderLine(model, text, start, end)
            || IsSynchronizedDescriptionLine(text, start, end))
            return;

        if (builder.Length > 0)
            builder.Append('\n');

        if (!model.IsReleased() && IsReleaseEffectDescriptionLine(text, start, end))
            builder.Append(InactiveReleaseEffectLine(text, start, end));
        else
            builder.Append(text, start, end - start);
    }

    private static string ClearCardHeaderText(CardModel model) =>
        string.Join('\n', PackHeaderLines(ClearCardHeaderParts(model)));

    private static IEnumerable<string> ClearCardHeaderParts(CardModel model)
    {
        foreach (var statusPart in ClearCardStatusParts(model))
            yield return statusPart;
    }

    private static IEnumerable<string> ClearCardStatusParts(CardModel model)
    {
        var statusText = ClearCardStatusText();
        var released = model.IsReleased();
        var temporary = model.IsTemporary();

        if (released)
            yield return statusText.Released;
        if (temporary)
            yield return statusText.Temporary;
    }

    private static (string Released, string Temporary) ClearCardStatusText()
    {
        var language = CurrentLanguageKey();
        if (ClearCardStatusTextCache.TryGetValue(language, out var cachedText))
            return cachedText;

        var text = (
            Released: $"[color=#ffe094]{SakuraStateText.ReleasedLabel()}[/color]",
            Temporary: $"[color=#a6e0ff]{SakuraStateText.TemporaryLabel()}[/color]");
        ClearCardStatusTextCache[language] = text;
        return text;
    }

    private static string ElementTitle(SakuraElement element)
    {
        var key = (CurrentLanguageKey(), element);
        if (ElementTitleCache.TryGetValue(key, out var title))
            return title;

        title = new LocString("card_keywords", ElementLocKey(element)).GetFormattedText();
        ElementTitleCache[key] = title;
        return title;
    }

    private static string ElementLocKey(SakuraElement element) =>
        element switch
        {
            SakuraElement.Wind => "SAKURAMOD-WIND.title",
            SakuraElement.Water => "SAKURAMOD-WATER.title",
            SakuraElement.Fire => "SAKURAMOD-FIRE.title",
            SakuraElement.Earth => "SAKURAMOD-EARTH.title",
            _ => throw new ArgumentOutOfRangeException(nameof(element), element, null)
        };

    private static IReadOnlyList<string> PackHeaderLines(IEnumerable<string> parts)
    {
        var lines = new List<string>();
        var currentParts = new List<string>();
        var currentUnits = 0f;
        foreach (var part in parts)
        {
            var partUnits = VisibleTextUnits(part);
            var candidateUnits = currentParts.Count > 0
                ? currentUnits + HeaderPartSeparatorUnits + partUnits
                : partUnits;
            if (currentParts.Count > 0 && candidateUnits > Spec.HeaderLineUnits)
            {
                lines.Add(JoinHeaderParts(currentParts));
                currentParts.Clear();
                currentUnits = 0f;
            }

            currentUnits = currentParts.Count > 0
                ? currentUnits + HeaderPartSeparatorUnits + partUnits
                : partUnits;
            currentParts.Add(part);
        }

        if (currentParts.Count > 0)
            lines.Add(JoinHeaderParts(currentParts));

        return lines;
    }

    private static string JoinHeaderParts(IEnumerable<string> parts) =>
        string.Join(HeaderPartSeparator, parts);

    private static string CenterText(string text) =>
        $"[center]{text}[/center]";

    private static float VisibleTextUnits(string text)
    {
        var units = 0f;
        var inTag = false;
        foreach (var character in text)
        {
            if (character == '[')
            {
                inTag = true;
                continue;
            }
            if (character == ']')
            {
                inTag = false;
                continue;
            }
            if (inTag)
                continue;

            units += VisibleCharacterUnits(character);
        }

        return units;
    }

    private static float VisibleCharacterUnits(char character)
    {
        if (char.IsWhiteSpace(character))
            return 0.5f;

        return IsWideCharacter(character) ? 2f : 1f;
    }

    private static bool IsWideCharacter(char character) =>
        character is >= '\u1100' and <= '\u115f'
            or >= '\u2e80' and <= '\ua4cf'
            or >= '\uac00' and <= '\ud7a3'
            or >= '\uf900' and <= '\ufaff'
            or >= '\ufe10' and <= '\ufe19'
            or >= '\ufe30' and <= '\ufe6f'
            or >= '\uff00' and <= '\uff60'
            or >= '\uffe0' and <= '\uffe6';

    private static bool IsClearCardHeaderLine(CardModel model, string text, int start, int end)
    {
        var visibleText = RemoveRichTextTags(text, start, end);
        var index = 0;
        while (index < visibleText.Length)
        {
            var character = visibleText[index];
            if (char.IsWhiteSpace(character) || character is '。' or '.')
            {
                index++;
                continue;
            }

            if (TryMatchKnownVisibleHeaderLabel(model, visibleText, index, out var labelLength))
            {
                index += labelLength;
                continue;
            }

            return false;
        }

        return true;
    }

    private static bool TryMatchKnownVisibleHeaderLabel(CardModel model, string text, int start, out int length)
    {
        if (TryMatchKnownVisibleStatusLabel(text, start, out length))
            return true;
        if (TryMatchVisibleElementTitle(model, text, start, out length))
            return true;

        length = 0;
        return false;
    }

    private static bool TryMatchKnownVisibleStatusLabel(string text, int start, out int length)
    {
        foreach (var label in KnownVisibleStatusLabels)
        {
            if (start + label.Length > text.Length)
                continue;
            if (string.CompareOrdinal(text, start, label, 0, label.Length) != 0)
                continue;

            length = label.Length;
            return true;
        }

        length = 0;
        return false;
    }

    private static bool TryMatchVisibleElementTitle(CardModel model, string text, int start, out int length)
    {
        foreach (var element in SakuraActions.ElementSetOf(model).AsElements())
        {
            var title = ElementTitle(element);
            if (start + title.Length > text.Length)
                continue;
            if (string.CompareOrdinal(text, start, title, 0, title.Length) != 0)
                continue;

            length = title.Length;
            return true;
        }

        length = 0;
        return false;
    }

    private static bool IsSynchronizedDescriptionLine(string text, int start, int end)
    {
        var visibleText = RemoveRichTextTags(text, start, end).Trim();
        return visibleText.StartsWith("同步：", StringComparison.Ordinal)
               || visibleText.StartsWith("Synced:", StringComparison.Ordinal);
    }

    private static bool IsReleaseEffectDescriptionLine(string text, int start, int end)
    {
        var visibleText = RemoveRichTextTags(text, start, end).TrimStart();
        return visibleText.StartsWith("解封：", StringComparison.Ordinal)
               || visibleText.StartsWith("Release:", StringComparison.Ordinal);
    }

    private static string InactiveReleaseEffectLine(string text, int start, int end)
    {
        while (start < end && char.IsWhiteSpace(text[start]))
            start++;
        while (end > start && char.IsWhiteSpace(text[end - 1]))
            end--;

        var visibleText = RemoveRichTextTags(text, start, end).Trim();
        return $"[color={InactiveReleaseTextColor}]{visibleText}[/color]";
    }

    private static string RemoveRichTextTags(string text) =>
        RemoveRichTextTags(text, 0, text.Length);

    private static string RemoveCenterTags(string text) =>
        text
            .Replace("[center]", string.Empty, StringComparison.Ordinal)
            .Replace("[/center]", string.Empty, StringComparison.Ordinal);

    private static string RemoveRichTextTags(string text, int start, int end)
    {
        var builder = new StringBuilder(end - start);
        var inTag = false;
        for (var i = start; i < end; i++)
        {
            var character = text[i];
            if (character == '[')
            {
                inTag = true;
                continue;
            }
            if (character == ']')
            {
                inTag = false;
                continue;
            }
            if (!inTag)
                builder.Append(character);
        }

        return builder.ToString();
    }

    private static void ApplyCostLayout(
        TextureRect? icon,
        MegaLabel? label,
        TextureRect? unplayableIcon,
        Rect2 box,
        Rect2? labelBox = null,
        Texture2D? iconTexture = null)
    {
        ApplyTopLeftAnchors(icon);
        ApplyTopLeftAnchors(label);
        ApplyTopLeftAnchors(unplayableIcon);
        ApplyBox(icon, box);
        ApplyBox(label, labelBox ?? box);
        ApplyBox(unplayableIcon, box);

        if (icon is not null)
        {
            if (icon.ExpandMode != TextureRect.ExpandModeEnum.IgnoreSize)
                icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            if (icon.StretchMode != TextureRect.StretchModeEnum.Scale)
                icon.StretchMode = TextureRect.StretchModeEnum.Scale;
            if (iconTexture is not null)
                SetTextureIfDifferent(icon, iconTexture);
        }

        if (unplayableIcon is not null && iconTexture is not null)
        {
            if (unplayableIcon.ExpandMode != TextureRect.ExpandModeEnum.IgnoreSize)
                unplayableIcon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            if (unplayableIcon.StretchMode != TextureRect.StretchModeEnum.Scale)
                unplayableIcon.StretchMode = TextureRect.StretchModeEnum.Scale;
            SetTextureIfDifferent(unplayableIcon, iconTexture);
        }

        if (label is null)
            return;

        if (!label.Visible)
            label.Visible = true;
        ApplyTextCanvasColor(label);
        if (label.ZIndex != Spec.TextZIndex)
            label.ZIndex = Spec.TextZIndex;
        if (label.HorizontalAlignment != HorizontalAlignment.Center)
            label.HorizontalAlignment = HorizontalAlignment.Center;
        if (label.VerticalAlignment != VerticalAlignment.Center)
            label.VerticalAlignment = VerticalAlignment.Center;
        if (label.MinFontSize != 22)
            label.MinFontSize = 22;
        if (label.MaxFontSize != 36)
            label.MaxFontSize = 36;
    }

    private static Texture2D? ClearCardTexture(NCard card) =>
        ClearCardTexture(card.Model!.GetType());

    private static void SetTextureIfDifferent(TextureRect textureRect, Texture2D? texture)
    {
        SakuraCardVisualInfrastructure.SetTextureIfDifferent(textureRect, texture);
    }

    private static bool HasTexture(TextureRect textureRect, Texture2D? texture) =>
        SakuraCardVisualInfrastructure.HasTexture(textureRect, texture);

    private static bool TryGetTexture(TextureRect textureRect, out Texture2D? texture)
    {
        return SakuraCardVisualInfrastructure.TryGetTexture(textureRect, out texture);
    }

    private static Texture2D? ClearCardTexture(Type cardType)
    {
        if (ClearCardArtCache.TryGetValue(cardType, out var cachedTexture))
        {
            if (cachedTexture is null || IsGodotInstanceUsable(cachedTexture))
                return cachedTexture;

            ClearCardArtCache.Remove(cardType);
        }

        var artPath = CardArtPath(cardType);
        var texture = ResourceLoader.Exists(artPath)
            ? ResourceLoader.Load<Texture2D>(artPath)
            : null;
        ClearCardArtCache[cardType] = texture;
        return texture;
    }

    private static Texture2D ClearCardHighlightTexture()
    {
        if (ClearCardHighlightTextureCache is { } cachedTexture && IsGodotInstanceUsable(cachedTexture))
            return cachedTexture;

        var box = Spec.HighlightBox;
        var imageScale = Spec.HighlightTextureScale;
        var width = Mathf.CeilToInt(box.Size.X * imageScale);
        var height = Mathf.CeilToInt(box.Size.Y * imageScale);
        var image = Image.CreateEmpty(width, height, false, Image.Format.Rgbaf);
        var cardCenter = -box.Position + Spec.RootSize * 0.5f;
        var cardHalfSize = Spec.RootSize * 0.5f;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var point = new Vector2((x + 0.5f) / imageScale, (y + 0.5f) / imageScale);
                var distance = RoundedRectDistance(point - cardCenter, cardHalfSize, Spec.HighlightCornerRadius);
                var alpha = 1f - Mathf.Clamp(Mathf.Abs(distance) / Spec.HighlightSdfRange, 0f, 1f);
                image.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        ClearCardHighlightTextureCache = ImageTexture.CreateFromImage(image);
        return ClearCardHighlightTextureCache;
    }

    private static Texture2D? DefaultHighlightTexture()
    {
        if (DefaultHighlightTextureCache is { } cachedTexture && IsGodotInstanceUsable(cachedTexture))
            return cachedTexture;

        const string path = "res://images/packed/card_template/card_frame_sdf.exr";
        if (!ResourceLoader.Exists(path))
            return null;

        DefaultHighlightTextureCache = ResourceLoader.Load<Texture2D>(path, null, ResourceLoader.CacheMode.Reuse);
        return DefaultHighlightTextureCache;
    }

    private static float RoundedRectDistance(Vector2 point, Vector2 halfSize, float radius)
    {
        return SakuraCardVisualInfrastructure.RoundedRectDistance(point, halfSize, radius);
    }

    private static string ClearCardArtFileName(Type cardType)
    {
        var name = cardType.Name;
        var builder = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var character = name[i];
            if (i > 0 && char.IsUpper(character) && char.IsLower(name[i - 1]))
                builder.Append('_');

            builder.Append(char.ToUpperInvariant(character));
        }

        builder.Append(".png");
        return builder.ToString();
    }

    private static string ClearCardEnglishName(Type cardType)
    {
        if (ClearCardEnglishNameCache.TryGetValue(cardType, out var name))
            return name;

        name = Path.GetFileNameWithoutExtension(ClearCardArtFileName(cardType)).Replace('_', ' ');
        ClearCardEnglishNameCache[cardType] = name;
        return name;
    }

    private static Color NameTextColor(CardModel model) =>
        model.IsUpgraded ? Spec.UpgradedNameTextColor : Spec.DefaultNameTextColor;

    private static void ApplySize(Control? control, Vector2 size)
    {
        SakuraCardVisualInfrastructure.ApplySize(control, size);
    }

    private static void ApplySize(Control? control, Vector2 size, Vector2 pivotOffset)
    {
        SakuraCardVisualInfrastructure.ApplySize(control, size, pivotOffset);
    }

    private static void ApplyBox(Control? control, Rect2 box)
    {
        SakuraCardVisualInfrastructure.ApplyBox(control, box);
    }

    private static string CurrentLanguageKey() =>
        LocManager.Instance?.Language ?? string.Empty;

    private static string FormatVector(Vector2 value) =>
        $"({FormatFloat(value.X)},{FormatFloat(value.Y)})";

    private static string FormatFloat(float value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string DescribeControl(Control? control)
    {
        if (control is null)
            return "null";
        if (!IsGodotInstanceUsable(control))
            return "invalid";

        return $"visible:{control.Visible};size:{FormatVector(control.Size)};pos:{FormatVector(control.Position)}";
    }

    private static string DescribeCanvasItem(CanvasItem? item)
    {
        if (item is null)
            return "null";
        if (!IsGodotInstanceUsable(item))
            return "invalid";

        return $"visible:{item.Visible};modAlpha:{FormatFloat(item.Modulate.A)};selfAlpha:{FormatFloat(item.SelfModulate.A)}";
    }

    private static string DescribeTextureRect(TextureRect? textureRect)
    {
        if (textureRect is null)
            return "null";
        if (!IsGodotInstanceUsable(textureRect))
            return "invalid";

        var hasTexture = TryGetTexture(textureRect, out var texture)
                         && texture is not null
                         && IsGodotInstanceUsable(texture);
        return $"{DescribeControl(textureRect)};texture={hasTexture};stretch={textureRect.StretchMode}";
    }

    private static string AlphaOf(CanvasItem? item)
    {
        if (item is null || !IsGodotInstanceUsable(item))
            return "n/a";

        return FormatFloat(item.SelfModulate.A);
    }

    private static IEnumerable<Control?> CardControls(NCard card, ClearCardNodes nodes)
    {
        yield return card.Body;
        yield return card.CardHighlight;
        yield return nodes.TitleLabel;
        yield return nodes.DescriptionLabel;
        yield return nodes.EnergyIcon;
        yield return nodes.EnergyLabel;
        yield return nodes.UnplayableEnergyIcon;
        yield return nodes.StarIcon;
        yield return nodes.StarLabel;
        yield return nodes.UnplayableStarIcon;
    }

    private static IEnumerable<Control> HiddenVanillaControls(NCard card, ClearCardState state, ClearCardNodes nodes)
    {
        foreach (var visual in state.GetOrCreateVanillaBodyVisuals(card).OfType<Control>())
            yield return visual;

        foreach (var hiddenNode in nodes.HiddenNodes.OfType<Control>())
            yield return hiddenNode;
    }

    private static IEnumerable<CanvasItem?> CardVisibilityItems(NCard card, ClearCardState state, ClearCardNodes nodes)
    {
        yield return card.CardHighlight;

        foreach (var item in state.GetOrCreateVanillaBodyVisuals(card))
            yield return item;

        foreach (var hiddenNode in nodes.HiddenNodes)
            yield return hiddenNode;
    }

    private static void HideVanillaBodyVisuals(NCard card, ClearCardState state)
    {
        foreach (var visual in state.GetOrCreateVanillaBodyVisuals(card))
            Hide(visual);
    }

    private static void HideLateRewardGlows(NCard card)
    {
        Hide(FieldValue<CanvasItem>(RareGlowField, card));
        Hide(FieldValue<CanvasItem>(UncommonGlowField, card));
    }

    private static List<CanvasItem> CreateVanillaBodyVisuals(NCard card, ClearCardState state)
    {
        var nodes = state.GetOrCreateNodes(card);
        var keptNodes = new HashSet<Node?>(
        [
            nodes.TitleLabel,
            nodes.DescriptionLabel,
            nodes.EnergyIcon,
            nodes.EnergyLabel,
            nodes.UnplayableEnergyIcon,
            nodes.StarIcon,
            nodes.StarLabel,
            nodes.UnplayableStarIcon,
            card.CardHighlight,
            state.Art,
            state.EnglishNameLabel,
            state.DescriptionPanel,
        ]);
        keptNodes.Remove(null);

        List<CanvasItem> visuals = [];
        CollectBodyChildrenExcept(card.Body, keptNodes, visuals);
        return visuals;
    }

    private static void CollectBodyChildrenExcept(Node node, IReadOnlySet<Node?> keptNodes, List<CanvasItem> visuals)
    {
        foreach (var child in node.GetChildren())
        {
            if (keptNodes.Contains(child))
                continue;

            if (keptNodes.Any(keptNode => keptNode is not null && child.IsAncestorOf(keptNode)))
            {
                CollectBodyChildrenExcept(child, keptNodes, visuals);
                continue;
            }

            if (child is CanvasItem canvasItem)
                visuals.Add(canvasItem);
        }
    }

    private static NClickableControl? HolderHitbox(NCardHolder holder) =>
        FieldValue<NClickableControl>(HolderHitboxField, holder);

    private static Control? HandFlash(NCardHolder holder) =>
        holder is NHandCardHolder handHolder
            ? FieldValue<Control>(HandFlashField, handHolder)
            : null;

    private static T? FieldValue<T>(FieldInfo? field, object instance)
        where T : class =>
        field?.GetValue(instance) as T;

    private static void Hide(object? node)
    {
        if (node is CanvasItem canvasItem && IsGodotInstanceUsable(canvasItem))
            canvasItem.Visible = false;
    }

    private static bool IsGodotInstanceUsable(GodotObject? instance)
    {
        return SakuraCardVisualInfrastructure.IsGodotInstanceUsable(instance);
    }

    private static bool TryGetOwnedBodyChild<T>(NCard card, T? node, int? childIndex, out T result)
        where T : Node
    {
        result = null!;
        if (!IsGodotInstanceUsable(card.Body) || !IsGodotInstanceUsable(node))
            return false;

        result = node!;
        AttachOwnedBodyChild(card, result, childIndex);
        return true;
    }

    private static void AttachOwnedBodyChild(NCard card, Node node, int? childIndex = null)
    {
        if (!IsGodotInstanceUsable(card.Body) || !IsGodotInstanceUsable(node))
            return;

        var parent = node.GetParent();
        if (!ReferenceEquals(parent, card.Body))
        {
            if (parent is not null && IsGodotInstanceUsable(parent))
                parent.RemoveChild(node);
            card.Body.AddChild(node);
        }

        if (childIndex is not { } index)
            return;

        var maxIndex = Mathf.Max(card.Body.GetChildCount() - 1, 0);
        card.Body.MoveChild(node, Mathf.Clamp(index, 0, maxIndex));
    }

    public static void ApplySelectionHighlightLayer(NCardHighlight highlight, bool selected)
    {
        if (ParentCard(highlight) is not { } card
            || !IsClearCard(card)
            || !HasAncestor<NCardGridSelectionScreen>(highlight))
            return;

        var targetZIndex = selected ? Spec.SelectionHighlightZIndex : Spec.HighlightZIndex;
        if (highlight.ZIndex != targetZIndex)
            highlight.ZIndex = targetZIndex;
    }

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

    private static float ShaderWidthValue(NCardHighlight? highlight)
    {
        if (highlight?.Material is not ShaderMaterial material)
            return 0f;

        return material.GetShaderParameter(HighlightWidthParameterName).AsSingle();
    }

    private static void AnimateStateHighlightWidth(NCardHighlight highlight)
    {
        if (highlight.Material is not ShaderMaterial material)
            return;

        KillHighlightTween(highlight);
        var tween = highlight.CreateTween();
        tween.TweenMethod(
                Callable.From<float>(value => material.SetShaderParameter(HighlightWidthParameterName, value)),
                ShaderWidthValue(highlight),
                Spec.StateHighlightWidth,
                Spec.StateHighlightShowDuration)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
        HighlightCurrentTweenField?.SetValue(highlight, tween);
    }

    private static void KillHighlightTween(NCardHighlight highlight)
    {
        if (FieldValue<Tween>(HighlightCurrentTweenField, highlight) is { } tween)
            tween.Kill();
    }

    private static void SetShaderWidth(NCardHighlight highlight, float width)
    {
        if (highlight.Material is ShaderMaterial material)
        {
            var currentWidth = material.GetShaderParameter(HighlightWidthParameterName).AsSingle();
            if (Mathf.Abs(currentWidth - width) > 0.001f)
                material.SetShaderParameter(HighlightWidthParameterName, width);
        }
    }

    private static bool Approximately(Color left, Color right)
    {
        const float tolerance = 0.001f;
        return Mathf.Abs(left.R - right.R) <= tolerance
            && Mathf.Abs(left.G - right.G) <= tolerance
            && Mathf.Abs(left.B - right.B) <= tolerance;
    }

    private static void RemoveKeys<TKey, TValue>(Dictionary<TKey, TValue> dictionary, List<TKey>? keys)
        where TKey : notnull
    {
        if (keys is null)
            return;

        foreach (var key in keys)
            dictionary.Remove(key);
    }

    private sealed class ClearCardState
    {
        private readonly Dictionary<Control, ControlSnapshot> _controlSnapshots = [];
        private readonly Dictionary<CanvasItem, bool> _visibilitySnapshots = [];
        private SizeSnapshot? _rootSnapshot;
        private bool _captured;
        private bool _isApplied;
        private TextureRect? _art;
        private Label? _englishNameLabel;
        private Panel? _descriptionPanel;
        private ClearCardNodes? _nodes;
        private List<CanvasItem>? _vanillaBodyVisuals;
        private ClearCardDescriptionCache? _descriptionCache;

        public TextureRect? Art => _art;

        public Label? EnglishNameLabel => _englishNameLabel;

        public Panel? DescriptionPanel => _descriptionPanel;

        public bool IsApplied => _isApplied;

        public string ClearCardDescriptionText(CardModel model, string currentText)
        {
            _descriptionCache ??= new ClearCardDescriptionCache();
            return _descriptionCache.Text(model, currentText);
        }

        public void Capture(NCard card)
        {
            if (_captured)
                return;

            var nodes = GetOrCreateNodes(card);
            _rootSnapshot = SizeSnapshot.Capture(card);
            foreach (var control in CardControls(card, nodes))
                CaptureControl(control);
            foreach (var control in HiddenVanillaControls(card, this, nodes))
                CaptureControl(control);

            foreach (var item in CardVisibilityItems(card, this, nodes))
                CaptureVisibility(item);

            _captured = true;
        }

        public void MarkApplied() => _isApplied = true;

        public void Restore(NCard card)
        {
            _rootSnapshot?.Restore(card);
            List<Control>? invalidControls = null;
            foreach (var (control, snapshot) in _controlSnapshots)
            {
                if (!IsGodotInstanceUsable(control))
                {
                    invalidControls ??= [];
                    invalidControls.Add(control);
                    continue;
                }

                snapshot.Restore(control);
            }
            RemoveKeys(_controlSnapshots, invalidControls);

            List<CanvasItem>? invalidCanvasItems = null;
            foreach (var (canvasItem, visible) in _visibilitySnapshots)
            {
                if (!IsGodotInstanceUsable(canvasItem))
                {
                    invalidCanvasItems ??= [];
                    invalidCanvasItems.Add(canvasItem);
                    continue;
                }

                canvasItem.Visible = visible;
            }
            RemoveKeys(_visibilitySnapshots, invalidCanvasItems);

            Hide(_art);
            Hide(_englishNameLabel);
            Hide(_descriptionPanel);
            RestoreVanillaHighlightDefaultsIfSakuraTextureLeaked(card);
            _rootSnapshot = null;
            _controlSnapshots.Clear();
            _visibilitySnapshots.Clear();
            _vanillaBodyVisuals = null;
            _captured = false;
            _isApplied = false;
        }

        public IReadOnlyList<CanvasItem> GetOrCreateVanillaBodyVisuals(NCard card)
        {
            _vanillaBodyVisuals ??= CreateVanillaBodyVisuals(card, this);
            return _vanillaBodyVisuals;
        }

        public ClearCardNodes GetOrCreateNodes(NCard card)
        {
            _nodes ??= ClearCardNodes.From(card);
            return _nodes;
        }

        public TextureRect GetOrCreateArt(NCard card)
        {
            if (TryGetOwnedBodyChild(card, _art, 0, out var existingArt))
                return existingArt;

            _art = new TextureRect
            {
                Name = "SakuraClearCardArt",
                MouseFilter = Control.MouseFilterEnum.Ignore
            };

            AttachOwnedBodyChild(card, _art, 0);
            return _art;
        }

        public Label GetOrCreateEnglishNameLabel(NCard card)
        {
            if (TryGetOwnedBodyChild(card, _englishNameLabel, null, out var existingLabel))
                return existingLabel;

            _englishNameLabel = new Label
            {
                Name = "SakuraClearCardEnglishName",
                MouseFilter = Control.MouseFilterEnum.Ignore
            };

            AttachOwnedBodyChild(card, _englishNameLabel);
            return _englishNameLabel;
        }

        public Panel GetOrCreateDescriptionPanel(NCard card)
        {
            var childIndex = Mathf.Min(2, Mathf.Max(card.Body.GetChildCount() - 1, 0));
            if (TryGetOwnedBodyChild(card, _descriptionPanel, childIndex, out var existingPanel))
                return existingPanel;

            _descriptionPanel = CreatePanel(
                "SakuraClearCardDescriptionPanel",
                Spec.DescriptionPanelColor,
                Spec.DescriptionPanelCornerRadius);
            AttachOwnedBodyChild(card, _descriptionPanel, childIndex);
            return _descriptionPanel;
        }

        private void CaptureControl(Control? control)
        {
            if (control is null || !IsGodotInstanceUsable(control) || _controlSnapshots.ContainsKey(control))
                return;

            _controlSnapshots[control] = ControlSnapshot.Capture(control);
        }

        private void CaptureVisibility(CanvasItem? canvasItem)
        {
            if (canvasItem is null || !IsGodotInstanceUsable(canvasItem) || _visibilitySnapshots.ContainsKey(canvasItem))
                return;

            _visibilitySnapshots[canvasItem] = canvasItem.Visible;
        }

        private static void RestoreVanillaHighlightDefaultsIfSakuraTextureLeaked(NCard card)
        {
            if (!SakuraCardVisualFamilies.IsVanilla(card))
                return;
            if (!IsGodotInstanceUsable(card.CardHighlight))
                return;

            var highlight = card.CardHighlight!;
            if (!IsSakuraRuntimeTexture(highlight.Texture))
                return;

            ApplyCenteredAnchors(highlight);
            if (highlight.Position != Spec.DefaultHighlightPosition)
                highlight.Position = Spec.DefaultHighlightPosition;
            if (highlight.Size != Spec.DefaultHighlightSize)
                highlight.Size = Spec.DefaultHighlightSize;
            if (highlight.CustomMinimumSize != Vector2.Zero)
                highlight.CustomMinimumSize = Vector2.Zero;
            if (highlight.Scale != Vector2.One)
                highlight.Scale = Vector2.One;
            if (highlight.PivotOffset != Spec.DefaultHighlightPivotOffset)
                highlight.PivotOffset = Spec.DefaultHighlightPivotOffset;
            if (highlight.ExpandMode != TextureRect.ExpandModeEnum.FitHeight)
                highlight.ExpandMode = TextureRect.ExpandModeEnum.FitHeight;
            if (highlight.StretchMode != TextureRect.StretchModeEnum.KeepAspectCentered)
                highlight.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            if (highlight.ZIndex != 0)
                highlight.ZIndex = 0;
            if (highlight.MouseFilter != Control.MouseFilterEnum.Ignore)
                highlight.MouseFilter = Control.MouseFilterEnum.Ignore;
            SetTextureIfDifferent(highlight, DefaultHighlightTexture());
        }

        private static bool IsSakuraRuntimeTexture(Texture2D? texture)
        {
            if (!IsGodotInstanceUsable(texture))
                return true;

            return string.IsNullOrEmpty(texture!.ResourcePath);
        }

        private static Panel CreatePanel(string name, Color color, int cornerRadius)
        {
            return SakuraCardVisualInfrastructure.CreatePanel(name, color, cornerRadius);
        }
    }

    private sealed class ClearCardDescriptionCache
    {
        private Type? _cardType;
        private string? _sourceText;
        private string? _language;
        private bool _released;
        private bool _temporary;
        private SakuraElementSet _elements;
        private string? _synchronizedLine;
        private string? _text;

        public string Text(CardModel model, string currentText)
        {
            var cardType = model.GetType();
            var language = CurrentLanguageKey();
            var released = model.IsReleased();
            var temporary = model.IsTemporary();
            var elements = SakuraActions.ElementSetOf(model);
            var synchronizedLine = SakuraStateText.SynchronizedLine(model);

            if (_text is not null
                && _cardType == cardType
                && _sourceText == currentText
                && _language == language
                && _released == released
                && _temporary == temporary
                && _elements == elements
                && _synchronizedLine == synchronizedLine)
                return _text;

            _cardType = cardType;
            _sourceText = currentText;
            _language = language;
            _released = released;
            _temporary = temporary;
            _elements = elements;
            _synchronizedLine = synchronizedLine;
            _text = ClearCardLayout.ClearCardDescriptionText(model, currentText, synchronizedLine);
            return _text;
        }
    }

    private sealed class ClearCardNodes
    {
        private ClearCardNodes(
            MegaLabel? titleLabel,
            MegaRichTextLabel? descriptionLabel,
            TextureRect? energyIcon,
            MegaLabel? energyLabel,
            TextureRect? unplayableEnergyIcon,
            TextureRect? starIcon,
            MegaLabel? starLabel,
            TextureRect? unplayableStarIcon,
            IReadOnlyList<CanvasItem> hiddenNodes)
        {
            TitleLabel = titleLabel;
            DescriptionLabel = descriptionLabel;
            EnergyIcon = energyIcon;
            EnergyLabel = energyLabel;
            UnplayableEnergyIcon = unplayableEnergyIcon;
            StarIcon = starIcon;
            StarLabel = starLabel;
            UnplayableStarIcon = unplayableStarIcon;
            HiddenNodes = hiddenNodes;
        }

        public MegaLabel? TitleLabel { get; }

        public MegaRichTextLabel? DescriptionLabel { get; }

        public TextureRect? EnergyIcon { get; }

        public MegaLabel? EnergyLabel { get; }

        public TextureRect? UnplayableEnergyIcon { get; }

        public TextureRect? StarIcon { get; }

        public MegaLabel? StarLabel { get; }

        public TextureRect? UnplayableStarIcon { get; }

        public IReadOnlyList<CanvasItem> HiddenNodes { get; }

        public static ClearCardNodes From(NCard card) =>
            new(
                FieldValue<MegaLabel>(TitleLabelField, card),
                FieldValue<MegaRichTextLabel>(DescriptionLabelField, card),
                FieldValue<TextureRect>(EnergyIconField, card),
                FieldValue<MegaLabel>(EnergyLabelField, card),
                FieldValue<TextureRect>(UnplayableEnergyIconField, card),
                FieldValue<TextureRect>(StarIconField, card),
                FieldValue<MegaLabel>(StarLabelField, card),
                FieldValue<TextureRect>(UnplayableStarIconField, card),
                HiddenCardNodeFields
                    .Select(field => field.GetValue(card))
                    .OfType<CanvasItem>()
                    .Distinct()
                    .ToList());
    }

    private sealed class ClearCardHolderState
    {
        private readonly Dictionary<Control, ControlSnapshot> _controlSnapshots = [];
        private SizeSnapshot? _rootSnapshot;
        private Vector2? _cardNodePosition;
        private bool _captured;
        private bool _isApplied;

        public bool IsApplied => _isApplied;

        public void Capture(NCardHolder holder)
        {
            if (_captured)
                return;

            _rootSnapshot = SizeSnapshot.Capture(holder);
            CaptureControl(HolderHitbox(holder));
            CaptureControl(HandFlash(holder));
            _cardNodePosition = holder.CardNode?.Position;
            _captured = true;
        }

        public void MarkApplied() => _isApplied = true;

        public void Restore(NCardHolder holder)
        {
            _rootSnapshot?.Restore(holder);
            List<Control>? invalidControls = null;
            foreach (var (control, snapshot) in _controlSnapshots)
            {
                if (!IsGodotInstanceUsable(control))
                {
                    invalidControls ??= [];
                    invalidControls.Add(control);
                    continue;
                }

                snapshot.Restore(control);
            }
            RemoveKeys(_controlSnapshots, invalidControls);

            var cardNode = holder.CardNode;
            if (_cardNodePosition is { } cardNodePosition
                && cardNode is not null
                && IsGodotInstanceUsable(cardNode)
                && cardNode.Position != cardNodePosition)
                cardNode.Position = cardNodePosition;

            _rootSnapshot = null;
            _controlSnapshots.Clear();
            _cardNodePosition = null;
            _captured = false;
            _isApplied = false;
        }

        private void CaptureControl(Control? control)
        {
            if (control is null || !IsGodotInstanceUsable(control) || _controlSnapshots.ContainsKey(control))
                return;

            _controlSnapshots[control] = ControlSnapshot.Capture(control);
        }
    }

    private sealed class ClearCardGridState
    {
        public bool AllCardsAreClearCards { get; set; }
        public bool NeedsDeferredCenter { get; set; }
        public bool DeferredCenterQueued { get; set; }
        public int DeferredCenterAttempts { get; set; }
    }

    private readonly record struct ClearCardLayoutContext(
        Rect2 RootBox,
        Vector2 RootPivotOffset,
        SubViewport? TransformVfxViewport)
    {
        public static ClearCardLayoutContext For(NCard card)
        {
            var context = new ClearCardLayoutContext(
                Spec.DefaultCardCenteredRootBox,
                Spec.DefaultRootPivotOffset,
                null);
            var priority = 0;
            var parent = card.GetParent();
            if (parent is NCardHolder || IsGeneratedTransparentHandEntryCard(card, parent))
            {
                context = ForCenteredOrigin(null);
                priority = 4;
            }

            SubViewport? viewport = null;
            for (; parent is not null; parent = parent.GetParent())
            {
                if (parent is SubViewport subViewport)
                    viewport = subViewport;

                if (parent is NCardTransformVfx)
                {
                    context = priority < 3
                        ? ForCenteredOrigin(viewport)
                        : context with { TransformVfxViewport = viewport };
                    break;
                }

                if (parent is NMerchantCard && priority < 2)
                {
                    context = ForCenteredOrigin(null);
                    priority = 2;
                    continue;
                }

                if (parent is NInspectCardScreen && priority < 1)
                {
                    context = new ClearCardLayoutContext(Spec.CenteredRootBox, Vector2.Zero, null);
                    priority = 1;
                }
            }

            return context;
        }

        private static bool IsGeneratedTransparentHandEntryCard(NCard card, Node? parent) =>
            parent is NCombatUi
            && card.DisplayingPile == PileType.Hand
            && SakuraGeneratedCardLifecycle.IsGeneratedTransparentHandVisualCard(card.Model);

        private static ClearCardLayoutContext ForCenteredOrigin(SubViewport? transformVfxViewport) =>
            new(Spec.CenteredRootBox, Spec.DefaultRootPivotOffset, transformVfxViewport);
    }

    private sealed class ClearCardLayoutSpec
    {
        private const float SizeScale = 1.05f;

        public Vector2 RootSize { get; } = Scaled(new Vector2(206f, 450f));
        public Vector2 LayoutSize => RootSize;
        public Vector2 DefaultCardSize => NCard.defaultSize;
        public Vector2 DefaultCardCenteredOffset => (DefaultCardSize - RootSize) * 0.5f;
        public Vector2 HolderVisualOffset => Vector2.Zero;
        public Vector2 DefaultRootPivotOffset => RootSize * 0.5f;
        public Rect2 RootBox => new(Vector2.Zero, RootSize);
        public Rect2 DefaultCardCenteredRootBox => new(DefaultCardCenteredOffset, RootSize);
        public Rect2 CenteredRootBox => new(RootSize * -0.5f + HolderVisualOffset, RootSize);
        public Rect2 ArtBox => RootBox;
        public Vector2 HighlightMargin { get; } = Scaled(new Vector2(32f, 36f));
        public Rect2 HighlightBox => new(-HighlightMargin, RootSize + HighlightMargin * 2f);
        public Rect2 CenteredHighlightBox => new(CenteredRootBox.Position - HighlightMargin, HighlightBox.Size);
        public Rect2 TitleBox { get; } = Scaled(new Rect2(new Vector2(23f, 8f), new Vector2(160f, 34f)));
        public Rect2 EnglishNameBox { get; } = Scaled(new Rect2(new Vector2(23f, 396f), new Vector2(160f, 30f)));
        public Rect2 DescriptionPanelBox { get; } = Scaled(new Rect2(new Vector2(12f, 230f), new Vector2(182f, 156f)));
        public Rect2 DescriptionBox { get; } = Scaled(new Rect2(new Vector2(16f, 238f), new Vector2(174f, 140f)));
        public Rect2 EnergyCostBox { get; } = Scaled(new Rect2(new Vector2(-14f, -12f), new Vector2(56f, 56f)));
        public Rect2 EnergyCostLabelBox { get; } = Scaled(new Rect2(new Vector2(12f, -2f), new Vector2(44f, 44f)));
        public Rect2 StarCostBox { get; } = Scaled(new Rect2(new Vector2(174f, 16f), new Vector2(44f, 44f)));
        public Vector2 DefaultGridCellSize => NCard.defaultSize * NCardHolder.smallScale;
        public Vector2 GridCellSize => new(
            Mathf.Max(DefaultGridCellSize.X, RootSize.X * NCardHolder.smallScale.X),
            Mathf.Max(DefaultGridCellSize.Y, RootSize.Y * NCardHolder.smallScale.Y));
        public float GridCardPadding { get; } = Scaled(40f);
        public float ClearCardGridVerticalOffset { get; } = Scaled(-36f);
        public Color DescriptionPanelColor { get; } = new(0f, 0f, 0f, 0.72f);
        public Color DefaultNameTextColor { get; } = new(1f, 1f, 1f, 1f);
        public Color UpgradedNameTextColor => SakuraCardVisualStyle.UpgradedNameTextColor;
        public Color NameTextOutlineColor { get; } = new(0.03f, 0.03f, 0.03f, 0.95f);
        public Color TitleTextShadowColor { get; } = new(0f, 0f, 0f, 0.1882353f);
        public float HighlightCornerRadius { get; } = Scaled(16f);
        public float HighlightSdfRange { get; } = Scaled(240f);
        public float HighlightTextureScale { get; } = 2f;
        public float StateHighlightWidth { get; } = 0.12f;
        public float StateHighlightShowDuration { get; } = 0.32f;
        public float HandClearPairGapAdjustment { get; } = Scaled(-30f);
        public float HandMixedPairGapAdjustment { get; } = Scaled(22f);
        public float HandMinimumAdjacentGap { get; } = Scaled(96f);
        public float HeaderLineUnits { get; } = Scaled(14f);
        public Vector2 DefaultHighlightPosition { get; } = new(-381f, -475f);
        public Vector2 DefaultHighlightSize { get; } = new(759f, 951f);
        public Vector2 DefaultHighlightPivotOffset { get; } = new(150f, 211f);
        public int TitleTextFontSize { get; } = ScaledToInt(26);
        public int TitleTextOutlineSize { get; } = ScaledToInt(12);
        public int TitleTextShadowOffset { get; } = ScaledToInt(2);
        public int TitleTextShadowOutlineSize { get; } = ScaledToInt(12);
        public int EnglishNameFontSize { get; } = ScaledToInt(22);
        public int NameTextOutlineSize { get; } = ScaledToInt(3);
        public int DescriptionPanelCornerRadius { get; } = ScaledToInt(4);
        public int ArtZIndex { get; } = 0;
        public int HighlightZIndex { get; } = -1;
        public int SelectionHighlightZIndex { get; } = 1;
        public int DescriptionPanelZIndex { get; } = 0;
        public int TextZIndex { get; } = 0;

        private static Vector2 Scaled(Vector2 value) =>
            value * SizeScale;

        private static Rect2 Scaled(Rect2 value) =>
            new(value.Position * SizeScale, value.Size * SizeScale);

        private static float Scaled(float value) =>
            value * SizeScale;

        private static int ScaledToInt(int value) =>
            Mathf.RoundToInt(value * SizeScale);
    }

    private readonly record struct SizeSnapshot(
        Vector2 Size,
        Vector2 CustomMinimumSize,
        Vector2 PivotOffset)
    {
        public static SizeSnapshot Capture(Control control) =>
            new(control.Size, control.CustomMinimumSize, control.PivotOffset);

        public void Restore(Control control)
        {
            if (control.Size != Size)
                control.Size = Size;
            if (control.CustomMinimumSize != CustomMinimumSize)
                control.CustomMinimumSize = CustomMinimumSize;
            if (control.PivotOffset != PivotOffset)
                control.PivotOffset = PivotOffset;
        }
    }

    private readonly record struct ControlSnapshot(
        Vector2 Position,
        Vector2 Size,
        Vector2 CustomMinimumSize,
        Vector2 Scale,
        Vector2 PivotOffset,
        Color Modulate,
        Color SelfModulate,
        int ZIndex,
        float AnchorLeft,
        float AnchorTop,
        float AnchorRight,
        float AnchorBottom,
        ThemeColorSnapshot FontColor,
        ThemeColorSnapshot FontOutlineColor,
        ThemeConstantSnapshot OutlineSize,
        ThemeFontSizeSnapshot FontSize,
        TextureRect.ExpandModeEnum? TextureExpandMode,
        TextureRect.StretchModeEnum? TextureStretchMode,
        TextureSnapshot? Texture)
    {
        public static ControlSnapshot Capture(Control control) =>
            new(
                control.Position,
                control.Size,
                control.CustomMinimumSize,
                control.Scale,
                control.PivotOffset,
                control.Modulate,
                control.SelfModulate,
                control.ZIndex,
                control.AnchorLeft,
                control.AnchorTop,
                control.AnchorRight,
                control.AnchorBottom,
                ThemeColorSnapshot.Capture(control, FontColorName),
                ThemeColorSnapshot.Capture(control, FontOutlineColorName),
                ThemeConstantSnapshot.Capture(control, OutlineSizeName),
                ThemeFontSizeSnapshot.Capture(control, FontSizeName),
                (control as TextureRect)?.ExpandMode,
                (control as TextureRect)?.StretchMode,
                control is TextureRect textureRect
                    ? TextureSnapshot.Capture(textureRect.Texture)
                    : null);

        public void Restore(Control control)
        {
            if (control.AnchorLeft != AnchorLeft)
                control.AnchorLeft = AnchorLeft;
            if (control.AnchorTop != AnchorTop)
                control.AnchorTop = AnchorTop;
            if (control.AnchorRight != AnchorRight)
                control.AnchorRight = AnchorRight;
            if (control.AnchorBottom != AnchorBottom)
                control.AnchorBottom = AnchorBottom;
            if (control.Position != Position)
                control.Position = Position;
            if (control.Size != Size)
                control.Size = Size;
            if (control.CustomMinimumSize != CustomMinimumSize)
                control.CustomMinimumSize = CustomMinimumSize;
            if (control.Scale != Scale)
                control.Scale = Scale;
            if (control.PivotOffset != PivotOffset)
                control.PivotOffset = PivotOffset;
            if (control.Modulate != Modulate)
                control.Modulate = Modulate;
            if (control.SelfModulate != SelfModulate)
                control.SelfModulate = SelfModulate;
            if (control.ZIndex != ZIndex)
                control.ZIndex = ZIndex;
            FontColor.Restore(control);
            FontOutlineColor.Restore(control);
            OutlineSize.Restore(control);
            FontSize.Restore(control);
            if (control is TextureRect textureRect && TextureStretchMode is not null && TextureExpandMode is not null)
            {
                if (textureRect.ExpandMode != TextureExpandMode.Value)
                    textureRect.ExpandMode = TextureExpandMode.Value;
                if (textureRect.StretchMode != TextureStretchMode.Value)
                    textureRect.StretchMode = TextureStretchMode.Value;
                Texture?.Restore(textureRect);
            }
        }
    }

    private readonly record struct TextureSnapshot(Texture2D? Texture, string? ResourcePath)
    {
        public static TextureSnapshot Capture(Texture2D? texture)
        {
            if (texture is null || !IsGodotInstanceUsable(texture))
                return new TextureSnapshot(null, null);

            return new TextureSnapshot(texture, texture.ResourcePath);
        }

        public void Restore(TextureRect textureRect)
        {
            SetTextureIfDifferent(textureRect, ResolveTexture());
        }

        private Texture2D? ResolveTexture()
        {
            if (!string.IsNullOrEmpty(ResourcePath) && ResourceLoader.Exists(ResourcePath))
                return ResourceLoader.Load<Texture2D>(ResourcePath, null, ResourceLoader.CacheMode.Reuse);
            if (IsGodotInstanceUsable(Texture) && !string.IsNullOrEmpty(Texture!.ResourcePath))
                return Texture;

            return null;
        }
    }

    private readonly record struct ThemeColorSnapshot(
        StringName Name,
        bool HadOverride,
        Color Color)
    {
        public static ThemeColorSnapshot Capture(Control control, StringName name) =>
            new(
                name,
                control.HasThemeColorOverride(name),
                control.HasThemeColorOverride(name) ? control.GetThemeColor(name) : default);

        public void Restore(Control control)
        {
            if (!HadOverride)
            {
                if (control.HasThemeColorOverride(Name))
                    control.RemoveThemeColorOverride(Name);
                return;
            }

            ApplyThemeColorOverride(control, Name, Color);
        }
    }

    private readonly record struct ThemeConstantSnapshot(
        StringName Name,
        bool HadOverride,
        int Value)
    {
        public static ThemeConstantSnapshot Capture(Control control, StringName name) =>
            new(
                name,
                control.HasThemeConstantOverride(name),
                control.HasThemeConstantOverride(name) ? control.GetThemeConstant(name) : default);

        public void Restore(Control control)
        {
            if (!HadOverride)
            {
                if (control.HasThemeConstantOverride(Name))
                    control.RemoveThemeConstantOverride(Name);
                return;
            }

            ApplyThemeConstantOverride(control, Name, Value);
        }
    }

    private readonly record struct ThemeFontSizeSnapshot(
        StringName Name,
        bool HadOverride,
        int Value)
    {
        public static ThemeFontSizeSnapshot Capture(Control control, StringName name) =>
            new(
                name,
                control.HasThemeFontSizeOverride(name),
                control.HasThemeFontSizeOverride(name) ? control.GetThemeFontSize(name) : default);

        public void Restore(Control control)
        {
            if (!HadOverride)
            {
                if (control.HasThemeFontSizeOverride(Name))
                    control.RemoveThemeFontSizeOverride(Name);
                return;
            }

            ApplyThemeFontSizeOverride(control, Name, Value);
        }
    }
}
