using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

/// <summary>
/// Plays a real Sakura Time and checks the held enemy: paused Spine, the hourglass
/// under the creature UI, the amber tint (alone and with Freeze), hits, the turn-end
/// release, Clow Time's Stasis holding the last stack, and death while held.
/// </summary>
internal static class SakuraTimeFreezeScenario
{
    private const int HeldFrames = 12;

    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions)
    {
        var context = await CombatScenarioContext.StartAsync(request);
        var combat = await context.EnterWeakSlimesCombatAsync();
        var player = context.Player;
        var room = NCombatRoom.Instance!;
        var enemies = combat.HittableEnemies.Take(2).ToArray();
        if (enemies.Length != 2) throw new InvalidOperationException("Sakura Time fixture requires two enemies.");
        assertions.True("fixture_enemies_have_spine", enemies.All(e => room.GetCreatureNode(e)!.HasSpineAnimation));

        // --- Play -------------------------------------------------------------
        var voidsBefore = player.PlayerCombatState!.DiscardPile.Cards.OfType<MegaCrit.Sts2.Core.Models.Cards.Void>().Count();
        var card = await CombatScenarioContext.AddGeneratedCardToHandAsync<SakuraTime>(combat, player);
        await CombatScenarioContext.PlayCardAsync(card);
        // The star fades for 0.3 s after the last Time Stop, so it is still here.
        assertions.True("play_ran_the_sweep",
            room.CombatVfxContainer.GetNodeOrNull<Node2D>("SakuraTimeSweepVfx") is not null);
        foreach (var enemy in enemies)
            assertions.Equal($"play_applies_time_stop_{enemy.CombatId}", 1, enemy.GetPowerAmount<ClassicTimePower>());
        assertions.Equal("play_adds_one_void_to_discard", voidsBefore + 1,
            player.PlayerCombatState.DiscardPile.Cards.OfType<MegaCrit.Sts2.Core.Models.Cards.Void>().Count());
        // Scoped to our own resources: the first stun in this fixture also loads
        // native's stun intent icon, which is not ours to preload.
        assertions.True("play_reads_only_cached_vfx_assets",
            SakuraTimeSweepVfx.AssetPaths.Concat(TimeFreezeVisual.AssetPaths).All(PreloadManager.Cache.ContainsKey));

        var held = enemies[1];
        var heldNode = room.GetCreatureNode(held)!;
        var root = RootFor(held);
        assertions.True("hold_mounted_for_each_enemy", RootFor(enemies[0]) is not null && root is not null);
        if (root is null) throw new InvalidOperationException("Time Stop did not mount its hold.");
        assertions.True("hold_materials_independent", !ReferenceEquals(Material(root), Material(RootFor(enemies[0])!)));
        assertions.Equal("hold_ignores_mouse", Control.MouseFilterEnum.Ignore, root.GetNode<ColorRect>("Hourglass").MouseFilter);
        assertions.Equal("hold_drawn_right_after_body", heldNode.Visuals.GetIndex() + 1, root.GetIndex());
        assertions.True("hold_below_creature_ui",
            root.GetIndex() < heldNode.GetNode<Control>("%HealthBar").GetIndex()
            && root.GetIndex() < heldNode.IntentContainer.GetIndex());
        await CombatScenarioContext.WaitUntilAsync(() => Material(root).GetShaderParameter("intro").AsSingle() >= 1f, "hold intro");
        assertions.Equal("hold_sand_full_at_one_stack", 1f, Material(root).GetShaderParameter("sand").AsSingle());

        assertions.True("held_spine_does_not_advance", await TrackStillAsync(heldNode));
        assertions.Equal("held_spine_state_scale_zero", 0f, StateTimeScale(heldNode));
        var amber = heldNode.Visuals.Modulate;
        assertions.True("held_tint_is_amber", amber.R > 1.05f && amber.B < 0.75f);
        assertions.Equal("held_tint_keeps_native_alpha", 1f, amber.A);

        // --- Freeze on top: one stable product, not a compounding colour --------
        var both = enemies[0];
        var bothNode = room.GetCreatureNode(both)!;
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choiceContext =>
            await PowerCmd.Apply<ClassicFreezePower>(choiceContext, both, 1, player.Creature, null)));
        await CombatScenarioContext.WaitUntilAsync(() => FreezeIce(both)?.GetShaderParameter("growth").AsSingle() >= 1f,
            "Freeze shell over the hold");
        var combined = bothNode.Visuals.Modulate;
        await Frames(HeldFrames);
        var later = bothNode.Visuals.Modulate;
        var product = new Color(1.10f * 0.80f, 0.92f * 1.02f, 0.68f * 1.30f);
        assertions.True("freeze_and_time_tint_is_the_product", RgbClose(combined, product));
        assertions.True("freeze_and_time_tint_is_stable", RgbClose(combined, later));

        // --- A hit keeps the pose and cracks the glass ----------------------------
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choiceContext =>
            await CreatureCmd.Damage(choiceContext, held, 1, ValueProp.Unpowered, player.Creature)));
        assertions.True("hit_cracks_the_glass", Material(root).GetShaderParameter("crack").AsSingle() > 0f);
        assertions.True("hit_keeps_spine_held", await TrackStillAsync(heldNode));
        assertions.Equal("hit_keeps_time_stop", 1, held.GetPowerAmount<ClassicTimePower>());

        // --- Turn end: Time Stop runs out and the enemy moves again --------------
        await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
        assertions.Equal("time_stop_expires", 0, held.GetPowerAmount<ClassicTimePower>());
        await CombatScenarioContext.WaitUntilAsync(() => !GodotObject.IsInstanceValid(root), "hold released");
        assertions.Equal("release_restores_spine_scale", 1f, StateTimeScale(heldNode));
        assertions.True("release_spine_advances", !await TrackStillAsync(heldNode));
        assertions.True("release_skips_late_hurt", CurrentAnimation(heldNode) != "hurt");
        await CombatScenarioContext.WaitUntilAsync(() => RgbClose(heldNode.Visuals.Modulate, Colors.White), "tint restored");
        assertions.True("release_restores_tint", RgbClose(heldNode.Visuals.Modulate, Colors.White));
        await CombatScenarioContext.WaitUntilAsync(() => RgbClose(bothNode.Visuals.Modulate, Colors.White), "combined tint restored");
        assertions.True("combined_release_restores_tint", RgbClose(bothNode.Visuals.Modulate, Colors.White));

        // --- Clow Time's Stasis holds the last stack: the sand stays put ---------
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choiceContext =>
        {
            await PowerCmd.Apply<ClassicTimeStasisPower>(choiceContext, player.Creature, 1, player.Creature, null);
            await PowerCmd.Apply<ClassicTimePower>(choiceContext, held, 1, player.Creature, null);
        }));
        var stasisRoot = RootFor(held);
        assertions.True("reapply_mounts_a_hold", stasisRoot is not null);
        await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
        assertions.Equal("stasis_holds_last_stack", 1, held.GetPowerAmount<ClassicTimePower>());
        assertions.True("stasis_keeps_the_same_hold", stasisRoot is not null && ReferenceEquals(stasisRoot, RootFor(held)));
        if (stasisRoot is not null)
            assertions.Equal("stasis_sand_does_not_fall", 1f, Material(stasisRoot).GetShaderParameter("sand").AsSingle());
        assertions.True("stasis_spine_still_held", await TrackStillAsync(heldNode));

        // --- Death while held: native's death animation must play ----------------
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player,
            async _ => await CreatureCmd.Kill(held, force: true)));
        assertions.Equal("death_restores_spine_scale", 1f, StateTimeScale(heldNode));
        await CombatScenarioContext.WaitUntilAsync(() => stasisRoot is null || !GodotObject.IsInstanceValid(stasisRoot),
            "hold death cleanup");
        assertions.True("death_clears_the_hold", RootFor(held) is null);

        var previous = RootFor(both);
        combat = await context.EnterWeakSlimesCombatAsync();
        assertions.True("next_combat_no_hold_left", previous is null || !GodotObject.IsInstanceValid(previous));
        return new Dictionary<string, object?> { ["enemies"] = enemies.Length };
    }

    private static Node2D? RootFor(Creature creature) =>
        NCombatRoom.Instance!.GetCreatureNode(creature)?.GetNodeOrNull<Node2D>($"{TimeFreezeVisual.RootName}_{creature.CombatId}");

    private static ShaderMaterial Material(Node2D root) =>
        (ShaderMaterial)root.GetNode<ColorRect>("Hourglass").Material;

    private static ShaderMaterial? FreezeIce(Creature creature) =>
        NCombatRoom.Instance!.GetCreatureNode(creature)
            ?.GetNodeOrNull<Node2D>($"{FreezeShellVisual.RootName}_{creature.CombatId}")
            ?.GetNode<ColorRect>("Ice").Material as ShaderMaterial;

    // What the player sees: the current track's time does not move.
    private static async Task<bool> TrackStillAsync(NCreature node)
    {
        var before = TrackTime(node);
        await Frames(HeldFrames);
        return Math.Abs(TrackTime(node) - before) < 0.0001f;
    }

    private static float TrackTime(NCreature node)
    {
        var track = node.SpineAnimation.GetCurrentTrack();
        return track?.GetTrackTime() ?? float.NaN;
    }

    private static string? CurrentAnimation(NCreature node)
    {
        var track = node.SpineAnimation.GetCurrentTrack();
        return track?.GetAnimationName();
    }

    // The binding has no getter; read the spine-godot AnimationState directly.
    private static float StateTimeScale(NCreature node) =>
        node.SpineAnimation.GetAnimationState()!.BoundObject.Call("get_time_scale").AsSingle();

    private static bool RgbClose(Color a, Color b) =>
        Math.Abs(a.R - b.R) < 0.002f && Math.Abs(a.G - b.G) < 0.002f && Math.Abs(a.B - b.B) < 0.002f;

    private static async Task Frames(int count)
    {
        for (var i = 0; i < count; i++)
            await NGame.Instance!.AwaitProcessFrame();
    }
}
