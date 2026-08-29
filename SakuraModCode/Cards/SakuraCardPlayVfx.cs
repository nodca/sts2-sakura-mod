using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;

namespace SakuraMod.SakuraModCode.Cards;

public static class SakuraCardPlayVfx
{
    private const int VfxZIndex = 3000;
    private const float TimeDuration = 0.95f;

    private static readonly Color TimeGoldColor = new(1f, 0.88f, 0.54f, 0.76f);
    private static readonly Color TimeBlueColor = new(0.72f, 0.9f, 1f, 0.56f);

    public static void PlayTime(Creature owner)
    {
        if (!TryCreateRoot("SakuraTimeVfx", out var root, out var room))
            return;

        root.GlobalPosition = CreatureCenter(room, owner) + Vector2.Up * 18f;
        BuildTime(root);
        TaskHelper.RunSafely(AnimateTime(root));
    }

    private static bool TryCreateRoot(string name, out Node2D root, out NCombatRoom room)
    {
        root = null!;
        room = null!;

        if (!SakuraModConfig.IsCardVfxEnabled()
            || TestMode.IsOn
            || NCombatRoom.Instance is not { } currentRoom)
            return false;

        var container = currentRoom.CombatVfxContainer;
        if (container is null)
            return false;

        room = currentRoom;
        root = new Node2D
        {
            Name = name,
            ZIndex = VfxZIndex,
            ZAsRelative = false
        };
        container.AddChildSafely(root);
        return true;
    }

    private static Vector2 CreatureCenter(NCombatRoom room, Creature creature) =>
        room.GetCreatureNode(creature)?.VfxSpawnPosition ?? RoomCenter(room);

    private static Vector2 RoomCenter(NCombatRoom room) =>
        room.CombatVfxContainer.GetViewportRect().GetCenter();

    private static void BuildTime(Node2D root)
    {
        AddEllipse(root, 72f, 72f, TimeGoldColor, 2.6f, 0f, "TimeRing");
        AddEllipse(root, 54f, 54f, TimeBlueColor, 1.8f, 0f, "TimeRing");

        for (var i = 0; i < 12; i++)
        {
            var angle = -Mathf.Pi * 0.5f + Mathf.Tau * i / 12f;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var tick = new Line2D
            {
                Name = "TimeTick",
                Width = i % 3 == 0 ? 2.5f : 1.5f,
                DefaultColor = i % 3 == 0 ? TimeGoldColor : TimeBlueColor,
                Antialiased = true,
                Points = [direction * 58f, direction * 70f]
            };
            root.AddChild(tick);
        }

        var minute = new Line2D
        {
            Name = "TimeHand",
            Width = 2.4f,
            DefaultColor = TimeGoldColor,
            Antialiased = true,
            Points = [Vector2.Zero, Vector2.Up * 48f]
        };
        root.AddChild(minute);

        var hour = new Line2D
        {
            Name = "TimeHand",
            Width = 3.2f,
            DefaultColor = TimeBlueColor,
            Antialiased = true,
            Points = [Vector2.Zero, new Vector2(32f, -18f)]
        };
        root.AddChild(hour);
    }

    private static void AddEllipse(
        Node2D root,
        float radiusX,
        float radiusY,
        Color color,
        float width,
        float rotation,
        string name,
        Vector2? position = null)
    {
        var ring = new Line2D
        {
            Name = name,
            Width = width,
            DefaultColor = color,
            Closed = true,
            Antialiased = true,
            Points = EllipsePoints(radiusX, radiusY, rotation)
        };
        ring.Position = position ?? Vector2.Zero;
        root.AddChild(ring);
    }

    private static Vector2[] EllipsePoints(float radiusX, float radiusY, float rotation)
    {
        const int pointCount = 64;
        var points = new Vector2[pointCount];
        var sin = MathF.Sin(rotation);
        var cos = MathF.Cos(rotation);

        for (var i = 0; i < pointCount; i++)
        {
            var angle = Mathf.Tau * i / pointCount;
            var point = new Vector2(MathF.Cos(angle) * radiusX, MathF.Sin(angle) * radiusY);
            points[i] = new Vector2(point.X * cos - point.Y * sin, point.X * sin + point.Y * cos);
        }

        return points;
    }

    private static async Task AnimateTime(Node2D root)
    {
        if (!root.IsInsideTree())
            return;

        var tween = root.CreateTween().SetParallel();
        tween.TweenProperty(root, "scale", Vector2.One * 1.12f, TimeDuration * 0.62f)
            .From(Vector2.One * 0.82f)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(root, "modulate:a", 0f, TimeDuration * 0.34f)
            .SetDelay(TimeDuration * 0.62f);

        foreach (var ring in root.GetChildren().OfType<Line2D>().Where(node => node.Name == "TimeRing"))
        {
            tween.TweenProperty(ring, "rotation", ring.Rotation + Mathf.Pi * 0.38f, TimeDuration)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Cubic);
        }

        foreach (var hand in root.GetChildren().OfType<Line2D>().Where(node => node.Name == "TimeHand"))
        {
            tween.TweenProperty(hand, "rotation", hand.Rotation + Mathf.Pi * 0.16f, TimeDuration * 0.52f)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Cubic);
        }

        await root.ToSignal(tween, Tween.SignalName.Finished);
        root.QueueFreeSafely();
    }
}
