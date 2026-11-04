using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
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
/// Recall: a card leaving Memory for the hand waits hidden in its new hand slot while a relay
/// thread runs from the Memory button to that slot; where the thread lands, the card develops
/// in place out of the blank glass card. Nothing flies. The gameplay path (native
/// CardPileCmd.Add and its hooks) is unchanged; only the new node's start, its modulate and its
/// holder's settle are presentation state. Without the relay (shader missing, no room) the card
/// falls back to starting at the Memory button and developing in flight.
/// </summary>
internal static class SakuraMemoryRecallVisual
{
    internal const string RelayShaderPath = MainFile.ResPath + "/shaders/card_vfx/memory_relay.gdshader";
    internal static IReadOnlyList<string> AssetPaths { get; } = [RelayShaderPath];

    // Keep in step with scripts/render_remind_vfx.gd.
    internal const float RelayLead = 1f / 12f;
    internal const float CardStagger = 0.07f;
    internal const float CardStaggerCap = 0.5f;
    internal const float RelayReach = 0.16f;
    internal const float RelayTail = 0.12f;
    internal const float LandRipple = 0.24f;
    // The in-place develop is the whole read of the recall, so it runs longer than the
    // in-flight develop and ignores Fast mode; it never holds gameplay back.
    internal const float InPlaceDevelop = 0.75f;
    private const float RelayWidth = 10f;
    private const float RippleSize = 220f;
    // The card waits this long for native code to give it a hand holder.
    private const float HolderWait = 0.5f;
    private const int RelayZIndex = 3000;
    private static readonly Color DevelopStart = new(0.62f, 0.93f, 1f, 0.25f);

    private static readonly Dictionary<CardModel, (int Index, int Count)> Pending =
        new(ReferenceEqualityComparer.Instance);
    private static ImageTexture? _lineTexture;

    public static void Mark(CardModel card, int index, int count) => Pending[card] = (index, count);

    public static void Unmark(CardModel card) => Pending.Remove(card);

    /// <summary>When the <paramref name="index"/>-th of <paramref name="count"/> relay threads leaves the button.</summary>
    internal static float RelayDelay(int index, int count) =>
        RelayLead + Math.Min(CardStagger, CardStaggerCap / Math.Max(count - 1, 1)) * index;

