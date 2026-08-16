using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.TestSupport;
using SakuraMod.SakuraModCode.Cards;
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

        var choice = await CombatScenarioContext.AddGeneratedCardToHandAsync<Choice>(combat, player);
        var energyBefore = playerCombat.Energy;
        var drawBefore = playerCombat.DrawPile.Cards.Count;
        var triggerBefore = SakuraActions.ExtraEffectTriggerCountThisTurn(player);
        var selector = new TestCardSelector();
        selector.PrepareToSelect([1]);
        PlayCardAction playAction;
        using (CardSelectCmd.UseSelector(selector))
        {
            playAction = await CombatScenarioContext.PlayCardAsync(choice);
        }

        var magicAfter = player.Creature.GetPower<ClassicMagicChargePower>()?.Amount ?? 0;
        var triggerAfter = SakuraActions.ExtraEffectTriggerCountThisTurn(player);
        var drawAfter = playerCombat.DrawPile.Cards.Count;
        assertions.True(
            "play_choice_context",
            playAction.PlayerChoiceContext is GameActionPlayerChoiceContext);
        assertions.Equal("magic_charge_spent_once_then_regained", 1, magicAfter);
        assertions.Equal("extra_effect_trigger_delta", 1, triggerAfter - triggerBefore);
        assertions.Equal("choice_draw_count", 4, drawBefore - drawAfter);
        assertions.Equal("choice_energy_unchanged", energyBefore, playerCombat.Energy);
        assertions.True("choice_result_pile", playerCombat.DiscardPile.Cards.Contains(choice));
        assertions.Equal("choice_selector_released", null, CardSelectCmd.Selector);
        RuntimeTestHost.WriteCheckpoint(
            request,
            "extra_effect_choice_verified",
            "Choice spent Magic Charge and resolved the deterministic Draw branch through PlayCardAction.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["fixture"] = new
            {
                seed = request.Seed.ToString("X16"),
                card = typeof(Choice).FullName,
                setup_mutations = new[]
                {
                    $"RuntimeFixtureAction -> PowerCmd.Apply<{nameof(ClassicMagicChargePower)}>({FixtureMagicCharge})",
                    $"Generated {nameof(Choice)} -> hand",
                    "TestCardSelector index 1 -> ChoiceDrawChoice"
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
                play_choice_context = playAction.PlayerChoiceContext?.GetType().FullName
            }
        };
    }
}
