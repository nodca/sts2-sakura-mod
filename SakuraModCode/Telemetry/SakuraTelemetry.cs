using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves;
using SakuraMod.SakuraModCode.Character;
using STS2RitsuLib;
using STS2RitsuLib.Compat;
using STS2RitsuLib.Content;
using STS2RitsuLib.RunData;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Telemetry;
using ClassicSakuraCharacter = SakuraMod.SakuraModCode.Classic.Character.ClassicSakura;

namespace SakuraMod.SakuraModCode.Telemetry;

internal static class SakuraTelemetry
{
    internal const string ApplicantId = MainFile.ModId;
    internal const string RunHistoryRequestId = "run_history";
    internal const string BalanceRunContributionId = "balance_run";
    internal const string EndpointUrl = "https://telemetry.cyb1.org/v1/ritsulib/batch";
    internal const string AuthorizationHeaderName = "Authorization";
    internal const string PublicWriteCredential = "sakuramod-balance-v2";

    private const string DisplayName = "SakuraMod";
    private const string ConsentDescriptionKey = "SAKURAMOD-TELEMETRY_CONSENT.description";
    private const string ConsentDescriptionFallback = "Sends completed Standard-mode Kinomoto Sakura run history, correlated card reward choices, aggregate card draw counts, an opaque run key, exact ascension/player count, SakuraMod version, and loaded gameplay Mod ids/versions for balance diagnostics. SakuraMod adds no card-play data, card text, local path, or personal identifier.";
    private const string BalanceContextEventName = "balance_run.context";
    internal const string CardRewardOfferedEventName = "card_reward.offered";
    internal const string CardRewardTakenEventName = "card_reward.taken";
    internal const string RunHistoryEventName = "run_history.completed";
    private const string RunSavedDataKey = "balance_run_v1";

    internal static readonly string SakuraCharacterEntry =
        ModContentRegistry.GetCompoundId(MainFile.ModId, "CHARACTER", ClassicSakuraCharacter.CharacterId);

    private static RunSavedData<BalanceRunIdentity>? _runData;
    private static bool _registered;

    public static void Register()
    {
        if (_registered)
            return;

        try
        {
            _runData = RitsuLibFramework.GetRunSavedDataStore(MainFile.ModId).Register(
                RunSavedDataKey,
                BalanceRunIdentity.Create,
                new RunSavedDataOptions
                {
                    SchemaVersion = 1,
                    WritePolicy = RunSavedDataWritePolicy.WhenSet
                });
            RitsuLibFramework.RegisterTelemetryContributionProvider(new SakuraBalanceRunContributionProvider());
            RitsuLibFramework.RegisterTelemetryApplicant(CreateApplicant());
            SakuraTelemetryRunHooks.Register();
            _registered = true;
        }
        catch (Exception exception)
        {
            MainFile.Logger.Warn($"SakuraMod telemetry registration failed: {exception.Message}");
        }
    }

    internal static TelemetryApplicant CreateApplicant() =>
        CreateApplicant(CreateAdapter());

    internal static TelemetryApplicant CreateApplicant(ITelemetryAdapter adapter) =>
        new()
        {
            ApplicantId = ApplicantId,
            OwnerModId = MainFile.ModId,
            DisplayName = DisplayName,
            Adapter = adapter,
            Requests =
            [
                TelemetryRequest.RunHistory(
                    ModSettingsText.LocString("settings_ui", ConsentDescriptionKey, ConsentDescriptionFallback),
                    sharedContributionSubscriptions: [BalanceRunContributionId],
                    captureFilter: ShouldCaptureRunHistory)
            ]
        };

    internal static ITelemetryAdapter CreateAdapter() =>
        new HttpJsonTelemetryAdapter(
            EndpointUrl,
            new Dictionary<string, string>
            {
                [AuthorizationHeaderName] = $"Bearer {PublicWriteCredential}"
            });

    internal static bool ShouldCaptureRunHistory(RunEndedEvent runEndedEvent) =>
        IsSakuraSerializableRun(runEndedEvent.Run);

    internal static bool IsSakuraSerializableRun(SerializableRun? run) =>
        run is { GameMode: GameMode.Standard, Players.Count: > 0 }
        && run.Players.All(player => IsSakuraCharacterId(player.CharacterId));

