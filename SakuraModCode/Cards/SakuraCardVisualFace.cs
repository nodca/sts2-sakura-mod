using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.addons.mega_text;
using STS2RitsuLib.Patching;
using System.Reflection;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
/// One Sakura full-card layout behind the card face seam. The face module owns pooled
/// <see cref="NCard"/> borrowing, restoration, owned nodes, and recovery; an adapter only
/// supplies what differs between layouts.
/// </summary>
internal interface ISakuraCardLayoutAdapter
{
    SakuraCardRendererId Id { get; }

    SakuraCardLayoutGeometry Geometry { get; }

    string ArtNodeName { get; }

    string EnglishNameNodeName { get; }

    TextureRect.StretchModeEnum ArtStretchMode { get; }

    /// <summary>Whether the model can be drawn by this layout; others keep the borrowed state only.</summary>
    bool Accepts(CardModel model);

    /// <summary>The full-card art for the card's current visibility, or null when it is unavailable.</summary>
    Texture2D? ArtTexture(NCard card, CardModel model);

    Texture2D HighlightTexture();

    Rect2 TitleBox(CardModel model);

    void ApplyEnglishName(Label label, NCard card, CardModel model);

    void ApplyDescription(NCard card, CardModel model, MegaRichTextLabel? description, SakuraCardFaceState state);

    void ApplyCost(SakuraCardFaceNodes nodes, CardModel model);

    /// <summary>Creates the plain description panel for cards outside the shared description region, if this layout has one.</summary>
    Panel? CreateDescriptionPanel();

    void ApplyHolderExtras(Control? handFlash, SakuraCardMutationLedger ledger);
}

/// <summary>
/// Per-<see cref="NCard"/> face state shared by every layout. The card ledger already records
/// which renderer applied; this state adds the owned nodes and the adapter that drew them.
/// </summary>
internal sealed class SakuraCardFaceState
{
    private const SakuraControlProperty BoxProperties =
        SakuraControlProperty.Position
        | SakuraControlProperty.Size
        | SakuraControlProperty.CustomMinimumSize
        | SakuraControlProperty.Scale
        | SakuraControlProperty.PivotOffset;
    private const SakuraControlProperty TextureBoxProperties =
        BoxProperties
        | SakuraControlProperty.Anchors
        | SakuraControlProperty.TextureExpandMode
        | SakuraControlProperty.TextureStretchMode;

    private static readonly FieldInfo? RareGlowField = PrivateAccess.DeclaredField(typeof(NCard), "_rareGlow");
    private static readonly FieldInfo? UncommonGlowField = PrivateAccess.DeclaredField(typeof(NCard), "_uncommonGlow");

    private readonly Dictionary<SakuraCardRendererId, OwnedNodes> _ownedNodes = [];
    private SakuraCardMutationLedger? _ledger;
    private SakuraCardFaceNodes? _nodes;
    private SakuraDescriptionRegionNodes? _descriptionRegion;

    public ISakuraCardLayoutAdapter? Adapter { get; private set; }

    public TextureRect? Art => Adapter is null ? null : Owned(Adapter).Art;

    public SakuraDescriptionRegionNodes? DescriptionRegion => _descriptionRegion;

    public bool IsApplied =>
        Adapter is not null && _ledger?.IsApplied(Adapter.Id) == true;

