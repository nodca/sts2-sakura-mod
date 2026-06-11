using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;

namespace SakuraMod.SakuraModCode.Cards;

public static class PileExchangeVfx
{
    private const float Duration = 0.9f;
    private const int VfxZIndex = 3000;
    private const int GhostCount = 5;
    private static readonly Color DrawToDiscardColor = new(0.66f, 0.9f, 1f, 0.78f);
    private static readonly Color DiscardToDrawColor = new(1f, 0.78f, 0.56f, 0.78f);
    private static readonly Color PileGlowColor = new(1f, 0.95f, 0.72f, 0.72f);
    private static readonly Color CenterSparkColor = new(1f, 1f, 0.9f, 0.9f);
    private static readonly Vector2 GhostCardSize = new(30f, 42f);

    public static void Play(int drawCount, int discardCount)
    {
        if (TestMode.IsOn || drawCount + discardCount == 0 || NCombatRoom.Instance is not { } room || room.Ui is not { } ui)
            return;

        var container = (Control?)room.Ui ?? room.CombatVfxContainer;
        if (container is null)
            return;

        var drawCenter = CenterOf(ui.DrawPile);
        var discardCenter = CenterOf(ui.DiscardPile);
        if (drawCenter.DistanceSquaredTo(discardCenter) <= 1f)
            return;

        var midpoint = (drawCenter + discardCenter) * 0.5f;
        var root = new Node2D
        {
            Name = "SakuraPileExchangeVfx",
            ZIndex = VfxZIndex,
            ZAsRelative = false
        };
        container.AddChildSafely(root);
        root.GlobalPosition = midpoint;

        var draw = drawCenter - midpoint;
        var discard = discardCenter - midpoint;
        BuildPileGlow(root, draw);
        BuildPileGlow(root, discard);
        if (drawCount > 0)
        {
            BuildArc(root, draw, discard, DrawToDiscardColor, -1f);
            BuildGhostCards(root, draw, discard, DrawToDiscardColor, -1f, Math.Clamp(drawCount, 1, GhostCount));
        }

        if (discardCount > 0)
        {
            BuildArc(root, discard, draw, DiscardToDrawColor, 1f);
            BuildGhostCards(root, discard, draw, DiscardToDrawColor, 1f, Math.Clamp(discardCount, 1, GhostCount));
        }

        BuildCenterSpark(root);
        TaskHelper.RunSafely(Animate(root));
    }

    private static Vector2 CenterOf(Control control) =>
        control.GlobalPosition + new Vector2(control.Size.X * control.Scale.X, control.Size.Y * control.Scale.Y) * 0.5f;

    private static void BuildPileGlow(Node2D root, Vector2 position)
    {
        var glow = new Line2D
        {
            Name = "PileGlow",
            Width = 4.5f,
            DefaultColor = PileGlowColor,
            Closed = true,
            Antialiased = true,
            Points = EllipsePoints(42f, 30f)
        };
        glow.Position = position;
        root.AddChild(glow);
    }

    private static void BuildArc(Node2D root, Vector2 start, Vector2 end, Color color, float direction)
    {
        var arc = new Line2D
        {
            Name = "ExchangeArc",
            Width = 2.4f,
            DefaultColor = color,
            Antialiased = true,
            Points = ArcPoints(start, end, direction)
        };
        root.AddChild(arc);
    }

