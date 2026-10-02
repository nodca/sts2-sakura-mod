using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal static class LabyrinthFieldScenario
{
    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request, RuntimeAssertionCollector assertions)
    {
        var context = await CombatScenarioContext.StartAsync(request);
        var combat = await context.EnterWeakCrawlerCombatAsync();
        var player = context.Player;
        var targets = combat.HittableEnemies.ToList();
        var target = targets[0];
        var room = NCombatRoom.Instance!;
        var originalBackground = room.Background;
        NCombatBackground? Field() => originalBackground.GetParent().GetNodeOrNull<NCombatBackground>("LabyrinthFieldBackground");
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choice =>
        {
            await PlayerCmd.GainEnergy(20, player);
        }));
        var labyrinth = await CombatScenarioContext.AddGeneratedCardToHandAsync<Labyrinth>(combat, player);
        var energyBeforeField = player.PlayerCombatState!.Energy;
        await CombatScenarioContext.PlayCardAsync(labyrinth);
        assertions.Equal("field_pays_own_cost", energyBeforeField - 2, player.PlayerCombatState.Energy);
        assertions.True("field_covers_all_creatures", combat.Creatures.All(c => c.GetPower<LabyrinthLostPower>() is not null));
        var fieldBackground = Field()!;
        assertions.True("field_background_mounted", fieldBackground is not null && fieldBackground.IsInsideTree());
        await CombatScenarioContext.WaitUntilAsync(() => !originalBackground.Visible, "Labyrinth fade-in completion");
        assertions.Equal("field_fades_in_to_full_opacity", 1f, fieldBackground!.Modulate.A);
        assertions.True("original_background_hidden", !originalBackground.Visible);
        var painting = fieldBackground!.GetNode<TextureRect>("Layer_00/LabyrinthBase/LabyrinthPainting");
        assertions.Equal("field_uses_approved_plate", "res://SakuraMod/images/backgrounds/labyrinth/labyrinth_base.png", painting.Texture.ResourcePath);
        assertions.True("field_plate_canvas", painting.Texture.GetSize().IsEqualApprox(new Vector2(2720, 1360)));
        assertions.True("field_does_not_replace_room_background_reference", room.Background == originalBackground);

        Creature other = null!;
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choice =>
        {
            other = await CreatureCmd.Add<FuzzyWurmCrawler>(combat);
        }));
        assertions.True("new_creature_enters_field", other.GetPower<LabyrinthLostPower>() is not null);

        // Actual damage commands prove previews do not consume state, Block does, and hits are tracked per target.
        var lost = target.GetPower<LabyrinthLostPower>()!;
        var preview1 = lost.ModifyDamageMultiplicative(target, 10, ValueProp.Move, player.Creature, null);
        var preview2 = lost.ModifyDamageMultiplicative(target, 10, ValueProp.Move, player.Creature, null);
        assertions.Equal("repeated_preview_keeps_protection", preview1, preview2);
        assertions.Equal("preview_halves_first_hit", 0.5m, preview1);
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choice =>
        {
            var earthSource = combat.CreateCard<ClowMaze>(player);
            var earthHp = target.CurrentHp;
            await DamageCmd.Attack(4).FromCard(earthSource).Targeting(target).WithHitCount(2).Execute(choice);
            assertions.Equal("earth_multi_hit_damage_unaffected", earthHp - 8, target.CurrentHp);
            assertions.Equal("earth_hits_do_not_consume_protection", 0.5m,
                lost.ModifyDamageMultiplicative(target, 10, ValueProp.Move, player.Creature, null));
            await CreatureCmd.Damage(choice, other, 2, ValueProp.Unpowered, player.Creature, null);
            assertions.Equal("unpowered_damage_does_not_spend_protection", 0.5m,
                other.GetPower<LabyrinthLostPower>()!.ModifyDamageMultiplicative(other, 10, ValueProp.Move, player.Creature, null));
            await CreatureCmd.GainBlock(target, 10, ValueProp.Unpowered, null, false);
            var hpBefore = target.CurrentHp;
            await CreatureCmd.Damage(choice, target, 10, ValueProp.Move, player.Creature, null);
            assertions.Equal("fully_blocked_first_hit", hpBefore, target.CurrentHp);
            assertions.Equal("half_before_block", 5m, target.Block);
            await CreatureCmd.Damage(choice, target, 6, ValueProp.Move, player.Creature, null);
            assertions.Equal("blocked_hit_spends_protection", hpBefore - 1, target.CurrentHp);
            var otherHp = other.CurrentHp;
            await CreatureCmd.Damage(choice, other, 10, ValueProp.Move, player.Creature, null);
            await CreatureCmd.Damage(choice, other, 2, ValueProp.Move, player.Creature, null);
            assertions.Equal("targets_have_independent_first_hits", otherHp - 7, other.CurrentHp);
            var ownHp = player.Creature.CurrentHp;
            await CreatureCmd.Damage(choice, player.Creature, 10, ValueProp.Move, target, null);
            assertions.Equal("player_also_protected", ownHp - 5, player.Creature.CurrentHp);
            await CreatureCmd.Damage(choice, other, 2, ValueProp.Unpowered, player.Creature, null);
            assertions.Equal("unpowered_damage_unaffected", otherHp - 9, other.CurrentHp);
        }));

        var firstEarth = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowMaze>(combat, player);
        var secondEarth = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowMaze>(combat, player);
        assertions.Equal("first_earth_discount_preview", 1, firstEarth.EnergyCost.GetWithModifiers(CostModifiers.All));
        var energyBeforeEarth = player.PlayerCombatState.Energy;
        await CombatScenarioContext.PlayCardAsync(firstEarth);
        assertions.Equal("first_earth_discount_paid", energyBeforeEarth - 1, player.PlayerCombatState.Energy);
        assertions.True("played_card_normally_enters_discard", firstEarth.Pile?.Type == PileType.Discard);
        assertions.Equal("second_earth_full_cost", 2, secondEarth.EnergyCost.GetWithModifiers(CostModifiers.All));

        await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
        var nextEarth = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowMaze>(combat, player);
        assertions.Equal("next_turn_discount_reset", 1, nextEarth.EnergyCost.GetWithModifiers(CostModifiers.All));
        assertions.Equal("next_turn_damage_reset", 0.5m,
            lost.ModifyDamageMultiplicative(target, 10, ValueProp.Move, player.Creature, null));
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choice =>
        {
            var hpBefore = target.CurrentHp;
            var attack = DamageCmd.Attack(6).FromCard(combat.CreateCard<ClowSword>(player))
                .Targeting(target).WithHitCount(2);
            await attack.Execute(choice);
            assertions.Equal("multi_hit_only_first_hit_halved", hpBefore - 9, target.CurrentHp);
        }));
        var zeroEarth = await CombatScenarioContext.AddGeneratedCardToHandAsync<Siege>(combat, player);
        await CombatScenarioContext.PlayCardAsync(zeroEarth);
        assertions.Equal("zero_cost_earth_spends_discount", 2, nextEarth.EnergyCost.GetWithModifiers(CostModifiers.All));

        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choice =>
        {
            await PlayerCmd.GainEnergy(20, player);
            await PowerCmd.Apply<GravitationHoldPower>(choice, player.Creature, 1, player.Creature, null);
        }));
        var returned = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowMaze>(combat, player);
        var returnCost = returned.EnergyCost.GetWithModifiers(CostModifiers.All);
        await CombatScenarioContext.PlayCardAsync(returned);
        assertions.True("gravitation_after_labyrinth_returns_to_hand", returned.Pile?.Type == PileType.Hand);
        assertions.Equal("gravitation_return_cost_still_increases", returnCost + 1, returned.EnergyCost.GetWithModifiers(CostModifiers.All));

        var excluded = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowMaze>(combat, player);
        var exhausted = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowMaze>(combat, player);
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, choice =>
        {
            player.Creature.GetPower<GravitationHoldPower>()!.ExcludeSource(excluded);
            exhausted.AddKeyword(CardKeyword.Exhaust);
            return Task.CompletedTask;
        }));
        await CombatScenarioContext.PlayCardAsync(excluded);
        assertions.True("gravitation_excluded_card_normally_discards", excluded.Pile?.Type == PileType.Discard);
        await CombatScenarioContext.PlayCardAsync(exhausted);
        assertions.True("exhaust_still_takes_priority", exhausted.Pile?.Type == PileType.Exhaust);

        // Reapplying Labyrinth must not interfere with Gravitation's own return rules.
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choice =>
        {
            await PowerCmd.Remove(player.Creature.GetPower<LabyrinthPower>()!);
            await PlayerCmd.SetEnergy(0, player);
        }));
        var xWithoutField = await CombatScenarioContext.AddGeneratedCardToHandAsync<Whirlwind>(combat, player);
        await CombatScenarioContext.PlayCardAsync(xWithoutField);
        assertions.True("gravitation_leaves_x_cost_card_in_discard", xWithoutField.Pile?.Type == PileType.Discard);
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choice =>
        {
            await PowerCmd.Apply<LabyrinthPower>(choice, player.Creature, 1, player.Creature, null);
        }));
        var xWithField = await CombatScenarioContext.AddGeneratedCardToHandAsync<Whirlwind>(combat, player);
        await CombatScenarioContext.PlayCardAsync(xWithField);
        assertions.True("x_cost_card_discards_with_both_fields", xWithField.Pile?.Type == PileType.Discard);
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choice =>
        {
            await PlayerCmd.GainEnergy(20, player);
        }));
        var reverseOrder = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowMaze>(combat, player);
        await CombatScenarioContext.PlayCardAsync(reverseOrder);
        assertions.True("gravitation_before_labyrinth_returns_to_hand", reverseOrder.Pile?.Type == PileType.Hand);
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choice =>
        {
            await PowerCmd.Remove(player.Creature.GetPower<GravitationHoldPower>()!);
        }));
        await CombatScenarioContext.PlayCardAsync(reverseOrder);
        assertions.True("cards_normally_discard_after_gravitation_ends", reverseOrder.Pile?.Type == PileType.Discard);
        fieldBackground = Field()!;
        painting = fieldBackground.GetNode<TextureRect>("Layer_00/LabyrinthBase/LabyrinthPainting");
        await CombatScenarioContext.WaitUntilAsync(() => !originalBackground.Visible, "Labyrinth resumed fade-in completion");

        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choice =>
        {
            await PowerCmd.Apply<LabyrinthPower>(choice, other, 1, player.Creature, null);
            assertions.True("multiple_fields_share_one_background", Field() == fieldBackground);
            await PowerCmd.Remove(player.Creature.GetPower<LabyrinthPower>()!);
            assertions.True("remaining_field_keeps_background", Field() == fieldBackground && !originalBackground.Visible);
            await PowerCmd.Remove(other.GetPower<LabyrinthPower>()!);
            assertions.True("last_field_removal_starts_fade_out", originalBackground.Visible && Field() == fieldBackground);
            assertions.True("last_field_removal_cleans_lost", combat.Creatures.All(c => c.GetPower<LabyrinthLostPower>() is null));
            await CombatScenarioContext.WaitUntilAsync(() => fieldBackground.Modulate.A is > 0.1f and < 0.9f,
                "Labyrinth partial fade-out");
            var interruptedAlpha = fieldBackground.Modulate.A;
            await PowerCmd.Apply<LabyrinthPower>(choice, player.Creature, 1, player.Creature, null);
            assertions.True("field_reopens_during_fade_on_same_node", Field() == fieldBackground);
            assertions.True("interrupted_fade_preserves_current_opacity", fieldBackground.Modulate.A > 0
                && fieldBackground.Modulate.A <= interruptedAlpha + 0.05f);
        }));
        await CombatScenarioContext.WaitUntilAsync(() => !originalBackground.Visible, "Labyrinth interrupted fade-in completion");
        assertions.Equal("interrupted_fade_returns_to_full_opacity", 1f, fieldBackground.Modulate.A);
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choice =>
        {
            await PowerCmd.Remove(player.Creature.GetPower<LabyrinthPower>()!);
        }));
        await CombatScenarioContext.WaitUntilAsync(() => Field() is null, "Labyrinth fade-out completion");
        assertions.True("last_field_removal_restores_background", originalBackground.Visible);
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choice =>
        {
            await PowerCmd.Apply<LabyrinthPower>(choice, player.Creature, 1, player.Creature, null);
        }));
        fieldBackground = Field()!;
        painting = fieldBackground.GetNode<TextureRect>("Layer_00/LabyrinthBase/LabyrinthPainting");
        await CombatScenarioContext.WaitUntilAsync(() => fieldBackground.Modulate.A is > 0.1f and < 0.9f,
            "Labyrinth partial fade-in before room exit");
        await context.EnterWeakCrawlerCombatAsync();
        assertions.True("field_background_freed_on_room_exit", !GodotObject.IsInstanceValid(painting));
        assertions.True("next_combat_has_original_background", NCombatRoom.Instance!.Background.Visible
            && NCombatRoom.Instance.Background.GetParent().GetNodeOrNull("LabyrinthFieldBackground") is null);

        var nextCombat = CombatManager.Instance.DebugOnlyGetState()!;
        var nextOriginal = NCombatRoom.Instance.Background;
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choice =>
        {
            await PowerCmd.Apply<LabyrinthPower>(choice, player.Creature, 1, player.Creature, null);
            foreach (var enemy in nextCombat.HittableEnemies.ToList())
                await CreatureCmd.Damage(choice, enemy, 9999, ValueProp.Unpowered, player.Creature, null);
        }));
        await CombatScenarioContext.WaitUntilAsync(() => !CombatManager.Instance.IsInProgress, "Labyrinth combat end");
        assertions.True("combat_end_restores_original_background", nextOriginal.Visible
            && nextOriginal.GetParent().GetNodeOrNull("LabyrinthFieldBackground") is null);

        RuntimeTestHost.WriteCheckpoint(request, "labyrinth_field_verified",
            "Labyrinth mechanics, background mounting/restoration, multiple fields, room exit and combat end verified.");
        return new Dictionary<string, object?> { ["creatures"] = combat.Creatures.Count, ["field_cost"] = 2 };
    }
}
