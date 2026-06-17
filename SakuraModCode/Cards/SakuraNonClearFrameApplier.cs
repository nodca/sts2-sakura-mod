using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.addons.mega_text;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace SakuraMod.SakuraModCode.Cards;

internal static class SakuraNonClearFrameApplier
{
    private static readonly FieldInfo? TitleLabelField = AccessTools.Field(typeof(NCard), "_titleLabel");
    private static readonly FieldInfo? DescriptionLabelField = AccessTools.Field(typeof(NCard), "_descriptionLabel");
    private static readonly FieldInfo? AncientPortraitField = AccessTools.Field(typeof(NCard), "_ancientPortrait");
    private static readonly FieldInfo? AncientBorderField = AccessTools.Field(typeof(NCard), "_ancientBorder");
    private static readonly FieldInfo? AncientBannerField = AccessTools.Field(typeof(NCard), "_ancientBanner");
    private static readonly FieldInfo? AncientTextBgField = AccessTools.Field(typeof(NCard), "_ancientTextBg");
    private static readonly FieldInfo? AncientHighlightField = AccessTools.Field(typeof(NCard), "_ancientHighlight");
    private static readonly FieldInfo? PortraitField = AccessTools.Field(typeof(NCard), "_portrait");
    private static readonly FieldInfo? FrameField = AccessTools.Field(typeof(NCard), "_frame");
    private static readonly FieldInfo? PortraitBorderField = AccessTools.Field(typeof(NCard), "_portraitBorder");
    private static readonly FieldInfo? BannerField = AccessTools.Field(typeof(NCard), "_banner");
    private static readonly FieldInfo? TypeLabelField = AccessTools.Field(typeof(NCard), "_typeLabel");
    private static readonly FieldInfo? TypePlaqueField = AccessTools.Field(typeof(NCard), "_typePlaque");
    private static readonly FieldInfo? EnergyLabelField = AccessTools.Field(typeof(NCard), "_energyLabel");

    private static readonly StringName FontColorName = new("font_color");
    private static readonly StringName DefaultColorName = new("default_color");
    private static readonly StringName FontOutlineColorName = new("font_outline_color");
    private static readonly StringName OutlineSizeName = new("outline_size");

    private static readonly Color PartnerTextColor = new(1f, 1f, 1f, 1f);
    private const string PartnerCategoryTextColor = "#ffd6e8";
    private static readonly Color PartnerTitleOutlineColor = new("371720");
    private static readonly Color PartnerDescriptionOutlineColor = new("30181F");
    private static readonly Color TechniqueTextColor = new(1f, 1f, 1f, 1f);
    private const string TechniqueCategoryTextColor = "#9fe7ff";
    private static readonly Color TechniqueTitleOutlineColor = new("152A3A");
    private static readonly Color TechniqueDescriptionOutlineColor = new("102331");
    private static readonly Vector2 EnergyCostLabelOffset = new(4f, -4f);
    private const int TitleOutlineSize = 2;
    private const int DescriptionOutlineSize = 2;
    private const string HeaderPartSeparator = "  ";
    private const float HeaderPartSeparatorUnits = 1f;
    private const float HeaderLineUnits = 16f;
    private const float HighlightTextureScale = 2f;
    private const float HighlightFrameInset = 8f;
    private const float HighlightCornerRadius = 28f;
    private const float HighlightOuterGlowWidth = 92f;
    private const float HighlightInnerGlowWidth = 34f;
    private const float HighlightInnerAlphaScale = 0.55f;

    private static readonly ConditionalWeakTable<NCard, SakuraCostLabelState> CostLabelStates = new();
    private static readonly ConditionalWeakTable<NCard, SakuraNonClearDescriptionState> DescriptionStates = new();
    private static readonly ConditionalWeakTable<NCard, SakuraNonClearHighlightState> HighlightStates = new();
    private static readonly ConditionalWeakTable<NCard, SakuraNonClearStyleState> StyleStates = new();
    private static readonly ConditionalWeakTable<NinePatchRect, TypePlaqueTextureState> TypePlaqueTextureStates = new();
    private static readonly Dictionary<Vector2I, Texture2D> SakuraNonClearHighlightTextureCache = [];

