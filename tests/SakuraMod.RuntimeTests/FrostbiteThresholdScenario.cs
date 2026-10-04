using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using SakuraMod.SakuraModCode.FourthAct.Water.Powers;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal static class FrostbiteThresholdScenario
{
    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions)
    {
        var context = await CombatScenarioContext.StartAsync(request);
        var combat = await context.EnterWeakSlimesCombatAsync();
        var player = context.Player;
        var enemies = combat.HittableEnemies.Take(2).ToArray();
        if (enemies.Length != 2) throw new InvalidOperationException("Threshold fixture requires two enemies.");
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choiceContext =>
        {
            foreach (var enemy in combat.Enemies)
            {
                var idle = new MoveState("FROSTBITE_TEST_IDLE", _ => Task.CompletedTask, new BuffIntent())
                {
                    FollowUpStateId = "FROSTBITE_TEST_IDLE"
                };
                enemy.Monster!.MoveStateMachine!.States[idle.Id] = idle;
                enemy.Monster.SetMoveImmediate(idle, forceTransition: true);
            }
            await PowerCmd.Apply<SakuraFrostbitePower>(choiceContext, enemies[0], 12, player.Creature, null);
            await PowerCmd.Apply<SakuraFrostbitePower>(choiceContext, enemies[1], 4, player.Creature, null);
        }));
        assertions.Equal("bulk_12_freeze", 2, enemies[0].GetPower<ClassicFreezePower>()?.Amount ?? 0);
        assertions.Equal("bulk_12_remainder", 1, enemies[0].GetPower<SakuraFrostbitePower>()?.Amount ?? 0);
        assertions.Equal("bulk_12_conversion_count", 2, enemies[0].GetPower<SakuraFreezeResistancePower>()?.Amount ?? 0);
        var frostbite = enemies[0].GetPower<SakuraFrostbitePower>()!;
        assertions.Equal("bulk_12_next_threshold", 7, frostbite.DynamicVars["FreezeThreshold"].IntValue);
        var hoverText = frostbite.HoverTips.OfType<HoverTip>().First().Description;
        assertions.True("hover_formats_live_threshold", hoverText.Contains("7", StringComparison.Ordinal) && !hoverText.Contains("{FreezeThreshold}", StringComparison.Ordinal));
        assertions.True("conversion_counter_hidden", !enemies[0].GetPower<SakuraFreezeResistancePower>()!.IsVisible);
        assertions.Equal("second_enemy_no_freeze_at_4", 0, enemies[1].GetPower<ClassicFreezePower>()?.Amount ?? 0);
        assertions.Equal("second_enemy_initial_threshold", 5, enemies[1].GetPower<SakuraFrostbitePower>()!.DynamicVars["FreezeThreshold"].IntValue);
        await VerifyFrostAmountAsync(enemies[1], 4f / 5f, "initial_enemy", assertions);
        assertions.Equal("frostbite_alone_has_no_shell", 0f, IceFor(enemies[1]).GetShaderParameter("growth").AsSingle());

        await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
        await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
        assertions.Equal("freeze_expires", null, enemies[0].GetPower<ClassicFreezePower>());
        assertions.Equal("frostbite_expires", null, enemies[0].GetPower<SakuraFrostbitePower>());
        assertions.Equal("threshold_count_survives_expiry", 2, enemies[0].GetPower<SakuraFreezeResistancePower>()!.Amount);
        await VerifyFrostAmountAsync(enemies[1], 2f / 5f, "decayed_enemy", assertions);
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choiceContext =>
            await PowerCmd.Apply<SakuraFrostbitePower>(choiceContext, enemies[0], 6, player.Creature, null)));
        assertions.Equal("reapplied_6_not_enough", null, enemies[0].GetPower<ClassicFreezePower>());
        assertions.Equal("reapplied_threshold_still_7", 7, enemies[0].GetPower<SakuraFrostbitePower>()!.DynamicVars["FreezeThreshold"].IntValue);
        await VerifyFrostAmountAsync(enemies[0], 6f / 7f, "increased_threshold", assertions);
        var frostRoot = RootFor(enemies[0]);
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choiceContext =>
        {
            await PowerCmd.Apply<SakuraFrostbitePower>(choiceContext, enemies[0], 1, player.Creature, null);
            await PowerCmd.Apply<SakuraFrostbitePower>(choiceContext, enemies[1], 3, player.Creature, null);
        }));
        assertions.Equal("stacked_6_plus_1_converts", 1, enemies[0].GetPower<ClassicFreezePower>()!.Amount);
        assertions.Equal("stacked_conversion_count_3", 3, enemies[0].GetPower<SakuraFreezeResistancePower>()!.Amount);
        assertions.Equal("exact_conversion_removes_frostbite", null, enemies[0].GetPower<SakuraFrostbitePower>());
        assertions.Equal("second_enemy_converts_at_5", 1, enemies[1].GetPower<SakuraFreezeResistancePower>()!.Amount);
        assertions.True("frostbite_to_freeze_reuses_visual", ReferenceEquals(frostRoot, RootFor(enemies[0])));
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choiceContext =>
        {
            await PowerCmd.Remove(enemies[0].GetPower<ClassicFreezePower>()!);
            await PowerCmd.Apply<SakuraFrostbitePower>(choiceContext, enemies[0], 8, player.Creature, null);
            await PowerCmd.Apply<ClassicFreezePower>(choiceContext, enemies[1], 2, player.Creature, null);
        }));
        assertions.Equal("threshold_survives_explicit_freeze_removal", 4, enemies[0].GetPower<SakuraFreezeResistancePower>()!.Amount);
        assertions.Equal("direct_freeze_does_not_raise_threshold", 1, enemies[1].GetPower<SakuraFreezeResistancePower>()!.Amount);

        var oldShells = await VerifyFreezeShellAsync(context, enemies, assertions);

        combat = await context.EnterWeakSlimesCombatAsync();
        assertions.True("freeze_shell_previous_combat_cleared", oldShells.All(root => !GodotObject.IsInstanceValid(root)));
        enemies = combat.HittableEnemies.Take(2).ToArray();
        assertions.Equal("new_combat_no_resistance", null, enemies[0].GetPower<SakuraFreezeResistancePower>());
        assertions.Equal("new_combat_player_no_resistance", null, player.Creature.GetPower<SakuraFreezeResistancePower>());
        var blockBefore = player.Creature.Block;
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choiceContext =>
        {
            await PowerCmd.Apply<SakuraFrostbitePower>(choiceContext, enemies[0], 12, player.Creature, null);
            foreach (var amount in new[] { 5, 6, 1 })
                await PowerCmd.Apply<SakuraFrostbitePower>(choiceContext, enemies[1], amount, player.Creature, null);
            await PowerCmd.Apply<SakuraFrostbitePower>(choiceContext, player.Creature, 12, player.Creature, null);
        }));
        foreach (var enemy in enemies)
        {
            assertions.Equal($"new_combat_{enemy.CombatId}_freeze", 2, enemy.GetPower<ClassicFreezePower>()!.Amount);
            assertions.Equal($"new_combat_{enemy.CombatId}_remainder", 1, enemy.GetPower<SakuraFrostbitePower>()!.Amount);
            assertions.Equal($"new_combat_{enemy.CombatId}_threshold", 7, enemy.GetPower<SakuraFrostbitePower>()!.DynamicVars["FreezeThreshold"].IntValue);
        }
        assertions.Equal("player_bulk_12_freeze", 2, player.Creature.GetPower<WaterFrozenPower>()!.Amount);
        assertions.Equal("player_still_flat_5_block", 5m, player.Creature.Block - blockBefore);
        assertions.Equal("player_bulk_12_remainder", 1, player.Creature.GetPower<SakuraFrostbitePower>()?.Amount ?? 0);
        assertions.Equal("player_bulk_12_conversion_count", 2, player.Creature.GetPower<SakuraFreezeResistancePower>()?.Amount ?? 0);
        var playerFrostbite = player.Creature.GetPower<SakuraFrostbitePower>()!;
        assertions.Equal("player_bulk_12_next_threshold", 7, playerFrostbite.DynamicVars["FreezeThreshold"].IntValue);
        var playerHover = playerFrostbite.HoverTips.OfType<HoverTip>().First().Description;
        assertions.True("player_hover_formats_live_threshold", playerHover.Contains("7", StringComparison.Ordinal)
            && !playerHover.Contains("{FreezeThreshold}", StringComparison.Ordinal));
        assertions.True("player_conversion_counter_hidden", !player.Creature.GetPower<SakuraFreezeResistancePower>()!.IsVisible);
        await VerifyFrostAmountAsync(player.Creature, 1f / 7f, "player_remainder", assertions);
        await CombatScenarioContext.WaitUntilAsync(() => IceFor(player.Creature).GetShaderParameter("growth").AsSingle() >= 1f,
            "Player Freeze shell formation");
        assertions.Equal("player_freeze_shell_visible", 1f, IceFor(player.Creature).GetShaderParameter("growth").AsSingle());

        await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
        await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
        assertions.Equal("player_freeze_expires", null, player.Creature.GetPower<WaterFrozenPower>());
        assertions.Equal("player_frostbite_expires", null, player.Creature.GetPower<SakuraFrostbitePower>());
        assertions.Equal("player_threshold_survives_expiry", 2, player.Creature.GetPower<SakuraFreezeResistancePower>()!.Amount);
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choiceContext =>
            await PowerCmd.Apply<SakuraFrostbitePower>(choiceContext, player.Creature, 6, enemies[0], null)));
        assertions.Equal("player_reapplied_6_not_enough", null, player.Creature.GetPower<WaterFrozenPower>());
        assertions.Equal("player_reapplied_threshold_still_7", 7, player.Creature.GetPower<SakuraFrostbitePower>()!.DynamicVars["FreezeThreshold"].IntValue);
        await VerifyFrostAmountAsync(player.Creature, 6f / 7f, "player_frostbite", assertions);
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choiceContext =>
        {
            await PowerCmd.Apply<SakuraFrostbitePower>(choiceContext, player.Creature, 1, enemies[0], null);
            await PowerCmd.Remove(player.Creature.GetPower<WaterFrozenPower>()!);
            await PowerCmd.Apply<WaterFrozenPower>(choiceContext, player.Creature, 2, enemies[0], null);
        }));
        assertions.Equal("player_exact_conversion_removes_frostbite", null, player.Creature.GetPower<SakuraFrostbitePower>());
        assertions.Equal("player_threshold_survives_removal_and_direct_freeze", 3, player.Creature.GetPower<SakuraFreezeResistancePower>()!.Amount);
        assertions.Equal("player_conversion_does_not_raise_enemy_threshold", 2, enemies[0].GetPower<SakuraFreezeResistancePower>()!.Amount);

        combat = await context.EnterWeakSlimesCombatAsync();
        assertions.Equal("next_combat_player_resistance_reset", null, player.Creature.GetPower<SakuraFreezeResistancePower>());
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choiceContext =>
            await PowerCmd.Apply<SakuraFrostbitePower>(choiceContext, player.Creature, 5, combat.Enemies.First(), null)));
        assertions.Equal("next_combat_player_converts_at_5", 1, player.Creature.GetPower<WaterFrozenPower>()!.Amount);
        assertions.Equal("next_combat_player_conversion_count", 1, player.Creature.GetPower<SakuraFreezeResistancePower>()!.Amount);
        return new Dictionary<string, object?> { ["thresholds"] = new[] { 5, 6, 7, 8 } };
    }

    private static Node2D? RootFor(Creature creature) => NCombatRoom.Instance!.GetCreatureNode(creature)?.GetNodeOrNull<Node2D>(
        $"{FreezeShellVisual.RootName}_{creature.CombatId}");

    private static ShaderMaterial IceFor(Creature creature) =>
        (ShaderMaterial)RootFor(creature)!.GetNode<ColorRect>("Ice").Material;

    private static async Task VerifyFrostAmountAsync(Creature creature, float expected, string name,
        RuntimeAssertionCollector assertions)
    {
        await CombatScenarioContext.WaitUntilAsync(() => RootFor(creature) is not null
            && Math.Abs(IceFor(creature).GetShaderParameter("frost").AsSingle() - expected) < 0.001f,
            $"{name} Frostbite ratio");
        assertions.True($"{name}_frostbite_uses_live_threshold",
            Math.Abs(IceFor(creature).GetShaderParameter("frost").AsSingle() - expected) < 0.001f);
    }

    private static async Task<Node2D[]> VerifyFreezeShellAsync(
        CombatScenarioContext context, Creature[] enemies, RuntimeAssertionCollector assertions)
    {
        var room = NCombatRoom.Instance!;
        Node2D? Shell(Creature enemy) => RootFor(enemy);
        ShaderMaterial Ice(Node2D root) => (ShaderMaterial)root.GetNode<ColorRect>("Ice").Material;
        var first = Shell(enemies[0]);
        var second = Shell(enemies[1]);
        assertions.True("freeze_shell_each_enemy_mounted", first is not null && second is not null);
        if (first is null || second is null)
            throw new InvalidOperationException("Freeze Power did not mount its shell.");
        assertions.True("freeze_shell_independent_materials", !ReferenceEquals(Ice(first), Ice(second)));
        assertions.Equal("freeze_shell_ignores_mouse", Control.MouseFilterEnum.Ignore,
            first.GetNode<ColorRect>("Ice").MouseFilter);
        var firstNode = room.GetCreatureNode(enemies[0])!;
        assertions.Equal("freeze_shell_drawn_right_after_body", firstNode.Visuals.GetIndex() + 1, first.GetIndex());
        assertions.True("freeze_shell_below_creature_ui",
            first.GetIndex() < firstNode.GetNode<Control>("%HealthBar").GetIndex()
            && first.GetIndex() < firstNode.IntentContainer.GetIndex());
        await CombatScenarioContext.WaitUntilAsync(() => Ice(first).GetShaderParameter("growth").AsSingle() >= 1f,
            "Freeze shell formation");
        Color Tint(Creature enemy) => room.GetCreatureNode(enemy)!.Visuals.Modulate;
        assertions.True("freeze_tints_creature_rgb", Tint(enemies[0]).B > 1.25f && Tint(enemies[0]).R < 0.85f);
        assertions.Equal("freeze_tint_keeps_native_alpha", 1f, Tint(enemies[0]).A);
        var cachedBefore = PreloadManager.Cache.MissedCacheAssetCount;
        var stacks = enemies[0].GetPowerAmount<ClassicFreezePower>();
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(context.Player, async choiceContext =>
        {
            await PowerCmd.Apply<ClassicFreezePower>(choiceContext, enemies[0], 1, context.Player.Creature, null);
            await PowerCmd.Decrement(enemies[0].GetPower<ClassicFreezePower>()!);
            await CreatureCmd.Damage(choiceContext, enemies[0], 1, ValueProp.Unpowered, context.Player.Creature);
            assertions.True("freeze_shell_damage_flashes_cracks", Ice(first).GetShaderParameter("hit").AsSingle() > 0f);
        }));
        assertions.True("freeze_shell_stacking_reuses_root", ReferenceEquals(first, Shell(enemies[0])));
        assertions.Equal("freeze_shell_hit_preserves_power", stacks, enemies[0].GetPowerAmount<ClassicFreezePower>());
        assertions.Equal("freeze_shell_hit_does_not_release", 0f, Ice(first).GetShaderParameter("release").AsSingle());

        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(context.Player,
            async _ => await PowerCmd.Remove(enemies[0].GetPower<ClassicFreezePower>()!)));
        await CombatScenarioContext.WaitUntilAsync(() => Ice(first).GetShaderParameter("release").AsSingle() > 0f,
            "Freeze shell release begins");
        assertions.Equal("freeze_shell_other_enemy_still_frozen", 0f, Ice(second).GetShaderParameter("release").AsSingle());
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(context.Player,
            async choiceContext => await PowerCmd.Apply<ClassicFreezePower>(choiceContext, enemies[0], 1, context.Player.Creature, null)));
        assertions.True("freeze_shell_reapply_interrupts_release", ReferenceEquals(first, Shell(enemies[0])));
        assertions.Equal("freeze_shell_reapply_resets_release", 0f, Ice(first).GetShaderParameter("release").AsSingle());
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(context.Player,
            async choiceContext => await PowerCmd.Apply<SakuraFrostbitePower>(choiceContext, enemies[0], 1, context.Player.Creature, null)));
        await VerifyFrostAmountAsync(enemies[0], 1f / 9f, "frozen_remainder", assertions);
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(context.Player,
            async _ => await PowerCmd.Remove(enemies[0].GetPower<ClassicFreezePower>()!)));
        await CombatScenarioContext.WaitUntilAsync(() => Ice(first).GetShaderParameter("growth").AsSingle() == 0f,
            "Freeze expires over remaining Frostbite");
        assertions.True("freeze_release_retains_frostbite_root", ReferenceEquals(first, Shell(enemies[0])));
        assertions.True("freeze_release_retains_frostbite_visual", Ice(first).GetShaderParameter("frost").AsSingle() > 0f);
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(context.Player,
            async _ => await PowerCmd.Remove(enemies[0].GetPower<SakuraFrostbitePower>()!)));
        await CombatScenarioContext.WaitUntilAsync(() => !GodotObject.IsInstanceValid(first), "Freeze shell removed");
        assertions.True("freeze_shell_explicit_removal_clears", Shell(enemies[0]) is null);
        assertions.True("freeze_tint_restored_on_removal", Tint(enemies[0]).IsEqualApprox(Colors.White));

        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(context.Player,
            async choiceContext => await PowerCmd.Apply<ClassicFreezePower>(choiceContext, enemies[0], 1, context.Player.Creature, null)));
        assertions.True("freeze_shell_can_mount_again", Shell(enemies[0]) is not null);
        assertions.Equal("freeze_shell_playback_cache_misses", cachedBefore, PreloadManager.Cache.MissedCacheAssetCount);
        var restored = Shell(enemies[0])!;
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(context.Player,
            async _ => await CreatureCmd.Kill(enemies[0], force: true)));
        await CombatScenarioContext.WaitUntilAsync(() => !GodotObject.IsInstanceValid(restored), "Freeze shell death cleanup");
        assertions.True("freeze_shell_death_clears_only_its_owner", Shell(enemies[0]) is null && Shell(enemies[1]) == second);
        return [restored, second];
    }
}
