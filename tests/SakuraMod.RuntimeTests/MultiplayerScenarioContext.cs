using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Actions;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Connection;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Multiplayer.Messages.Game.Checksums;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Unlocks;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.TestProtocol;
using System.Diagnostics;

namespace SakuraMod.RuntimeTests;

internal sealed class MultiplayerScenarioContext : IAsyncDisposable
{
    private static readonly TimeSpan StateTimeout = TimeSpan.FromSeconds(30);
    private readonly SakuraTestRequest _request;
    private readonly SakuraMultiplayerRequest _multiplayer;
    private readonly INetGameService _netService;
    private readonly CancellationTokenSource _pumpCancellation;
    private readonly Task _pumpTask;
    private readonly Func<bool> _previousAutoSlayerCheck;
    private readonly ChecksumTracker _checksumTracker;
    private readonly List<ChecksumObservation> _checksumObservations = [];
    private string? _networkFailure;

    private MultiplayerScenarioContext(
        SakuraTestRequest request,
        INetGameService netService,
        RunState run,
        CancellationTokenSource pumpCancellation,
        Task pumpTask,
        Func<bool> previousAutoSlayerCheck)
    {
        _request = request;
        _multiplayer = request.Multiplayer!;
        _netService = netService;
        Run = run;
        _pumpCancellation = pumpCancellation;
        _pumpTask = pumpTask;
        _previousAutoSlayerCheck = previousAutoSlayerCheck;
        _checksumTracker = RunManager.Instance.ChecksumTracker;
        NonInteractiveMode.AutoSlayerCheck = static () => false;
        if (TestMode.IsOn)
            throw new InvalidOperationException("Multiplayer runtime tests must keep TestMode off for native checksums.");
        if (NonInteractiveMode.IsActive)
            throw new InvalidOperationException("Multiplayer runtime tests must use the interactive ActionExecutor path.");
        if (!_checksumTracker.IsEnabled)
            throw new InvalidOperationException("Native multiplayer checksum tracking is disabled.");

        _checksumTracker.ChecksumGenerated += OnChecksumGenerated;
        _checksumTracker.StateDiverged += OnStateDiverged;
        switch (netService)
        {
            case NetHostGameService host:
                host.ClientDisconnected += OnHostClientDisconnected;
                break;
            case NetClientGameService client:
                client.Disconnected += OnClientDisconnected;
                break;
        }
    }

    public RunState Run { get; }
    public Player LocalPlayer => LocalContext.GetMe(Run)
        ?? throw new InvalidOperationException("The local multiplayer player is unavailable.");

    /// <summary>
    /// Only meaningful in a two-peer session. Three-peer scenarios must address
    /// seats explicitly through <see cref="Player(ulong)"/> or <see cref="PeerPlayers"/>.
    /// </summary>
    public Player PeerPlayer => PeerCount == 2
        ? Run.Players.Single(player => player.NetId != LocalPlayer.NetId)
        : throw new InvalidOperationException(
            $"PeerPlayer is ambiguous in a {PeerCount}-peer session; address a seat by net id instead.");

    public IReadOnlyList<Player> PeerPlayers => Run.Players
        .Where(player => player.NetId != LocalPlayer.NetId)
        .OrderBy(static player => player.NetId)
        .ToList();

    /// <summary>
    /// The first client seat (net id 2). Stable across peer counts, so existing
    /// two-peer scenarios keep their meaning when a third seat is added.
    /// </summary>
    public Player ClientPlayer => Player(SakuraMultiplayerRoles.NetIdFor(SakuraMultiplayerRoles.Client));
    public IReadOnlyList<Player> OrderedPlayers => Run.Players
        .OrderBy(static player => player.NetId)
        .ToList();
    public bool IsHost => _multiplayer.Role == SakuraMultiplayerRoles.Host;
    public int PeerCount => _multiplayer.ExpectedPlayerCount;
    public int ChecksumCount => _checksumObservations.Count;
    public IReadOnlyList<ChecksumObservation> ChecksumObservations => _checksumObservations;

