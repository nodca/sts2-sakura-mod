using MegaCrit.Sts2.Core.Entities.Cards;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;

public sealed class LoopCardSuite
{
    [Fact]
    public void ClowUpgradeReducesSetupCostWithoutIncreasingCycleDraw()
    {
        var card = RegressionTestHarness.MutableForCostTest(new ClowLoop());

        Assert.Equal(1, card.EnergyCost.Canonical);
        Assert.Equal(1, card.DynamicVars.Cards.IntValue);
        card.UpgradeInternal();
        Assert.Equal(0, card.EnergyCost.GetWithModifiers(CostModifiers.Local));
        Assert.Equal(1, card.DynamicVars.Cards.IntValue);
    }

    [Fact]
    public void SakuraFormDoublesCycleDrawAtOneEnergy()
    {
        var card = new SakuraLoop();

        Assert.Equal(1, card.EnergyCost.Canonical);
        Assert.Equal(2, card.DynamicVars.Cards.IntValue);
    }

    [Fact]
    public void PileSwapsDrawOnFirstSwapEachTurn()
    {
        var source = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Powers/SourceCards/ClassicLoopPower.cs"));

        Assert.Contains("if (_exchangedThisTurn)", source);
        Assert.Contains("_exchangedThisTurn = false;", source);
        Assert.True(RegressionTestHarness.DeclaresMethod<ClassicLoopPower>("BeforeSideTurnStart"));
        Assert.DoesNotContain("ExchangesPerDraw", source);
    }
}
