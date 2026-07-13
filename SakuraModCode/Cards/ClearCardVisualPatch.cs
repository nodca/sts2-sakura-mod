using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.addons.mega_text;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Extensions;
using STS2RitsuLib.Patching;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace SakuraMod.SakuraModCode.Cards;

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
    private static readonly StringName FontOutlineColorName = new("font_outline_color");
    private static readonly StringName FontSizeName = new("font_size");
    private static readonly StringName OutlineSizeName = new("outline_size");
    private static readonly StringName FontShadowColorName = new("font_shadow_color");
    private static readonly StringName ShadowOffsetXName = new("shadow_offset_x");
    private static readonly StringName ShadowOffsetYName = new("shadow_offset_y");
    private static readonly StringName ShadowOutlineSizeName = new("shadow_outline_size");
    private const string HeaderPartSeparator = "  ";
    private const float HeaderPartSeparatorUnits = 1f;

    private const string DefaultHighlightTexturePath = "res://images/packed/card_template/card_frame_sdf.exr";
    private static readonly Dictionary<Type, SakuraCardTextureResource> ClearCardArtResources = [];
    private static readonly Dictionary<Type, string> ClearCardEnglishNameCache = [];
    private static readonly Dictionary<(string Language, SakuraElement Element), string> ElementTitleCache = [];
    private static readonly Dictionary<string, string> ClearCardStatusTextCache = [];
    private static readonly SakuraCardTextureResource ClearCardHighlightTextureResource =
        SakuraCardTextureResource.FromFactory(CreateClearCardHighlightTexture);
    private static readonly SakuraCardTextureResource DefaultHighlightTextureResource =
        SakuraCardTextureResource.FromPath(DefaultHighlightTexturePath);
    private static readonly ConditionalWeakTable<NCard, ClearCardState> CardStates = new();

    private static FieldInfo? OptionalCardField(string fieldName) =>
        typeof(NCard).GetField(
            fieldName,
            BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    public static bool IsClearCard(NCard? card) =>
        SakuraCardVisualFamilies.UsesClearLayout(card);

    public static void PreloadVisualResources()
    {
        foreach (var cardType in SakuraTransparentCardCatalog.TransparentCardTypes)
            _ = ClearCardTexture(cardType);
        _ = ClearCardHighlightTexture();
    }

    public static string CardArtPath(Type cardType) =>
        ClearCardArtFileName(cardType).ClearCardAssetPath();

    public static void RestoreCardIfTracked(NCard card)
    {
        if (CardStates.TryGetValue(card, out var existingState))
            existingState.Restore(card);
    }

    public static void Apply(NCard card)
    {
        if (!IsClearCard(card))
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
        if (!IsClearCard(card)
            || card.Model is null
            || !CardStates.TryGetValue(card, out var state)
            || !state.IsApplied
            || !IsGodotInstanceUsable(state.Art)
            || !IsGodotInstanceUsable(card.CardHighlight))
        {
            return false;
        }

        var artTexture = ClearCardTexture(card.Model.GetType());
        var highlightTexture = ClearCardHighlightTexture();
        var descriptionTexture = SakuraDescriptionRegion.ShapeTexture(card.Model);
        if (!IsGodotInstanceUsable(artTexture)
            || !IsGodotInstanceUsable(highlightTexture)
            || !IsGodotInstanceUsable(state.DescriptionRegion?.Background)
            || !IsGodotInstanceUsable(descriptionTexture))
            return false;

        SetTextureIfDifferent(state.Art!, artTexture);
        SetTextureIfDifferent(card.CardHighlight!, highlightTexture);
        SetTextureIfDifferent(state.DescriptionRegion!.Background, descriptionTexture);
        return state.Art!.Visible
            && HasTexture(state.Art, artTexture)
            && HasTexture(card.CardHighlight!, highlightTexture)
            && HasTexture(state.DescriptionRegion.Background, descriptionTexture);
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
        SakuraCardGeometryLifecycle.ApplyCardRoot(
            card,
            layout.RootBox,
            layout.RootPivotOffset,
            layout.TransformVfxViewport);
        card.Body.SelfModulate = new Color(1f, 1f, 1f, 0f);

        ApplyArtLayout(state.GetOrCreateArt(card), card);
        SakuraCardGeometryLifecycle.ApplyCardHighlight(highlight, Spec.HighlightBox, Spec.HighlightZIndex);
        SetTextureIfDifferent(highlight, ClearCardHighlightTexture());

        ApplyTitleLayout(card, nodes.TitleLabel, model);
        ApplyEnglishNameLayout(state.GetOrCreateEnglishNameLabel(card), model);
        ApplyDescriptionRegion(card, nodes.DescriptionLabel, model, state);
        ApplyCostLayout(
            nodes.EnergyIcon,
            nodes.EnergyLabel,
            Spec.EnergyCostBox,
            Spec.EnergyCostLabelBox,
            model.EnergyIcon);

        state.HideLateRewardGlows(card);
        foreach (var hiddenNode in nodes.HiddenNodes(card))
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

    private static void ApplyDescriptionRegion(
        NCard card,
        MegaRichTextLabel? description,
        CardModel model,
        ClearCardState state)
    {
        if (description is null)
            return;

        var showIdentity = card.Visibility == ModelVisibility.Visible;
        var region = state.GetOrCreateDescriptionRegion(card);
        var text = showIdentity
            ? state.ClearCardDescriptionText(model, description.Text)
            : description.Text;
        SakuraDescriptionRegion.ApplyBackground(
            region.Background,
            model,
            SakuraCardVisualLayout.Clear,
            showIdentity);
        SakuraDescriptionRegion.ApplyText(
            description,
            SakuraDescriptionRegion.TextBox(SakuraCardVisualLayout.Clear),
            model,
            text,
            visible: true);
    }

    public static void ApplyHolderVisuals(
        NCardHolder holder,
        SakuraCardMutationLedger ledger)
    {
        if (!IsClearCard(holder.CardNode))
            return;

        var flash = HandFlash(holder);
        ledger.Borrow(flash, SakuraControlProperty.Modulate);

        SakuraHandHighlightVisual.Apply(holder, ledger);
        ApplyHandFlashColor(holder, flash);
    }

    public static void ApplyDescriptionVisibility(NCard? card, bool visible)
    {
        if (card?.Model is not { } model
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

    private static string ClearCardDescriptionText(
        CardModel model,
        string currentText,
        string? synchronizedLine = null)
    {
        var body = ClearCardDescriptionBody(model, currentText);
        synchronizedLine ??= SakuraStateText.SynchronizedLine(model);
        if (!string.IsNullOrEmpty(synchronizedLine))
            body = AppendDescriptionBodyTextLine(body, synchronizedLine.TrimStart('\r', '\n'));

        var header = ClearCardHeaderText(model);
        if (header.Length == 0)
            return body;
        if (body.Length == 0)
            return header;

        return $"{header}\n{body}";
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

        if (IsExtraEffectDescriptionLine(text, start, end)
            && !SakuraModCard.ShouldShowMagicChargeExtraEffectDescription(model))
            return;

        if (builder.Length > 0)
            builder.Append('\n');

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
        if (model.IsTemporary())
            yield return ClearCardStatusText();
    }

    private static string ClearCardStatusText()
    {
        var language = CurrentLanguageKey();
        if (ClearCardStatusTextCache.TryGetValue(language, out var cachedText))
            return cachedText;

        var text = $"[color=#a6e0ff]{SakuraStateText.TemporaryLabel()}[/color]";
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

    private static bool IsExtraEffectDescriptionLine(string text, int start, int end)
    {
        var visibleText = RemoveRichTextTags(text, start, end).TrimStart();
        return visibleText.StartsWith("额外效果：", StringComparison.Ordinal)
               || visibleText.StartsWith("Extra:", StringComparison.Ordinal);
    }

    internal static bool IsExtraEffectDescriptionLineForTests(string text) =>
        IsExtraEffectDescriptionLine(text, 0, text.Length);

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
        Rect2 box,
        Rect2? labelBox = null,
        Texture2D? iconTexture = null)
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
            if (iconTexture is not null)
                SetTextureIfDifferent(icon, iconTexture);
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
        if (!ClearCardArtResources.TryGetValue(cardType, out var resource))
        {
            resource = SakuraCardTextureResource.FromPath(CardArtPath(cardType));
            ClearCardArtResources[cardType] = resource;
        }

        return resource.TryResolve(out var texture) ? texture : null;
    }

    private static Texture2D ClearCardHighlightTexture() =>
        ClearCardHighlightTextureResource.ResolveRequired("Clear Card highlight");

    private static Texture2D CreateClearCardHighlightTexture()
    {
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

        return ImageTexture.CreateFromImage(image);
    }

    private static Texture2D? DefaultHighlightTexture()
    {
        return DefaultHighlightTextureResource.TryResolve(out var texture) ? texture : null;
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

    private static void ApplyBox(Control? control, Rect2 box)
    {
        SakuraCardVisualInfrastructure.ApplyBox(control, box);
    }

    private static string CurrentLanguageKey() =>
        LocManager.Instance?.Language ?? string.Empty;

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

    private sealed class ClearCardState
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
        private TextureRect? _art;
        private Label? _englishNameLabel;
        private SakuraDescriptionRegionNodes? _descriptionRegion;
        private ClearCardNodes? _nodes;
        private ClearCardDescriptionCache? _descriptionCache;

        public TextureRect? Art => _art;

        public Label? EnglishNameLabel => _englishNameLabel;

        public SakuraDescriptionRegionNodes? DescriptionRegion => _descriptionRegion;

        public bool IsApplied =>
            _ledger?.IsApplied(SakuraCardRendererId.Clear) == true;

        public string ClearCardDescriptionText(CardModel model, string currentText)
        {
            _descriptionCache ??= new ClearCardDescriptionCache();
            return _descriptionCache.Text(model, currentText);
        }

        public void Capture(NCard card)
        {
            var ledger = Ledger(card);
            ledger.Begin(SakuraCardRendererId.Clear);
            var nodes = GetOrCreateNodes(card);
            var hiddenNodes = nodes.HiddenNodes(card);
            var layout = ClearCardLayoutContext.For(card);
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
                | SakuraControlProperty.FontBounds
                | SakuraControlProperty.RichTextLayout);
            ledger.Borrow(nodes.EnergyIcon, TextureBoxProperties);
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
            if (nodes.DescriptionLabel is not null)
            {
                var region = GetOrCreateDescriptionRegion(card);
                ledger.Own(region.Background);
            }
        }

        public void MarkApplied()
        {
            _ledger?.MarkApplied(SakuraCardRendererId.Clear);
        }

        public void Restore(NCard card)
        {
            _ledger?.Restore(SakuraCardRendererId.Clear);
            RestoreVanillaHighlightDefaultsIfSakuraTextureLeaked(card);
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

        public SakuraDescriptionRegionNodes GetOrCreateDescriptionRegion(NCard card)
        {
            _descriptionRegion = SakuraDescriptionRegion.NodesFor(card);
            return _descriptionRegion;
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

    private sealed class ClearCardDescriptionCache
    {
        private Type? _cardType;
        private string? _sourceText;
        private string? _language;
        private bool _temporary;
        private bool _showExtraEffectDescription;
        private SakuraElementSet _elements;
        private string? _synchronizedLine;
        private string? _renderedText;
        private string? _text;

        public string Text(CardModel model, string currentText)
        {
            var cardType = model.GetType();
            var sourceText = _renderedText == currentText && _sourceText is not null
                ? _sourceText
                : currentText;
            var language = CurrentLanguageKey();
            var temporary = model.IsTemporary();
            var showExtraEffectDescription = SakuraModCard.ShouldShowMagicChargeExtraEffectDescription(model);
            var elements = SakuraActions.ElementSetOf(model);
            var synchronizedLine = SakuraStateText.SynchronizedLine(model);

            if (_cardType == cardType
                && _sourceText == sourceText
                && _language == language
                && _temporary == temporary
                && _showExtraEffectDescription == showExtraEffectDescription
                && _elements == elements
                && _synchronizedLine == synchronizedLine)
                return _text!;

            _cardType = cardType;
            _sourceText = sourceText;
            _language = language;
            _temporary = temporary;
            _showExtraEffectDescription = showExtraEffectDescription;
            _elements = elements;
            _synchronizedLine = synchronizedLine;
            _text = ClearCardLayout.ClearCardDescriptionText(model, sourceText, synchronizedLine);
            _renderedText = SakuraDescriptionRegion.Centered(SakuraDescriptionRegion.NormalizeText(model, _text));
            return _text;
        }
    }

    private sealed class ClearCardNodes
    {
        private ClearCardNodes(
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

        public static ClearCardNodes From(NCard card) =>
            new(
                FieldValue<MegaLabel>(TitleLabelField, card),
                FieldValue<MegaRichTextLabel>(DescriptionLabelField, card),
                FieldValue<TextureRect>(EnergyIconField, card),
                FieldValue<MegaLabel>(EnergyLabelField, card));
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
            if (HasAncestor<NHoverTipCardContainer>(card))
                return new ClearCardLayoutContext(Spec.RootBox, Spec.DefaultRootPivotOffset, null);

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

        public Vector2 RootSize => SakuraCardGeometry.ClearLayout.RootSize;
        public Vector2 DefaultCardSize => NCard.defaultSize;
        public Vector2 DefaultCardCenteredOffset => (DefaultCardSize - RootSize) * 0.5f;
        public Vector2 DefaultRootPivotOffset => RootSize * 0.5f;
        public Rect2 RootBox => new(Vector2.Zero, RootSize);
        public Rect2 DefaultCardCenteredRootBox => new(DefaultCardCenteredOffset, RootSize);
        public Rect2 CenteredRootBox => SakuraCardGeometry.ClearLayout.CenteredRootBox;
        public Rect2 ArtBox => RootBox;
        public Vector2 HighlightMargin => SakuraCardGeometry.ClearLayout.HighlightMargin;
        public Rect2 HighlightBox => new(-HighlightMargin, RootSize + HighlightMargin * 2f);
        public Rect2 TitleBox { get; } = Scaled(new Rect2(new Vector2(23f, 8f), new Vector2(160f, 34f)));
        public Rect2 EnglishNameBox { get; } = Scaled(new Rect2(new Vector2(23f, 396f), new Vector2(160f, 30f)));
        public Rect2 DescriptionPanelBox { get; } = Scaled(new Rect2(new Vector2(12f, 230f), new Vector2(182f, 156f)));
        public Rect2 DescriptionBox { get; } = Scaled(new Rect2(new Vector2(16f, 238f), new Vector2(174f, 140f)));
        public Rect2 EnergyCostBox { get; } = Scaled(new Rect2(new Vector2(-14f, -12f), new Vector2(56f, 56f)));
        public Rect2 EnergyCostLabelBox { get; } = Scaled(new Rect2(new Vector2(12f, -2f), new Vector2(44f, 44f)));
        public Color DescriptionPanelColor { get; } = new(0f, 0f, 0f, 0.72f);
        public Color DefaultNameTextColor { get; } = new(1f, 1f, 1f, 1f);
        public Color UpgradedNameTextColor => SakuraCardVisualStyle.UpgradedNameTextColor;
        public Color NameTextOutlineColor { get; } = new(0.03f, 0.03f, 0.03f, 0.95f);
        public Color TitleTextShadowColor { get; } = new(0f, 0f, 0f, 0.1882353f);
        public float HighlightCornerRadius { get; } = Scaled(16f);
        public float HighlightSdfRange { get; } = Scaled(240f);
        public float HighlightTextureScale { get; } = 2f;
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
        public int HighlightZIndex => SakuraCardGeometry.ClearLayout.HighlightZIndex;
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

}