    public static bool IsSakuraNonClearCard(NCard? card) =>
        card?.Model is { } model
        && model is SakuraModCard or SakuraOptionCard
        && !SakuraActions.IsClearCard(model)
        && model.Rarity != CardRarity.Ancient;

    public static void Apply(NCard card)
    {
        if (!IsSakuraNonClearCard(card))
        {
            RestoreNonSakuraCardVisuals(card);
            return;
        }

        ApplyCostLayout(card);
        ApplyVisuals(card);
    }

    public static void RestoreIfTracked(NCard card)
    {
        if (CostLabelStates.TryGetValue(card, out var costState))
            costState.Restore();
        if (DescriptionStates.TryGetValue(card, out var descriptionState))
            descriptionState.Restore();
        if (HighlightStates.TryGetValue(card, out var highlightState))
            highlightState.Restore();
        if (StyleStates.TryGetValue(card, out var styleState))
            styleState.Restore(card);
    }

    public static void RestoreNonSakuraCardVisuals(NCard? card)
    {
        if (card is null || IsSakuraNonClearCard(card))
            return;

        RestoreIfTracked(card);
        RestoreCurrentModelVisuals(card);
    }

    private static void RestoreCurrentModelVisuals(NCard card)
    {
        if (card.Model is not { } model)
            return;

        var isAncient = model.Rarity == CardRarity.Ancient;
        RestoreCurrentModelPortrait(card, model, isAncient);
        RestoreCurrentModelAncientVisuals(card, model, isAncient);
        RestoreCurrentModelFrame(card, model, isAncient);
        RestoreCurrentModelBanner(card, model, isAncient);
        RestoreCurrentModelTypePlaque(card, model);
    }

    private static void RestoreCurrentModelPortrait(NCard card, CardModel model, bool isAncient)
    {
        if (FieldValue<TextureRect>(PortraitField, card) is { } portrait && IsGodotInstanceUsable(portrait))
        {
            if (portrait.Visible == isAncient)
                portrait.Visible = !isAncient;
            if (!isAncient)
                SetTextureIfDifferent(portrait, model.Portrait);
        }

        if (FieldValue<TextureRect>(PortraitBorderField, card) is not { } border || !IsGodotInstanceUsable(border))
            return;

        if (border.Visible == isAncient)
            border.Visible = !isAncient;
        if (isAncient)
            return;

        SetTextureIfDifferent(border, model.PortraitBorder);
        if (border.Material != model.BannerMaterial)
            border.Material = model.BannerMaterial;
    }

    private static void RestoreCurrentModelAncientVisuals(NCard card, CardModel model, bool isAncient)
    {
        RestoreAncientTexture(FieldValue<TextureRect>(AncientPortraitField, card), isAncient, isAncient ? model.Portrait : null);
        RestoreAncientTexture(FieldValue<TextureRect>(AncientTextBgField, card), isAncient, isAncient ? model.AncientTextBg : null);
        RestoreAncientCanvasItem(FieldValue<CanvasItem>(AncientBorderField, card), isAncient);
        RestoreAncientCanvasItem(FieldValue<CanvasItem>(AncientBannerField, card), isAncient);
        RestoreAncientCanvasItem(FieldValue<CanvasItem>(AncientHighlightField, card), isAncient);
    }

    private static void RestoreAncientTexture(TextureRect? item, bool isAncient, Texture2D? texture)
    {
        if (item is null || !IsGodotInstanceUsable(item))
            return;

        if (item.Visible != isAncient)
            item.Visible = isAncient;
        if (isAncient)
            SetTextureIfDifferent(item, texture);
    }

    private static void RestoreAncientCanvasItem(CanvasItem? item, bool isAncient)
    {
        if (item is null || !IsGodotInstanceUsable(item))
            return;

        if (item.Visible != isAncient)
            item.Visible = isAncient;
    }

    private static void RestoreCurrentModelFrame(NCard card, CardModel model, bool isAncient)
    {
        if (FieldValue<TextureRect>(FrameField, card) is not { } frame || !IsGodotInstanceUsable(frame))
            return;

        if (frame.Visible == isAncient)
            frame.Visible = !isAncient;
        if (!isAncient)
            SetTextureIfDifferent(frame, model.Frame);
        if (frame.Material != model.FrameMaterial)
            frame.Material = model.FrameMaterial;
    }

