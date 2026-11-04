using System.Globalization;
using System.Text.RegularExpressions;
using SakuraMod.SakuraModCode;
using SakuraMod.SakuraModCode.Cards;

public sealed class RewindVfxSuite
{
    private const string ShaderPath = "SakuraMod/shaders/card_vfx/rewind_watch.gdshader";
    private const string PlatePath = "scripts/render_rewind_vfx.gd";

    [Fact]
    public void RewindRouteDeclaresTheWatchShaderBesideTheMagicCircle()
    {
        var declared = SakuraCardVfxAssets.RunAssetPaths(new Rewind()).ToArray();
        RegressionTestHarness.Require(
            declared.Contains(RewindVfx.ShaderPath)
            && declared.Except(RewindVfx.AssetPaths).All(SakuraMagicCirclePresenter.AssetPaths.Contains)
            && declared.Length == declared.Distinct(StringComparer.Ordinal).Count(),
            "Expected Rewind to declare the watch shader plus only the shared magic circle.");
    }

    [Fact]
    public void ShaderUsesTheSharedIncludeAndOwnsNoClock()
    {
        var shader = File.ReadAllText(RegressionTestHarness.FindRepoFile(ShaderPath));
        RegressionTestHarness.Require(
            shader.Contains("#include \"res://SakuraMod/shaders/card_vfx/cel_vfx.gdshaderinc\"", StringComparison.Ordinal)
            && !Regex.IsMatch(shader, @"\bTIME\b")
            && !shader.Contains("SCREEN_UV", StringComparison.Ordinal),
            "Expected the Rewind shader to use the shared include and take time from the session.");
    }

    [Fact]
    public async Task CuesNeverWaitOrSkipGameplayWithoutPresentation()
    {
        foreach (var enabled in new[] { false, true })
        {
            var created = 0;
            var moves = 0;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            await CelVfxSession.PlayOrResolveAsync<RewindVfx>(
                enabled,
                "rewind inert",
                () =>
                {
                    created++;
                    return null;
                },
                static _ => Task.FromResult(true),
                async scope =>
                {
                    var cues = new RewindVfx.Cues(scope);
                    await cues.StopAsync();
                    var card = new Time();
                    cues.Mark(card, 0, 1, free: true);
                    moves++;
                    RewindVfx.Cues.Unmark(card);
                },
                static _ => { },
                static _ => { });
            watch.Stop();
            Assert.Equal(enabled ? 1 : 0, created);
            Assert.Equal(1, moves);
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(0.2), "Expected the inert stop cue to add no wait.");
        }
    }

    [Fact]
    public void MovesWaitForTheStopAndAlwaysClearTheirMark()
    {
        var rewind = File.ReadAllText(RegressionTestHarness.FindRepoFile("SakuraModCode/Cards/Transparent/Rewind.cs"));
        var select = rewind.IndexOf("SakuraActions.SelectUpToFromCards(", StringComparison.Ordinal);
        var stop = rewind.IndexOf("await cues.StopAsync();", StringComparison.Ordinal);
        var mark = rewind.IndexOf("cues.Mark(card, index, selected.Count, activation.IsActive);", StringComparison.Ordinal);
        var move = rewind.IndexOf("await SakuraActions.MoveExistingCardToHand(this, card);", StringComparison.Ordinal);
        var unmark = rewind.IndexOf("RewindVfx.Cues.Unmark(card);", StringComparison.Ordinal);
        var free = rewind.IndexOf("card.SetToFreeThisTurn();", StringComparison.Ordinal);
        RegressionTestHarness.Require(
            rewind.Contains("RewindVfx.PlayOrResolveAsync(this, Owner.Creature,", StringComparison.Ordinal)
            && select >= 0 && stop > select && mark > stop && move > mark && unmark > move && free > unmark
            && rewind.Contains("finally", StringComparison.Ordinal),
            "Expected the selection first, then the stop cue, each move marked and unmarked in finally, then the free cost.");

        var patches = File.ReadAllText(RegressionTestHarness.FindRepoFile("SakuraModCode/Cards/SakuraCardVisualPatches.cs"));
        RegressionTestHarness.Require(
            patches.Contains("RewindVfx.AfterNativeCardNodeCreated(card, __result, targetPileType, owningPlayerIsLocal);", StringComparison.Ordinal),
            "Expected Rewind returns to hook the same native card node creation as the Memory recall.");
    }

    [Fact]
    public void StreamStaggerStaysBounded()
    {
        var last = RewindVfx.StreamDelay(29, 30) - RewindVfx.StreamDelay(0, 30);
        RegressionTestHarness.Require(
            last <= RewindVfx.CardStaggerCap + 1e-4f
            && RewindVfx.StreamDelay(0, 1) == RewindVfx.StreamLead,
            "Expected the stream stagger across many cards capped, and a lone card to leave one frame after the stop.");
    }

    [Fact]
    public void RuntimeBeatsMirrorThePlate()
    {
        var plate = File.ReadAllText(RegressionTestHarness.FindRepoFile(PlatePath));
        var expected = new (string Name, float Value)[]
        {
            ("WATCH_POP_START", RewindVfx.WatchPopStart),
            ("WATCH_POP", RewindVfx.WatchPop),
            ("CROWN_CLICK", RewindVfx.CrownClick),
            ("SPIN_START", RewindVfx.SpinStart),
            ("SPIN_END", RewindVfx.SpinEnd),
            ("PULSE", RewindVfx.Pulse),
            ("CARD_STAGGER", RewindVfx.CardStagger),
            ("CARD_STAGGER_CAP", RewindVfx.CardStaggerCap),
            ("STREAM_REACH", RewindVfx.StreamReach),
            ("STREAM_TAIL", RewindVfx.StreamTail),
            ("ASSEMBLE", RewindVfx.Assemble),
            ("UNBURN", RewindVfx.Unburn),
            ("SHEEN", RewindVfx.Sheen),
            ("SPARK_REVEAL", RewindVfx.SparkReveal),
            ("SPARK_FLIGHT", RewindVfx.SparkFlight),
            ("COST_POP", RewindVfx.CostPop),
            ("OUTRO_AFTER_STOP", RewindVfx.OutroAfterStop),
            ("OUTRO_AFTER_RESTORE", RewindVfx.OutroAfterRestore),
            ("VANISH", RewindVfx.Vanish),
        };
        foreach (var (name, value) in expected)
        {
            var match = Regex.Match(plate, $@"^const {name} := ([0-9.]+)\s*$", RegexOptions.Multiline);
            RegressionTestHarness.Require(
                match.Success && Math.Abs(float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) - value) < 1e-4f,
                $"Expected the runtime {name} to match {PlatePath}.");
        }
    }
}
