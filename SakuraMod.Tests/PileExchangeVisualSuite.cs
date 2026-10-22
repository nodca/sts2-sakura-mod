using System.Text.RegularExpressions;
using SakuraMod.SakuraModCode.Cards;

namespace SakuraMod.Tests;

public sealed class PileExchangeVisualSuite
{
    private const string PlateShaderPath = "SakuraMod/shaders/card_vfx/reversal_plate.gdshader";

    private static string Source(string path) => File.ReadAllText(RegressionTestHarness.FindRepoFile(path));

    [Fact]
    public void ReversalDeclaresItsPlateAndExchangeNeedsNoResource()
    {
        var reversal = SakuraCardVfxAssets.RunAssetPaths(new Reversal()).ToArray();
        RegressionTestHarness.Require(
            ReversalPlateMotif.AssetPaths.All(reversal.Contains)
            && reversal.Length == reversal.Distinct(StringComparer.Ordinal).Count(),
            "Expected Reversal to declare its plate shader once.");
        var exchange = SakuraCardVfxAssets.RunAssetPaths(new Exchange()).ToArray();
        RegressionTestHarness.Require(
            !CelRibbonStrip.AssetPaths.Any(exchange.Contains) && !ReversalPlateMotif.AssetPaths.Any(exchange.Contains),
            "Expected Exchange's flowers and vine to need neither the Loop's ribbon nor Reversal's plate.");
    }

    [Fact]
    public void EachCardShowsItsOwnMotif()
    {
        RegressionTestHarness.Require(
            Source("SakuraModCode/Cards/Transparent/Reversal.cs").Contains("PileExchangeMotif.Reversal", StringComparison.Ordinal)
            && !Source("SakuraModCode/Cards/Transparent/Exchange.cs").Contains("PileExchangeMotif.Reversal", StringComparison.Ordinal),
            "Expected Reversal to ask for its diagonal plate and Exchange to keep the flower vine.");
        var vfx = Source("SakuraModCode/Cards/PileExchangeVfx.cs");
        RegressionTestHarness.Require(
            vfx.Contains("ExchangeVineMotif.Create(", StringComparison.Ordinal)
            && vfx.Contains("ReversalPlateMotif.Create(", StringComparison.Ordinal)
            && !vfx.Contains("CelRibbon", StringComparison.Ordinal),
            "Expected the exchange to draw each card's own motif, not the Loop's ribbon.");
    }

    [Fact]
    public void ExchangeFliesTheGamesOwnSoulsAndFailsOpen()
    {
        var vfx = Source("SakuraModCode/Cards/PileExchangeVfx.cs");
        RegressionTestHarness.Require(
            vfx.Contains("NCardFlyShuffleVfx.Create(", StringComparison.Ordinal)
            && vfx.Contains("owner.Character.TrailPath", StringComparison.Ordinal)
            && vfx.Contains("LocalContext.IsMe(owner)", StringComparison.Ordinal)
            && vfx.Contains("TestMode.IsOn", StringComparison.Ordinal)
            && vfx.Contains("SakuraModConfig.IsCardVfxEnabled()", StringComparison.Ordinal)
            && vfx.Contains("ReversalPlateMotif.TryGetShader()", StringComparison.Ordinal)
            && !vfx.Contains("ResourceLoader.Load", StringComparison.Ordinal),
            "Expected the exchange to fly native shuffle souls, stay local, follow the card-VFX setting, and read its shader from the run cache.");
    }

    [Fact]
    public void PlateShaderUsesTheSharedIncludeAndOwnsNoClock()
    {
        var shader = Source(PlateShaderPath);
        RegressionTestHarness.Require(
            shader.Contains("#include \"res://SakuraMod/shaders/card_vfx/cel_vfx.gdshaderinc\"", StringComparison.Ordinal)
            && !shader.Contains("TIME", StringComparison.Ordinal)
            && !shader.Contains("SCREEN_", StringComparison.Ordinal),
            "Expected the Reversal plate to use the shared cel include and take time only from its owner.");
        RegressionTestHarness.Require(
            File.Exists(RegressionTestHarness.FindRepoFile(PlateShaderPath + ".uid")),
            "Expected the plate shader's .uid in the repository.");
    }

    [Fact]
    public void PlatesInitialiseEveryUniformAndLetClicksThrough()
    {
        var motif = Source("SakuraModCode/Cards/Visuals/Transparent/ReversalPlateMotif.cs");
        foreach (var uniform in Regex.Matches(Source(PlateShaderPath), @"^uniform \w+ (\w+)", RegexOptions.Multiline).Select(match => match.Groups[1].Value))
        {
            RegressionTestHarness.Require(
                motif.Contains($"SetShaderParameter(\"{uniform}\"", StringComparison.Ordinal),
                $"Expected ReversalPlateMotif to initialise the {uniform} uniform.");
        }
        RegressionTestHarness.Require(
            motif.Contains("MouseFilter = Control.MouseFilterEnum.Ignore", StringComparison.Ordinal),
            "Expected the plates beside the piles not to swallow clicks on the HUD.");
    }

    [Fact]
    public void MotifsFadeOutWithinTheExchange()
    {
        Assert.Equal(1f, PileExchangeVfx.FadeAt(0f));
        Assert.Equal(1f, PileExchangeVfx.FadeAt(PileExchangeVfx.FadeFrom));
        Assert.Equal(0f, PileExchangeVfx.FadeAt(PileExchangeVfx.Duration));
        Assert.Equal(0.5f, PileExchangeVfx.Stepped(0.6f));
        Assert.Equal(1f, PileExchangeVfx.Stepped(1f));
    }
}