    public static async Task<MultiplayerScenarioContext> StartAsync(
        SakuraTestRequest request,
        bool shouldSave = false)
    {
        var multiplayer = request.Multiplayer
            ?? throw new InvalidDataException("Multiplayer context requires a multiplayer request block.");
        var previousAutoSlayerCheck = NonInteractiveMode.AutoSlayerCheck;
        SaveManager.Instance.MarkFtueAsComplete("combat_rules_ftue");
        SaveManager.Instance.MarkFtueAsComplete("can_play_cards_ftue");
        SaveManager.Instance.MarkFtueAsComplete("shuffle_ftue");

        var game = NGame.Instance
            ?? throw new InvalidOperationException("NGame is unavailable at multiplayer scenario dispatch.");
        var listener = new StartRunListener();
        StartRunLobby lobby;
        INetGameService netService;
        if (multiplayer.Role == SakuraMultiplayerRoles.Host)
        {
            var host = new NetHostGameService(PeerVersionInfo.LocalDefault());
            var error = host.StartENetHost(
                multiplayer.Port,
                maxClients: multiplayer.ExpectedPlayerCount - 1);
            if (error.HasValue)
                throw new InvalidOperationException($"Failed to start ENet multiplayer host: {error.Value}.");
            netService = host;
            LocalContext.NetId = host.NetId;
            lobby = new StartRunLobby(
                GameMode.Standard,
                host,
                listener,
                maxPlayers: multiplayer.ExpectedPlayerCount);
            lobby.AddLocalHostPlayer(
                new UnlockState(SaveManager.Instance.Progress),
                SaveManager.Instance.Progress.MaxMultiplayerAscension);
            RuntimeTestHost.WriteCheckpoint(
                request,
                "host_listening",
                $"ENet host is listening on {multiplayer.HostAddress}:{multiplayer.Port}.");
        }
        else
        {
            var joinFlow = new JoinFlow(new NetClientGameService(PeerVersionInfo.LocalDefault()));
            IClientConnectionInitializer initializer = new ENetClientConnectionInitializer(
                multiplayer.LocalNetId,
                multiplayer.HostAddress,
                multiplayer.Port);
            var join = await joinFlow.Begin(initializer, game.GetTree());
            if (join.sessionState != RunSessionState.InLobby || !join.joinResponse.HasValue)
                throw new InvalidOperationException($"Client joined unexpected session state {join.sessionState}.");
            netService = joinFlow.NetService
                ?? throw new InvalidOperationException("JoinFlow returned without a client network service.");
            LocalContext.NetId = netService.NetId;
            lobby = new StartRunLobby(join.gameMode, netService, listener, maxPlayers: -1);
            lobby.InitializeFromMessage(join.joinResponse.Value);
        }

        var pumpCancellation = new CancellationTokenSource();
        var pumpTask = PumpNetworkAsync(game, netService, pumpCancellation.Token);
        NonInteractiveMode.AutoSlayerCheck = static () => true;
        try
        {
            game.RemoteCursorContainer.Initialize(lobby.InputSynchronizer, lobby.Players.Select(static player => player.id));
            game.ReactionContainer.InitializeNetworking(netService);
            lobby.SetLocalCharacter(ModelDb.Character<ClassicSakura>());
            if (multiplayer.Role == SakuraMultiplayerRoles.Host)
                lobby.SetSeed(request.Seed.ToString("X16"));

            await WaitUntilAsync(
                game,
                () => lobby.Players.Count == multiplayer.ExpectedPlayerCount
                    && lobby.Players.All(static player => player.character is ClassicSakura),
                $"{multiplayer.ExpectedPlayerCount} synchronized Sakura lobby players");
            lobby.SetReady(ready: true);
            var begin = await listener.Begin.Task.WaitAsync(StateTimeout);
            var run = await game.StartNewMultiplayerRun(
                lobby,
                shouldSave,
                begin.Acts,
                begin.Modifiers,
                begin.Seed,
                lobby.Ascension);
            lobby.CleanUp(disconnectSession: false);
            RunManager.Instance.CombatReplayWriter.IsEnabled = false;
            var ids = run.Players.Select(static player => player.NetId).Order().ToArray();
            var expectedIds = Enumerable.Range(1, multiplayer.ExpectedPlayerCount)
                .Select(static value => (ulong)value)
                .ToArray();
            if (!ids.SequenceEqual(expectedIds))
            {
                throw new InvalidOperationException(
                    $"Expected multiplayer player ids {string.Join(',', expectedIds)}; found {string.Join(',', ids)}.");
            }
            RuntimeTestHost.WriteCheckpoint(
                request,
                "run_started",
                $"{multiplayer.ExpectedPlayerCount}-player Sakura run started for local net id {LocalContext.NetId}.");
            return new MultiplayerScenarioContext(
                request,
                netService,
                run,
                pumpCancellation,
                pumpTask,
                previousAutoSlayerCheck);
        }
        catch
        {
            pumpCancellation.Cancel();
            await ObserveAsync(pumpTask);
            lobby.CleanUp(disconnectSession: true);
            NonInteractiveMode.AutoSlayerCheck = previousAutoSlayerCheck;
            throw;
        }
    }

