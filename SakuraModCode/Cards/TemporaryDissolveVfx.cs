using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;

namespace SakuraMod.SakuraModCode.Cards;

public static class TemporaryDissolveVfx
{
    private const float CardDuration = 1.2f;
    private const float PileDuration = 1.0f;
    private const int VfxZIndex = 3000;
    private static readonly Color EdgeColor = new(0.86f, 1f, 0.97f, 0.96f);
    private static readonly Color GoldColor = new(1f, 0.92f, 0.48f, 0.92f);
    private static readonly Color ShardColor = new(0.74f, 0.97f, 1f, 0.9f);
    private static readonly Vector2 PileVfxSize = new(116f, 162f);
    private static readonly Vector2[] ShardDirections =
    [
        new(-0.95f, -0.55f),
        new(-0.58f, -0.95f),
        new(-0.18f, -1.12f),
        new(0.36f, -1.02f),
        new(0.88f, -0.66f),
        new(1.05f, -0.12f),
        new(-1.08f, -0.08f),
        new(0.64f, 0.36f),
        new(-0.45f, -1.25f),
        new(0.15f, -1.35f),
        new(0.75f, -1.15f),
        new(-0.85f, -0.85f)
    ];

    public static void Play(CardModel card)
    {
        if (TestMode.IsOn || NCombatRoom.Instance is not { } room)
            return;

        var container = (Control?)room.Ui ?? room.CombatVfxContainer;
        if (container is null)
            return;

        var visibleCard = NCard.FindOnTable(card);
        var center = visibleCard is not null
            ? CenterOf(visibleCard)
            : PileAnchorPosition(card);
        var root = new Node2D
        {
            Name = "SakuraTemporaryDissolveVfx",
            ZIndex = VfxZIndex,
            ZAsRelative = false
        };
        container.AddChildSafely(root);
        root.GlobalPosition = center;

        var cardNode = visibleCard is not null
            ? AttachVisibleCard(root, card, visibleCard)
            : null;
        var visualSize = cardNode is not null
            ? ScaledSize(cardNode)
            : PileVfxSize;

        BuildOutline(root, visualSize);
        BuildShards(root, visualSize);
        TaskHelper.RunSafely(Animate(root, cardNode));
    }

    private static Vector2 CenterOf(NCard card)
    {
        var size = card.GetCurrentSize();
        var scaled = new Vector2(size.X * card.Scale.X, size.Y * card.Scale.Y);
        return card.GlobalPosition + scaled * 0.5f;
    }

    private static Vector2 PileAnchorPosition(CardModel card)
    {
        var ui = NCombatRoom.Instance?.Ui;
        Control? anchor = card.Pile?.Type switch
        {
            PileType.Draw => ui?.DrawPile,
            PileType.Discard => ui?.DiscardPile,
            PileType.Exhaust => ui?.ExhaustPile,
            PileType.Hand => ui?.Hand,
            PileType.Play => ui?.PlayContainer,
            _ => null
        };

        return anchor is not null
            ? anchor.GlobalPosition + new Vector2(anchor.Size.X * anchor.Scale.X, anchor.Size.Y * anchor.Scale.Y) * 0.5f
            : NCombatRoom.Instance?.CombatVfxContainer.GetViewportRect().GetCenter() ?? Vector2.Zero;
    }

    private static NCard AttachVisibleCard(Node2D root, CardModel card, NCard cardNode)
    {
        var globalPosition = cardNode.GlobalPosition;
        var rotation = cardNode.Rotation;
        var scale = cardNode.Scale;
        var ui = NCombatRoom.Instance?.Ui;
        if (ui is null)
        {
            cardNode.GetParent()?.RemoveChildSafely(cardNode);
        }
        else
        {
            var playContainer = ui.PlayContainer;
            var playQueue = ui.PlayQueue;

            if (playQueue.IsAncestorOf(cardNode))
                playQueue.RemoveCardFromQueueForCancellation(cardNode);

            if (card.Pile?.Type == PileType.Hand && !NodeUtil.IsDescendant(playContainer, cardNode))
                ui.Hand.Remove(card);
            else
                cardNode.GetParent()?.RemoveChildSafely(cardNode);
        }

        root.AddChildSafely(cardNode);
        cardNode.GlobalPosition = globalPosition;
        cardNode.Rotation = rotation;
        cardNode.Scale = scale;
        cardNode.MouseFilter = Control.MouseFilterEnum.Ignore;
        return cardNode;
    }