    private static void RestoreCurrentModelBanner(NCard card, CardModel model, bool isAncient)
    {
        if (FieldValue<TextureRect>(BannerField, card) is not { } banner || !IsGodotInstanceUsable(banner))
            return;

        if (banner.Visible == isAncient)
            banner.Visible = !isAncient;
        if (isAncient)
        {
            if (banner.Material is not null)
                banner.Material = null;
            return;
        }

        SetTextureIfDifferent(banner, model.BannerTexture);
        if (banner.Material != model.BannerMaterial)
            banner.Material = model.BannerMaterial;
    }

    private static void RestoreCurrentModelTypePlaque(NCard card, CardModel model)
    {
        if (FieldValue<NinePatchRect>(TypePlaqueField, card) is not { } plaque || !IsGodotInstanceUsable(plaque))
            return;

        var vanillaTexture = VanillaTypePlaqueTexture(plaque);
        SetTextureIfDifferent(plaque, vanillaTexture);
        if (plaque.Material != model.BannerMaterial)
            plaque.Material = model.BannerMaterial;
    }

    private static void ApplyCostLayout(NCard card)
    {
        if (FieldValue<MegaLabel>(EnergyLabelField, card) is not { } label)
            return;

        var state = CostLabelStates.GetOrCreateValue(card);
        state.Apply(label, EnergyCostLabelOffset);
    }

    private static void ApplyVisuals(NCard card)
    {
        if (card.Model is not { } model)
            return;

        RestoreCurrentModelVisuals(card);

        if (FieldValue<TextureRect>(PortraitField, card) is { } portrait
            && SakuraCardFrameVisuals.PortraitTexture(model) is { } portraitTexture
            && !HasTexture(portrait, portraitTexture))
        {
            SetTextureIfDifferent(portrait, portraitTexture);
        }

        ApplyFrameTexture(
            FieldValue<TextureRect>(FrameField, card),
            SakuraCardFrameVisuals.FrameTexture(model, SakuraFramePart.Frame),
            SakuraCardFrameVisuals.PlainFrameMaterial);
        ApplyFrameTexture(
            FieldValue<TextureRect>(PortraitBorderField, card),
            SakuraCardFrameVisuals.FrameTexture(model, SakuraFramePart.PortraitBorder),
            null);
        ApplyFrameTexture(
            FieldValue<TextureRect>(BannerField, card),
            SakuraCardFrameVisuals.FrameTexture(model, SakuraFramePart.Banner),
            null);
        ApplyFrameTexture(
            FieldValue<NinePatchRect>(TypePlaqueField, card),
            SakuraCardFrameVisuals.FrameTexture(model, SakuraFramePart.TypePlaque));
        ApplyHighlightTexture(card, card.CardHighlight);
        var isPartner = SakuraActions.IsPartner(model);
        ApplyDescriptionCategory(card, isPartner);
        ApplyTextStyle(card, isPartner);
        StyleStates.GetOrCreateValue(card).MarkApplied();
    }

    private static void ApplyFrameTexture(TextureRect? item, Texture2D texture, Material? material)
    {
        if (item is null || !IsGodotInstanceUsable(item))
            return;

        if (!item.Visible)
            item.Visible = true;
        SetTextureIfDifferent(item, texture);
        if (item.Material != material)
            item.Material = material;
    }

    private static void ApplyFrameTexture(NinePatchRect? item, Texture2D texture)
    {
        if (item is null || !IsGodotInstanceUsable(item))
            return;

        _ = VanillaTypePlaqueTexture(item);
        SetTextureIfDifferent(item, texture);
        if (item.Material is not null)
            item.Material = null;
    }

    private static void ApplyHighlightTexture(NCard card, NCardHighlight? highlight)
    {
        if (highlight is null || !IsGodotInstanceUsable(highlight))
            return;

        HighlightStates
            .GetOrCreateValue(card)
            .Apply(highlight, SakuraNonClearHighlightTexture(highlight.Size));
    }