    public static async Task<MultiplayerScenarioContext> LoadAsync(SakuraTestRequest request)
    {
        var multiplayer = request.Multiplayer
            ?? throw new InvalidDataException("Multiplayer context requires a multiplayer request block.");
        var previousAutoSlayerCheck = NonInteractiveMode.AutoSlayerCheck;
        SaveManager.Instance.MarkFtueAsComplete("combat_rules_ftue");
        SaveManager.Instance.MarkFtueAsComplete("can_play_cards_ftue");
        SaveManager.Instance.MarkFtueAsComplete("shuffle_ftue");

        var game = NGame.Instance
            ?? throw new InvalidOperationException("NGame is unavailable at multiplayer scenario dispatch.");
        var listener = new LoadRunListener();
        LoadRunLobby lobby;
        INetGameService netService;
        if (multiplayer.Role == SakuraMultiplayerRoles.Host)
        {
            var host = new NetHostGameService(PeerVersionInfo.LocalDefault());
            var error = host.StartENetHost(multiplayer.Port, maxClients: multiplayer.ExpectedPlayerCount - 1);
            if (error.HasValue)
                throw new InvalidOperationException($"Failed to start ENet multiplayer host: {error.Value}.");
            netService = host;
            LocalContext.NetId = host.NetId;
            var read = SaveManager.Instance.LoadAndCanonicalizeMultiplayerRunSave(host.NetId);
            if (!read.Success || read.SaveData is null)
                throw new InvalidDataException($"Host multiplayer save could not be loaded: {read.Status} {read.ErrorMessage}");
            lobby = new LoadRunLobby(host, listener, read.SaveData);
            lobby.AddLocalHostPlayer();
            RuntimeTestHost.WriteCheckpoint(
                request,
                "host_listening",
                $"ENet load-run host is listening on {multiplayer.HostAddress}:{multiplayer.Port}.");
        }
        else
        {
            var joinFlow = new JoinFlow(new NetClientGameService(PeerVersionInfo.LocalDefault()));
            IClientConnectionInitializer initializer = new ENetClientConnectionInitializer(
                multiplayer.LocalNetId,
                multiplayer.HostAddress,
                multiplayer.Port);
            var join = await joinFlow.Begin(initializer, game.GetTree());
            if (join.sessionState != RunSessionState.InLoadedLobby || !join.loadJoinResponse.HasValue)
                throw new InvalidOperationException(
                    $"Client joined unexpected load-run session state {join.sessionState}.");
            netService = joinFlow.NetService
                ?? throw new InvalidOperationException("JoinFlow returned without a client network service.");
            LocalContext.NetId = netService.NetId;
            lobby = new LoadRunLobby(netService, listener, join.loadJoinResponse.Value);
        }

        var pumpCancellation = new CancellationTokenSource();
        var pumpTask = PumpNetworkAsync(game, netService, pumpCancellation.Token);
        NonInteractiveMode.AutoSlayerCheck = static () => true;
        try
        {
            game.ReactionContainer.InitializeNetworking(netService);
            await WaitUntilAsync(
                game,
                () => lobby.PlayerCount == multiplayer.ExpectedPlayerCount,
                $"{multiplayer.ExpectedPlayerCount} connected load-run lobby players");
            game.RemoteCursorContainer.Initialize(
                lobby.InputSynchronizer,
                lobby.PlayerIds.Order().ToArray());
            lobby.SetReady(ready: true);
            await listener.Begin.Task.WaitAsync(StateTimeout);

            var run = RunState.FromSerializable(lobby.Run);
            await RunManager.Instance.SetUpSavedMultiplayer(run, lobby);
            await game.LoadRun(run, lobby.Run.PreFinishedRoom);
            lobby.CleanUp(disconnectSession: false);
            RunManager.Instance.CombatReplayWriter.IsEnabled = false;
            var expectedIds = Enumerable.Range(1, multiplayer.ExpectedPlayerCount)
                .Select(static id => (ulong)id)
                .ToArray();
            if (!run.Players.Select(static player => player.NetId).Order().SequenceEqual(expectedIds))
                throw new InvalidOperationException("Loaded multiplayer player ids do not match the requested peer group.");
            RuntimeTestHost.WriteCheckpoint(
                request,
                "run_loaded",
                $"{multiplayer.ExpectedPlayerCount}-player Sakura save loaded for local net id {LocalContext.NetId}.");
            return new MultiplayerScenarioContext(
                request,
                netService,
                run,
                pumpCancellation,
                pumpTask,
                previousAutoSlayerCheck);
        }
        catch
        {
            pumpCancellation.Cancel();
            await ObserveAsync(pumpTask);
            lobby.CleanUp(disconnectSession: true);
            NonInteractiveMode.AutoSlayerCheck = previousAutoSlayerCheck;
            throw;
        }
    }

