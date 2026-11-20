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
        "88C3C64EBAFABA628D7E8B1B483DCB17076EBF5A7036264E855018AC8D89F2B4";

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
                     "SakuraMod_ClassicTwinCardsDoubledThisTurn",
                     "SakuraMod_ClowCreateCostReductions",
                     "SakuraMod_RedCapeActivatedTurn",
                     "SakuraMod_RedCapeActivatedThisCombat",
                     "SakuraMod_FrogRaincoatManifestedThisTurn",
                     "SakuraMod_RepairPowerHealedThisTurn"
                 })
        {
            RegressionTestHarness.Require(
                source.Contains($"\"{key}\"", StringComparison.Ordinal),
                $"Expected the persisted attached-state key {key} to remain explicit and unchanged.");
        }
    }
}
