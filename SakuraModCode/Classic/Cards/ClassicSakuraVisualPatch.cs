using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.addons.mega_text;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Classic.Character;
using SakuraMod.SakuraModCode.Extensions;
using STS2RitsuLib.Patching;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace SakuraMod.SakuraModCode.Classic.Cards;

internal static class ClassicSakuraVisualAssets
{
    private static readonly IReadOnlyDictionary<Type, string> SpellArtStems = new Dictionary<Type, string>
    {
        [typeof(SpellSeal)] = "default_card_p.png",
        [typeof(SpellRelease)] = "default_card_p.png",
        [typeof(SpellTurn)] = "default_card_p.png",
        [typeof(SpellEmptySpell)] = "empty_spell_p.png",
        [typeof(SpellHuoShen)] = "huoshen_p.png",
        [typeof(SpellLeiDi)] = "leidi_p.png",
        [typeof(SpellFengHua)] = "fenghua_p.png",
        [typeof(SpellShuiLong)] = "shuilong_p.png"
    };
    private static readonly Dictionary<string, SakuraCardTextureResource> TextureResources = [];

    public static IEnumerable<string> RunAssetPaths(ClassicSakuraCard card)
    {
        if (!SakuraCardVisualFamilies.UsesClassicLayout(card))
        {
            yield return card.PortraitPath;
            yield break;
        }

        if (IsFullFaceCard(card))
        {
            yield return FullFacePath(card);
            yield return UnknownFullFacePath(card);
            if (EnergyIconPath(card) is { } energyIconPath)
                yield return energyIconPath;
            if (TextEnergyIconPath(card) is { } textEnergyIconPath)
                yield return textEnergyIconPath;
            yield return card.PortraitPath;
            yield return FlashPath();
            foreach (var path in SakuraDescriptionRegion.AssetPaths(card))
                yield return path;
            yield break;
        }

        yield return card.PortraitPath;
    }

    public static Texture2D Texture(string path)
    {
        if (!TextureResources.TryGetValue(path, out var resource))
        {
            resource = SakuraCardTextureResource.FromPath(path);
            TextureResources[path] = resource;
        }

        return resource.ResolveRequired("Classic Sakura visual texture");
    }

    public static string FullFacePath(ClassicSakuraCard card) =>
        FullFacePath(card, ArtStem(card.GetType()).NormalClassicArtStem());

    public static string UnknownFullFacePath(ClassicSakuraCard card) =>
        FullFacePath(card, "unknown.png");

    public static string FullFacePath(ClassicSakuraCard card, string fileName) =>
        Path.Join(FullFaceFamilyDirectory(card), fileName).ClassicFullFaceImagePath();

    public static string ArtStem(Type type)
    {
        if (SpellArtStems.TryGetValue(type, out var spellStem))
            return spellStem;

        var metadata = SakuraCardCatalog.MetadataFor(type);
        if (metadata.VisualRoute != SakuraSourceCardVisualRoute.Classic || metadata.Identity is not { } identity)
            throw new InvalidOperationException($"Missing Classic Sakura art mapping for {type.Name}.");

        return $"the_{identity.ToString().ToLowerInvariant()}_p.png";
    }

    public static string FlashPath() =>
        Path.Join("general", "flash", "flash.png").ClassicCardUiImagePath();

    public static string? EnergyIconPath(ClassicSakuraCard card) =>
        card.IsClowCard
            ? ClassicSakuraEnergyIcon.ClowBigPath
            : card.IsSakuraCard
                ? ClassicSakuraEnergyIcon.SakuraBigPath
                : card is SpellSeal or SpellRelease or SpellTurn
                    ? ClassicSakuraEnergyIcon.ClowBigPath
                    : null;

    public static string? TextEnergyIconPath(ClassicSakuraCard card) =>
        card.IsClowCard
            ? ClassicSakuraEnergyIcon.ClowTextPath
            : card.IsSakuraCard
                ? ClassicSakuraEnergyIcon.SakuraTextPath
                : card is SpellSeal or SpellRelease or SpellTurn
                    ? ClassicSakuraEnergyIcon.ClowTextPath
                    : null;