    private static void ApplyDescriptionCategory(NCard card, bool isPartner)
    {
        if (FieldValue<MegaRichTextLabel>(DescriptionLabelField, card) is not { } description)
            return;

        var label = isPartner ? SakuraStateText.PartnerCardLabel() : SakuraStateText.TechniqueCardLabel();
        var color = isPartner ? PartnerCategoryTextColor : TechniqueCategoryTextColor;
        DescriptionStates
            .GetOrCreateValue(card)
            .Apply(description, $"[color={color}]{label}[/color]");
    }

    private static void ApplyTextStyle(NCard card, bool isPartner)
    {
        var textColor = isPartner ? PartnerTextColor : TechniqueTextColor;
        var titleTextColor = card.Model?.IsUpgraded == true ? SakuraCardVisualStyle.UpgradedNameTextColor : textColor;
        var titleOutlineColor = isPartner ? PartnerTitleOutlineColor : TechniqueTitleOutlineColor;
        var descriptionOutlineColor = isPartner
            ? PartnerDescriptionOutlineColor
            : TechniqueDescriptionOutlineColor;

        var titleLabel = FieldValue<Control>(TitleLabelField, card);
        ApplyThemeColorOverride(titleLabel, FontColorName, titleTextColor);
        ApplyThemeColorOverride(titleLabel, FontOutlineColorName, titleOutlineColor);
        ApplyThemeConstantOverride(titleLabel, OutlineSizeName, TitleOutlineSize);

        var descriptionLabel = FieldValue<Control>(DescriptionLabelField, card);
        ApplyThemeColorOverride(descriptionLabel, FontColorName, textColor);
        ApplyThemeColorOverride(descriptionLabel, DefaultColorName, textColor);
        ApplyThemeColorOverride(descriptionLabel, FontOutlineColorName, descriptionOutlineColor);
        ApplyThemeConstantOverride(descriptionLabel, OutlineSizeName, DescriptionOutlineSize);

        var typeLabel = FieldValue<Control>(TypeLabelField, card);
        ApplyThemeColorOverride(typeLabel, FontColorName, textColor);
        ApplyThemeColorOverride(typeLabel, FontOutlineColorName, descriptionOutlineColor);
    }

    private static T? FieldValue<T>(FieldInfo? field, object instance)
        where T : class =>
        field?.GetValue(instance) as T;

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

    private static bool HasTexture(TextureRect item, Texture2D? texture) =>
        TryGetTexture(item, out var currentTexture)
        && ((currentTexture is null && texture is null)
            || (IsGodotInstanceUsable(currentTexture) && ReferenceEquals(currentTexture, texture)));

    private static void SetTextureIfDifferent(TextureRect item, Texture2D? texture)
    {
        if (texture is not null && !IsGodotInstanceUsable(texture))
            return;
        if (HasTexture(item, texture))
            return;

        try
        {
            item.Texture = texture;
        }
        catch (ObjectDisposedException)
        {
            return;
        }
    }

    private static bool HasTexture(NinePatchRect item, Texture2D? texture) =>
        TryGetTexture(item, out var currentTexture)
        && ((currentTexture is null && texture is null)
            || (IsGodotInstanceUsable(currentTexture) && ReferenceEquals(currentTexture, texture)));

    private static void SetTextureIfDifferent(NinePatchRect item, Texture2D? texture)
    {
        if (texture is not null && !IsGodotInstanceUsable(texture))
            return;
        if (HasTexture(item, texture))
            return;

        try
        {
            item.Texture = texture;
        }
        catch (ObjectDisposedException)
        {
            return;
        }
    }

    private static bool TryGetTexture(TextureRect item, out Texture2D? texture)
    {
        try
        {
            texture = item.Texture;
            return true;
        }
        catch (ObjectDisposedException)
        {
            texture = null;
            return false;
        }
    }

    private static bool TryGetTexture(NinePatchRect item, out Texture2D? texture)
    {
        try
        {
            texture = item.Texture;
            return true;
        }
        catch (ObjectDisposedException)
        {
            texture = null;
            return false;
        }
    }

