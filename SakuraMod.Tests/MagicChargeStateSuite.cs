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
    public void MagicChargePowerStaysHiddenInNativeBar()
    {
        RegressionTestHarness.Require(
            !new ClassicMagicChargePower().IsVisible,
            "Expected dedicated Magic Charge UI to replace the native Power-bar icon.");
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
    public void MagicChargeLocalizationKeepsPowerDescriptionKey()
    {
        foreach (var locale in new[] { "eng", "zhs", "jpn" })
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
