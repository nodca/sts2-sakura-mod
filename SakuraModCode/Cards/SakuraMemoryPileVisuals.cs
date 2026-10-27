using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;
using MegaCrit.Sts2.Core.TestSupport;

namespace SakuraMod.SakuraModCode.Cards;

internal static class SakuraMemoryVisualTiming
{
    public const float RippleDuration = 0.4f;
    public const float DevelopDuration = 0.45f;
    // Arrivals closer together than this share one ripple.
    public const float RippleMergeWindow = 0.15f;

    // Instant mode returns 0: the presentation is skipped.
    public static float TimeScale(FastModeType mode) =>
        mode switch
        {
            FastModeType.Instant => 0f,
            FastModeType.Fast => 0.55f,
            _ => 1f
        };

    public static bool ShouldMergeRipple(double now, double lastRippleAt) =>
        now - lastRippleAt < RippleMergeWindow;

    public static float CurrentTimeScale() =>
        TimeScale(SaveManager.Instance?.PrefsSave.FastMode ?? FastModeType.Normal);
}

/// <summary>
/// A pale ripple on the Memory button whenever a card finishes landing there. Cards keep the
/// native flight into Memory; this only listens to the pile's own CardAddFinished event and
/// draws an independent node at the button, so it never touches a card node.
/// </summary>
internal static class SakuraMemoryArrivalRipple
{
    private const int RippleZIndex = 3000;
    private static readonly ConditionalWeakTable<CardPile, object> Subscribed = new();
    private static double _lastRippleAt = double.NegativeInfinity;

    public static void EnsureSubscribed(CardPile pile, Player owner)
    {
        if (TestMode.IsOn || Subscribed.TryGetValue(pile, out _))
            return;

        Subscribed.Add(pile, owner);
        pile.CardAddFinished += () => Play(owner);
    }

    private static void Play(Player owner)
    {
        if (TestMode.IsOn
            || !LocalContext.IsMe(owner)
            || !SakuraModConfig.IsCardVfxEnabled()
            || NCombatRoom.Instance is not { Ui: { } ui } room
            || !PileExchangeVfx.TryGetPileCenter(room, SakuraMemoryPile.PileType, out var center))
        {
            return;
        }

        var timeScale = SakuraMemoryVisualTiming.CurrentTimeScale();
        var now = Godot.Time.GetTicksMsec() / 1000d;
        if (timeScale <= 0f || SakuraMemoryVisualTiming.ShouldMergeRipple(now, _lastRippleAt))
            return;
        _lastRippleAt = now;

        var ripple = new Line2D
        {
            Name = "SakuraMemoryRipple",
            TopLevel = true,
            ZIndex = RippleZIndex,
            ZAsRelative = false,
            Width = 4f,
            Closed = true,
            Antialiased = true,
            DefaultColor = PileExchangeVfx.MemoryColor,
            Points = RoundedRect(new Vector2(46f, 62f), 10f),
            Scale = Vector2.One * 0.7f
        };
        ui.AddChildSafely(ripple);
        ripple.GlobalPosition = center;

        var duration = SakuraMemoryVisualTiming.RippleDuration * timeScale;
        var tween = ripple.CreateTween().SetParallel();
        tween.TweenProperty(ripple, "scale", Vector2.One * 1.6f, duration)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(ripple, "modulate:a", 0f, duration)
            .SetEase(Tween.EaseType.In)
            .SetTrans(Tween.TransitionType.Quad);
        tween.Chain().TweenCallback(Callable.From(ripple.QueueFreeSafely));
    }

    private static Vector2[] RoundedRect(Vector2 size, float radius)
    {
        var half = size * 0.5f;
        (Vector2 Corner, float Start)[] corners =
        [
            (new Vector2(half.X - radius, -half.Y + radius), -Mathf.Pi / 2f),
            (new Vector2(half.X - radius, half.Y - radius), 0f),
            (new Vector2(-half.X + radius, half.Y - radius), Mathf.Pi / 2f),
            (new Vector2(-half.X + radius, -half.Y + radius), Mathf.Pi)
        ];
        const int steps = 5;
        var points = new List<Vector2>();
        foreach (var (corner, start) in corners)
        {
            for (var i = 0; i <= steps; i++)
            {
                var angle = start + Mathf.Pi / 2f * i / steps;
                points.Add(corner + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }
        return [.. points];
    }
}

/// <summary>
/// Recall: cards leaving Memory for the hand appear at the Memory button and develop out of the
/// blank glass card while native code carries them into the hand, like a draw. The gameplay path
/// (native CardPileCmd.Add and its hooks) is unchanged; only the new card node's start is moved.
/// </summary>
internal static class SakuraMemoryRecallVisual
{
    private static readonly HashSet<CardModel> Pending = new(ReferenceEqualityComparer.Instance);

    public static void Mark(CardModel card) => Pending.Add(card);

    public static void Unmark(CardModel card) => Pending.Remove(card);

    public static void AfterNativeCardNodeCreated(CardModel card, NCard? node, bool owningPlayerIsLocal)
    {
        if (!Pending.Remove(card)
            || node is null
            || !owningPlayerIsLocal
            || TestMode.IsOn
            || !SakuraModConfig.IsCardVfxEnabled()
            || !GodotObject.IsInstanceValid(node))
        {
            return;
        }

        var timeScale = SakuraMemoryVisualTiming.CurrentTimeScale();
        if (timeScale <= 0f)
            return;

        node.Position = SakuraMemoryPile.PileType.GetTargetPosition(node);
        SakuraForgottenOverlay.PlayDevelop(node, SakuraMemoryVisualTiming.DevelopDuration * timeScale);
    }
}
