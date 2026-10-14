using System.Buffers.Binary;
using SakuraMod.SakuraModCode.Events;
using SakuraMod.SakuraModCode.FourthAct.Visuals;

public sealed class TheNothingSealBackgroundSuite
{
    private const string ImageDir = "SakuraMod/images/backgrounds/events/the_nothing/";

    [Fact]
    public void NothingEncounterUsesSealPlateWithAnimatedLayers()
    {
        Assert.Equal(
            "res://SakuraMod/scenes/backgrounds/events/the_nothing/the_nothing_base.tscn",
            ClassicTheNothingEncounter.SealLayerPath);
        var layerScene = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/scenes/backgrounds/events/the_nothing/the_nothing_base.tscn"));
        Assert.Contains(StaticCombatBackgroundVisuals.TheNothingPaintingNodeName, layerScene);
        Assert.Contains("res://SakuraMod/shaders/events/the_nothing_seal.gdshader", layerScene);

        foreach (var name in new[] { "the_nothing_base", "sky_clouds", "sky_mask", "town_lights" })
        {
            Assert.Contains($"res://{ImageDir}{name}.png", layerScene);
            var texture = RegressionTestHarness.FindRepoFile($"{ImageDir}{name}.png");
            var header = File.ReadAllBytes(texture).AsSpan(0, 24);
            Assert.Equal((int)StaticCombatBackgroundVisuals.CanvasWidth, BinaryPrimitives.ReadInt32BigEndian(header[16..20]));
            Assert.Equal((int)StaticCombatBackgroundVisuals.CanvasHeight, BinaryPrimitives.ReadInt32BigEndian(header[20..24]));
            var import = File.ReadAllText($"{texture}.import");
            Assert.Contains($"source_file=\"res://{ImageDir}{name}.png\"", import);
            Assert.Contains("mipmaps/generate=false", import);
        }

        var encounterSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Events/Models/ClassicTheNothingEncounter.cs"));
        Assert.Contains("UseProgrammaticCombatBackground => true", encounterSource);
        Assert.Contains("[SealLayerPath]", encounterSource);

        var patchSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Visuals/StaticCombatBackgroundPatch.cs"));
        Assert.Contains("TheNothingPaintingNodeName", patchSource);
    }
}
