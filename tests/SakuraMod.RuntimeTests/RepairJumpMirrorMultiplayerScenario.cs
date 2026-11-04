using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.TestSupport;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal static class RepairJumpMirrorMultiplayerScenario
{
    private const int FixtureMagicCharge = 20;

    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        SakuraRuntimeEnvironment environment,
        RuntimeAssertionCollector assertions)
    {
        if (request.ScenarioId is SakuraMultiplayerScenarios.RepairJumpMirrorThreePlayerRace
            or SakuraMultiplayerScenarios.RepairJumpMirrorThreePlayerForeseerRace)
        {
            return await ExecuteRaceAsync(request, environment, assertions);
        }

        if (request.ScenarioId == SakuraMultiplayerScenarios.RepairJumpMirrorThreePlayerChoiceRace)
            return await ExecuteChoiceRaceAsync(request, environment, assertions);

        await using var context = await MultiplayerScenarioContext.StartAsync(request);
        var combat = await context.EnterWeakCrawlerCombatAsync();
        var client = context.ClientPlayer;
        var fixtureContext = new ThrowingPlayerChoiceContext();

        var repair = combat.CreateCard<Repair>(client);
        var jump = combat.CreateCard<ClowJump>(client);
        await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
            repair, PileType.Hand, client, CardPilePosition.Random);
        await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
            jump, PileType.Hand, client, CardPilePosition.Random);
        await PowerCmd.Apply<ClassicMagicChargePower>(
            fixtureContext,
            client.Creature,
            FixtureMagicCharge,
            client.Creature,
            null,
            silent: true);
        await PowerCmd.Apply<VulnerablePower>(
            fixtureContext,
            client.Creature,
            2,
            client.Creature,
            null,
            silent: true);
        await context.SignalAndWaitAsync("repair-jump-fixture-ready");

        if (context.LocalPlayer.NetId == client.NetId)
            await context.PlayOwnedCardAsync(repair);
        await context.SignalAndWaitAsync("repair-played");
        await MultiplayerScenarioContext.WaitForStateAsync(
            () => repair.Pile?.Type == PileType.Exhaust
                && client.Creature.GetPower<RegenPower>()?.Amount == repair.DynamicVars["RegenPower"].IntValue + Repair.ExtraRegen,
            "client Repair extra effect to synchronize");

        var regenBeforeJump = client.Creature.GetPower<RegenPower>()?.Amount ?? 0;
        assertions.True("repair_regen_positive_before_jump", regenBeforeJump > 0);
        assertions.True("vulnerable_present_before_jump", client.Creature.HasPower<VulnerablePower>());

        if (context.LocalPlayer.NetId == client.NetId)
            await context.PlayOwnedCardAsync(jump);
        await context.SignalAndWaitAsync("jump-played");
        await MultiplayerScenarioContext.WaitForStateAsync(
            () => jump.Pile?.Type == PileType.Exhaust
                && !client.Creature.HasPower<VulnerablePower>(),
            "client activated Jump to remove Vulnerable");

        assertions.Equal(
            "repair_regen_retained_after_jump",
            regenBeforeJump,
            client.Creature.GetPower<RegenPower>()?.Amount ?? 0);
        assertions.Equal("vulnerable_removed_by_jump", false, client.Creature.HasPower<VulnerablePower>());

        foreach (var card in client.PlayerCombatState!.Hand.Cards.ToArray())
        {
            await SakuraActions.MoveExistingCardToPileWithoutVisuals(
                null,
                card,
                PileType.Discard,
                CardPilePosition.Bottom);
        }

        var mirrorTarget = combat.CreateCard<ClowSword>(client);
        var mirror = combat.CreateCard<ClowMirror>(client);
        await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
            mirrorTarget, PileType.Hand, client, CardPilePosition.Random);
        await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
            mirror, PileType.Hand, client, CardPilePosition.Random);
        var deckCountBeforeMirror = client.Deck.Cards.Count;
        var deckSwordCountBeforeMirror = client.Deck.Cards.OfType<ClowSword>().Count();
        await context.SignalAndWaitAsync("mirror-fixture-ready");

        if (context.LocalPlayer.NetId == client.NetId)
            await context.PlayOwnedCardAsync(mirror);
        await context.SignalAndWaitAsync("mirror-played");
        await MultiplayerScenarioContext.WaitForStateAsync(
            () => mirror.Pile?.Type == PileType.Exhaust
                && client.PlayerCombatState!.Hand.Cards.OfType<ClowSword>().Count() == 2,
            "client Mirror copy to synchronize");

        assertions.Equal("mirror_deck_count_unchanged", deckCountBeforeMirror, client.Deck.Cards.Count);
        assertions.Equal(
            "mirror_deck_sword_count_unchanged",
            deckSwordCountBeforeMirror,
            client.Deck.Cards.OfType<ClowSword>().Count());
        assertions.Equal(
            "mirror_two_swords_in_combat_hand",
            2,
            client.PlayerCombatState!.Hand.Cards.OfType<ClowSword>().Count());

        foreach (var enemy in combat.Enemies.Where(static enemy => enemy.IsAlive))
            await CreatureCmd.Stun(enemy);
        await context.SignalAndWaitAsync("enemies-stunned");
        foreach (var player in context.Run.Players.OrderBy(static player => player.NetId))
        {
            if (context.LocalPlayer.NetId == player.NetId)
                await context.EndLocalTurnAsync();
            await context.SignalAndWaitAsync($"turn-ended-{player.NetId}");
        }

        await MultiplayerScenarioContext.WaitForStateAsync(
            () => context.Run.Players.All(static player => player.PlayerCombatState is
            {
                Phase: PlayerTurnPhase.Play,
                TurnNumber: > 1
            }),
            $"all {context.Run.Players.Count} peers to reach the next player turn");
        assertions.Equal(
            "repair_regen_decays_once_after_turn_boundary",
            regenBeforeJump - 1,
            client.Creature.GetPower<RegenPower>()?.Amount ?? 0);
        assertions.Equal(
            "mirror_deck_count_unchanged_after_turn_boundary",
            deckCountBeforeMirror,
            client.Deck.Cards.Count);

        context.ThrowIfNetworkFailed();
        await context.SignalAndWaitAsync("comparison-ready");
        RuntimeTestHost.WriteCheckpoint(
            request,
            "repair_jump_mirror_multiplayer_verified",
            $"Client Repair, Jump, and Mirror state converged across all {context.Run.Players.Count} peers.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["peer"] = new
            {
                role = request.Multiplayer!.Role,
                local_net_id = context.LocalPlayer.NetId,
                player_count = context.Run.Players.Count
            },
            ["comparison"] = new
            {
                versions = new
                {
                    environment.GameVersion,
                    environment.RitsuVersion,
                    environment.SakuraVersion
                },
                client_net_id = client.NetId,
                regen = client.Creature.GetPower<RegenPower>()?.Amount ?? 0,
                vulnerable = client.Creature.HasPower<VulnerablePower>(),
                deck_count_before_mirror = deckCountBeforeMirror,
                deck_count_after_turn = client.Deck.Cards.Count,
                deck_swords_before_mirror = deckSwordCountBeforeMirror,
                deck_swords_after_turn = client.Deck.Cards.OfType<ClowSword>().Count(),
                combat_swords = client.PlayerCombatState!.AllPiles
                    .SelectMany(static pile => pile.Cards)
                    .OfType<ClowSword>()
                    .Count(),
                turn = client.PlayerCombatState.TurnNumber
            }
        };
    }

    private static async Task<Dictionary<string, object?>> ExecuteRaceAsync(
        SakuraTestRequest request,
        SakuraRuntimeEnvironment environment,
        RuntimeAssertionCollector assertions)
    {
        await using var context = await MultiplayerScenarioContext.StartAsync(request);
        var combat = await context.EnterWeakCrawlerCombatAsync();
        var client = context.ClientPlayer;
        var fixtureContext = new ThrowingPlayerChoiceContext();

        var repair = combat.CreateCard<Repair>(client);
        var jump = combat.CreateCard<ClowJump>(client);
        var mirrorTarget = combat.CreateCard<ClowSword>(client);
        var mirror = combat.CreateCard<ClowMirror>(client);
        foreach (var card in new CardModel[] { repair, jump, mirrorTarget, mirror })
        {
            await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
                card,
                PileType.Hand,
                client,
                CardPilePosition.Random);
        }

        await PowerCmd.Apply<ClassicMagicChargePower>(
            fixtureContext,
            client.Creature,
            FixtureMagicCharge,
            client.Creature,
            null,
            silent: true);
        await PowerCmd.Apply<VulnerablePower>(
            fixtureContext,
            client.Creature,
            2,
            client.Creature,
            null,
            silent: true);
        var deckCountBeforeMirror = client.Deck.Cards.Count;
        var deckSwordCountBeforeMirror = client.Deck.Cards.OfType<ClowSword>().Count();
        await context.SignalAndWaitAsync("race-fixture-ready");

        if (context.LocalPlayer.NetId == client.NetId)
        {
            // Queue all three cards without waiting for the previous action to
            // finish. This leaves Mirror's choice pending while other peers
            // submit their end-turn requests.
            var repairAction = context.RequestOwnedCard(repair);
            var jumpAction = context.RequestOwnedCard(jump);
            await Task.WhenAll(
                context.WaitForOwnedCardAsync(repairAction, repair),
                context.WaitForOwnedCardAsync(jumpAction, jump));
        }
        else
        {
            await context.EndLocalTurnAsync();
        }

        await context.SignalAndWaitAsync("race-actions-finished");
        await MultiplayerScenarioContext.WaitForStateAsync(
            () => repair.Pile?.Type == PileType.Exhaust
                && jump.Pile?.Type == PileType.Exhaust
                && client.Creature.GetPower<RegenPower>()?.Amount == repair.DynamicVars["RegenPower"].IntValue + Repair.ExtraRegen,
            "raced Repair, Jump, and peer turn actions to synchronize");

        var regenAfterRace = client.Creature.GetPower<RegenPower>()?.Amount ?? 0;
        assertions.Equal("race_repair_regen_retained", repair.DynamicVars["RegenPower"].IntValue + Repair.ExtraRegen, regenAfterRace);
        assertions.Equal("race_vulnerable_removed", false, client.Creature.HasPower<VulnerablePower>());

        if (context.LocalPlayer.NetId == client.NetId)
            await context.PlayOwnedCardAsync(mirror);
        await context.SignalAndWaitAsync("race-mirror-played");
        await MultiplayerScenarioContext.WaitForStateAsync(
            () => mirror.Pile?.Type == PileType.Exhaust
                && client.PlayerCombatState!.Hand.Cards.OfType<ClowSword>().Count() == 2,
            "sequential Mirror action after the Repair/Jump race");

        assertions.Equal("race_mirror_deck_count_unchanged", deckCountBeforeMirror, client.Deck.Cards.Count);
        assertions.Equal(
            "race_mirror_deck_sword_count_unchanged",
            deckSwordCountBeforeMirror,
            client.Deck.Cards.OfType<ClowSword>().Count());

        if (context.LocalPlayer.NetId == client.NetId)
            await context.EndLocalTurnAsync();
        await context.SignalAndWaitAsync("race-turn-ended");
        await MultiplayerScenarioContext.WaitForStateAsync(
            () => context.Run.Players.All(static player => player.PlayerCombatState is
            {
                Phase: PlayerTurnPhase.Play,
                TurnNumber: > 1
            }),
            "all raced peers to reach the next player turn");

        assertions.Equal(
            "race_repair_regen_decays_once_after_turn",
            regenAfterRace - 1,
            client.Creature.GetPower<RegenPower>()?.Amount ?? 0);
        assertions.Equal(
            "race_mirror_deck_count_unchanged_after_turn",
            deckCountBeforeMirror,
            client.Deck.Cards.Count);
        context.ThrowIfNetworkFailed();
        await context.SignalAndWaitAsync("race-comparison-ready");
        RuntimeTestHost.WriteCheckpoint(
            request,
            "repair_jump_mirror_race_verified",
            "Concurrent Repair, Jump, Mirror, and peer turn actions converged across all peers.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["peer"] = new
            {
                role = request.Multiplayer!.Role,
                local_net_id = context.LocalPlayer.NetId,
                player_count = context.Run.Players.Count
            },
            ["comparison"] = new
            {
                versions = new
                {
                    environment.GameVersion,
                    environment.RitsuVersion,
                    environment.SakuraVersion
                },
                client_net_id = client.NetId,
                regen = client.Creature.GetPower<RegenPower>()?.Amount ?? 0,
                vulnerable = client.Creature.HasPower<VulnerablePower>(),
                deck_count_before_mirror = deckCountBeforeMirror,
                deck_count_after_turn = client.Deck.Cards.Count,
                deck_swords_before_mirror = deckSwordCountBeforeMirror,
                deck_swords_after_turn = client.Deck.Cards.OfType<ClowSword>().Count(),
                combat_swords = client.PlayerCombatState!.AllPiles
                    .SelectMany(static pile => pile.Cards)
                    .OfType<ClowSword>()
                    .Count(),
                turn = client.PlayerCombatState.TurnNumber
            }
        };
    }

    private static async Task<Dictionary<string, object?>> ExecuteChoiceRaceAsync(
        SakuraTestRequest request,
        SakuraRuntimeEnvironment environment,
        RuntimeAssertionCollector assertions)
    {
        await using var context = await MultiplayerScenarioContext.StartAsync(request);
        var combat = await context.EnterWeakCrawlerCombatAsync();
        var client = context.ClientPlayer;
        // Let the local selector hold the choice open; the multiplayer harness
        // normally enables AutoSlay, which would answer Mirror immediately.
        NonInteractiveMode.AutoSlayerCheck = static () => false;

        foreach (var card in client.PlayerCombatState!.Hand.Cards.ToArray())
        {
            await SakuraActions.MoveExistingCardToPileWithoutVisuals(
                null,
                card,
                PileType.Discard,
                CardPilePosition.Bottom);
        }

        var mirrorTarget = combat.CreateCard<ClowSword>(client);
        var mirror = combat.CreateCard<ClowMirror>(client);
        await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
            mirrorTarget,
            PileType.Hand,
            client,
            CardPilePosition.Bottom);
        await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
            mirror,
            PileType.Hand,
            client,
            CardPilePosition.Bottom);

        var deckCountBeforeMirror = client.Deck.Cards.Count;
        var deckSwordCountBeforeMirror = client.Deck.Cards.OfType<ClowSword>().Count();
        await context.SignalAndWaitAsync("mirror-choice-race-fixture-ready");

        GatedCardSelector? selector = null;
        IDisposable? selectorScope = null;
        var suspendedSelectorScope = CardSelectCmd.SuspendSelectorForTest();
        var suspendedLocalSelectorScope = CardSelectCmd.SuspendSelectorForTest(localOnly: true);
        try
        {
            if (context.LocalPlayer.NetId == client.NetId)
            {
                selector = new GatedCardSelector();
                selectorScope = CardSelectCmd.UseSelector(selector, localOnly: true);
                context.RequestOwnedCard(mirror);
                await MultiplayerScenarioContext.WaitForStateAsync(
                    () => selector.ChoiceStarted.Task.IsCompleted,
                    "Clow Mirror choice to open on client 2");
            }

            // Host and client 3 enter this barrier immediately; client 2 only
            // arrives after its Mirror selection is visibly waiting.
            await context.SignalAndWaitAsync("mirror-choice-race-choice-open");

            if (context.LocalPlayer.NetId != client.NetId)
                await context.EndLocalTurnAsync();
            await context.SignalAndWaitAsync("mirror-choice-race-peer-turns");

            if (context.LocalPlayer.NetId == client.NetId)
                selector!.ReleaseChoice();

            await MultiplayerScenarioContext.WaitForStateAsync(
                () => mirror.Pile?.Type == PileType.Exhaust
                    && client.PlayerCombatState!.Hand.Cards.OfType<ClowSword>().Count() == 2,
                "Clow Mirror to resolve after peer end-turn actions");
            assertions.Equal("choice_race_mirror_deck_count_unchanged", deckCountBeforeMirror, client.Deck.Cards.Count);
            assertions.Equal(
                "choice_race_mirror_deck_sword_count_unchanged",
                deckSwordCountBeforeMirror,
                client.Deck.Cards.OfType<ClowSword>().Count());

            await context.SignalAndWaitAsync("mirror-choice-race-resolved");

            if (context.LocalPlayer.NetId == client.NetId)
                await context.EndLocalTurnAsync();
            await context.SignalAndWaitAsync("mirror-choice-race-turn-ended");
            await MultiplayerScenarioContext.WaitForStateAsync(
                () => context.Run.Players.All(static player => player.PlayerCombatState is
                {
                    Phase: PlayerTurnPhase.Play,
                    TurnNumber: > 1
                }),
                "all peers to reach the next turn after the choice race");
            assertions.Equal(
                "choice_race_mirror_deck_count_unchanged_after_turn",
                deckCountBeforeMirror,
                client.Deck.Cards.Count);
            context.ThrowIfNetworkFailed();
            await context.SignalAndWaitAsync("mirror-choice-race-comparison-ready");
            RuntimeTestHost.WriteCheckpoint(
                request,
                "repair_jump_mirror_choice_race_verified",
                "Clow Mirror choice remained pending while peer end-turn actions resolved without adding the copy to the deck.");

            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["peer"] = new
                {
                    role = request.Multiplayer!.Role,
                    local_net_id = context.LocalPlayer.NetId,
                    player_count = context.Run.Players.Count
                },
                ["comparison"] = new
                {
                    versions = new
                    {
                        environment.GameVersion,
                        environment.RitsuVersion,
                        environment.SakuraVersion
                    },
                    client_net_id = client.NetId,
                    deck_count_before_mirror = deckCountBeforeMirror,
                    deck_count_after_turn = client.Deck.Cards.Count,
                    deck_swords_before_mirror = deckSwordCountBeforeMirror,
                    deck_swords_after_turn = client.Deck.Cards.OfType<ClowSword>().Count(),
                    combat_swords = client.PlayerCombatState!.AllPiles
                        .SelectMany(static pile => pile.Cards)
                        .OfType<ClowSword>()
                        .Count(),
                    turn = client.PlayerCombatState.TurnNumber
                }
            };
        }
        finally
        {
            selectorScope?.Dispose();
            suspendedLocalSelectorScope.Dispose();
            suspendedSelectorScope.Dispose();
        }
    }

    private sealed class GatedCardSelector : ICardSelector
    {
        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ChoiceStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<IEnumerable<CardModel>> GetSelectedCards(
            IEnumerable<CardModel> options,
            int minSelect,
            int maxSelect)
        {
            var selected = options.FirstOrDefault(static card => card is ClowSword)
                ?? throw new InvalidOperationException("Clow Mirror choice race has no ClowSword candidate.");
            ChoiceStarted.TrySetResult();
            await _release.Task;
            return [selected];
        }

        public CardRewardSelection GetSelectedCardReward(
            IReadOnlyList<CardCreationResult> options,
            IReadOnlyList<CardRewardAlternative> alternatives) =>
            new() { card = options.FirstOrDefault()?.Card };

        public void ReleaseChoice() => _release.TrySetResult();
    }
}
