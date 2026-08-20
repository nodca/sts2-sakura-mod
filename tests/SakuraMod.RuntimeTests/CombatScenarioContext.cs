using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Actions;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.FourthAct.Dark.Encounters;
using SakuraMod.SakuraModCode.FourthAct.Wind.Encounters;
using SakuraMod.TestProtocol;
using System.Diagnostics;

namespace SakuraMod.RuntimeTests;

internal sealed record CombatScenarioContext(
    SakuraTestRequest Request,
    RunState Run,
    Player Player)
{
    private static readonly TimeSpan StateTimeout = TimeSpan.FromSeconds(20);

    public static async Task<CombatScenarioContext> StartAsync(
        SakuraTestRequest request,
        bool shouldSave = false,
        IReadOnlyList<ActModel>? acts = null)
    {
        NonInteractiveMode.AutoSlayerCheck = static () => true;
        SaveManager.Instance.MarkFtueAsComplete("combat_rules_ftue");
        SaveManager.Instance.MarkFtueAsComplete("can_play_cards_ftue");
        SaveManager.Instance.MarkFtueAsComplete("shuffle_ftue");

        var game = NGame.Instance
            ?? throw new InvalidOperationException("NGame is unavailable at main-menu scenario dispatch.");
        var run = await game.StartNewSingleplayerRun(
            ModelDb.Character<ClassicSakura>(),
            shouldSave,
            acts ?? ActModel.GetDefaultList(),
            Array.Empty<ModifierModel>(),
            request.Seed.ToString("X16"),
            GameMode.Standard);
        RunManager.Instance.CombatReplayWriter.IsEnabled = false;
        if (run.Players.Count != 1)
        {
            throw new InvalidOperationException(
                $"Expected one player in a singleplayer run, found {run.Players.Count}.");
        }

        RuntimeTestHost.WriteCheckpoint(
            request,
            "run_started",
            "A fixed-seed Sakura singleplayer run reached the native run scene.");
        return new CombatScenarioContext(request, run, run.Players[0]);
    }

    public Task<CombatState> EnterWeakCrawlerCombatAsync() =>
        EnterCombatAsync(
            ModelDb.Encounter<FuzzyWurmCrawlerWeak>().ToMutable(),
            "The fixed Fuzzy Wurm/Crawler encounter reached player Play phase.");

    public Task<CombatState> EnterWeakSlimesCombatAsync() =>
        EnterCombatAsync(
            ModelDb.Encounter<SlimesWeak>().ToMutable(),
            "The fixed weak Slimes encounter reached player Play phase.");

    public Task<CombatState> EnterKnightsEliteCombatAsync() =>
        EnterCombatAsync(
            ModelDb.Encounter<KnightsElite>().ToMutable(),
            "The fixed Knights elite encounter reached player Play phase.",
            RoomType.Elite);

    public Task<CombatState> EnterVantomBossCombatAsync() =>
        EnterCombatAsync(
            ModelDb.Encounter<VantomBoss>().ToMutable(),
            "The fixed first-act Vantom boss encounter reached player Play phase.",
            RoomType.Boss);

    public Task<CombatState> EnterDarkCombatAsync() =>
        EnterCombatAsync(
            ModelDb.Encounter<DarkEncounter>().ToMutable(),
            "The Dark endpoint encounter reached player Play phase.",
            RoomType.Boss);

    public Task<CombatState> EnterWindyCombatAsync() =>
        EnterCombatAsync(
            ModelDb.Encounter<WindyEncounter>().ToMutable(),
            "The Windy encounter reached player Play phase.",
            RoomType.Boss);

    private async Task<CombatState> EnterCombatAsync(
        EncounterModel encounter,
        string checkpointDetail,
        RoomType roomType = RoomType.Monster)
    {
        await RunManager.Instance.EnterRoomDebug(
            roomType,
            model: encounter,
            showTransition: false);
        await WaitUntilAsync(
            () => CombatManager.Instance.IsInProgress
                && Player.PlayerCombatState?.Phase == PlayerTurnPhase.Play
                && CombatManager.Instance.DebugOnlyGetState() is not null,
            "combat play phase");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();

        var state = CombatManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException("CombatManager has no state after combat startup.");
        RuntimeTestHost.WriteCheckpoint(
            Request,
            "combat_ready",
            checkpointDetail);
        return state;
    }

    public static async Task<T> AddGeneratedCardToHandAsync<T>(
        CombatState combat,
        Player player)
        where T : CardModel
    {
        var card = combat.CreateCard<T>(player);
        await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
            card,
            PileType.Hand,
            player,
            CardPilePosition.Random);
        return card;
    }

    public static async Task<PlayCardAction> PlayCardAsync(
        CardModel card,
        Creature? target = null)
    {
        var action = new PlayCardAction(card, target);
        await EnqueueAndWaitAsync(action);
        return action;
    }

    public static async Task EnqueueAndWaitAsync(GameAction action)
    {
        RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(action);
        await action.CompletionTask.WaitAsync(StateTimeout);
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions().WaitAsync(StateTimeout);
        if (action.Exception is { } exception)
        {
            throw exception.GetBaseException();
        }

        if (action.State != GameActionState.Finished)
        {
            throw new InvalidOperationException(
                $"Game action ended in unexpected state {action.State}: {action}.");
        }
    }

    public static async Task EndTurnAndWaitForNextPlayAsync(Player player)
    {
        var combatState = player.PlayerCombatState
            ?? throw new InvalidOperationException("Player has no combat state before ending the turn.");
        var previousTurn = combatState.TurnNumber;
        await EnqueueAndWaitAsync(new EndPlayerTurnAction(player, previousTurn));
        await WaitUntilAsync(
            () => CombatManager.Instance.IsInProgress
                && player.PlayerCombatState is
                {
                    Phase: PlayerTurnPhase.Play,
                    TurnNumber: > 1
                } current
                && current.TurnNumber > previousTurn,
            $"player Play phase after turn {previousTurn}");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions().WaitAsync(StateTimeout);
    }

    public static async Task WaitUntilAsync(Func<bool> predicate, string description)
    {
        var game = NGame.Instance
            ?? throw new InvalidOperationException("NGame became unavailable while awaiting runtime state.");
        var stopwatch = Stopwatch.StartNew();
        while (!predicate())
        {
            if (stopwatch.Elapsed >= StateTimeout)
            {
                throw new TimeoutException(
                    $"Timed out waiting for runtime state: {description}.");
            }

            await game.AwaitProcessFrame();
        }
    }
}
