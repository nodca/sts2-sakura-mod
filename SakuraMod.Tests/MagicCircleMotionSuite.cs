using Godot;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;

namespace SakuraMod.Tests;

public sealed class MagicCircleMotionSuite
{
    [Theory]
    [InlineData(SourceEraClass.Clow)]
    [InlineData(SourceEraClass.Clear)]
    [InlineData(SourceEraClass.Sakura)]
    public void RenewalPreservesPoseRevealAndBrightness(object eraValue)
    {
        var era = (SourceEraClass)eraValue;
        var state = new SakuraMagicCircleMotion(era);
        state.Advance(0.08f);
        var before = (state.Phases, state.Scale, state.Visibility, state.Gates, state.Pulse);
        state.Refresh();
        Assert.Equal(before, (state.Phases, state.Scale, state.Visibility, state.Gates, state.Pulse));
        for (var i = 0; i < 30; i++)
        {
            state.Advance(0.01f);
            state.Refresh();
            Assert.InRange(state.Pulse, 0f, 0.18001f);
            Assert.InRange(state.Scale, 0.78f, 1.0151f);
        }
        Assert.Equal(Vector4.One, state.Gates);
        Assert.Equal(1f, state.Scale, 5);
        state.Advance(0.18f);
        Assert.Equal(0f, state.Pulse, 5);
        state.Advance(1f);
        Assert.False(state.IsAlive);
        Assert.Equal(0f, state.Visibility);
    }

    [Theory]
    [InlineData(SourceEraClass.Clow)]
    [InlineData(SourceEraClass.Clear)]
    [InlineData(SourceEraClass.Sakura)]
    public void FadingRenewalRecoversWithoutJumpOrAddedLifetime(object eraValue)
    {
        var era = (SourceEraClass)eraValue;
        var state = new SakuraMagicCircleMotion(era);
        state.Advance(1f);
        var before = (state.Visibility, state.Scale, state.Phases);
        state.Refresh();
        Assert.Equal(before, (state.Visibility, state.Scale, state.Phases));
        state.Advance(0.1f);
        Assert.True(state.Visibility > before.Visibility);
        state.Advance(0.15f);
        Assert.Equal(1f, state.Visibility);
        Assert.Equal(1f, state.Scale);
        state.Advance(0.901f);
        Assert.False(state.IsAlive);
    }

    [Theory]
    [InlineData(SourceEraClass.Clow)]
    [InlineData(SourceEraClass.Clear)]
    [InlineData(SourceEraClass.Sakura)]
    public void TransitionShowsOnlyRingsUntilOldGeometryRetires(object eraValue)
    {
        var era = (SourceEraClass)eraValue;
        var state = new SakuraMagicCircleMotion(era, transition: true);
        state.Advance(0.08f);
        state.Refresh();
        state.Advance(0.03f);
        Assert.True(state.Gates.X > 0f);
        Assert.Equal(0f, state.Gates.Y);
        Assert.Equal(0f, state.Gates.Z);
        Assert.Equal(0f, state.Gates.W);
        state.Advance(0.111f);
        Assert.Equal(Vector4.One, state.Gates);
    }

    [Theory]
    [InlineData(SourceEraClass.Clow)]
    [InlineData(SourceEraClass.Clear)]
    [InlineData(SourceEraClass.Sakura)]
    public void MotionIsFrameRateIndependent(object eraValue)
    {
        var era = (SourceEraClass)eraValue;
        var coarse = new SakuraMagicCircleMotion(era);
        var fine = new SakuraMagicCircleMotion(era);
        for (var eventIndex = 0; eventIndex < 3; eventIndex++)
        {
            coarse.Refresh();
            fine.Refresh();
            coarse.Advance(0.4f);
            for (var i = 0; i < 48; i++)
                fine.Advance(1f / 120f);
            for (var channel = 0; channel < 4; channel++)
                Assert.InRange(MathF.Abs(coarse.Phases[channel] - fine.Phases[channel]), 0f, 0.00001f);
        }
    }

    [Theory]
    [InlineData(SourceEraClass.Clow)]
    [InlineData(SourceEraClass.Clear)]
    public void LongRapidComboRemainsBoundedAndRespondsAfterSettling(object eraValue)
    {
        var era = (SourceEraClass)eraValue;
        var state = new SakuraMagicCircleMotion(era);
        for (var i = 0; i < 6000; i++)
        {
            state.Refresh();
            state.Advance(0.01f);
            Assert.InRange(MathF.Abs(state.Phases.X), 0f, 0.16f);
            Assert.InRange(MathF.Abs(state.Phases.Y), 0f, 0.09f);
            Assert.Equal(0f, state.Phases.Z);
            Assert.Equal(0f, state.Phases.W);
        }
        state.Advance(0.75f);
        var settled = state.Phases;
        state.Refresh();
        state.Advance(0.08f);
        Assert.True((state.Phases - settled).Length() > 0.001f);
    }

    [Fact]
    public void EveryEraUsesItsOwnDeclaredMaskPair()
    {
        var paths = Enum.GetValues<SourceEraClass>()
            .Select(SakuraMagicCirclePresenter.MaskPathsFor).ToArray();
        Assert.Equal(3, paths.Distinct().Count());
        foreach (var pair in paths)
        {
            Assert.Contains(pair.Ink, SakuraMagicCirclePresenter.AssetPaths);
            Assert.Contains(pair.Knockout, SakuraMagicCirclePresenter.AssetPaths);
        }
    }

    [Fact]
    public void CircleOnlyCardsDeclareResourcesBeforeTheirFirstPlay()
    {
        foreach (var card in new MegaCrit.Sts2.Core.Models.CardModel[]
                 { new ClowLight(), new Dreaming(), new SakuraMirror() })
        {
            var roots = SakuraCardVfxAssets.RunAssetPaths(card).ToArray();
            Assert.All(SakuraMagicCirclePresenter.AssetPaths, path => Assert.Contains(path, roots));
            Assert.Equal(roots.Length, roots.Distinct().Count());
        }
        Assert.Empty(SakuraCardVfxAssets.RunAssetPaths(new AnotherMe()));
    }
}
