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
