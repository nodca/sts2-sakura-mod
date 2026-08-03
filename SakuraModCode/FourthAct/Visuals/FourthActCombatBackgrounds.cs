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
    public static IReadOnlyList<string> DarkStageLayers { get; } = [DarkStageLayerPath];

    public static BackgroundAssets CreateWindRooftop() =>
        CombatBackgroundAssetsFactory.Create(MainScenePath, WindRooftopLayers, null);

    public static BackgroundAssets CreateDarkStage() =>
        CombatBackgroundAssetsFactory.Create(MainScenePath, DarkStageLayers, null);
}
