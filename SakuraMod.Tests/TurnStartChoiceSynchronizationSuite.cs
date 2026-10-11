using SakuraMod.SakuraModCode.Powers;
using SakuraMod.TestRunner;

public sealed class TurnStartChoiceSynchronizationSuite
{
    private const string LoopPowerPath =
        "SakuraModCode/Powers/SourceCards/ClassicLoopPower.cs";

    private static string LoopPowerSource() =>
        File.ReadAllText(RegressionTestHarness.FindRepoFile(LoopPowerPath));

    [Fact]
    public void LoopPowerMovesTurnStartDrawIntoModifyHandDraw()
    {
        var source = LoopPowerSource();

        RegressionTestHarness.Require(
            source.Contains("ModifyHandDraw(Player player, decimal count)", StringComparison.Ordinal),
            "Expected ClassicLoopPower to express its extra turn-start draw through ModifyHandDraw.");

        var turnStart = source[source.IndexOf("AfterPlayerTurnStart", StringComparison.Ordinal)..];
        RegressionTestHarness.Require(
            !turnStart.Contains("CardPileCmd.Draw", StringComparison.Ordinal),
            "Expected ClassicLoopPower to stop calling CardPileCmd.Draw from inside AfterPlayerTurnStart.");
    }

    [Fact]
    public void LoopPowerUsesSynchronizedDiscardSelectionWithoutInertCancel()
    {
        var source = LoopPowerSource();

        RegressionTestHarness.Require(
            source.Contains("CardSelectCmd.FromHandForDiscard", StringComparison.Ordinal),
            "Expected ClassicLoopPower to select through CardSelectCmd.FromHandForDiscard.");

        RegressionTestHarness.Require(
            !source.Contains("CardSelectCmd.FromHand(", StringComparison.Ordinal),
            "Expected ClassicLoopPower to stop using the plain CardSelectCmd.FromHand overload.");

        RegressionTestHarness.Require(
            !source.Contains("Cancelable", StringComparison.Ordinal),
            "Expected ClassicLoopPower to drop Cancelable, which NPlayerHand ignores for hand selections.");

        RegressionTestHarness.Require(
            !source.Contains("hand.Contains", StringComparison.Ordinal),
            "Expected ClassicLoopPower to pass a null filter instead of a captured hand.Contains delegate.");

        RegressionTestHarness.Require(
            source.Contains(
                "new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, 0, Amount)",
                StringComparison.Ordinal),
            "Expected ClassicLoopPower to keep the \"discard up to N\" bounds of min 0 and max Amount.");
    }

    [Fact]
    public void LoopCardUpgradeAddsOneCard()
    {
        var cardSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/ClowSakura/Loop.cs"));
        RegressionTestHarness.Require(
            cardSource.Contains("Cards.UpgradeValueBy(1)", StringComparison.Ordinal),
            "Expected ClowLoop.OnUpgrade to still add one card to the Loop value.");
    }

    [Fact]
    public void LoopPowerOverridesResolveAgainstTheTargetGameAssemblies()
    {
        RegressionTestHarness.Require(
            RegressionTestHarness.DeclaresMethod<ClassicLoopPower>(nameof(ClassicLoopPower.ModifyHandDraw)),
            "Expected ClassicLoopPower.ModifyHandDraw to override a real base member.");
    }
}
