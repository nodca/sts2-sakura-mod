using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Classic.Cards;
using SakuraMod.SakuraModCode.Extensions;
using System.Runtime.CompilerServices;
using System.Text;

namespace SakuraMod.SakuraModCode.Cards;

internal enum SakuraDescriptionShape
{
    Attack,
    Skill,
    Power
}

internal static class SakuraDescriptionRegion
{
    private static readonly Vector2 RegionSize = new(204f, 236f);
    private static readonly Vector2 TextOffset = new(7f, 18f);
    private static readonly Vector2 TextSize = new(190f, 200f);
    private static readonly Vector2 ClassicOrigin = new(8.5f, 203f);
    private static readonly Vector2 ClearOrigin = new(6.15f, 188f);
    private static readonly Dictionary<string, SakuraCardTextureResource> TextureResources = [];
    private static readonly ConditionalWeakTable<NCard, SakuraDescriptionRegionNodes> NodeStates = new();

    public const int MinimumFontSize = 12;
    public const int MaximumFontSize = 18;
    public const int BackgroundZIndex = 0;
    public const int TextZIndex = 0;
    public static readonly Color BackgroundModulate = new(1f, 1f, 1f, 0.72f);

    public static bool AppliesTo(CardModel? card)
    {
        if (card is null || card.Type is not (CardType.Attack or CardType.Skill or CardType.Power))
            return false;

        if (card is SakuraOptionCard)
            return true;

        return SakuraCardCatalog.TryGetMetadata(card, out var metadata)
            && metadata.VisualRoute is SakuraSourceCardVisualRoute.Classic or SakuraSourceCardVisualRoute.Clear
            && (metadata.Era is SourceEraClass.Clow or SourceEraClass.Sakura or SourceEraClass.Clear
                || card is ClassicSpellCard);
    }

    public static bool ShouldShow(
        bool isInCombatHand,
        bool isFocused,
        bool isCurrentCardPlay) =>
        !isInCombatHand || isFocused || isCurrentCardPlay;

    public static SakuraDescriptionShape ShapeFor(CardModel card)
    {
        if (!AppliesTo(card))
            throw new ArgumentException($"Card {card.GetType().Name} does not use the Sakura description region.", nameof(card));

        return card.Type switch
        {
            CardType.Attack => SakuraDescriptionShape.Attack,
            CardType.Skill => SakuraDescriptionShape.Skill,
            CardType.Power => SakuraDescriptionShape.Power,
            _ => throw new ArgumentOutOfRangeException(nameof(card), card.Type, "Unsupported Sakura description card type.")
        };
    }

    public static Rect2 RegionBox(SakuraCardVisualLayout layout) =>
        new(Origin(layout), RegionSize);

    public static Rect2 TextBox(SakuraCardVisualLayout layout) =>
        new(Origin(layout) + TextOffset, TextSize);

    public static string ShapeAssetPath(CardModel card) =>
        ShapeFileName(ShapeFor(card)).DescriptionRegionAssetPath();

    public static IEnumerable<string> AssetPaths(CardModel card)
    {
        if (AppliesTo(card))
            yield return ShapeAssetPath(card);
    }

    public static Texture2D ShapeTexture(CardModel card)
    {
        var path = ShapeFileName(ShapeFor(card)).DescriptionRegionImagePath();
        if (!TextureResources.TryGetValue(path, out var resource))
        {
            resource = SakuraCardTextureResource.FromPath(path);
            TextureResources[path] = resource;
        }

        return resource.ResolveRequired("Sakura description region texture");
    }

    public static SakuraDescriptionRegionNodes NodesFor(NCard card)
    {
        var nodes = NodeStates.GetOrCreateValue(card);
        nodes.Attach(card);
        return nodes;
    }

    public static void ApplyBackground(
        TextureRect background,
        CardModel card,
        SakuraCardVisualLayout layout,
        bool visible)
    {
        SakuraCardVisualInfrastructure.ApplyBox(background, RegionBox(layout));
        if (background.ExpandMode != TextureRect.ExpandModeEnum.IgnoreSize)
            background.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        if (background.StretchMode != TextureRect.StretchModeEnum.Scale)
            background.StretchMode = TextureRect.StretchModeEnum.Scale;
        if (visible)
            SakuraCardVisualInfrastructure.SetTextureIfDifferent(background, ShapeTexture(card));
        if (background.Visible != visible)
            background.Visible = visible;
        if (background.SelfModulate != BackgroundModulate)
            background.SelfModulate = BackgroundModulate;
        if (background.ZIndex != BackgroundZIndex)
            background.ZIndex = BackgroundZIndex;
        if (background.MouseFilter != Control.MouseFilterEnum.Ignore)
            background.MouseFilter = Control.MouseFilterEnum.Ignore;
    }

