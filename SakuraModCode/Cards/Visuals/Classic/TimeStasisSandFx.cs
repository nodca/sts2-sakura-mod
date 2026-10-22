using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
/// Clow Time's Stasis has no standing visual; it shows only when it acts. At
/// each turn end it keeps Block, energy, the hand and element states, and sand
/// drops onto each of them; when it blocks an enemy debuff's decay, sand drops
/// onto that debuff's icon. Fire-and-forget: nothing here waits or throws into
/// the turn-end resolution.
/// </summary>
internal static class TimeStasisSandFx
{
    internal const string ShaderPath = MainFile.ResPath + "/shaders/card_vfx/time_stasis_sand.gdshader";
    internal const string RootName = "SakuraTimeStasisSand";
    internal static IReadOnlyList<string> AssetPaths { get; } = [ShaderPath];

    internal const float Lifetime = 0.6f;
    // Above the hand and energy UI, like the hourglass's flying sand.
    private const int SandZIndex = 3000;
    private const int MaxHandCards = 10;
    // Room above the display for the drop, and a little around it for the rim.
    private const float DropRoom = 60f;
    private const float Margin = 14f;

    private static bool _failureLogged;

    private sealed record Piece(ColorRect Rect, ShaderMaterial Material, Control Target);

    /// <summary>Stasis kept the owner's state across this turn end.</summary>
    internal static void BurstOnPreserved(Creature owner)
    {
        if (!TryGetRoom(out var room))
            return;

        Guard("turn end", () =>
        {
            var targets = new List<(string Kind, Control Node)>();
            foreach (var group in SakuraPreservedStateTargets.Resolve(room, room.GetCreatureNode(owner), owner))
            {
                foreach (var node in group.Nodes.Take(group.Kind == "hand" ? MaxHandCards : 1))
                    targets.Add((group.Kind, node));
            }
            Burst(room, targets);
        });
    }

    /// <summary>Stasis just blocked the decay of this enemy debuff.</summary>
    internal static void BurstOnPower(PowerModel power)
    {
        if (!TryGetRoom(out var room))
            return;

        Guard("blocked decay", () =>
        {
            if (FindPowerNode(room, power) is { } node)
                Burst(room, [("power", node)]);
        });
    }

    private static bool TryGetRoom(out NCombatRoom room)
    {
        room = null!;
        if (TestMode.IsOn
            || !SakuraModConfig.IsCardVfxEnabled()
            || NCombatRoom.Instance is not { Ui: { } ui } current
            || !GodotObject.IsInstanceValid(ui))
        {
            return false;
        }
        room = current;
        return true;
    }

    private static NPower? FindPowerNode(NCombatRoom room, PowerModel power)
    {
        var display = room.GetCreatureNode(power.Owner)?.GetNodeOrNull<NCreatureStateDisplay>("%HealthBar");
        if (display?.GetNodeOrNull<Control>("%PowerContainer") is not { } powers)
            return null;

        foreach (var node in powers.GetChildren().OfType<NPower>())
        {
            try
            {
                if (ReferenceEquals(node.Model, power) && node.IsVisibleInTree())
                    return node;
            }
            catch (InvalidOperationException)
            {
                // A power node whose model is not bound yet.
            }
        }
        return null;
    }

    // One root per burst, one sand rect per display; the root frees itself when
    // the tween ends.
    private static void Burst(NCombatRoom room, IReadOnlyList<(string Kind, Control Node)> targets)
    {
        if (targets.Count == 0)
            return;

        var shader = PreloadManager.Cache.GetAsset<Shader>(ShaderPath);
        var root = new Node2D
        {
            Name = RootName,
            ZAsRelative = false,
            ZIndex = SandZIndex
        };
        room.Ui.AddChildSafely(root);

        var pieces = new List<Piece>(targets.Count);
        foreach (var (kind, node) in targets)
        {
            var material = new ShaderMaterial { Shader = shader };
            material.SetShaderParameter("roundness", kind == "hand" ? 0.08f : 1f);
            material.SetShaderParameter("seed", Random.Shared.NextSingle() * 37f);
            material.SetShaderParameter("progress", 0f);
            var rect = new ColorRect
            {
                // Unique per rect: Godot renames a repeated sibling name (every hand card).
                Name = $"Sand_{kind}_{pieces.Count}",
                Color = Colors.White,
                Material = material,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            root.AddChild(rect);
            pieces.Add(new Piece(rect, material, node));
        }

        Advance(pieces, 0f);
        var tween = root.CreateTween();
        tween.TweenMethod(Callable.From<float>(progress => Advance(pieces, progress)), 0f, 1f, Lifetime);
        tween.TweenCallback(Callable.From(root.QueueFreeSafely));
    }

    // Re-reads each display every frame: hand cards still settle while it plays.
    private static void Advance(IReadOnlyList<Piece> pieces, float progress)
    {
        foreach (var piece in pieces)
        {
            if (!GodotObject.IsInstanceValid(piece.Rect))
                continue;
            if (!GodotObject.IsInstanceValid(piece.Target) || !piece.Target.IsVisibleInTree())
            {
                piece.Rect.Visible = false;
                continue;
            }

            var target = piece.Target.GetGlobalRect();
            var offset = new Vector2(Margin, DropRoom);
            var size = target.Size + new Vector2(Margin * 2f, DropRoom + Margin);
            piece.Rect.Size = size;
            piece.Rect.GlobalPosition = target.Position - offset;
            piece.Material.SetShaderParameter("region_size", size);
            piece.Material.SetShaderParameter("target_rect",
                new Vector4(offset.X, offset.Y, target.Size.X, target.Size.Y));
            piece.Material.SetShaderParameter("progress", progress);
        }
    }

    private static void Guard(string label, System.Action burst)
    {
        try
        {
            burst();
        }
        catch (Exception exception)
        {
            if (_failureLogged)
                return;
            _failureLogged = true;
            MainFile.Logger.Error($"Clow Time Stasis sand ({label}) failed: {exception}");
        }
    }
}
