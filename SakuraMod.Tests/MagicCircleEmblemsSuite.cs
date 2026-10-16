using Godot;
using SakuraMod.SakuraModCode.Cards;

namespace SakuraMod.Tests;

public sealed class MagicCircleEmblemsSuite
{
    [Fact]
    public void ActiveStatesAtAppearanceAreLitImmediatelyInQuadrantOrder()
    {
        var emblems = new SakuraMagicCircleEmblems(SakuraElementSet.Fire | SakuraElementSet.Water);
        Assert.Equal(new Vector4(1f, 0f, 0f, 1f), emblems.Glow);
        Assert.Equal(1f, emblems.Dim);
        emblems.Advance(0.5f, SakuraElementSet.Fire | SakuraElementSet.Water);
        Assert.Equal(new Vector4(1f, 0f, 0f, 1f), emblems.Glow);
    }

    [Fact]
    public void NoActiveStateNeverDims()
    {
        var emblems = new SakuraMagicCircleEmblems(SakuraElementSet.None);
        emblems.Trigger(SakuraElementSet.Wind, 0f);
        for (var i = 0; i < 60; i++)
        {
            emblems.Advance(0.01f, SakuraElementSet.None);
            Assert.Equal(0f, emblems.Dim);
        }
        Assert.Equal(Vector4.Zero, emblems.Glow);
    }

    [Fact]
    public void NewStateLightsSmoothlyWithinAboutFifteenHundredths()
    {
        var emblems = new SakuraMagicCircleEmblems(SakuraElementSet.None);
        emblems.Advance(0.01f, SakuraElementSet.Earth);
        Assert.InRange(emblems.Glow.Z, 0.01f, 0.5f);
        Assert.InRange(emblems.Dim, 0.01f, 0.5f);
        emblems.Advance(0.14f, SakuraElementSet.Earth);
        Assert.InRange(emblems.Glow.Z, 0.94f, 1f);
        Assert.InRange(emblems.Dim, 0.94f, 1f);
        Assert.Equal(0f, emblems.Glow.X);
    }

    [Fact]
    public void SmoothingIsFrameRateIndependent()
    {
        var coarse = new SakuraMagicCircleEmblems(SakuraElementSet.None);
        var fine = new SakuraMagicCircleEmblems(SakuraElementSet.None);
        coarse.Trigger(SakuraElementSet.Wind, 0.02f);
        fine.Trigger(SakuraElementSet.Wind, 0.02f);
        coarse.Advance(0.12f, SakuraElementSet.Fire);
        for (var i = 0; i < 24; i++)
            fine.Advance(0.005f, SakuraElementSet.Fire);
        for (var channel = 0; channel < 4; channel++)
            Assert.InRange(MathF.Abs(coarse.Glow[channel] - fine.Glow[channel]), 0f, 0.0001f);
        Assert.InRange(MathF.Abs(coarse.Dim - fine.Dim), 0f, 0.0001f);
    }

    [Fact]
    public void FreshEntryFlashWaitsForTheForegroundReveal()
    {
        var motion = new SakuraMagicCircleMotion(SakuraMod.SakuraModCode.Character.SourceEraClass.Clear);
        var emblems = new SakuraMagicCircleEmblems(SakuraElementSet.None);
        emblems.Trigger(SakuraElementSet.Wind | SakuraElementSet.Water, motion.TimeUntilForeground);
        Assert.Equal(0.30f, motion.TimeUntilForeground, 5);
        emblems.Advance(0.29f, SakuraElementSet.None);
        Assert.Equal(0f, emblems.Glow.Y);
        emblems.Advance(0.06f, SakuraElementSet.None);
        Assert.Equal(0.8f, emblems.Glow.Y, 4);
        Assert.Equal(0.8f, emblems.Glow.W, 4);
        Assert.Equal(0f, emblems.Glow.X);
        emblems.Advance(0.30f, SakuraElementSet.None);
        Assert.Equal(Vector4.Zero, emblems.Glow);
    }

    [Fact]
    public void FlashOnALitEmblemPeaksAboveItsSteadyGlowThenSettles()
    {
        var emblems = new SakuraMagicCircleEmblems(SakuraElementSet.Fire);
        emblems.Trigger(SakuraElementSet.Fire, 0f);
        emblems.Advance(0.05f, SakuraElementSet.Fire);
        Assert.Equal(1.8f, emblems.Glow.X, 4);
        emblems.Advance(0.30f, SakuraElementSet.Fire);
        Assert.Equal(1f, emblems.Glow.X, 4);
    }

    [Fact]
    public void RetriggerMidFlashRisesFromCurrentBrightness()
    {
        var emblems = new SakuraMagicCircleEmblems(SakuraElementSet.None);
        emblems.Trigger(SakuraElementSet.Earth, 0f);
        emblems.Advance(0.15f, SakuraElementSet.None);
        var before = emblems.Glow.Z;
        Assert.InRange(before, 0.1f, 0.79f);
        emblems.Trigger(SakuraElementSet.Earth, 0f);
        emblems.Advance(0.001f, SakuraElementSet.None);
        Assert.True(emblems.Glow.Z >= before);
        Assert.InRange(emblems.Glow.Z - before, 0f, 0.05f);
    }
}