    public void Capture(NCard card, ISakuraCardLayoutAdapter adapter)
    {
        Adapter = adapter;
        var ledger = Ledger(card);
        ledger.Begin(adapter.Id);
        var nodes = GetOrCreateNodes(card);
        var hiddenNodes = nodes.HiddenNodes(card);
        var layout = SakuraCardFaceLayoutContext.For(card, adapter.Geometry);
        SakuraCardGeometryLifecycle.BorrowCardGeometry(
            ledger,
            card,
            hiddenNodes,
            layout.TransformVfxViewport);
        ledger.Borrow(card.Body, SakuraControlProperty.SelfModulate);
        ledger.BorrowTexture(card.CardHighlight);

        ledger.Borrow(
            nodes.TitleLabel,
            BoxProperties
            | SakuraControlProperty.Visibility
            | SakuraControlProperty.Modulate
            | SakuraControlProperty.SelfModulate
            | SakuraControlProperty.ZIndex
            | SakuraControlProperty.HorizontalAlignment
            | SakuraControlProperty.VerticalAlignment
            | SakuraControlProperty.AutowrapMode
            | SakuraControlProperty.FontBounds);
        BorrowTitleTheme(ledger, nodes.TitleLabel);
        ledger.Borrow(
            nodes.DescriptionLabel,
            BoxProperties
            | SakuraControlProperty.Visibility
            | SakuraControlProperty.Modulate
            | SakuraControlProperty.SelfModulate
            | SakuraControlProperty.ZIndex
            | SakuraControlProperty.FontBounds
            | SakuraControlProperty.RichTextLayout);
        BorrowDescriptionTheme(ledger, nodes.DescriptionLabel);
        ledger.Borrow(nodes.EnergyIcon, TextureBoxProperties | SakuraControlProperty.Visibility);
        ledger.Borrow(
            nodes.EnergyLabel,
            BoxProperties
            | SakuraControlProperty.Anchors
            | SakuraControlProperty.Visibility
            | SakuraControlProperty.Modulate
            | SakuraControlProperty.SelfModulate
            | SakuraControlProperty.ZIndex
            | SakuraControlProperty.HorizontalAlignment
            | SakuraControlProperty.VerticalAlignment
            | SakuraControlProperty.FontBounds);
        foreach (var hiddenNode in hiddenNodes)
        {
            if (SakuraCardVisualInfrastructure.IsReloadOwnedVisibility(card, hiddenNode))
                ledger.YieldVisibilityToNative(hiddenNode);
            else
                ledger.BorrowVisibility(hiddenNode);
        }

        ledger.Own(GetOrCreateArt(card));
        ledger.Own(GetOrCreateEnglishNameLabel(card));
        if (card.Model is { } model
            && SakuraDescriptionRegion.AppliesTo(model)
            && nodes.DescriptionLabel is not null)
        {
            ledger.Own(GetOrCreateDescriptionRegion(card).Background);
        }
        else if (TryGetOrCreateDescriptionPanel(card, out var panel))
        {
            ledger.Own(panel);
        }
    }

    public void MarkApplied()
    {
        if (Adapter is { } adapter)
            _ledger?.MarkApplied(adapter.Id);
    }

    public void Restore(NCard card)
    {
        if (Adapter is { } adapter)
            _ledger?.Restore(adapter.Id);
        RestoreVanillaHighlightDefaultsIfSakuraTextureLeaked(card);
    }

    public SakuraCardFaceNodes GetOrCreateNodes(NCard card)
    {
        _nodes ??= SakuraCardFaceNodes.From(card);
        return _nodes;
    }

    public TextureRect GetOrCreateArt(NCard card)
    {
        var owned = Owned(RequireAdapter());
        if (SakuraCardFace.TryGetOwnedBodyChild(card, owned.Art, 0, out var existingArt))
            return existingArt;

        owned.Art = new TextureRect
        {
            Name = RequireAdapter().ArtNodeName,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };

        SakuraCardFace.AttachOwnedBodyChild(card, owned.Art, 0);
        return owned.Art;
    }

    public Label GetOrCreateEnglishNameLabel(NCard card)
    {
        var owned = Owned(RequireAdapter());
        if (SakuraCardFace.TryGetOwnedBodyChild(card, owned.EnglishName, null, out var existingLabel))
            return existingLabel;

        owned.EnglishName = new Label
        {
            Name = RequireAdapter().EnglishNameNodeName,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };

        SakuraCardFace.AttachOwnedBodyChild(card, owned.EnglishName);
        return owned.EnglishName;
    }

    public SakuraDescriptionRegionNodes GetOrCreateDescriptionRegion(NCard card)
    {
        _descriptionRegion = SakuraDescriptionRegion.NodesFor(card);
        return _descriptionRegion;
    }