    internal static string FullFaceFamilyDirectory(ClassicSakuraCard card) =>
        card.IsClowCard
            ? "clow"
            : card.IsSakuraCard
                ? "sakura"
                : card.IsSpellCard
                    ? "spell"
                    : throw new InvalidOperationException($"Classic full-card face assets are not defined for {card.GetType().Name}.");

    private static bool IsFullFaceCard(ClassicSakuraCard card) =>
        card.IsClassicSourceCard || card.IsSpellCard;

}

internal static class ClassicSakuraCardLayout
{
    private static readonly ClassicSakuraCardLayoutSpec Spec = new();

    private static readonly string[] HiddenCardNodeFieldNames =
    [
        "_ancientBanner",
        "_ancientBorder",
        "_ancientBorderGlassOverlay",
        "_ancientPortrait",
        "_ancientTextBg",
        "_banner",
        "_cardOverlay",
        "_cardVfxContainer",
        "_enchantmentTab",
        "_enchantmentVfxOverride",
        "_frame",
        "_lock",
        "_overlayContainer",
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
            .Select(OptionalCardField)
            .OfType<FieldInfo>()
            .ToList();

    private static readonly FieldInfo? TitleLabelField = PrivateAccess.DeclaredField(typeof(NCard), "_titleLabel");
    private static readonly FieldInfo? DescriptionLabelField = PrivateAccess.DeclaredField(typeof(NCard), "_descriptionLabel");
    private static readonly FieldInfo? EnergyIconField = PrivateAccess.DeclaredField(typeof(NCard), "_energyIcon");
    private static readonly FieldInfo? EnergyLabelField = PrivateAccess.DeclaredField(typeof(NCard), "_energyLabel");
    private static readonly FieldInfo? RareGlowField = PrivateAccess.DeclaredField(typeof(NCard), "_rareGlow");
    private static readonly FieldInfo? UncommonGlowField = PrivateAccess.DeclaredField(typeof(NCard), "_uncommonGlow");
    private static readonly FieldInfo? HandFlashField = PrivateAccess.DeclaredField(typeof(NHandCardHolder), "_flash");

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

    private const string DefaultHighlightTexturePath = "res://images/packed/card_template/card_frame_sdf.exr";
    private static readonly Dictionary<Vector2I, SakuraCardTextureResource> HighlightTextureResources = [];
    private static readonly SakuraCardTextureResource DefaultHighlightTextureResource =
        SakuraCardTextureResource.FromPath(DefaultHighlightTexturePath);
    private static readonly ConditionalWeakTable<NCard, ClassicCardState> CardStates = new();

    private static FieldInfo? OptionalCardField(string fieldName) =>
        typeof(NCard).GetField(
            fieldName,
            BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    public static bool IsClassicCard(NCard? card) =>
        SakuraCardVisualFamilies.UsesClassicLayout(card);

    public static bool IsClassicCard(CardModel? card) =>
        SakuraCardVisualFamilies.UsesClassicLayout(card);

    public static void RestoreCardIfTracked(NCard card)
    {
        if (CardStates.TryGetValue(card, out var existingState))
            existingState.Restore(card);
    }

    public static void Apply(NCard card)
    {
        if (!IsClassicCard(card))
        {
            if (CardStates.TryGetValue(card, out var existingState))
                existingState.Restore(card);

            return;
        }

        var state = CardStates.GetOrCreateValue(card);
        state.Capture(card);
        ApplyCardLayout(card, state);
        state.MarkApplied();
    }

    public static bool TryRestoreOwnedTexturesForRecovery(NCard card)
    {
        if (card.Model is not ClassicSakuraCard model
            || !IsClassicCard(card)
            || !CardStates.TryGetValue(card, out var state)
            || !state.IsApplied
            || !IsGodotInstanceUsable(state.Face)
            || !IsGodotInstanceUsable(card.CardHighlight))
        {
            return false;
        }

        var facePath = card.Visibility == ModelVisibility.Visible
            ? ClassicSakuraVisualAssets.FullFacePath(model)
            : ClassicSakuraVisualAssets.UnknownFullFacePath(model);
        var faceTexture = ClassicSakuraVisualAssets.Texture(facePath);
        var highlightTexture = HighlightTexture(Spec.HighlightBox.Size);
        var descriptionTexture = SakuraDescriptionRegion.AppliesTo(model)
            ? SakuraDescriptionRegion.ShapeTexture(model)
            : null;
        if (!IsGodotInstanceUsable(faceTexture)
            || !IsGodotInstanceUsable(highlightTexture)
            || descriptionTexture is not null
            && (!IsGodotInstanceUsable(state.DescriptionRegion?.Background)
                || !IsGodotInstanceUsable(descriptionTexture)))
            return false;

        SetTextureIfDifferent(state.Face!, faceTexture);
        SetTextureIfDifferent(card.CardHighlight!, highlightTexture);
        if (descriptionTexture is not null)
            SetTextureIfDifferent(state.DescriptionRegion!.Background, descriptionTexture);
        return state.Face!.Visible
            && SakuraCardVisualInfrastructure.HasTexture(state.Face, faceTexture)
            && SakuraCardVisualInfrastructure.HasTexture(card.CardHighlight, highlightTexture)
            && (descriptionTexture is null
                || SakuraCardVisualInfrastructure.HasTexture(state.DescriptionRegion!.Background, descriptionTexture));
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
        SakuraCardGeometryLifecycle.ApplyCardRoot(
            card,
            layout.RootBox,
            layout.RootPivotOffset,
            layout.TransformVfxViewport);
        var transparentBody = new Color(1f, 1f, 1f, 0f);
        if (card.Body.SelfModulate != transparentBody)
            card.Body.SelfModulate = transparentBody;

        var showFaceIdentity = card.Visibility == ModelVisibility.Visible;
        var facePath = showFaceIdentity
            ? ClassicSakuraVisualAssets.FullFacePath(model)
            : ClassicSakuraVisualAssets.UnknownFullFacePath(model);
        ApplyTextureLayer(state.GetOrCreateFace(card), Spec.RootBox, facePath, Spec.ArtZIndex);

        SakuraCardGeometryLifecycle.ApplyCardHighlight(highlight, Spec.HighlightBox, Spec.HighlightZIndex);
        SetTextureIfDifferent(highlight, HighlightTexture(Spec.HighlightBox.Size));
        ApplyTitleLayout(nodes.TitleLabel, model);
        ApplyEnglishNameLayout(
            state.GetOrCreateEnglishNameLabel(card),
            model,
            showFaceIdentity && !model.IsSpellCard);
        ApplyDescriptionRegion(card, model, nodes.DescriptionLabel, state, showFaceIdentity);
        if (model.ShowsEnergyCost)
        {
            ApplyCostLayout(
                nodes.EnergyIcon,
                nodes.EnergyLabel,
                Spec.EnergyCostBox,
                Spec.EnergyCostLabelBox,
                ClassicSakuraVisualAssets.EnergyIconPath(model));
        }
        else
        {
            Hide(nodes.EnergyIcon);
            Hide(nodes.EnergyLabel);
        }

        state.HideLateRewardGlows(card);
        foreach (var hiddenNode in nodes.HiddenNodes(card))
            Hide(hiddenNode);
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

    private static void ApplyDescriptionRegion(
        NCard card,
        ClassicSakuraCard model,
        MegaRichTextLabel? description,
        ClassicCardState state,
        bool showFaceIdentity)
    {
        if (description is null)
            return;

        if (!SakuraDescriptionRegion.AppliesTo(model))
        {
            ApplyPanelLayout(
                state.GetOrCreateDescriptionPanel(card),
                Spec.DescriptionPanelBox,
                Spec.DescriptionPanelZIndex,
                showFaceIdentity);
            ApplyDescriptionLayout(description, Spec.DescriptionBox, null, null, visible: true);
            return;
        }

        var region = state.GetOrCreateDescriptionRegion(card);
        SakuraDescriptionRegion.ApplyBackground(
            region.Background,
            model,
            SakuraCardVisualLayout.Classic,
            showFaceIdentity);
        ApplyDescriptionLayout(
            description,
            SakuraDescriptionRegion.TextBox(SakuraCardVisualLayout.Classic),
            model,
            description.Text,
            visible: true);
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

    private static Panel CreatePanel(string name, Color color, int cornerRadius) =>
        SakuraCardVisualInfrastructure.CreatePanel(name, color, cornerRadius);

    public static void ApplyHolderVisuals(
        NCardHolder holder,
        SakuraCardMutationLedger ledger)
    {
        if (!IsClassicCard(holder.CardNode))
            return;

        var flash = HandFlash(holder);
        ledger.Borrow(flash, SakuraControlProperty.Modulate);
        SakuraHandHighlightVisual.Apply(holder, ledger);
        ledger.BorrowTexture(flash as TextureRect);
        if (flash is TextureRect textureRect)
            SetTextureIfDifferent(textureRect, ClassicSakuraVisualAssets.Texture(ClassicSakuraVisualAssets.FlashPath()));
        ApplyHandFlashColor(holder, flash);
    }

    public static void ApplyDescriptionVisibility(NCard? card, bool visible)
    {
        if (card?.Model is not ClassicSakuraCard model
            || !SakuraDescriptionRegion.AppliesTo(model)
            || !CardStates.TryGetValue(card, out var state)
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

    private static void ApplyDescriptionLayout(
        MegaRichTextLabel? description,
        Rect2 box,
        CardModel? model,
        string? text,
        bool visible)
    {
        if (description is null)
            return;

        if (text is null)
        {
            ApplyBox(description, box);
            if (description.Visible != visible)
                description.Visible = visible;
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
        }
        else
        {
            if (model is null)
                throw new InvalidOperationException("A shared Sakura description region requires a card model.");

            SakuraDescriptionRegion.ApplyText(description, box, model, text, visible);
        }

        ApplyTextCanvasColor(description);
        ApplyThemeColorOverride(description, FontColorName, Spec.DescriptionTextColor);
        ApplyThemeColorOverride(description, DefaultColorName, Spec.DescriptionTextColor);
        ApplyThemeColorOverride(description, FontOutlineColorName, Spec.DescriptionTextOutlineColor);
        ApplyThemeConstantOverride(description, OutlineSizeName, Spec.DescriptionOutlineSize);
    }

    private static void ApplyCostLayout(
        TextureRect? icon,
        MegaLabel? label,
        Rect2 box,
        Rect2? labelBox = null,
        string? iconPath = null)
    {
        ApplyTopLeftAnchors(icon);
        ApplyTopLeftAnchors(label);
        ApplyBox(icon, box);
        ApplyBox(label, labelBox ?? box);

        if (icon is not null)
        {
            if (icon.ExpandMode != TextureRect.ExpandModeEnum.IgnoreSize)
                icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            if (icon.StretchMode != TextureRect.StretchModeEnum.Scale)
                icon.StretchMode = TextureRect.StretchModeEnum.Scale;
            if (iconPath is not null)
                SetTextureIfDifferent(icon, ClassicSakuraVisualAssets.Texture(iconPath));
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

    private static void ApplyTextCanvasColor(CanvasItem item)
    {
        if (item.Modulate != Colors.White)
            item.Modulate = Colors.White;
        if (item.SelfModulate != Colors.White)
            item.SelfModulate = Colors.White;
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

    private static void ApplyBox(Control? control, Rect2 box)
    {
        SakuraCardVisualInfrastructure.ApplyBox(control, box);
    }

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
        SakuraCardVisualInfrastructure.SetTextureIfDifferent(textureRect, texture);
    }

    private static Texture2D HighlightTexture(Vector2 textureSize)
    {
        var key = new Vector2I(Mathf.CeilToInt(textureSize.X), Mathf.CeilToInt(textureSize.Y));
        if (!HighlightTextureResources.TryGetValue(key, out var resource))
        {
            resource = SakuraCardTextureResource.FromFactory(() => CreateHighlightTexture(textureSize));
            HighlightTextureResources[key] = resource;
        }

        return resource.ResolveRequired("Classic Sakura highlight");
    }

    private static Texture2D CreateHighlightTexture(Vector2 textureSize)
    {
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

        return ImageTexture.CreateFromImage(image);
    }

    private static Texture2D? DefaultHighlightTexture() =>
        DefaultHighlightTextureResource.TryResolve(out var texture) ? texture : null;

    private static float RoundedRectDistance(Vector2 point, Vector2 halfSize, float radius)
    {
        return SakuraCardVisualInfrastructure.RoundedRectDistance(point, halfSize, radius);
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
        model.IsSakuraCard
            ? Spec.SakuraEnglishNameBox
            : Spec.ClowEnglishNameBox;

    private static string ClassicEnglishName(Type cardType)
    {
        var artStem = ClassicSakuraVisualAssets.ArtStem(cardType).NormalClassicArtStem();
        var name = Path.GetFileNameWithoutExtension(artStem);
        if (name.StartsWith("the_", StringComparison.Ordinal))
            name = name["the_".Length..];

        return $"THE {name.Replace('_', ' ').ToUpperInvariant()}";
    }

    private static bool IsGodotInstanceUsable(GodotObject? instance) =>
        SakuraCardVisualInfrastructure.IsGodotInstanceUsable(instance);

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

        private SakuraCardMutationLedger? _ledger;
        private TextureRect? _face;
        private Label? _englishNameLabel;
        private Panel? _descriptionPanel;
        private SakuraDescriptionRegionNodes? _descriptionRegion;
        private ClassicCardNodes? _nodes;

        public TextureRect? Face => _face;

        public Label? EnglishNameLabel => _englishNameLabel;

        public Panel? DescriptionPanel => _descriptionPanel;

        public SakuraDescriptionRegionNodes? DescriptionRegion => _descriptionRegion;

        public bool IsApplied =>
            _ledger?.IsApplied(SakuraCardRendererId.Classic) == true;

        public void Capture(NCard card)
        {
            var ledger = Ledger(card);
            ledger.Begin(SakuraCardRendererId.Classic);
            var nodes = GetOrCreateNodes(card);
            var hiddenNodes = nodes.HiddenNodes(card);
            var layout = ClassicLayoutContext.For(card);
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
            ledger.Own(GetOrCreateFace(card));
            ledger.Own(GetOrCreateEnglishNameLabel(card));
            if (card.Model is { } model
                && SakuraDescriptionRegion.AppliesTo(model)
                && nodes.DescriptionLabel is not null)
            {
                var region = GetOrCreateDescriptionRegion(card);
                ledger.Own(region.Background);
            }
            else
            {
                ledger.Own(GetOrCreateDescriptionPanel(card));
            }
        }

        public void MarkApplied()
        {
            _ledger?.MarkApplied(SakuraCardRendererId.Classic);
        }

        public void Restore(NCard card)
        {
            _ledger?.Restore(SakuraCardRendererId.Classic);
            RestoreVanillaHighlightDefaultsIfSakuraTextureLeaked(card);
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

        public SakuraDescriptionRegionNodes GetOrCreateDescriptionRegion(NCard card)
        {
            _descriptionRegion = SakuraDescriptionRegion.NodesFor(card);
            return _descriptionRegion;
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

        public void HideLateRewardGlows(NCard card)
        {
            HideBorrowed(FieldValue<CanvasItem>(RareGlowField, card));
            HideBorrowed(FieldValue<CanvasItem>(UncommonGlowField, card));
        }

        private SakuraCardMutationLedger Ledger(NCard card)
        {
            _ledger ??= SakuraCardMutationLedgers.For(card);
            return _ledger;
        }

        private static void BorrowTitleTheme(SakuraCardMutationLedger ledger, Control? title)
        {
            ledger.BorrowThemeColor(title, FontColorName);
            ledger.BorrowThemeColor(title, FontOutlineColorName);
            ledger.BorrowThemeColor(title, FontShadowColorName);
            ledger.BorrowThemeConstant(title, OutlineSizeName);
            ledger.BorrowThemeConstant(title, ShadowOffsetXName);
            ledger.BorrowThemeConstant(title, ShadowOffsetYName);
            ledger.BorrowThemeConstant(title, ShadowOutlineSizeName);
            ledger.BorrowThemeFontSize(title, FontSizeName);
        }

        private static void BorrowDescriptionTheme(SakuraCardMutationLedger ledger, Control? description)
        {
            ledger.BorrowThemeColor(description, FontColorName);
            ledger.BorrowThemeColor(description, DefaultColorName);
            ledger.BorrowThemeColor(description, FontOutlineColorName);
            ledger.BorrowThemeConstant(description, OutlineSizeName);
        }

        private void HideBorrowed(CanvasItem? item)
        {
            _ledger?.BorrowVisibility(item);
            Hide(item);
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
    }

    private sealed class ClassicCardNodes
    {
        private ClassicCardNodes(
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

        public static ClassicCardNodes From(NCard card) =>
            new(
                FieldValue<MegaLabel>(TitleLabelField, card),
                FieldValue<MegaRichTextLabel>(DescriptionLabelField, card),
                FieldValue<TextureRect>(EnergyIconField, card),
                FieldValue<MegaLabel>(EnergyLabelField, card));
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
            if (HasAncestor<NHoverTipCardContainer>(card))
                return new ClassicLayoutContext(Spec.RootBox, Spec.DefaultRootPivotOffset, null);

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
        public Vector2 RootSize => SakuraCardGeometry.ClassicLayout.RootSize;
        public Vector2 DefaultCardSize => NCard.defaultSize;
        public Vector2 DefaultCardCenteredOffset => (DefaultCardSize - RootSize) * 0.5f;
        public Vector2 DefaultRootPivotOffset => RootSize * 0.5f;
        public Rect2 RootBox => new(Vector2.Zero, RootSize);
        public Rect2 DefaultCardCenteredRootBox => new(DefaultCardCenteredOffset, RootSize);
        public Rect2 CenteredRootBox => SakuraCardGeometry.ClassicLayout.CenteredRootBox;
        public Vector2 HighlightMargin => SakuraCardGeometry.ClassicLayout.HighlightMargin;
        public Rect2 HighlightBox => new(-HighlightMargin, RootSize + HighlightMargin * 2f);
        public Rect2 TitleBox { get; } = new(new Vector2(28f, 6f), new Vector2(165f, 42f));
        public Rect2 ClowEnglishNameBox { get; } = new(new Vector2(23f, 433f), new Vector2(175f, 34f));
        public Rect2 SakuraEnglishNameBox { get; } = new(new Vector2(23f, 425f), new Vector2(175f, 34f));
        public Rect2 DescriptionPanelBox { get; } = new(new Vector2(16f, 273f), new Vector2(190f, 140f));
        public Rect2 DescriptionBox { get; } = new(new Vector2(16f, 273f), new Vector2(190f, 140f));
        public Rect2 EnergyCostBox { get; } = new(new Vector2(-14f, -12f), new Vector2(56f, 56f));
        public Rect2 EnergyCostLabelBox { get; } = new(new Vector2(12f, -2f), new Vector2(44f, 44f));
        public float HighlightCornerRadius { get; } = 18f;
        public float HighlightSdfRange { get; } = 240f;
        public float HighlightTextureScale { get; } = 2f;
        public Vector2 DefaultHighlightPosition { get; } = new(-381f, -475f);
        public Vector2 DefaultHighlightSize { get; } = new(759f, 951f);
        public Vector2 DefaultHighlightPivotOffset { get; } = new(150f, 211f);
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
        public int DescriptionMinFontSize { get; } = 12;
        public int DescriptionMaxFontSize { get; } = 18;
        public int DescriptionOutlineSize { get; } = 2;
        public int CostMinFontSize { get; } = 22;
        public int CostMaxFontSize { get; } = 36;
        public int ArtZIndex { get; } = 0;
        public int DescriptionPanelZIndex { get; } = 0;
        public int HighlightZIndex => SakuraCardGeometry.ClassicLayout.HighlightZIndex;
        public int TextZIndex { get; } = 0;
    }

}
