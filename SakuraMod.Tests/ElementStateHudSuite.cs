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
            SakuraElementFacetProjection.FromOffset(0f, -40f, 50f) == SakuraElement.Water
            && SakuraElementFacetProjection.FromOffset(40f, 0f, 50f) == SakuraElement.Fire
            && SakuraElementFacetProjection.FromOffset(0f, 40f, 50f) == SakuraElement.Wind
            && SakuraElementFacetProjection.FromOffset(-40f, 0f, 50f) == SakuraElement.Earth,
            "Expected the Lasin-board layout: Water top, Fire right, Wind bottom, Earth left.");

        RegressionTestHarness.Require(
            SakuraElementFacetProjection.FromOffset(30f, -10f, 50f) == SakuraElement.Fire
            && SakuraElementFacetProjection.FromOffset(-10f, 30f, 50f) == SakuraElement.Wind
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
            scene.Contains("offset_right = 208.0", StringComparison.Ordinal)
            && scene.Contains("mouse_filter = 0", StringComparison.Ordinal)
            && !scene.Contains("z_index", StringComparison.Ordinal),
            "Expected a 208 px root (176 px board plus glow margin) that stops the pointer without manual z-index layering.");

        var board = scene.IndexOf("[node name=\"Board\"", StringComparison.Ordinal);
        var dial = scene.IndexOf("[node name=\"Dial\"", StringComparison.Ordinal);
        var water = scene.IndexOf("[node name=\"WaterMedallion\"", StringComparison.Ordinal);
        var fire = scene.IndexOf("[node name=\"FireMedallion\"", StringComparison.Ordinal);
        var wind = scene.IndexOf("[node name=\"WindMedallion\"", StringComparison.Ordinal);
        var earth = scene.IndexOf("[node name=\"EarthMedallion\"", StringComparison.Ordinal);
        RegressionTestHarness.Require(
            board >= 0 && dial > board && water > dial && fire > water && wind > fire && earth > wind,
            "Expected the board, then the dial, then the four medallions drawn above them.");

        foreach (var medallion in new[] { "WaterMedallion", "FireMedallion", "WindMedallion", "EarthMedallion" })
        {
            foreach (var (layer, type) in new[]
                     {
                         ("Glow", "TextureRect"), ("Plate", "TextureRect"), ("Lit", "TextureRect"),
                         ("PermanentRing", "TextureRect"), ("Crack", "TextureRect"), ("Turns", "Label"),
                     })
            {
                RegressionTestHarness.Require(
                    scene.Contains($"[node name=\"{layer}\" type=\"{type}\" parent=\"{medallion}\"]", StringComparison.Ordinal),
                    $"Expected {medallion} to carry its {layer} layer.");
            }
        }

        var assets = new List<string> { "board", "board_sheen_mask", "dial", "medallion_permanent_ring", "medallion_crack" };
        foreach (var element in new[] { "water", "fire", "wind", "earth" })
            assets.AddRange([$"{element}_plate", $"{element}_lit", $"{element}_glow"]);
        foreach (var asset in assets)
        {
            RegressionTestHarness.Require(
                scene.Contains($"res://SakuraMod/images/charui/element_state_hud/{asset}.png", StringComparison.Ordinal),
                $"Expected the element state HUD to use its {asset} layer.");
            RegressionTestHarness.FindRepoFile($"SakuraMod/images/charui/element_state_hud/{asset}.png");
            RegressionTestHarness.FindRepoFile($"SakuraMod/images/charui/element_state_hud/{asset}.png.import");
        }

        RegressionTestHarness.Require(
            !scene.Contains("_facet.png", StringComparison.Ordinal)
            && !scene.Contains("element_outline", StringComparison.Ordinal)
            && !scene.Contains("Needle", StringComparison.Ordinal)
            && !scene.Contains("images/powers/", StringComparison.Ordinal),
            "Expected the retired four-sector seal assets, a compass needle, and reused power icons to stay out.");

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

        // Turns and permanence come from the element powers, never from HUD-owned counters.
        RegressionTestHarness.Require(
            source.Contains("SakuraElementState.ReadTurns", StringComparison.Ordinal)
            && source.Contains("SakuraElementState.IsPermanent", StringComparison.Ordinal)
            && source.Contains("power is SakuraPermanentElementPower", StringComparison.Ordinal),
            "Expected turn counts and permanence to be read from the element powers on their lifecycle events.");

        // Card VFX off keeps the board static: no sheen, flow, breathing, or animated beats.
        RegressionTestHarness.Require(
            source.Contains("SakuraModConfig.IsCardVfxEnabled()", StringComparison.Ordinal)
            && source.Contains("\"sheen_strength\", vfx ? 1f : 0f", StringComparison.Ordinal)
            && source.Contains("\"flow_strength\", vfx ? 1f : 0f", StringComparison.Ordinal)
            && source.Contains("\"breath\", vfx ? 1f : 0f", StringComparison.Ordinal)
            && source.Contains("animate && vfx", StringComparison.Ordinal),
            "Expected the card VFX setting to gate every HUD animation while the state stays visible.");

        // Trigger beams start at a medallion only while this player's HUD is live.
        RegressionTestHarness.Require(
            source.Contains("internal static void NotifyTriggered(Player? player, SakuraElement element)", StringComparison.Ordinal)
            && source.Contains("internal static bool TryGetMedallionCenter(Player? player, SakuraElement element, out Vector2 globalPosition)", StringComparison.Ordinal),
            "Expected the HUD to expose medallion flashes and beam origins keyed by player.");
    }
}
