using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.addons.mega_text;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Classic.Character;
using SakuraMod.SakuraModCode.Extensions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace SakuraMod.SakuraModCode.Classic.Cards;

[HarmonyPatch(typeof(NCard))]
public static class ClassicSakuraCardVisualPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(NCard.UpdateVisuals))]
    public static void UpdateVisualsPrefix(NCard __instance)
    {
        ClassicSakuraCardLayout.RestoreCardIfTracked(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NCard.UpdateVisuals))]
    public static void UpdateVisualsPostfix(NCard __instance)
    {
        ClassicSakuraCardLayout.Apply(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NCard.ActivateRewardScreenGlow))]
    public static void ActivateRewardScreenGlowPostfix(NCard __instance)
    {
        ClassicSakuraCardLayout.Apply(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NCard.GetCurrentSize))]
    public static void GetCurrentSizePostfix(NCard __instance, ref Vector2 __result)
    {
        if (ClassicSakuraCardLayout.IsClassicCard(__instance))
            __result = ClassicSakuraCardLayout.CurrentSize(__instance);
    }
}

[HarmonyPatch(typeof(NCardHolder))]
public static class ClassicSakuraCardHolderPatch
{
    [HarmonyPrefix]
    [HarmonyPatch("SetCard")]
    public static void SetCardPrefix(NCardHolder __instance)
    {
        ClassicSakuraCardLayout.RestoreHolderIfTracked(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch("SetCard")]
    public static void SetCardPostfix(NCardHolder __instance)
    {
        ClassicSakuraCardLayout.Apply(__instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch("OnCardReassigned")]
    public static void OnCardReassignedPrefix(NCardHolder __instance)
    {
        ClassicSakuraCardLayout.RestoreHolderIfTracked(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch("OnCardReassigned")]
    public static void OnCardReassignedPostfix(NCardHolder __instance)
    {
        ClassicSakuraCardLayout.Apply(__instance);
    }
}

[HarmonyPatch(typeof(NPlayerHand))]
public static class ClassicSakuraPlayerHandPatch
{
    [HarmonyPrefix]
    [HarmonyPatch("RefreshLayout")]
    public static void RefreshLayoutPrefix(NPlayerHand __instance)
    {
        ClassicSakuraCardLayout.Apply(__instance.ActiveHolders);
    }

    [HarmonyPostfix]
    [HarmonyPatch("RefreshLayout")]
    public static void RefreshLayoutPostfix(NPlayerHand __instance)
    {
        ClassicSakuraCardLayout.Apply(__instance.ActiveHolders);
        ClassicSakuraCardLayout.ApplyHandSpacing(__instance);
    }
}

[HarmonyPatch(typeof(NHandCardHolder))]
public static class ClassicSakuraHandHolderPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(NHandCardHolder._Ready))]
    public static void ReadyPostfix(NHandCardHolder __instance)
    {
        ClassicSakuraCardLayout.Apply(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NHandCardHolder.UpdateCard))]
    public static void UpdateCardPostfix(NHandCardHolder __instance)
    {
        ClassicSakuraCardLayout.Apply(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NHandCardHolder.SetDefaultTargets))]
    public static void SetDefaultTargetsPostfix(NHandCardHolder __instance)
    {
        ClassicSakuraCardLayout.Apply(__instance);
    }
}

[HarmonyPatch(typeof(NPreviewCardHolder))]
public static class ClassicSakuraPreviewHolderPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(NPreviewCardHolder._Ready))]
    public static void ReadyPostfix(NPreviewCardHolder __instance)
    {
        ClassicSakuraCardLayout.Apply(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NPreviewCardHolder.SetCardScale))]
    public static void SetCardScalePostfix(NPreviewCardHolder __instance)
    {
        ClassicSakuraCardLayout.Apply(__instance);
    }
}

[HarmonyPatch(typeof(NCardGrid))]
public static class ClassicSakuraCardGridPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(NCardGrid.SetCards))]
    public static void SetCardsPrefix(NCardGrid __instance, IReadOnlyList<CardModel> cardsToDisplay)
    {
        ClassicSakuraCardLayout.ApplyGridCardSize(__instance, cardsToDisplay);
    }

    [HarmonyPostfix]
    [HarmonyPatch("UpdateGridPositions")]
    public static void UpdateGridPositionsPostfix(NCardGrid __instance)
    {
        ClassicSakuraCardLayout.CenterGridRows(__instance);
    }
}

[HarmonyPatch(typeof(NCardHighlight))]
public static class ClassicSakuraCardSelectionHighlightPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(NCardHighlight.AnimShow))]
    public static void AnimShowPostfix(NCardHighlight __instance)
    {
        ClassicSakuraCardLayout.ApplySelectionHighlightLayer(__instance, selected: true);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NCardHighlight.AnimHide))]
    public static void AnimHidePostfix(NCardHighlight __instance)
    {
        ClassicSakuraCardLayout.ApplySelectionHighlightLayer(__instance, selected: false);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NCardHighlight.AnimHideInstantly))]
    public static void AnimHideInstantlyPostfix(NCardHighlight __instance)
    {
        ClassicSakuraCardLayout.ApplySelectionHighlightLayer(__instance, selected: false);
    }
}

internal static class ClassicSakuraVisualAssets
{
    private static readonly Dictionary<string, Texture2D> TextureCache = [];

    public static IEnumerable<string> RunAssetPaths(ClassicSakuraCard card)
    {
        if (IsFullFaceFamily(card.Family))
        {
            yield return FullFacePath(card);
            yield return UnknownFullFacePath(card.Family);
            if (EnergyIconPath(card) is { } energyIconPath)
                yield return energyIconPath;
            yield return card.PortraitPath;
            yield return FlashPath();
            yield break;
        }

        yield return card.PortraitPath;
    }

