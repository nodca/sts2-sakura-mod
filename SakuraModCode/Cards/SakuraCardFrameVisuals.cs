using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Extensions;

namespace SakuraMod.SakuraModCode.Cards;

internal static class SakuraCardFrameVisuals
{
    private const string DefaultPortraitFileName = "card.png";
    private const string AliceReadingPortraitFileName = "alice_reading.png";
    private const string ArchivePortraitFileName = "archive.png";
    private const string BigBrotherSensePortraitFileName = "big_brother_sense.png";
    private const string CardBookSortingPortraitFileName = "card_book_sorting.png";
    private const string CerberusTrueFormPortraitFileName = "cerberus_true_form.png";
    private const string ClockCountryAlicePortraitFileName = "clock_country_alice.png";
    private const string DWatchPortraitFileName = "d_watch.png";
    private const string DreamCompassPortraitFileName = "dream_compass.png";
    private const string DreamKeyGlowPortraitFileName = "dream_key_glow.png";
    private const string DreamWandPortraitFileName = "dream_wand.png";
    private const string FourSymbolsPortraitFileName = "four_symbols.png";
    private const string FujitakaNotePortraitFileName = "fujitaka_note.png";
    private const string GrowingMagicPortraitFileName = "growing_magic.png";
    private const string KeroBondPortraitFileName = "kero_bond.png";
    private const string KeroSnackBreakPortraitFileName = "kero_snack_break.png";
    private const string MagicSurgePortraitFileName = "magic_surge.png";
    private const string RollerbladeDashPortraitFileName = "rollerblade_dash.png";
    private const string SealedBookPortraitFileName = "sealed_book.png";
    private const string StabilizePortraitFileName = "stabilize.png";
    private const string SyaoranBondPortraitFileName = "syaoran_bond.png";
    private const string ThunderEmperorSummonPortraitFileName = "thunder_emperor_summon.png";
    private const string TomoyoBondPortraitFileName = "tomoyo_bond.png";
    private const string TomoyoCameraPortraitFileName = "tomoyo_camera.png";
    private const string TomoyoCostumePortraitFileName = "tomoyo_costume.png";
    private const string YamazakiTallTalePortraitFileName = "yamazaki_tall_tale.png";
    private const string YueTrueFormPortraitFileName = "yue_true_form.png";
    private static readonly Dictionary<string, Texture2D?> PortraitTextureCache = [];
    private static readonly Dictionary<string, Texture2D?> FrameTextureCache = [];
    private static readonly CanvasItemMaterial _plainFrameMaterial = new();

    public static Material PlainFrameMaterial => _plainFrameMaterial;

    public static string BigPortraitPath(CardModel card) => PortraitFileName(card).BigCardImagePath();

    public static string PortraitPath(CardModel card) => PortraitFileName(card).CardImagePath();

    public static IEnumerable<string> RunAssetPaths(CardModel card)
    {
        if (SakuraCardCatalog.IsTransparentCard(card))
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
        ResourceLoader.Load<Texture2D>(path, null, ResourceLoader.CacheMode.Reuse);

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
        SakuraCardCatalog.IsTransparentCard(card) || card.Rarity == CardRarity.Ancient;

    private static string PortraitFileName(CardModel card) =>
        card switch
        {
            AliceReading => AliceReadingPortraitFileName,
            Archive => ArchivePortraitFileName,
            BigBrotherSense => BigBrotherSensePortraitFileName,
            CardBookSorting => CardBookSortingPortraitFileName,
            CerberusTrueForm => CerberusTrueFormPortraitFileName,
            ClockCountryAlice => ClockCountryAlicePortraitFileName,
            DWatch => DWatchPortraitFileName,
            DreamCompass => DreamCompassPortraitFileName,
            DreamKeyGlow => DreamKeyGlowPortraitFileName,
            DreamWand => DreamWandPortraitFileName,
            FourSymbols => FourSymbolsPortraitFileName,
            FujitakaNote => FujitakaNotePortraitFileName,
            GrowingMagic => GrowingMagicPortraitFileName,
            KeroBond => KeroBondPortraitFileName,
            KeroSnackBreak => KeroSnackBreakPortraitFileName,
            MagicSurge => MagicSurgePortraitFileName,
            RollerbladeDash => RollerbladeDashPortraitFileName,
            SealedBook => SealedBookPortraitFileName,
            Stabilize => StabilizePortraitFileName,
            SyaoranBond => SyaoranBondPortraitFileName,
            ThunderEmperorSummon => ThunderEmperorSummonPortraitFileName,
            TomoyoBond => TomoyoBondPortraitFileName,
            TomoyoCamera => TomoyoCameraPortraitFileName,
            TomoyoCostume => TomoyoCostumePortraitFileName,
            YamazakiTallTale => YamazakiTallTalePortraitFileName,
            YueTrueForm => YueTrueFormPortraitFileName,
            _ => DefaultPortraitFileName
        };

    private static string FrameDirectory(CardModel card) =>
        SakuraCardCatalog.IsPartnerCard(card) ? "partner" : "technique";

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