    internal static bool IsEligibleRun(RunState? runState) =>
        runState is { GameMode: GameMode.Standard, Players.Count: > 0 }
        && runState.Players.All(player => IsSakuraCharacterId(player.Character.Id));

    internal static bool IsSakuraCharacterId(ModelId? characterId) =>
        IsSakuraCharacterIdEntry(characterId?.Entry);

    internal static bool IsSakuraCharacterIdEntry(string? characterEntry) =>
        characterEntry == SakuraCharacterEntry;

    internal static BalanceRunIdentity? ResolveRunData(RunState runState)
    {
        if (_runData is null || !IsEligibleRun(runState))
            return null;

        if (_runData.TryGet(runState, out var existing))
            return existing.IsValid() ? existing : null;

        var created = BalanceRunIdentity.Create();
        _runData.Set(runState, created);
        return created;
    }

    internal static void PersistRunData(RunState runState, BalanceRunIdentity data)
    {
        if (_runData is not null)
            _runData.Set(runState, data);
    }

    internal static SakuraTelemetryRunContext BuildRunContext(RunState runState, string runKey)
    {
        var mods = SakuraTelemetryContract.GameplayMods(RitsuModManager.GetKnownMods());
        return new SakuraTelemetryRunContext(
            SakuraTelemetryContract.Version,
            runKey,
            SakuraTelemetryContract.SakuraModVersion(mods),
            runState.AscensionLevel,
            runState.Players.Count,
            runState.GameMode.ToString(),
            mods);
    }

    internal static void CaptureRunContext(SakuraTelemetryRunContext context)
    {
        var client = RitsuLibFramework.GetTelemetryClient(ApplicantId);
        if (!client.IsEnabled(RunHistoryRequestId))
            return;

        client.CapturePayload(
            BalanceContextEventName,
            RunHistoryRequestId,
            JsonSerializer.SerializeToNode(context)!);
    }

    internal static JsonObject BuildCardRewardOfferedPayload(CardRewardOfferSnapshot snapshot) =>
        JsonSerializer.SerializeToNode(new
        {
            balance_contract_version = SakuraTelemetryContract.Version,
            run_key = snapshot.RunKey,
            offer_sequence = snapshot.OfferSequence,
            run_floor = snapshot.RunFloor,
            act_floor = snapshot.ActFloor,
            reward_source = snapshot.RewardSource,
            rarity_odds = snapshot.RarityOdds,
            offered_cards = snapshot.OfferedCards
        })!.AsObject();

    internal static JsonObject BuildCardRewardTakenPayload(
        CardRewardOfferSnapshot snapshot,
        IReadOnlyList<SakuraTelemetryCardChoice> choices,
        bool skipped = false) =>
        JsonSerializer.SerializeToNode(new
        {
            balance_contract_version = SakuraTelemetryContract.Version,
            run_key = snapshot.RunKey,
            offer_sequence = snapshot.OfferSequence,
            selected_cards = choices
                .Where(static choice => choice.WasPicked)
                .Select(static choice => new { id = choice.CardId, upgrade = choice.UpgradeLevel })
                .ToArray(),
            unpicked_cards = choices
                .Where(static choice => !choice.WasPicked)
                .Select(static choice => new { id = choice.CardId, upgrade = choice.UpgradeLevel })
                .ToArray(),
            skipped
        })!.AsObject();

    internal static IReadOnlyDictionary<string, object?> BuildCardRewardProperties(CardRewardOfferSnapshot snapshot) =>
        new Dictionary<string, object?>
        {
            ["run_key"] = snapshot.RunKey,
            ["run_floor"] = snapshot.RunFloor,
            ["act_floor"] = snapshot.ActFloor,
            ["reward_source"] = snapshot.RewardSource,
            ["offer_sequence"] = snapshot.OfferSequence
        };

    internal static void LogCaptureFailure(string phase, Exception exception) =>
        MainFile.Logger.Warn($"SakuraMod telemetry {phase} capture failed: {exception.Message}");

    internal static bool TryExecute(Action action, Action<Exception> onFailure)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception exception)
        {
            try
            {
                onFailure(exception);
            }
            catch
            {
                // Telemetry diagnostics must not turn an optional capture failure into a gameplay failure.
            }
            return false;
        }
    }
}

internal static class SakuraTelemetryRunHooks
{
    private const string RunHookId = "SakuraMod.Telemetry.Run";
    private static readonly ConditionalWeakTable<RunState, SakuraTelemetryRunHook> Hooks = new();
    private static SakuraTelemetryRunHook? _activeHook;
    private static bool _registered;

