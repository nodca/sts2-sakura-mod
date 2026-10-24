using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.TestSupport;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal static class ExtraEffectChoiceScenario
{
    private const int FixtureMagicCharge = 10;

    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions)
    {
        var context = await CombatScenarioContext.StartAsync(request);
        var combat = await context.EnterWeakCrawlerCombatAsync();
        var player = context.Player;
        var playerCombat = player.PlayerCombatState
            ?? throw new InvalidOperationException("Player combat state is unavailable.");

        var fixtureAction = new RuntimeFixtureAction(
            player,
            choiceContext => PowerCmd.Apply<ClassicMagicChargePower>(
                choiceContext,
                player.Creature,
                FixtureMagicCharge,
                player.Creature,
                null,
                silent: true));
        await CombatScenarioContext.EnqueueAndWaitAsync(fixtureAction);
        assertions.True(
            "fixture_choice_context",
            fixtureAction.ChoiceContext is GameActionPlayerChoiceContext);
        assertions.Equal(
            "magic_charge_before_play",
            FixtureMagicCharge,
            player.Creature.GetPower<ClassicMagicChargePower>()?.Amount ?? 0);

        var memory = SakuraMemoryPile.Get(player)
            ?? throw new InvalidOperationException("Memory pile is unavailable.");
        var rememberedSource = await CombatScenarioContext.AddGeneratedCardToHandAsync<Snooze>(combat, player);
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(
            player,
            async _ => await SakuraActions.MoveExistingCardToPileWithoutVisuals(
                null,
                rememberedSource,
                SakuraMemoryPile.PileType,
                CardPilePosition.Bottom)));
        assertions.Equal("fixture_memory_seeded", 1, memory.Cards.Count);

        var choice = await CombatScenarioContext.AddGeneratedCardToHandAsync<Choice>(combat, player);
        var energyBefore = playerCombat.Energy;
        var drawBefore = playerCombat.DrawPile.Cards.Count;
        var triggerBefore = SakuraActions.ExtraEffectTriggerCountThisTurn(player);
        var selector = new TestCardSelector();
        selector.PrepareToSelect([0]);
        PlayCardAction playAction;
        using (CardSelectCmd.UseSelector(selector))
        {
            playAction = await CombatScenarioContext.PlayCardAsync(choice);
        }

        var magicAfter = player.Creature.GetPower<ClassicMagicChargePower>()?.Amount ?? 0;
        var triggerAfter = SakuraActions.ExtraEffectTriggerCountThisTurn(player);
        var drawAfter = playerCombat.DrawPile.Cards.Count;
        var manifestedCards = playerCombat.Hand.Cards
            .Where(card => card.IsTemporary() && SakuraTransparentCardCatalog.IsTransparentCard(card))
            .ToArray();
        assertions.True(
            "play_choice_context",
            playAction.PlayerChoiceContext is GameActionPlayerChoiceContext);
        assertions.Equal("magic_charge_spent_once_then_regained", 1, magicAfter);
        assertions.Equal("extra_effect_trigger_delta", 1, triggerAfter - triggerBefore);
        assertions.Equal("choice_extra_does_not_draw", 0, drawBefore - drawAfter);
        assertions.Equal("choice_extra_remind_consumed_memory", 0, memory.Cards.Count);
        assertions.Equal(
            "choice_extra_remind_copy_in_hand",
            1,
            playerCombat.Hand.Cards.Count(card => card is Snooze && !card.IsTemporary()));
        assertions.Equal("choice_extra_manifest_count", 1, manifestedCards.Length);
        assertions.Equal("choice_energy_spent", energyBefore - 1, playerCombat.Energy);
        assertions.True("choice_result_pile", playerCombat.DiscardPile.Cards.Contains(choice));
        assertions.Equal("choice_selector_released", null, CardSelectCmd.Selector);
        RuntimeTestHost.WriteCheckpoint(
            request,
            "extra_effect_choice_verified",
            "Choice spent 1 energy and resolved Extra Manifest plus Remind.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["fixture"] = new
            {
                seed = request.Seed.ToString("X16"),
                card = typeof(Choice).FullName,
                setup_mutations = new[]
                {
                    $"RuntimeFixtureAction -> PowerCmd.Apply<{nameof(ClassicMagicChargePower)}>({FixtureMagicCharge})",
                    $"Generated {nameof(Snooze)} -> Memory pile",
                    $"Generated {nameof(Choice)} -> hand",
                    "TestCardSelector index 0 -> first Manifest atlas option"
                }
            },
            ["before"] = new
            {
                magic_charge = FixtureMagicCharge,
                energy = energyBefore,
                draw_count = drawBefore,
                trigger_count = triggerBefore
            },
            ["after"] = new
            {
                magic_charge = magicAfter,
                energy = playerCombat.Energy,
                draw_count = drawAfter,
                trigger_count = triggerAfter,
                result_pile = choice.Pile?.Type,
                manifested_count = manifestedCards.Length,
                play_choice_context = playAction.PlayerChoiceContext?.GetType().FullName
            }
        };
    }
}
