using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.Saves;
using STS2RitsuLib;
using STS2RitsuLib.Compat;
using STS2RitsuLib.Telemetry;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Telemetry;
using SakuraMod.SakuraModCode;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json;
using VanillaStrikeIronclad = MegaCrit.Sts2.Core.Models.Cards.StrikeIronclad;

public sealed class TelemetryContractSuite
{
    [Fact]
    public void SerializedRunFilterAcceptsOnlyStandardKinomotoSakuraRuns()
    {
        RegressionTestHarness.Require(
            SakuraTelemetry.SakuraCharacterEntry == "SAKURA_MOD_CHARACTER_CLASSIC_SAKURA"
            && !SakuraTelemetry.IsSakuraCharacterIdEntry(ClassicSakura.CharacterId)
            && !SakuraTelemetry.IsSakuraCharacterIdEntry("SakuraMod"),
            "Expected Sakura telemetry serialized character filter to accept only the registered host entry.");
        RegressionTestHarness.Require(
            !SakuraTelemetry.IsSakuraCharacterIdEntry("IRONCLAD"),
            "Expected Sakura telemetry serialized character filter to exclude non-Sakura character ids.");
        RegressionTestHarness.Require(
            SakuraTelemetry.IsSakuraSerializableRun(new SerializableRun
            {
                GameMode = GameMode.Standard,
                Players =
                [
                    new SerializablePlayer
                    {
                        CharacterId = new ModelId("CHARACTER", SakuraTelemetry.SakuraCharacterEntry)
                    }
                ]
            }),
            "Expected Sakura telemetry run-history filter to accept serialized Kinomoto Sakura runs.");
        RegressionTestHarness.Require(
            !SakuraTelemetry.IsSakuraSerializableRun(new SerializableRun
            {
                GameMode = GameMode.Standard,
                Players =
                [
                    new SerializablePlayer
                    {
                        CharacterId = new ModelId("CHARACTER", "SakuraMod")
                    }
                ]
            }),
            "Expected Sakura telemetry run-history filter to reject the retired Clear Sakura id.");
        RegressionTestHarness.Require(
            !SakuraTelemetry.IsSakuraSerializableRun(new SerializableRun
            {
                GameMode = GameMode.Standard,
                Players =
                [
                    new SerializablePlayer
                    {
                        CharacterId = new ModelId("CHARACTER", "IRONCLAD")
                    }
                ]
            }),
            "Expected Sakura telemetry run-history filter to reject serialized non-Sakura runs.");
        RegressionTestHarness.Require(
            !SakuraTelemetry.IsSakuraSerializableRun(new SerializableRun
            {
                GameMode = GameMode.Daily,
                Players =
                [
                    new SerializablePlayer
                    {
                        CharacterId = new ModelId("CHARACTER", SakuraTelemetry.SakuraCharacterEntry)
                    }
                ]
            })
            && !SakuraTelemetry.IsSakuraSerializableRun(new SerializableRun
            {
                GameMode = GameMode.Custom,
                Players =
                [
                    new SerializablePlayer
                    {
                        CharacterId = new ModelId("CHARACTER", SakuraTelemetry.SakuraCharacterEntry)
                    }
                ]
            }),
            "Expected Sakura telemetry to reject Daily and Custom Sakura runs.");
    }