    public static void Register()
    {
        if (_registered)
            return;

        ModHelper.SubscribeForRunStateHooks(
            RunHookId,
            HooksForRunState);
        RitsuLibFramework.SubscribeLifecycle<RunStartedEvent>(
            static evt => Activate(evt.RunState),
            replayCurrentState: false);
        RitsuLibFramework.SubscribeLifecycle<RunLoadedEvent>(
            static evt => Activate(evt.RunState),
            replayCurrentState: false);
        RitsuLibFramework.SubscribeLifecycle<RewardsScreenContinuingEvent>(
            static _ => _activeHook?.CaptureSkippedOffers(),
            replayCurrentState: false);
        _registered = true;
    }

    internal static JsonNode? BuildActiveContribution() =>
        _activeHook?.BuildContribution();

    private static IEnumerable<AbstractModel> HooksForRunState(RunState runState)
    {
        Activate(runState);
        return _activeHook is null ? [] : [_activeHook];
    }

    private static void Activate(RunState runState)
    {
        SakuraTelemetryRunHook? hook = null;
        try
        {
            if (!SakuraTelemetry.IsEligibleRun(runState))
            {
                _activeHook = null;
                return;
            }

            hook = Hooks.GetValue(runState, SakuraTelemetryRunHook.CreateForRun);
            _activeHook = hook;
            hook.Activate();
        }
        catch (Exception exception)
        {
            hook?.Disable();
            _activeHook = null;
            SakuraTelemetry.LogCaptureFailure("run activation", exception);
        }
    }
}

internal sealed class SakuraBalanceRunContributionProvider : ITelemetryContributionProvider
{
    public string ContributorModId => MainFile.ModId;
    public string ContributionId => SakuraTelemetry.BalanceRunContributionId;
    public TelemetryDataCategory Category => TelemetryDataCategory.RunHistory;
    public TelemetryContributionVisibility Visibility => TelemetryContributionVisibility.PrivateToApplicant;

    public JsonNode? Build(TelemetryContributionContext context)
    {
        if (context.ApplicantId != SakuraTelemetry.ApplicantId
            || context.RequestId != SakuraTelemetry.RunHistoryRequestId
            || context.EventName != SakuraTelemetry.RunHistoryEventName)
            return null;

        try
        {
            return SakuraTelemetryRunHooks.BuildActiveContribution();
        }
        catch (Exception exception)
        {
            SakuraTelemetry.LogCaptureFailure("run history contribution", exception);
            return null;
        }
    }
}

internal sealed class SakuraTelemetryRunHook : AbstractModel
{
    private RunState? _runState;
    private SakuraTelemetryUsageAccumulator _usage = new();
    private ConditionalWeakTable<Player, CardRewardCorrelation<CardRewardOfferSnapshot>> _rewardStates = new();
    private BalanceRunIdentity? _runData;
    private SakuraTelemetryRunContext? _context;
    private bool _activated;
    private bool _captureBalance;
    private bool _captureRewards;

    // ModelDb constructs every mod-owned AbstractModel through a public parameterless constructor.
    public SakuraTelemetryRunHook()
    {
    }

    internal static SakuraTelemetryRunHook CreateForRun(RunState runState)
    {
        var hook = (SakuraTelemetryRunHook)ModelDb
            .GetById<SakuraTelemetryRunHook>(ModelDb.GetId<SakuraTelemetryRunHook>())
            .MutableClone();
        hook._runState = runState;
        return hook;
    }

    public override bool ShouldReceiveCombatHooks => true;

