using SakuraMod.TestRunner;

public sealed class EndOfTurnExhaustChoiceSynchronizationSuite
{
    private const string DarkPowerPath =
        "SakuraModCode/Powers/SourceCards/ClassicDarkPower.cs";

    private const string DarkSakuraPowerPath =
        "SakuraModCode/Powers/SourceCards/ClassicDarkSakuraPower.cs";

    private static string Read(string relativePath) =>
        File.ReadAllText(RegressionTestHarness.FindRepoFile(relativePath));

    [Fact]
    public void DarkPowerUsesLiveHandSelectionWithAmountCapAndZeroGuard()
    {
        var source = Read(DarkPowerPath);

        RegressionTestHarness.Require(
            source.Contains("CardSelectCmd.FromHand(", StringComparison.Ordinal),
            "Expected ClassicDarkPower to select through CardSelectCmd.FromHand.");

        RegressionTestHarness.Require(
            source.Contains("new CardSelectorPrefs(SelectionPrompt, 0, Amount)", StringComparison.Ordinal),
            "Expected ClassicDarkPower to keep the min-0 / max-Amount exhaust bounds.");

        RegressionTestHarness.Require(
            !source.Contains("Cancelable", StringComparison.Ordinal),
            "Expected ClassicDarkPower to drop Cancelable, which NPlayerHand ignores for hand selections.");

        RegressionTestHarness.Require(
            !source.Contains("hand.Contains", StringComparison.Ordinal),
            "Expected ClassicDarkPower to pass a null filter instead of a captured hand.Contains delegate.");

        RegressionTestHarness.Require(
            source.Contains("Amount <= 0", StringComparison.Ordinal),
            "Expected ClassicDarkPower to skip the selection when Amount is not positive.");
    }

    [Fact]
    public void DarkSakuraPowerUsesLiveHandSelectionWithUnboundedCap()
    {
        var source = Read(DarkSakuraPowerPath);

        RegressionTestHarness.Require(
            source.Contains("CardSelectCmd.FromHand(", StringComparison.Ordinal),
            "Expected ClassicDarkSakuraPower to select through CardSelectCmd.FromHand.");

        RegressionTestHarness.Require(
            source.Contains(
                "new CardSelectorPrefs(ClassicDarkPower.SelectionPrompt, 0, 999999999)",
                StringComparison.Ordinal),
            "Expected ClassicDarkSakuraPower to keep an unbounded \"any number\" exhaust selection.");

        RegressionTestHarness.Require(
            !source.Contains("Cancelable", StringComparison.Ordinal),
            "Expected ClassicDarkSakuraPower to drop Cancelable, which NPlayerHand ignores for hand selections.");

        RegressionTestHarness.Require(
            !source.Contains("hand.Contains", StringComparison.Ordinal),
            "Expected ClassicDarkSakuraPower to pass a null filter instead of a captured hand.Contains delegate.");
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
    public void DarkPowerTextStaysUnchangedInBothLocales()
    {
        foreach (var (locale, expected) in new[]
                 {
                     ("zhs", "回合结束时，你可以消耗手牌。若以此消耗了牌，下回合获得 1 点能量。"),
                     ("eng", "At end of turn, you may Exhaust cards from your hand. If you Exhaust any, gain 1 Energy next turn."),
                 })
        {
            var powers = Read($"SakuraMod/localization/{locale}/powers.json");
            RegressionTestHarness.Require(
                powers.Contains(expected, StringComparison.Ordinal),
                $"Expected SAKURA_MOD_POWER_CLASSIC_DARK_POWER.description in {locale} to keep its existing wording.");
        }

        foreach (var (locale, expected) in new[]
                 {
                     ("zhs", "回合结束时，选择消耗任意张手牌，并将相同数量升级过的随机库洛牌加入弃牌堆。"),
                     ("eng", "At end of turn, choose any number of cards in your hand to Exhaust, then add the same number of upgraded random Clow cards to your discard pile."),
                 })
        {
            var powers = Read($"SakuraMod/localization/{locale}/powers.json");
            RegressionTestHarness.Require(
                powers.Contains(expected, StringComparison.Ordinal),
                $"Expected SAKURA_MOD_POWER_CLASSIC_DARK_SAKURA_POWER.description in {locale} to keep its existing wording.");
        }
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
