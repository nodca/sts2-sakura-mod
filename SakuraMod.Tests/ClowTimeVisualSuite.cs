using SakuraMod.SakuraModCode.Cards;

namespace SakuraMod.Tests;

public sealed class ClowTimeVisualSuite
{
    [Fact]
    public void RouteDeclaresTheHourglassAndTheSharedPrelude()
    {
        var declared = SakuraCardVfxAssets.RunAssetPaths(new ClowTime()).ToArray();
        RegressionTestHarness.Require(
            ClowTimeHourglassVfx.AssetPaths.All(declared.Contains)
            && TimeStasisSandFx.AssetPaths.All(declared.Contains)
            && CelVfxSession.SharedAssetPaths.All(declared.Contains)
            && SakuraMagicCirclePresenter.AssetPaths.All(declared.Contains)
            && !TimeFreezeVisual.AssetPaths.Any(declared.Contains)
            && declared.Length == declared.Distinct(StringComparer.Ordinal).Count(),
            "Expected Clow Time to declare its hourglass, Stasis's sand, the cel prelude and the magic circle once each, and not Sakura Time's hold.");
    }

    [Fact]
    public void DeclaredResourcesExistInTheRepository()
    {
        foreach (var path in ClowTimeHourglassVfx.AssetPaths.Concat(TimeStasisSandFx.AssetPaths))
        {
            Assert.StartsWith("res://SakuraMod/", path, StringComparison.Ordinal);
            var relative = "SakuraMod/" + path["res://SakuraMod/".Length..];
            RegressionTestHarness.Require(
                File.Exists(RegressionTestHarness.FindRepoFile(relative))
                && File.Exists(RegressionTestHarness.FindRepoFile(relative + ".uid")),
                $"Expected {path} and its .uid in the repository.");
        }
    }

    [Fact]
    public void SandFitsInsideTheTurnEnd()
    {
        // Fire-and-forget at the turn end: short enough that a burst never
        // overlaps the next turn's sand.
        Assert.InRange(TimeStasisSandFx.Lifetime, 0.3f, 1.0f);
    }

    [Fact]
    public void HaltLandsWithinTheUsualBlockingBudget()
    {
        Assert.True(ClowTimeHourglassVfx.Timeline.HaltAt <= 0.8f);
        Assert.True(ClowTimeHourglassVfx.Timeline.ScatterAt > ClowTimeHourglassVfx.Timeline.HaltAt);
        Assert.InRange(ClowTimeHourglassVfx.Timeline.PourShare, 0.2f, 0.8f);
    }

    [Fact]
    public async Task HaltCueNeverWaitsOrSkipsGameplayWithoutPresentation()
    {
        foreach (var enabled in new[] { false, true })
        {
            var created = 0;
            var applied = 0;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            await CelVfxSession.PlayOrResolveAsync<ClowTimeHourglassVfx>(
                enabled,
                "clow time inert",
                () =>
                {
                    created++;
                    return null;
                },
                static _ => Task.FromResult(true),
                async scope =>
                {
                    await new ClowTimeHourglassVfx.Cues(scope).HaltAsync();
                    applied++;
                },
                static _ => { },
                static _ => { });
            watch.Stop();
            Assert.Equal(enabled ? 1 : 0, created);
            Assert.Equal(1, applied);
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(0.2), "Expected the inert halt cue to add no wait.");
        }
    }

    [Fact]
    public void StasisWaitsForTheHaltAndTheExtraDrawStaysAfterIt()
    {
        // Kept as source text (Hail / Blaze / Gale / Aqua precedent, 10-21
        // behavior-tests task): runtime TestMode skips VFX, so the presentation
        // call order has no observable seam.
        var source = File.ReadAllText(RegressionTestHarness.FindRepoFile("SakuraModCode/Cards/ClowSakura/Time.cs"));
        var clow = source[source.IndexOf("class ClowTime", StringComparison.Ordinal)..source.IndexOf("class SakuraTime", StringComparison.Ordinal)];
        var halt = clow.IndexOf("await cues.HaltAsync();", StringComparison.Ordinal);
        var stasis = clow.IndexOf("PowerCmd.Apply<ClassicTimeStasisPower>", StringComparison.Ordinal);
        var activated = clow.IndexOf("PlayActivatedCard", StringComparison.Ordinal);
        var replay = clow.IndexOf("await PlayCard(choiceContext, play);", activated, StringComparison.Ordinal);
        var draw = clow.IndexOf("CardPileCmd.Draw", activated, StringComparison.Ordinal);
        RegressionTestHarness.Require(
            halt > 0 && stasis > halt && activated > stasis && replay > activated && draw > replay,
            "Expected Stasis after the halt cue, and the extra draw after the whole play.");
    }

    [Fact]
    public void SandBurstsFollowWhatStasisAlreadyDid()
    {
        // Kept as source text (Hail / Blaze / Gale / Aqua precedent, 10-21
        // behavior-tests task): runtime TestMode skips VFX, so the hook order
        // has no observable seam.
        var source = File.ReadAllText(RegressionTestHarness.FindRepoFile("SakuraModCode/Powers/SourceCards/ClassicTimeStasisPower.cs"));
        var flash = source.IndexOf("Flash();", StringComparison.Ordinal);
        var onPower = source.IndexOf("TimeStasisSandFx.BurstOnPower(power);", StringComparison.Ordinal);
        var preserve = source.IndexOf("SakuraElementStatePower.PreserveAllForNextTurn(Owner);", StringComparison.Ordinal);
        var onPreserved = source.IndexOf("TimeStasisSandFx.BurstOnPreserved(Owner);", StringComparison.Ordinal);
        RegressionTestHarness.Require(
            flash > 0 && onPower > flash && preserve > 0 && onPreserved > preserve
            && !source.Contains("await TimeStasisSandFx", StringComparison.Ordinal),
            "Expected each sand burst right after the Stasis effect it shows, never awaited.");
    }

    [Theory]
    [InlineData("SakuraMod/shaders/card_vfx/clow_time_hourglass.gdshader")]
    [InlineData("SakuraMod/shaders/card_vfx/time_stasis_sand.gdshader")]
    public void ShadersUseTheSharedIncludeAndOwnNoClock(string path)
    {
        var shader = File.ReadAllText(RegressionTestHarness.FindRepoFile(path));
        RegressionTestHarness.Require(
            shader.Contains("#include \"res://SakuraMod/shaders/card_vfx/cel_vfx.gdshaderinc\"", StringComparison.Ordinal)
            && !shader.Contains("TIME", StringComparison.Ordinal)
            && !shader.Contains("SCREEN_UV", StringComparison.Ordinal),
            $"Expected {path} to use the shared cel include and take time only from its driver.");
    }
}