    private static void ApplyThemeColorOverride(Control? control, StringName name, Color color)
    {
        if (control is null || !IsGodotInstanceUsable(control))
            return;

        if (!control.HasThemeColorOverride(name) || control.GetThemeColor(name) != color)
            control.AddThemeColorOverride(name, color);
    }

    private static void ApplyThemeConstantOverride(Control? control, StringName name, int value)
    {
        if (control is null || !IsGodotInstanceUsable(control))
            return;

        if (!control.HasThemeConstantOverride(name) || control.GetThemeConstant(name) != value)
            control.AddThemeConstantOverride(name, value);
    }

    private static void RemoveThemeColorOverride(Control? control, StringName name)
    {
        if (control is null || !IsGodotInstanceUsable(control) || !control.HasThemeColorOverride(name))
            return;

        control.RemoveThemeColorOverride(name);
    }

    private static void RemoveThemeConstantOverride(Control? control, StringName name)
    {
        if (control is null || !IsGodotInstanceUsable(control) || !control.HasThemeConstantOverride(name))
            return;

        control.RemoveThemeConstantOverride(name);
    }

    private static Texture2D? VanillaTypePlaqueTexture(NinePatchRect plaque) =>
        TypePlaqueTextureStates.GetValue(
            plaque,
            static item => new TypePlaqueTextureState(TryGetTexture(item, out var texture) ? texture : null)).Texture;

    private static Texture2D SakuraNonClearHighlightTexture(Vector2 controlSize)
    {
        var textureSize = new Vector2(
            Mathf.Max(controlSize.X, NCard.defaultSize.X),
            Mathf.Max(controlSize.Y, NCard.defaultSize.Y));
        var cacheKey = new Vector2I(Mathf.RoundToInt(textureSize.X), Mathf.RoundToInt(textureSize.Y));
        if (SakuraNonClearHighlightTextureCache.TryGetValue(cacheKey, out var cachedTexture))
        {
            if (IsGodotInstanceUsable(cachedTexture))
                return cachedTexture;

            SakuraNonClearHighlightTextureCache.Remove(cacheKey);
        }

        var width = Mathf.CeilToInt(textureSize.X * HighlightTextureScale);
        var height = Mathf.CeilToInt(textureSize.Y * HighlightTextureScale);
        var image = Image.CreateEmpty(width, height, false, Image.Format.Rgbaf);
        var cardCenter = textureSize * 0.5f;
        var cardHalfSize = NCard.defaultSize * 0.5f - Vector2.One * HighlightFrameInset;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var point = new Vector2((x + 0.5f) / HighlightTextureScale, (y + 0.5f) / HighlightTextureScale);
                var distance = RoundedRectDistance(point - cardCenter, cardHalfSize, HighlightCornerRadius);
                var alpha = HighlightAlpha(distance);
                image.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        var texture = ImageTexture.CreateFromImage(image);
        SakuraNonClearHighlightTextureCache[cacheKey] = texture;
        return texture;
    }

    private static float HighlightAlpha(float signedDistance)
    {
        if (signedDistance >= 0f)
            return SmoothFade(signedDistance, HighlightOuterGlowWidth);

        return SmoothFade(-signedDistance, HighlightInnerGlowWidth) * HighlightInnerAlphaScale;
    }

    private static float SmoothFade(float distance, float width)
    {
        var t = Mathf.Clamp(distance / width, 0f, 1f);
        return 1f - t * t * (3f - 2f * t);
    }

    private static float RoundedRectDistance(Vector2 point, Vector2 halfSize, float radius)
    {
        var cornerRadius = Mathf.Min(radius, Mathf.Min(halfSize.X, halfSize.Y));
        var q = point.Abs() - (halfSize - Vector2.One * cornerRadius);
        var outside = new Vector2(Mathf.Max(q.X, 0f), Mathf.Max(q.Y, 0f));
        return outside.Length() + Mathf.Min(Mathf.Max(q.X, q.Y), 0f) - cornerRadius;
    }