    public static void ApplyText(
        MegaRichTextLabel? label,
        Rect2 box,
        CardModel card,
        string text,
        bool visible)
    {
        if (label is null)
            return;

        SakuraCardVisualInfrastructure.ApplyBox(label, box);
        var renderedText = Centered(NormalizeText(card, text));
        if (label.Text != renderedText)
            label.SetTextAutoSize(renderedText);
        if (label.Visible != visible)
            label.Visible = visible;
        if (label.ScrollActive)
            label.ScrollActive = false;
        if (label.FitContent)
            label.FitContent = false;
        if (!label.IsHorizontallyBound)
            label.IsHorizontallyBound = true;
        if (!label.IsVerticallyBound)
            label.IsVerticallyBound = true;
        if (label.MinFontSize != MinimumFontSize)
            label.MinFontSize = MinimumFontSize;
        if (label.MaxFontSize != MaximumFontSize)
            label.MaxFontSize = MaximumFontSize;
        if (label.ZIndex != TextZIndex)
            label.ZIndex = TextZIndex;
    }

    public static void ApplyVisibility(
        CanvasItem? background,
        CanvasItem? description,
        bool visible)
    {
        SetVisible(background, visible);
        SetVisible(description, visible);
    }

    public static string Centered(string text) =>
        text.Length == 0 ? string.Empty : $"[center]{text}[/center]";

    public static string NormalizeText(CardModel card, string text) =>
        PackKeywordHeader(
            RemoveCenterTags(text).Trim(),
            BracketedIdentityKeywordCount(card),
            NativeKeywordCount(card));

    private static Vector2 Origin(SakuraCardVisualLayout layout) =>
        layout switch
        {
            SakuraCardVisualLayout.Classic => ClassicOrigin,
            SakuraCardVisualLayout.Clear => ClearOrigin,
            _ => throw new ArgumentOutOfRangeException(nameof(layout), layout, "Layout has no Sakura description region.")
        };