    private static Vector2 ScaledSize(NCard card)
    {
        var size = card.GetCurrentSize();
        return new Vector2(size.X * card.Scale.X, size.Y * card.Scale.Y);
    }

    private static void BuildOutline(Node2D root, Vector2 visualSize)
    {
        var half = visualSize * 0.5f;
        var outline = new Line2D
        {
            Width = 5.5f,
            DefaultColor = EdgeColor,
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
        root.AddChild(outline);

        var inner = new Line2D
        {
            Width = 2.4f,
            DefaultColor = GoldColor,
            Closed = true,
            Antialiased = true,
            Points =
            [
                new(-half.X * 0.82f, -half.Y * 0.82f),
                new(half.X * 0.82f, -half.Y * 0.82f),
                new(half.X * 0.82f, half.Y * 0.82f),
                new(-half.X * 0.82f, half.Y * 0.82f)
            ]
        };
        root.AddChild(inner);
    }

    private static void BuildShards(Node2D root, Vector2 visualSize)
    {
        var radius = Math.Min(visualSize.X, visualSize.Y) * 0.34f;
        for (var i = 0; i < ShardDirections.Length; i++)
        {
            var shard = CreateShard(i);
            shard.Position = ShardDirections[i] * radius * 0.32f;
            shard.Rotation = i * 0.43f;
            root.AddChild(shard);
        }
    }

    private static Polygon2D CreateShard(int index)
    {
        var width = 10f + index % 3 * 3f + (index % 2 == 0 ? 4f : 0f);
        var height = 18f + index % 4 * 3f + (index % 3 == 0 ? 5f : 0f);
        var color = index % 4 == 0 ? GoldColor : ShardColor;
        return new Polygon2D
        {
            Color = color,
            Polygon =
            [
                new(0f, -height * 0.5f),
                new(width * 0.52f, -height * 0.08f),
                new(width * 0.22f, height * 0.48f),
                new(-width * 0.46f, height * 0.18f)
            ]
        };
    }

    private static async Task Animate(Node2D root, NCard? cardNode)
    {
        if (!root.IsInsideTree())
            return;

        var duration = cardNode is null ? PileDuration : CardDuration;
        var tween = root.CreateTween().SetParallel();
        tween.TweenProperty(root, "scale", Vector2.One * 1.18f, duration * 0.62f)
            .From(Vector2.One * 0.84f)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(root, "modulate:a", 0f, duration * 0.46f)
            .SetDelay(duration * 0.54f)
            .SetEase(Tween.EaseType.In)
            .SetTrans(Tween.TransitionType.Quad);

        if (cardNode is not null)
        {
            tween.TweenProperty(cardNode, "position", cardNode.Position + Vector2.Up * 42f, duration)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Cubic);
            tween.TweenProperty(cardNode, "scale", cardNode.Scale * 0.9f, duration)
                .SetEase(Tween.EaseType.InOut)
                .SetTrans(Tween.TransitionType.Cubic);
            tween.TweenProperty(cardNode, "modulate:a", 0f, duration * 0.58f)
                .SetDelay(duration * 0.34f)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Quad);
        }

