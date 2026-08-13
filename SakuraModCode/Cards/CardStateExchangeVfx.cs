using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;

namespace SakuraMod.SakuraModCode.Cards;

internal static class CardStateExchangeVfx
{
    private const float Duration = 0.58f;
    private const int VfxZIndex = 3000;
    private static readonly Color CardColor = new(1f, 0.84f, 0.48f, 0.76f);
    private static readonly Color CostColor = new(1f, 0.94f, 0.64f, 0.96f);
    private static readonly Color TemporaryColor = new(0.58f, 0.92f, 1f, 0.88f);
    private static readonly Color CenterSparkColor = new(1f, 0.98f, 0.82f, 0.94f);
    private static readonly Vector2 GhostCardSize = new(38f, 54f);

    internal static void Play(CardModel first, CardModel second, bool firstIsTemporary, bool secondIsTemporary)
    {
        if (TestMode.IsOn
            || NCombatRoom.Instance is not { } room
            || NCard.FindOnTable(first) is not { } firstNode
            || NCard.FindOnTable(second) is not { } secondNode)
        {
            return;
        }

        var container = (Control?)room.Ui ?? room.CombatVfxContainer;
        if (container is null)
            return;

        var firstCenter = CenterOf(firstNode);
        var secondCenter = CenterOf(secondNode);
        if (firstCenter.DistanceSquaredTo(secondCenter) <= 1f)
            return;

        var midpoint = (firstCenter + secondCenter) * 0.5f;
        var root = new Node2D
        {
            Name = "SakuraCardStateExchangeVfx",
            ZIndex = VfxZIndex,
            ZAsRelative = false,
            GlobalPosition = midpoint
        };
        container.AddChildSafely(root);

        var firstPosition = firstCenter - midpoint;
        var secondPosition = secondCenter - midpoint;
        var firstGhost = BuildGhost(root, "FirstCardGhost", firstPosition, firstIsTemporary);
        var secondGhost = BuildGhost(root, "SecondCardGhost", secondPosition, secondIsTemporary);
        BuildCenterSpark(root);
        TaskHelper.RunSafely(Animate(root, firstGhost, secondGhost));
    }

    private static Vector2 CenterOf(NCard card)
    {
        var size = card.GetCurrentSize();
        var scaled = new Vector2(size.X * card.Scale.X, size.Y * card.Scale.Y);
        return card.GlobalPosition + scaled * 0.5f;
    }

    private static Node2D BuildGhost(Node2D root, string name, Vector2 position, bool isTemporary)
    {
        var ghost = new Node2D
        {
            Name = name,
            Position = position
        };
        root.AddChild(ghost);

        var half = GhostCardSize * 0.5f;
        var body = new Polygon2D
        {
            Name = "CardBody",
            Color = CardColor,
            Polygon =
            [
                new(-half.X, -half.Y),
                new(half.X, -half.Y),
                new(half.X, half.Y),
                new(-half.X, half.Y)
            ]
        };
        ghost.AddChild(body);

        var outline = new Line2D
        {
            Name = "CardOutline",
            Width = 2.4f,
            DefaultColor = isTemporary ? TemporaryColor : CostColor,
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
        ghost.AddChild(outline);

        var costCore = new Polygon2D
        {
            Name = "CostCore",
            Position = new Vector2(-half.X * 0.7f, -half.Y * 0.72f),
            Color = CostColor,
            Polygon = DiamondPoints(6.5f)
        };
        ghost.AddChild(costCore);

        if (isTemporary)
        {
            var temporaryHalo = new Line2D
            {
                Name = "TemporaryHalo",
                Width = 2f,
                DefaultColor = TemporaryColor,
                Closed = true,
                Antialiased = true,
                Points = EllipsePoints(half.X + 6f, half.Y + 7f)
            };
            ghost.AddChild(temporaryHalo);
        }

        return ghost;
    }

    private static void BuildCenterSpark(Node2D root)
    {
        for (var i = 0; i < 6; i++)
        {
            var angle = Mathf.Tau * i / 6f;
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            root.AddChild(new Line2D
            {
                Name = "CenterSpark",
                Width = i % 2 == 0 ? 2.3f : 1.5f,
                DefaultColor = CenterSparkColor,
                Antialiased = true,
                Points = [direction * 4f, direction * 19f]
            });
        }
    }

    private static async Task Animate(Node2D root, Node2D firstGhost, Node2D secondGhost)
    {
        if (!root.IsInsideTree())
            return;

        var firstStart = firstGhost.Position;
        var secondStart = secondGhost.Position;
        var separation = firstStart.DistanceTo(secondStart);
        var arcHeight = Mathf.Clamp(separation * 0.26f, 36f, 78f);
        var tween = root.CreateTween().SetParallel();

        AnimateGhost(tween, firstGhost, firstStart, secondStart, Vector2.Up * arcHeight, -0.34f);
        AnimateGhost(tween, secondGhost, secondStart, firstStart, Vector2.Down * arcHeight, 0.34f);
        foreach (var spark in root.GetChildren().OfType<Line2D>().Where(node => node.Name == "CenterSpark"))
        {
            tween.TweenProperty(spark, "scale", Vector2.One * 1.45f, Duration * 0.28f)
                .From(Vector2.One * 0.15f)
                .SetDelay(Duration * 0.28f)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Cubic);
            tween.TweenProperty(spark, "modulate:a", 0f, Duration * 0.22f)
                .SetDelay(Duration * 0.48f);
        }

        await root.ToSignal(tween, Tween.SignalName.Finished);
        root.QueueFreeSafely();
    }

    private static void AnimateGhost(
        Tween tween,
        Node2D ghost,
        Vector2 start,
        Vector2 end,
        Vector2 arcOffset,
        float rotation)
    {
        var control = (start + end) * 0.5f + arcOffset;
        tween.TweenMethod(
                Callable.From<float>(value => ghost.Position = Quadratic(start, control, end, value)),
                0f,
                1f,
                Duration * 0.82f)
            .SetEase(Tween.EaseType.InOut)
            .SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(ghost, "rotation", rotation, Duration * 0.42f)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Quad);
        tween.TweenProperty(ghost, "modulate:a", 0f, Duration * 0.2f)
            .SetDelay(Duration * 0.72f);
        tween.TweenProperty(ghost, "scale", Vector2.One * 1.1f, Duration * 0.3f)
            .From(Vector2.One * 0.84f)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Back);
    }

    private static Vector2 Quadratic(Vector2 start, Vector2 control, Vector2 end, float t)
    {
        var inverse = 1f - t;
        return start * inverse * inverse + control * 2f * inverse * t + end * t * t;
    }

    private static Vector2[] DiamondPoints(float radius) =>
    [
        new(0f, -radius),
        new(radius, 0f),
        new(0f, radius),
        new(-radius, 0f)
    ];

    private static Vector2[] EllipsePoints(float radiusX, float radiusY)
    {
        const int pointCount = 36;
        var points = new Vector2[pointCount];
        for (var i = 0; i < pointCount; i++)
        {
            var angle = Mathf.Tau * i / pointCount;
            points[i] = new Vector2(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY);
        }

        return points;
    }
}