    [Fact]
    public void ApplicantConsentAndPublicAdapterContractRemainStable()
    {
        var telemetryApplicant = SakuraTelemetry.CreateApplicant(new DisabledTelemetryAdapter("test"));
        var runHistoryRequest = telemetryApplicant.Requests.Single(request => request.RequestId == SakuraTelemetry.RunHistoryRequestId);
        var sakuraRun = new SerializableRun
        {
            GameMode = GameMode.Standard,
            Players =
            [
                new SerializablePlayer
                {
                    CharacterId = new ModelId("CHARACTER", SakuraTelemetry.SakuraCharacterEntry)
                }
            ]
        };
        var nonSakuraRun = new SerializableRun
        {
            GameMode = GameMode.Standard,
            Players =
            [
                new SerializablePlayer
                {
                    CharacterId = new ModelId("CHARACTER", "IRONCLAD")
                }
            ]
        };
        RegressionTestHarness.Require(
            typeof(SakuraTelemetryRunHook).GetConstructor(Type.EmptyTypes) is not null
            && Activator.CreateInstance(typeof(SakuraTelemetryRunHook)) is SakuraTelemetryRunHook
            && typeof(SakuraTelemetryRunHook).GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                [typeof(RunState)],
                modifiers: null) is null,
            "Expected the telemetry run hook to expose only ModelDb-compatible construction.");
        RegressionTestHarness.Require(telemetryApplicant.ApplicantId == MainFile.ModId, "Expected Sakura telemetry applicant id to match the mod id.");
        RegressionTestHarness.Require(telemetryApplicant.OwnerModId == MainFile.ModId, "Expected Sakura telemetry owner mod id to match the mod id.");
        RegressionTestHarness.Require(telemetryApplicant.Requests.Count == 1, "Expected the correlated balance dataset to require one atomic authorization.");
        RegressionTestHarness.Require(
            runHistoryRequest.Category == TelemetryDataCategory.RunHistory,
            "Expected the single authorization to target run-history data.");
        RegressionTestHarness.Require(
            runHistoryRequest.CaptureFilter is not null,
            "Expected the single authorization to include a capture filter.");
        RegressionTestHarness.Require(
            runHistoryRequest.ContributionSubscriptions.Contains(SakuraTelemetry.BalanceRunContributionId),
            "Expected the single authorization to include the private balance contribution.");
        RegressionTestHarness.Require(
            runHistoryRequest.DescriptionText is not null,
            "Expected the single authorization to include localized disclosure text.");
        RegressionTestHarness.Require(
            runHistoryRequest.Description.Contains("Standard-mode Kinomoto Sakura", StringComparison.Ordinal),
            "Expected the single authorization disclosure to mention Standard-mode Kinomoto Sakura.");
        RegressionTestHarness.Require(
            runHistoryRequest.CaptureFilter!(new TelemetryCaptureContext(
                SakuraTelemetry.RunHistoryEventName,
                SakuraTelemetry.RunHistoryRequestId,
                TelemetryDataCategory.RunHistory,
                "run_history",
                new RunEndedEvent(sakuraRun, IsVictory: true, IsAbandoned: false, DateTimeOffset.UnixEpoch))),
            "Expected the capture filter to accept Standard Sakura run-history events.");
        RegressionTestHarness.Require(
            !runHistoryRequest.CaptureFilter!(new TelemetryCaptureContext(
                SakuraTelemetry.RunHistoryEventName,
                SakuraTelemetry.RunHistoryRequestId,
                TelemetryDataCategory.RunHistory,
                "run_history",
                new RunEndedEvent(nonSakuraRun, IsVictory: true, IsAbandoned: false, DateTimeOffset.UnixEpoch))),
            "Expected the capture filter to reject non-Sakura run-history events.");
        RegressionTestHarness.Require(
            runHistoryRequest.CaptureFilter!(new TelemetryCaptureContext(
                SakuraTelemetry.BalanceContextEventName,
                SakuraTelemetry.RunHistoryRequestId,
                TelemetryDataCategory.RunHistory,
                "applicant")),
            "Expected the capture filter to accept the authorized balance context event.");
        RegressionTestHarness.Require(
            runHistoryRequest.CaptureFilter!(new TelemetryCaptureContext(
                SakuraTelemetry.CardRewardOfferedEventName,
                SakuraTelemetry.RunHistoryRequestId,
                TelemetryDataCategory.RunHistory,
                "applicant")),
            "Expected the capture filter to accept the authorized card reward offered event.");
        RegressionTestHarness.Require(
            runHistoryRequest.CaptureFilter!(new TelemetryCaptureContext(
                SakuraTelemetry.CardRewardTakenEventName,
                SakuraTelemetry.RunHistoryRequestId,
                TelemetryDataCategory.RunHistory,
                "applicant")),
            "Expected the capture filter to accept the authorized card reward taken event.");
        RegressionTestHarness.Require(
            runHistoryRequest.CaptureFilter!(new TelemetryCaptureContext(
                SakuraTelemetryCoverage.SessionStartedEventName,
                SakuraTelemetry.RunHistoryRequestId,
                TelemetryDataCategory.RunHistory,
                "applicant")),
            "Expected the capture filter to accept the authorized session started coverage event.");
        RegressionTestHarness.Require(
            runHistoryRequest.CaptureFilter!(new TelemetryCaptureContext(
                SakuraTelemetryCoverage.CoverageEventName,
                SakuraTelemetry.RunHistoryRequestId,
                TelemetryDataCategory.RunHistory,
                "applicant")),
            "Expected the capture filter to accept the authorized run coverage event.");
        RegressionTestHarness.Require(
            !runHistoryRequest.CaptureFilter!(new TelemetryCaptureContext(
                "unrelated",
                SakuraTelemetry.RunHistoryRequestId,
                TelemetryDataCategory.RunHistory,
                "applicant")),
            "Expected the capture filter to reject unrelated events.");
        using (var englishSettings = JsonDocument.Parse(File.ReadAllText(RegressionTestHarness.FindRepoFile(
                   "SakuraMod/localization/eng/settings_ui.json"))))
        using (var chineseSettings = JsonDocument.Parse(File.ReadAllText(RegressionTestHarness.FindRepoFile(
                   "SakuraMod/localization/zhs/settings_ui.json"))))
        {
            const string consentKey = "SAKURAMOD-TELEMETRY_CONSENT.description";
            RegressionTestHarness.Require(
                englishSettings.RootElement.TryGetProperty(consentKey, out var englishConsent),
                "Expected an English telemetry authorization disclosure to exist.");
            RegressionTestHarness.Require(
                chineseSettings.RootElement.TryGetProperty(consentKey, out var chineseConsent),
                "Expected a Simplified Chinese telemetry authorization disclosure to exist.");
            RegressionTestHarness.Require(
                !string.IsNullOrWhiteSpace(englishConsent.GetString()),
                "Expected the English telemetry authorization disclosure to be non-empty.");
            RegressionTestHarness.Require(
                !string.IsNullOrWhiteSpace(chineseConsent.GetString()),
                "Expected the Simplified Chinese telemetry authorization disclosure to be non-empty.");
            RegressionTestHarness.Require(
                englishConsent.GetString() != chineseConsent.GetString(),
                "Expected the English and Simplified Chinese telemetry disclosures to differ.");
        }
        RegressionTestHarness.Require(
            SakuraTelemetry.CreateAdapter() is SizeBoundedTelemetryAdapter
            && SakuraTelemetry.PublicWriteCredential == "sakuramod-balance-v2",
            "Expected the bundled public write credential to enable telemetry without player configuration.");
    }

    [Fact]
    public void TelemetryAdapterSplitsSerializedBatchesBeforeTransportLimit()
    {
        var events = Enumerable.Range(0, 3)
            .Select(index => new TelemetryEnvelope
            {
                ApplicantId = "SakuraMod",
                EventName = "benchmark",
                RequestId = "run_history",
                Category = TelemetryDataCategory.RunHistory,
                Payload = JsonNode.Parse($"{{\"value\":\"{new string('x', 80)}{index}\"}}")
            })
            .ToArray();

        var batches = SizeBoundedTelemetryAdapter.SplitBatches("SakuraMod", events, maxBytes: 300);

        RegressionTestHarness.Require(
            batches.Count == 3 && batches.All(batch => batch.Count == 1),
            "Expected oversized serialized telemetry events to be sent as separate bounded batches.");
    }

    [Fact]
    public void CardRewardPayloadSeparatesOfferedSelectedAndSkippedCards()
    {
        var offerSnapshot = new CardRewardOfferSnapshot(
            RunKey: "018f6b8d-78ef-7a63-8f4a-4d663f3f0e61",
            OfferSequence: 7,
            RunFloor: 12,
            ActFloor: 5,
            RewardSource: "Encounter",
            RarityOdds: "RegularEncounter",
            InitialCardChoiceHistoryCount: 3,
            OfferedCards:
            [
                new SakuraTelemetryCardInfo("SAKURA_MOD_CARD_CLOW_SWORD", 1, "Common", "Attack", "1", "clow", MainFile.ModId),
                new SakuraTelemetryCardInfo("SAKURA_MOD_CARD_CLOW_SHIELD", 0, "Common", "Skill", "1", "clow", MainFile.ModId)
            ]);
        JsonObject offeredPayload = SakuraTelemetry.BuildCardRewardOfferedPayload(offerSnapshot);
        RegressionTestHarness.Require(
            offeredPayload["offer_sequence"]!.GetValue<int>() == 7
            && offeredPayload["run_key"]!.GetValue<string>() == offerSnapshot.RunKey
            && offeredPayload["offered_cards"]!.AsArray().Count == 2,
            "Expected Sakura telemetry card reward offer payload to match the correlated snake-case contract.");
        JsonObject takenPayload = SakuraTelemetry.BuildCardRewardTakenPayload(
            offerSnapshot,
            [
                new SakuraTelemetryCardChoice("SAKURA_MOD_CARD_CLOW_SWORD", 1, WasPicked: true),
                new SakuraTelemetryCardChoice("SAKURA_MOD_CARD_CLOW_SHIELD", 0, WasPicked: false)
            ]);
        RegressionTestHarness.Require(
            takenPayload["selected_cards"]!.AsArray().Count == 1
            && takenPayload["unpicked_cards"]!.AsArray().Count == 1
            && !takenPayload["skipped"]!.GetValue<bool>(),
            "Expected Sakura telemetry card reward take payload to separate picked and unpicked compact card entries.");
        JsonObject filteredPickPayload = SakuraTelemetry.BuildCardRewardTakenPayload(
            offerSnapshot,
            [new SakuraTelemetryCardChoice("SAKURA_MOD_CARD_CLOW_SHIELD", 0, WasPicked: false)],
            skipped: true);
        RegressionTestHarness.Require(
            filteredPickPayload["selected_cards"]!.AsArray().Count == 0
            && filteredPickPayload["skipped"]!.GetValue<bool>(),
            "Expected a selected excluded-owner card to appear as skipped within the eligible analysis card pool.");
    }

    [Fact]
    public void ClientJsonAndChecksumMatchServerContractFixtures()
    {
        var fixtureContext = FixtureContext();
        var fixtureChecksum = SakuraTelemetryContract.ContextChecksum(fixtureContext);
        RegressionTestHarness.Require(
            fixtureChecksum == "sha256:3584344b761506a123720124c2cb4cc45b7c6374c49508c054caa2075a36f839",
            $"Expected the client context checksum to match the server's canonical v2 fixture; got {fixtureChecksum}.");
        RegressionTestHarness.Require(
            JsonNode.DeepEquals(
                JsonSerializer.SerializeToNode(fixtureContext),
                JsonNode.Parse(File.ReadAllText(RegressionTestHarness.FindRepoFile(
                    "tools/telemetry-ingestion/internal/contracts/testdata/balance_run_context_v2.json")))),
            "Expected the client run-context JSON to match the server fixture field-for-field.");
        var fixtureOffer = new CardRewardOfferSnapshot(
            RunKey: fixtureContext.RunKey,
            OfferSequence: 3,
            RunFloor: 12,
            ActFloor: 12,
            RewardSource: "Combat",
            RarityOdds: "Default",
            InitialCardChoiceHistoryCount: 0,
            OfferedCards:
            [
                new SakuraTelemetryCardInfo("Windy", 0, "Common", "Skill", "1", "clow", MainFile.ModId),
                new SakuraTelemetryCardInfo("StrikeRed", 0, "Basic", "Attack", "1", "vanilla", "vanilla")
            ]);
        RegressionTestHarness.Require(
            JsonNode.DeepEquals(
                SakuraTelemetry.BuildCardRewardOfferedPayload(fixtureOffer),
                JsonNode.Parse(File.ReadAllText(RegressionTestHarness.FindRepoFile(
                    "tools/telemetry-ingestion/internal/contracts/testdata/card_reward_offered_v2.json")))),
            "Expected the client reward-offer JSON to match the server fixture field-for-field.");
        RegressionTestHarness.Require(
            JsonNode.DeepEquals(
                SakuraTelemetry.BuildCardRewardTakenPayload(
                    fixtureOffer,
                    [
                        new SakuraTelemetryCardChoice("Windy", 0, WasPicked: true),
                        new SakuraTelemetryCardChoice("StrikeRed", 0, WasPicked: false)
                    ]),
                JsonNode.Parse(File.ReadAllText(RegressionTestHarness.FindRepoFile(
                    "tools/telemetry-ingestion/internal/contracts/testdata/card_reward_taken_v2.json")))),
            "Expected the client reward-take JSON to match the server fixture field-for-field.");
        RegressionTestHarness.Require(
            JsonNode.DeepEquals(
                SakuraTelemetryCoverage.BuildSessionStartedPayload("0.9.0"),
                JsonNode.Parse(File.ReadAllText(RegressionTestHarness.FindRepoFile(
                    "tools/telemetry-ingestion/internal/contracts/testdata/session_started_v1.json")))),
            "Expected the client session-started JSON to match the server fixture field-for-field.");
        var startedCoverage = new SakuraTelemetryCoverageAccumulator();
        startedCoverage.RecordContextCaptured();
        RegressionTestHarness.Require(
            JsonNode.DeepEquals(
                SakuraTelemetryCoverage.BuildCoveragePayload(
                    fixtureContext.RunKey,
                    SakuraTelemetryCoverage.StageStarted,
                    playerCount: 1,
                    sakuraPlayerCount: 1,
                    startedCoverage),
                JsonNode.Parse(File.ReadAllText(RegressionTestHarness.FindRepoFile(
                    "tools/telemetry-ingestion/internal/contracts/testdata/balance_run_coverage_started_v1.json")))),
            "Expected the client run-coverage JSON to match the started fixture field-for-field.");
    }

    [Fact]
    public void GameplayEnvironmentAndCardOwnershipAreMinimizedAndDeterministic()
    {
        var gameplayMods = SakuraTelemetryContract.GameplayMods(
        [
            new RitsuModInfo("Disabled", "Secret name", "author", "9", RitsuModLoadState.Disabled, RitsuModSource.SteamWorkshop, true, "/private/path.dll", "9", [], 123),
            new RitsuModInfo("Cosmetic", "Secret name", "author", "2", RitsuModLoadState.Loaded, RitsuModSource.ModsDirectory, false, "/private/path.dll", "2", []),
            new RitsuModInfo("SakuraMod", "Secret name", "author", "0.9.0", RitsuModLoadState.Loaded, RitsuModSource.ModsDirectory, true, "/private/path.dll", "2", []),
            new RitsuModInfo("STS2-RitsuLib", "Secret name", "author", "0.4.54", RitsuModLoadState.Loaded, RitsuModSource.ModsDirectory, true, "/private/path.dll", "2", [])
        ]);
        RegressionTestHarness.Require(
            gameplayMods.SequenceEqual(
            [
                new SakuraTelemetryGameplayMod("STS2-RitsuLib", "0.4.54"),
                new SakuraTelemetryGameplayMod("SakuraMod", "0.9.0")
            ]),
            "Expected gameplay environment telemetry to retain only sorted loaded gameplay Mod ids and versions.");

        RegressionTestHarness.Require(
            SakuraTelemetryCardClassifier.TryClassifyOwner(typeof(ClowSword), out var clowCategory, out var clowOwner),
            "Expected telemetry classification to classify Clow cards.");
        RegressionTestHarness.Require(
            clowCategory == "clow",
            "Expected Clow Sword to classify as clow.");
        RegressionTestHarness.Require(
            clowOwner == MainFile.ModId,
            "Expected Clow Sword ownership to resolve to the mod id.");
        RegressionTestHarness.Require(
            SakuraTelemetryCardClassifier.TryClassifyOwner(typeof(SpellSeal), out var spellCategory, out _),
            "Expected telemetry classification to classify Spell cards.");
        RegressionTestHarness.Require(
            spellCategory == "spell",
            "Expected Spell Seal to classify as spell.");
        RegressionTestHarness.Require(
            SakuraTelemetryCardClassifier.TryClassifyOwner(typeof(VanillaStrikeIronclad), out var vanillaCategory, out var vanillaOwner),
            "Expected telemetry classification to classify vanilla cards.");
        RegressionTestHarness.Require(
            vanillaCategory == "vanilla",
            "Expected Strike Ironclad to classify as vanilla.");
        RegressionTestHarness.Require(
            vanillaOwner == "vanilla",
            "Expected Strike Ironclad ownership to resolve to vanilla.");
        RegressionTestHarness.Require(
            !SakuraTelemetryCardClassifier.TryClassifyOwner(typeof(SakuraTelemetryRunHook), out _, out _),
            "Expected telemetry classification to exclude unknown ownership.");
    }

    [Fact]
    public void UsagePersistenceAndFailureContainmentRemainStable()
    {
        var fixtureContext = FixtureContext();
        var usageCard = new SakuraTelemetryCardInfo("Windy", 0, "Common", "Skill", "1", "clow", MainFile.ModId);
        var deckInstance = new object();
        var generatedInstance = new object();
        var usage = new SakuraTelemetryUsageAccumulator();
        usage.BeginCombat([(deckInstance, usageCard)]);
        usage.RecordDraw(deckInstance, usageCard);
        usage.RecordGenerated(generatedInstance, usageCard);
        usage.RecordDraw(generatedInstance, usageCard);
        var usageRows = usage.Snapshot();
        var deckUsage = usageRows.Single(row => row.Provenance == "deck_owned");
        var generatedUsage = usageRows.Single(row => row.Provenance == "generated");
        RegressionTestHarness.Require(
            deckUsage.DrawCount == 1,
            "Expected deck-owned usage telemetry to count one draw.");
        RegressionTestHarness.Require(
            deckUsage.CombatsSeen == 1,
            "Expected deck-owned usage telemetry to count one combat.");
        RegressionTestHarness.Require(
            generatedUsage.GeneratedCount == 1,
            "Expected generated usage telemetry to count one generation.");
        RegressionTestHarness.Require(
            generatedUsage.DrawCount == 1,
            "Expected generated usage telemetry to count one draw.");
        RegressionTestHarness.Require(
            generatedUsage.CombatsSeen == 1,
            "Expected generated usage telemetry to count one combat.");
        var serializedUsage = JsonSerializer.Serialize(usageRows);
        RegressionTestHarness.Require(
            !serializedUsage.Contains("play", StringComparison.OrdinalIgnoreCase)
            && !serializedUsage.Contains("execution", StringComparison.OrdinalIgnoreCase),
            "Expected v2 usage telemetry to omit all card-play fields.");

        var persistedRunData = BalanceRunIdentity.Create();
        persistedRunData.Context = fixtureContext with { RunKey = persistedRunData.RunKey };
        persistedRunData.ContextChecksum = SakuraTelemetryContract.ContextChecksum(persistedRunData.Context);
        persistedRunData.Usage = usageRows;
        persistedRunData.LastOfferSequence = 12;
        var restoredRunData = JsonSerializer.Deserialize<BalanceRunIdentity>(JsonSerializer.Serialize(persistedRunData));
        RegressionTestHarness.Require(
            restoredRunData is not null,
            "Expected the run data to deserialize after the save-data JSON round trip.");
        RegressionTestHarness.Require(
            restoredRunData!.IsValid(),
            "Expected the restored run data to remain valid.");
        RegressionTestHarness.Require(
            restoredRunData!.RunKey == persistedRunData.RunKey,
            "Expected the stable run key to survive the round trip.");
        RegressionTestHarness.Require(
            restoredRunData!.ContextChecksum == persistedRunData.ContextChecksum,
            "Expected the context checksum to survive the round trip.");
        RegressionTestHarness.Require(
            restoredRunData!.Usage.Count == 2,
            "Expected the aggregate usage to survive the round trip.");
        RegressionTestHarness.Require(
            restoredRunData!.LastOfferSequence == 12,
            "Expected the reward offer sequence to survive the round trip.");
        Exception? capturedTelemetryFailure = null;
        RegressionTestHarness.Require(
            !SakuraTelemetry.TryExecute(
                static () => throw new InvalidOperationException("expected test failure"),
                exception => capturedTelemetryFailure = exception)
            && capturedTelemetryFailure is InvalidOperationException,
            "Expected telemetry failures to be contained and reported without escaping into gameplay.");
    }

    [Fact]
    public void CardRewardCorrelationUsesBoundedNewestMatchSemantics()
    {
        var capacity = new CardRewardCorrelation<string>();
        for (var sequence = 1; sequence <= 17; sequence++)
        {
            capacity.Remember(Offer($"offer-{sequence}", sequence, $"card-{sequence}"));
        }

        RegressionTestHarness.Require(
            capacity.TakeMatching([new CardRewardCorrelationChoice("card-1", 0, WasPicked: true)]) is null
            && capacity.TakeMatching([new CardRewardCorrelationChoice("card-2", 0, WasPicked: true)])?.Payload == "offer-2",
            "Expected Card Reward Correlation to retain only the newest 16 offers.");

        var newest = new CardRewardCorrelation<string>();
        newest.Remember(Offer("old", 20, "old-card"));
        newest.Remember(Offer("shared-1", 21, "shared-card"));
        newest.Remember(Offer("shared-2", 22, "shared-card"));
        newest.Remember(Offer("future", 23, "future-card"));
        var newestMatch = newest.TakeMatching([
            new CardRewardCorrelationChoice("shared-card", 0, WasPicked: false)
        ]);
        RegressionTestHarness.Require(
            newestMatch?.Payload == "shared-2"
            && newest.TakeMatching([new CardRewardCorrelationChoice("old-card", 0, WasPicked: true)]) is null
            && newest.TakeMatching([new CardRewardCorrelationChoice("future-card", 0, WasPicked: true)])?.Payload == "future",
            "Expected the newest matching offer to win while removing it and older stale offers only.");

        var duplicates = new CardRewardCorrelation<string>();
        duplicates.Remember(Offer("duplicates", 24, "duplicate-card"));
        var duplicateMatch = duplicates.TakeMatching([
            new CardRewardCorrelationChoice("duplicate-card", 0, WasPicked: false),
            new CardRewardCorrelationChoice("duplicate-card", 0, WasPicked: true)
        ]);
        RegressionTestHarness.Require(
            duplicateMatch?.Choices.Count == 1
            && duplicateMatch.Choices[0].WasPicked,
            "Expected duplicate card-choice history to collapse with picked-if-any semantics.");

        var skipped = new CardRewardCorrelation<string>();
        skipped.Remember(Offer("skip-a", 25, "skip-a-card"));
        skipped.Remember(Offer("skip-b", 26, "skip-b-card"));
        var skippedResults = skipped.DrainSkipped();
        RegressionTestHarness.Require(
            skippedResults.Count == 2
            && skippedResults.All(static result => result.Choices.Count == 1 && !result.Choices[0].WasPicked)
            && skipped.DrainSkipped().Count == 0,
            "Expected continue/skip to drain every pending offer as unpicked results.");

        var pendingBeforeLoad = new CardRewardCorrelation<string>();
        pendingBeforeLoad.Remember(Offer("ephemeral", 27, "ephemeral-card"));
        var reconstructedAfterLoad = new CardRewardCorrelation<string>();
        RegressionTestHarness.Require(
            pendingBeforeLoad.DrainSkipped().Count == 1
            && reconstructedAfterLoad.DrainSkipped().Count == 0,
            "Expected pending Card Reward Correlation offers to be empty after reconstruction.");
    }

    [Fact]
    public void CoverageEmissionIsConsentGatedAndContainsLifecycleCounters()
    {
        var disabled = new RecordingTelemetryClient { Enabled = false };
        RegressionTestHarness.Require(
            SakuraTelemetryCoverage.CaptureIfEnabled(
                disabled,
                SakuraTelemetryCoverage.SessionStartedEventName,
                SakuraTelemetryCoverage.BuildSessionStartedPayload("0.9.0"))
            == false
            && disabled.Captured.Count == 0,
            "Expected coverage capture to emit nothing when run_history is not enabled.");

        var enabled = new RecordingTelemetryClient { Enabled = true };
        var accumulator = new SakuraTelemetryCoverageAccumulator();
        accumulator.RecordContextCaptured();
        accumulator.RecordOffer(captured: true);
        accumulator.RecordTake(captured: false);
        accumulator.RecordFailure(CoverageFailureKind.Unknown);
        accumulator.SetLastOfferSequence(3);
        RegressionTestHarness.Require(
            SakuraTelemetryCoverage.TryCaptureRunCoverage(
                enabled,
                "018f6b8d-78ef-7a63-8f4a-4d663f3f0e61",
                SakuraTelemetryCoverage.StageCheckpoint,
                playerCount: 1,
                sakuraPlayerCount: 1,
                accumulator)
            &&
            enabled.Captured.Count == 1
            && enabled.Captured[0].EventName == SakuraTelemetryCoverage.CoverageEventName,
            "Expected enabled coverage capture to send one run coverage event.");

        var payload = enabled.Captured[0].Payload!.AsObject();
        RegressionTestHarness.Require(
            payload["stage"]!.GetValue<string>() == SakuraTelemetryCoverage.StageCheckpoint
            && payload["expected"]!["reward_offers"]!.GetValue<int>() == 1
            && payload["captured"]!["reward_offers"]!.GetValue<int>() == 1
            && payload["expected"]!["reward_takes"]!.GetValue<int>() == 1
            && payload["captured"]!["reward_takes"]!.GetValue<int>() == 0
            && payload["captured"]!["completed"]!.GetValue<int>() == 0
            && payload["capture_failure_counts"]!["unknown"]!.GetValue<int>() == 1
            && payload["last_offer_sequence"]!.GetValue<int>() == 3,
            "Expected coverage lifecycle counters to record expected, captured, and bounded failure counts.");
        RegressionTestHarness.Require(
            !payload.ToJsonString().Contains("card_id", StringComparison.Ordinal)
            && !payload.ToJsonString().Contains("exception", StringComparison.Ordinal),
            "Expected coverage payloads to omit card identity and unrestricted exception text.");
        RegressionTestHarness.Require(
            SakuraTelemetryCoverage.ClassifyFailure(new JsonException("bad json")) == CoverageFailureKind.Serialization
            && SakuraTelemetryCoverage.ClassifyFailure(new InvalidOperationException("other")) == CoverageFailureKind.Unknown,
            "Expected coverage failures to stay in bounded reason codes.");
        Exception? contained = null;
        RegressionTestHarness.Require(
            !SakuraTelemetry.TryExecute(
                static () => throw new InvalidOperationException("coverage boom"),
                exception => contained = exception)
            && contained is InvalidOperationException,
            "Expected coverage capture failures to stay contained.");
    }

    private sealed class RecordingTelemetryClient : ITelemetryClient
    {
        public bool Enabled { get; set; }
        public List<(string EventName, JsonNode? Payload)> Captured { get; } = [];
        public string ApplicantId => MainFile.ModId;
        public bool IsEnabled(string requestId) => Enabled && requestId == SakuraTelemetry.RunHistoryRequestId;
        public void Capture(string eventName, string requestId, IReadOnlyDictionary<string, object?>? properties = null)
        {
        }
        public void CapturePayload(
            string eventName,
            string requestId,
            JsonNode payload,
            IReadOnlyDictionary<string, object?>? properties = null)
        {
            Captured.Add((eventName, payload));
        }
        public void CaptureException(Exception exception, IReadOnlyDictionary<string, object?>? properties = null)
        {
        }
    }

    private static SakuraTelemetryRunContext FixtureContext() => new(
        BalanceContractVersion: SakuraTelemetryContract.Version,
        RunKey: "018f6b8d-78ef-7a63-8f4a-4d663f3f0e61",
        SakuraModVersion: "0.9.0",
        Ascension: 90,
        PlayerCount: 1,
        GameMode: "Standard",
        GameplayMods:
        [
            new SakuraTelemetryGameplayMod("SakuraMod", "0.9.0"),
            new SakuraTelemetryGameplayMod("STS2-RitsuLib", "0.4.54")
        ]);

    private static CardRewardCorrelationOffer<string> Offer(string payload, int sequence, string cardId) =>
        new(
            payload,
            sequence,
            InitialCardChoiceHistoryCount: 0,
            [new CardRewardCorrelationCard(cardId, 0)]);
}
