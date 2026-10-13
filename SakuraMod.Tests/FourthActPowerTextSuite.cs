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
    private static readonly string[] Locales = ["zhs", "eng"];

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

        var lockSources = File.ReadAllText(RegressionTestHarness.FindRepoFile("SakuraModCode/Character/SakuraElementState.cs"))
            + File.ReadAllText(RegressionTestHarness.FindRepoFile("SakuraModCode/Character/SakuraElementStateHud.cs"));
        Assert.DoesNotContain("LightBattlePower", lockSources);
        Assert.Contains("HasPower<LightSovereigntyPower>()", lockSources);

        var light = File.ReadAllText(RegressionTestHarness.FindRepoFile("SakuraModCode/FourthAct/Fire/Models/FireMonsters.cs"));
        Assert.Contains("PowerCmd.Apply<LightSovereigntyPower>", light);
        Assert.Contains("PowerCmd.Apply<LightBattlePower>", light);
    }

    [Theory]
    [InlineData("powers.json")]
    [InlineData("intents.json")]
    [InlineData("afflictions.json")]
    public void FourthActTextAvoidsImplementationWordingAndRetiredTerms(string file)
    {
        var zhs = ReadJson($"SakuraMod/localization/zhs/{file}");
        string[] retired = ["内部状态", "份额", "正数未格挡", "元素主权", "召唤疾", "沉睡手牌"];
        foreach (var (key, value) in zhs)
            Assert.DoesNotContain(retired, term => value.Contains(term, StringComparison.Ordinal) && !key.Contains("CONFINEMENT_AFFLICTION"));

        var eng = ReadJson($"SakuraMod/localization/eng/{file}");
        Assert.DoesNotContain(eng.Values, value => value.Contains("Internal", StringComparison.Ordinal)
            || value.Contains("Positive unblocked", StringComparison.Ordinal)
            || value.Contains("share is spent", StringComparison.Ordinal));
    }

    [Fact]
    public void WaterFrozenNoLongerSharesTheFreezeName()
    {
        var zhs = ReadJson("SakuraMod/localization/zhs/powers.json");
        Assert.Equal("冰封", zhs["SAKURA_MOD_POWER_WATER_FROZEN_POWER.title"]);
        Assert.NotEqual(zhs["SAKURA_MOD_POWER_WATER_FROZEN_POWER.title"], zhs["SAKURA_MOD_POWER_CLASSIC_FREEZE_POWER.title"]);
    }

    [Fact]
    public void FourthActLocalizationKeySetsMatchAcrossLocales()
    {
        foreach (var file in new[] { "powers.json", "intents.json", "afflictions.json" })
        {
            var zhs = ReadJson($"SakuraMod/localization/zhs/{file}").Keys.Where(IsFourthActKey).ToHashSet();
            var eng = ReadJson($"SakuraMod/localization/eng/{file}").Keys.Where(IsFourthActKey).ToHashSet();
            Assert.Equal(zhs.Order(), eng.Order());
        }
    }

    [Theory]
    [InlineData("SakuraModCode/FourthAct/Earth/Powers/EarthCombatPowers.cs", "WoodRootedPower")]
    [InlineData("SakuraModCode/FourthAct/Earth/Powers/EarthCombatPowers.cs", "EarthySedimentPower")]
    [InlineData("SakuraModCode/FourthAct/Water/Powers/WaterCombatPowers.cs", "WaterReservoirPower")]
    [InlineData("SakuraModCode/FourthAct/Fire/Powers/FireCombatPowers.cs", "SwordBlockFeedingPower")]
    public void PerPlayerPowersDisplayThePreviewPlayersValue(string path, string powerName)
    {
        var source = File.ReadAllText(RegressionTestHarness.FindRepoFile(path));
        var start = source.IndexOf($"public sealed class {powerName}", StringComparison.Ordinal);
        var end = source.IndexOf("public sealed class ", start + 1, StringComparison.Ordinal);
        var body = end < 0 ? source[start..] : source[start..end];
        var display = Regex.Match(body, @"public override int DisplayAmount =>[^;]*;").Value;
        Assert.Contains("FourthActCombatRules.PreviewPlayer", display);
    }

    private static bool IsFourthActKey(string key) =>
        Regex.IsMatch(key, "WIND|WINDY|FLOAT|ILLUSION|DARK|WATER|DRENCHED|SWORD|FIRE|LIGHT|LIBRA|EARTH|SHADOW|WOOD|SUMMON|STEAL_BLOCK|SLEEPING");

    private static Dictionary<string, string> ReadJson(string relativePath) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(RegressionTestHarness.FindRepoFile(relativePath)))!;
}
