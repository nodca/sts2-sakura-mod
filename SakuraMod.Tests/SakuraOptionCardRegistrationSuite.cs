using MegaCrit.Sts2.Core.Entities.Cards;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode;

public sealed class SakuraOptionCardRegistrationSuite
{
    private static readonly IReadOnlyList<Type> ExpectedOptionCardTypes =
    [
        typeof(ChoiceDrawChoice),
        typeof(ChoiceManifestChoice),
        typeof(TrueOrFalseDrawChoice),
        typeof(TrueOrFalseEnergyChoice),
        typeof(ExchangeMemoryChoice),
        typeof(ExchangeExhaustChoice),
        typeof(ExchangeDrawChoice),
        typeof(ExchangeDiscardChoice)
    ];

    private static readonly IReadOnlyDictionary<Type, string> ExpectedEnglishNames =
        new Dictionary<Type, string>
        {
            [typeof(ChoiceManifestChoice)] = "MANIFEST",
            [typeof(ChoiceDrawChoice)] = "DRAW",
            [typeof(TrueOrFalseDrawChoice)] = "FALSE",
            [typeof(TrueOrFalseEnergyChoice)] = "TRUE",
            [typeof(ExchangeMemoryChoice)] = "MEMORY",
            [typeof(ExchangeExhaustChoice)] = "EXHAUST",
            [typeof(ExchangeDrawChoice)] = "DRAW",
            [typeof(ExchangeDiscardChoice)] = "DISCARD"
        };

    [Fact]
    public void OptionCardsAreRegisteredButExcludedFromTheRewardCatalog()
    {
        var optionCardTypes = SakuraOptionCardCatalog.CardTypes;
        var discoveredOptionCardTypes = typeof(SakuraOptionCard).Assembly.GetTypes()
            .Where(static type =>
                !type.IsAbstract
                && typeof(SakuraOptionCard).IsAssignableFrom(type))
            .ToHashSet();
        var registeredCardTypes = SakuraContentRegistration.AllCardTypesForRegistration().ToHashSet();
        var rewardCatalogTypes = ClassicSakuraCardPool.AllCardTypesForPool().ToHashSet();
        var registrationSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraContentRegistration.cs"));

        RegressionTestHarness.Require(
            optionCardTypes.SequenceEqual(ExpectedOptionCardTypes),
            "Expected the explicit Sakura option card catalog to retain its complete deterministic order.");
        RegressionTestHarness.Require(
            registrationSource.Contains(
                "ConfigureDefaultModelCapabilities<SakuraOptionCard>",
                StringComparison.Ordinal),
            "Expected registered Sakura option cards to receive the shared card hover-tip capability.");
        RegressionTestHarness.Require(
            discoveredOptionCardTypes.SetEquals(optionCardTypes)
            && optionCardTypes.Distinct().Count() == optionCardTypes.Count,
            "Expected the explicit Sakura option card catalog to match every concrete SakuraOptionCard subclass exactly once.");
        foreach (var optionCardType in optionCardTypes)
        {
            var optionCard = Activator.CreateInstance(optionCardType) as SakuraOptionCard
                ?? throw new InvalidOperationException($"Expected {optionCardType.Name} to be a Sakura option card.");
            RegressionTestHarness.Require(
                registeredCardTypes.Contains(optionCardType),
                $"Expected option card {optionCardType.Name} to receive a mod-scoped registered identity.");
            RegressionTestHarness.Require(
                !rewardCatalogTypes.Contains(optionCardType),
                $"Expected option card {optionCardType.Name} to remain outside the reward catalog.");
            RegressionTestHarness.Require(
                optionCard.Rarity == CardRarity.Basic
                && !optionCard.ShouldShowInCardLibrary
                && !optionCard.CanBeGeneratedInCombat,
                $"Expected option card {optionCardType.Name} to remain non-rewardable, hidden, and excluded from combat generation.");
            var runAssetPaths = SakuraCardFrameVisuals.RunAssetPaths(optionCard).ToList();
            RegressionTestHarness.Require(
                runAssetPaths.Contains(ClearCardVisualAssets.ArtPath(optionCardType))
                && SakuraDescriptionRegion.AssetPaths(optionCard).All(runAssetPaths.Contains)
                && runAssetPaths.All(path => path.StartsWith(MainFile.ResPath, StringComparison.Ordinal)),
                $"Expected option card {optionCardType.Name} to preload its Clear full-card art and description background.");
            RegressionTestHarness.Require(
                SakuraDescriptionRegion.ShapeFor(optionCard) == SakuraDescriptionShape.Skill
                && ClearCardVisualAssets.EnglishName(optionCard) == ExpectedEnglishNames[optionCardType],
                $"Expected option card {optionCardType.Name} to use the skill description mask and its short English ribbon name.");
        }
    }

    [Fact]
    public void ExchangePileChoicesExposeStableKindsAndLiveCountProjection()
    {
        var choices = new ExchangePileOptionCard[]
        {
            new ExchangeMemoryChoice(),
            new ExchangeExhaustChoice(),
            new ExchangeDrawChoice(),
            new ExchangeDiscardChoice()
        };

        Assert.Equal(
            [ExchangePileKind.Memory, ExchangePileKind.Exhaust, ExchangePileKind.Draw, ExchangePileKind.Discard],
            choices.Select(static choice => choice.Kind));
        for (var i = 0; i < choices.Length; i++)
        {
            choices[i].SetDisplayedCount(i - 1);
            Assert.Equal(Math.Max(0, i - 1), choices[i].DynamicVars.Cards.IntValue);
            Assert.Equal(0, choices[i].MaxUpgradeLevel);
        }
    }
}
