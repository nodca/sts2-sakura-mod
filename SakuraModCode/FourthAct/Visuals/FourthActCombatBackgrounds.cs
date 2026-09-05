using MegaCrit.Sts2.Core.Rooms;
using STS2RitsuLib.Scaffolding.Content;

namespace SakuraMod.SakuraModCode.FourthAct.Visuals;

public static class FourthActCombatBackgrounds
{
    public const string MainScenePath =
        "res://scenes/backgrounds/glory/glory_background.tscn";
    public const string WindRooftopLayerPath =
        "res://SakuraMod/scenes/backgrounds/fourth_act/rooftop/rooftop_base.tscn";
    public const string WindRooftopTexturePath =
        "res://SakuraMod/images/backgrounds/fourth_act/rooftop/rooftop_base.png";
    public const string WaterAquariumLayerPath =
        "res://SakuraMod/scenes/backgrounds/fourth_act/aquarium/aquarium_base.tscn";
    public const string WaterAquariumTexturePath =
        "res://SakuraMod/images/backgrounds/fourth_act/aquarium/aquarium_base.png";
    public const string FireAmusementParkLayerPath =
        "res://SakuraMod/scenes/backgrounds/fourth_act/amusement_park/amusement_park_base.tscn";
    public const string FireAmusementParkTexturePath =
        "res://SakuraMod/images/backgrounds/fourth_act/amusement_park/amusement_park_base.png";
    public const string FireTokyoTowerLayerPath =
        "res://SakuraMod/scenes/backgrounds/fourth_act/tokyo_tower/tokyo_tower_base.tscn";
    public const string FireTokyoTowerTexturePath =
        "res://SakuraMod/images/backgrounds/fourth_act/tokyo_tower/tokyo_tower_base.png";
    public const string EarthPenguinParkLayerPath =
        "res://SakuraMod/scenes/backgrounds/fourth_act/penguin_park/penguin_park_base.tscn";
    public const string EarthPenguinParkTexturePath =
        "res://SakuraMod/images/backgrounds/fourth_act/penguin_park/penguin_park_base.png";
    public const string TsukimineShrineLayerPath =
        "res://SakuraMod/scenes/backgrounds/fourth_act/tsukimine_shrine/tsukimine_shrine_base.tscn";
    public const string TsukimineShrineTexturePath =
        "res://SakuraMod/images/backgrounds/fourth_act/tsukimine_shrine/tsukimine_shrine_base.png";
    public const string DarkStageLayerPath =
        "res://SakuraMod/scenes/backgrounds/fourth_act/school_stage/dark_base.tscn";
    public const string DarkStageTexturePath =
        "res://SakuraMod/images/backgrounds/fourth_act/school_stage/dark_base.png";
    public const string EternalNightRegionMaskPath =
        "res://SakuraMod/images/backgrounds/fourth_act/school_stage/eternal_night_regions.png";
    public const string EternalNightShaderPath =
        "res://SakuraMod/shaders/fourth_act/eternal_night.gdshader";
    public const string EternalNightOverlayNodeName = "EternalNightOverlay";
    public const string EternalNightProgressParameterName = "night_progress";

    public static IReadOnlyList<string> WindRooftopLayers { get; } = [WindRooftopLayerPath];
    public static IReadOnlyList<string> WaterAquariumLayers { get; } = [WaterAquariumLayerPath];
    public static IReadOnlyList<string> FireAmusementParkLayers { get; } = [FireAmusementParkLayerPath];
    public static IReadOnlyList<string> FireTokyoTowerLayers { get; } = [FireTokyoTowerLayerPath];
    public static IReadOnlyList<string> EarthPenguinParkLayers { get; } = [EarthPenguinParkLayerPath];
    public static IReadOnlyList<string> TsukimineShrineLayers { get; } = [TsukimineShrineLayerPath];
    public static IReadOnlyList<string> DarkStageLayers { get; } = [DarkStageLayerPath];

    public static BackgroundAssets CreateWindRooftop() =>
        CombatBackgroundAssetsFactory.Create(MainScenePath, WindRooftopLayers, null);

    public static BackgroundAssets CreateWaterAquarium() =>
        CombatBackgroundAssetsFactory.Create(MainScenePath, WaterAquariumLayers, null);

    public static BackgroundAssets CreateFireAmusementPark() =>
        CombatBackgroundAssetsFactory.Create(MainScenePath, FireAmusementParkLayers, null);

    public static BackgroundAssets CreateFireTokyoTower() =>
        CombatBackgroundAssetsFactory.Create(MainScenePath, FireTokyoTowerLayers, null);

    public static BackgroundAssets CreateEarthPenguinPark() =>
        CombatBackgroundAssetsFactory.Create(MainScenePath, EarthPenguinParkLayers, null);

    public static BackgroundAssets CreateTsukimineShrine() =>
        CombatBackgroundAssetsFactory.Create(MainScenePath, TsukimineShrineLayers, null);

    public static BackgroundAssets CreateDarkStage() =>
        CombatBackgroundAssetsFactory.Create(MainScenePath, DarkStageLayers, null);
}