    private static void BuildGhostCards(Node2D root, Vector2 start, Vector2 end, Color color, float direction, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var ghost = new Polygon2D
            {
                Name = direction < 0f ? "DrawToDiscardGhost" : "DiscardToDrawGhost",
                Color = color,
                Polygon =
                [
                    new(-GhostCardSize.X * 0.5f, -GhostCardSize.Y * 0.5f),
                    new(GhostCardSize.X * 0.5f, -GhostCardSize.Y * 0.5f),
                    new(GhostCardSize.X * 0.5f, GhostCardSize.Y * 0.5f),
                    new(-GhostCardSize.X * 0.5f, GhostCardSize.Y * 0.5f)
                ],
                Position = start,
                Rotation = DirectionAngle(start, end) + (direction < 0f ? -0.18f : 0.18f),
                Scale = Vector2.One * (0.8f + i * 0.04f)
            };
            root.AddChild(ghost);
        }
    }

    private static void BuildCenterSpark(Node2D root)
    {
        for (var i = 0; i < 8; i++)
        {
            var angle = Mathf.Tau * i / 8f;
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var line = new Line2D
            {
                Name = "CenterSpark",
                Width = i % 2 == 0 ? 2.4f : 1.6f,
                DefaultColor = CenterSparkColor,
                Antialiased = true,
                Points = [direction * 6f, direction * 28f]
            };
            root.AddChild(line);
        }
    }

    private static async Task Animate(Node2D root)
    {
        if (!root.IsInsideTree())
            return;

        var tween = root.CreateTween().SetParallel();
        tween.TweenProperty(root, "modulate:a", 0f, Duration * 0.28f)
            .SetDelay(Duration * 0.72f)
            .SetEase(Tween.EaseType.In)
            .SetTrans(Tween.TransitionType.Quad);

        AnimatePileGlows(root, tween);
        AnimateArcs(root, tween);
        AnimateGhosts(root, tween, "DrawToDiscardGhost", -1f);
        AnimateGhosts(root, tween, "DiscardToDrawGhost", 1f);
        AnimateCenterSpark(root, tween);

        await root.ToSignal(tween, Tween.SignalName.Finished);
        root.QueueFreeSafely();
    }

    private static void AnimatePileGlows(Node2D root, Tween tween)
    {
        foreach (var glow in root.GetChildren().OfType<Line2D>().Where(node => node.Name == "PileGlow"))
        {
            tween.TweenProperty(glow, "scale", Vector2.One * 1.35f, Duration * 0.45f)
                .From(Vector2.One * 0.75f)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Cubic);
            tween.TweenProperty(glow, "modulate:a", 0f, Duration * 0.38f)
                .SetDelay(Duration * 0.52f);
        }
    }

    private static void AnimateArcs(Node2D root, Tween tween)
    {
        foreach (var arc in root.GetChildren().OfType<Line2D>().Where(node => node.Name == "ExchangeArc"))
        {
            tween.TweenProperty(arc, "modulate:a", 0f, Duration * 0.34f)
                .SetDelay(Duration * 0.58f);
        }
    }

    private static void AnimateGhosts(Node2D root, Tween tween, string nodeName, float direction)
    {
        var ghosts = root.GetChildren().OfType<Polygon2D>().Where(node => node.Name == nodeName).ToList();
        for (var i = 0; i < ghosts.Count; i++)
        {
            var ghost = ghosts[i];
            var delay = i * 0.055f;
            var start = ghost.Position;
            var end = -start;
            var control = ArcControlPoint(start, end, direction);
            tween.TweenMethod(
                    Callable.From<float>(value => ghost.Position = Quadratic(start, control, end, value)),
                    0f,
                    1f,
                    Duration * 0.68f)
                .SetDelay(delay)
                .SetEase(Tween.EaseType.InOut)
                .SetTrans(Tween.TransitionType.Cubic);
            tween.TweenProperty(ghost, "rotation", DirectionAngle(start, end) + direction * 0.42f, Duration * 0.68f)
                .SetDelay(delay)
                .SetEase(Tween.EaseType.InOut)
                .SetTrans(Tween.TransitionType.Cubic);
            tween.TweenProperty(ghost, "modulate:a", 0f, Duration * 0.2f)
                .SetDelay(delay + Duration * 0.52f);
        }
    }

    private static void AnimateCenterSpark(Node2D root, Tween tween)
    {
        foreach (var spark in root.GetChildren().OfType<Line2D>().Where(node => node.Name == "CenterSpark"))
        {
            tween.TweenProperty(spark, "scale", Vector2.One * 1.55f, Duration * 0.4f)
                .From(Vector2.One * 0.2f)
                .SetDelay(Duration * 0.24f)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Cubic);
            tween.TweenProperty(spark, "modulate:a", 0f, Duration * 0.28f)
                .SetDelay(Duration * 0.5f);
        }
    }

    private static Vector2[] ArcPoints(Vector2 start, Vector2 end, float direction)
    {
        const int pointCount = 28;
        var points = new Vector2[pointCount];
        var control = ArcControlPoint(start, end, direction);
        for (var i = 0; i < pointCount; i++)
        {
            var t = i / (float)(pointCount - 1);
            points[i] = Quadratic(start, control, end, t);
        }

        return points;
    }

    private static Vector2 ArcControlPoint(Vector2 start, Vector2 end, float direction)
    {
        var midpoint = (start + end) * 0.5f;
        var distance = start.DistanceTo(end);
        return midpoint + Vector2.Up * direction * Mathf.Clamp(distance * 0.28f, 52f, 96f);
    }

    private static Vector2 Quadratic(Vector2 start, Vector2 control, Vector2 end, float t)
    {
        var inverse = 1f - t;
        return start * inverse * inverse + control * 2f * inverse * t + end * t * t;
    }

    private static Vector2[] EllipsePoints(float radiusX, float radiusY)
    {
        const int pointCount = 48;
        var points = new Vector2[pointCount];
        for (var i = 0; i < pointCount; i++)
        {
            var angle = Mathf.Tau * i / pointCount;
            points[i] = new Vector2(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY);
        }

        return points;
    }

    private static float DirectionAngle(Vector2 start, Vector2 end) =>
        (end - start).Angle();
}