    private static void RemoveTextStyle(NCard card)
    {
        var titleLabel = FieldValue<Control>(TitleLabelField, card);
        RemoveThemeColorOverride(titleLabel, FontColorName);
        RemoveThemeColorOverride(titleLabel, FontOutlineColorName);
        RemoveThemeConstantOverride(titleLabel, OutlineSizeName);

        var descriptionLabel = FieldValue<Control>(DescriptionLabelField, card);
        RemoveThemeColorOverride(descriptionLabel, FontColorName);
        RemoveThemeColorOverride(descriptionLabel, DefaultColorName);
        RemoveThemeColorOverride(descriptionLabel, FontOutlineColorName);
        RemoveThemeConstantOverride(descriptionLabel, OutlineSizeName);

        var typeLabel = FieldValue<Control>(TypeLabelField, card);
        RemoveThemeColorOverride(typeLabel, FontColorName);
        RemoveThemeColorOverride(typeLabel, FontOutlineColorName);
    }

    private sealed class SakuraCostLabelState
    {
        private MegaLabel? _label;
        private Vector2? _position;

        public void Apply(MegaLabel label, Vector2 offset)
        {
            if (!IsGodotInstanceUsable(label))
                return;

            Capture(label);
            if (_position is { } position)
            {
                var targetPosition = position + offset;
                if (label.Position != targetPosition)
                    label.Position = targetPosition;
            }
        }

        public void Restore()
        {
            if (_label is null)
                return;

            if (!IsGodotInstanceUsable(_label))
            {
                _label = null;
                _position = null;
                return;
            }

            if (_position is { } position && _label.Position != position)
                _label.Position = position;
        }

        private void Capture(MegaLabel label)
        {
            if (_label == label && _position is not null)
                return;

            Restore();
            _label = label;
            _position = label.Position;
        }
    }

    private sealed class SakuraNonClearHighlightState
    {
        private NCardHighlight? _highlight;
        private Texture2D? _texture;
        private TextureRect.ExpandModeEnum? _expandMode;
        private TextureRect.StretchModeEnum? _stretchMode;
        private Control.MouseFilterEnum? _mouseFilter;
        private bool _captured;

        public void Apply(NCardHighlight highlight, Texture2D texture)
        {
            if (!IsGodotInstanceUsable(highlight))
                return;

            Capture(highlight);
            SetTextureIfDifferent(highlight, texture);
            if (highlight.ExpandMode != TextureRect.ExpandModeEnum.IgnoreSize)
                highlight.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            if (highlight.StretchMode != TextureRect.StretchModeEnum.Scale)
                highlight.StretchMode = TextureRect.StretchModeEnum.Scale;
            if (highlight.MouseFilter != Control.MouseFilterEnum.Ignore)
                highlight.MouseFilter = Control.MouseFilterEnum.Ignore;
        }

        public void Restore()
        {
            if (!_captured || _highlight is null)
                return;

            if (!IsGodotInstanceUsable(_highlight))
            {
                Clear();
                return;
            }

            SetTextureIfDifferent(_highlight, _texture);
            if (_expandMode is { } expandMode && _highlight.ExpandMode != expandMode)
                _highlight.ExpandMode = expandMode;
            if (_stretchMode is { } stretchMode && _highlight.StretchMode != stretchMode)
                _highlight.StretchMode = stretchMode;
            if (_mouseFilter is { } mouseFilter && _highlight.MouseFilter != mouseFilter)
                _highlight.MouseFilter = mouseFilter;

            Clear();
        }

        private void Capture(NCardHighlight highlight)
        {
            if (_highlight == highlight && _captured)
                return;

            Restore();
            _highlight = highlight;
            _texture = TryGetTexture(highlight, out var texture) ? texture : null;
            _expandMode = highlight.ExpandMode;
            _stretchMode = highlight.StretchMode;
            _mouseFilter = highlight.MouseFilter;
            _captured = true;
        }

        private void Clear()
        {
            _highlight = null;
            _texture = null;
            _expandMode = null;
            _stretchMode = null;
            _mouseFilter = null;
            _captured = false;
        }
    }

    private sealed class SakuraNonClearDescriptionState
    {
        private MegaRichTextLabel? _label;
        private string? _sourceText;
        private string? _appliedText;

