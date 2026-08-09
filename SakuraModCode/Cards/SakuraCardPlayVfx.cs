using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;

namespace SakuraMod.SakuraModCode.Cards;

public static class SakuraCardPlayVfx
{
    private const int VfxZIndex = 3000;
    private const float TimeDuration = 0.95f;
    private const float GravitationDuration = 0.82f;
    private const float GaleDuration = 0.34f;

    private static readonly Color TimeGoldColor = new(1f, 0.88f, 0.54f, 0.76f);
    private static readonly Color TimeBlueColor = new(0.72f, 0.9f, 1f, 0.56f);
    private static readonly Color GravityColor = new(0.48f, 0.38f, 0.86f, 0.54f);
    private static readonly Color GravityLineColor = new(0.78f, 0.82f, 1f, 0.5f);
    private static readonly Color GaleEdgeColor = new(0.92f, 1f, 0.96f, 0.72f);
    private static readonly Color GaleBodyColor = new(0.54f, 0.96f, 0.88f, 0.26f);
    private static readonly Color GaleTrailColor = new(0.72f, 1f, 0.96f, 0.4f);

    public static Node2D CreateGaleWindBlade(Creature attacker, Creature target)
    {
        var root = new Node2D
        {
            Name = "SakuraGaleWindBladeVfx",
            ZIndex = VfxZIndex,
            ZAsRelative = false
        };

        var start = Vector2.Zero;
        var end = Vector2.Zero;
        if (NCombatRoom.Instance is { } room)
        {
            start = CreatureCenter(room, attacker);
            end = CreatureCenter(room, target);
        }

        var travel = end - start;
        var hasPath = travel.LengthSquared() > 1f;

        root.GlobalPosition = start;
        // Align the blade's long axis with travel so the player sees its slicing edge, not its broad face.
        root.Rotation = hasPath ? travel.Angle() : -0.44f;
        BuildGaleWindBlade(root);
        TaskHelper.RunSafely(AnimateGaleWindBlade(root, start, end));
        return root;
    }

    public static void PlayTime(Creature owner)
    {
        if (!TryCreateRoot("SakuraTimeVfx", out var root, out var room))
            return;

        root.GlobalPosition = CreatureCenter(room, owner) + Vector2.Up * 18f;
        BuildTime(root);
        TaskHelper.RunSafely(AnimateTime(root));
    }

    public static void PlayGravitation(IEnumerable<Creature> targets)
    {
        var targetList = targets.ToList();
        if (targetList.Count == 0 || !TryCreateRoot("SakuraGravitationVfx", out var root, out var room))
            return;

        var area = EnemyArea(room, targetList);
        root.GlobalPosition = area.Center + Vector2.Up * 4f;
        BuildGravitation(root, area);
        TaskHelper.RunSafely(AnimateGravitation(root));
    }

