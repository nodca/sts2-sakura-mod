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
        var before = (state.Phases, state.Scale, state.Visibility, state.Gates, state.Draw, state.Pulse);
        state.Refresh();
        Assert.Equal(before, (state.Phases, state.Scale, state.Visibility, state.Gates, state.Draw, state.Pulse));
        for (var i = 0; i < 30; i++)
        {
            state.Advance(0.01f);
            state.Refresh();
            Assert.InRange(state.Pulse, 0f, 0.18001f);
            Assert.InRange(state.Scale, 0.94f, 1.0151f);
        }
        Assert.Equal(new Vector4(1f, 1f, 1f, 1f), state.Draw);
        state.Advance(0.18f);
        Assert.Equal(Vector4.One, state.Gates);
        Assert.Equal(1f, state.Scale, 5);
        Assert.Equal(0f, state.Pulse, 5);
        state.Advance(1.2f);
        Assert.False(state.IsAlive);
        Assert.Equal(0f, state.Visibility);
    }

    [Theory]
    [InlineData(SourceEraClass.Clow)]
    [InlineData(SourceEraClass.Clear)]
    [InlineData(SourceEraClass.Sakura)]
    public void FadingRenewalRedrawsAlongItsPathWithoutAddedLifetime(object eraValue)
    {
        var era = (SourceEraClass)eraValue;
        var state = new SakuraMagicCircleMotion(era);
        state.Advance(1.1f);
        var before = (state.Draw, state.Gates, state.Scale, state.Phases);
        Assert.True(before.Draw.Y < 1f && before.Gates.W < 1f);
        state.Refresh();
        Assert.Equal(before, (state.Draw, state.Gates, state.Scale, state.Phases));
        var previous = (state.Draw, state.Gates);
        for (var i = 0; i < 20; i++)
        {
            state.Advance(0.01f);
            Assert.True(state.Draw.X >= previous.Draw.X && state.Draw.Y >= previous.Draw.Y);
            Assert.True(state.Gates.Z >= previous.Gates.Z && state.Gates.W >= previous.Gates.W);
            Assert.Equal(1f, state.Visibility);
            previous = (state.Draw, state.Gates);
        }
        state.Advance(0.001f);
        Assert.Equal(Vector4.One, state.Draw);
        Assert.Equal(Vector4.One, state.Gates);
        Assert.Equal(1f, state.Scale);
        state.Advance(1.1f);
        Assert.False(state.IsAlive);
    }

    [Theory]
    [InlineData(SourceEraClass.Clow)]
    [InlineData(SourceEraClass.Clear)]
    [InlineData(SourceEraClass.Sakura)]
    public void ExitFadesMotifsThenRewindsScaffoldBeforeRings(object eraValue)
    {
        var era = (SourceEraClass)eraValue;
        var state = new SakuraMagicCircleMotion(era);
        state.Advance(SakuraMagicCircleMotion.FadeStart);
        Assert.Equal(Vector4.One, state.Draw);
        Assert.Equal(Vector4.One, state.Gates);
        var previous = (state.Draw, state.Gates);
        var exitAge = 0f;
        while (state.IsAlive)
        {
            state.Advance(1f / 120f);
            exitAge += 1f / 120f;
            Assert.True(state.Draw.X <= previous.Draw.X && state.Draw.Y <= previous.Draw.Y);
            Assert.True(state.Gates.Z <= previous.Gates.Z && state.Gates.W <= previous.Gates.W);
            Assert.InRange(state.Scale, 0.94f, 1f);
            if (exitAge < 0.0399f)
                Assert.Equal(1f, state.Draw.Y);
            if (exitAge < 0.0799f)
                Assert.Equal(1f, state.Draw.X);
            if (exitAge > 0.1001f)
                Assert.Equal(0f, state.Gates.W);
            if (exitAge < 0.2699f)
                Assert.Equal(1f, state.Visibility);
            previous = (state.Draw, state.Gates);
        }
        Assert.Equal(new Vector4(0f, 0f, 1f, 1f), state.Draw);
        Assert.Equal(new Vector4(1f, 1f, 0f, 0f), state.Gates);
        Assert.Equal(0f, state.Visibility);
        Assert.Equal(0.94f, state.Scale, 5);
    }

    [Theory]
    [InlineData(SourceEraClass.Clow)]
    [InlineData(SourceEraClass.Clear)]
    [InlineData(SourceEraClass.Sakura)]
    public void ExitAndRevivalAreFrameRateIndependent(object eraValue)
    {
        var era = (SourceEraClass)eraValue;
        var coarse = new SakuraMagicCircleMotion(era);
        var fine = new SakuraMagicCircleMotion(era);
        coarse.Advance(1.12f);
        for (var i = 0; i < 112; i++)
            fine.Advance(0.01f);
        coarse.Refresh();
        fine.Refresh();
        coarse.Advance(0.08f);
        for (var i = 0; i < 16; i++)
            fine.Advance(0.005f);
        for (var channel = 0; channel < 4; channel++)
        {
            Assert.InRange(MathF.Abs(coarse.Draw[channel] - fine.Draw[channel]), 0f, 0.0001f);
            Assert.InRange(MathF.Abs(coarse.Gates[channel] - fine.Gates[channel]), 0f, 0.0001f);
        }
        Assert.InRange(MathF.Abs(coarse.Scale - fine.Scale), 0f, 0.0001f);
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
        Assert.True(state.Draw.X > 0f);
        Assert.Equal(0f, state.Draw.Y);
        Assert.Equal(0f, state.Gates.Z);
        Assert.Equal(0f, state.Gates.W);
        state.Advance(0.30f);
        Assert.Equal(Vector4.One, state.Draw);
        Assert.Equal(Vector4.One, state.Gates);
    }

    [Theory]
    [InlineData(SourceEraClass.Clow)]
    [InlineData(SourceEraClass.Clear)]
    [InlineData(SourceEraClass.Sakura)]
    public void EntryDrawsRingsBeforeScaffoldAndRevealsMotifsLast(object eraValue)
    {
        var era = (SourceEraClass)eraValue;
        var state = new SakuraMagicCircleMotion(era);
        Assert.Equal(0f, state.Draw.X);
        Assert.Equal(1f, state.Visibility);
        var previous = state.Draw;
        var elapsed = 0f;
        while (elapsed < SakuraMagicCircleMotion.RevealDuration - 0.0001f)
        {
            state.Advance(1f / 120f);
            elapsed += 1f / 120f;
            Assert.True(state.Draw.X >= previous.X && state.Draw.Y >= previous.Y);
            Assert.Equal(1f, state.Draw.Z);
            Assert.Equal(1f, state.Draw.W);
            Assert.Equal(1f, state.Gates.X);
            Assert.Equal(1f, state.Gates.Y);
            if (elapsed < 0.099f)
            {
                Assert.True(state.Draw.X > 0f);
                Assert.Equal(0f, state.Draw.Y);
            }
            if (elapsed < 0.259f)
                Assert.Equal(0f, state.Gates.Z);
            if (elapsed < 0.299f)
                Assert.Equal(0f, state.Gates.W);
            previous = state.Draw;
        }
        Assert.Equal(Vector4.One, state.Draw);
        Assert.Equal(Vector4.One, state.Gates);
        Assert.True(state.Pulse > 0f);
    }

    [Theory]
    [InlineData(SourceEraClass.Clow)]
    [InlineData(SourceEraClass.Clear)]
    [InlineData(SourceEraClass.Sakura)]
    public void RenewalDuringDrawContinuesWithoutRewinding(object eraValue)
    {
        var era = (SourceEraClass)eraValue;
        var state = new SakuraMagicCircleMotion(era);
        state.Advance(0.15f);
        var drawn = state.Draw;
        Assert.InRange(drawn.X, 0.01f, 0.99f);
        state.Refresh();
        state.Advance(0.001f);
        Assert.True(state.Draw.X > drawn.X && state.Draw.Y >= drawn.Y);
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
            {
                Assert.InRange(MathF.Abs(coarse.Phases[channel] - fine.Phases[channel]), 0f, 0.00001f);
                Assert.InRange(MathF.Abs(coarse.Draw[channel] - fine.Draw[channel]), 0f, 0.00001f);
            }
        }
    }

    [Theory]
    [InlineData(SourceEraClass.Clow)]
    [InlineData(SourceEraClass.Clear)]
    [InlineData(SourceEraClass.Sakura)]
    public void LongRapidComboKeepsSpinningWithoutJumps(object eraValue)
    {
        var era = (SourceEraClass)eraValue;
        var (initial, _) = SakuraMagicCircleMotion.SpinSpeeds(era);
        var state = new SakuraMagicCircleMotion(era);
        var previous = state.Phases;
        for (var i = 0; i < 6000; i++)
        {
            state.Refresh();
            state.Advance(0.01f);
            for (var channel = 0; channel < 4; channel++)
                Assert.InRange(MathF.Abs(state.Phases[channel] - previous[channel]),
                    0f, MathF.Abs(initial[channel]) * 0.01f + 0.00001f);
            previous = state.Phases;
        }
        // Each renewal spins the layers up again.
        state.Advance(0.75f);
        var settledStep = state.Phases;
        state.Advance(0.01f);
        settledStep = state.Phases - settledStep;
        state.Refresh();
        var renewed = state.Phases;
        state.Advance(0.01f);
        Assert.True(MathF.Abs((state.Phases - renewed).X) > MathF.Abs(settledStep.X) * 1.5f);
    }

    [Fact]
    public void ClowTurnsAtHalfSakuraSpeedAroundAFixedCelestialLayer()
    {
        var clow = new SakuraMagicCircleMotion(SourceEraClass.Clow);
        var sakura = new SakuraMagicCircleMotion(SourceEraClass.Sakura);
        for (var i = 0; i < 130; i++)
        {
            clow.Advance(0.01f);
            sakura.Advance(0.01f);
            if (i == 40)
            {
                clow.Refresh();
                sakura.Refresh();
            }
            for (var channel = 0; channel < 4; channel++)
                Assert.InRange(MathF.Abs(clow.Phases[channel] - sakura.Phases[channel] * 0.5f), 0f, 0.0001f);
            Assert.Equal(0f, clow.Phases.Z);
        }
        Assert.True(clow.Phases.X > 0.1f);
    }

    [Fact]
    public void ClearTurnsAsOneRigidPlateAgainstSakuraRings()
    {
        var clear = new SakuraMagicCircleMotion(SourceEraClass.Clear);
        var sakura = new SakuraMagicCircleMotion(SourceEraClass.Sakura);
        for (var i = 0; i < 130; i++)
        {
            clear.Advance(0.01f);
            sakura.Advance(0.01f);
            Assert.Equal(clear.Phases.X, clear.Phases.Y);
            Assert.Equal(clear.Phases.X, clear.Phases.Z);
            Assert.Equal(clear.Phases.X, clear.Phases.W);
        }
        Assert.True(clear.Phases.X < -0.2f);
        Assert.True(sakura.Phases.X > 0f);
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
