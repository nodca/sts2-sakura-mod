using System.Reflection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal static class DreamTurnRestorationScenario
{
    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions)
    {
        var context = await CombatScenarioContext.StartAsync(request);
        var combat = await context.EnterWeakCrawlerCombatAsync();
        var player = context.Player;
        var playerCombat = player.PlayerCombatState
            ?? throw new InvalidOperationException("Player combat state is unavailable.");

        var originalClow = playerCombat.AllCards.OfType<ClowSword>().FirstOrDefault()
            ?? throw new InvalidOperationException("Starter combat did not contain ClowSword.");
        if (originalClow.Pile?.Type != PileType.Hand)
        {
            var moveAction = new RuntimeFixtureAction(
                player,
                _ => SakuraActions.MoveExistingCardToHand(null, originalClow));
            await CombatScenarioContext.EnqueueAndWaitAsync(moveAction);
        }

        var deckVersion = originalClow.DeckVersion
            ?? throw new InvalidOperationException("Starter ClowSword had no deck version.");
        var dream = await CombatScenarioContext.AddGeneratedCardToHandAsync<SakuraDream>(combat, player);
        await CombatScenarioContext.PlayCardAsync(dream);

        var transformedSakura = playerCombat.AllCards.OfType<SakuraSword>()
            .First(card => card.DeckVersion is null);
        assertions.Equal("original_clow_removed_during_transformation", false, playerCombat.AllCards.Contains(originalClow));
        assertions.True("dream_power_active_before_turn_end", player.Creature.HasPower<ClassicDreamPower>());
        assertions.True(
            "sakura_form_in_combat_pile",
            transformedSakura.Pile is { Type: PileType.Hand or PileType.Draw or PileType.Discard or PileType.Exhaust });

        await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);

        var restoredClow = playerCombat.AllCards.OfType<ClowSword>()
            .SingleOrDefault(card => ReferenceEquals(card.DeckVersion, deckVersion));
        assertions.True("clow_form_restored_after_turn", restoredClow is not null);
        assertions.True(
            "restored_clow_in_combat_pile",
            restoredClow?.Pile is { Type: PileType.Hand or PileType.Draw or PileType.Discard or PileType.Exhaust });
        assertions.Equal("temporary_sakura_removed_after_turn", false, playerCombat.AllCards.Contains(transformedSakura));
        assertions.Equal("dream_power_removed_after_turn", null, player.Creature.GetPower<ClassicDreamPower>());

        if (restoredClow?.Pile?.Type != PileType.Hand)
        {
            var moveRestoredAction = new RuntimeFixtureAction(
                player,
                _ => SakuraActions.MoveExistingCardToHand(null, restoredClow!));
            await CombatScenarioContext.EnqueueAndWaitAsync(moveRestoredAction);
        }

        var secondDream = await CombatScenarioContext.AddGeneratedCardToHandAsync<SakuraDream>(combat, player);
        await CombatScenarioContext.PlayCardAsync(secondDream);
        var removedTemplate = playerCombat.AllCards.OfType<SakuraSword>()
            .First(card => card.DeckVersion is null);
        var removedBackup = GetRestoredClone(
            player.Creature.GetPower<ClassicDreamPower>()!,
            NetCombatCard.FromModel(removedTemplate).CombatCardIndex);
        await CardPileCmd.RemoveFromCombat(removedTemplate, skipVisuals: true);
        await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);

        assertions.Equal("removed_template_backup_cleaned", false, combat.ContainsCard(removedBackup));
        assertions.Equal("removed_template_backup_owner_cleared", null, removedBackup.Owner);

        RuntimeTestHost.WriteCheckpoint(
            request,
            "dream_turn_restoration_verified",
            "Sakura Dream temporarily converted a live Clow Card, then restored its deck-linked Clow form after the turn boundary.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["fixture"] = new
            {
                seed = request.Seed.ToString("X16"),
                original_card = typeof(ClowSword).FullName,
                transformed_card = typeof(SakuraSword).FullName,
                setup_mutations = new[]
                {
                    "Moved one starter ClowSword combat card to hand when necessary",
                    "Generated SakuraDream into hand"
                }
            },
            ["after"] = new
            {
                restored = restoredClow is not null,
                restored_pile = restoredClow?.Pile?.Type.ToString(),
                temporary_sakura_present = playerCombat.AllCards.Contains(transformedSakura)
            }
        };
    }

    private static CardModel GetRestoredClone(ClassicDreamPower power, uint templateCombatCardIndex)
    {
        var swaps = typeof(ClassicDreamPower)
            .GetProperty("Swaps", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(power) as System.Collections.IEnumerable
            ?? throw new InvalidOperationException("ClassicDreamPower swaps are unavailable.");
        var swap = swaps.Cast<object>().Single(item =>
            (uint)item.GetType().GetField("TemplateCombatCardIndex")!.GetValue(item)! == templateCombatCardIndex);
        return (CardModel)swap.GetType()
            .GetProperty("RestoredClow")!
            .GetValue(swap)!;
    }
}
