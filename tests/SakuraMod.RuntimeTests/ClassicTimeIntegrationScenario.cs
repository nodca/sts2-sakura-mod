using System.Diagnostics;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using SakuraMod.SakuraModCode;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

/// <summary>
/// The three Time cards in one combat — Transparent Time's world stop, Clow
/// Time's hourglass, Sakura Time's sweep and hold — then Stasis carrying the
/// holds across a turn end and the holds releasing. Each step must leave no
/// node or tint of the others behind. A second combat replays the same steps
/// with card VFX off: identical gameplay, no nodes, shorter plays.
/// </summary>
internal static class ClassicTimeIntegrationScenario
{
    private sealed record Step(string Name, string Facts, double Seconds);

    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions)
    {
        var context = await CombatScenarioContext.StartAsync(request);
        assertions.True("fixture_card_vfx_enabled", SakuraModConfig.IsCardVfxEnabled());

        var on = await RunAsync(context, assertions, vfx: true);
        List<Step> off;
        var binding = SakuraModConfig.EnableCardVfxBinding;
        var original = binding.Read();
        try
        {
            binding.Write(false);
            off = await RunAsync(context, assertions, vfx: false);
        }
        finally
        {
            binding.Write(original);
        }

        foreach (var (withVfx, without) in on.Zip(off))
        {
            assertions.Equal($"vfx_off_same_gameplay_{withVfx.Name}", withVfx.Facts, without.Facts);
        }
        foreach (var name in new[] { "clow_time", "sakura_time" })
        {
            var withVfx = on.Single(step => step.Name == name);
            var without = off.Single(step => step.Name == name);
            assertions.True($"vfx_off_plays_faster_{name}", without.Seconds < withVfx.Seconds,
                $"{without.Seconds:F2}s off vs {withVfx.Seconds:F2}s on");
        }

        return new Dictionary<string, object?>
        {
            ["on"] = string.Join(" | ", on.Select(static step => $"{step.Name} {step.Seconds:F2}s {step.Facts}")),
            ["off"] = string.Join(" | ", off.Select(static step => $"{step.Name} {step.Seconds:F2}s {step.Facts}"))
        };
    }

    private static async Task<List<Step>> RunAsync(
        CombatScenarioContext context, RuntimeAssertionCollector assertions, bool vfx)
    {
        var tag = vfx ? "on" : "off";
        var combat = await context.EnterWeakSlimesCombatAsync();
        var player = context.Player;
        var owner = player.Creature;
        var room = NCombatRoom.Instance!;
        var enemies = combat.HittableEnemies.Take(2).ToArray();
        if (enemies.Length != 2) throw new InvalidOperationException("Time integration fixture requires two enemies.");
        var steps = new List<Step>();
        string Facts() => string.Join(",",
            $"turn+{player.PlayerCombatState!.TurnNumber}",
            $"stasis{owner.GetPowerAmount<ClassicTimeStasisPower>()}",
            $"stop{owner.GetPowerAmount<TimeStopPower>()}",
            string.Join("/", enemies.Select(static e => e.GetPowerAmount<ClassicTimePower>())),
            $"void{player.PlayerCombatState.DiscardPile.Cards.OfType<MegaCrit.Sts2.Core.Models.Cards.Void>().Count()}");

        // --- Transparent Time: world stop, the turn ends on the clack -----------
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player,
            async _ => await PlayerCmd.GainEnergy(3, player)));
        var turn = player.PlayerCombatState!.TurnNumber;
        var watch = Stopwatch.StartNew();
        var sawWorldStop = false;
        var watching = true;
        _ = WatchAsync(() =>
        {
            sawWorldStop |= room.CombatVfxContainer.GetNodeOrNull(TimeStopVfx.OverRootName) is not null;
            return watching;
        });
        var transparent = await CombatScenarioContext.AddGeneratedCardToHandAsync<SakuraMod.SakuraModCode.Cards.Time>(combat, player);
        await CombatScenarioContext.PlayCardAsync(transparent);
        await WaitForPlayPhaseAfterAsync(player, turn);
        watching = false;
        steps.Add(new Step("transparent_time", Facts(), watch.Elapsed.TotalSeconds));
        assertions.Equal($"{tag}_transparent_time_world_stop", vfx, sawWorldStop);
        await CombatScenarioContext.WaitUntilAsync(() => NoTimeNodes(room, owner, enemies), "world stop released");
        assertions.True($"{tag}_transparent_time_leaves_nothing", NoTimeNodes(room, owner, enemies) && AllUntinted(room, owner, enemies));

        // --- Clow Time: hourglass, Stasis on the halt --------------------------
        var clow = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowTime>(combat, player);
        var sawHourglass = false;
        watching = true;
        _ = WatchAsync(() =>
        {
            sawHourglass |= room.CombatVfxContainer.GetNodeOrNull("SakuraClowTimeHourglassVfx") is not null;
            return watching;
        });
        watch.Restart();
        await CombatScenarioContext.PlayCardAsync(clow);
        steps.Add(new Step("clow_time", Facts(), watch.Elapsed.TotalSeconds));
        await CombatScenarioContext.WaitUntilAsync(() => NoTimeNodes(room, owner, enemies), "hourglass released");
        watching = false;
        assertions.Equal($"{tag}_clow_time_hourglass", vfx, sawHourglass);
        assertions.True($"{tag}_clow_time_leaves_nothing", NoTimeNodes(room, owner, enemies) && AllUntinted(room, owner, enemies));

        // --- Sakura Time: sweep, then each enemy held in amber ------------------
        var sakura = await CombatScenarioContext.AddGeneratedCardToHandAsync<SakuraTime>(combat, player);
        watch.Restart();
        await CombatScenarioContext.PlayCardAsync(sakura);
        steps.Add(new Step("sakura_time", Facts(), watch.Elapsed.TotalSeconds));
        await CombatScenarioContext.WaitUntilAsync(
            () => room.CombatVfxContainer.GetNodeOrNull("SakuraTimeSweepVfx") is null, "sweep released");
        assertions.Equal($"{tag}_sakura_time_holds_each_enemy", vfx, enemies.All(e => HoldFor(room, e) is not null));
        assertions.True($"{tag}_sakura_time_tints_only_enemies", RgbWhite(room.GetCreatureNode(owner)!.Visuals.Modulate)
            && enemies.All(e => vfx != RgbWhite(room.GetCreatureNode(e)!.Visuals.Modulate)));
        assertions.True($"{tag}_no_other_time_nodes_during_hold",
            room.CombatVfxContainer.GetNodeOrNull(TimeStopVfx.OverRootName) is null
            && room.CombatVfxContainer.GetNodeOrNull("SakuraClowTimeHourglassVfx") is null
            && room.Ui.GetNodeOrNull(ClowTimeHourglassVfx.SandRootName) is null);

        // --- Turn end under Stasis: the holds carry over, sand drops ------------
        await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
        steps.Add(new Step("stasis_turn_end", Facts(), 0));
        assertions.Equal($"{tag}_stasis_keeps_holds", vfx, enemies.All(e => HoldFor(room, e) is not null));

        // --- Next turn end: Time Stop runs out, everything is released ---------
        await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
        steps.Add(new Step("release_turn_end", Facts(), 0));
        await CombatScenarioContext.WaitUntilAsync(
            () => NoTimeNodes(room, owner, enemies) && AllUntinted(room, owner, enemies), "holds released");
        assertions.True($"{tag}_release_leaves_nothing", NoTimeNodes(room, owner, enemies) && AllUntinted(room, owner, enemies));
        return steps;
    }

    private static async Task WaitForPlayPhaseAfterAsync(Player player, int previousTurn) =>
        await CombatScenarioContext.WaitUntilAsync(
            () => CombatManager.Instance.IsInProgress
                && player.PlayerCombatState is { Phase: PlayerTurnPhase.Play } current
                && current.TurnNumber > previousTurn,
            $"player Play phase after turn {previousTurn}");

    private static Node? HoldFor(NCombatRoom room, Creature enemy) =>
        room.GetCreatureNode(enemy)?.GetNodeOrNull($"{TimeFreezeVisual.RootName}_{enemy.CombatId}");

    // Every Time card's nodes: world stop layers and seals, the hourglass and its
    // sand, the sweep, Stasis's sand, and the holds.
    private static bool NoTimeNodes(NCombatRoom room, Creature owner, IEnumerable<Creature> enemies)
    {
        var ownerNode = room.GetCreatureNode(owner);
        return room.CombatVfxContainer.GetNodeOrNull(TimeStopVfx.OverRootName) is null
            && ownerNode?.GetNodeOrNull(TimeStopVfx.UnderRootName) is null
            && room.Ui.GetNodeOrNull(TimeStopVfx.SealRootName) is null
            && room.CombatVfxContainer.GetNodeOrNull("SakuraClowTimeHourglassVfx") is null
            && room.Ui.GetNodeOrNull(ClowTimeHourglassVfx.SandRootName) is null
            && room.CombatVfxContainer.GetNodeOrNull("SakuraTimeSweepVfx") is null
            && !room.Ui.GetChildren().Any(static node => node.GetChildren()
                .Any(static child => child.Name.ToString().StartsWith("Sand_", StringComparison.Ordinal)))
            && enemies.All(e => HoldFor(room, e) is null);
    }

    private static bool AllUntinted(NCombatRoom room, Creature owner, IEnumerable<Creature> enemies) =>
        enemies.Prepend(owner).All(c => room.GetCreatureNode(c) is not { } node || RgbWhite(node.Visuals.Modulate));

    private static bool RgbWhite(Color colour) =>
        Math.Abs(colour.R - 1f) < 0.002f && Math.Abs(colour.G - 1f) < 0.002f && Math.Abs(colour.B - 1f) < 0.002f;

    private static async Task WatchAsync(Func<bool> frame)
    {
        while (frame())
            await MegaCrit.Sts2.Core.Nodes.NGame.Instance!.ToSignal(
                MegaCrit.Sts2.Core.Nodes.NGame.Instance.GetTree(), SceneTree.SignalName.ProcessFrame);
    }
}
