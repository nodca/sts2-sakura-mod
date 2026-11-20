using System.Text.Json;
using MegaCrit.Sts2.Core.Entities.Cards;
using SakuraMod.SakuraModCode.FourthAct.Dark.Cards;
using SakuraMod.SakuraModCode.FourthAct.Water.CardState;

public sealed class FourthActCardAfflictionSuite
{
    [Fact]
    public void FrozenProjectionNeverGatesPlay()
    {
        var frozen = new FrozenAffliction();
        var card = new MicroLight();
        Assert.True(frozen.ShouldPlay(card, AutoPlayType.None));
        Assert.True(frozen.ShouldPlay(card, AutoPlayType.Default));
    }

    [Fact]
    public void AfflictionLocalizationExistsInAllLocales()
    {
        foreach (var locale in new[] { "zhs", "eng", "jpn" })
        {
            var text = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(
                RegressionTestHarness.FindRepoFile($"SakuraMod/localization/{locale}/afflictions.json")))!;
            foreach (var name in new[] { "SLEEPING", "BURIED", "FROZEN" })
            {
                Assert.Contains($"SAKURA_MOD_AFFLICTION_{name}_AFFLICTION.title", text.Keys);
                Assert.Contains($"SAKURA_MOD_AFFLICTION_{name}_AFFLICTION.description", text.Keys);
            }
        }
    }
}