        AnimateShards(root, tween, duration);
        await root.ToSignal(tween, Tween.SignalName.Finished);
        cardNode?.QueueFreeSafely();
        root.QueueFreeSafely();
    }

    private static void AnimateShards(Node2D root, Tween tween, float duration)
    {
        var children = root.GetChildren().OfType<Polygon2D>().ToList();
        for (var i = 0; i < children.Count; i++)
        {
            var shard = children[i];
            var distance = 68f + i % 3 * 22f; // Increased explosion radius
            var end = shard.Position + ShardDirections[i] * distance + Vector2.Up * 42f;
            tween.TweenProperty(shard, "position", end, duration)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Cubic);
            tween.TweenProperty(shard, "rotation", shard.Rotation + 1.8f + i * 0.32f, duration) // More spin
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Cubic);
            tween.TweenProperty(shard, "scale", Vector2.Zero, duration * 0.5f) // Shrink over time
                .SetDelay(duration * 0.5f)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Quad);
            tween.TweenProperty(shard, "modulate:a", 0f, duration * 0.48f)
                .SetDelay(duration * 0.48f);
        }
    }
}

public static class ReleaseVfx
{
    private const float Duration = 1.1f;
    private const int VfxZIndex = 3000;
    private static readonly Color FrameGlowColor = new(1f, 0.82f, 0.72f, 0.9f);
    private static readonly Color SealGoldColor = new(1f, 0.88f, 0.58f, 0.82f);
    private static readonly Color SealPinkColor = new(1f, 0.72f, 0.78f, 0.64f);
    private static readonly Color SealBlueColor = new(0.82f, 0.96f, 1f, 0.6f);
    private static readonly Color SparkColor = new(1f, 0.9f, 0.62f, 0.9f);
    private static readonly Vector2 PileVfxSize = new(116f, 162f);
    private static readonly Vector2[] SparkDirections =
    [
        new(-0.72f, -0.8f),
        new(-0.24f, -1.08f),
        new(0.42f, -0.96f),
        new(0.78f, -0.46f),
        new(-0.84f, -0.18f),
        new(0.18f, 0.64f),
        new(-0.55f, 0.85f),
        new(0.85f, 0.25f)
    ];

    public static void Play(CardModel card)
    {
        if (TestMode.IsOn || NCombatRoom.Instance is not { } room)
            return;

        var container = (Control?)room.Ui ?? room.CombatVfxContainer;
        if (container is null)
            return;

        var visibleCard = NCard.FindOnTable(card);
        var visualSize = visibleCard is not null
            ? ScaledSize(visibleCard)
            : PileVfxSize;
        var root = new Node2D
        {
            Name = "SakuraReleaseVfx",
            ZIndex = VfxZIndex,
            ZAsRelative = false
        };
        container.AddChildSafely(root);
        root.GlobalPosition = visibleCard is not null
            ? CenterOf(visibleCard)
            : PileAnchorPosition(card);

        BuildFrameGlow(root, visualSize);
        BuildSealRings(root, visualSize);
        BuildStarburst(root, visualSize);
        BuildSparks(root, visualSize);
        TaskHelper.RunSafely(Animate(root, visibleCard));
    }

    private static Vector2 CenterOf(NCard card)
    {
        var size = card.GetCurrentSize();
        var scaled = new Vector2(size.X * card.Scale.X, size.Y * card.Scale.Y);
        return card.GlobalPosition + scaled * 0.5f;
    }

    private static Vector2 PileAnchorPosition(CardModel card)
    {
        var ui = NCombatRoom.Instance?.Ui;
        Control? anchor = card.Pile?.Type switch
        {
            PileType.Draw => ui?.DrawPile,
            PileType.Discard => ui?.DiscardPile,
            PileType.Exhaust => ui?.ExhaustPile,
            PileType.Hand => ui?.Hand,
            PileType.Play => ui?.PlayContainer,
            _ => null
        };

        return anchor is not null
            ? anchor.GlobalPosition + new Vector2(anchor.Size.X * anchor.Scale.X, anchor.Size.Y * anchor.Scale.Y) * 0.5f
            : NCombatRoom.Instance?.CombatVfxContainer.GetViewportRect().GetCenter() ?? Vector2.Zero;
    }

