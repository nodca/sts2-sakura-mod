using MegaCrit.Sts2.Core.Models;

namespace SakuraMod.SakuraModCode.Cards;

internal static class SakuraCardVfxAssets
{
    private static readonly IReadOnlyList<string> HailPaths =
        [.. HailIceShardVfx.AssetPaths, .. CelVfxSession.SharedAssetPaths];
    private static readonly IReadOnlyList<string> BlazePaths =
        [.. BlazeFireColumnVfx.AssetPaths, .. CelVfxSession.SharedAssetPaths];
    private static readonly IReadOnlyList<string> SwordPaths =
        [.. SakuraSwordBladeVfx.AssetPaths, .. CelVfxSession.SharedAssetPaths];
    private static readonly IReadOnlyList<string> GalePaths =
        [.. GaleWindBladeVfx.AssetPaths, .. CelVfxSession.SharedAssetPaths];
    private static readonly IReadOnlyList<string> CloudRainPaths =
        [.. CloudRainWeatherVfx.AssetPaths, .. CelVfxSession.SharedAssetPaths];

    public static IEnumerable<string> RunAssetPaths(CardModel card) => card switch
    {
        Aqua => AquaWaterSphereVfx.AssetPaths,
        Hail => HailPaths,
        Blaze => BlazePaths,
        ClowSword or SakuraSword or Blade => SwordPaths,
        Gale => GalePaths,
        ClowCloud or SakuraCloud or ClowRain or SakuraRain => CloudRainPaths,
        SpellTurn => SpellTurnTransformationVfx.AssetPaths,
        _ => []
    };
}