    private RunState BoundRunState => _runState
        ?? throw new InvalidOperationException("The canonical telemetry hook is not bound to a run.");

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _runState = null;
        _usage = new SakuraTelemetryUsageAccumulator();
        _rewardStates = new ConditionalWeakTable<Player, CardRewardCorrelation<CardRewardOfferSnapshot>>();
        _runData = null;
        _context = null;
        _activated = false;
        _captureBalance = false;
        _captureRewards = false;
    }

    internal void Activate()
    {
        if (_activated)
            return;

        _activated = true;
        var client = RitsuLibFramework.GetTelemetryClient(SakuraTelemetry.ApplicantId);
        _captureBalance = client.IsEnabled(SakuraTelemetry.RunHistoryRequestId);
        _captureRewards = _captureBalance;
        if (!_captureBalance)
            return;

        _runData = SakuraTelemetry.ResolveRunData(BoundRunState);
        if (_runData is null)
        {
            _captureBalance = false;
            _captureRewards = false;
            return;
        }

        _usage.Restore(_runData.Usage);
        _context = _runData.Context ?? SakuraTelemetry.BuildRunContext(BoundRunState, _runData.RunKey);
        _runData.Context = _context;
        _runData.ContextChecksum = SakuraTelemetryContract.ContextChecksum(_context);
        SakuraTelemetry.PersistRunData(BoundRunState, _runData);
        SakuraTelemetry.CaptureRunContext(_context);
    }

    internal void Disable()
    {
        _captureBalance = false;
        _captureRewards = false;
    }

    public override Task BeforeCombatStart()
    {
        Guard("combat start", () =>
        {
            if (!_captureBalance)
                return;

            var deck = BoundRunState.Players
                .SelectMany(static player => player.Deck.Cards)
                .Select(static card => SakuraTelemetryCardClassifier.TryClassify(card, out var info)
                    ? (Valid: true, Instance: (object)card, Card: info)
                    : default)
                .Where(static item => item.Valid)
                .Select(static item => (item.Instance, item.Card));
            _usage.BeginCombat(deck);
        });
        return Task.CompletedTask;
    }

    public override Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        Guard("card generated", () =>
        {
            if (_captureBalance && SakuraTelemetryCardClassifier.TryClassify(card, out var info))
                _usage.RecordGenerated(card, info);
        });
        return Task.CompletedTask;
    }

    public override Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        Guard("card draw", () =>
        {
            if (_captureBalance && SakuraTelemetryCardClassifier.TryClassify(card, out var info))
                _usage.RecordDraw(card, info);
        });
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        Guard("combat end", () =>
        {
            if (!_captureBalance || _runData is null)
                return;

            _usage.EndCombat();
            _runData.Usage = _usage.Snapshot();
            SakuraTelemetry.PersistRunData(BoundRunState, _runData);
        });
        return Task.CompletedTask;
    }

    public override bool TryModifyCardRewardOptionsLate(
        Player player,
        List<CardCreationResult> cardRewardOptions,
        CardCreationOptions creationOptions)
    {
        Guard("card reward offer", () => CaptureCardRewardOffered(player, cardRewardOptions, creationOptions));
        return false;
    }

    public override Task AfterRewardTaken(Player player, Reward reward)
    {
        if (reward is CardReward)
            Guard("card reward take", () => CaptureCardRewardTaken(player));
        return Task.CompletedTask;
    }

    internal JsonNode? BuildContribution()
    {
        if (!_captureBalance || _runData is null || _context is null)
            return null;

        _runData.Usage = _usage.Snapshot();
        SakuraTelemetry.PersistRunData(BoundRunState, _runData);
        return JsonSerializer.SerializeToNode(new SakuraTelemetryBalanceRun(
            SakuraTelemetryContract.Version,
            _runData.RunKey,
            _runData.ContextChecksum,
            _runData.Usage));
    }

    internal void CaptureSkippedOffers()
    {
        if (!_captureRewards)
            return;

        foreach (var player in BoundRunState.Players)
        {
            if (!_rewardStates.TryGetValue(player, out var state))
                continue;

            foreach (var result in state.DrainSkipped())
            {
                Guard("card reward skip", () =>
                    CaptureTakenPayload(
                        result.Payload,
                        ToTelemetryChoices(result.Choices),
                        skipped: true));
            }
        }
    }

    private void CaptureCardRewardOffered(
        Player player,
        IReadOnlyList<CardCreationResult> cardRewardOptions,
        CardCreationOptions creationOptions)
    {
        if (!_captureRewards || _runData is null || cardRewardOptions.Count == 0)
            return;

        var offeredCards = cardRewardOptions
            .Select(static option => SakuraTelemetryCardClassifier.TryClassify(option.Card, out var info)
                ? (SakuraTelemetryCardInfo?)info
                : null)
            .Where(static card => card.HasValue)
            .Select(static card => card!.Value)
            .GroupBy(
                static card => (card.CardId, card.UpgradeLevel))
            .Select(static group => group.First())
            .ToArray();
        if (offeredCards.Length == 0)
            return;

        var state = _rewardStates.GetValue(player, static _ => new CardRewardCorrelation<CardRewardOfferSnapshot>());
        var snapshot = new CardRewardOfferSnapshot(
            _runData.RunKey,
            NextOfferSequence(),
            player.RunState.TotalFloor,
            player.RunState.ActFloor,
            creationOptions.Source.ToString(),
            creationOptions.RarityOdds.ToString(),
            CurrentCardChoiceHistoryCount(player),
            offeredCards);
        state.Remember(new CardRewardCorrelationOffer<CardRewardOfferSnapshot>(
            snapshot,
            snapshot.OfferSequence,
            snapshot.InitialCardChoiceHistoryCount,
            snapshot.OfferedCards
                .Select(static card => new CardRewardCorrelationCard(card.CardId, card.UpgradeLevel))
                .ToArray()));

        RitsuLibFramework.GetTelemetryClient(SakuraTelemetry.ApplicantId).CapturePayload(
            SakuraTelemetry.CardRewardOfferedEventName,
            SakuraTelemetry.RunHistoryRequestId,
            SakuraTelemetry.BuildCardRewardOfferedPayload(snapshot),
            SakuraTelemetry.BuildCardRewardProperties(snapshot));
    }

    private void CaptureCardRewardTaken(Player player)
    {
        if (!_captureRewards)
            return;

        var history = CurrentCardChoices(player);
        if (history is null || history.Count == 0 || !_rewardStates.TryGetValue(player, out var state))
            return;

        var result = state.TakeMatching(NormalizeCardChoices(history));
        if (result is null)
            return;

        var choices = ToTelemetryChoices(result.Choices);
        if (choices.Count > 0)
            CaptureTakenPayload(result.Payload, choices, skipped: !choices.Any(static choice => choice.WasPicked));
    }

    private static IReadOnlyList<CardChoiceHistoryEntry>? CurrentCardChoices(Player player) =>
        player.RunState.CurrentMapPointHistoryEntry?.GetEntry(player.NetId).CardChoices;

    private static int CurrentCardChoiceHistoryCount(Player player) =>
        CurrentCardChoices(player)?.Count ?? 0;

    private static IReadOnlyList<CardRewardCorrelationChoice> NormalizeCardChoices(
        IReadOnlyList<CardChoiceHistoryEntry> history) =>
        history
            .Select(static choice => new CardRewardCorrelationChoice(
                choice.Card.Id?.Entry,
                choice.Card.CurrentUpgradeLevel,
                choice.wasPicked))
            .ToArray();

    private static IReadOnlyList<SakuraTelemetryCardChoice> ToTelemetryChoices(
        IReadOnlyList<CardRewardCorrelationChoice> choices) =>
        choices
            .Where(static choice => choice.CardId is not null)
            .Select(static choice => new SakuraTelemetryCardChoice(
                choice.CardId!,
                choice.UpgradeLevel,
                choice.WasPicked))
            .ToArray();

    private int NextOfferSequence()
    {
        if (_runData is null)
            throw new InvalidOperationException("Balance run data is unavailable.");

        _runData.LastOfferSequence++;
        SakuraTelemetry.PersistRunData(BoundRunState, _runData);
        return _runData.LastOfferSequence;
    }

    private static void CaptureTakenPayload(
        CardRewardOfferSnapshot snapshot,
        IReadOnlyList<SakuraTelemetryCardChoice> choices,
        bool skipped)
    {
        RitsuLibFramework.GetTelemetryClient(SakuraTelemetry.ApplicantId).CapturePayload(
            SakuraTelemetry.CardRewardTakenEventName,
            SakuraTelemetry.RunHistoryRequestId,
            SakuraTelemetry.BuildCardRewardTakenPayload(snapshot, choices, skipped),
            SakuraTelemetry.BuildCardRewardProperties(snapshot));
    }

    private static void Guard(string phase, Action action)
        => SakuraTelemetry.TryExecute(action, exception => SakuraTelemetry.LogCaptureFailure(phase, exception));
}

internal sealed record CardRewardOfferSnapshot(
    string RunKey,
    int OfferSequence,
    int RunFloor,
    int ActFloor,
    string RewardSource,
    string RarityOdds,
    int InitialCardChoiceHistoryCount,
    IReadOnlyList<SakuraTelemetryCardInfo> OfferedCards);
