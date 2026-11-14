using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;
using System.Text.Json;

public sealed class MagicChargeStateSuite
{
    [Fact]
    public void ElementProjectionCoversEverySakuraCardFamily()
    {
        RegressionTestHarness.Require(
            SakuraActions.ElementSetOf(new ClowDark())
                == (SakuraElementSet.Wind | SakuraElementSet.Water)
            && SakuraActions.ElementSetOf(new SakuraDark())
                == (SakuraElementSet.Wind | SakuraElementSet.Water)
            && SakuraActions.ElementSetOf(new SpellFengHua()) == SakuraElementSet.Wind
            && SakuraActions.ElementSetOf(new Gale()) == SakuraElementSet.Wind,
            "Expected Clow, Sakura, Spell, and Transparent cards to share one element projection.");
    }

    [Fact]
    public void MagicChargeHasOneProductionWriteOwnerAndNoNativePowerIcon()
    {
        RegressionTestHarness.Require(
            !new ClassicMagicChargePower().IsVisible,
            "Expected dedicated Magic Charge UI to replace the native Power-bar icon.");

        var sourceRoot = Path.GetDirectoryName(RegressionTestHarness.FindRepoFile("SakuraMod.csproj"))!;
        var ownerPath = Path.GetFullPath(Path.Combine(
            sourceRoot,
            "SakuraModCode",
            "Character",
            "SakuraMagicCharge.cs"));
        foreach (var file in Directory.EnumerateFiles(
                     Path.Combine(sourceRoot, "SakuraModCode"),
                     "*.cs",
                     SearchOption.AllDirectories))
        {
            if (Path.GetFullPath(file) == ownerPath)
                continue;

            RegressionTestHarness.Require(
                !File.ReadAllText(file).Contains(
                    "PowerCmd.Apply<ClassicMagicChargePower>",
                    StringComparison.Ordinal),
                $"Expected {Path.GetRelativePath(sourceRoot, file)} to route Magic Charge writes through SakuraMagicCharge.");
        }
    }

    [Fact]
    public void AfterPlayGainRequiresSealedBookForTransparentAndClassicCards()
    {
        RegressionTestHarness.Require(
            !SakuraMagicCharge.GainsMagicAfterPlay(new Gale(), hasSealedBook: false)
            && SakuraMagicCharge.GainsMagicAfterPlay(new Gale(), hasSealedBook: true)
            && !SakuraMagicCharge.GainsMagicAfterPlay(new ClowSword(), hasSealedBook: false)
            && SakuraMagicCharge.GainsMagicAfterPlay(new ClowSword(), hasSealedBook: true)
            && !SakuraMagicCharge.GainsMagicAfterPlay(new SakuraSword(), hasSealedBook: false)
            && SakuraMagicCharge.GainsMagicAfterPlay(new SakuraSword(), hasSealedBook: true)
            && !SakuraMagicCharge.GainsMagicAfterPlay(new SpellSeal(), hasSealedBook: true)
            && !SakuraMagicCharge.GainsMagicAfterPlay(new AnotherMe(), hasSealedBook: true),
            "Expected Sealed Book to own after-play Magic Charge for Transparent and classic source cards, and Spell/Ancient cards not to grant.");
    }

    [Fact]
    public void GlowCardsUseMagicChargeAsTheirPresentationTrigger()
    {
        var clowGlow = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/ClowSakura/Glow.cs"));
        var charge = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraMagicCharge.cs"));

        RegressionTestHarness.Require(
            clowGlow.Contains("SakuraMagicCharge.GainMagic", StringComparison.Ordinal)
            && clowGlow.Contains("class SakuraGlow", StringComparison.Ordinal)
            && clowGlow.Contains("ApplyPower<ClassicGlowPower>", StringComparison.Ordinal)
            && charge.Contains("SakuraGlowVisual.NotifyMagicChargeGained", StringComparison.Ordinal),
            "Expected both Glow cards to enter the unified Magic Charge flow that drives the short The Glow feedback.");
    }

    [Fact]
    public void HudProjectionDistinguishesAllChargeAndReadinessStates()
    {
        AssertHudState(0, false, SakuraMagicChargeHudState.Zero);
        AssertHudState(4, false, SakuraMagicChargeHudState.Low);
        AssertHudState(5, false, SakuraMagicChargeHudState.Low);
        AssertHudState(9, false, SakuraMagicChargeHudState.Low);
        AssertHudState(10, true, SakuraMagicChargeHudState.FullReady);
        AssertHudState(17, false, SakuraMagicChargeHudState.FullLocked);
    }

    [Fact]
    public void HudLiquidFillTracksChargeAndCapsAtFull()
    {
        RegressionTestHarness.Require(
            SakuraMagicChargeHud.LiquidFillFor(-1) == 0f
            && SakuraMagicChargeHud.LiquidFillFor(0) == 0f
            && SakuraMagicChargeHud.LiquidFillFor(5) == 0.5f
            && SakuraMagicChargeHud.LiquidFillFor(10) == 1f
            && SakuraMagicChargeHud.LiquidFillFor(17) == 1f,
            "Expected Magic Charge liquid to rise from 0% to 100% across 0-10 charge and remain full above 10.");
    }

