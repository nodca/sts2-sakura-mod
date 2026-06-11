using Godot;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Extensions;

namespace SakuraMod.SakuraModCode.Cards;

internal static class SakuraCardFrameVisuals
{
    private const string DefaultPortraitFileName = "card.png";
    private const string DreamWandPortraitFileName = "dream_wand.png";
    private static readonly Dictionary<string, Texture2D?> PortraitTextureCache = [];
    private static readonly Dictionary<string, Texture2D?> FrameTextureCache = [];
    private static readonly CanvasItemMaterial _plainFrameMaterial = new();

    public static Material PlainFrameMaterial => _plainFrameMaterial;

    public static string BigPortraitPath(CardModel card) => PortraitFileName(card).BigCardImagePath();

    public static string PortraitPath(CardModel card) => PortraitFileName(card).CardImagePath();

    public static Texture2D? PortraitTexture(CardModel card)
    {
        var path = PortraitPath(card);
        if (PortraitTextureCache.TryGetValue(path, out var cachedTexture))
            return cachedTexture;

        var texture = ResourceLoader.Exists(path)
            ? ResourceLoader.Load<Texture2D>(path)
            : null;
        PortraitTextureCache[path] = texture;
        return texture;
    }

    public static Texture2D? CustomFrameTexture(CardModel card) =>
        SakuraActions.IsClearCard(card)
            ? null
            : FrameTexture(card, SakuraFramePart.Frame);

    public static Texture2D FrameTexture(CardModel card, SakuraFramePart part)
    {
        var path = FramePartPath(card, part);
        if (FrameTextureCache.TryGetValue(path, out var cachedTexture))
            return cachedTexture ?? throw new InvalidOperationException($"Missing Sakura frame texture: {path}");

        if (!ResourceLoader.Exists(path))
            throw new FileNotFoundException($"Missing Sakura frame texture: {path}", path);

        var texture = ResourceLoader.Load<Texture2D>(path)
            ?? throw new InvalidOperationException($"Failed to load Sakura frame texture: {path}");
        FrameTextureCache[path] = texture;
        return texture;
    }

    private static string FramePartPath(CardModel card, SakuraFramePart part)
    {
        if (SakuraActions.IsClearCard(card))
            throw new ArgumentException("Clear Cards do not use Sakura non-clear frame textures.", nameof(card));

        return Path.Join("cards", FrameDirectory(card), FramePartFileName(part)).ImagePath();
    }

    private static string PortraitFileName(CardModel card) =>
        card is DreamWand ? DreamWandPortraitFileName : DefaultPortraitFileName;

    private static string FrameDirectory(CardModel card) =>
        SakuraActions.IsPartner(card) ? "partner" : "technique";

    private static string FramePartFileName(SakuraFramePart part) =>
        part switch
        {
            SakuraFramePart.Frame => "frame.png",
            SakuraFramePart.PortraitBorder => "portrait_border.png",
            SakuraFramePart.Banner => "banner.png",
            SakuraFramePart.TypePlaque => "type_plaque.png",
            _ => throw new ArgumentOutOfRangeException(nameof(part), part, null)
        };
}

internal enum SakuraFramePart
{
    Frame,
    PortraitBorder,
    Banner,
    TypePlaque
}
