using SakuraMod.TestRunner;

public sealed class EndOfTurnExhaustChoiceSynchronizationSuite
{
    private const string DarkPowerPath =
        "SakuraModCode/Powers/SourceCards/ClassicDarkPower.cs";

    private const string DarkSakuraPowerPath =
        "SakuraModCode/Powers/SourceCards/ClassicDarkSakuraPower.cs";

    private const string DarkSelectionLifecyclePath =
        "SakuraModCode/Powers/SourceCards/ClassicDarkSelectionLifecycle.cs";

    private static string Read(string relativePath) =>
        File.ReadAllText(RegressionTestHarness.FindRepoFile(relativePath));

    [Fact]
    public void DarkPowerUsesLiveHandSelectionWithAmountCapAndZeroGuard()
    {
        var source = Read(DarkPowerPath);
        var lifecycle = Read(DarkSelectionLifecyclePath);

        RegressionTestHarness.Require(
            lifecycle.Contains("CardSelectCmd.FromHand(", StringComparison.Ordinal),
            "Expected the shared Dark selection owner to select through CardSelectCmd.FromHand.");

        RegressionTestHarness.Require(
            lifecycle.Contains(
                "new CardSelectorPrefs(ClassicDarkPower.SelectionPrompt, 0, maxSelect)",
                StringComparison.Ordinal),
            "Expected the shared Dark selection owner to keep the min-0 / max-Amount exhaust bounds.");

        RegressionTestHarness.Require(
            !lifecycle.Contains("Cancelable", StringComparison.Ordinal),
            "Expected the shared Dark selection owner to drop Cancelable, which NPlayerHand ignores for hand selections.");

        RegressionTestHarness.Require(
            !lifecycle.Contains("hand.Contains", StringComparison.Ordinal),
            "Expected the shared Dark selection owner to pass a null filter instead of a captured hand.Contains delegate.");

        RegressionTestHarness.Require(
            source.Contains("Amount <= 0", StringComparison.Ordinal),
            "Expected ClassicDarkPower to skip the selection when Amount is not positive.");

        RegressionTestHarness.Require(
            source.Contains("ClassicDarkSelectionLifecycle.SelectFromHand(", StringComparison.Ordinal)
            && source.Contains("Amount,", StringComparison.Ordinal),
            "Expected ClassicDarkPower to cap its selection at Amount through the shared owner.");
    }

    [Fact]
    public void DarkSakuraPowerUsesLiveHandSelectionWithUnboundedCap()
    {
        var source = Read(DarkSakuraPowerPath);
        var lifecycle = Read(DarkSelectionLifecyclePath);

        RegressionTestHarness.Require(
            lifecycle.Contains("CardSelectCmd.FromHand(", StringComparison.Ordinal),
            "Expected the shared Dark selection owner to select through CardSelectCmd.FromHand.");

        RegressionTestHarness.Require(
            source.Contains("ClassicDarkSelectionLifecycle.SelectFromHand(", StringComparison.Ordinal)
            && source.Contains("999999999", StringComparison.Ordinal),
            "Expected ClassicDarkSakuraPower to keep an unbounded \"any number\" exhaust selection.");

        RegressionTestHarness.Require(
            !lifecycle.Contains("hand.Contains", StringComparison.Ordinal),
            "Expected the shared Dark selection owner to pass a null filter instead of a captured hand.Contains delegate.");
    }

    [Fact]
    public void DarkPowersKeepTheirExhaustEffects()
    {
        var darkPower = Read(DarkPowerPath);
        RegressionTestHarness.Require(
            darkPower.Contains("CardCmd.Exhaust", StringComparison.Ordinal),
            "Expected ClassicDarkPower to still Exhaust the selected cards.");
        RegressionTestHarness.Require(
            darkPower.Contains("EnergyNextTurnPower", StringComparison.Ordinal),
            "Expected ClassicDarkPower to still grant next-turn Energy when cards were exhausted.");

        var darkSakura = Read(DarkSakuraPowerPath);
        RegressionTestHarness.Require(
            darkSakura.Contains("CardCmd.Exhaust", StringComparison.Ordinal),
            "Expected ClassicDarkSakuraPower to still Exhaust the selected cards.");
        RegressionTestHarness.Require(
            darkSakura.Contains("CreateRandomDarkClowCard", StringComparison.Ordinal)
            && darkSakura.Contains("UpgradeInternal", StringComparison.Ordinal)
            && darkSakura.Contains("AddGeneratedCardsToCombatWithResults", StringComparison.Ordinal),
            "Expected ClassicDarkSakuraPower to still add upgraded random Clow cards to the discard pile.");
    }

    [Fact]
    public void DarkCardKeepsItsValues()
    {
        var darkCard = Read("SakuraModCode/Cards/ClowSakura/Dark.cs");
        RegressionTestHarness.Require(
            darkCard.Contains("new DynamicVar(\"Magic\", 1)", StringComparison.Ordinal)
            && darkCard.Contains("new DynamicVar(\"Magic\", 2)", StringComparison.Ordinal)
            && darkCard.Contains("DynamicVars[\"Magic\"].UpgradeValueBy(2)", StringComparison.Ordinal),
            "Expected ClowDark/SakuraDark Magic values and ClowDark.OnUpgrade to stay unchanged.");
    }
}
