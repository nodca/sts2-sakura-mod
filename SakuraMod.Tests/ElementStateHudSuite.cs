using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;
using System.Text.Json;

public sealed class ElementStateHudSuite
{
    [Fact]
    public void ElementPowersStayOutOfNativePowerBar()
    {
        RegressionTestHarness.Require(
            !new ClassicEarthyPower().IsVisible
            && !new ClassicFireyPower().IsVisible
            && !new ClassicWateryPower().IsVisible
            && !new ClassicWindyPower().IsVisible,
            "Expected the four active element-state powers to stay out of the native power bar.");

        RegressionTestHarness.Require(
            !new ClassicEarthyPermanentPower().IsVisible
            && !new ClassicFireyPermanentPower().IsVisible
            && !new ClassicWateryPermanentPower().IsVisible
            && !new ClassicWindyPermanentPower().IsVisible,
            "Expected permanent element powers to share the HUD-only display contract.");
    }

    [Fact]
    public void CursorOffsetsMapToNonOverlappingFacets()
    {
        RegressionTestHarness.Require(
            SakuraElementFacetProjection.FromOffset(0f, -40f, 50f) == SakuraElement.Fire
            && SakuraElementFacetProjection.FromOffset(40f, 0f, 50f) == SakuraElement.Wind
            && SakuraElementFacetProjection.FromOffset(0f, 40f, 50f) == SakuraElement.Earth
            && SakuraElementFacetProjection.FromOffset(-40f, 0f, 50f) == SakuraElement.Water,
            "Expected cursor geometry to map Fire-top, Wind-right, Earth-bottom, and Water-left sectors.");

        RegressionTestHarness.Require(
            SakuraElementFacetProjection.FromOffset(30f, -10f, 50f) == SakuraElement.Wind
            && SakuraElementFacetProjection.FromOffset(-10f, 30f, 50f) == SakuraElement.Earth
            && SakuraElementFacetProjection.FromOffset(49f, 49f, 50f) is null,
            "Expected dominant-axis geometry to keep triangular hover regions non-overlapping.");
    }

    [Fact]
    public void ActiveElementProjectionTracksStateAndNewTransitions()
    {
        var none = SakuraElementSet.None;
        var earth = SakuraElementSet.Earth;
        var fireAndWater = SakuraElementSet.Fire | SakuraElementSet.Water;
        var all = SakuraElementSet.All;

        RegressionTestHarness.Require(
            none == SakuraElementSet.None
            && earth == SakuraElementSet.Earth
            && fireAndWater == (SakuraElementSet.Fire | SakuraElementSet.Water)
            && all == (SakuraElementSet.Wind
                | SakuraElementSet.Water
                | SakuraElementSet.Fire
                | SakuraElementSet.Earth),
            "Expected the element projection to represent none, one, multiple, and all markers independently.");

        RegressionTestHarness.Require(
            SakuraElementState.NewlyActive(earth, all)
                == (SakuraElementSet.Fire
                    | SakuraElementSet.Water
                    | SakuraElementSet.Wind)
            && SakuraElementState.NewlyActive(all, earth) == SakuraElementSet.None,
            "Expected only inactive-to-active element transitions to request HUD pulses.");
    }

    [Fact]
    public void SovereigntyLockProjectionIsDerivedFromEnemySources()
    {
        Assert.Equal(SakuraElementSet.None, SakuraElementState.LocksFromSovereignty(false, false));
        Assert.Equal(SakuraElementSet.Wind, SakuraElementState.LocksFromSovereignty(true, false));
        Assert.Equal(
            SakuraElementSet.Wind | SakuraElementSet.Water,
            SakuraElementState.LocksFromSovereignty(false, true));
        Assert.Equal(
            SakuraElementSet.Wind | SakuraElementSet.Water,
            SakuraElementState.LocksFromSovereignty(true, true));
    }

