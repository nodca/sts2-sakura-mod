using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Extensions;

namespace SakuraMod.SakuraModCode.Cards;

internal static class SakuraCardFrameVisuals
{
    private const string DefaultPortraitFileName = "card.png";
    private const string BigBrotherSensePortraitFileName = "big_brother_sense.png";
    private const string CerberusTrueFormPortraitFileName = "cerberus_true_form.png";
    private const string ClockCountryAlicePortraitFileName = "clock_country_alice.png";
    private const string DreamKeyGlowPortraitFileName = "dream_key_glow.png";
    private const string DreamWandPortraitFileName = "dream_wand.png";
    private const string KeroBondPortraitFileName = "kero_bond.png";
    private const string KeroSnackBreakPortraitFileName = "kero_snack_break.png";
    private const string SyaoranBondPortraitFileName = "syaoran_bond.png";
    private const string TomoyoBondPortraitFileName = "tomoyo_bond.png";
    private const string TomoyoCameraPortraitFileName = "tomoyo_camera.png";
    private const string YamazakiTallTalePortraitFileName = "yamazaki_tall_tale.png";
    private static readonly Dictionary<string, Texture2D?> PortraitTextureCache = [];
    private static readonly Dictionary<string, Texture2D?> FrameTextureCache = [];
    private static readonly CanvasItemMaterial _plainFrameMaterial = new();

    public static Material PlainFrameMaterial => _plainFrameMaterial;

    public static string BigPortraitPath(CardModel card) => PortraitFileName(card).BigCardImagePath();

    public static string PortraitPath(CardModel card) => PortraitFileName(card).CardImagePath();

    public static IEnumerable<string> RunAssetPaths(CardModel card)
    {
        if (SakuraActions.IsClearCard(card))
        {
            yield return ClearCardLayout.CardArtPath(card.GetType());
            yield break;
        }

        yield return PortraitPath(card);
        yield return BigPortraitPath(card);

        if (card.Rarity == CardRarity.Ancient)
            yield break;

        foreach (var part in Enum.GetValues<SakuraFramePart>())
            yield return FramePartPath(card, part);
    }

    public static Texture2D? PortraitTexture(CardModel card)
    {
        var path = PortraitPath(card);
        if (PortraitTextureCache.TryGetValue(path, out var cachedTexture))
        {
            if (cachedTexture is null || IsGodotInstanceUsable(cachedTexture))
                return cachedTexture;

            PortraitTextureCache.Remove(path);
        }

        var texture = ResourceLoader.Exists(path)
            ? LoadTexture(path)
            : null;
        PortraitTextureCache[path] = texture;
        return texture;
    }

    public static Texture2D? CustomFrameTexture(CardModel card) =>
        ShouldUseVanillaFrame(card)
            ? null
            : FrameTexture(card, SakuraFramePart.Frame);

    public static Material? CustomFrameMaterial(CardModel card) =>
        ShouldUseVanillaFrame(card)
            ? null
            : _plainFrameMaterial;

    public static Texture2D FrameTexture(CardModel card, SakuraFramePart part)
    {
        var path = FramePartPath(card, part);
        if (FrameTextureCache.TryGetValue(path, out var cachedTexture))
        {
            if (IsGodotInstanceUsable(cachedTexture))
                return cachedTexture ?? throw new InvalidOperationException($"Missing Sakura frame texture: {path}");

            FrameTextureCache.Remove(path);
        }

        if (!ResourceLoader.Exists(path))
            throw new FileNotFoundException($"Missing Sakura frame texture: {path}", path);

        var texture = LoadTexture(path)
            ?? throw new InvalidOperationException($"Failed to load Sakura frame texture: {path}");
        FrameTextureCache[path] = texture;
        return texture;
    }

    private static Texture2D? LoadTexture(string path) =>
        ResourceLoader.Load<Texture2D>(path, null, ResourceLoader.CacheMode.Ignore);

    private static bool IsGodotInstanceUsable(GodotObject? instance)
    {
        try
        {
            return instance is not null && GodotObject.IsInstanceValid(instance);
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    private static string FramePartPath(CardModel card, SakuraFramePart part)
    {
        if (ShouldUseVanillaFrame(card))
            throw new ArgumentException("This card uses vanilla card frame textures.", nameof(card));

        return Path.Join("cards", FrameDirectory(card), FramePartFileName(part)).ImagePath();
    }

    private static bool ShouldUseVanillaFrame(CardModel card) =>
        SakuraActions.IsClearCard(card) || card.Rarity == CardRarity.Ancient;

    private static string PortraitFileName(CardModel card) =>
        card switch
        {
            BigBrotherSense => BigBrotherSensePortraitFileName,
            CerberusTrueForm => CerberusTrueFormPortraitFileName,
            ClockCountryAlice => ClockCountryAlicePortraitFileName,
            DreamKeyGlow => DreamKeyGlowPortraitFileName,
            DreamWand => DreamWandPortraitFileName,
            KeroBond => KeroBondPortraitFileName,
            KeroSnackBreak => KeroSnackBreakPortraitFileName,
            SyaoranBond => SyaoranBondPortraitFileName,
            TomoyoBond => TomoyoBondPortraitFileName,
            TomoyoCamera => TomoyoCameraPortraitFileName,
            YamazakiTallTale => YamazakiTallTalePortraitFileName,
            _ => DefaultPortraitFileName
        };

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