    public Player Player(ulong netId) => Run.GetPlayer(netId)
        ?? throw new InvalidOperationException($"Multiplayer run has no player {netId}.");

    public Task WaitForActionsAsync() =>
        RunManager.Instance.ActionExecutor.FinishedExecutingActions().WaitAsync(StateTimeout);

    public async Task<CombatState> EnterWeakCrawlerCombatAsync()
    {
        await SignalAndWaitAsync("run-ready-for-combat");
        // The game's own multiplayer debug bootstrap enters the same deterministic
        // debug room locally on every peer after the synchronized run starts.
        await RunManager.Instance.EnterRoomDebug(
            MegaCrit.Sts2.Core.Rooms.RoomType.Monster,
            model: ModelDb.Encounter<MegaCrit.Sts2.Core.Models.Encounters.FuzzyWurmCrawlerWeak>().ToMutable(),
            showTransition: false);
        await WaitUntilAsync(
            NGame.Instance!,
            () => CombatManager.Instance.IsInProgress
                && Run.Players.All(static player => player.PlayerCombatState?.Phase == PlayerTurnPhase.Play)
                && CombatManager.Instance.DebugOnlyGetState() is not null,
            $"{_multiplayer.ExpectedPlayerCount}-player combat play phase");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions().WaitAsync(StateTimeout);
        RuntimeTestHost.WriteCheckpoint(
            _request,
            "combat_ready",
            $"{_multiplayer.ExpectedPlayerCount}-player weak Crawler combat reached Play phase.");
        return CombatManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException("CombatManager has no multiplayer combat state.");
    }

    public async Task<CombatState> EnterWeakSlimesCombatAsync()
    {
        await SignalAndWaitAsync("run-ready-for-combat");
        await RunManager.Instance.EnterRoomDebug(
            MegaCrit.Sts2.Core.Rooms.RoomType.Monster,
            model: ModelDb.Encounter<MegaCrit.Sts2.Core.Models.Encounters.SlimesWeak>().ToMutable(),
            showTransition: false);
        await WaitUntilAsync(
            NGame.Instance!,
            () => CombatManager.Instance.IsInProgress
                && Run.Players.All(static player => player.PlayerCombatState?.Phase == PlayerTurnPhase.Play)
                && CombatManager.Instance.DebugOnlyGetState() is not null,
            $"{_multiplayer.ExpectedPlayerCount}-player combat play phase");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions().WaitAsync(StateTimeout);
        RuntimeTestHost.WriteCheckpoint(
            _request,
            "combat_ready",
            $"{_multiplayer.ExpectedPlayerCount}-player weak Slimes combat reached Play phase.");
        return CombatManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException("CombatManager has no multiplayer combat state.");
    }