    [Fact]
    public void HudSceneKeepsItsAssetAndLocalizationContract()
    {
        var scene = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/scenes/combat/sakura_element_state_hud.tscn"));

        RegressionTestHarness.Require(
            scene.Contains("offset_right = 128.0", StringComparison.Ordinal)
            && scene.Contains("offset_bottom = 128.0", StringComparison.Ordinal)
            && scene.Contains("mouse_filter = 0", StringComparison.Ordinal)
            && !scene.Contains("z_index", StringComparison.Ordinal),
            "Expected the element state HUD to keep its readable 128x128 root bounds.");

        var wind = scene.IndexOf("[node name=\"WindFacet\"", StringComparison.Ordinal);
        var fire = scene.IndexOf("[node name=\"FireFacet\"", StringComparison.Ordinal);
        var earth = scene.IndexOf("[node name=\"EarthFacet\"", StringComparison.Ordinal);
        var water = scene.IndexOf("[node name=\"WaterFacet\"", StringComparison.Ordinal);
        var outline = scene.IndexOf("[node name=\"Outline\"", StringComparison.Ordinal);
        RegressionTestHarness.Require(
            wind >= 0 && fire > wind && earth > fire && water > earth && outline > water,
            "Expected stable Wind-top, Fire-right, Earth-bottom, and Water-left facets beneath one shared outline.");

        foreach (var lockName in new[] { "WindLock", "FireLock", "EarthLock", "WaterLock" })
        {
            RegressionTestHarness.Require(
                scene.Contains($"[node name=\"{lockName}\"", StringComparison.Ordinal)
                && scene.Contains($"[node name=\"Clasp\" type=\"ColorRect\" parent=\"{lockName}\"]", StringComparison.Ordinal),
                $"Expected the element compass to include a bounded chain-and-clasp overlay for {lockName}.");
        }

        foreach (var asset in new[] { "wind_facet", "fire_facet", "earth_facet", "water_facet", "element_outline" })
        {
            RegressionTestHarness.Require(
                scene.Contains($"res://SakuraMod/images/charui/element_state_hud/{asset}.png", StringComparison.Ordinal),
                $"Expected the element state HUD to use its custom {asset} asset.");
            RegressionTestHarness.FindRepoFile($"SakuraMod/images/charui/element_state_hud/{asset}.png");
            RegressionTestHarness.FindRepoFile($"SakuraMod/images/charui/element_state_hud/{asset}.png.import");
        }

        string[] hoverTipKeys =
        [
            "SAKURAMOD-WINDY_STATE",
            "SAKURAMOD-FIREY_STATE",
            "SAKURAMOD-EARTHY_STATE",
            "SAKURAMOD-WATERY_STATE",
        ];
        foreach (var locale in new[] { "eng", "zhs" })
        {
            var tips = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(
                RegressionTestHarness.FindRepoFile($"SakuraMod/localization/{locale}/static_hover_tips.json")))
                ?? throw new InvalidOperationException($"Could not parse {locale} static hover tips.");
            foreach (var key in hoverTipKeys)
            {
                RegressionTestHarness.Require(
                    tips.ContainsKey($"{key}.title") && tips.ContainsKey($"{key}.description"),
                    $"Expected {locale} hover-tip localization for {key}.");
            }
        }

        RegressionTestHarness.Require(
            !scene.Contains("Needle", StringComparison.Ordinal)
            && !scene.Contains("Count", StringComparison.Ordinal)
            && !scene.Contains("CenterSeal", StringComparison.Ordinal)
            && !scene.Contains("Glow", StringComparison.Ordinal)
            && !scene.Contains("images/powers/", StringComparison.Ordinal),
            "Expected the emblem to omit the retired needle, counters, center ring, glow panels, and reused power icons.");
    }

    [Fact]
    public void HudRuntimeUsesAuthoritativeStateAndNativeHoverTips()
    {
        var source = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraElementStateHud.cs"));
        RegressionTestHarness.Require(
            source.Contains("SakuraStarterCompatibility.IsKinomotoSakura(player)", StringComparison.Ordinal)
            && source.Contains("SakuraElementState.ReadActive(_player)", StringComparison.Ordinal)
            && source.Contains("private const float MountHorizontalOffset = 8f", StringComparison.Ordinal)
            && source.Contains("private const float MountGap = 12f", StringComparison.Ordinal)
            && source.Contains("+ MountHorizontalOffset", StringComparison.Ordinal)
            && source.Contains("-root.Size.Y - MountGap", StringComparison.Ordinal)
            && source.Contains("Root.GuiInput += OnGuiInput", StringComparison.Ordinal)
            && !source.Contains("Root.AcceptEvent()", StringComparison.Ordinal)
            && source.Contains("SakuraElementFacetProjection.FromOffset", StringComparison.Ordinal)
            && source.Contains("NHoverTipSet.CreateAndShow", StringComparison.Ordinal)
            && source.Contains("NHoverTipSet.Remove", StringComparison.Ordinal)
            && source.Contains("ElementStateTipKey", StringComparison.Ordinal),
            "Expected the HUD to be Sakura-scoped, project authoritative element state, and reuse native localized hover tips.");

        RegressionTestHarness.Require(
            source.Contains("_combatState.CreaturesChanged += OnCreaturesChanged", StringComparison.Ordinal)
            && source.Contains("_combatState.CreaturesChanged -= OnCreaturesChanged", StringComparison.Ordinal)
            && source.Contains("enemy.PowerApplied += OnEnemyPowerApplied", StringComparison.Ordinal)
            && source.Contains("enemy.PowerApplied -= OnEnemyPowerApplied", StringComparison.Ordinal)
            && source.Contains("SakuraElementState.ReadLocks(_combatState)", StringComparison.Ordinal)
            && !source.Contains("_Process", StringComparison.Ordinal),
            "Expected Sovereignty locks to follow enemy and combat lifecycle events without polling or duplicate state.");
    }
}
