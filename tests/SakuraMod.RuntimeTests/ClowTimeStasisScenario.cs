using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

/// <summary>
/// Plays a real Clow Time and checks its presentation: the hourglass session
/// lands Stasis on the halt and frees its sand; at the turn end Stasis drops
/// one sand rect per preserved display and one on each enemy debuff whose decay
/// it blocked; with card VFX off nothing mounts while gameplay is unchanged.
/// </summary>
internal static class ClowTimeStasisScenario
{
    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions)
    {
        var context = await CombatScenarioContext.StartAsync(request);
        var combat = await context.EnterWeakSlimesCombatAsync();
        var player = context.Player;
        var owner = player.Creature;
        var room = NCombatRoom.Instance!;
        var enemy = combat.HittableEnemies.First();
        assertions.True("fixture_card_vfx_enabled", SakuraModConfig.IsCardVfxEnabled());

        // --- Play: Stasis lands on the halt, then the sand flies and frees ------
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choiceContext =>
        {
            await CreatureCmd.GainBlock(owner, 7, ValueProp.Unpowered, null);
            await PowerCmd.Apply<WeakPower>(choiceContext, enemy, 2, owner, null);
        }));
        var card = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowTime>(combat, player);
        float? haltedWhenApplied = null;
        Node2D? hourglass = null;
        Node2D? flyingSand = null;
        var watching = true;
        _ = WatchAsync(() =>
        {
            hourglass ??= room.CombatVfxContainer.GetNodeOrNull<Node2D>("SakuraClowTimeHourglassVfx");
            flyingSand ??= room.Ui.GetNodeOrNull<Node2D>(ClowTimeHourglassVfx.SandRootName);
            if (haltedWhenApplied is null && owner.GetPowerAmount<ClassicTimeStasisPower>() > 0)
                haltedWhenApplied = hourglass is { } glass && GodotObject.IsInstanceValid(glass)
                    ? HourglassMaterial(glass).GetShaderParameter("halted").AsSingle()
                    : -1f;
            return watching;
        });
        await CombatScenarioContext.PlayCardAsync(card);
        watching = false;

        assertions.Equal("play_applies_stasis", 1, owner.GetPowerAmount<ClassicTimeStasisPower>());
        assertions.True("play_ran_the_hourglass", hourglass is not null);
        assertions.Equal("stasis_lands_on_the_halt", 1f, haltedWhenApplied);
        assertions.True("play_reads_only_cached_vfx_assets",
            ClowTimeHourglassVfx.AssetPaths.Concat(TimeStasisSandFx.AssetPaths).All(PreloadManager.Cache.ContainsKey));
        assertions.True("hourglass_ignores_mouse", hourglass is null
            || hourglass.GetNode<ColorRect>("Hourglass").MouseFilter == Control.MouseFilterEnum.Ignore);
        await CombatScenarioContext.WaitUntilAsync(() => flyingSand is not null
            && GodotObject.IsInstanceValid(flyingSand) && flyingSand.GetChildCount() > 0, "hourglass sand scattered");
        assertions.True("scatter_spawns_grains", flyingSand is not null && GodotObject.IsInstanceValid(flyingSand)
            && flyingSand.GetChildCount() > 0);
        await CombatScenarioContext.WaitUntilAsync(() => (hourglass is null || !GodotObject.IsInstanceValid(hourglass))
            && (flyingSand is null || !GodotObject.IsInstanceValid(flyingSand)), "hourglass session released");
        assertions.True("hourglass_session_released", room.Ui.GetNodeOrNull(ClowTimeHourglassVfx.SandRootName) is null);

        // --- Turn end: one rect per preserved display, one per blocked debuff ---
        var expected = SakuraPreservedStateTargets.Resolve(room, room.GetCreatureNode(owner), owner)
            .Sum(static group => group.Kind == "hand" ? Math.Min(group.Nodes.Count, 10) : 1);
        var weakBefore = enemy.GetPowerAmount<WeakPower>();
        var bursts = await RecordBurstsAsync(room, () => CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player));
        var preserved = bursts.Where(static burst => !burst.Kinds.Contains("power")).ToList();
        assertions.Equal("turn_end_one_preserved_burst", 1, preserved.Count);
        assertions.Equal("turn_end_rect_per_display", expected, preserved.FirstOrDefault()?.Kinds.Count ?? 0);
        assertions.True("turn_end_covers_block_energy_and_hand", preserved.FirstOrDefault() is { } kept
            && kept.Kinds.Contains("block") && kept.Kinds.Contains("energy") && kept.Kinds.Contains("hand"));
        assertions.Equal("weak_decay_blocked", weakBefore, enemy.GetPowerAmount<WeakPower>());
        assertions.True("blocked_decay_drops_sand", bursts.Any(static burst => burst.Kinds.SequenceEqual(["power"])));
        assertions.True("sand_ignores_mouse", bursts.All(static burst => burst.IgnoresMouse));
        await CombatScenarioContext.WaitUntilAsync(() => bursts.All(static burst => !GodotObject.IsInstanceValid(burst.Root)),
            "turn-end sand released");
        assertions.True("sand_released_within_lifetime",
            bursts.All(static burst => burst.Lifetime <= TimeStasisSandFx.Lifetime + 0.5f));

        // --- VFX off: same gameplay, no nodes ------------------------------------
        var binding = SakuraModConfig.EnableCardVfxBinding;
        var original = binding.Read();
        try
        {
            binding.Write(false);
            // Turn two's hand is the kept hand plus the draw: make room for the card.
            await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async _ =>
            {
                foreach (var held in player.PlayerCombatState!.Hand.Cards.ToList())
                    await CardPileCmd.Add(held, PileType.Discard);
            }));
            var offCard = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowTime>(combat, player);
            var offPlay = await RecordBurstsAsync(room, () => CombatScenarioContext.PlayCardAsync(offCard));
            assertions.True("vfx_off_no_hourglass",
                room.CombatVfxContainer.GetNodeOrNull("SakuraClowTimeHourglassVfx") is null
                && room.Ui.GetNodeOrNull(ClowTimeHourglassVfx.SandRootName) is null);
            assertions.True("vfx_off_still_applies_stasis", owner.GetPowerAmount<ClassicTimeStasisPower>() > 0);
            var weakOff = enemy.GetPowerAmount<WeakPower>();
            var offTurn = await RecordBurstsAsync(room, () => CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player));
            assertions.Equal("vfx_off_no_sand", 0, offPlay.Count + offTurn.Count);
            assertions.Equal("vfx_off_still_blocks_decay", weakOff, enemy.GetPowerAmount<WeakPower>());
        }
        finally
        {
            binding.Write(original);
        }

        return new Dictionary<string, object?> { ["preserved_displays"] = expected, ["bursts"] = bursts.Count, ["kinds"] = string.Join("|", bursts.Select(static b => string.Join(",", b.Kinds))) };
    }

    private sealed class Burst(Node2D root, ulong born)
    {
        internal Node2D Root { get; } = root;
        internal List<string> Kinds { get; } = [];
        internal bool IgnoresMouse { get; set; } = true;
        internal ulong Born { get; } = born;
        internal ulong Freed { get; set; }
        internal float Lifetime => Freed == 0 ? float.PositiveInfinity : (Freed - Born) / 1000f;
    }

    // Burst roots live 0.6 s, inside the turn end; they are read as they enter
    // (deferred, once their rects are attached) and timed until they leave.
    private static async Task<List<Burst>> RecordBurstsAsync(NCombatRoom room, Func<Task> during)
    {
        var bursts = new List<Burst>();
        // Matched by its sand rects, not its name: two bursts alive at once get
        // the second one renamed by Godot.
        void OnChild(Node node)
        {
            if (node is not Node2D root)
                return;
            var born = Godot.Time.GetTicksMsec();
            Callable.From(() =>
            {
                if (!GodotObject.IsInstanceValid(root))
                    return;
                var rects = root.GetChildren().OfType<ColorRect>()
                    .Where(static rect => rect.Name.ToString().StartsWith("Sand_", StringComparison.Ordinal)).ToList();
                if (rects.Count == 0)
                    return;
                var burst = new Burst(root, born);
                root.TreeExiting += () => burst.Freed = Godot.Time.GetTicksMsec();
                foreach (var rect in rects)
                {
                    burst.Kinds.Add(rect.Name.ToString().Split('_')[1]);
                    burst.IgnoresMouse &= rect.MouseFilter == Control.MouseFilterEnum.Ignore;
                }
                bursts.Add(burst);
            }).CallDeferred();
        }

        room.Ui.ChildEnteredTree += OnChild;
        try
        {
            await during();
            // A burst that started at the very end still gets read.
            await NGame.Instance!.AwaitProcessFrame();
        }
        finally
        {
            room.Ui.ChildEnteredTree -= OnChild;
        }
        return bursts;
    }

    private static ShaderMaterial HourglassMaterial(Node2D root) =>
        (ShaderMaterial)root.GetNode<ColorRect>("Hourglass").Material;

    private static async Task WatchAsync(Func<bool> frame)
    {
        while (frame())
            await NGame.Instance!.AwaitProcessFrame();
    }
}
