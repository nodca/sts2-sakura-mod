using System.Text.Json;
using SakuraMod.SakuraModCode.Events.Models;
using SakuraMod.SakuraModCode.Relics;

public sealed class TomoyoAncientCostumesSuite
{
    private static readonly Type[] CostumeRelicTypes =
    [
        typeof(ClassicRedCapeRelic),
        typeof(ClassicPinkTransformationCostumeRelic),
        typeof(ClassicFrogRaincoatRelic)
    ];

    [Fact]
    public void EventUsesTheRegisteredBackgroundAndThreeCostumeRelics()
    {
        var source = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Events/Models/ClassicTomoyoAncientCostumes.cs"));
        var registration = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Events/SakuraEventRegistration.cs"));
        var availability = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Events/TomoyoAncientAvailability.cs"));

        Assert.Contains("tomoyo_ancient_costumes_background.tscn", source, StringComparison.Ordinal);
        Assert.Contains("SakuraStarterCompatibility.IsKinomotoSakuraRun(runState)", source, StringComparison.Ordinal);
        Assert.Contains("HarmonyPatch(typeof(Hive), nameof(Hive.GetUnlockedAncients)", availability, StringComparison.Ordinal);
        Assert.Contains("HarmonyPriority(Priority.Last)", availability, StringComparison.Ordinal);
        Assert.Contains("RunManager.Instance.DebugOnlyGetState()", availability, StringComparison.Ordinal);
        Assert.Contains("SakuraStarterCompatibility.IsKinomotoSakuraRun(runState)", availability, StringComparison.Ordinal);
        Assert.Contains("ancient is not ClassicTomoyoAncientCostumes", availability, StringComparison.Ordinal);
        Assert.Contains("tomoyo_ancient_icon.png", source, StringComparison.Ordinal);
        Assert.Contains("tomoyo_ancient_icon_outline.png", source, StringComparison.Ordinal);
        Assert.Contains("HoverTipFactory.FromRelic(relic)", source, StringComparison.Ordinal);
        Assert.Contains(".WithRelic(relic)", source, StringComparison.Ordinal);
        Assert.Contains("RegisterActAncient<Hive, ClassicTomoyoAncientCostumes>()", registration, StringComparison.Ordinal);
        Assert.DoesNotContain("RegisterSharedAncient<ClassicTomoyoAncientCostumes>()", registration, StringComparison.Ordinal);
        foreach (var relicType in CostumeRelicTypes)
            Assert.Equal(2, source.Split($"CreateCostumeRelicOption<{relicType.Name}>()", StringSplitOptions.None).Length - 1);
    }

    [Theory]
    [InlineData("eng")]
    [InlineData("zhs")]
    public void EventLocalizationCoversIdentityDialoguePagesAndRelicOptions(string locale)
    {
        var relativePath = $"SakuraMod/localization/{locale}/ancients.json";
        var entries = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            File.ReadAllText(RegressionTestHarness.FindRepoFile(relativePath)))
            ?? throw new InvalidOperationException($"Could not parse {relativePath}.");
        var eventEntry = RegressionTestHarness.RegisteredModelEntry(typeof(ClassicTomoyoAncientCostumes));

        foreach (var suffix in new[]
                 {
                     ".title",
                     ".epithet",
                     ".talk.firstVisitEver.0-0.ancient",
                     ".talk.firstVisitEver.0-0.next",
                     ".talk.firstVisitEver.0-1.char",
                     ".talk.firstVisitEver.0-1.next",
                     ".talk.firstVisitEver.0-2.ancient",
                     ".talk.SAKURA_MOD_CHARACTER_CLASSIC_SAKURA.0-0.ancient",
                     ".talk.ANY.0-0r.ancient",
                     ".pages.INITIAL.description",
                     ".pages.DONE.description"
                 })
        {
            Assert.True(entries.ContainsKey(eventEntry + suffix), $"Missing {eventEntry + suffix} in {relativePath}.");
        }

        foreach (var relicType in CostumeRelicTypes)
        {
            var optionPrefix = $"{eventEntry}.pages.INITIAL.options.{RegressionTestHarness.RegisteredModelEntry(relicType)}";
            Assert.True(entries.ContainsKey(optionPrefix + ".title"), $"Missing {optionPrefix}.title in {relativePath}.");
            Assert.True(entries.ContainsKey(optionPrefix + ".description"), $"Missing {optionPrefix}.description in {relativePath}.");
        }

        if (locale == "zhs")
        {
            Assert.Equal("小樱，终于找到你了！我还带来了几套专门为你准备的战斗服。", entries[$"{eventEntry}.talk.firstVisitEver.0-0.ancient"].GetString());
            Assert.Equal("选择库洛牌战斗服。", entries[$"{eventEntry}.pages.INITIAL.options.{RegressionTestHarness.RegisteredModelEntry(typeof(ClassicRedCapeRelic))}.title"].GetString());
            Assert.Equal("获得红披风战斗服。", entries[$"{eventEntry}.pages.INITIAL.options.{RegressionTestHarness.RegisteredModelEntry(typeof(ClassicRedCapeRelic))}.description"].GetString());
            Assert.Equal("选择小樱牌战斗服。", entries[$"{eventEntry}.pages.INITIAL.options.{RegressionTestHarness.RegisteredModelEntry(typeof(ClassicPinkTransformationCostumeRelic))}.title"].GetString());
            Assert.Equal("获得粉色战斗服。", entries[$"{eventEntry}.pages.INITIAL.options.{RegressionTestHarness.RegisteredModelEntry(typeof(ClassicPinkTransformationCostumeRelic))}.description"].GetString());
            Assert.Equal("选择透明牌战斗服。", entries[$"{eventEntry}.pages.INITIAL.options.{RegressionTestHarness.RegisteredModelEntry(typeof(ClassicFrogRaincoatRelic))}.title"].GetString());
            Assert.Equal("获得青蛙雨衣。", entries[$"{eventEntry}.pages.INITIAL.options.{RegressionTestHarness.RegisteredModelEntry(typeof(ClassicFrogRaincoatRelic))}.description"].GetString());
        }
    }
}
