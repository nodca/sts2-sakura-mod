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
        var earth = SakuraElementSet.Earth;
        var all = SakuraElementSet.All;

        RegressionTestHarness.Require(
            all == (SakuraElementSet.Wind
                | SakuraElementSet.Water
                | SakuraElementSet.Fire
                | SakuraElementSet.Earth),
            "Expected the All marker to cover exactly the four elements.");

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
        Assert.Equal(
            SakuraElementSet.Water,
            SakuraElementState.LocksFromSovereignty(false, false, true));
        Assert.Equal(
            SakuraElementSet.Fire,
            SakuraElementState.LocksFromSovereignty(false, false, false, true));
        Assert.Equal(
            SakuraElementSet.Fire | SakuraElementSet.Earth,
            SakuraElementState.LocksFromSovereignty(false, false, false, false, true));
    }

    [Fact]
    public void HudSceneKeepsItsAssetAndLocalizationContract()
    {
        var scene = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/scenes/combat/sakura_element_state_hud.tscn"));

        RegressionTestHarness.Require(
            scene.Contains("mouse_filter", StringComparison.Ordinal)
            && !scene.Contains("z_index", StringComparison.Ordinal),
            "Expected the element state HUD root to keep an explicit mouse filter without manual z-index layering.");

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

        // Sakura-scoped, projecting authoritative element state.
        RegressionTestHarness.Require(
            source.Contains("IsKinomotoSakura", StringComparison.Ordinal)
            && source.Contains("SakuraElementState.ReadActive", StringComparison.Ordinal),
            "Expected the HUD to be Sakura-scoped and project authoritative element state.");

        // The mount keeps its named offset knobs instead of inline numbers.
        RegressionTestHarness.Require(
            source.Contains("MountHorizontalOffset", StringComparison.Ordinal)
            && source.Contains("MountGap", StringComparison.Ordinal),
            "Expected the HUD mount to place itself through its named offset knobs.");

        // Hover routing listens to the root's GUI input without swallowing events.
        RegressionTestHarness.Require(
            source.Contains("GuiInput", StringComparison.Ordinal)
            && !source.Contains("Root.AcceptEvent()", StringComparison.Ordinal),
            "Expected hover routing to listen without swallowing GUI events.");

        // Facet hover reuses the native localized hover-tip system.
        RegressionTestHarness.Require(
            source.Contains("SakuraElementFacetProjection.FromOffset", StringComparison.Ordinal)
            && source.Contains("NHoverTipSet.CreateAndShow", StringComparison.Ordinal)
            && source.Contains("NHoverTipSet.Remove", StringComparison.Ordinal)
            && source.Contains("ElementStateTipKey", StringComparison.Ordinal),
            "Expected facet hover to reuse native localized hover tips.");

        RegressionTestHarness.Require(
            source.Contains("_combatState.CreaturesChanged += OnCreaturesChanged", StringComparison.Ordinal)
            && source.Contains("_combatState.CreaturesChanged -= OnCreaturesChanged", StringComparison.Ordinal)
            && source.Contains("enemy.PowerApplied += OnEnemyPowerApplied", StringComparison.Ordinal)
            && source.Contains("enemy.PowerApplied -= OnEnemyPowerApplied", StringComparison.Ordinal)
            && source.Contains("SakuraElementState.ReadLocks", StringComparison.Ordinal)
            && !source.Contains("_Process", StringComparison.Ordinal),
            "Expected Sovereignty locks to follow enemy and combat lifecycle events without polling or duplicate state.");
    }
}
