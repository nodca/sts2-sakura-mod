using System.Security.Cryptography;
using System.Text;
using SakuraMod.SakuraModCode;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Events;
using SakuraMod.SakuraModCode.Relics;
using STS2RitsuLib.Content;

public sealed class PublicIdentityCompatibilitySuite
{
    private const string ExpectedRegisteredEntryHash =
        "7534C81506E9D97B457ABD6995915102D04FD34E5922334BF6F0D0F8E34E11AB";

    [Fact]
    public void RegisteredPublicEntriesRemainSaveCompatible()
    {
        var registeredTypes = new[]
            {
                typeof(ClassicSakura),
                typeof(ClassicSakuraCardPool),
                typeof(ClassicSakuraRelicPool),
                typeof(ClassicSakuraPotionPool),
                typeof(ClassicMonsterEvent),
                typeof(ClassicXiaoLangsFeelingsEvent),
                typeof(ClassicTheSealedCardEvent),
                typeof(ClassicTheNothingEncounter),
                typeof(ClassicTheNothingMonster)
            }
            .Concat(SakuraContentRegistration.AllCardTypesForRegistration())
            .Concat(SakuraContentRegistration.AllPowerTypesForRegistration())
            .Concat(SakuraRelicCatalog.AllRelicTypes())
            .Distinct()
            .Select(type => $"{type.Name}|{ModContentRegistry.GetFixedPublicEntry(MainFile.ModId, type)}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        var fingerprint = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', registeredTypes))));

        RegressionTestHarness.Require(
            fingerprint == ExpectedRegisteredEntryHash,
            $"Registered public entries changed. Expected {ExpectedRegisteredEntryHash}, got {fingerprint} across {registeredTypes.Length} entries.");
    }

    [Fact]
    public void ExplicitCompatibilityStemsRemainStable()
    {
        RegressionTestHarness.Require(
            ClassicSakura.CharacterId == "ClassicSakura",
            "Expected the registered Kinomoto Sakura character stem to remain ClassicSakura for old saves and telemetry.");
        RegressionTestHarness.Require(
            SakuraSourceCardTextCapability.CapabilityIdValue
            == ModContentRegistry.GetQualifiedModelCapabilityId(MainFile.ModId, "CLASSIC_SAKURA_CARD_TEXT"),
            "Expected the registered Classic card-text capability entry to remain stable.");
    }

    [Fact]
    public void SavedAttachedStateKeysRemainStable()
    {
        var sourceRoot = Path.GetDirectoryName(
            RegressionTestHarness.FindRepoFile("SakuraModCode/MainFile.cs"))!;
        var source = string.Join(
            '\n',
            Directory.EnumerateFiles(
                    sourceRoot,
                    "*.cs",
                    SearchOption.AllDirectories)
                .Select(File.ReadAllText));

        foreach (var key in new[]
                 {
                     "SakuraMod_ClassicSealedWandCharge",
                     "SakuraMod_ClassicMoonBellUsed",
                     "SakuraMod_ClassicMagicChargeOpportunityToken",
                     "SakuraMod_ClassicTwinCardsDoubledThisTurn",
                     "SakuraMod_ClowCreateCostReductions",
                     "SakuraMod_RedCapeActivatedThisCombat",
                     "SakuraMod_FrogRaincoatMemoryRemainder",
                     "SakuraMod_FrogRaincoatPendingReminds"
                 })
        {
            RegressionTestHarness.Require(
                source.Contains($"\"{key}\"", StringComparison.Ordinal),
                $"Expected the persisted attached-state key {key} to remain explicit and unchanged.");
        }
    }

    [Fact]
    public void CurrentSourceTreeRejectsObsoleteRoleBucketsAndResourceNames()
    {
        var repoRoot = Path.GetDirectoryName(
            RegressionTestHarness.FindRepoFile("SakuraMod.csproj"))!;
        var codeRoot = Path.Join(repoRoot, "SakuraModCode");

        RegressionTestHarness.Require(
            !Directory.Exists(Path.Join(codeRoot, "Classic")),
            "Expected Classic to remain scoped to the card renderer, not a top-level gameplay module.");

        foreach (var obsoleteFile in new[] { "MvpCards.cs", "StarterCards.cs", "ExtendedCards.cs" })
        {
            RegressionTestHarness.Require(
                !Directory.EnumerateFiles(codeRoot, obsoleteFile, SearchOption.AllDirectories).Any(),
                $"Expected the obsolete aggregate {obsoleteFile} to remain deleted.");
        }

        var expectedCardFileCounts = new Dictionary<string, int>
        {
            ["Transparent"] = 36,
            ["ClowSakura"] = 52,
            ["Spells"] = 8,
            ["EventCards"] = 3,
            ["Ancients"] = 2
        };
        foreach (var (directory, expectedCount) in expectedCardFileCounts)
        {
            var files = Directory.EnumerateFiles(
                    Path.Join(codeRoot, "Cards", directory),
                    "*.cs",
                    SearchOption.TopDirectoryOnly);
            var actualCount = directory == "ClowSakura"
                ? files.Count(path => Path.GetFileName(path) != "SakuraLightVoidPatch.cs")
                : files.Count();
            RegressionTestHarness.Require(
                actualCount == expectedCount,
                $"Expected Cards/{directory} to contain {expectedCount} authoritative model files, got {actualCount}.");
        }

        foreach (var obsoletePath in new[]
                 {
                     "images/vfx/classic_magic_charge_blur_ring.png",
                     "images/vfx/classic_turn_lumin.png",
                     "scenes/combat/classic_turn_transformation_vfx.tscn",
                     "scenes/combat/energy_counters/classic_sakura_energy_counter.tscn",
                     "scenes/screens/char_select/char_select_bg_classic_sakura.tscn",
                     "sfx/classic_turn"
                 })
        {
            RegressionTestHarness.Require(
                !File.Exists(Path.Join(repoRoot, "SakuraMod", obsoletePath))
                && !Directory.Exists(Path.Join(repoRoot, "SakuraMod", obsoletePath)),
                $"Expected obsolete runtime resource path {obsoletePath} to remain absent.");
        }
    }
}
