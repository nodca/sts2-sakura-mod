using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;

public sealed class BigLittleCardSuite
{
    [Fact]
    public void BigAndLittleGrantTwoStatsWithoutDrawbacks()
    {
        var big = new ClowBig();
        var little = new ClowLittle();

        Assert.Equal(2, big.DynamicVars["StrengthPower"].IntValue);
        Assert.Equal(2, little.DynamicVars["DexterityPower"].IntValue);
        Assert.Equal(2, big.DynamicVars["ExtraTimes"].IntValue);
        Assert.Equal(2, little.DynamicVars["ZeroCostBlock"].IntValue);
    }

    [Fact]
    public void UpgradedPowersReadTheActualCostWhenThePlayStarts()
    {
        Assert.Equal(2, ClassicBigUpgradedPower.MinimumCost);
        Assert.Equal(PowerStackType.Counter, new ClassicBigUpgradedPower().StackType);
        Assert.Equal(PowerStackType.Counter, new ClassicLittleUpgradedPower().StackType);
        Assert.True(RegressionTestHarness.DeclaresMethod<ClassicBigUpgradedPower>("BeforeCardPlayed"));
        Assert.True(RegressionTestHarness.DeclaresMethod<ClassicLittleUpgradedPower>("BeforeCardPlayed"));
    }

    [Fact]
    public void UpgradedBigReadsOriginalCostThroughDiscounts()
    {
        var swing = RegressionTestHarness.MutableForCostTest(new Swing());
        swing.EnergyCost.SetThisTurn(0, true);
        Assert.Equal(0, swing.EnergyCost.GetWithModifiers(CostModifiers.Local));
        Assert.Equal(2, ClassicSizeCostRules.OriginalPreviewCost(swing));
    }
}
