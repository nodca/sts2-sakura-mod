using System.Text.Json;
using MegaCrit.Sts2.Core.Entities.Cards;
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
    public void LocalizedDarknessAndMicroLightTextExistInAllLocales()
    {
        foreach (var locale in new[] { "eng", "zhs", "jpn" })
        {
            var cards = ReadJson($"SakuraMod/localization/{locale}/cards.json");
            var powers = ReadJson($"SakuraMod/localization/{locale}/powers.json");
            Assert.Contains("SAKURA_MOD_CARD_MICRO_LIGHT.description", cards.Keys);
            Assert.Contains("SAKURA_MOD_POWER_DARKNESS_POWER.title", powers.Keys);
            Assert.Contains("SAKURA_MOD_POWER_DARK_CONFINEMENT_SELECTION_POWER.selectionPrompt", powers.Keys);
        }
    }

    private static Dictionary<string, string> ReadJson(string relativePath) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(RegressionTestHarness.FindRepoFile(relativePath)))
        ?? throw new InvalidOperationException($"Could not parse {relativePath}.");
}
