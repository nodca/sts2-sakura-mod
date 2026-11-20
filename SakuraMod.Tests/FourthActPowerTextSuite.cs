using System.Text.Json;
using System.Text.RegularExpressions;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.FourthAct.Dark.Powers;
using SakuraMod.SakuraModCode.FourthAct.Earth.Powers;
using SakuraMod.SakuraModCode.FourthAct.Fire.Powers;
using SakuraMod.SakuraModCode.FourthAct.Water.Powers;
using SakuraMod.SakuraModCode.FourthAct.Wind.Powers;

public sealed class FourthActPowerTextSuite
{
    private static readonly string[] Locales = ["zhs", "eng", "jpn"];

    public static TheoryData<PowerModel> PowersWithTunableText => new()
    {
        new WindBindPower(),
        new FloatDrawCounterPower(),
        new DarknessPower(),
        new DarkBattlePower(),
        new DrenchedPower(),
        new SwordBlockFeedingPower(),
        new ShadowEchoPower(),
    };

    [Theory]
    [MemberData(nameof(PowersWithTunableText))]
    public void SmartDescriptionReadsEveryRuleValueAndDescriptionMatchesIt(PowerModel power)
    {
        var entry = power.Id.Entry.StartsWith("SAKURA_MOD_POWER_", StringComparison.Ordinal)
            ? power.Id.Entry
            : $"SAKURA_MOD_POWER_{power.Id.Entry}";
        foreach (var locale in Locales)
        {
            var powers = ReadJson($"SakuraMod/localization/{locale}/powers.json");
            var smart = powers[$"{entry}.smartDescription"];
            var description = powers[$"{entry}.description"];
            foreach (var (name, variable) in power.DynamicVars)
            {
                Assert.Contains($"{{{name}", smart);
                if (name == "Cards")
                    continue;
                Assert.Contains($"[blue]{variable.BaseValue:0.##}", description);
            }
        }
    }

    [Fact]
    public void SovereigntyPowersShareOneNameAndSentenceShape()
    {
        var zhs = ReadJson("SakuraMod/localization/zhs/powers.json");
        string[] names = ["WIND", "WATER", "FIRE", "EARTH", "DARK", "LIGHT"];
        string[] titles = ["风之支配", "水之支配", "火之支配", "地之支配", "暗之支配", "光之支配"];
        for (var index = 0; index < names.Length; index++)
        {
            var key = $"SAKURA_MOD_POWER_{names[index]}_SOVEREIGNTY_POWER";
            Assert.Equal(titles[index], zhs[$"{key}.title"]);
            Assert.StartsWith("玩家无法获得临时或永久的[gold]", zhs[$"{key}.description"]);
        }
    }

    [Fact]
    public void LightSovereigntyOwnsTheFireAndEarthLock()
    {
        Assert.True(RegressionTestHarness.DeclaresMethod<LightSovereigntyPower>("TryModifyPowerAmountReceived"));
        Assert.False(RegressionTestHarness.DeclaresMethod<LightBattlePower>("TryModifyPowerAmountReceived"));
        Assert.Equal(
            SakuraElementSet.Fire | SakuraElementSet.Earth,
            SakuraElementState.LocksFromSovereignty(false, false, false, false, light: true));
    }

    [Fact]
    public void FourthActLocalizationKeySetsMatchAcrossLocales()
    {
        foreach (var file in new[] { "powers.json", "intents.json", "afflictions.json" })
        {
            var zhs = ReadJson($"SakuraMod/localization/zhs/{file}").Keys.Where(IsFourthActKey).ToHashSet();
            var eng = ReadJson($"SakuraMod/localization/eng/{file}").Keys.Where(IsFourthActKey).ToHashSet();
            var jpn = ReadJson($"SakuraMod/localization/jpn/{file}").Keys.Where(IsFourthActKey).ToHashSet();
            Assert.Equal(zhs.Order(), eng.Order());
            Assert.Equal(zhs.Order(), jpn.Order());
        }
    }

    private static bool IsFourthActKey(string key) =>
        Regex.IsMatch(key, "WIND|WINDY|FLOAT|ILLUSION|DARK|WATER|DRENCHED|SWORD|FIRE|LIGHT|LIBRA|EARTH|SHADOW|WOOD|SUMMON|STEAL_BLOCK|SLEEPING");

    private static Dictionary<string, string> ReadJson(string relativePath) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(RegressionTestHarness.FindRepoFile(relativePath)))!;
}
