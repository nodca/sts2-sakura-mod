using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;
using SakuraMod.SakuraModCode.FourthAct.Wind.CardState;

namespace SakuraMod.SakuraModCode.FourthAct.Wind.Visuals;

internal static class SleepingCardVisuals
{
    internal const string OverlayNodeName = "SleepingAffliction";
    internal const float WakeDuration = 0.25f;
    private const int VfxZIndex = 3001;
    private static readonly Color SilverBlue = new(0.66f, 0.75f, 0.9f, 0.92f);
    private static readonly Color VeilColor = new(0.12f, 0.08f, 0.2f, 0.62f);
    private static readonly Color StarColor = new(0.78f, 0.65f, 0.32f, 0.86f);

    public static void PlayRejected(CardModel card)
    {
        if (TestMode.IsOn || NCard.FindOnTable(card) is not { } cardNode)
            return;
        if (cardNode.FindChild(OverlayNodeName, recursive: true, owned: false) is not Control overlay
            || !overlay.IsInsideTree())
        {
            return;
        }

        var tween = overlay.CreateTween();
        tween.TweenProperty(overlay, "position:y", 5f, 0.08f).AsRelative()
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Quad);
        tween.Parallel().TweenProperty(overlay, "self_modulate", new Color(0.76f, 0.72f, 0.94f, 1f), 0.08f);
        tween.TweenProperty(overlay, "position:y", -5f, 0.12f).AsRelative()
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Back);
        tween.Parallel().TweenProperty(overlay, "self_modulate", Colors.White, 0.12f);
    }

    public static void PlayWake(CardModel card)
    {
        if (TestMode.IsOn
            || NCombatRoom.Instance?.Ui is not { } ui
            || NCard.FindOnTable(card) is not { } cardNode)
        {
            return;
        }

        var size = cardNode.GetCurrentSize() * cardNode.Scale;
        var root = new Node2D
        {
            Name = "SakuraSleepingWakeVfx",
            ZIndex = VfxZIndex,
            ZAsRelative = false,
            GlobalPosition = cardNode.GlobalPosition + size * new Vector2(0.5f, 0.36f)
        };
        ui.AddChildSafely(root);

        var wash = new Polygon2D
        {
            Color = VeilColor,
            Polygon =
            [
                new(-size.X * 0.42f, -size.Y * 0.16f),
                new(size.X * 0.42f, -size.Y * 0.16f),
                new(size.X * 0.35f, size.Y * 0.2f),
                new(-size.X * 0.35f, size.Y * 0.2f)
            ]
        };
        root.AddChild(wash);

        var leftEye = CreateEyelid(size.X * -0.17f, size.X * 0.15f, -0.08f);
        var rightEye = CreateEyelid(size.X * 0.17f, size.X * 0.15f, 0.08f);
        root.AddChild(leftEye);
        root.AddChild(rightEye);
        for (var index = 0; index < 3; index++)
        {
            var star = new Polygon2D
            {
                Position = new Vector2((index - 1) * size.X * 0.18f, size.Y * (0.09f + index * 0.025f)),
                Color = StarColor,
                Polygon = [new(0f, -5f), new(2f, -1f), new(5f, 0f), new(2f, 1f), new(0f, 5f), new(-2f, 1f), new(-5f, 0f), new(-2f, -1f)]
            };
            root.AddChild(star);
        }

        var tween = root.CreateTween().SetParallel();
        tween.TweenProperty(leftEye, "rotation", leftEye.Rotation - 0.18f, WakeDuration)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(rightEye, "rotation", rightEye.Rotation + 0.18f, WakeDuration)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(root, "position:y", root.Position.Y - 20f, WakeDuration)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(root, "modulate:a", 0f, WakeDuration)
            .SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
        tween.Chain().TweenCallback(Callable.From(root.QueueFreeSafely));
    }

    private static Line2D CreateEyelid(float x, float halfWidth, float rotation) => new()
    {
        Position = new Vector2(x, 0f),
        Rotation = rotation,
        Width = 4f,
        DefaultColor = SilverBlue,
        Antialiased = true,
        Points = [new(-halfWidth, 0f), new(0f, halfWidth * 0.16f), new(halfWidth, 0f)]
    };
}

[HarmonyPatch(typeof(NMouseCardPlay), nameof(NMouseCardPlay.Start))]
internal static class SleepingMouseCardPlayPatch
{
    [HarmonyPrefix]
    private static void PlayRejectedCue(NMouseCardPlay __instance)
    {
        if (__instance.Holder?.CardModel is { Affliction: SleepingAffliction } card)
            SleepingCardVisuals.PlayRejected(card);
    }
}

[HarmonyPatch(typeof(NControllerCardPlay), nameof(NControllerCardPlay.Start))]
internal static class SleepingControllerCardPlayPatch
{
    [HarmonyPrefix]
    private static void PlayRejectedCue(NControllerCardPlay __instance)
    {
        if (__instance.Holder?.CardModel is { Affliction: SleepingAffliction } card)
            SleepingCardVisuals.PlayRejected(card);
    }
}
