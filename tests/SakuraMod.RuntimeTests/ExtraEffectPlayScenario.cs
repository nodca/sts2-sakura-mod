using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.Relics;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal static class ExtraEffectPlayScenario
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
        var target = combat.HittableEnemies.First();

        var energyFixture = new RuntimeFixtureAction(
            player,
            _ => PlayerCmd.GainEnergy(10, player));
        await CombatScenarioContext.EnqueueAndWaitAsync(energyFixture);

        var triggerBeforeInactive = SakuraActions.ExtraEffectTriggerCountThisTurn(player);
        var inactiveGale = await CombatScenarioContext.AddGeneratedCardToHandAsync<Gale>(combat, player);
        var inactivePlay = await CombatScenarioContext.PlayCardAsync(inactiveGale, target);
        assertions.True(
            "inactive_gale_choice_context",
            inactivePlay.PlayerChoiceContext is GameActionPlayerChoiceContext);
        assertions.Equal(
            "inactive_gale_adds_no_temporary_copies",
            0,
            CountTemporaryGales(player));
        assertions.Equal(
            "inactive_gale_does_not_trigger_extra",
            triggerBeforeInactive,
            SakuraActions.ExtraEffectTriggerCountThisTurn(player));
        assertions.Equal(
            "inactive_gale_does_not_apply_wind",
            false,
            player.Creature.HasPower<ClassicWindyPower>());
        assertions.True("inactive_gale_result_pile", playerCombat.DiscardPile.Cards.Contains(inactiveGale));

        var armExtra = new RuntimeFixtureAction(
            player,
            async choiceContext =>
            {
                await SakuraMagicCharge.SpendAllMagic(choiceContext, player);
                await SakuraMagicCharge.GainMagic(choiceContext, player, FixtureMagicCharge);
            });
        await CombatScenarioContext.EnqueueAndWaitAsync(armExtra);
        assertions.Equal(
            "extra_charge_ready",
            FixtureMagicCharge,
            player.Creature.GetPower<ClassicMagicChargePower>()?.Amount ?? 0);

        var triggerBeforeExtra = SakuraActions.ExtraEffectTriggerCountThisTurn(player);
        var extraGale = await CombatScenarioContext.AddGeneratedCardToHandAsync<Gale>(combat, player);
        var extraPlay = await CombatScenarioContext.PlayCardAsync(extraGale, target);
        var extraCopies = playerCombat.Hand.Cards.OfType<Gale>().Where(static card => card.IsTemporary()).ToArray();
        assertions.True(
            "extra_gale_choice_context",
            extraPlay.PlayerChoiceContext is GameActionPlayerChoiceContext);
        assertions.Equal("extra_gale_temporary_copy_count", 2, extraCopies.Length);
        assertions.True(
            "extra_gale_copies_are_temporary_gales",
            extraCopies.All(static copy => copy is Gale && copy.IsTemporary()));
        assertions.Equal(
            "extra_gale_trigger_delta",
            1,
            SakuraActions.ExtraEffectTriggerCountThisTurn(player) - triggerBeforeExtra);
        assertions.Equal(
            "extra_gale_spends_then_regains",
            1,
            player.Creature.GetPower<ClassicMagicChargePower>()?.Amount ?? 0);
        assertions.True("extra_gale_applies_wind", player.Creature.HasPower<ClassicWindyPower>());
        assertions.True("extra_gale_result_pile", playerCombat.DiscardPile.Cards.Contains(extraGale));

        var drawVoidsBeforeForm = CountVoids(player, PileType.Draw);
        var discardVoidsBeforeForm = CountVoids(player, PileType.Discard);
        var formShield = await CombatScenarioContext.AddGeneratedCardToHandAsync<SakuraShield>(combat, player);
        await CombatScenarioContext.PlayCardAsync(formShield);
        assertions.Equal("sakura_form_void_enters_draw", drawVoidsBeforeForm + 1, CountVoids(player, PileType.Draw));
        assertions.Equal(
            "sakura_form_void_does_not_enter_discard",
            discardVoidsBeforeForm,
            CountVoids(player, PileType.Discard));

        var drawVoidsBeforeReturn = CountVoids(player, PileType.Draw);
        var discardVoidsBeforeReturn = CountVoids(player, PileType.Discard);
        var printedReturn = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowReturn>(combat, player);
        await CombatScenarioContext.PlayCardAsync(printedReturn);
        assertions.Equal(
            "clow_return_printed_voids_enter_discard",
            discardVoidsBeforeReturn + 2,
            CountVoids(player, PileType.Discard));
        assertions.Equal(
            "clow_return_printed_voids_do_not_enter_draw",
            drawVoidsBeforeReturn,
            CountVoids(player, PileType.Draw));

        var obtainPink = new RuntimeFixtureAction(
            player,
            _ => RelicCmd.Obtain<ClassicPinkTransformationCostumeRelic>(player));
        await CombatScenarioContext.EnqueueAndWaitAsync(obtainPink);
        assertions.True(
            "pink_costume_obtained",
            player.GetRelic<ClassicPinkTransformationCostumeRelic>() is not null);

        var drawVoidsBeforePinkForm = CountVoids(player, PileType.Draw);
        var discardVoidsBeforePinkForm = CountVoids(player, PileType.Discard);
        var pinkFormShield = await CombatScenarioContext.AddGeneratedCardToHandAsync<SakuraShield>(combat, player);
        await CombatScenarioContext.PlayCardAsync(pinkFormShield);
        assertions.Equal(
            "pink_costume_exempts_sakura_form_void",
            drawVoidsBeforePinkForm,
            CountVoids(player, PileType.Draw));
        assertions.Equal(
            "pink_costume_does_not_move_form_void_to_discard",
            discardVoidsBeforePinkForm,
            CountVoids(player, PileType.Discard));

        var drawVoidsBeforePinkReturn = CountVoids(player, PileType.Draw);
        var discardVoidsBeforePinkReturn = CountVoids(player, PileType.Discard);
        var pinkReturn = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowReturn>(combat, player);
        await CombatScenarioContext.PlayCardAsync(pinkReturn);
        assertions.Equal(
            "pink_costume_does_not_exempt_clow_return_voids",
            discardVoidsBeforePinkReturn + 2,
            CountVoids(player, PileType.Discard));
        assertions.Equal(
            "pink_return_voids_still_skip_draw",
            drawVoidsBeforePinkReturn,
            CountVoids(player, PileType.Draw));

        RuntimeTestHost.WriteCheckpoint(
            request,
            "extra_effect_play_verified",
            "PlayCardAction executed Extra Effect, Sakura-Form Void, and Clow Return printed Voids.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["fixture"] = new
            {
                seed = request.Seed.ToString("X16"),
                extra_card = typeof(Gale).FullName,
                form_card = typeof(SakuraShield).FullName,
                printed_void_card = typeof(ClowReturn).FullName,
                setup_mutations = new[]
                {
                    "PlayerCmd.GainEnergy(10)",
                    $"Generated {nameof(Gale)} -> hand, played without Extra",
                    $"RuntimeFixtureAction -> SpendAllMagic + GainMagic({FixtureMagicCharge})",
                    $"Generated {nameof(Gale)} -> hand, played with Extra",
                    $"Generated {nameof(SakuraShield)} -> hand",
                    $"Generated {nameof(ClowReturn)} -> hand",
                    $"RelicCmd.Obtain<{nameof(ClassicPinkTransformationCostumeRelic)}>",
                    $"Generated {nameof(SakuraShield)} -> hand after pink costume",
                    $"Generated {nameof(ClowReturn)} -> hand after pink costume"
                }
            },
            ["extra"] = new
            {
                temporary_gale_copies = extraCopies.Length,
                magic_charge = player.Creature.GetPower<ClassicMagicChargePower>()?.Amount ?? 0,
                trigger_count = SakuraActions.ExtraEffectTriggerCountThisTurn(player),
                windy = player.Creature.HasPower<ClassicWindyPower>()
            },
            ["voids"] = new
            {
                draw = CountVoids(player, PileType.Draw),
                discard = CountVoids(player, PileType.Discard)
            }
        };
    }

    private static int CountTemporaryGales(Player player) =>
        player.PlayerCombatState?.Hand.Cards.OfType<Gale>().Count(static card => card.IsTemporary()) ?? 0;

    private static int CountVoids(Player player, PileType pile) =>
        CardPile.Get(pile, player)?.Cards.Count(static card => card is MegaCrit.Sts2.Core.Models.Cards.Void) ?? 0;
}
