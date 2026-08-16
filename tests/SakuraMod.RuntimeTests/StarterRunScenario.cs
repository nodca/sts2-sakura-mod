using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Relics;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal static class StarterRunScenario
{
    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions)
    {
        var context = await CombatScenarioContext.StartAsync(request);
        var run = context.Run;
        var player = context.Player;
        var deckTypes = player.Deck.Cards
            .Select(card => card.GetType().Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var relicTypes = player.Relics
            .Select(relic => relic.GetType().Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        assertions.Equal("run_player_count", 1, run.Players.Count);
        assertions.True("run_character_identity", player.Character is ClassicSakura);
        assertions.Equal("run_game_mode", GameMode.Standard, run.GameMode);
        assertions.Equal("run_ascension", 0, run.AscensionLevel);
        assertions.Equal("run_starting_hp", 70, player.Creature.CurrentHp);
        assertions.Equal("run_starting_max_hp", 70, player.Creature.MaxHp);
        assertions.Equal("run_starting_gold", 99, player.Gold);
        assertions.Equal("run_max_energy", 3, player.MaxEnergy);
        assertions.Equal("run_deck_size", 10, player.Deck.Cards.Count);
        assertions.Equal("run_deck_types", ExpectedDeckTypes, string.Join(",", deckTypes));
        assertions.True(
            "run_deck_ownership",
            player.Deck.Cards.All(card => ReferenceEquals(card.Owner, player)));
        assertions.Equal("run_relic_count", 2, player.Relics.Count);
        assertions.Equal("run_relic_types", ExpectedRelicTypes, string.Join(",", relicTypes));
        assertions.True("run_relic_ownership", player.Relics.All(relic => ReferenceEquals(relic.Owner, player)));
        assertions.True("run_card_pool", player.Character.CardPool is ClassicSakuraCardPool);
        assertions.True("run_relic_pool", player.Character.RelicPool is ClassicSakuraRelicPool);
        assertions.True("run_potion_pool", player.Character.PotionPool is ClassicSakuraPotionPool);
        RuntimeTestHost.WriteCheckpoint(
            request,
            "starter_inventory_verified",
            "Starter deck, relics, pools, and base run state were inspected.");

        var combat = await context.EnterWeakCrawlerCombatAsync();
        var playerCombat = player.PlayerCombatState
            ?? throw new InvalidOperationException("Player combat state is unavailable in Play phase.");
        var allCombatCards = playerCombat.AllCards.ToArray();
        assertions.True("first_room_is_combat", run.CurrentRoom is CombatRoom);
        assertions.Equal("first_room_type", RoomType.Monster, run.CurrentRoom?.RoomType);
        assertions.True("first_encounter_identity", combat.Encounter is FuzzyWurmCrawlerWeak);
        assertions.Equal("first_encounter_enemy_count", 1, combat.Enemies.Count);
        assertions.Equal("first_combat_round", 1, combat.RoundNumber);
        assertions.Equal("first_combat_side", CombatSide.Player, combat.CurrentSide);
        assertions.Equal("first_player_turn", 1, playerCombat.TurnNumber);
        assertions.Equal("first_player_phase", PlayerTurnPhase.Play, playerCombat.Phase);
        assertions.Equal("opening_hand_count", CombatManager.baseHandDrawCount, playerCombat.Hand.Cards.Count);
        assertions.Equal("opening_draw_count", 5, playerCombat.DrawPile.Cards.Count);
        assertions.Equal("opening_discard_count", 0, playerCombat.DiscardPile.Cards.Count);
        assertions.Equal("opening_exhaust_count", 0, playerCombat.ExhaustPile.Cards.Count);
        assertions.Equal("opening_play_count", 0, playerCombat.PlayPile.Cards.Count);
        assertions.Equal("opening_total_card_count", 10, allCombatCards.Length);
        assertions.Equal("opening_energy", 3, playerCombat.Energy);
        assertions.True(
            "opening_card_ownership",
            allCombatCards.All(card => ReferenceEquals(card.Owner, player)));
        assertions.True(
            "opening_deck_version_mapping",
            allCombatCards.All(card => card.DeckVersion is not null
                && player.Deck.Cards.Contains(card.DeckVersion)));
        RuntimeTestHost.WriteCheckpoint(
            request,
            "starter_combat_verified",
            "First encounter and opening combat state were inspected.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["fixture"] = new
            {
                seed = request.Seed.ToString("X16"),
                character = typeof(ClassicSakura).FullName,
                encounter = typeof(FuzzyWurmCrawlerWeak).FullName,
                setup_mutations = Array.Empty<string>()
            },
            ["run"] = new
            {
                player.Character.Id,
                run.GameMode,
                run.AscensionLevel,
                current_hp = player.Creature.CurrentHp,
                max_hp = player.Creature.MaxHp,
                player.Gold,
                player.MaxEnergy,
                deck = deckTypes,
                relics = relicTypes,
                card_pool = player.Character.CardPool.Id,
                relic_pool = player.Character.RelicPool.Id,
                potion_pool = player.Character.PotionPool.Id
            },
            ["combat"] = new
            {
                room_type = run.CurrentRoom?.RoomType,
                encounter = combat.Encounter?.Id,
                combat.RoundNumber,
                combat.CurrentSide,
                player_turn = playerCombat.TurnNumber,
                player_phase = playerCombat.Phase,
                energy = playerCombat.Energy,
                hand = playerCombat.Hand.Cards.Select(card => card.Id.ToString()).ToArray(),
                draw = playerCombat.DrawPile.Cards.Select(card => card.Id.ToString()).ToArray()
            }
        };
    }

    private static string ExpectedDeckTypes => string.Join(",", new[]
    {
        nameof(ClowShield), nameof(ClowShield), nameof(ClowShield), nameof(ClowShield),
        nameof(ClowSword), nameof(ClowSword), nameof(ClowSword), nameof(ClowSword),
        nameof(SpellRelease), nameof(SpellSeal)
    }.Order(StringComparer.Ordinal));

    private static string ExpectedRelicTypes => string.Join(",", new[]
    {
        nameof(ClassicSealedBookRelic), nameof(ClassicSealedWandRelic)
    }.Order(StringComparer.Ordinal));
}
