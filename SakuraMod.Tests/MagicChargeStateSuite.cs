using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;
using System.Text.Json;

public sealed class MagicChargeStateSuite
{
    [Fact]
    public void ChargeBandsAndOpportunityTransitionsFollowVisibleRanges()
    {
        RegressionTestHarness.Require(
            SakuraMagicCharge.BandFor(0) == SakuraMagicChargeBand.Low
            && SakuraMagicCharge.BandFor(4) == SakuraMagicChargeBand.Low
            && SakuraMagicCharge.BandFor(5) == SakuraMagicChargeBand.Resonant
            && SakuraMagicCharge.BandFor(9) == SakuraMagicChargeBand.Resonant
            && SakuraMagicCharge.BandFor(10) == SakuraMagicChargeBand.Full
            && SakuraMagicCharge.BandFor(17) == SakuraMagicChargeBand.Full,
            "Expected Magic Charge bands to use 0-4, 5-9, and 10+.");

        AssertTransition(4, 5, SakuraMagicChargeOpportunityTransition.Arm);
        AssertTransition(4, 6, SakuraMagicChargeOpportunityTransition.Arm);
        AssertTransition(17, 7, SakuraMagicChargeOpportunityTransition.Arm);
        AssertTransition(5, 9, SakuraMagicChargeOpportunityTransition.Preserve);
        AssertTransition(9, 6, SakuraMagicChargeOpportunityTransition.Preserve);
        AssertTransition(9, 10, SakuraMagicChargeOpportunityTransition.Expire);
        AssertTransition(6, 4, SakuraMagicChargeOpportunityTransition.Expire);
        AssertTransition(4, 10, SakuraMagicChargeOpportunityTransition.Expire);
        AssertTransition(12, 3, SakuraMagicChargeOpportunityTransition.Expire);
    }

    [Fact]
    public void OpportunityGenerationIdentifiesExactlyOneArmedEntry()
    {
        var power = new ClassicMagicChargePower();
        RegressionTestHarness.Require(
            power.ArmedOpportunityGeneration == 0,
            "Expected a new Magic Charge Power to have no midpoint opportunity.");

        power.ArmNextOpportunity();
        var firstGeneration = power.ArmedOpportunityGeneration;
        RegressionTestHarness.Require(
            firstGeneration == 1
            && !power.TryConsumeOpportunity(firstGeneration + 1)
            && power.ArmedOpportunityGeneration == firstGeneration,
            "Expected an unrelated generation not to consume the armed opportunity.");

        RegressionTestHarness.Require(
            power.TryConsumeOpportunity(firstGeneration)
            && power.ArmedOpportunityGeneration == 0
            && !power.TryConsumeOpportunity(firstGeneration),
            "Expected the matching opportunity generation to be consumed exactly once.");

        power.ArmNextOpportunity();
        var secondGeneration = power.ArmedOpportunityGeneration;
        power.ExpireOpportunity();
        RegressionTestHarness.Require(
            secondGeneration > firstGeneration
            && power.ArmedOpportunityGeneration == 0,
            "Expected reentry to create a new generation and leaving the band to expire it.");
    }