    private static string ShapeFileName(SakuraDescriptionShape shape) =>
        shape switch
        {
            SakuraDescriptionShape.Attack => "attack.png",
            SakuraDescriptionShape.Skill => "skill.png",
            SakuraDescriptionShape.Power => "power.png",
            _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, null)
        };

    private static string PackKeywordHeader(string text, int bracketedKeywordCount, int nativeKeywordCount)
    {
        var lines = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

        var keywordRunStart = Array.FindIndex(lines, IsKeywordOnlyLine);
        if (keywordRunStart >= 0 && bracketedKeywordCount > 0)
        {
            var identityLine = keywordRunStart;
            while (identityLine + 1 < lines.Length && IsKeywordOnlyLine(lines[identityLine + 1]))
                identityLine++;

            var keywordSpanCount = lines
                .Skip(keywordRunStart)
                .Take(identityLine - keywordRunStart + 1)
                .Sum(CountGoldSpans);
            if (keywordSpanCount > nativeKeywordCount)
                lines[identityLine] = DecorateIdentityKeywords(lines[identityLine], bracketedKeywordCount);
        }

        var builder = new StringBuilder(text.Length);
        for (var i = 0; i < lines.Length; i++)
        {
            var joinsNextKeyword =
                i < lines.Length - 1
                && IsKeywordOnlyLine(lines[i])
                && IsKeywordOnlyLine(lines[i + 1]);
            builder.Append(joinsNextKeyword ? TrimTerminalKeywordPunctuation(lines[i]) : lines[i]);
            if (i >= lines.Length - 1)
                continue;

            builder.Append(joinsNextKeyword ? " " : "\n");
        }

        return builder.ToString();
    }

    private static int BracketedIdentityKeywordCount(CardModel card)
    {
        if (card is ClassicSpellCard spell)
            return 1 + spell.Element.AsElements().Count();
        if (card is ClassicSakuraCard classic)
        {
            var elementCount = classic.Element.AsElements().Count();
            return classic.IsSakuraCard
                ? 1 + elementCount
                : elementCount;
        }

        return SakuraActions.StaticElementSetOf(card).AsElements().Count();
    }

    private static int NativeKeywordCount(CardModel card) =>
        card.CanonicalKeywords
            .Concat(card.Keywords)
            .Distinct()
            .Count(keyword => keyword == CardKeyword.Exhaust
                || keyword == CardKeyword.Ethereal
                || keyword == CardKeyword.Retain
                || keyword == CardKeyword.Innate
                || keyword == CardKeyword.Unplayable);

    private static int CountGoldSpans(string line)
    {
        const string openTag = "[gold]";
        const string closeTag = "[/gold]";
        var count = 0;
        var position = 0;
        while (true)
        {
            var open = line.IndexOf(openTag, position, StringComparison.OrdinalIgnoreCase);
            if (open < 0)
                return count;
            var close = line.IndexOf(closeTag, open + openTag.Length, StringComparison.OrdinalIgnoreCase);
            if (close < 0)
                return count;

            count++;
            position = close + closeTag.Length;
        }
    }

    private static string DecorateIdentityKeywords(string line, int count)
    {
        const string openTag = "[gold]";
        const string closeTag = "[/gold]";
        if (CountDecoratedGoldSpans(line, openTag, closeTag) >= count)
            return CompactDecoratedKeywordGaps(TrimTerminalKeywordPunctuation(line));

        var builder = new StringBuilder(line.Length + count * 2);
        var position = 0;
        var decorated = 0;
        while (decorated < count)
        {
            var open = line.IndexOf(openTag, position, StringComparison.OrdinalIgnoreCase);
            if (open < 0)
                break;
            var close = line.IndexOf(closeTag, open + openTag.Length, StringComparison.OrdinalIgnoreCase);
            if (close < 0)
                break;

            builder.Append(line, position, open - position);
            builder.Append('「');
            builder.Append(line, open, close + closeTag.Length - open);
            builder.Append('」');
            position = close + closeTag.Length;
            decorated++;
        }

        builder.Append(line, position, line.Length - position);
        return CompactDecoratedKeywordGaps(TrimTerminalKeywordPunctuation(builder.ToString()));
    }

    private static int CountDecoratedGoldSpans(string line, string openTag, string closeTag)
    {
        var count = 0;
        var position = 0;
        while (true)
        {
            var open = line.IndexOf(openTag, position, StringComparison.OrdinalIgnoreCase);
            if (open < 0)
                return count;
            var close = line.IndexOf(closeTag, open + openTag.Length, StringComparison.OrdinalIgnoreCase);
            if (close < 0)
                return count;

            var afterClose = close + closeTag.Length;
            if (open > 0
                && line[open - 1] == '「'
                && afterClose < line.Length
                && line[afterClose] == '」')
            {
                count++;
            }

            position = afterClose;
        }
    }

    private static string CompactDecoratedKeywordGaps(string line)
    {
        var builder = new StringBuilder(line.Length);
        for (var i = 0; i < line.Length; i++)
        {
            builder.Append(line[i]);
            if (line[i] != '」')
                continue;

            var next = i + 1;
            while (next < line.Length && char.IsWhiteSpace(line[next]))
                next++;
            if (next < line.Length && line[next] == '「')
                i = next - 1;
        }

        return builder.ToString();
    }

    private static string TrimTerminalKeywordPunctuation(string line) =>
        line.TrimEnd().TrimEnd('.', '。', ',', '，', ';', '；', '、').TrimEnd();

    private static bool IsKeywordOnlyLine(string line)
    {
        var goldDepth = 0;
        var sawGoldContent = false;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '[')
            {
                var tagEnd = line.IndexOf(']', i + 1);
                if (tagEnd < 0)
                    return false;

                var tag = line.AsSpan(i + 1, tagEnd - i - 1).Trim();
                if (tag.Equals("gold", StringComparison.OrdinalIgnoreCase))
                    goldDepth++;
                else if (tag.Equals("/gold", StringComparison.OrdinalIgnoreCase))
                {
                    if (goldDepth == 0)
                        return false;
                    goldDepth--;
                }

                i = tagEnd;
                continue;
            }

            if (goldDepth > 0)
            {
                sawGoldContent |= !char.IsWhiteSpace(line[i]);
                continue;
            }

            if (!char.IsWhiteSpace(line[i]) && !char.IsPunctuation(line[i]))
                return false;
        }

        return goldDepth == 0 && sawGoldContent;
    }

    private static string RemoveCenterTags(string text) =>
        text
            .Replace("[center]", string.Empty, StringComparison.Ordinal)
            .Replace("[/center]", string.Empty, StringComparison.Ordinal);

    private static void SetVisible(CanvasItem? item, bool visible)
    {
        if (!SakuraCardVisualInfrastructure.IsGodotInstanceUsable(item)
            || item!.Visible == visible)
            return;

        item.Visible = visible;
    }
}

internal sealed class SakuraDescriptionRegionNodes
{
    private TextureRect? _background;

    public TextureRect Background => _background
        ?? throw new InvalidOperationException("Description background has not been attached.");

    public void Attach(NCard card)
    {
        if (!SakuraCardVisualInfrastructure.IsGodotInstanceUsable(card.Body))
            throw new InvalidOperationException("Cannot attach a description region without a valid card body.");

        if (!SakuraCardVisualInfrastructure.IsGodotInstanceUsable(_background))
        {
            _background = new TextureRect
            {
                Name = "SakuraDescriptionRegionBackground",
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
        }

        AttachBodyChild(card, _background!, 2);
    }

    private static void AttachBodyChild(NCard card, Node node, int? childIndex)
    {
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
}