    public bool TryGetOrCreateDescriptionPanel(NCard card, out Panel panel)
    {
        var owned = Owned(RequireAdapter());
        var childIndex = Mathf.Min(2, Mathf.Max(card.Body.GetChildCount() - 1, 0));
        if (SakuraCardFace.TryGetOwnedBodyChild(card, owned.DescriptionPanel, childIndex, out panel))
            return true;

        if (RequireAdapter().CreateDescriptionPanel() is not { } created)
            return false;

        owned.DescriptionPanel = created;
        SakuraCardFace.AttachOwnedBodyChild(card, created, childIndex);
        panel = created;
        return true;
    }

    public void HideLateRewardGlows(NCard card)
    {
        HideBorrowed(RareGlowField?.GetValue(card) as CanvasItem);
        HideBorrowed(UncommonGlowField?.GetValue(card) as CanvasItem);
    }

    private OwnedNodes Owned(ISakuraCardLayoutAdapter adapter)
    {
        if (!_ownedNodes.TryGetValue(adapter.Id, out var owned))
        {
            owned = new OwnedNodes();
            _ownedNodes[adapter.Id] = owned;
        }

        return owned;
    }

    private ISakuraCardLayoutAdapter RequireAdapter() =>
        Adapter ?? throw new InvalidOperationException("Sakura card face nodes require a captured layout adapter.");

    private SakuraCardMutationLedger Ledger(NCard card)
    {
        _ledger ??= SakuraCardMutationLedgers.For(card);
        return _ledger;
    }

    private static void BorrowTitleTheme(SakuraCardMutationLedger ledger, Control? title)
    {
        ledger.BorrowThemeColor(title, SakuraCardFace.FontColorName);
        ledger.BorrowThemeColor(title, SakuraCardFace.FontOutlineColorName);
        ledger.BorrowThemeColor(title, SakuraCardFace.FontShadowColorName);
        ledger.BorrowThemeConstant(title, SakuraCardFace.OutlineSizeName);
        ledger.BorrowThemeConstant(title, SakuraCardFace.ShadowOffsetXName);
        ledger.BorrowThemeConstant(title, SakuraCardFace.ShadowOffsetYName);
        ledger.BorrowThemeConstant(title, SakuraCardFace.ShadowOutlineSizeName);
        ledger.BorrowThemeFontSize(title, SakuraCardFace.FontSizeName);
    }

    private static void BorrowDescriptionTheme(SakuraCardMutationLedger ledger, Control? description)
    {
        ledger.BorrowThemeColor(description, SakuraCardFace.FontColorName);
        ledger.BorrowThemeColor(description, SakuraCardFace.DefaultColorName);
        ledger.BorrowThemeColor(description, SakuraCardFace.FontOutlineColorName);
        ledger.BorrowThemeConstant(description, SakuraCardFace.OutlineSizeName);
    }

    private void HideBorrowed(CanvasItem? item)
    {
        _ledger?.BorrowVisibility(item);
        SakuraCardFace.Hide(item);
    }

    private static void RestoreVanillaHighlightDefaultsIfSakuraTextureLeaked(NCard card)
    {
        if (!SakuraCardVisualFamilies.IsVanilla(card))
            return;
        if (!SakuraCardVisualInfrastructure.IsGodotInstanceUsable(card.CardHighlight))
            return;

        var highlight = card.CardHighlight!;
        if (!SakuraCardHighlightResources.IsSakuraHighlight(highlight.Texture))
            return;

        SakuraCardFace.ApplyCenteredAnchors(highlight);
        if (highlight.Position != SakuraCardFace.DefaultHighlightPosition)
            highlight.Position = SakuraCardFace.DefaultHighlightPosition;
        if (highlight.Size != SakuraCardFace.DefaultHighlightSize)
            highlight.Size = SakuraCardFace.DefaultHighlightSize;
        if (highlight.CustomMinimumSize != Vector2.Zero)
            highlight.CustomMinimumSize = Vector2.Zero;
        if (highlight.Scale != Vector2.One)
            highlight.Scale = Vector2.One;
        if (highlight.PivotOffset != SakuraCardFace.DefaultHighlightPivotOffset)
            highlight.PivotOffset = SakuraCardFace.DefaultHighlightPivotOffset;
        if (highlight.ExpandMode != TextureRect.ExpandModeEnum.FitHeight)
            highlight.ExpandMode = TextureRect.ExpandModeEnum.FitHeight;
        if (highlight.StretchMode != TextureRect.StretchModeEnum.KeepAspectCentered)
            highlight.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        if (highlight.ZIndex != 0)
            highlight.ZIndex = 0;
        if (highlight.MouseFilter != Control.MouseFilterEnum.Ignore)
            highlight.MouseFilter = Control.MouseFilterEnum.Ignore;
        SakuraCardVisualInfrastructure.SetTextureIfDifferent(highlight, SakuraCardFace.DefaultHighlightTexture());
    }