    private static bool TryCreateRoot(string name, out Node2D root, out NCombatRoom room)
    {
        root = null!;
        room = null!;

        if (TestMode.IsOn || NCombatRoom.Instance is not { } currentRoom)
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

    private static Vector2 CreatureFloor(NCombatRoom room, Creature creature)
    {
        var node = room.GetCreatureNode(creature);
        return node?.GetBottomOfHitbox() ?? node?.VfxSpawnPosition ?? RoomCenter(room);
    }

    private static Vector2 RoomCenter(NCombatRoom room) =>
        room.CombatVfxContainer.GetViewportRect().GetCenter();

    private static VfxArea EnemyArea(NCombatRoom room, IReadOnlyList<Creature> targets)
    {
        var centers = targets
            .Select(target => room.GetCreatureNode(target))
            .OfType<NCreature>()
            .Select(node => node.VfxSpawnPosition)
            .ToList();
        var floors = targets.Select(target => CreatureFloor(room, target)).ToList();

        if (centers.Count == 0)
        {
            var center = RoomCenter(room);
            return new VfxArea(center, 360f, 170f);
        }

        var minX = centers.Min(position => position.X);
        var maxX = centers.Max(position => position.X);
        var centerX = (minX + maxX) * 0.5f;
        var centerY = centers.Average(position => position.Y);
        var floorY = floors.Max(position => position.Y);
        var width = Math.Clamp(maxX - minX + 210f, 260f, 980f);
        var height = Math.Clamp(floorY - centerY + 130f, 150f, 310f);

        return new VfxArea(new Vector2(centerX, centerY), width, height);
    }

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

    private static void BuildGravitation(Node2D root, VfxArea area)
    {
        AddEllipse(root, area.Width * 0.35f, 24f, GravityColor, 2.6f, 0f, "GravityRing", Vector2.Down * area.Height * 0.32f);
        AddEllipse(root, area.Width * 0.24f, 14f, GravityLineColor, 1.8f, 0f, "GravityRing", Vector2.Down * area.Height * 0.22f);

        for (var i = 0; i < 7; i++)
        {
            var x = -area.Width * 0.36f + i * area.Width * 0.12f;
            var line = new Line2D
            {
                Name = "GravityLine",
                Width = i % 2 == 0 ? 2f : 1.4f,
                DefaultColor = i % 2 == 0 ? GravityLineColor : GravityColor,
                Antialiased = true,
                Points = [new Vector2(x, -area.Height * 0.44f), new Vector2(x, area.Height * 0.22f)]
            };
            root.AddChild(line);
        }
    }

    private static void BuildGaleWindBlade(Node2D root)
    {
        const float bladeLength = 380f;
        const float thickness = 24f;
        var back = new Vector2(-bladeLength * 0.5f, 0f);
        var front = new Vector2(bladeLength * 0.5f, 0f);
        // Slim crescent: thin perpendicular to travel so the player reads the slicing edge, not a flat face.
        var topControl = new Vector2(bladeLength * 0.04f, -thickness);
        var bottomControl = new Vector2(-bladeLength * 0.06f, thickness * 0.5f);

        var body = new Polygon2D
        {
            Name = "GaleBladeBody",
            Color = GaleBodyColor,
            Polygon =
            [
                back,
                new(-bladeLength * 0.22f, -thickness * 0.7f),
                topControl,
                new(bladeLength * 0.3f, -thickness * 0.5f),
                front,
                new(bladeLength * 0.26f, thickness * 0.42f),
                bottomControl,
                new(-bladeLength * 0.24f, thickness * 0.5f)
            ]
        };
        root.AddChild(body);

        var edge = new Line2D
        {
            Name = "GaleBladeEdge",
            Width = 5.4f,
            DefaultColor = GaleEdgeColor,
            Antialiased = true,
            Points = QuadraticPoints(back, topControl, front, 20)
        };
        root.AddChild(edge);

        var lowerEdge = new Line2D
        {
            Name = "GaleBladeEdge",
            Width = 2.4f,
            DefaultColor = GaleTrailColor,
            Antialiased = true,
            Points = QuadraticPoints(back, bottomControl, front, 16)
        };
        root.AddChild(lowerEdge);

        // Thin airflow streaks trailing straight back along the travel axis.
        for (var i = 0; i < 4; i++)
        {
            var offsetY = -thickness * 0.5f + i * thickness * 0.34f;
            var streakBack = new Vector2(-bladeLength * (0.5f + 0.14f * i), offsetY);
            var streakFront = new Vector2(bladeLength * 0.14f, offsetY * 0.5f);
            var trail = new Line2D
            {
                Name = "GaleTrail",
                Width = Math.Max(1f, 2.2f - i * 0.3f),
                DefaultColor = i % 2 == 0 ? GaleTrailColor : GaleBodyColor,
                Antialiased = true,
                Points = [streakBack, streakFront]
            };
            root.AddChild(trail);
        }
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

    private static Vector2[] QuadraticPoints(Vector2 start, Vector2 control, Vector2 end, int pointCount)
    {
        var points = new Vector2[pointCount];
        for (var i = 0; i < pointCount; i++)
        {
            var t = i / (float)(pointCount - 1);
            var inverse = 1f - t;
            points[i] = start * inverse * inverse + control * 2f * inverse * t + end * t * t;
        }

        return points;
    }

    private static async Task AnimateGaleWindBlade(Node2D root, Vector2 start, Vector2 end)
    {
        if (!root.IsInsideTree())
        {
            await root.ToSignal(root, Node.SignalName.TreeEntered);
            if (!root.IsInsideTree())
                return;
        }

        const float duration = GaleDuration;

        var tween = root.CreateTween().SetParallel();

        // Charge straight at the target at constant speed — no arc, no deceleration.
        tween.TweenProperty(root, "global_position", end, duration)
            .From(start)
            .SetTrans(Tween.TransitionType.Linear);
        // Stay solid through the charge, then cut out the instant it lands.
        tween.TweenProperty(root, "modulate:a", 0f, duration * 0.2f)
            .SetDelay(duration * 0.8f)
            .SetEase(Tween.EaseType.In)
            .SetTrans(Tween.TransitionType.Quad);

        foreach (var line in root.GetChildren().OfType<Line2D>().Where(node => node.Name == "GaleTrail"))
        {
            tween.TweenProperty(line, "position", line.Position + Vector2.Left * 34f, duration * 0.6f)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Cubic);
            tween.TweenProperty(line, "modulate:a", 0f, duration * 0.34f)
                .SetDelay(duration * 0.4f);
        }

        await root.ToSignal(tween, Tween.SignalName.Finished);
        root.QueueFreeSafely();
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

    private static async Task AnimateSimple(Node2D root, float duration)
    {
        if (!root.IsInsideTree())
            return;

        var tween = root.CreateTween().SetParallel();
        tween.TweenProperty(root, "scale", Vector2.One * 1.04f, duration * 0.52f)
            .From(Vector2.One * 0.92f)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(root, "modulate:a", 0f, duration * 0.36f)
            .SetDelay(duration * 0.58f);

        await root.ToSignal(tween, Tween.SignalName.Finished);
        root.QueueFreeSafely();
    }

    private static async Task AnimateGravitation(Node2D root)
    {
        if (!root.IsInsideTree())
            return;

        var tween = root.CreateTween().SetParallel();
        tween.TweenProperty(root, "modulate:a", 0f, GravitationDuration * 0.36f)
            .SetDelay(GravitationDuration * 0.6f);

        foreach (var line in root.GetChildren().OfType<Line2D>().Where(node => node.Name == "GravityLine"))
        {
            tween.TweenProperty(line, "position", line.Position + Vector2.Down * 30f, GravitationDuration * 0.62f)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Quad);
        }

        foreach (var ring in root.GetChildren().OfType<Line2D>().Where(node => node.Name == "GravityRing"))
        {
            tween.TweenProperty(ring, "scale", new Vector2(0.86f, 0.58f), GravitationDuration * 0.55f)
                .From(new Vector2(1.22f, 1.08f))
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Cubic);
        }

        await root.ToSignal(tween, Tween.SignalName.Finished);
        root.QueueFreeSafely();
    }

    private readonly record struct VfxArea(Vector2 Center, float Width, float Height);
}
