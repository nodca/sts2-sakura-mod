using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Character;

namespace SakuraMod.SakuraModCode.Cards;

internal static class SakuraCardVfxAssets
{
    private static readonly IReadOnlyList<string> ArrowPaths =
        [.. ArrowBowProjectileVfx.AssetPaths, .. CelVfxSession.SharedAssetPaths];
    private static readonly IReadOnlyList<string> AquaPaths =
        [.. AquaPhoenixVfx.AssetPaths, .. CelVfxSession.SharedAssetPaths];
    private static readonly IReadOnlyList<string> HailPaths =
        [.. HailIceShardVfx.AssetPaths, .. FreezeShellVisual.AssetPaths, .. CelVfxSession.SharedAssetPaths];
    private static readonly IReadOnlyList<string> TimePaths =
        [.. TimeStopVfx.AssetPaths, .. CelVfxSession.SharedAssetPaths];
    private static readonly IReadOnlyList<string> BlazePaths =
        [.. BlazePhoenixVfx.AssetPaths, .. CelVfxSession.SharedAssetPaths];
    private static readonly IReadOnlyList<string> SwordPaths =
        [.. SakuraSwordBladeVfx.AssetPaths, .. CelVfxSession.SharedAssetPaths];
    private static readonly IReadOnlyList<string> GalePaths =
        [.. GaleWindBladeVfx.AssetPaths, .. CelVfxSession.SharedAssetPaths];
    private static readonly IReadOnlyList<string> CloudRainPaths =
        [.. CloudRainWeatherVfx.AssetPaths, .. CelVfxSession.SharedAssetPaths];
    private static readonly IReadOnlyList<string> SnowPaths =
        [.. SnowBlizzardVfx.AssetPaths, .. FreezeShellVisual.AssetPaths, .. CelVfxSession.SharedAssetPaths];
    private static readonly IReadOnlyList<string> FreezePaths =
        [.. FreezeCageVfx.AssetPaths, .. FreezeShellVisual.AssetPaths, .. CelVfxSession.SharedAssetPaths];
    // The line burst stays declared: it is Song's hit feedback whenever the staff is not drawn.
    private static readonly IReadOnlyList<string> SongPaths =
        [.. SongStaffVfx.AssetPaths, .. CelVfxSession.SharedAssetPaths, .. SakuraNativeHitFx.LineBurstAssetPaths];

    public static IEnumerable<string> RunAssetPaths(CardModel card)
    {
        var effectPaths = CardEffectPaths(card);
        return SakuraCardCatalog.TryGetMetadata(card, out var metadata) && metadata.Era.HasValue
            ? effectPaths.Concat(SakuraMagicCirclePresenter.AssetPaths).Distinct(StringComparer.Ordinal)
            : effectPaths;
    }

    private static IEnumerable<string> CardEffectPaths(CardModel card) => card switch
    {
        ClowArrow or SakuraArrow => ArrowPaths,
        Aqua => AquaPaths,
        Hail => HailPaths,
        Siege => SiegeEnclosureVfx.AssetPaths,
        Blaze => BlazePaths,
        Time => TimePaths,
        ClowSword or SakuraSword or Blade => SwordPaths,
        Gale => GalePaths,
        ClowCloud or SakuraCloud or ClowRain or SakuraRain => CloudRainPaths,
        ClowSnow or SakuraSnow => SnowPaths,
        ClowFreeze or SakuraFreeze => FreezePaths,
        SpellTurn => SpellTurnTransformationVfx.AssetPaths,
        ClowSong or SakuraSong => SongPaths,
        ClowSilent or SakuraSilent => SilentQuietVisual.AssetPaths,
        Gravitation => GravitationHoldVisual.AssetPaths,
        Transfer => TransferVfx.AssetPaths,
        SpellHuoShen or SpellLeiDi or SpellShuiLong or SpellFengHua => SpellTalismanVfx.RunAssetPaths(card),
        _ => []
    };
}
