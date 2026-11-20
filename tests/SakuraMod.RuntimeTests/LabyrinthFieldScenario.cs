using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.TestProtocol;
using STS2RitsuLib.Combat.HandSize;

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
        var preview1 = lost.ModifyDamageMultiplicative(target, 10, ValueProp.Move, player.Creature, null, null);
        var preview2 = lost.ModifyDamageMultiplicative(target, 10, ValueProp.Move, player.Creature, null, null);
        assertions.Equal("repeated_preview_keeps_protection", preview1, preview2);
        assertions.Equal("preview_halves_first_hit", 0.5m, preview1);
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choice =>
        {
            var earthSource = combat.CreateCard<ClowMaze>(player);
            assertions.Equal("earth_hits_are_also_halved", 0.5m,
                lost.ModifyDamageMultiplicative(target, 10, ValueProp.Move, player.Creature, earthSource, null));
            await CreatureCmd.Damage(choice, other, 2, ValueProp.Unpowered, player.Creature, null, null);
            assertions.Equal("unpowered_damage_does_not_spend_protection", 0.5m,
                other.GetPower<LabyrinthLostPower>()!.ModifyDamageMultiplicative(other, 10, ValueProp.Move, player.Creature, null, null));
            await CreatureCmd.GainBlock(target, 10, ValueProp.Unpowered, null, false);
            var hpBefore = target.CurrentHp;
            await CreatureCmd.Damage(choice, target, 10, ValueProp.Move, player.Creature, null, null);
            assertions.Equal("fully_blocked_first_hit", hpBefore, target.CurrentHp);
            assertions.Equal("half_before_block", 5m, target.Block);
            await CreatureCmd.Damage(choice, target, 6, ValueProp.Move, player.Creature, null, null);
            assertions.Equal("blocked_hit_spends_protection", hpBefore - 1, target.CurrentHp);
            var otherHp = other.CurrentHp;
            await CreatureCmd.Damage(choice, other, 10, ValueProp.Move, player.Creature, null, null);
            await CreatureCmd.Damage(choice, other, 2, ValueProp.Move, player.Creature, null, null);
            assertions.Equal("targets_have_independent_first_hits", otherHp - 7, other.CurrentHp);
            var ownHp = player.Creature.CurrentHp;
            await CreatureCmd.Damage(choice, player.Creature, 10, ValueProp.Move, target, null, null);
            assertions.Equal("player_also_protected", ownHp - 5, player.Creature.CurrentHp);
            await CreatureCmd.Damage(choice, other, 2, ValueProp.Unpowered, player.Creature, null, null);
            assertions.Equal("unpowered_damage_unaffected", otherHp - 9, other.CurrentHp);
        }));

        var firstEarth = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowMaze>(combat, player);
        var secondEarth = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowMaze>(combat, player);
        assertions.Equal("first_earth_free_preview", 0, firstEarth.EnergyCost.GetWithModifiers(CostModifiers.All));
        var energyBeforeEarth = player.PlayerCombatState.Energy;
        var discard = CardPile.Get(PileType.Discard, player)
            ?? throw new InvalidOperationException("Combat discard pile is missing.");
        var dazedBeforeEarth = discard.Cards.OfType<Dazed>().Count();
        await CombatScenarioContext.PlayCardAsync(firstEarth);
        assertions.Equal("maze_adds_one_dazed_to_discard", dazedBeforeEarth + 1,
            discard.Cards.OfType<Dazed>().Count());
        assertions.Equal("first_earth_free_paid", energyBeforeEarth, player.PlayerCombatState.Energy);
        assertions.True("played_card_normally_enters_discard", firstEarth.Pile?.Type == PileType.Discard);
        assertions.Equal("second_earth_full_cost", 2, secondEarth.EnergyCost.GetWithModifiers(CostModifiers.All));

        await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
        var nextEarth = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowMaze>(combat, player);
        assertions.Equal("next_turn_free_reset", 0, nextEarth.EnergyCost.GetWithModifiers(CostModifiers.All));
        assertions.Equal("next_turn_damage_reset", 0.5m,
            lost.ModifyDamageMultiplicative(target, 10, ValueProp.Move, player.Creature, null, null));
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choice =>
        {
            var hpBefore = target.CurrentHp;
            var attack = DamageCmd.Attack(6).FromCard(combat.CreateCard<ClowSword>(player), null)
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

        var exhausted = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowMaze>(combat, player);
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, choice =>
        {
            exhausted.AddKeyword(CardKeyword.Exhaust);
            return Task.CompletedTask;
        }));
        await CombatScenarioContext.PlayCardAsync(exhausted);
        assertions.True("exhaust_still_takes_priority", exhausted.Pile?.Type == PileType.Exhaust);

        // Isolate Gravitation from Labyrinth's one-shot cost modifier.
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choice =>
        {
            await PowerCmd.Remove(player.Creature.GetPower<LabyrinthPower>()!);
            foreach (var enemy in combat.HittableEnemies)
                await CreatureCmd.GainBlock(enemy, 100, ValueProp.Unpowered, null, false);
        }));
        // A Gravitation played into an active field stacks its damage and returns like any card;
        // every return deals the same flat damage.
        var enemies = combat.HittableEnemies.ToArray();
        int[] Durability() => enemies.Select(enemy => enemy.CurrentHp + enemy.Block).ToArray();
        bool EachLost(int[] before, int amount) =>
            enemies.Select((enemy, i) => before[i] - (enemy.CurrentHp + enemy.Block)).All(lost => lost == amount);
        var beforeGravitation = Durability();
        var gravitation = await CombatScenarioContext.AddGeneratedCardToHandAsync<Gravitation>(combat, player);
        await CombatScenarioContext.PlayCardAsync(gravitation);
        assertions.True("gravitation_in_field_returns_itself", gravitation.Pile?.Type == PileType.Hand);
        assertions.Equal(
            "gravitation_damage_stacks",
            3,
            player.Creature.GetPower<GravitationHoldPower>()!.DynamicVars[GravitationHoldPower.ReturnDamageVar].IntValue);
        assertions.True("gravitation_return_hits_all_enemies", EachLost(beforeGravitation, 3));
        var beforeLoop = Durability();
        var looped = await CombatScenarioContext.AddGeneratedCardToHandAsync<DefendIronclad>(combat, player);
        await CombatScenarioContext.PlayCardAsync(looped);
        await CombatScenarioContext.PlayCardAsync(looped);
        assertions.True("looped_card_returns", looped.Pile?.Type == PileType.Hand);
        assertions.True("repeat_returns_deal_flat_damage", EachLost(beforeLoop, 3 + 3));

        // Discard follows native Sly: a card discarded from the Hand returns and counts as a return;
        // a card discarded straight from the Draw Pile does not.
        var discarded = await CombatScenarioContext.AddGeneratedCardToHandAsync<DefendIronclad>(combat, player);
        var discardedCost = discarded.EnergyCost.GetWithModifiers(CostModifiers.All);
        var durabilityBeforeDiscard = Durability();
        CardModel? fromDrawPile = null;
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choice =>
        {
            // Every power receives the pile hooks, including instances owned by
            // another creature. Give the other instance different damage to catch cross-talk.
            var otherField = await PowerCmd.Apply<GravitationHoldPower>(choice, other, 1, other, null)
                ?? throw new InvalidOperationException("Second Gravitation power was not applied.");
            otherField.AddReturnDamage(11);
            await CardCmd.Discard(choice, discarded);
            fromDrawPile = combat.CreateCard<DefendIronclad>(player);
            await CardPileCmd.AddGeneratedCardToCombat(fromDrawPile, PileType.Draw, player, CardPilePosition.Top);
            await CardCmd.Discard(choice, fromDrawPile);
            await PowerCmd.Remove(otherField);
        }));
        assertions.True("discarded_hand_card_returns_with_two_gravitation_powers", discarded.Pile?.Type == PileType.Hand);
        assertions.True("discard_return_uses_owners_damage", EachLost(durabilityBeforeDiscard, 3));
        assertions.Equal("removing_other_gravitation_keeps_return_cost",
            discardedCost + 1, discarded.EnergyCost.GetWithModifiers(CostModifiers.All));
        assertions.True("draw_pile_discard_stays_in_discard", fromDrawPile?.Pile?.Type == PileType.Discard);

        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choice =>
        {
            await CardCmd.Exhaust(choice, looped, skipVisuals: true);
            await CardCmd.Exhaust(choice, discarded, skipVisuals: true);
            await CardCmd.Exhaust(choice, gravitation, skipVisuals: true);
        }));
        var finesse = await CombatScenarioContext.AddGeneratedCardToHandAsync<Finesse>(combat, player);
        var finesseCost = finesse.EnergyCost.GetWithModifiers(CostModifiers.All);
        var fillers = new List<CardModel>();
        var drawFiller = combat.CreateCard<DefendIronclad>(player);
        var hand = player.PlayerCombatState.Hand;
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choice =>
        {
            await CardPileCmd.AddGeneratedCardToCombat(drawFiller, PileType.Draw, player, CardPilePosition.Top);
            var vacancies = MaxHandSizeCalculator.Calculate(player) - hand.Cards.Count;
            for (var i = 0; i < vacancies; i++)
            {
                var filler = combat.CreateCard<DefendIronclad>(player);
                await CardPileCmd.AddGeneratedCardToCombat(filler, PileType.Hand, player);
                fillers.Add(filler);
            }
        }));
        assertions.True("finesse_starts_in_full_hand", finesse.Pile?.Type == PileType.Hand
            && hand.Cards.Count == MaxHandSizeCalculator.Calculate(player));
        var durabilityBeforeOverflow = Durability();
        await CombatScenarioContext.PlayCardAsync(finesse);
        assertions.True("finesse_draw_fills_hand", drawFiller.Pile?.Type == PileType.Hand);
        assertions.True("full_hand_sends_played_card_to_discard", finesse.Pile?.Type == PileType.Discard);
        assertions.True("failed_return_deals_no_damage", EachLost(durabilityBeforeOverflow, 0));
        assertions.Equal("failed_return_does_not_raise_cost", finesseCost,
            finesse.EnergyCost.GetWithModifiers(CostModifiers.All));

        // Clear the hand and disable return damage for the remaining cost/interaction checks.
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choice =>
        {
            player.Creature.GetPower<GravitationHoldPower>()!.DynamicVars[GravitationHoldPower.ReturnDamageVar].BaseValue = 0;
            foreach (var filler in fillers.Append(drawFiller))
                await CardCmd.Exhaust(choice, filler, skipVisuals: true);
            await CreatureCmd.GainBlock(other, 100, ValueProp.Unpowered, null, false);
        }));
        var struggle = await CombatScenarioContext.AddGeneratedCardToHandAsync<Struggle>(combat, player);
        var strike = await CombatScenarioContext.AddGeneratedCardToHandAsync<StrikeIronclad>(combat, player);
        for (var i = 0; i < 3; i++)
            await CombatScenarioContext.PlayCardAsync(strike, other);

        assertions.Equal("struggle_free_after_three_other_attacks", 0, struggle.EnergyCost.GetAmountToSpend());
        await CombatScenarioContext.PlayCardAsync(struggle, other);
        assertions.True("gravitation_returns_struggle", struggle.Pile?.Type == PileType.Hand);
        assertions.Equal("struggle_first_return_costs_one", 1, struggle.EnergyCost.GetAmountToSpend());

        // The follow-up is a separate hit, but Vigor covers the whole attack.
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choice =>
        {
            await PowerCmd.Apply<ClassicMagicChargePower>(choice, player.Creature, SakuraMagicCharge.ExtraEffectCost, player.Creature, null);
            await PowerCmd.Apply<StrengthPower>(choice, player.Creature, 1, player.Creature, null);
            await PowerCmd.Apply<VigorPower>(choice, player.Creature, 2, player.Creature, null);
        }));
        var history = CombatManager.Instance.History;
        var historyBeforeExtra = history.Entries.Count();
        await CombatScenarioContext.PlayCardAsync(struggle, other);
        var extraHits = history.Entries.Skip(historyBeforeExtra).OfType<DamageReceivedEntry>()
            .Where(entry => entry.CardSource == struggle && entry.Receiver == other)
            .Select(entry => entry.Result.TotalDamage);
        assertions.Equal("struggle_extra_hits_with_doubled_strength_and_vigor", "15,11", string.Join(',', extraHits));
        assertions.True("struggle_extra_consumes_vigor", player.Creature.GetPower<VigorPower>() is null);
        assertions.Equal("struggle_second_return_costs_two", 2, struggle.EnergyCost.GetAmountToSpend());

        // Reapplying Labyrinth must not interfere with Gravitation's own return rules.
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choice =>
        {
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
                await CreatureCmd.Damage(choice, enemy, 9999, ValueProp.Unpowered, player.Creature, null, null);
        }));
        await CombatScenarioContext.WaitUntilAsync(() => !CombatManager.Instance.IsInProgress, "Labyrinth combat end");
        assertions.True("combat_end_restores_original_background", nextOriginal.Visible
            && nextOriginal.GetParent().GetNodeOrNull("LabyrinthFieldBackground") is null);

        RuntimeTestHost.WriteCheckpoint(request, "labyrinth_field_verified",
            "Labyrinth mechanics, background mounting/restoration, multiple fields, room exit and combat end verified.");
        return new Dictionary<string, object?> { ["creatures"] = combat.Creatures.Count, ["field_cost"] = 2 };
    }
}