        public void Apply(MegaRichTextLabel label, string categoryText)
        {
            if (!IsGodotInstanceUsable(label))
                return;

            Capture(label);
            var sourceText = label.Text == _appliedText && _sourceText is not null
                ? _sourceText
                : label.Text;
            var text = DescriptionText(categoryText, sourceText);

            _sourceText = sourceText;
            _appliedText = text;
            if (label.Text != text)
                label.SetTextAutoSize(text);
        }

        public void Restore()
        {
            if (_label is null)
                return;

            if (!IsGodotInstanceUsable(_label))
            {
                Clear();
                return;
            }

            if (_sourceText is not null && _label.Text == _appliedText)
                _label.SetTextAutoSize(_sourceText);

            Clear();
        }

        private void Capture(MegaRichTextLabel label)
        {
            if (_label == label)
                return;

            Restore();
            _label = label;
            _sourceText = null;
            _appliedText = null;
        }

        private static string DescriptionText(string categoryText, string sourceText)
        {
            var body = RemoveCenterTags(sourceText);
            var headerParts = new List<string> { categoryText };
            var bodyStart = ExtractLeadingKeywordLines(body, headerParts);
            var header = string.Join('\n', PackHeaderLines(headerParts));
            var bodyText = bodyStart > 0 ? body[bodyStart..] : body;

            return bodyText.Length == 0
                ? CenterText(header)
                : CenterText($"{header}\n{bodyText}");
        }

        private static int ExtractLeadingKeywordLines(string text, List<string> headerParts)
        {
            var bodyStart = 0;
            var lineStart = 0;
            while (lineStart < text.Length)
            {
                var lineEnd = LineEnd(text, lineStart);
                var line = text[lineStart..lineEnd].Trim();
                if (!TryPackableKeywordLine(line, out var keywordText))
                    break;

                headerParts.Add(keywordText);
                bodyStart = NextLineStart(text, lineEnd);
                lineStart = bodyStart;
            }

            return bodyStart;
        }

        private static int LineEnd(string text, int start)
        {
            var index = start;
            while (index < text.Length && text[index] is not '\r' and not '\n')
                index++;

            return index;
        }

        private static int NextLineStart(string text, int lineEnd)
        {
            if (lineEnd >= text.Length)
                return lineEnd;

            var next = lineEnd + 1;
            if (text[lineEnd] == '\r' && next < text.Length && text[next] == '\n')
                next++;

            return next;
        }

        private static bool TryPackableKeywordLine(string line, out string keywordText)
        {
            var visibleText = TrimKeywordLineEnding(RemoveRichTextTags(line));
            if (SakuraStateText.KnownNonClearHeaderKeywordLabels.Contains(visibleText))
            {
                keywordText = $"[gold]{visibleText}[/gold]";
                return true;
            }

            keywordText = string.Empty;
            return false;
        }

        private static string TrimKeywordLineEnding(string text)
        {
            var end = text.Length;
            while (end > 0 && (char.IsWhiteSpace(text[end - 1]) || text[end - 1] is '.' or '。'))
                end--;

            return text[..end].Trim();
        }

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
                if (currentParts.Count > 0 && candidateUnits > HeaderLineUnits)
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

        private static string CenterText(string text) =>
            $"[center]{text}[/center]";

        private static string RemoveCenterTags(string text) =>
            text
                .Replace("[center]", string.Empty, StringComparison.Ordinal)
                .Replace("[/center]", string.Empty, StringComparison.Ordinal);

        private static string RemoveRichTextTags(string text)
        {
            var builder = new StringBuilder(text.Length);
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
                if (!inTag)
                    builder.Append(character);
            }

            return builder.ToString().Trim();
        }

        private void Clear()
        {
            _label = null;
            _sourceText = null;
            _appliedText = null;
        }
    }

    private sealed class SakuraNonClearStyleState
    {
        private bool _isApplied;

        public void MarkApplied() => _isApplied = true;

        public void Restore(NCard card)
        {
            if (!_isApplied)
                return;

            RemoveTextStyle(card);
            _isApplied = false;
        }
    }

    private sealed class TypePlaqueTextureState(Texture2D? texture)
    {
        public Texture2D? Texture { get; } = texture;
    }
}