    public static void AfterNativeCardNodeCreated(
        CardModel card,
        NCard? node,
        PileType targetPileType,
        bool owningPlayerIsLocal)
    {
        if (!Pending.Remove(card, out var order)
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

        if (targetPileType != PileType.Hand
            || NCombatRoom.Instance is not { Ui: { } ui } room
            || !TryLoadRelayShader(out var shader))
        {
            node.Position = SakuraMemoryPile.PileType.GetTargetPosition(node);
            SakuraForgottenOverlay.PlayDevelop(node, SakuraMemoryVisualTiming.DevelopDuration * timeScale);
            return;
        }

        node.Position = PileType.Hand.GetTargetPosition(node);
        node.Modulate = Colors.Transparent;
        TaskHelper.RunSafely(PlayRelay(room, ui, node, shader, RelayDelay(order.Index, order.Count), timeScale));
    }

    private static bool TryLoadRelayShader(out Shader shader)
    {
        try
        {
            shader = PreloadManager.Cache.GetAsset<Shader>(RelayShaderPath);
            return shader is not null;
        }
        catch (Exception exception)
        {
            MainFile.Logger.Error($"Could not load the Memory relay shader: {exception}");
            shader = null!;
            return false;
        }
    }

    private static async Task PlayRelay(
        NCombatRoom room,
        Control ui,
        NCard node,
        Shader shader,
        float delay,
        float timeScale)
    {
        var revealed = false;
        Line2D? line = null;
        ColorRect? ripple = null;
        try
        {
            // Native code gives the node its hand holder when the pile tween starts.
            var waited = 0f;
            NHandCardHolder? holder;
            while (true)
            {
                if (!GodotObject.IsInstanceValid(node) || waited > delay * timeScale + HolderWait)
                    return;
                if (waited >= delay * timeScale && (holder = HolderOf(node)) is not null)
                    break;
                waited += await ui.AwaitProcessFrame();
            }

            if (!PileExchangeVfx.TryGetPileCenter(room, SakuraMemoryPile.PileType, out var pile))
                return;

            line = CreateLine(ui, shader, node.Model?.Id.GetHashCode() ?? 0);
            var lineMaterial = (ShaderMaterial)line.Material;
            var reach = RelayReach * timeScale;
            var elapsed = 0f;
            while (elapsed < reach)
            {
                if (!Settle(node, holder))
                    return;
                line.Points = RelayPoints(pile, LandingPoint(node));
                SetThread(lineMaterial, Ease(elapsed / reach), 0f, waited + elapsed);
                elapsed += await ui.AwaitProcessFrame();
            }

            if (!Settle(node, holder))
                return;
            var landing = LandingPoint(node);
            line.Points = RelayPoints(pile, landing);
            Reveal(node);
            revealed = true;

            ripple = CreateRipple(ui, shader, CardCentre(node));
            var rippleMaterial = (ShaderMaterial)ripple.Material;
            var after = 0f;
            var tail = RelayTail * timeScale;
            var rippleDuration = LandRipple * timeScale;
            while (after < Math.Max(tail, rippleDuration))
            {
                SetThread(lineMaterial, 1f, EaseIn(after / tail), waited + elapsed + after);
                rippleMaterial.SetShaderParameter("ripple", Math.Clamp(after / rippleDuration, 0f, 1f));
                after += await ui.AwaitProcessFrame();
            }
        }
        catch (OperationCanceledException)
        {
            // The room left the tree; the nodes go with it.
        }
        finally
        {
            if (!revealed && GodotObject.IsInstanceValid(node))
                node.Modulate = Colors.White;
            FreeSafely(line);
            FreeSafely(ripple);
        }
    }

    private static NHandCardHolder? HolderOf(NCard node) =>
        GodotObject.IsInstanceValid(node) ? node.GetParent() as NHandCardHolder : null;

    // The card is hidden, so its holder skips the native settle and sits in its slot; a card
    // that left the hand (played, discarded) stops the relay and is shown at once.
    private static bool Settle(NCard node, NHandCardHolder holder)
    {
        if (!GodotObject.IsInstanceValid(holder) || HolderOf(node) != holder)
            return false;
        holder.Position = holder.TargetPosition;
        return true;
    }

    private static void Reveal(NCard node)
    {
        // Same curve as the overlay's glass so the colour and the face clear together.
        node.Modulate = DevelopStart;
        node.CreateTween()
            .TweenProperty(node, "modulate", Colors.White, InPlaceDevelop)
            .SetEase(Tween.EaseType.InOut)
            .SetTrans(Tween.TransitionType.Sine);
        SakuraForgottenOverlay.PlayDevelop(node, InPlaceDevelop);
    }

    private static Vector2 CardCentre(NCard node) =>
        node.GetGlobalTransform() * (node.GetCurrentSize() * 0.5f);

    // The thread ends above the card centre, where the card develops over it.
    private static Vector2 LandingPoint(NCard node) =>
        CardCentre(node) + new Vector2(0f, -node.GetCurrentSize().Y * node.GetGlobalTransform().Scale.Y * 0.12f);

    /// <summary>
    /// From the Memory button straight down past the last enemy's health bar, then left above
    /// the End Turn button into the slot.
    /// </summary>
    internal static Vector2[] RelayPoints(Vector2 pile, Vector2 landing)
    {
        var b = pile + new Vector2(-30f, 190f);
        var c = landing + new Vector2(320f, -70f);
        var points = new Vector2[33];
        for (var k = 0; k < points.Length; k++)
            points[k] = Bezier(pile, b, c, landing, k / 32f);
        return points;
    }

    internal static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
    {
        var s = 1f - t;
        return s * s * s * a + 3f * s * s * t * b + 3f * s * t * t * c + t * t * t * d;
    }

    private static void SetThread(ShaderMaterial material, float head, float tail, float clock)
    {
        material.SetShaderParameter("head", head);
        material.SetShaderParameter("tail", tail);
        material.SetShaderParameter("step_time", Mathf.Floor(clock * 12f) / 12f);
    }

    internal static Line2D CreateThreadLine(Shader shader, float width, float seed)
    {
        _lineTexture ??= ImageTexture.CreateFromImage(Image.CreateEmpty(4, 4, false, Image.Format.Rgba8));
        var material = new ShaderMaterial { Shader = shader };
        material.SetShaderParameter("mode", 0f);
        material.SetShaderParameter("seed", seed);
        material.SetShaderParameter("head", 0f);
        material.SetShaderParameter("tail", 0f);
        material.SetShaderParameter("step_time", 0f);
        material.SetShaderParameter("opacity", 1f);
        return new Line2D
        {
            Width = width,
            Texture = _lineTexture,
            TextureMode = Line2D.LineTextureMode.Stretch,
            JointMode = Line2D.LineJointMode.Round,
            Material = material
        };
    }

    internal static ColorRect CreateSocket(Shader shader, Vector2 size, Vector3 tint)
    {
        var material = new ShaderMaterial { Shader = shader };
        material.SetShaderParameter("mode", 1f);
        material.SetShaderParameter("region_size", size);
        material.SetShaderParameter("converge", 0f);
        material.SetShaderParameter("ripple", 0f);
        material.SetShaderParameter("socket_tint", tint);
        material.SetShaderParameter("opacity", 1f);
        return new ColorRect
        {
            Color = Colors.White,
            Size = size,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Material = material
        };
    }

    private static Line2D CreateLine(Control ui, Shader shader, int hash)
    {
        var line = CreateThreadLine(shader, RelayWidth, (hash & 0xff) / 255f);
        line.Name = "SakuraMemoryRelay";
        line.TopLevel = true;
        line.ZIndex = RelayZIndex;
        line.ZAsRelative = false;
        ui.AddChildSafely(line);
        return line;
    }

    private static ColorRect CreateRipple(Control ui, Shader shader, Vector2 centre)
    {
        var tint = PileExchangeVfx.MemoryColor;
        var ripple = CreateSocket(shader, Vector2.One * RippleSize, new Vector3(tint.R, tint.G, tint.B));
        ripple.Name = "SakuraMemoryRelayLanding";
        ripple.TopLevel = true;
        ripple.ZIndex = RelayZIndex;
        ripple.ZAsRelative = false;
        ui.AddChildSafely(ripple);
        ripple.GlobalPosition = centre - ripple.Size * 0.5f;
        return ripple;
    }

    private static void FreeSafely(Node? node)
    {
        if (node is not null && GodotObject.IsInstanceValid(node) && !node.IsQueuedForDeletion())
            node.QueueFreeSafely();
    }

    private static float Ease(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return 1f - MathF.Pow(1f - t, 3f);
    }

    private static float EaseIn(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t * t * t;
    }
}
