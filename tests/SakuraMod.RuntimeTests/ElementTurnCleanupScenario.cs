using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal static class ElementTurnCleanupScenario
{
    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions)
    {
        var context = await CombatScenarioContext.StartAsync(request);
        var combat = await context.EnterWeakCrawlerCombatAsync();
        var player = context.Player;
        var enemy = combat.Enemies.Single();
        decimal poweredBlockGain = 0m;
        decimal unpoweredBlockGain = 0m;

        var temporary = combat.CreateCard<Spiral>(player);
        await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
            temporary,
            new GeneratedCardOptions
            {
                Pile = PileType.Hand,
                AddTemporary = true
            });
        var fixtureAction = new RuntimeFixtureAction(
            player,
            async choiceContext =>
            {
                await PowerCmd.Apply<ClassicFireyPower>(
                    choiceContext,
                    player.Creature,
                    1,
                    player.Creature,
                    null,
                    silent: true);
                await PowerCmd.Apply<ClassicEarthyPower>(
                    choiceContext,
                    player.Creature,
                    1,
                    player.Creature,
                    null,
                    silent: true);
                await PowerCmd.Apply<ClassicEarthyPermanentPower>(
                    choiceContext,
                    player.Creature,
                    1,
                    player.Creature,
                    null,
                    silent: true);
                await PowerCmd.Apply<ClassicFloatSakuraPower>(
                    choiceContext,
                    player.Creature,
                    2,
                    player.Creature,
                    null,
                    silent: true);
                poweredBlockGain = await CreatureCmd.GainBlock(
                    player.Creature,
                    5,
                    ValueProp.Move,
                    null,
                    fast: true);
                unpoweredBlockGain = await CreatureCmd.GainBlock(
                    player.Creature,
                    4,
                    ValueProp.Unpowered,
                    null,
                    fast: true);
            });
        await CombatScenarioContext.EnqueueAndWaitAsync(fixtureAction);

        assertions.True("element_fixture_choice_context", fixtureAction.ChoiceContext is GameActionPlayerChoiceContext);
        assertions.Equal("firey_before_turn_end", 1, player.Creature.GetPower<ClassicFireyPower>()?.Amount ?? 0);
        assertions.Equal("earthy_before_turn_end", 1, player.Creature.GetPower<ClassicEarthyPower>()?.Amount ?? 0);
        assertions.True("earthy_permanent_before_turn_end", player.Creature.HasPower<ClassicEarthyPermanentPower>());
        assertions.Equal("sakura_float_modifies_powered_block", 7m, poweredBlockGain);
        assertions.Equal("sakura_float_modifies_unpowered_block", 6m, unpoweredBlockGain);
        assertions.Equal("enemy_firey_before_turn_end", 0, enemy.GetPower<ClassicFireyPower>()?.Amount ?? 0);
        assertions.True("temporary_before_turn_end", temporary.IsTemporary());

        var snow = await CombatScenarioContext.AddGeneratedCardToHandAsync<SakuraSnow>(combat, player);
        await VerifySnowVisualCleanupAsync(snow, player.Creature, enemy, assertions);
        await CombatScenarioContext.PlayCardAsync(snow);
        assertions.Equal("snow_count_after_first_play", 1, SakuraSnowRules.PlayedWateryCards(snow));
        var replaySnow = new RuntimeFixtureAction(
            player,
            async _ => await SakuraActions.MoveExistingCardToPileWithoutVisuals(
                null,
                snow,
                PileType.Hand,
                CardPilePosition.Bottom));
        await CombatScenarioContext.EnqueueAndWaitAsync(replaySnow);
        await CombatScenarioContext.PlayCardAsync(snow);
        assertions.Equal("snow_count_after_second_play", 2, SakuraSnowRules.PlayedWateryCards(snow));

        await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
        var playerCombat = player.PlayerCombatState
            ?? throw new InvalidOperationException("Player combat state disappeared after the turn boundary.");
        assertions.Equal("turn_boundary_reached", 2, playerCombat.TurnNumber);
        assertions.Equal("firey_cleared_after_turn", 0, player.Creature.GetPower<ClassicFireyPower>()?.Amount ?? 0);
        assertions.Equal("earthy_retained_after_turn", 1, player.Creature.GetPower<ClassicEarthyPower>()?.Amount ?? 0);
        assertions.True("earthy_permanent_retained", player.Creature.HasPower<ClassicEarthyPermanentPower>());
        assertions.Equal("temporary_retained_after_turn", false, temporary.HasBeenRemovedFromState);
        assertions.True(
            "temporary_enters_memory_after_turn",
            ReferenceEquals(temporary.Pile, SakuraMemoryPile.Get(player)));
        assertions.Equal("temporary_state_cleared_after_turn", false, temporary.IsTemporary());
        assertions.Equal("enemy_firey_after_turn", 0, enemy.GetPower<ClassicFireyPower>()?.Amount ?? 0);
        assertions.Equal("enemy_earthy_after_turn", 0, enemy.GetPower<ClassicEarthyPower>()?.Amount ?? 0);
        RuntimeTestHost.WriteCheckpoint(
            request,
            "element_turn_cleanup_verified",
            "Element duration and Temporary cleanup crossed a complete player/enemy turn boundary.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["fixture"] = new
            {
                seed = request.Seed.ToString("X16"),
                temporary_card = typeof(Spiral).FullName,
                setup_mutations = new[]
                {
                    "Generated Temporary Spiral -> hand",
                    $"RuntimeFixtureAction -> {nameof(ClassicFireyPower)}(1)",
                    $"RuntimeFixtureAction -> {nameof(ClassicEarthyPower)}(1)",
                    $"RuntimeFixtureAction -> {nameof(ClassicEarthyPermanentPower)}(1)",
                    $"RuntimeFixtureAction -> {nameof(ClassicFloatSakuraPower)}(2)",
                    "RuntimeFixtureAction -> gain 5 powered and 4 unpowered Block"
                }
            },
            ["after_turn"] = new
            {
                turn = playerCombat.TurnNumber,
                firey = player.Creature.GetPower<ClassicFireyPower>()?.Amount ?? 0,
                earthy = player.Creature.GetPower<ClassicEarthyPower>()?.Amount ?? 0,
                earthy_permanent = player.Creature.HasPower<ClassicEarthyPermanentPower>(),
                temporary_memory_pile = temporary.Pile?.Type,
                enemy_firey = enemy.GetPower<ClassicFireyPower>()?.Amount ?? 0,
                enemy_earthy = enemy.GetPower<ClassicEarthyPower>()?.Amount ?? 0
            }
        };
    }

    private static async Task VerifySnowVisualCleanupAsync(
        CardModel card,
        Creature caster,
        Creature target,
        RuntimeAssertionCollector assertions)
    {
        var room = NCombatRoom.Instance ?? throw new InvalidOperationException("Snow needs a live combat room.");
        int FrontCount() => room.CombatVfxContainer.GetChildren()
            .OfType<Node2D>().Count(node => node.Name == "SakuraSnowBlizzardVfx");
        int BackCount() => room.BackCombatVfxContainer.GetChildren()
            .OfType<Node2D>().Count(node => node.Name == "SakuraSnowBlizzardBackVfx");

        await SnowBlizzardVfx.PlayOrResolveAsync(card, caster, [target], cues =>
        {
            assertions.Equal("snow_front_layer_created", 1, FrontCount());
            assertions.Equal("snow_back_layer_created", 1, BackCount());
            for (var hit = 0; hit < 12; hit++)
                cues.Impact(target);
            cues.Finale();
            assertions.Equal("snow_repeated_hits_keep_one_front_root", 1, FrontCount());
            assertions.Equal("snow_repeated_hits_keep_one_back_root", 1, BackCount());
            return Task.CompletedTask;
        });
        await CombatScenarioContext.WaitUntilAsync(() => FrontCount() == 0 && BackCount() == 0, "Snow outro cleanup");
        assertions.Equal("snow_outro_front_cleared", 0, FrontCount());
        assertions.Equal("snow_outro_back_cleared", 0, BackCount());

        var gameplayFailure = new InvalidOperationException("Snow visual cleanup fixture");
        try
        {
            await SnowBlizzardVfx.PlayOrResolveAsync(card, caster, [target], _ => throw gameplayFailure);
            throw new InvalidOperationException("Snow swallowed the fixture gameplay failure.");
        }
        catch (InvalidOperationException exception) when (ReferenceEquals(exception, gameplayFailure))
        {
            assertions.True("snow_gameplay_exception_preserved", true);
        }
        await CombatScenarioContext.WaitUntilAsync(() => FrontCount() == 0 && BackCount() == 0, "Snow failure cleanup");
        assertions.Equal("snow_failure_front_cleared", 0, FrontCount());
        assertions.Equal("snow_failure_back_cleared", 0, BackCount());
    }
}
