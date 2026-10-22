using Godot;
using SakuraMod.SakuraModCode.Cards;

namespace SakuraMod.Tests;

public sealed class SakuraTimeVisualSuite
{
    private static readonly SakuraTimeSweepVfx.Timeline Base = SakuraTimeSweepVfx.Timeline.For(
        new Vector2(500f, 600f), [new Vector2(900f, 640f), new Vector2(1200f, 600f), new Vector2(1500f, 620f)], 0f, 1920f);

    [Fact]
    public void RouteDeclaresTheSweepTheHoldAndTheSharedPrelude()
    {
        var declared = SakuraCardVfxAssets.RunAssetPaths(new SakuraTime()).ToArray();
        RegressionTestHarness.Require(
            SakuraTimeSweepVfx.AssetPaths.All(declared.Contains)
            && TimeFreezeVisual.AssetPaths.All(declared.Contains)
            && CelVfxSession.SharedAssetPaths.All(declared.Contains)
            && SakuraMagicCirclePresenter.AssetPaths.All(declared.Contains)
            && declared.Length == declared.Distinct(StringComparer.Ordinal).Count(),
            "Expected Sakura Time to declare its sweep, its hold, the cel prelude and the magic circle once each.");
    }

    [Fact]
    public void StarPassesEachEnemyInOrderWithinTheSweep()
    {
        var arrivals = Base.Arrivals;
        Assert.Equal(3, arrivals.Count);
        Assert.True(arrivals[0] < arrivals[1] && arrivals[1] < arrivals[2]);
        Assert.All(arrivals, a => Assert.InRange(a, Base.FormSeconds, Base.End0));
        // First Time Stop lands within the same budget as Shade / Aqua.
        Assert.True(arrivals[0] < 0.75f);
    }

    [Fact]
    public void PathOvershootsTheLastEnemyButStaysOnScreen()
    {
        Assert.True(Base.End.X > 1500f);
        var crowded = SakuraTimeSweepVfx.Timeline.For(new Vector2(500f, 600f), [new Vector2(1900f, 600f)], 0f, 1920f);
        Assert.True(crowded.End.X <= 1880f);
        Assert.True(crowded.Arrivals[0] <= crowded.End0);
    }

    [Fact]
    public void MirroredLayoutSweepsLeftAndKeepsTheOrder()
    {
        var mirrored = SakuraTimeSweepVfx.Timeline.For(
            new Vector2(1400f, 600f), [new Vector2(1000f, 600f), new Vector2(700f, 600f)], 0f, 1920f);
        Assert.True(mirrored.End.X < 700f);
        Assert.True(mirrored.Arrivals[0] < mirrored.Arrivals[1]);
    }

    [Fact]
    public void NoTargetsStillGivesAFinitePass()
    {
        var empty = SakuraTimeSweepVfx.Timeline.For(new Vector2(500f, 600f), [], 0f, 1920f);
        Assert.Empty(empty.Arrivals);
        Assert.True(float.IsFinite(empty.End0) && empty.End0 > 0f);
    }

    [Fact]
    public async Task ReachCueNeverWaitsOrSkipsGameplayWithoutPresentation()
    {
        foreach (var enabled in new[] { false, true })
        {
            var created = 0;
            var applied = 0;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            await CelVfxSession.PlayOrResolveAsync<SakuraTimeSweepVfx>(
                enabled,
                "sakura time inert",
                () =>
                {
                    created++;
                    return null;
                },
                static _ => Task.FromResult(true),
                async scope =>
                {
                    var cues = new SakuraTimeSweepVfx.Cues(scope);
                    for (var i = 0; i < 3; i++)
                    {
                        await cues.ReachAsync(new MegaCrit.Sts2.Core.Entities.Creatures.Creature(
                            (MegaCrit.Sts2.Core.Entities.Players.Player)null!, 30, 30));
                        applied++;
                    }
                },
                static _ => { },
                static _ => { });
            watch.Stop();
            Assert.Equal(enabled ? 1 : 0, created);
            Assert.Equal(3, applied);
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(0.2), "Expected the inert reach cue to add no wait.");
        }
    }

    [Fact]
    public void EachTimeStopWaitsForItsOwnReach()
    {
        // Kept as source text (Hail / Blaze / Gale / Aqua precedent, 10-21
        // behavior-tests task): runtime TestMode skips VFX, so the presentation
        // call order has no observable seam. Gameplay parity is the runtime
        // `sakura-time-freeze` scenario.
        var source = File.ReadAllText(RegressionTestHarness.FindRepoFile("SakuraModCode/Cards/ClowSakura/Time.cs"));
        var play = source[source.IndexOf("class SakuraTime", StringComparison.Ordinal)..];
        var reach = play.IndexOf("await cues.ReachAsync(enemy);", StringComparison.Ordinal);
        var apply = play.IndexOf("ApplyBypassingArtifact<ClassicTimePower>", StringComparison.Ordinal);
        var sweepEnds = play.IndexOf("});", StringComparison.Ordinal);
        var voidCard = play.IndexOf("AddVoidToDiscardPile", StringComparison.Ordinal);
        RegressionTestHarness.Require(
            reach > 0 && apply > reach && sweepEnds > apply && voidCard > sweepEnds,
            "Expected each Time Stop after its reach cue, and the Void after the sweep.");
    }

    [Fact]
    public void SweepShaderUsesTheSharedIncludeAndOwnsNoClock()
    {
        foreach (var path in new[] { "SakuraMod/shaders/card_vfx/sakura_time_sweep.gdshader", "SakuraMod/shaders/card_vfx/time_freeze.gdshader" })
        {
            var shader = File.ReadAllText(RegressionTestHarness.FindRepoFile(path));
            RegressionTestHarness.Require(
                shader.Contains("#include \"res://SakuraMod/shaders/card_vfx/cel_vfx.gdshaderinc\"", StringComparison.Ordinal)
                && !shader.Contains("TIME", StringComparison.Ordinal)
                && !shader.Contains("SCREEN_UV", StringComparison.Ordinal),
                $"Expected {path} to use the shared cel include and take time only from its owner.");
        }
    }
}