    [Fact]
    public void HudSceneAndRuntimeKeepDedicatedResourceContracts()
    {
        var scene = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/scenes/combat/sakura_magic_charge_hud.tscn"));
        RegressionTestHarness.Require(
            scene.Contains("mouse_filter", StringComparison.Ordinal)
            && scene.Contains("resource_local_to_scene", StringComparison.Ordinal)
            && scene.Contains("shader_parameter/fill_ratio", StringComparison.Ordinal)
            && scene.Contains("[node name=\"Amount\"", StringComparison.Ordinal)
            && !scene.Contains("z_index", StringComparison.Ordinal),
            "Expected the Magic Charge HUD to keep a centered Amount node in a self-contained, non-interactive root.");

        foreach (var asset in new[] { "magic_charge_glow", "magic_charge_emblem", "magic_charge_liquid" })
        {
            RegressionTestHarness.Require(
                scene.Contains($"res://SakuraMod/images/charui/magic_charge_hud/{asset}.png", StringComparison.Ordinal),
                $"Expected the Magic Charge HUD to use its custom {asset} layer.");
            RegressionTestHarness.FindRepoFile($"SakuraMod/images/charui/magic_charge_hud/{asset}.png");
            RegressionTestHarness.FindRepoFile($"SakuraMod/images/charui/magic_charge_hud/{asset}.png.import");
        }

        var runtime = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraMagicChargeHud.cs"));
        var elementRuntime = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraElementStateHud.cs"));
        var main = File.ReadAllText(RegressionTestHarness.FindRepoFile("SakuraModCode/MainFile.cs"));
        // The charge HUD runtime stays event-driven: liquid fill and hover tips ride
        // named parameters and projections instead of frame polling.
        RegressionTestHarness.Require(
            runtime.Contains("MountHorizontalOffset", StringComparison.Ordinal)
            && runtime.Contains("MountVerticalOffset", StringComparison.Ordinal)
            && runtime.Contains("BaseScale", StringComparison.Ordinal)
            && runtime.Contains("SetShaderParameter", StringComparison.Ordinal)
            && runtime.Contains("HoverTipFactory.FromPower", StringComparison.Ordinal)
            && runtime.Contains("PowerApplied += OnPowerApplied", StringComparison.Ordinal)
            && runtime.Contains("PowerIncreased += OnPowerIncreased", StringComparison.Ordinal)
            && runtime.Contains("PowerDecreased += OnPowerDecreased", StringComparison.Ordinal)
            && runtime.Contains("PowerRemoved += OnPowerRemoved", StringComparison.Ordinal)
            && !runtime.Contains("_Process(", StringComparison.Ordinal),
            "Expected the Magic Charge HUD to stay event-driven with a native power hover tip.");

        // The combat resource runtime mounts the element disc and attaches the emblem.
        RegressionTestHarness.Require(
            runtime.Contains("SakuraElementStateHud.Mount", StringComparison.Ordinal)
            && runtime.Contains("SakuraMagicChargeHud.Mount", StringComparison.Ordinal),
            "Expected the combat resource runtime to mount the element disc and the attached charge emblem.");

        // MainFile registers the unified combat resource patch only.
        RegressionTestHarness.Require(
            elementRuntime.Contains("SakuraCombatResourceHud.Mount", StringComparison.Ordinal)
            && main.Contains("SakuraCombatResourceHudPatchRegistration.Register", StringComparison.Ordinal)
            && !main.Contains("SakuraElementStateHudPatchRegistration.Register()", StringComparison.Ordinal),
            "Expected MainFile to wire the unified combat resource HUD patch alone.");
    }

    [Fact]
    public void MagicChargeLocalizationKeepsPowerDescriptionKey()
    {
        foreach (var locale in new[] { "eng", "zhs" })
        {
            var powers = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(
                RegressionTestHarness.FindRepoFile($"SakuraMod/localization/{locale}/powers.json")))
                ?? throw new InvalidOperationException($"Could not parse {locale} powers localization.");
            RegressionTestHarness.Require(
                powers.ContainsKey("SAKURA_MOD_POWER_CLASSIC_MAGIC_CHARGE_POWER.description"),
                $"Expected {locale} powers localization to keep the Magic Charge description key.");
        }
    }

    private static void AssertHudState(
        int amount,
        bool canActivateExtra,
        SakuraMagicChargeHudState expected)
    {
        var projection = SakuraMagicChargeProjection.From(amount, canActivateExtra);
        RegressionTestHarness.Require(
            projection.Amount == amount && projection.State == expected,
            $"Expected {amount} Magic Charge to project as {expected}.");
    }
}
