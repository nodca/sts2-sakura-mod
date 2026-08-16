using MegaCrit.Sts2.Core.Models.Singleton;
using SakuraMod.SakuraModCode.FourthAct.Compatibility;
using SakuraMod.SakuraModCode.FourthAct.Wind.Encounters;

public sealed class FourthActMultiplayerScalingSuite
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public void CompatibilityLeavesEveryNonFourthActIndexToTheNativeMethod(int actIndex)
    {
        Assert.False(FourthActMultiplayerScalingCompatibility.TryResolve(null, actIndex, out _));
    }

    [Fact]
    public void FourthActUsesTheApprovedEncounterTierScales()
    {
        Assert.True(FourthActMultiplayerScalingCompatibility.TryResolve(
            new FlyEncounter(),
            FourthActMultiplayerScalingCompatibility.FourthActIndex,
            out var eliteScale));
        Assert.True(FourthActMultiplayerScalingCompatibility.TryResolve(
            new WindyEncounter(),
            FourthActMultiplayerScalingCompatibility.FourthActIndex,
            out var bossScale));

        Assert.Equal(1.2m, eliteScale);
        Assert.Equal(1.3m, bossScale);
    }

    [Fact]
    public void NativeActsRetainTheirOriginalMultipliers()
    {
        var elite = new FlyEncounter();
        var boss = new WindyEncounter();

        Assert.Equal(1.1m, MultiplayerScalingModel.GetMultiplayerScaling(elite, 0));
        Assert.Equal(1.2m, MultiplayerScalingModel.GetMultiplayerScaling(elite, 1));
        Assert.Equal(1.2m, MultiplayerScalingModel.GetMultiplayerScaling(elite, 2));
        Assert.Equal(1.3m, MultiplayerScalingModel.GetMultiplayerScaling(boss, 2));
    }

    [Fact]
    public void CompatibilityIsLimitedToThePinnedGameCommit()
    {
        Assert.True(FourthActMultiplayerScalingCompatibility.IsSupportedGameAssembly(
            typeof(MultiplayerScalingModel).Assembly));
        Assert.False(FourthActMultiplayerScalingCompatibility.IsSupportedGameAssembly(
            typeof(FourthActMultiplayerScalingSuite).Assembly));
    }
}