    private static Vector2 ScaledSize(NCard card)
    {
        var size = card.GetCurrentSize();
        return new Vector2(size.X * card.Scale.X, size.Y * card.Scale.Y);
    }

    private static void BuildFrameGlow(Node2D root, Vector2 visualSize)
    {
        var half = visualSize * 0.5f;
        var inset = new Vector2(visualSize.X * 0.08f, visualSize.Y * 0.06f);
        var glow = new Line2D
        {
            Width = 5f,
            DefaultColor = FrameGlowColor,
            Closed = true,
            Antialiased = true,
            Points =
            [
                new(-half.X + inset.X, -half.Y + inset.Y),
                new(half.X - inset.X, -half.Y + inset.Y),
                new(half.X - inset.X, half.Y - inset.Y),
                new(-half.X + inset.X, half.Y - inset.Y)
            ]
        };
        root.AddChild(glow);
    }

    private static void BuildSealRings(Node2D root, Vector2 visualSize)
    {
        AddEllipse(root, visualSize.X * 0.34f, visualSize.Y * 0.44f, SealGoldColor, 3.2f, 0f);
        AddEllipse(root, visualSize.X * 0.25f, visualSize.Y * 0.33f, SealBlueColor, 2.2f, 0.18f);
        AddEllipse(root, visualSize.X * 0.43f, visualSize.Y * 0.25f, SealPinkColor, 2f, -0.14f);
    }

    private static void AddEllipse(Node2D root, float radiusX, float radiusY, Color color, float width, float rotation)
    {
        var ring = new Line2D
        {
            Name = "SealRing",
            Width = width,
            DefaultColor = color,
            Closed = true,
            Antialiased = true,
            Points = EllipsePoints(radiusX, radiusY, rotation)
        };
        root.AddChild(ring);
    }

    private static Vector2[] EllipsePoints(float radiusX, float radiusY, float rotation)
    {
        const int pointCount = 72;
        var points = new Vector2[pointCount];
        var sin = MathF.Sin(rotation);
        var cos = MathF.Cos(rotation);

        for (var i = 0; i < pointCount; i++)
        {
            var angle = Mathf.Tau * i / pointCount;
            var point = new Vector2(MathF.Cos(angle) * radiusX, MathF.Sin(angle) * radiusY);
            points[i] = new Vector2(
                point.X * cos - point.Y * sin,
                point.X * sin + point.Y * cos);
        }

        return points;
    }

    private static void BuildStarburst(Node2D root, Vector2 visualSize)
    {
        var container = new Node2D { Name = "StarburstContainer" };
        root.AddChild(container);
        var minSize = Math.Min(visualSize.X, visualSize.Y);
        for (var i = 0; i < 12; i++)
        {
            var angle = -Mathf.Pi * 0.5f + Mathf.Tau * i / 12f;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var start = direction * minSize * 0.05f;
            var end = direction * minSize * (i % 2 == 0 ? 0.46f : 0.32f);
            var line = new Line2D
            {
                Width = i % 2 == 0 ? 2.6f : 1.8f,
                DefaultColor = i % 3 == 0 ? SealGoldColor : SealBlueColor,
                Antialiased = true,
                Points = [start, end]
            };
            container.AddChild(line);
        }
    }

    private static void BuildSparks(Node2D root, Vector2 visualSize)
    {
        var radius = Math.Min(visualSize.X, visualSize.Y) * 0.22f;
        for (var i = 0; i < SparkDirections.Length; i++)
        {
            var spark = CreateSpark(i);
            spark.Position = SparkDirections[i] * radius;
            spark.Rotation = i * 0.36f;
            root.AddChild(spark);
        }
    }

