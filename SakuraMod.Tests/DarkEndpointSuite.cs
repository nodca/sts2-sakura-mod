using System.Text.Json;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Rooms;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.FourthAct.Dark;
using SakuraMod.SakuraModCode.FourthAct.Dark.Cards;
using SakuraMod.SakuraModCode.FourthAct.Dark.Encounters;
using SakuraMod.SakuraModCode.FourthAct.Dark.Models;
using SakuraMod.SakuraModCode.FourthAct.Dark.Powers;

public sealed class DarkEndpointSuite
{
    [Fact]
    public void DarknessRulesMatchFinalValues()
    {
        Assert.Equal((520, 545, 2, 5, 3, 0.2m, 5, 12),
            (DarkEnemyRules.BaseHp, DarkEnemyRules.ToughHp, DarkEnemyRules.MicroLightsPerDraw,
             DarkEnemyRules.DarknessMaximum, DarkEnemyRules.DarknessReset,
             DarkEnemyRules.DarknessDamageReductionPerLayer,
             DarkEnemyRules.DarknessAttackBonusPerLayer, DarkEnemyRules.NightBlock));
        Assert.Equal([1, 1, 2, 5, 5], new[] { -1, 0, 2, 5, 8 }.Select(DarkEnemyRules.ClampDarkness));
        Assert.Equal([0.8m, 0.6m, 0m], new[] { 1, 2, 5 }.Select(DarkEnemyRules.DarknessDamageMultiplier));
        Assert.Equal(16, DarkEnemyRules.AttackDamage(DarkRegularAction.Confinement, 1, false));
        Assert.Equal(31, DarkEnemyRules.AttackDamage(DarkRegularAction.Confinement, 4, false));
        Assert.Equal(47, DarkEnemyRules.AttackDamage(DarkRegularAction.NonConfinement, 5, true));
        Assert.True(DarkEnemyRules.ShouldUseUltimate(5));
        Assert.False(DarkEnemyRules.ShouldUseUltimate(4));
        Assert.Equal(DarkRegularAction.NonConfinement, DarkEnemyRules.Toggle(DarkRegularAction.Confinement));
    }

    [Fact]
    public void MicroLightIsRetainedAndLowersDarkness()
    {
        var card = new MicroLight();
        Assert.Contains(CardKeyword.Retain, card.CanonicalKeywords);
        Assert.Contains(CardKeyword.Exhaust, card.CanonicalKeywords);
        Assert.Equal(-1, card.DynamicVars[nameof(DarknessPower)].IntValue);
        Assert.IsAssignableFrom<ISakuraForgottenImmune>(card);
        Assert.Contains(typeof(MicroLight), SakuraContentRegistration.AllCardTypesForRegistration());
        Assert.Contains(typeof(MicroLight), SakuraContentRegistration.ClearLayoutOnlyCardTypes);
    }

    [Fact]
    public void DarkEncounterAndPowerContractsRemainRegistered()
    {
        var encounter = new DarkEncounter();
        Assert.Equal(RoomType.Boss, encounter.RoomType);
        Assert.False(encounter.ShouldGiveRewards);
        Assert.Equal(["BOSS"], encounter.Slots);
        Assert.Equal(typeof(DarkEncounter), DarkEnemyCatalog.EndpointEncounterType);
        Assert.Equal([typeof(DarkMonster)], DarkEnemyCatalog.MonsterTypes);
        Assert.All(new MegaCrit.Sts2.Core.Models.PowerModel[]
        {
            new DarknessPower(), new DarkSovereigntyPower(), new DarkBattlePower(),
            new DarkConfinementSelectionPower()
        }, static power => Assert.False(power.ShouldScaleInMultiplayer));
    }

    [Fact]
    public void LocalizedDarknessAndMicroLightTextExistInBothLocales()
    {
        foreach (var locale in new[] { "eng", "zhs" })
        {
            var cards = ReadJson($"SakuraMod/localization/{locale}/cards.json");
            var powers = ReadJson($"SakuraMod/localization/{locale}/powers.json");
            Assert.Contains("SAKURA_MOD_CARD_MICRO_LIGHT.description", cards.Keys);
            Assert.Contains("SAKURA_MOD_POWER_DARKNESS_POWER.title", powers.Keys);
            Assert.Contains("SAKURA_MOD_POWER_DARK_CONFINEMENT_SELECTION_POWER.selectionPrompt", powers.Keys);
            Assert.DoesNotContain(cards["SAKURA_MOD_CARD_MICRO_LIGHT.description"], locale == "zhs" ? "暗幕" : "Dark Veil");
            Assert.DoesNotContain(powers.Keys, key => key.Contains("DARK_VEIL_POWER", StringComparison.Ordinal));
            Assert.DoesNotContain(powers.Keys, key => key.Contains("DARK_NIGHT_POWER", StringComparison.Ordinal));
        }
    }

    private static Dictionary<string, string> ReadJson(string relativePath) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(RegressionTestHarness.FindRepoFile(relativePath)))
        ?? throw new InvalidOperationException($"Could not parse {relativePath}.");
}