    public async Task SignalAndWaitAsync(string stage)
    {
        if (stage.Any(static character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_'))
            throw new ArgumentException("Coordination stage contains unsupported characters.", nameof(stage));
        var own = Path.Combine(
            _multiplayer.CoordinationRoot,
            $"{stage}.{_multiplayer.LocalNetId}.json");
        SakuraTestProtocol.WriteAtomic(own, new
        {
            schema_version = SakuraTestProtocol.CurrentSchemaVersion,
            _request.RunId,
            _request.ScenarioId,
            role = _multiplayer.Role,
            stage,
            occurred_at_utc = DateTimeOffset.UtcNow
        });
        var peerMarkers = Enumerable.Range(1, _multiplayer.ExpectedPlayerCount)
            .Select(value => Path.Combine(_multiplayer.CoordinationRoot, $"{stage}.{value}.json"))
            .ToArray();
        await WaitUntilAsync(
            NGame.Instance!,
            () => peerMarkers.All(File.Exists),
            $"all {_multiplayer.ExpectedPlayerCount} peer stage markers for {stage}");
    }

    public async Task PlayOwnedCardAsync(CardModel card, Creature? target = null)
    {
        var action = RequestOwnedCard(card, target);
        await WaitForOwnedCardAsync(action, card);
    }

    public GameAction RequestOwnedCard(CardModel card, Creature? target = null)
    {
        if (!LocalContext.IsMine(card))
            throw new InvalidOperationException($"Local peer cannot request play for player {card.Owner.NetId}.");
        var action = new PlayCardAction(card, target);
        RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(action);
        return action;
    }

    public async Task WaitForOwnedCardAsync(GameAction action, CardModel card)
    {
        if (IsHost)
        {
            await WaitForOwnedHostActionAsync(action);
        }
        else
        {
            // Client requests are accepted by the host and broadcast back as a
            // new local action instance, so the original request never completes.
            await WaitUntilAsync(
                NGame.Instance!,
                () => card.Pile?.Type != MegaCrit.Sts2.Core.Entities.Cards.PileType.Hand,
                $"client-owned card {card.Id} to leave the hand");
        }
    }

    public async Task EndLocalTurnAsync()
    {
        var combat = LocalPlayer.PlayerCombatState
            ?? throw new InvalidOperationException("Local player has no combat state.");
        var action = new EndPlayerTurnAction(LocalPlayer, combat.TurnNumber);
        RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(action);
        if (IsHost)
        {
            await WaitForOwnedHostActionAsync(action);
        }
        else
        {
            await WaitUntilAsync(
                NGame.Instance!,
                () => CombatManager.Instance.IsPlayerReadyToEndTurn(LocalPlayer),
                "client-owned end-turn request to synchronize");
        }
    }

    public async Task WaitForActionChecksumsAsync(
        int baselineCount,
        string description,
        params string[] requiredActionNames)
    {
        await WaitUntilAsync(
            NGame.Instance!,
            () => _networkFailure is not null
                || requiredActionNames.All(actionName =>
                    _checksumObservations.Skip(baselineCount).Any(observation =>
                        observation.Context.Contains(actionName, StringComparison.Ordinal))),
            $"{description} checksum observations");
        ThrowIfNetworkFailed();

        var observations = _checksumObservations.Skip(baselineCount).ToArray();
        foreach (var actionName in requiredActionNames)
        {
            if (!observations.Any(observation =>
                    observation.Context.Contains(actionName, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"Missing checksum for {actionName} after {description}. "
                    + $"Observed: {string.Join(" | ", observations.Select(static item => item.Context))}");
            }
        }
    }

    public void ThrowIfNetworkFailed()
    {
        if (_networkFailure is not null)
            throw new InvalidOperationException($"Multiplayer network failed: {_networkFailure}");
    }

    private static async Task WaitForOwnedHostActionAsync(GameAction action)
    {
        await action.CompletionTask.WaitAsync(StateTimeout);
        if (action.Exception is { } exception)
            throw exception.GetBaseException();
        if (action.State != GameActionState.Finished)
            throw new InvalidOperationException($"Multiplayer action ended in state {action.State}: {action}.");
    }

    public static Task WaitForStateAsync(Func<bool> predicate, string description) =>
        WaitUntilAsync(NGame.Instance!, predicate, description);

    public async ValueTask DisposeAsync()
    {
        _checksumTracker.ChecksumGenerated -= OnChecksumGenerated;
        _checksumTracker.StateDiverged -= OnStateDiverged;
        switch (_netService)
        {
            case NetHostGameService host:
                host.ClientDisconnected -= OnHostClientDisconnected;
                break;
            case NetClientGameService client:
                client.Disconnected -= OnClientDisconnected;
                break;
        }
        NonInteractiveMode.AutoSlayerCheck = _previousAutoSlayerCheck;
        _pumpCancellation.Cancel();
        await ObserveAsync(_pumpTask);
        _netService.Disconnect(NetError.Quit, now: true);
        _pumpCancellation.Dispose();
    }

    private static async Task PumpNetworkAsync(NGame game, INetGameService service, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            service.Update();
            await game.AwaitProcessFrame();
        }
    }

    private static async Task WaitUntilAsync(NGame game, Func<bool> predicate, string description)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!predicate())
        {
            if (stopwatch.Elapsed >= StateTimeout)
                throw new TimeoutException($"Timed out waiting for multiplayer state: {description}.");
            await game.AwaitProcessFrame();
        }
    }