    private static Polygon2D CreateSpark(int index)
    {
        var size = 8f + index % 3 * 1.5f;
        return new Polygon2D
        {
            Color = SparkColor,
            Polygon =
            [
                new(0f, -size),
                new(size * 0.36f, -size * 0.2f),
                new(size, 0f),
                new(size * 0.36f, size * 0.2f),
                new(0f, size),
                new(-size * 0.36f, size * 0.2f),
                new(-size, 0f),
                new(-size * 0.36f, -size * 0.2f)
            ]
        };
    }

    private static async Task Animate(Node2D root, NCard? visibleCard)
    {
        if (!root.IsInsideTree())
            return;

        var tween = root.CreateTween().SetParallel();
        tween.TweenProperty(root, "scale", Vector2.One * 1.2f, Duration * 0.7f)
            .From(Vector2.One * 0.78f)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(root, "modulate:a", 0f, Duration * 0.42f)
            .SetDelay(Duration * 0.58f)
            .SetEase(Tween.EaseType.In)
            .SetTrans(Tween.TransitionType.Quad);

        AnimateSparks(root, tween);
        AnimateSealRings(root, tween);
        AnimateCardPulse(root, visibleCard);

        await root.ToSignal(tween, Tween.SignalName.Finished);
        root.QueueFreeSafely();
    }

    private static void AnimateSparks(Node2D root, Tween tween)
    {
        var sparks = root.GetChildren().OfType<Polygon2D>().ToList();
        for (var i = 0; i < sparks.Count; i++)
        {
            var spark = sparks[i];
            var end = spark.Position + SparkDirections[i] * (54f + i % 2 * 18f) + Vector2.Up * 28f;
            tween.TweenProperty(spark, "position", end, Duration)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Cubic);
            tween.TweenProperty(spark, "rotation", spark.Rotation + Mathf.Pi * (i % 2 == 0 ? 1.5f : -1.5f), Duration)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Cubic);
            tween.TweenProperty(spark, "scale", Vector2.Zero, Duration * 0.5f)
                .SetDelay(Duration * 0.5f)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Quad);
            tween.TweenProperty(spark, "modulate:a", 0f, Duration * 0.42f)
                .SetDelay(Duration * 0.5f);
        }
    }

    private static void AnimateSealRings(Node2D root, Tween tween)
    {
        var rings = root.GetChildren().OfType<Line2D>().Where(l => l.Name == "SealRing").ToList();
        for (var i = 0; i < rings.Count; i++)
        {
            var ring = rings[i];
            var direction = i % 2 == 0 ? 1f : -1f;
            tween.TweenProperty(ring, "rotation", ring.Rotation + direction * Mathf.Pi * 1.5f, Duration)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Cubic);
            // Add a slight scale pulse to the rings
            tween.TweenProperty(ring, "scale", Vector2.One * 1.15f, Duration * 0.5f)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Sine);
            tween.TweenProperty(ring, "scale", Vector2.One * 1.05f, Duration * 0.5f)
                .SetDelay(Duration * 0.5f)
                .SetEase(Tween.EaseType.InOut)
                .SetTrans(Tween.TransitionType.Sine);
        }

        if (root.GetNodeOrNull<Node2D>("StarburstContainer") is { } starburst)
        {
            tween.TweenProperty(starburst, "rotation", Mathf.Pi * 0.75f, Duration)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Cubic);
            tween.TweenProperty(starburst, "scale", Vector2.One * 1.5f, Duration)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Cubic);
        }
    }

    private static void AnimateCardPulse(Node2D root, NCard? visibleCard)
    {
        if (visibleCard is null || visibleCard.IsQueuedForDeletion())
            return;

        var originalScale = visibleCard.Scale;
        var cardTween = root.CreateTween();
        cardTween.TweenProperty(visibleCard, "scale", originalScale * 1.065f, Duration * 0.36f)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
        cardTween.TweenProperty(visibleCard, "scale", originalScale, Duration * 0.44f)
            .SetEase(Tween.EaseType.InOut)
            .SetTrans(Tween.TransitionType.Cubic);
    }
}
