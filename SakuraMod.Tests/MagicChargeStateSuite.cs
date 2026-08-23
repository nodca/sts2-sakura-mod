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
    public void NonExtraPlayPathsApplyCapturedMidpointOpportunity()
    {
        var sourceCard = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/SakuraSourceCard.cs"));
        var cardModel = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/SakuraCardModel.cs"));
        var extra = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/SakuraExtraEffectTransaction.cs"));
        var charge = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraMagicCharge.cs"));

        RegressionTestHarness.Require(
            charge.Contains("TryApplyCapturedOpportunity", StringComparison.Ordinal)
            && sourceCard.Contains("SakuraMagicCharge.CaptureOpportunity", StringComparison.Ordinal)
            && sourceCard.Contains("SakuraMagicCharge.TryApplyCapturedOpportunity", StringComparison.Ordinal)
            && cardModel.Contains("SakuraMagicCharge.CaptureOpportunity", StringComparison.Ordinal)
            && cardModel.Contains("SakuraMagicCharge.TryApplyCapturedOpportunity", StringComparison.Ordinal)
            && extra.Contains("SakuraMagicCharge.TryApplyCapturedOpportunity", StringComparison.Ordinal)
            && extra.Contains("SakuraMagicCharge.CaptureOpportunity", StringComparison.Ordinal),
            "Expected Magic Charge to own midpoint opportunity apply for Extra Effect and both non-extra OnPlay paths.");
    }

    [Fact]
    public void AfterPlayGainCoversTransparentAndSealedBookClassicCards()
    {
        RegressionTestHarness.Require(
            SakuraMagicCharge.GainsMagicAfterPlay(new Gale(), hasSealedBook: false)
            && SakuraMagicCharge.GainsMagicAfterPlay(new Gale(), hasSealedBook: true)
            && !SakuraMagicCharge.GainsMagicAfterPlay(new ClowSword(), hasSealedBook: false)
            && SakuraMagicCharge.GainsMagicAfterPlay(new ClowSword(), hasSealedBook: true)
            && !SakuraMagicCharge.GainsMagicAfterPlay(new SakuraSword(), hasSealedBook: false)
            && SakuraMagicCharge.GainsMagicAfterPlay(new SakuraSword(), hasSealedBook: true)
            && !SakuraMagicCharge.GainsMagicAfterPlay(new SpellSeal(), hasSealedBook: true)
            && !SakuraMagicCharge.GainsMagicAfterPlay(new AnotherMe(), hasSealedBook: true),
            "Expected Transparent cards to gain Magic Charge after play, classic source cards to need Sealed Book, and Spell/Ancient cards not to grant.");
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
            && runtime.Contains("ProjectionChanged", StringComparison.Ordinal)
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
