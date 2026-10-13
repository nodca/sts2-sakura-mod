using System.Text.Json;
using System.Text.RegularExpressions;
using MegaCrit.Sts2.Core.Entities.Cards;
using SakuraMod.SakuraModCode.FourthAct.Dark.Cards;
using SakuraMod.SakuraModCode.FourthAct.Earth.CardState;
using SakuraMod.SakuraModCode.FourthAct.Water.CardState;
using SakuraMod.SakuraModCode.FourthAct.Wind.CardState;

public sealed class FourthActCardAfflictionSuite
{
    public static TheoryData<string, string> Overlays => new()
    {
        { SleepingAffliction.OverlayScenePath, "SleepingAffliction" },
        { BuriedAffliction.OverlayScenePath, "BuriedAffliction" },
        { FrozenAffliction.OverlayScenePath, "FrozenAffliction" },
    };

    [Theory]
    [MemberData(nameof(Overlays))]
    public void OverlayMirrorsTheNativeCenteredFrame(string scenePath, string rootName)
    {
        var scene = ReadResource(scenePath);
        Assert.StartsWith("[gd_scene", scene);
        Assert.Contains($"[node name=\"{rootName}\" type=\"Control\"]", scene);
        Assert.Contains("[node name=\"Frame\" type=\"Control\" parent=\".\"]", scene);
        Assert.Contains("offset_left = -150.0\noffset_top = -211.0\noffset_right = 150.0\noffset_bottom = 211.0", scene);
        Assert.Contains("clip_contents = true", scene);
        Assert.Contains("res://SakuraMod/shaders/card_vfx/affliction_overlay.gdshader", scene);
        // Intro animates the reveal parameter; a shared material would restart every card's intro.
        Assert.Contains("resource_local_to_scene = true", scene);
        Assert.Contains("autoplay = \"intro\"", scene);
        Assert.Contains("autoplay = \"idle\"", scene);

        var controls = Regex.Matches(scene, "\\[node name=\"[^\"]+\" type=\"(Control|TextureRect|ColorRect)\"[^\\]]*\\]([^\\[]*)");
        Assert.All(controls, static node => Assert.Contains("mouse_filter = 2", node.Groups[2].Value));
        foreach (Match texture in Regex.Matches(scene, "path=\"(res://SakuraMod/images/[^\"]+\\.png)\""))
        {
            var file = texture.Groups[1].Value["res://".Length..];
            Assert.True(File.Exists(RegressionTestHarness.FindRepoFile(file)), file);
            Assert.True(File.Exists(RegressionTestHarness.FindRepoFile(file + ".import")), file + ".import");
        }
    }

    [Fact]
    public void FrozenProjectionNeverGatesPlay()
    {
        var frozen = new FrozenAffliction();
        var card = new MicroLight();
        Assert.True(frozen.ShouldPlay(card, AutoPlayType.None));
        Assert.True(frozen.ShouldPlay(card, AutoPlayType.Default));

        var power = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Water/Powers/WaterCombatPowers.cs"));
        Assert.Contains("public override async Task AfterApplied", power);
        Assert.Contains("public override async Task AfterCardChangedPiles", power);
        Assert.Contains("public override Task AfterRemoved", power);
        Assert.Contains("if (card.Affliction is null)\n            await CardCmd.Afflict<FrozenAffliction>(card, 1);", power);
    }

    [Fact]
    public void SakuraLayoutFitsEveryModOverlay()
    {
        var layout = File.ReadAllText(RegressionTestHarness.FindRepoFile("SakuraModCode/Cards/SakuraAfflictionVisualLayout.cs"));
        Assert.Contains("affliction is SleepingAffliction or BuriedAffliction or FrozenAffliction", layout);
        Assert.Contains("ledger.Borrow(frame, BoxProperties);", layout);
        Assert.Contains("overlay.Position = nativeOverlayPosition + targetSize * 0.5f;", layout);
    }

    [Fact]
    public void AfflictionTextExistsAndConfinementIsGone()
    {
        foreach (var locale in new[] { "zhs", "eng" })
        {
            var text = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(
                RegressionTestHarness.FindRepoFile($"SakuraMod/localization/{locale}/afflictions.json")))!;
            foreach (var name in new[] { "SLEEPING", "BURIED", "FROZEN" })
            {
                Assert.Contains($"SAKURA_MOD_AFFLICTION_{name}_AFFLICTION.title", text.Keys);
                Assert.Contains($"SAKURA_MOD_AFFLICTION_{name}_AFFLICTION.description", text.Keys);
            }
            Assert.DoesNotContain(text.Keys, static key => key.Contains("CONFINEMENT", StringComparison.Ordinal));
        }
        Assert.False(File.Exists(Path.Combine(
            Path.GetDirectoryName(RegressionTestHarness.FindRepoFile("SakuraMod/scenes/cards/overlays/sleeping_affliction.tscn"))!,
            "dark_confinement.tscn")));
    }

    private static string ReadResource(string resPath) =>
        File.ReadAllText(RegressionTestHarness.FindRepoFile(resPath["res://".Length..]));
}
