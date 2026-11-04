using System.Globalization;
using System.Text.RegularExpressions;
using SakuraMod.SakuraModCode;
using SakuraMod.SakuraModCode.Cards;

public sealed class RemindVfxSuite
{
    private const string DialShaderPath = "SakuraMod/shaders/card_vfx/remind_dial.gdshader";
    private const string RelayShaderPath = "SakuraMod/shaders/card_vfx/memory_relay.gdshader";
    private const string PlatePath = "scripts/render_remind_vfx.gd";

    [Fact]
    public void RemindRouteDeclaresTheDialAndRelayShadersBesideTheMagicCircle()
    {
        var declared = SakuraCardVfxAssets.RunAssetPaths(new Remind()).ToArray();
        RegressionTestHarness.Require(
            declared.Contains(RemindVfx.ShaderPath)
            && declared.Contains(SakuraMemoryRecallVisual.RelayShaderPath)
            && declared.Except(RemindVfx.AssetPaths).All(SakuraMagicCirclePresenter.AssetPaths.Contains)
            && declared.Length == declared.Distinct(StringComparer.Ordinal).Count(),
            "Expected Remind to declare the dial and relay shaders plus only the shared magic circle.");

        var character = File.ReadAllText(RegressionTestHarness.FindRepoFile("SakuraModCode/Character/ClassicSakura.cs"));
        RegressionTestHarness.Require(
            character.Contains(".. SakuraMemoryRecallVisual.AssetPaths,", StringComparison.Ordinal),
            "Expected the relay shader preloaded with the character: Dreaming and the Frog Raincoat recall too.");
    }

    [Fact]
    public void ShadersUseTheSharedIncludeAndOwnNoClock()
    {
        foreach (var path in new[] { DialShaderPath, RelayShaderPath })
        {
            var shader = File.ReadAllText(RegressionTestHarness.FindRepoFile(path));
            RegressionTestHarness.Require(
                shader.Contains("#include \"res://SakuraMod/shaders/card_vfx/cel_vfx.gdshaderinc\"", StringComparison.Ordinal)
                && !Regex.IsMatch(shader, @"\bTIME\b")
                && !shader.Contains("SCREEN_UV", StringComparison.Ordinal),
                $"Expected {path} to use the shared include and take time from its caller.");
        }
    }

    [Fact]
    public async Task CuesNeverWaitOrSkipGameplayWithoutPresentation()
    {
        foreach (var enabled in new[] { false, true })
        {
            var created = 0;
            var recalls = 0;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            await CelVfxSession.PlayOrResolveAsync<RemindVfx>(
                enabled,
                "remind inert",
                () =>
                {
                    created++;
                    return null;
                },
                static _ => Task.FromResult(true),
                async scope =>
                {
                    var cues = new RemindVfx.Cues(scope);
                    await cues.ReachMemoryAsync(3);
                    recalls++;
                    cues.Split(new Time());
                },
                static _ => { },
                static _ => { });
            watch.Stop();
            Assert.Equal(enabled ? 1 : 0, created);
            Assert.Equal(1, recalls);
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(0.2), "Expected the inert reach cue to add no wait.");
        }
    }

    [Fact]
    public void RecallWaitsForTheThreadsAndTheSplitFollowsTheNewCard()
    {
        var remind = File.ReadAllText(RegressionTestHarness.FindRepoFile("SakuraModCode/Cards/Transparent/Remind.cs"));
        var reach = remind.IndexOf("await beforeRecall(cards.Count);", StringComparison.Ordinal);
        var recall = remind.IndexOf("SakuraMemoryPile.RecallToHand(owner, cards, upgradeCopies)", StringComparison.Ordinal);
        var add = remind.IndexOf("SakuraGeneratedCardLifecycle.AddGeneratedCardToHand(", StringComparison.Ordinal);
        var split = remind.IndexOf("cues.Split(selected);", StringComparison.Ordinal);
        RegressionTestHarness.Require(
            remind.Contains("RemindVfx.PlayOrResolveAsync(this, Owner.Creature,", StringComparison.Ordinal)
            && remind.Contains("beforeRecall: cues.ReachMemoryAsync", StringComparison.Ordinal)
            && reach >= 0 && recall > reach
            && add >= 0 && split > add,
            "Expected the recall to resolve on the threads' arrival and the split to stamp the card already in hand.");
    }

    [Fact]
    public void ThreadsAndRelaysStayBounded()
    {
        RegressionTestHarness.Require(
            RemindVfx.ArriveTime(1f, 40) == RemindVfx.ArriveTime(1f, RemindVfx.MaxThreads)
            && RemindVfx.ArriveTime(1f, 1) == 1f + RemindVfx.ThreadReach,
            "Expected at most five dial threads, and one thread to land after a single reach.");

        var last = SakuraMemoryRecallVisual.RelayDelay(29, 30) - SakuraMemoryRecallVisual.RelayDelay(0, 30);
        RegressionTestHarness.Require(
            last <= SakuraMemoryRecallVisual.CardStaggerCap + 1e-4f
            && SakuraMemoryRecallVisual.RelayDelay(0, 1) == SakuraMemoryRecallVisual.RelayLead,
            "Expected the relay stagger across a large recall capped, and a lone card to relay one frame after the add.");
    }

    [Fact]
    public void RuntimeBeatsMirrorThePlate()
    {
        var plate = File.ReadAllText(RegressionTestHarness.FindRepoFile(PlatePath));
        var expected = new (string Name, float Value)[]
        {
            ("RING_START", RemindVfx.RingStart),
            ("MERGE", RemindVfx.Merge),
            ("THREAD_REACH", RemindVfx.ThreadReach),
            ("THREAD_STAGGER", RemindVfx.ThreadStagger),
            ("MAX_THREADS", RemindVfx.MaxThreads),
            ("OUTRO_AFTER_ARRIVE", RemindVfx.OutroAfterArrive),
            ("OUTRO_AFTER_MERGE", RemindVfx.OutroAfterMerge),
            ("OUTRO_AFTER_SPLIT", RemindVfx.OutroAfterSplit),
            ("SHATTER", RemindVfx.Shatter),
            ("CARD_STAGGER", SakuraMemoryRecallVisual.CardStagger),
            ("CARD_STAGGER_CAP", SakuraMemoryRecallVisual.CardStaggerCap),
            ("RELAY_REACH", SakuraMemoryRecallVisual.RelayReach),
            ("RELAY_TAIL", SakuraMemoryRecallVisual.RelayTail),
            ("LAND_RIPPLE", SakuraMemoryRecallVisual.LandRipple),
            ("DEVELOP", SakuraMemoryRecallVisual.InPlaceDevelop),
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