    private sealed class OwnedNodes
    {
        public TextureRect? Art { get; set; }

        public Label? EnglishName { get; set; }

        public Panel? DescriptionPanel { get; set; }
    }
}

internal sealed class SakuraCardFaceNodes
{
    private static readonly string[] HiddenCardNodeFieldNames =
    [
        "_ancientBanner",
        "_ancientBorder",
        "_ancientBorderGlassOverlay",
        "_ancientPortrait",
        "_ancientTextBg",
        "_banner",
        "_cardVfxContainer",
        "_frame",
        "_lock",
        "_portrait",
        "_portraitBorder",
        "_portraitCanvasGroup",
        "_rareGlow",
        "_sparkles",
        "_starIcon",
        "_typeLabel",
        "_typePlaque",
        "_uncommonGlow",
    ];

    private static readonly List<FieldInfo> HiddenCardNodeFields =
        HiddenCardNodeFieldNames
            .Select(static fieldName => typeof(NCard).GetField(
                fieldName,
                BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            .OfType<FieldInfo>()
            .ToList();

    private static readonly FieldInfo? TitleLabelField = PrivateAccess.DeclaredField(typeof(NCard), "_titleLabel");
    private static readonly FieldInfo? DescriptionLabelField = PrivateAccess.DeclaredField(typeof(NCard), "_descriptionLabel");
    private static readonly FieldInfo? EnergyIconField = PrivateAccess.DeclaredField(typeof(NCard), "_energyIcon");
    private static readonly FieldInfo? EnergyLabelField = PrivateAccess.DeclaredField(typeof(NCard), "_energyLabel");

    private SakuraCardFaceNodes(
        MegaLabel? titleLabel,
        MegaRichTextLabel? descriptionLabel,
        TextureRect? energyIcon,
        MegaLabel? energyLabel)
    {
        TitleLabel = titleLabel;
        DescriptionLabel = descriptionLabel;
        EnergyIcon = energyIcon;
        EnergyLabel = energyLabel;
    }

    public MegaLabel? TitleLabel { get; }

    public MegaRichTextLabel? DescriptionLabel { get; }

    public TextureRect? EnergyIcon { get; }

    public MegaLabel? EnergyLabel { get; }

    public IReadOnlyList<CanvasItem> HiddenNodes(NCard card)
    {
        var nodes = HiddenCardNodeFields
            .Select(field => field.GetValue(card))
            .OfType<CanvasItem>()
            .ToList();

        // Shadow is a scene-only node in card.tscn and has no NCard field.
        if (card.Body.GetNodeOrNull<CanvasItem>("Shadow") is { } shadow)
            nodes.Add(shadow);

        return nodes.Distinct().ToList();
    }

    public static SakuraCardFaceNodes From(NCard card) =>
        new(
            TitleLabelField?.GetValue(card) as MegaLabel,
            DescriptionLabelField?.GetValue(card) as MegaRichTextLabel,
            EnergyIconField?.GetValue(card) as TextureRect,
            EnergyLabelField?.GetValue(card) as MegaLabel);
}

internal readonly record struct SakuraCardFaceLayoutContext(
    Rect2 RootBox,
    Vector2 RootPivotOffset,
    SubViewport? TransformVfxViewport)
{
    public static SakuraCardFaceLayoutContext For(NCard card, SakuraCardLayoutGeometry geometry)
    {
        var rootSize = geometry.RootSize;
        var pivot = rootSize * 0.5f;
        var context = new SakuraCardFaceLayoutContext(
            new Rect2((NCard.defaultSize - rootSize) * 0.5f, rootSize),
            pivot,
            null);
        var priority = 0;
        if (SakuraCardFace.HasAncestor<NHoverTipCardContainer>(card))
            return new SakuraCardFaceLayoutContext(new Rect2(Vector2.Zero, rootSize), pivot, null);

        var parent = card.GetParent();
        if (parent is NCardHolder || IsGeneratedTransparentHandEntryCard(card, parent))
        {
            context = CenteredOrigin(null);
            priority = 4;
        }

        SubViewport? viewport = null;
        for (; parent is not null; parent = parent.GetParent())
        {
            if (parent is SubViewport subViewport)
                viewport = subViewport;

            // Spell Turn previews only ever show Classic cards.
            if (parent is NCardTransformVfx or SpellTurnCardPreviewContainer)
            {
                context = priority < 3
                    ? CenteredOrigin(viewport)
                    : context with { TransformVfxViewport = viewport };
                break;
            }

            if (parent is NMerchantCard && priority < 2)
            {
                context = CenteredOrigin(null);
                priority = 2;
                continue;
            }

            if (parent is NInspectCardScreen && priority < 1)
            {
                context = new SakuraCardFaceLayoutContext(geometry.CenteredRootBox, Vector2.Zero, null);
                priority = 1;
            }
        }

        return context;

        SakuraCardFaceLayoutContext CenteredOrigin(SubViewport? transformVfxViewport) =>
            new(geometry.CenteredRootBox, pivot, transformVfxViewport);
    }

    // Only generated Transparent Cards are tracked here, and they always use the Clear layout.
    private static bool IsGeneratedTransparentHandEntryCard(NCard card, Node? parent) =>
        parent is NCombatUi
        && card.DisplayingPile == PileType.Hand
        && SakuraGeneratedCardLifecycle.IsGeneratedTransparentHandVisualCard(card.Model);
}

/// <summary>Shared drawing for every Sakura card face layout.</summary>
internal static class SakuraCardFace
{
    public static readonly StringName FontColorName = new("font_color");
    public static readonly StringName DefaultColorName = new("default_color");
    public static readonly StringName FontOutlineColorName = new("font_outline_color");
    public static readonly StringName FontSizeName = new("font_size");
    public static readonly StringName OutlineSizeName = new("outline_size");
    public static readonly StringName FontShadowColorName = new("font_shadow_color");
    public static readonly StringName ShadowOffsetXName = new("shadow_offset_x");
    public static readonly StringName ShadowOffsetYName = new("shadow_offset_y");
    public static readonly StringName ShadowOutlineSizeName = new("shadow_outline_size");

    public static readonly Vector2 DefaultHighlightPosition = new(-381f, -475f);
    public static readonly Vector2 DefaultHighlightSize = new(759f, 951f);
    public static readonly Vector2 DefaultHighlightPivotOffset = new(150f, 211f);

    private const int NameTextOutlineSize = 3;
    private const int CostMinFontSize = 22;
    private const int CostMaxFontSize = 36;
    private const string DefaultHighlightTexturePath = "res://images/packed/card_template/card_frame_sdf.exr";

    private static readonly Color DefaultNameTextColor = new(1f, 1f, 1f, 1f);
    private static readonly Color NameTextOutlineColor = new(0.03f, 0.03f, 0.03f, 0.95f);
    private static readonly Color TitleTextShadowColor = new(0f, 0f, 0f, 0.1882353f);
    private static readonly SakuraCardTextureResource DefaultHighlightTextureResource =
        SakuraCardTextureResource.FromPath(DefaultHighlightTexturePath);
    private static readonly FieldInfo? HandFlashField = PrivateAccess.DeclaredField(typeof(NHandCardHolder), "_flash");

    public static void Apply(NCard card, SakuraCardFaceState state)
    {
        var adapter = state.Adapter;
        if (adapter is null || card.Model is not { } model || !adapter.Accepts(model))
            return;
        if (!SakuraCardVisualInfrastructure.IsGodotInstanceUsable(card.Body))
            return;
        if (card.CardHighlight is not { } highlight)
            return;

        var nodes = state.GetOrCreateNodes(card);
        var layout = SakuraCardFaceLayoutContext.For(card, adapter.Geometry);
        SakuraCardGeometryLifecycle.ApplyCardRoot(
            card,
            layout.RootBox,
            layout.RootPivotOffset,
            layout.TransformVfxViewport);
        var transparentBody = new Color(1f, 1f, 1f, 0f);
        if (card.Body.SelfModulate != transparentBody)
            card.Body.SelfModulate = transparentBody;

        ApplyArt(state.GetOrCreateArt(card), adapter, adapter.ArtTexture(card, model));
        SakuraCardGeometryLifecycle.ApplyCardHighlight(
            highlight,
            adapter.Geometry.HighlightBox,
            adapter.Geometry.HighlightZIndex);
        SakuraCardVisualInfrastructure.SetTextureIfDifferent(highlight, adapter.HighlightTexture());

        ApplyTitle(nodes.TitleLabel, adapter.TitleBox(model), model);
        adapter.ApplyEnglishName(state.GetOrCreateEnglishNameLabel(card), card, model);
        adapter.ApplyDescription(card, model, nodes.DescriptionLabel, state);
        adapter.ApplyCost(nodes, model);

        state.HideLateRewardGlows(card);
        foreach (var hiddenNode in nodes.HiddenNodes(card))
            Hide(hiddenNode);
    }

    public static bool TryRestoreOwnedTexturesForRecovery(NCard card, SakuraCardFaceState state)
    {
        if (state.Adapter is not { } adapter
            || card.Model is not { } model
            || !adapter.Accepts(model)
            || !state.IsApplied
            || state.Art is not { } art
            || !SakuraCardVisualInfrastructure.IsGodotInstanceUsable(art)
            || !SakuraCardVisualInfrastructure.IsGodotInstanceUsable(card.CardHighlight))
        {
            return false;
        }

        var artTexture = adapter.ArtTexture(card, model);
        var highlightTexture = adapter.HighlightTexture();
        var descriptionTexture = SakuraDescriptionRegion.AppliesTo(model)
            ? SakuraDescriptionRegion.ShapeTexture(model)
            : null;
        if (!SakuraCardVisualInfrastructure.IsGodotInstanceUsable(artTexture)
            || !SakuraCardVisualInfrastructure.IsGodotInstanceUsable(highlightTexture)
            || descriptionTexture is not null
            && (!SakuraCardVisualInfrastructure.IsGodotInstanceUsable(state.DescriptionRegion?.Background)
                || !SakuraCardVisualInfrastructure.IsGodotInstanceUsable(descriptionTexture)))
            return false;

        SakuraCardVisualInfrastructure.SetTextureIfDifferent(art, artTexture);
        SakuraCardVisualInfrastructure.SetTextureIfDifferent(card.CardHighlight!, highlightTexture);
        if (descriptionTexture is not null)
            SakuraCardVisualInfrastructure.SetTextureIfDifferent(state.DescriptionRegion!.Background, descriptionTexture);
        return art.Visible
            && SakuraCardVisualInfrastructure.HasTexture(art, artTexture)
            && SakuraCardVisualInfrastructure.HasTexture(card.CardHighlight, highlightTexture)
            && (descriptionTexture is null
                || SakuraCardVisualInfrastructure.HasTexture(state.DescriptionRegion!.Background, descriptionTexture));
    }

    public static void ApplyHolder(
        NCardHolder holder,
        SakuraCardMutationLedger ledger,
        ISakuraCardLayoutAdapter adapter)
    {
        var flash = holder is NHandCardHolder handHolder
            ? HandFlashField?.GetValue(handHolder) as Control
            : null;
        ledger.Borrow(flash, SakuraControlProperty.Modulate);
        SakuraHandHighlightVisual.Apply(holder, ledger);
        adapter.ApplyHolderExtras(flash, ledger);
        ApplyHandFlashColor(holder, flash);
    }

    public static void ApplyDescriptionVisibility(NCard card, SakuraCardFaceState state, bool visible)
    {
        if (card.Model is not { } model
            || !SakuraDescriptionRegion.AppliesTo(model)
            || !state.IsApplied
            || state.DescriptionRegion is not { } region)
        {
            return;
        }

        var description = state.GetOrCreateNodes(card).DescriptionLabel;
        var showIdentity = card.Visibility == ModelVisibility.Visible;
        SakuraDescriptionRegion.ApplyVisibility(
            region.Background,
            description,
            visible && showIdentity);
    }

    public static void ApplyTitle(MegaLabel? title, Rect2 box, CardModel model)
    {
        SakuraCardVisualInfrastructure.ApplyBox(title, box);
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
        if (title.MinFontSize != SakuraCardVisualStyle.NativeTitleMinFontSize)
            title.MinFontSize = SakuraCardVisualStyle.NativeTitleMinFontSize;
        if (title.MaxFontSize != SakuraCardVisualStyle.NativeTitleMaxFontSize)
            title.MaxFontSize = SakuraCardVisualStyle.NativeTitleMaxFontSize;
        if (title.ZIndex != 0)
            title.ZIndex = 0;

        ApplyTextCanvasColor(title);
        SakuraCardVisualInfrastructure.ApplyThemeColorOverride(title, FontColorName, NameTextColor(model));
        SakuraCardVisualInfrastructure.ApplyThemeColorOverride(title, FontOutlineColorName, NameTextOutlineColor);
        SakuraCardVisualInfrastructure.ApplyThemeColorOverride(title, FontShadowColorName, TitleTextShadowColor);
        SakuraCardVisualInfrastructure.ApplyThemeConstantOverride(title, OutlineSizeName, SakuraCardVisualStyle.NativeTitleOutlineSize);
        SakuraCardVisualInfrastructure.ApplyThemeConstantOverride(title, ShadowOffsetXName, SakuraCardVisualStyle.NativeTitleShadowOffset);
        SakuraCardVisualInfrastructure.ApplyThemeConstantOverride(title, ShadowOffsetYName, SakuraCardVisualStyle.NativeTitleShadowOffset);
        SakuraCardVisualInfrastructure.ApplyThemeConstantOverride(title, ShadowOutlineSizeName, SakuraCardVisualStyle.NativeTitleShadowOutlineSize);
        SakuraCardVisualInfrastructure.ApplyThemeFontSizeOverride(title, FontSizeName, SakuraCardVisualStyle.NativeTitleFontSize);
    }

    public static void ApplyEnglishName(
        Label label,
        Rect2 box,
        string text,
        bool visible,
        int fontSize,
        CardModel model)
    {
        SakuraCardVisualInfrastructure.ApplyBox(label, box);
        if (label.Text != text)
            label.Text = text;
        if (label.Visible != visible)
            label.Visible = visible;
        if (label.ZIndex != 0)
            label.ZIndex = 0;
        if (label.HorizontalAlignment != HorizontalAlignment.Center)
            label.HorizontalAlignment = HorizontalAlignment.Center;
        if (label.VerticalAlignment != VerticalAlignment.Center)
            label.VerticalAlignment = VerticalAlignment.Center;
        if (label.AutowrapMode != TextServer.AutowrapMode.Off)
            label.AutowrapMode = TextServer.AutowrapMode.Off;

        ApplyTextCanvasColor(label);
        SakuraCardVisualInfrastructure.ApplyThemeColorOverride(label, FontColorName, NameTextColor(model));
        SakuraCardVisualInfrastructure.ApplyThemeColorOverride(label, FontOutlineColorName, NameTextOutlineColor);
        SakuraCardVisualInfrastructure.ApplyThemeFontSizeOverride(label, FontSizeName, fontSize);
        SakuraCardVisualInfrastructure.ApplyThemeConstantOverride(label, OutlineSizeName, NameTextOutlineSize);
    }

    public static void ApplyCost(
        TextureRect? icon,
        MegaLabel? label,
        Rect2 box,
        Rect2 labelBox,
        Texture2D? iconTexture)
    {
        SakuraCardVisualInfrastructure.ApplyTopLeftAnchors(icon);
        SakuraCardVisualInfrastructure.ApplyTopLeftAnchors(label);
        SakuraCardVisualInfrastructure.ApplyBox(icon, box);
        SakuraCardVisualInfrastructure.ApplyBox(label, labelBox);

        if (icon is not null)
        {
            if (icon.ExpandMode != TextureRect.ExpandModeEnum.IgnoreSize)
                icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            if (icon.StretchMode != TextureRect.StretchModeEnum.Scale)
                icon.StretchMode = TextureRect.StretchModeEnum.Scale;
            if (iconTexture is not null)
                SakuraCardVisualInfrastructure.SetTextureIfDifferent(icon, iconTexture);
        }

        if (label is null)
            return;

        if (!label.Visible)
            label.Visible = true;
        ApplyTextCanvasColor(label);
        if (label.ZIndex != 0)
            label.ZIndex = 0;
        if (label.HorizontalAlignment != HorizontalAlignment.Center)
            label.HorizontalAlignment = HorizontalAlignment.Center;
        if (label.VerticalAlignment != VerticalAlignment.Center)
            label.VerticalAlignment = VerticalAlignment.Center;
        if (label.MinFontSize != CostMinFontSize)
            label.MinFontSize = CostMinFontSize;
        if (label.MaxFontSize != CostMaxFontSize)
            label.MaxFontSize = CostMaxFontSize;
    }

    public static void ApplyTextCanvasColor(CanvasItem item)
    {
        if (item.Modulate != Colors.White)
            item.Modulate = Colors.White;
        if (item.SelfModulate != Colors.White)
            item.SelfModulate = Colors.White;
    }

    public static void ApplyCenteredAnchors(Control? control)
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

    public static void Hide(object? node)
    {
        if (node is CanvasItem canvasItem && SakuraCardVisualInfrastructure.IsGodotInstanceUsable(canvasItem))
            canvasItem.Visible = false;
    }

    public static Texture2D? DefaultHighlightTexture() =>
        DefaultHighlightTextureResource.TryResolve(out var texture) ? texture : null;

    public static bool HasAncestor<T>(Node node)
        where T : Node
    {
        for (var current = node.GetParent(); current is not null; current = current.GetParent())
        {
            if (current is T)
                return true;
        }

        return false;
    }

    public static bool TryGetOwnedBodyChild<T>(NCard card, T? node, int? childIndex, out T result)
        where T : Node
    {
        result = null!;
        if (!SakuraCardVisualInfrastructure.IsGodotInstanceUsable(card.Body)
            || !SakuraCardVisualInfrastructure.IsGodotInstanceUsable(node))
            return false;

        result = node!;
        AttachOwnedBodyChild(card, result, childIndex);
        return true;
    }

    public static void AttachOwnedBodyChild(NCard card, Node node, int? childIndex = null)
    {
        if (!SakuraCardVisualInfrastructure.IsGodotInstanceUsable(card.Body)
            || !SakuraCardVisualInfrastructure.IsGodotInstanceUsable(node))
            return;

        var parent = node.GetParent();
        if (!ReferenceEquals(parent, card.Body))
        {
            if (parent is not null && SakuraCardVisualInfrastructure.IsGodotInstanceUsable(parent))
                parent.RemoveChild(node);
            card.Body.AddChild(node);
        }

        if (childIndex is not { } index)
            return;

        var maxIndex = Mathf.Max(card.Body.GetChildCount() - 1, 0);
        card.Body.MoveChild(node, Mathf.Clamp(index, 0, maxIndex));
    }

    private static void ApplyArt(TextureRect art, ISakuraCardLayoutAdapter adapter, Texture2D? texture)
    {
        if (art.ExpandMode != TextureRect.ExpandModeEnum.IgnoreSize)
            art.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        if (art.StretchMode != adapter.ArtStretchMode)
            art.StretchMode = adapter.ArtStretchMode;
        SakuraCardVisualInfrastructure.ApplyBox(art, new Rect2(Vector2.Zero, adapter.Geometry.RootSize));
        SakuraCardVisualInfrastructure.SetTextureIfDifferent(art, texture);
        var visible = art.Texture is not null;
        if (art.Visible != visible)
            art.Visible = visible;
        if (art.ZIndex != 0)
            art.ZIndex = 0;
        if (art.MouseFilter != Control.MouseFilterEnum.Ignore)
            art.MouseFilter = Control.MouseFilterEnum.Ignore;
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

    private static Color NameTextColor(CardModel model) =>
        model.IsUpgraded ? SakuraCardVisualStyle.UpgradedNameTextColor : DefaultNameTextColor;
}