    public static Texture2D Texture(string path)
    {
        if (TextureCache.TryGetValue(path, out var cachedTexture))
        {
            if (IsGodotInstanceUsable(cachedTexture))
                return cachedTexture;

            TextureCache.Remove(path);
        }

        if (!ResourceLoader.Exists(path))
            throw new FileNotFoundException($"Missing Classic Sakura visual texture: {path}", path);

        var texture = ResourceLoader.Load<Texture2D>(path, null, ResourceLoader.CacheMode.Reuse)
            ?? throw new InvalidOperationException($"Failed to load Classic Sakura visual texture: {path}");
        TextureCache[path] = texture;
        return texture;
    }

    public static string FullFacePath(ClassicSakuraCard card) =>
        FullFacePath(card.Family, ClassicSakuraCardCatalog.ArtStem(card.GetType()).NormalClassicArtStem());

    public static string UnknownFullFacePath(ClassicSakuraCardFamily family) =>
        FullFacePath(family, "unknown.png");

    public static string FullFacePath(ClassicSakuraCardFamily family, string fileName) =>
        Path.Join(FullFaceFamilyDirectory(family), fileName).ClassicFullFaceImagePath();

    public static string FlashPath() =>
        Path.Join("general", "flash", "flash.png").ClassicCardUiImagePath();

    public static string? EnergyIconPath(ClassicSakuraCard card) =>
        card.Family switch
        {
            ClassicSakuraCardFamily.Clow => ClassicSakuraEnergyIcon.ClowBigPath,
            ClassicSakuraCardFamily.Sakura => ClassicSakuraEnergyIcon.SakuraBigPath,
            ClassicSakuraCardFamily.Spell when card is SpellSeal or SpellRelease or SpellTurn => ClassicSakuraEnergyIcon.ClowBigPath,
            _ => null
        };

    private static string FullFaceFamilyDirectory(ClassicSakuraCardFamily family) =>
        family switch
        {
            ClassicSakuraCardFamily.Clow => "clow",
            ClassicSakuraCardFamily.Sakura => "sakura",
            ClassicSakuraCardFamily.Spell => "spell",
            _ => throw new InvalidOperationException($"Classic full-card face assets are not defined for {family} cards.")
        };

    private static bool IsFullFaceFamily(ClassicSakuraCardFamily family) =>
        family is ClassicSakuraCardFamily.Clow or ClassicSakuraCardFamily.Sakura or ClassicSakuraCardFamily.Spell;

    private static bool IsGodotInstanceUsable(GodotObject? instance)
    {
        try
        {
            return instance is not null
                && GodotObject.IsInstanceValid(instance)
                && (instance is not Node node || !node.IsQueuedForDeletion());
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }
}

internal static class ClassicSakuraCardLayout
{
    private static readonly ClassicSakuraCardLayoutSpec Spec = new();