    [Fact]
    public void MidpointElementProjectionCoversEverySakuraCardFamily()
    {
        RegressionTestHarness.Require(
            SakuraActions.ElementSetOf(new ClowDark())
                == (SakuraElementSet.Wind | SakuraElementSet.Water)
            && SakuraActions.ElementSetOf(new SakuraDark())
                == (SakuraElementSet.Wind | SakuraElementSet.Water)
            && SakuraActions.ElementSetOf(new SpellFengHua()) == SakuraElementSet.Wind
            && SakuraActions.ElementSetOf(new Gale()) == SakuraElementSet.Wind,
            "Expected Clow, Sakura, Spell, and Transparent cards to share one midpoint element projection.");
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
            "Cards",
            "SakuraSourceCard.cs"));
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
    public void HudProjectionDistinguishesAllChargeAndReadinessStates()
    {
        AssertHudState(0, false, false, SakuraMagicChargeHudState.Zero);
        AssertHudState(4, false, false, SakuraMagicChargeHudState.Low);
        AssertHudState(5, true, false, SakuraMagicChargeHudState.ResonantReady);
        AssertHudState(9, false, false, SakuraMagicChargeHudState.ResonantSpent);
        AssertHudState(10, false, true, SakuraMagicChargeHudState.FullReady);
        AssertHudState(17, false, false, SakuraMagicChargeHudState.FullLocked);
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
            scene.Contains("offset_right = 128.0", StringComparison.Ordinal)
            && scene.Contains("offset_bottom = 128.0", StringComparison.Ordinal)
            && scene.Contains("scale = Vector2(0.8, 0.8)", StringComparison.Ordinal)
            && scene.Contains("mouse_filter = 0", StringComparison.Ordinal)
            && scene.Contains("resource_local_to_scene = true", StringComparison.Ordinal)
            && scene.Contains("shader_parameter/fill_ratio = 0.0", StringComparison.Ordinal)
            && scene.Contains("[node name=\"Amount\"", StringComparison.Ordinal)
            && !scene.Contains("z_index", StringComparison.Ordinal),
            "Expected Magic Charge to match the native Star counter's 128x128 at 0.8 scale with a centered amount.");

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
        RegressionTestHarness.Require(
            runtime.Contains("private const float MountHorizontalOffset = -50f", StringComparison.Ordinal)
            && runtime.Contains("private const float MountVerticalOffset = 40f", StringComparison.Ordinal)
            && runtime.Contains("private static readonly Vector2 BaseScale = new(0.8f, 0.8f)", StringComparison.Ordinal)
            && runtime.Contains("_chargeLiquidMaterial.SetShaderParameter(LiquidFillParameterName, value)", StringComparison.Ordinal)
            && runtime.Contains("HoverTipFactory.FromPower<ClassicMagicChargePower>()", StringComparison.Ordinal)
            && runtime.Contains("ProjectionChanged += OnProjectionChanged", StringComparison.Ordinal)
            && !runtime.Contains("_Process(", StringComparison.Ordinal)
            && runtime.Contains("SakuraElementStateHud.Mount(ui, combatState)", StringComparison.Ordinal)
            && runtime.Contains("SakuraMagicChargeHud.Mount(ui, combatState)", StringComparison.Ordinal)
            && elementRuntime.Contains("SakuraCombatResourceHud.Mount(__instance, state)", StringComparison.Ordinal)
            && main.Contains("SakuraCombatResourceHudPatchRegistration.Register()", StringComparison.Ordinal)
            && !main.Contains("SakuraElementStateHudPatchRegistration.Register()", StringComparison.Ordinal),
            "Expected one combat resource HUD lifecycle to mount the element disc and attached Magic Charge emblem.");
    }

    [Fact]
    public void MagicChargeLocalizationExplainsResonanceAndExtraThresholds()
    {
        foreach (var locale in new[] { "eng", "zhs" })
        {
            var powers = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(
                RegressionTestHarness.FindRepoFile($"SakuraMod/localization/{locale}/powers.json")))
                ?? throw new InvalidOperationException($"Could not parse {locale} powers localization.");
            var description = powers["SAKURA_MOD_POWER_CLASSIC_MAGIC_CHARGE_POWER.description"];
            RegressionTestHarness.Require(
                description.Contains("5", StringComparison.Ordinal)
                && description.Contains("9", StringComparison.Ordinal)
                && description.Contains("10", StringComparison.Ordinal),
                $"Expected {locale} Magic Charge hover text to explain midpoint resonance and full charge.");
        }
    }

    private static void AssertTransition(
        int previousAmount,
        int currentAmount,
        SakuraMagicChargeOpportunityTransition expected) =>
        RegressionTestHarness.Require(
            SakuraMagicCharge.OpportunityTransition(previousAmount, currentAmount) == expected,
            $"Expected Magic Charge transition {previousAmount}->{currentAmount} to be {expected}.");

    private static void AssertHudState(
        int amount,
        bool hasOpportunity,
        bool canActivateExtra,
        SakuraMagicChargeHudState expected)
    {
        var projection = SakuraMagicChargeProjection.From(amount, hasOpportunity, canActivateExtra);
        RegressionTestHarness.Require(
            projection.Amount == amount && projection.State == expected,
            $"Expected {amount} Magic Charge to project as {expected}.");
    }
}
