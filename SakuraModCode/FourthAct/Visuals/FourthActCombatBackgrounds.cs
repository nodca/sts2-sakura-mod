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

    public static IReadOnlyList<string> WindRooftopLayers { get; } = [WindRooftopLayerPath];

    public static BackgroundAssets CreateWindRooftop() =>
        CombatBackgroundAssetsFactory.Create(MainScenePath, WindRooftopLayers, null);
}