    private static readonly string[] HiddenCardNodeFieldNames =
    [
        "_ancientBanner",
        "_ancientBorder",
        "_ancientHighlight",
        "_ancientPortrait",
        "_ancientTextBg",
        "_banner",
        "_cardOverlay",
        "_cardPortraitShadow",
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

    private static readonly StringName FontColorName = new("font_color");
    private static readonly StringName DefaultColorName = new("default_color");
    private static readonly StringName FontOutlineColorName = new("font_outline_color");
    private static readonly StringName FontSizeName = new("font_size");
    private static readonly StringName OutlineSizeName = new("outline_size");
    private static readonly StringName FontShadowColorName = new("font_shadow_color");
    private static readonly StringName ShadowOffsetXName = new("shadow_offset_x");
    private static readonly StringName ShadowOffsetYName = new("shadow_offset_y");
    private static readonly StringName ShadowOutlineSizeName = new("shadow_outline_size");
    private static readonly StringName HighlightWidthParameterName = new("width");
    private static readonly StringName PanelStyleName = new("panel");
    private const int MaxDeferredGridCenterAttempts = 4;

    private static readonly Dictionary<Vector2I, Texture2D> HighlightTextureCache = [];
    private static readonly ConditionalWeakTable<NCard, ClassicCardState> CardStates = new();
    private static readonly ConditionalWeakTable<NCardHolder, ClassicHolderState> HolderStates = new();
    private static readonly ConditionalWeakTable<NCardGrid, ClassicGridState> GridStates = new();

    public static bool IsClassicCard(NCard? card) =>
        SakuraCardVisualFamilies.IsClassic(card);

    public static bool IsClassicCard(CardModel? card) =>
        SakuraCardVisualFamilies.IsClassic(card);

    public static Vector2 CurrentSize(NCard card) =>
        Spec.LayoutSize * card.Scale;

    public static void RestoreCardIfTracked(NCard card)
    {
        if (CardStates.TryGetValue(card, out var existingState))
            existingState.Restore(card);
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

        var allCardsAreClassic = AllCardsAreClassic(cards);
        var state = GridStates.GetOrCreateValue(grid);
        state.AllCardsAreClassic = allCardsAreClassic;
        state.NeedsDeferredCenter = allCardsAreClassic;
        state.DeferredCenterAttempts = 0;
        state.DeferredCenterQueued = false;
        GridCardSizeField.SetValue(grid, allCardsAreClassic ? Spec.GridCellSize : Spec.DefaultGridCellSize);
    }

    public static void CenterGridRows(NCardGrid grid)
    {
        if (!GridStates.TryGetValue(grid, out var state) || !state.AllCardsAreClassic)
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

    public static void Apply(NCard card)
    {
        if (!IsClassicCard(card))
        {
            if (CardStates.TryGetValue(card, out var existingState))
            {
                existingState.Restore(card);
                ApplyParentHolder(card);
            }

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

        if (!IsClassicCard(holder.CardNode))
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
        var previousIsClassic = IsClassicCard(holders[0].CardNode);
        var hasClassic = previousIsClassic;

        for (var i = 1; i < holders.Count; i++)
        {
            var holder = holders[i];
            var originalPosition = holder.TargetPosition;
            var currentIsClassic = IsClassicCard(holder.CardNode);
            hasClassic |= currentIsClassic;

            var originalGap = originalPosition.X - previousOriginalX;
            if (originalGap <= 0f)
                return;

            var adjustedGap = Mathf.Max(
                Spec.HandMinimumAdjacentGap,
                originalGap + HandPairGapAdjustment(previousIsClassic, currentIsClassic));
            adjustedXs[i] = adjustedXs[i - 1] + adjustedGap;
            previousOriginalX = originalPosition.X;
            previousIsClassic = currentIsClassic;
        }

        if (!hasClassic)
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

    public static void ApplySelectionHighlightLayer(NCardHighlight highlight, bool selected)
    {
        if (ParentCard(highlight) is not { } card
            || !IsClassicCard(card)
            || !HasAncestor<NCardGridSelectionScreen>(highlight))
            return;

        var targetZIndex = selected ? Spec.SelectionHighlightZIndex : Spec.HighlightZIndex;
        if (highlight.ZIndex != targetZIndex)
            highlight.ZIndex = targetZIndex;
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
            var visibleHolderCount = ClassicGridRowHolderCount(cardRows[rowIndex]);
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
            ? (grid.Size.Y - contentHeight) * 0.5f + cardSize.Y * 0.5f - scrollContainer.Position.Y + Spec.GridVerticalOffset
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

    private static void ScheduleDeferredGridCenter(NCardGrid grid, ClassicGridState state)
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

    private static float HandPairGapAdjustment(bool leftIsClassic, bool rightIsClassic) =>
        (leftIsClassic, rightIsClassic) switch
        {
            (true, true) => Spec.HandClassicPairGapAdjustment,
            (true, false) or (false, true) => Spec.HandMixedPairGapAdjustment,
            _ => 0f
        };

    private static int ClassicGridRowHolderCount(IReadOnlyList<NGridCardHolder> row)
    {
        var count = 0;
        for (var i = 0; i < row.Count; i++)
        {
            var holder = row[i];
            if (!IsVisibleGridCardHolder(holder))
                continue;
            if (!SakuraCardVisualFamilies.IsClassic(holder.CardModel))
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

    private static bool AllCardsAreClassic(IReadOnlyList<CardModel> cards)
    {
        if (cards.Count == 0)
            return false;

        for (var i = 0; i < cards.Count; i++)
        {
            if (!SakuraCardVisualFamilies.IsClassic(cards[i]))
                return false;
        }

        return true;
    }

    private static void ApplyCardLayout(NCard card, ClassicCardState state)
    {
        if (card.Model is not ClassicSakuraCard model)
            return;
        if (!IsGodotInstanceUsable(card.Body))
            return;
        if (card.CardHighlight is not { } highlight)
            return;

        var nodes = state.GetOrCreateNodes(card);
        var layout = ClassicLayoutContext.For(card);
        EnsureTransformVfxViewportFits(layout.TransformVfxViewport);

        ApplySize(card, Spec.RootSize, layout.RootPivotOffset);
        ApplyBox(card.Body, layout.RootBox);
        var transparentBody = new Color(1f, 1f, 1f, 0f);
        if (card.Body.SelfModulate != transparentBody)
            card.Body.SelfModulate = transparentBody;

        var showFaceIdentity = card.Visibility == ModelVisibility.Visible;
        var facePath = showFaceIdentity
            ? ClassicSakuraVisualAssets.FullFacePath(model)
            : ClassicSakuraVisualAssets.UnknownFullFacePath(model.Family);
        ApplyTextureLayer(state.GetOrCreateFace(card), Spec.RootBox, facePath, Spec.ArtZIndex);
        ApplyPanelLayout(state.GetOrCreateDescriptionPanel(card), Spec.DescriptionPanelBox, Spec.DescriptionPanelZIndex, showFaceIdentity);

        ApplyHighlightLayout(highlight, Spec.HighlightBox);
        ApplyTitleLayout(nodes.TitleLabel, model);
        ApplyEnglishNameLayout(
            state.GetOrCreateEnglishNameLabel(card),
            model,
            showFaceIdentity && model.Family is not ClassicSakuraCardFamily.Spell);
        ApplyDescriptionLayout(nodes.DescriptionLabel);
        if (ShouldShowCost(model))
        {
            ApplyCostLayout(
                nodes.EnergyIcon,
                nodes.EnergyLabel,
                nodes.UnplayableEnergyIcon,
                Spec.EnergyCostBox,
                Spec.EnergyCostLabelBox,
                ClassicSakuraVisualAssets.EnergyIconPath(model));
        }
        else
        {
            Hide(nodes.EnergyIcon);
            Hide(nodes.EnergyLabel);
            Hide(nodes.UnplayableEnergyIcon);
        }

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

    private static bool ShouldShowCost(ClassicSakuraCard model) =>
        model is SpellSeal or SpellRelease || model.Family is not ClassicSakuraCardFamily.Spell;

    private static void ApplyHolderLayout(NCardHolder holder)
    {
        ApplyBox(HolderHitbox(holder), Spec.CenteredRootBox);
        ApplyHandFlashLayout(holder);
        if (holder.CardNode is not null && holder.CardNode.Position != Vector2.Zero)
            holder.CardNode.Position = Vector2.Zero;
    }

    private static void ApplyTextureLayer(
        TextureRect layer,
        Rect2 box,
        string path,
        int zIndex,
        TextureRect.StretchModeEnum stretchMode = TextureRect.StretchModeEnum.Scale,
        bool visible = true)
    {
        if (layer.ExpandMode != TextureRect.ExpandModeEnum.IgnoreSize)
            layer.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        if (layer.StretchMode != stretchMode)
            layer.StretchMode = stretchMode;
        ApplyBox(layer, box);
        if (visible)
            SetTextureIfDifferent(layer, ClassicSakuraVisualAssets.Texture(path));
        if (layer.Visible != visible)
            layer.Visible = visible;
        if (layer.ZIndex != zIndex)
            layer.ZIndex = zIndex;
        if (layer.MouseFilter != Control.MouseFilterEnum.Ignore)
            layer.MouseFilter = Control.MouseFilterEnum.Ignore;
    }

    private static void ApplyPanelLayout(Panel panel, Rect2 box, int zIndex, bool visible)
    {
        ApplyBox(panel, box);
        if (panel.Visible != visible)
            panel.Visible = visible;
        if (panel.ZIndex != zIndex)
            panel.ZIndex = zIndex;
        if (panel.MouseFilter != Control.MouseFilterEnum.Ignore)
            panel.MouseFilter = Control.MouseFilterEnum.Ignore;
    }

    private static Panel CreatePanel(string name, Color color, int cornerRadius)
    {
        var style = new StyleBoxFlat
        {
            BgColor = color,
            CornerRadiusTopLeft = cornerRadius,
            CornerRadiusTopRight = cornerRadius,
            CornerRadiusBottomRight = cornerRadius,
            CornerRadiusBottomLeft = cornerRadius
        };

        var panel = new Panel
        {
            Name = name,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        panel.AddThemeStyleboxOverride(PanelStyleName, style);
        return panel;
    }

    private static void ApplyHighlightLayout(NCardHighlight highlight, Rect2 box)
    {
        ApplyTopLeftAnchors(highlight);
        if (highlight.ExpandMode != TextureRect.ExpandModeEnum.IgnoreSize)
            highlight.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        if (highlight.StretchMode != TextureRect.StretchModeEnum.Scale)
            highlight.StretchMode = TextureRect.StretchModeEnum.Scale;
        SetTextureIfDifferent(highlight, HighlightTexture(Spec.HighlightBox.Size));
        ApplyBox(highlight, box);
        if (highlight.ZIndex != Spec.HighlightZIndex)
            highlight.ZIndex = Spec.HighlightZIndex;
        if (highlight.MouseFilter != Control.MouseFilterEnum.Ignore)
            highlight.MouseFilter = Control.MouseFilterEnum.Ignore;
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
            SetTextureIfDifferent(textureRect, ClassicSakuraVisualAssets.Texture(ClassicSakuraVisualAssets.FlashPath()));
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

    private static void ApplyTitleLayout(MegaLabel? title, ClassicSakuraCard model)
    {
        ApplyBox(title, Spec.TitleBox);
        if (title is null)
            return;

        if (!title.Visible)
            title.Visible = true;
        if (title.HorizontalAlignment != HorizontalAlignment.Center)
            title.HorizontalAlignment = HorizontalAlignment.Center;
        if (title.VerticalAlignment != VerticalAlignment.Center)
            title.VerticalAlignment = VerticalAlignment.Center;
        if (title.AutowrapMode != TextServer.AutowrapMode.Off)
            title.AutowrapMode = TextServer.AutowrapMode.Off;
        if (title.MinFontSize != Spec.TitleMinFontSize)
            title.MinFontSize = Spec.TitleMinFontSize;
        if (title.MaxFontSize != Spec.TitleMaxFontSize)
            title.MaxFontSize = Spec.TitleMaxFontSize;
        if (title.ZIndex != Spec.TextZIndex)
            title.ZIndex = Spec.TextZIndex;

        ApplyTextCanvasColor(title);
        ApplyThemeColorOverride(title, FontColorName, NameTextColor(model));
        ApplyThemeColorOverride(title, FontOutlineColorName, Spec.NameTextOutlineColor);
        ApplyThemeColorOverride(title, FontShadowColorName, Spec.NameTextShadowColor);
        ApplyThemeConstantOverride(title, OutlineSizeName, Spec.NameTextOutlineSize);
        ApplyThemeConstantOverride(title, ShadowOffsetXName, Spec.NameTextShadowOffset);
        ApplyThemeConstantOverride(title, ShadowOffsetYName, Spec.NameTextShadowOffset);
        ApplyThemeConstantOverride(title, ShadowOutlineSizeName, Spec.NameTextShadowOutlineSize);
        ApplyThemeFontSizeOverride(title, FontSizeName, Spec.TitleFontSize);
    }

    private static void ApplyEnglishNameLayout(Label label, ClassicSakuraCard model, bool visible)
    {
        ApplyBox(label, EnglishNameBox(model));
        var text = ClassicEnglishName(model.GetType());
        if (label.Text != text)
            label.Text = text;
        if (label.Visible != visible)
            label.Visible = visible;
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
        ApplyThemeConstantOverride(label, OutlineSizeName, Spec.EnglishNameOutlineSize);
    }

    private static void ApplyDescriptionLayout(MegaRichTextLabel? description)
    {
        ApplyBox(description, Spec.DescriptionBox);
        if (description is null)
            return;

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
        if (description.MinFontSize != Spec.DescriptionMinFontSize)
            description.MinFontSize = Spec.DescriptionMinFontSize;
        if (description.MaxFontSize != Spec.DescriptionMaxFontSize)
            description.MaxFontSize = Spec.DescriptionMaxFontSize;
        if (description.ZIndex != Spec.TextZIndex)
            description.ZIndex = Spec.TextZIndex;

        ApplyTextCanvasColor(description);
        ApplyThemeColorOverride(description, FontColorName, Spec.DescriptionTextColor);
        ApplyThemeColorOverride(description, DefaultColorName, Spec.DescriptionTextColor);
        ApplyThemeColorOverride(description, FontOutlineColorName, Spec.DescriptionTextOutlineColor);
        ApplyThemeConstantOverride(description, OutlineSizeName, Spec.DescriptionOutlineSize);
    }

    private static void ApplyCostLayout(
        TextureRect? icon,
        MegaLabel? label,
        TextureRect? unplayableIcon,
        Rect2 box,
        Rect2? labelBox = null,
        string? iconPath = null)
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
            if (iconPath is not null)
                SetTextureIfDifferent(icon, ClassicSakuraVisualAssets.Texture(iconPath));
        }

        if (unplayableIcon is not null && iconPath is not null)
        {
            if (unplayableIcon.ExpandMode != TextureRect.ExpandModeEnum.IgnoreSize)
                unplayableIcon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            if (unplayableIcon.StretchMode != TextureRect.StretchModeEnum.Scale)
                unplayableIcon.StretchMode = TextureRect.StretchModeEnum.Scale;
            SetTextureIfDifferent(unplayableIcon, ClassicSakuraVisualAssets.Texture(iconPath));
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
        if (label.MinFontSize != Spec.CostMinFontSize)
            label.MinFontSize = Spec.CostMinFontSize;
        if (label.MaxFontSize != Spec.CostMaxFontSize)
            label.MaxFontSize = Spec.CostMaxFontSize;
    }

    private static void ApplyTopLeftAnchors(Control? control)
    {
        if (control is null)
            return;

        if (control.AnchorLeft != 0f)
            control.AnchorLeft = 0f;
        if (control.AnchorTop != 0f)
            control.AnchorTop = 0f;
        if (control.AnchorRight != 0f)
            control.AnchorRight = 0f;
        if (control.AnchorBottom != 0f)
            control.AnchorBottom = 0f;
    }

    private static void ApplyTextCanvasColor(CanvasItem item)
    {
        if (item.Modulate != Colors.White)
            item.Modulate = Colors.White;
        if (item.SelfModulate != Colors.White)
            item.SelfModulate = Colors.White;
    }

    private static void ApplyThemeColorOverride(Control control, StringName name, Color color)
    {
        if (!control.HasThemeColorOverride(name) || control.GetThemeColor(name) != color)
            control.AddThemeColorOverride(name, color);
    }

    private static void ApplyThemeConstantOverride(Control control, StringName name, int value)
    {
        if (!control.HasThemeConstantOverride(name) || control.GetThemeConstant(name) != value)
            control.AddThemeConstantOverride(name, value);
    }

    private static void ApplyThemeFontSizeOverride(Control control, StringName name, int value)
    {
        if (!control.HasThemeFontSizeOverride(name) || control.GetThemeFontSize(name) != value)
            control.AddThemeFontSizeOverride(name, value);
    }

    private static void ApplySize(Control? control, Vector2 size)
    {
        ApplySize(control, size, size * 0.5f);
    }

    private static void ApplySize(Control? control, Vector2 size, Vector2 pivotOffset)
    {
        if (control is null)
            return;

        if (control.CustomMinimumSize != size)
            control.CustomMinimumSize = size;
        if (control.Size != size)
            control.Size = size;
        if (control.PivotOffset != pivotOffset)
            control.PivotOffset = pivotOffset;
    }

    private static void ApplyBox(Control? control, Rect2 box)
    {
        if (control is null)
            return;

        if (control.Position != box.Position)
            control.Position = box.Position;
        if (control.CustomMinimumSize != box.Size)
            control.CustomMinimumSize = box.Size;
        if (control.Size != box.Size)
            control.Size = box.Size;
        if (control.Scale != Vector2.One)
            control.Scale = Vector2.One;

        var pivotOffset = box.Size * 0.5f;
        if (control.PivotOffset != pivotOffset)
            control.PivotOffset = pivotOffset;
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

    private static void ApplyParentHolder(NCard card)
    {
        if (card.GetParent() is NCardHolder holder)
            Apply(holder);
    }

    private static IEnumerable<Control?> CardControls(NCard card, ClassicCardNodes nodes)
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

    private static IEnumerable<Control> HiddenVanillaControls(NCard card, ClassicCardState state, ClassicCardNodes nodes)
    {
        foreach (var visual in state.GetOrCreateVanillaBodyVisuals(card).OfType<Control>())
            yield return visual;

        foreach (var hiddenNode in nodes.HiddenNodes.OfType<Control>())
            yield return hiddenNode;
    }

    private static IEnumerable<CanvasItem?> CardVisibilityItems(NCard card, ClassicCardState state, ClassicCardNodes nodes)
    {
        yield return card.CardHighlight;

        foreach (var item in state.GetOrCreateVanillaBodyVisuals(card))
            yield return item;

        foreach (var hiddenNode in nodes.HiddenNodes)
            yield return hiddenNode;
    }

    private static void HideVanillaBodyVisuals(NCard card, ClassicCardState state)
    {
        foreach (var visual in state.GetOrCreateVanillaBodyVisuals(card))
            Hide(visual);
    }

    private static void HideLateRewardGlows(NCard card)
    {
        Hide(FieldValue<CanvasItem>(RareGlowField, card));
        Hide(FieldValue<CanvasItem>(UncommonGlowField, card));
    }

    private static List<CanvasItem> CreateVanillaBodyVisuals(NCard card, ClassicCardState state)
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
            state.Face,
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

    private static void SetTextureIfDifferent(TextureRect textureRect, Texture2D? texture)
    {
        if (!IsGodotInstanceUsable(textureRect) || (texture is not null && !IsGodotInstanceUsable(texture)))
            return;

        if (HasTexture(textureRect, texture))
            return;

        try
        {
            textureRect.Texture = texture;
        }
        catch (ObjectDisposedException)
        {
            return;
        }
    }

    private static bool HasTexture(TextureRect textureRect, Texture2D? texture) =>
        TryGetTexture(textureRect, out var currentTexture)
        && ((currentTexture is null && texture is null)
            || (IsGodotInstanceUsable(currentTexture) && ReferenceEquals(currentTexture, texture)));

    private static bool TryGetTexture(TextureRect textureRect, out Texture2D? texture)
    {
        try
        {
            texture = textureRect.Texture;
            return true;
        }
        catch (ObjectDisposedException)
        {
            texture = null;
            return false;
        }
    }

    private static Texture2D HighlightTexture(Vector2 textureSize)
    {
        var key = new Vector2I(Mathf.CeilToInt(textureSize.X), Mathf.CeilToInt(textureSize.Y));
        if (HighlightTextureCache.TryGetValue(key, out var cachedTexture) && IsGodotInstanceUsable(cachedTexture))
            return cachedTexture;

        var imageScale = Spec.HighlightTextureScale;
        var width = Mathf.CeilToInt(textureSize.X * imageScale);
        var height = Mathf.CeilToInt(textureSize.Y * imageScale);
        var image = Image.CreateEmpty(width, height, false, Image.Format.Rgbaf);
        var cardCenter = textureSize * 0.5f;
        var cardHalfSize = (Spec.RootSize + Spec.HighlightMargin * 0.25f) * 0.5f;

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

        var texture = ImageTexture.CreateFromImage(image);
        HighlightTextureCache[key] = texture;
        return texture;
    }

    private static float RoundedRectDistance(Vector2 point, Vector2 halfSize, float radius)
    {
        var cornerRadius = Mathf.Min(radius, Mathf.Min(halfSize.X, halfSize.Y));
        var q = point.Abs() - (halfSize - Vector2.One * cornerRadius);
        var outside = new Vector2(Mathf.Max(q.X, 0f), Mathf.Max(q.Y, 0f));
        return outside.Length() + Mathf.Min(Mathf.Max(q.X, q.Y), 0f) - cornerRadius;
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

    private static Color NameTextColor(CardModel model) =>
        model.IsUpgraded ? SakuraCardVisualStyle.UpgradedNameTextColor : Spec.DefaultNameTextColor;

    private static Rect2 EnglishNameBox(ClassicSakuraCard model) =>
        model.Family == ClassicSakuraCardFamily.Sakura
            ? Spec.SakuraEnglishNameBox
            : Spec.ClowEnglishNameBox;

    private static string ClassicEnglishName(Type cardType)
    {
        var artStem = ClassicSakuraCardCatalog.ArtStem(cardType).NormalClassicArtStem();
        var name = Path.GetFileNameWithoutExtension(artStem);
        if (name.StartsWith("the_", StringComparison.Ordinal))
            name = name["the_".Length..];

        return $"THE {name.Replace('_', ' ').ToUpperInvariant()}";
    }

    private static bool IsGodotInstanceUsable(GodotObject? instance) =>
        TryIsGodotInstanceUsable(instance);

    private static bool TryIsGodotInstanceUsable(GodotObject? instance)
    {
        try
        {
            return instance is not null
                && GodotObject.IsInstanceValid(instance)
                && (instance is not Node node || !node.IsQueuedForDeletion());
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
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

    private sealed class ClassicCardState
    {
        private readonly Dictionary<Control, ControlSnapshot> _controlSnapshots = [];
        private readonly Dictionary<CanvasItem, bool> _visibilitySnapshots = [];
        private SizeSnapshot? _rootSnapshot;
        private TextureRect? _face;
        private Label? _englishNameLabel;
        private Panel? _descriptionPanel;
        private ClassicCardNodes? _nodes;
        private List<CanvasItem>? _vanillaBodyVisuals;
        private bool _captured;
        private bool _isApplied;

        public TextureRect? Face => _face;

        public Label? EnglishNameLabel => _englishNameLabel;

        public Panel? DescriptionPanel => _descriptionPanel;

        public bool IsApplied => _isApplied;

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

            Hide(_face);
            Hide(_englishNameLabel);
            Hide(_descriptionPanel);

            _rootSnapshot = null;
            _controlSnapshots.Clear();
            _visibilitySnapshots.Clear();
            _vanillaBodyVisuals = null;
            _captured = false;
            _isApplied = false;
        }

        public ClassicCardNodes GetOrCreateNodes(NCard card)
        {
            _nodes ??= ClassicCardNodes.From(card);
            return _nodes;
        }

        public TextureRect GetOrCreateFace(NCard card)
        {
            if (TryGetOwnedBodyChild(card, _face, 0, out var existingFace))
                return existingFace;

            _face = new TextureRect
            {
                Name = "ClassicSakuraFace",
                MouseFilter = Control.MouseFilterEnum.Ignore
            };

            AttachOwnedBodyChild(card, _face, 0);
            return _face;
        }

        public Label GetOrCreateEnglishNameLabel(NCard card)
        {
            if (TryGetOwnedBodyChild(card, _englishNameLabel, null, out var existingLabel))
                return existingLabel;

            _englishNameLabel = new Label
            {
                Name = "ClassicSakuraEnglishName",
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
                "ClassicSakuraDescriptionPanel",
                Spec.DescriptionPanelColor,
                Spec.DescriptionPanelCornerRadius);
            AttachOwnedBodyChild(card, _descriptionPanel, childIndex);
            return _descriptionPanel;
        }

        public IReadOnlyList<CanvasItem> GetOrCreateVanillaBodyVisuals(NCard card)
        {
            _vanillaBodyVisuals ??= CreateVanillaBodyVisuals(card, this);
            return _vanillaBodyVisuals;
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
    }

    private sealed class ClassicCardNodes
    {
        private ClassicCardNodes(
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

        public static ClassicCardNodes From(NCard card) =>
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

    private sealed class ClassicHolderState
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

    private sealed class ClassicGridState
    {
        public bool AllCardsAreClassic { get; set; }
        public bool NeedsDeferredCenter { get; set; }
        public bool DeferredCenterQueued { get; set; }
        public int DeferredCenterAttempts { get; set; }
    }

    private readonly record struct ClassicLayoutContext(
        Rect2 RootBox,
        Vector2 RootPivotOffset,
        SubViewport? TransformVfxViewport)
    {
        public static ClassicLayoutContext For(NCard card)
        {
            var context = new ClassicLayoutContext(
                Spec.DefaultCardCenteredRootBox,
                Spec.DefaultRootPivotOffset,
                null);
            var priority = 0;
            if (card.GetParent() is NCardHolder)
            {
                context = ForCenteredOrigin(null);
                priority = 4;
            }

            SubViewport? viewport = null;
            for (var parent = card.GetParent(); parent is not null; parent = parent.GetParent())
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
                    context = new ClassicLayoutContext(Spec.CenteredRootBox, Vector2.Zero, null);
                    priority = 1;
                }
            }

            return context;
        }

        private static ClassicLayoutContext ForCenteredOrigin(SubViewport? transformVfxViewport) =>
            new(Spec.CenteredRootBox, Spec.DefaultRootPivotOffset, transformVfxViewport);
    }

    private sealed class ClassicSakuraCardLayoutSpec
    {
        public Vector2 RootSize { get; } = new(221f, 491f);
        public Vector2 LayoutSize => RootSize;
        public Vector2 DefaultCardSize => NCard.defaultSize;
        public Vector2 DefaultCardCenteredOffset => (DefaultCardSize - RootSize) * 0.5f;
        public Vector2 HolderVisualOffset => Vector2.Zero;
        public Vector2 DefaultRootPivotOffset => RootSize * 0.5f;
        public Rect2 RootBox => new(Vector2.Zero, RootSize);
        public Rect2 DefaultCardCenteredRootBox => new(DefaultCardCenteredOffset, RootSize);
        public Rect2 CenteredRootBox => new(RootSize * -0.5f + HolderVisualOffset, RootSize);
        public Vector2 HighlightMargin { get; } = new(34f, 38f);
        public Rect2 HighlightBox => new(-HighlightMargin, RootSize + HighlightMargin * 2f);
        public Rect2 CenteredHighlightBox => new(CenteredRootBox.Position - HighlightMargin, HighlightBox.Size);
        public Rect2 TitleBox { get; } = new(new Vector2(28f, 6f), new Vector2(165f, 42f));
        public Rect2 ClowEnglishNameBox { get; } = new(new Vector2(23f, 433f), new Vector2(175f, 34f));
        public Rect2 SakuraEnglishNameBox { get; } = new(new Vector2(23f, 425f), new Vector2(175f, 34f));
        public Rect2 DescriptionPanelBox { get; } = new(new Vector2(16f, 273f), new Vector2(190f, 140f));
        public Rect2 DescriptionBox { get; } = new(new Vector2(16f, 273f), new Vector2(190f, 140f));
        public Rect2 EnergyCostBox { get; } = new(new Vector2(-14f, -12f), new Vector2(56f, 56f));
        public Rect2 EnergyCostLabelBox { get; } = new(new Vector2(12f, -2f), new Vector2(44f, 44f));
        public Rect2 StarCostBox { get; } = new(new Vector2(174f, 14f), new Vector2(44f, 44f));
        public Vector2 DefaultGridCellSize => NCard.defaultSize * NCardHolder.smallScale;
        public Vector2 GridCellSize => new(
            Mathf.Max(DefaultGridCellSize.X, RootSize.X * NCardHolder.smallScale.X),
            Mathf.Max(DefaultGridCellSize.Y, RootSize.Y * NCardHolder.smallScale.Y));
        public float GridCardPadding { get; } = 44f;
        public float GridVerticalOffset { get; } = -34f;
        public float HighlightCornerRadius { get; } = 18f;
        public float HighlightSdfRange { get; } = 240f;
        public float HighlightTextureScale { get; } = 2f;
        public float HandClassicPairGapAdjustment { get; } = -54f;
        public float HandMixedPairGapAdjustment { get; } = 24f;
        public float HandMinimumAdjacentGap { get; } = 100f;
        public Color DefaultNameTextColor { get; } = new(1f, 1f, 1f, 1f);
        public Color NameTextOutlineColor { get; } = new(0.03f, 0.03f, 0.03f, 0.95f);
        public Color NameTextShadowColor { get; } = new(0f, 0f, 0f, 0.1882353f);
        public Color DescriptionTextColor { get; } = new(0.98f, 0.94f, 0.78f, 1f);
        public Color DescriptionTextOutlineColor { get; } = new(0.08f, 0.05f, 0.04f, 0.92f);
        public Color DescriptionPanelColor { get; } = new(0f, 0f, 0f, 0.72f);
        public int TitleFontSize { get; } = 25;
        public int TitleMinFontSize { get; } = 15;
        public int TitleMaxFontSize { get; } = 26;
        public int NameTextOutlineSize { get; } = 4;
        public int NameTextShadowOffset { get; } = 1;
        public int NameTextShadowOutlineSize { get; } = 4;
        public int EnglishNameFontSize { get; } = 22;
        public int EnglishNameOutlineSize { get; } = 3;
        public int DescriptionPanelCornerRadius { get; } = 3;
        public int DescriptionMinFontSize { get; } = 10;
        public int DescriptionMaxFontSize { get; } = 15;
        public int DescriptionOutlineSize { get; } = 2;
        public int CostMinFontSize { get; } = 22;
        public int CostMaxFontSize { get; } = 36;
        public int ArtZIndex { get; } = 0;
        public int DescriptionPanelZIndex { get; } = 0;
        public int HighlightZIndex { get; } = -1;
        public int SelectionHighlightZIndex { get; } = 1;
        public int TextZIndex { get; } = 0;
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
        HorizontalAlignment? HorizontalAlignment,
        VerticalAlignment? VerticalAlignment,
        TextServer.AutowrapMode? AutowrapMode,
        int? MinFontSize,
        int? MaxFontSize,
        ThemeColorSnapshot FontColor,
        ThemeColorSnapshot DefaultColor,
        ThemeColorSnapshot FontOutlineColor,
        ThemeColorSnapshot FontShadowColor,
        ThemeConstantSnapshot OutlineSize,
        ThemeConstantSnapshot ShadowOffsetX,
        ThemeConstantSnapshot ShadowOffsetY,
        ThemeConstantSnapshot ShadowOutlineSize,
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
                LabelHorizontalAlignment(control),
                LabelVerticalAlignment(control),
                LabelAutowrapMode(control),
                MegaLabelMinFontSize(control),
                MegaLabelMaxFontSize(control),
                ThemeColorSnapshot.Capture(control, FontColorName),
                ThemeColorSnapshot.Capture(control, DefaultColorName),
                ThemeColorSnapshot.Capture(control, FontOutlineColorName),
                ThemeColorSnapshot.Capture(control, FontShadowColorName),
                ThemeConstantSnapshot.Capture(control, OutlineSizeName),
                ThemeConstantSnapshot.Capture(control, ShadowOffsetXName),
                ThemeConstantSnapshot.Capture(control, ShadowOffsetYName),
                ThemeConstantSnapshot.Capture(control, ShadowOutlineSizeName),
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
            RestoreLabelAlignment(control, HorizontalAlignment, VerticalAlignment, AutowrapMode);
            RestoreMegaLabelFontBounds(control, MinFontSize, MaxFontSize);
            FontColor.Restore(control);
            DefaultColor.Restore(control);
            FontOutlineColor.Restore(control);
            FontShadowColor.Restore(control);
            OutlineSize.Restore(control);
            ShadowOffsetX.Restore(control);
            ShadowOffsetY.Restore(control);
            ShadowOutlineSize.Restore(control);
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

        private static HorizontalAlignment? LabelHorizontalAlignment(Control control) =>
            control switch
            {
                MegaLabel label => label.HorizontalAlignment,
                Label label => label.HorizontalAlignment,
                _ => null
            };

        private static VerticalAlignment? LabelVerticalAlignment(Control control) =>
            control switch
            {
                MegaLabel label => label.VerticalAlignment,
                Label label => label.VerticalAlignment,
                _ => null
            };

        private static TextServer.AutowrapMode? LabelAutowrapMode(Control control) =>
            control switch
            {
                MegaLabel label => label.AutowrapMode,
                Label label => label.AutowrapMode,
                _ => null
            };

        private static int? MegaLabelMinFontSize(Control control) =>
            control switch
            {
                MegaLabel label => label.MinFontSize,
                MegaRichTextLabel label => label.MinFontSize,
                _ => null
            };

        private static int? MegaLabelMaxFontSize(Control control) =>
            control switch
            {
                MegaLabel label => label.MaxFontSize,
                MegaRichTextLabel label => label.MaxFontSize,
                _ => null
            };

        private static void RestoreLabelAlignment(
            Control control,
            HorizontalAlignment? horizontalAlignment,
            VerticalAlignment? verticalAlignment,
            TextServer.AutowrapMode? autowrapMode)
        {
            if (horizontalAlignment is null || verticalAlignment is null || autowrapMode is null)
                return;

            switch (control)
            {
                case MegaLabel label:
                    label.HorizontalAlignment = horizontalAlignment.Value;
                    label.VerticalAlignment = verticalAlignment.Value;
                    label.AutowrapMode = autowrapMode.Value;
                    break;
                case Label label:
                    label.HorizontalAlignment = horizontalAlignment.Value;
                    label.VerticalAlignment = verticalAlignment.Value;
                    label.AutowrapMode = autowrapMode.Value;
                    break;
            }
        }

        private static void RestoreMegaLabelFontBounds(Control control, int? minFontSize, int? maxFontSize)
        {
            if (minFontSize is null || maxFontSize is null)
                return;

            switch (control)
            {
                case MegaLabel label:
                    label.MinFontSize = minFontSize.Value;
                    label.MaxFontSize = maxFontSize.Value;
                    break;
                case MegaRichTextLabel label:
                    label.MinFontSize = minFontSize.Value;
                    label.MaxFontSize = maxFontSize.Value;
                    break;
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
            if (IsGodotInstanceUsable(Texture))
                return Texture;
            if (!string.IsNullOrEmpty(ResourcePath) && ResourceLoader.Exists(ResourcePath))
                return ResourceLoader.Load<Texture2D>(ResourcePath, null, ResourceLoader.CacheMode.Reuse);

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

    private static void RemoveKeys<TKey, TValue>(Dictionary<TKey, TValue> dictionary, List<TKey>? keys)
        where TKey : notnull
    {
        if (keys is null)
            return;

        foreach (var key in keys)
            dictionary.Remove(key);
    }
}