    private static async Task ObserveAsync(Task task)
    {
        try { await task; }
        catch { }
    }

    private void OnChecksumGenerated(
        NetChecksumData data,
        string context,
        NetFullCombatState _)
    {
        _checksumObservations.Add(new ChecksumObservation(data.id, context, data.checksum));
    }

    private void OnStateDiverged(ulong _, NetFullCombatState __) =>
        _networkFailure = "ChecksumTracker reported StateDiverged.";

    private void OnHostClientDisconnected(ulong _, NetErrorInfo info) =>
        _networkFailure = $"Host observed client disconnect: {info}";

    private void OnClientDisconnected(NetErrorInfo info) =>
        _networkFailure = $"Client disconnected from host: {info}";

    private sealed class StartRunListener : IStartRunLobbyListener
    {
        public TaskCompletionSource<BeginRunData> Begin { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void BeginRun(string seed, List<ActModel> acts, IReadOnlyList<ModifierModel> modifiers) =>
            Begin.TrySetResult(new BeginRunData(seed, acts, modifiers));
        public void LocalPlayerDisconnected(NetErrorInfo info) =>
            Begin.TrySetException(new InvalidOperationException($"Disconnected before run start: {info}."));
        public void PlayerConnected(StartRunLobbyPlayer player) { }
        public void PlayerChanged(StartRunLobbyPlayer player, bool isRandomCharacterResolution) { }
        public void AscensionChanged() { }
        public void SeedChanged() { }
        public void ModifiersChanged() { }
        public void MaxAscensionChanged() { }
        public void RemotePlayerDisconnected(StartRunLobbyPlayer player) { }
    }

    private sealed class LoadRunListener : ILoadRunLobbyListener
    {
        public TaskCompletionSource Begin { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void BeginRun() => Begin.TrySetResult();
        public Task<bool> ShouldAllowRunToBegin() => Task.FromResult(true);
        public void LocalPlayerDisconnected(NetErrorInfo info) =>
            Begin.TrySetException(new InvalidOperationException($"Disconnected before saved run loaded: {info}."));
        public void PlayerConnected(LoadRunLobbyPlayer player) { }
        public void RemotePlayerDisconnected(ulong playerId) { }
        public void PlayerReadyChanged(ulong playerId) { }
    }

    private sealed record BeginRunData(
        string Seed,
        List<ActModel> Acts,
        IReadOnlyList<ModifierModel> Modifiers);
}

internal sealed record ChecksumObservation(uint Id, string Context, uint Checksum);
