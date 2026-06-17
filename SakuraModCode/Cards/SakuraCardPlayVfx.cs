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
    private const float HailDuration = 0.58f;
    private const float BlazeDuration = 0.72f;
    private const float AquaDuration = 0.82f;
    private const float TimeDuration = 0.95f;
    private const float LabyrinthDuration = 0.88f;
    private const float GravitationDuration = 0.82f;
    private const float GaleDuration = 0.34f;

    private static readonly Color IceColor = new(0.76f, 0.96f, 1f, 0.88f);
    private static readonly Color IceCoreColor = new(0.93f, 1f, 1f, 0.95f);
    private static readonly Color FlameColor = new(1f, 0.35f, 0.16f, 0.72f);
    private static readonly Color FlameGoldColor = new(1f, 0.78f, 0.28f, 0.78f);
    private static readonly Color AquaColor = new(0.38f, 0.86f, 1f, 0.62f);
    private static readonly Color AquaFoamColor = new(0.86f, 1f, 1f, 0.72f);
    private static readonly Color TimeGoldColor = new(1f, 0.88f, 0.54f, 0.76f);
    private static readonly Color TimeBlueColor = new(0.72f, 0.9f, 1f, 0.56f);
    private static readonly Color LabyrinthGoldColor = new(1f, 0.86f, 0.52f, 0.72f);
    private static readonly Color LabyrinthPinkColor = new(1f, 0.72f, 0.8f, 0.48f);
    private static readonly Color GravityColor = new(0.48f, 0.38f, 0.86f, 0.54f);
    private static readonly Color GravityLineColor = new(0.78f, 0.82f, 1f, 0.5f);
    private static readonly Color GaleEdgeColor = new(0.92f, 1f, 0.96f, 0.72f);
    private static readonly Color GaleBodyColor = new(0.54f, 0.96f, 0.88f, 0.26f);
    private static readonly Color GaleTrailColor = new(0.72f, 1f, 0.96f, 0.4f);
    private static readonly Color GaleReleaseColor = new(0.82f, 1f, 0.95f, 0.32f);

    public static Node2D CreateGaleWindBlade(Creature attacker, Creature target, bool releaseFollowUp = false)
    {
        var root = new Node2D
        {
            Name = releaseFollowUp ? "SakuraGaleChasingBladeVfx" : "SakuraGaleWindBladeVfx",
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

        // Shift the chasing blade onto a parallel lane so it reads as a second gust.
        if (hasPath && releaseFollowUp)
        {
            var lane = new Vector2(-travel.Y, travel.X).Normalized() * 26f;
            start += lane;
            end += lane;
        }

        root.GlobalPosition = start;
        // Align the blade's long axis with travel so the player sees its slicing edge, not its broad face.
        root.Rotation = hasPath ? travel.Angle() : (releaseFollowUp ? -0.26f : -0.44f);
        root.Scale = releaseFollowUp ? Vector2.One * 0.86f : Vector2.One;
        BuildGaleWindBlade(root, releaseFollowUp);
        TaskHelper.RunSafely(AnimateGaleWindBlade(root, releaseFollowUp, start, end));
        return root;
    }

    public static void PlayHail(Creature target)
    {
        if (!TryCreateRoot("SakuraHailVfx", out var root, out var room))
            return;

        root.GlobalPosition = CreatureCenter(room, target) + Vector2.Up * 34f;
        BuildHail(root);
        TaskHelper.RunSafely(AnimateHail(root));
    }

    public static void PlayBlaze(Creature target)
    {
        if (!TryCreateRoot("SakuraBlazeVfx", out var root, out var room))
            return;

        root.GlobalPosition = CreatureCenter(room, target) + Vector2.Down * 10f;
        BuildBlaze(root);
        TaskHelper.RunSafely(AnimateBlaze(root));
    }

    public static void PlayAqua(IEnumerable<Creature> targets)
    {
        var targetList = targets.ToList();
        if (targetList.Count == 0 || !TryCreateRoot("SakuraAquaVfx", out var root, out var room))
            return;

        var area = EnemyArea(room, targetList);
        root.GlobalPosition = area.Center;
        BuildAqua(root, room, targetList, area);
        TaskHelper.RunSafely(AnimateAqua(root));
    }

    public static void PlayTime(Creature owner)
    {
        if (!TryCreateRoot("SakuraTimeVfx", out var root, out var room))
            return;

        root.GlobalPosition = CreatureCenter(room, owner) + Vector2.Up * 18f;
        BuildTime(root);
        TaskHelper.RunSafely(AnimateTime(root));
    }

    public static void PlayLabyrinth(IEnumerable<Creature> targets)
    {
        var targetList = targets.ToList();
        if (targetList.Count == 0 || !TryCreateRoot("SakuraLabyrinthVfx", out var root, out var room))
            return;

        var area = EnemyArea(room, targetList);
        root.GlobalPosition = area.Center + Vector2.Up * 16f;
        BuildLabyrinth(root, area);
        TaskHelper.RunSafely(AnimateSimple(root, LabyrinthDuration));
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

    private static void BuildHail(Node2D root)
    {
        for (var i = 0; i < 5; i++)
        {
            var shard = CreateDiamond(7f + i % 2 * 2f, 16f + i % 3 * 2f, i % 2 == 0 ? IceCoreColor : IceColor);
            shard.Name = "HailShard";
            shard.Position = new Vector2(-44f + i * 22f, -62f - i % 2 * 12f);
            shard.Rotation = -0.18f + i * 0.09f;
            root.AddChild(shard);

            var streak = new Line2D
            {
                Name = "HailStreak",
                Width = 1.8f,
                DefaultColor = IceColor,
                Antialiased = true,
                Points = [shard.Position + Vector2.Up * 16f, shard.Position + Vector2.Down * 16f]
            };
            root.AddChild(streak);
        }

        for (var i = 0; i < 6; i++)
        {
            var angle = Mathf.Tau * i / 6f;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var impact = new Line2D
            {
                Name = "HailImpact",
                Width = 1.9f,
                DefaultColor = IceCoreColor,
                Antialiased = true,
                Points = [direction * 4f, direction * 22f],
                Modulate = new Color(1f, 1f, 1f, 0.76f)
            };
            impact.Position = Vector2.Down * 30f;
            root.AddChild(impact);
        }
    }

    private static void BuildBlaze(Node2D root)
    {
        AddEllipse(root, 48f, 18f, FlameGoldColor, 2.4f, 0f, "BlazeRing");
        for (var i = 0; i < 7; i++)
        {
            var angle = -Mathf.Pi * 0.85f + i * Mathf.Pi * 0.28f;
            var flame = new Polygon2D
            {
                Name = "BlazeFlame",
                Color = i % 2 == 0 ? FlameColor : FlameGoldColor,
                Polygon =
                [
                    new(0f, -48f),
                    new(17f, -7f),
                    new(5f, 20f),
                    new(-15f, 10f)
                ],
                Position = new Vector2(MathF.Cos(angle) * 28f, 18f + MathF.Sin(angle) * 8f),
                Rotation = angle * 0.24f
            };
            root.AddChild(flame);
        }

        for (var i = 0; i < 10; i++)
        {
            var ember = CreateDiamond(3.2f, 3.2f, i % 2 == 0 ? FlameGoldColor : FlameColor);
            ember.Name = "BlazeEmber";
            ember.Position = new Vector2(-38f + i * 8f, 8f - i % 3 * 9f);
            root.AddChild(ember);
        }
    }

    private static void BuildAqua(Node2D root, NCombatRoom room, IReadOnlyList<Creature> targets, VfxArea area)
    {
        for (var i = 0; i < 3; i++)
        {
            var wave = new Line2D
            {
                Name = "AquaWave",
                Width = 2.2f - i * 0.25f,
                DefaultColor = i == 0 ? AquaFoamColor : AquaColor,
                Antialiased = true,
                Points = WavePoints(area.Width, 16f + i * 5f, i * 0.72f)
            };
            wave.Position = new Vector2(0f, 20f + i * 13f);
            root.AddChild(wave);
        }

        foreach (var target in targets)
        {
            var localFloor = CreatureFloor(room, target) - root.GlobalPosition;
            AddEllipse(root, 42f, 14f, AquaColor, 2f, 0f, "AquaRipple", localFloor + Vector2.Up * 8f);
        }
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

    private static void BuildLabyrinth(Node2D root, VfxArea area)
    {
        var width = area.Width * 0.86f;
        var height = area.Height * 0.74f;
        var half = new Vector2(width * 0.5f, height * 0.5f);

        var outer = new Line2D
        {
            Name = "LabyrinthLine",
            Width = 2.6f,
            DefaultColor = LabyrinthGoldColor,
            Closed = true,
            Antialiased = true,
            Points =
            [
                new(-half.X, -half.Y),
                new(half.X, -half.Y),
                new(half.X, half.Y),
                new(-half.X, half.Y)
            ]
        };
        root.AddChild(outer);

        var maze = new Line2D
        {
            Name = "LabyrinthLine",
            Width = 2f,
            DefaultColor = LabyrinthPinkColor,
            Antialiased = true,
            Points =
            [
                new(-half.X * 0.72f, -half.Y * 0.48f),
                new(-half.X * 0.18f, -half.Y * 0.48f),
                new(-half.X * 0.18f, -half.Y * 0.12f),
                new(half.X * 0.42f, -half.Y * 0.12f),
                new(half.X * 0.42f, half.Y * 0.22f),
                new(-half.X * 0.46f, half.Y * 0.22f),
                new(-half.X * 0.46f, half.Y * 0.52f),
                new(half.X * 0.68f, half.Y * 0.52f)
            ]
        };
        root.AddChild(maze);

        for (var i = 0; i < 4; i++)
        {
            var x = -half.X * 0.5f + i * width / 3f;
            var gate = new Line2D
            {
                Name = "LabyrinthLine",
                Width = 1.6f,
                DefaultColor = i % 2 == 0 ? LabyrinthGoldColor : LabyrinthPinkColor,
                Antialiased = true,
                Points = [new Vector2(x, -half.Y * 0.74f), new Vector2(x, -half.Y * 0.36f)]
            };
            root.AddChild(gate);
        }
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

    private static void BuildGaleWindBlade(Node2D root, bool releaseFollowUp)
    {
        var bladeLength = releaseFollowUp ? 320f : 380f;
        var thickness = releaseFollowUp ? 18f : 24f;
        var back = new Vector2(-bladeLength * 0.5f, 0f);
        var front = new Vector2(bladeLength * 0.5f, 0f);
        // Slim crescent: thin perpendicular to travel so the player reads the slicing edge, not a flat face.
        var topControl = new Vector2(bladeLength * 0.04f, -thickness);
        var bottomControl = new Vector2(-bladeLength * 0.06f, thickness * 0.5f);

        var body = new Polygon2D
        {
            Name = "GaleBladeBody",
            Color = releaseFollowUp ? GaleReleaseColor : GaleBodyColor,
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
            Width = releaseFollowUp ? 4.4f : 5.4f,
            DefaultColor = GaleEdgeColor,
            Antialiased = true,
            Points = QuadraticPoints(back, topControl, front, 20)
        };
        root.AddChild(edge);

        var lowerEdge = new Line2D
        {
            Name = "GaleBladeEdge",
            Width = releaseFollowUp ? 1.8f : 2.4f,
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

    private static Polygon2D CreateDiamond(float width, float height, Color color) =>
        new()
        {
            Color = color,
            Polygon =
            [
                new(0f, -height * 0.5f),
                new(width * 0.5f, 0f),
                new(0f, height * 0.5f),
                new(-width * 0.5f, 0f)
            ]
        };

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

    private static Vector2[] WavePoints(float width, float amplitude, float phase)
    {
        const int pointCount = 36;
        var points = new Vector2[pointCount];
        for (var i = 0; i < pointCount; i++)
        {
            var t = i / (float)(pointCount - 1);
            var x = -width * 0.5f + width * t;
            var y = MathF.Sin(t * Mathf.Tau * 1.35f + phase) * amplitude;
            points[i] = new Vector2(x, y);
        }

        return points;
    }

    private static async Task AnimateHail(Node2D root)
    {
        if (!root.IsInsideTree())
            return;

        var tween = root.CreateTween().SetParallel();
        tween.TweenProperty(root, "modulate:a", 0f, HailDuration * 0.28f)
            .SetDelay(HailDuration * 0.72f);

        var shards = root.GetChildren().OfType<Polygon2D>().Where(node => node.Name == "HailShard").ToList();
        for (var i = 0; i < shards.Count; i++)
        {
            var shard = shards[i];
            var delay = i * 0.035f;
            tween.TweenProperty(shard, "position", shard.Position + Vector2.Down * 86f, HailDuration * 0.62f)
                .SetDelay(delay)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Quad);
            tween.TweenProperty(shard, "scale", Vector2.Zero, HailDuration * 0.2f)
                .SetDelay(delay + HailDuration * 0.48f);
        }

        foreach (var streak in root.GetChildren().OfType<Line2D>().Where(node => node.Name == "HailStreak"))
        {
            tween.TweenProperty(streak, "position", streak.Position + Vector2.Down * 64f, HailDuration * 0.55f)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Quad);
            tween.TweenProperty(streak, "modulate:a", 0f, HailDuration * 0.22f)
                .SetDelay(HailDuration * 0.38f);
        }

        foreach (var impact in root.GetChildren().OfType<Line2D>().Where(node => node.Name == "HailImpact"))
        {
            tween.TweenProperty(impact, "scale", Vector2.One * 1.28f, HailDuration * 0.34f)
                .From(Vector2.One * 0.12f)
                .SetDelay(HailDuration * 0.42f)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Cubic);
        }

        await root.ToSignal(tween, Tween.SignalName.Finished);
        root.QueueFreeSafely();
    }

    private static async Task AnimateBlaze(Node2D root)
    {
        if (!root.IsInsideTree())
            return;

        var tween = root.CreateTween().SetParallel();
        tween.TweenProperty(root, "scale", Vector2.One * 1.14f, BlazeDuration * 0.58f)
            .From(Vector2.One * 0.82f)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(root, "modulate:a", 0f, BlazeDuration * 0.38f)
            .SetDelay(BlazeDuration * 0.56f);

        foreach (var flame in root.GetChildren().OfType<Polygon2D>().Where(node => node.Name == "BlazeFlame"))
        {
            tween.TweenProperty(flame, "position", flame.Position + Vector2.Up * 34f, BlazeDuration)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Cubic);
            tween.TweenProperty(flame, "scale", Vector2.Zero, BlazeDuration * 0.42f)
                .SetDelay(BlazeDuration * 0.5f);
        }

        var embers = root.GetChildren().OfType<Polygon2D>().Where(node => node.Name == "BlazeEmber").ToList();
        for (var i = 0; i < embers.Count; i++)
        {
            var ember = embers[i];
            var direction = new Vector2(-0.7f + i * 0.15f, -1f).Normalized();
            tween.TweenProperty(ember, "position", ember.Position + direction * (46f + i % 3 * 10f), BlazeDuration)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Cubic);
            tween.TweenProperty(ember, "scale", Vector2.Zero, BlazeDuration * 0.36f)
                .SetDelay(BlazeDuration * 0.56f);
        }

        await root.ToSignal(tween, Tween.SignalName.Finished);
        root.QueueFreeSafely();
    }

    private static async Task AnimateGaleWindBlade(Node2D root, bool releaseFollowUp, Vector2 start, Vector2 end)
    {
        if (!root.IsInsideTree())
        {
            await root.ToSignal(root, Node.SignalName.TreeEntered);
            if (!root.IsInsideTree())
                return;
        }

        var duration = releaseFollowUp ? GaleDuration * 0.82f : GaleDuration;

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
            tween.TweenProperty(line, "position", line.Position + Vector2.Left * (releaseFollowUp ? 26f : 34f), duration * 0.6f)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Cubic);
            tween.TweenProperty(line, "modulate:a", 0f, duration * 0.34f)
                .SetDelay(duration * 0.4f);
        }

        await root.ToSignal(tween, Tween.SignalName.Finished);
        root.QueueFreeSafely();
    }

    private static async Task AnimateAqua(Node2D root)
    {
        if (!root.IsInsideTree())
            return;

        var tween = root.CreateTween().SetParallel();
        tween.TweenProperty(root, "modulate:a", 0f, AquaDuration * 0.38f)
            .SetDelay(AquaDuration * 0.58f);

        var waves = root.GetChildren().OfType<Line2D>().Where(node => node.Name == "AquaWave").ToList();
        for (var i = 0; i < waves.Count; i++)
        {
            var wave = waves[i];
            tween.TweenProperty(wave, "position", wave.Position + new Vector2(42f - i * 24f, -8f), AquaDuration * 0.78f)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Sine);
            tween.TweenProperty(wave, "scale", new Vector2(1.04f, 0.76f), AquaDuration)
                .From(new Vector2(0.9f, 1f));
        }

        foreach (var ripple in root.GetChildren().OfType<Line2D>().Where(node => node.Name == "AquaRipple"))
        {
            tween.TweenProperty(ripple, "scale", Vector2.One * 1.42f, AquaDuration * 0.62f)
                .From(Vector2.One * 0.58f)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Cubic);
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
