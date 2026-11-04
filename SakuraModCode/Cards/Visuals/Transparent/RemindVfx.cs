using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
/// The Clear card Remind, drawn from its chest medallion: a compass dial opens
/// beside Sakura in a gold (Time) half and a silver (Rewind) half, a hand sweeps
/// up each half and they fuse at XII. Threads then reach from the dial into the
/// Memory button, where the card's recall cue returns; the shared recall visual
/// carries the cards into the hand. With the extra effect the chosen hand splits
/// off and stamps the new Time or Rewind card. The session owns presentation
/// only; the card owns the selection, the recall and the generated card.
/// </summary>
internal sealed class RemindVfx : CelVfxSession
{
    internal const string ShaderPath =
        MainFile.ResPath + "/shaders/card_vfx/remind_dial.gdshader";
    internal static IReadOnlyList<string> AssetPaths { get; } =
        [ShaderPath, .. SakuraMemoryRecallVisual.AssetPaths];

    // Keep in step with scripts/render_remind_vfx.gd (seconds after the card resolves).
    private const float Frame = 1f / StepFrequency;
    internal const float RingStart = 0.18f;
    private const float RingEnd = 0.34f;
    private const float HandGrowStart = 0.30f;
    private const float HandGrow = 0.08f;
    private const float SweepStart = 0.36f;
    internal const float Merge = 0.62f;
    private const int FlashFrames = 1;
    private const float Burst = 0.30f;
    private const float Feather = 0.80f;
    internal const float ThreadReach = 0.20f;
    internal const float ThreadStagger = 0.03f;
    private const float ThreadTail = 0.14f;
    internal const int MaxThreads = 5;
    private const float SocketRipple = 0.32f;
    private const float HoldDim = 0.15f;
    private const float SplitFlight = 0.22f;
    private const float SplitReturn = 0.20f;
    private const float Stamp = 0.30f;
    internal const float OutroAfterArrive = 0.30f;
    internal const float OutroAfterMerge = 0.32f;
    internal const float OutroAfterSplit = 0.16f;
    internal const float Shatter = 0.24f;

    private const float DialRadius = 118f;
    private const float DialRegion = 640f;
    // The dial stands this far past the caster's front edge, a little above her centre.
    private const float DialGap = 180f;
    private const float DialLift = 0.28f;
    private const float ThreadWidth = 14f;
    private const float ThreadFan = 26f;
    private const float SocketSize = 220f;
    private const float SplitHandLength = 96f;
    private const int VfxZIndex = 3000;
    private static readonly Vector3 TimeTint = new(1f, 0.80f, 0.36f);
    private static readonly Vector3 RewindTint = new(0.70f, 0.88f, 1f);

    private readonly Shader _relayShader;
    private readonly Node2D _overlay;
    private readonly ShaderMaterial _dial;
    private readonly Vector2 _dialCentre;
    private readonly bool _local;
    private readonly List<(Line2D Line, ShaderMaterial Material, float Start)> _threads = [];
    private ShaderMaterial? _socket;
    private SplitVisual? _split;
    private float _t;
    private float _threadStart = -1f;
    private float _arrive = -1f;
    private float _outro = -1f;

    private RemindVfx(
        Node2D root,
        NCombatRoom room,
        Node2D overlay,
        Shader dialShader,
        Shader relayShader,
        Vector2 dialCentre,
        bool local)
        : base(root, room)
    {
        _overlay = overlay;
        _relayShader = relayShader;
        _dialCentre = dialCentre;
        _local = local;
        Root.TreeExiting += FreeOverlay;

        var size = Vector2.One * DialRegion;
        var rect = new ColorRect
        {
            Name = "Dial",
            Color = Colors.White,
            Size = size,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _dial = new ShaderMaterial { Shader = dialShader };
        _dial.SetShaderParameter("mode", 0f);
        _dial.SetShaderParameter("region_size", size);
        _dial.SetShaderParameter("dial_radius", DialRadius);
        _dial.SetShaderParameter("seed", 0.41f);
        _dial.SetShaderParameter("opacity", 0f);
        rect.Material = _dial;
        Root.AddChild(rect);
        rect.GlobalPosition = dialCentre - size * 0.5f;
    }

    protected override IEnumerable<ShaderMaterial> Materials => [];

    // The dial holds behind the native selection grid for as long as the player takes.
    protected override float MaximumLifetime => 90f;

    internal static Task PlayOrResolveAsync(
        CardModel card,
        Creature caster,
        Func<Cues, Task> resolveGameplay)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(caster);
        ArgumentNullException.ThrowIfNull(resolveGameplay);

        return CelVfxSession.PlayOrResolveAsync(
            "Remind",
            () => TryCreate(caster),
            session => session.PlayCelPrelude(card, caster),
            scope => resolveGameplay(new Cues(scope)),
            session => session.BeginOutro(),
            session => session.Dispose());
    }

    internal sealed class Cues(CueScope<RemindVfx> scope)
    {
        /// <summary>
        /// Threads reach from the fused dial into the Memory button; returns when the
        /// last one lands, which is when the recall should resolve.
        /// </summary>
        internal Task ReachMemoryAsync(int cardCount) =>
            cardCount <= 0
                ? Task.CompletedTask
                : scope.InvokeAsync("reach memory", session => session.ReachMemoryAsync(cardCount));

        /// <summary>The chosen hand leaves the dial and stamps the new card.</summary>
        internal void Split(CardModel chosen)
        {
            ArgumentNullException.ThrowIfNull(chosen);
            scope.Invoke("split", session => session.Split(chosen));
        }
    }

    /// <summary>When the last of <paramref name="threads"/> threads leaving at <paramref name="start"/> lands.</summary>
    internal static float ArriveTime(float start, int threads) =>
        start + ThreadReach + ThreadStagger * (Math.Clamp(threads, 1, MaxThreads) - 1);

    private static RemindVfx? TryCreate(Creature caster)
    {
        if (!TryPrepare(
                "Remind",
                static () => (
                    Dial: PreloadManager.Cache.GetAsset<Shader>(ShaderPath),
                    Relay: PreloadManager.Cache.GetAsset<Shader>(SakuraMemoryRecallVisual.RelayShaderPath)),
                out var room,
                out _,
                out var shaders)
            || room.Ui is not { } ui)
        {
            return null;
        }

        Node2D? root = null;
        Node2D? overlay = null;
        try
        {
            if (CelVfxGeometry.ResolveCaster(room.GetCreatureNode(caster)) is not { } anchor)
                return null;

            root = new Node2D
            {
                Name = "SakuraRemindVfx",
                ZAsRelative = false,
                ZIndex = VfxZIndex
            };
            room.CombatVfxContainer.AddChildSafely(root);
            root.GlobalPosition = Vector2.Zero;
            overlay = new Node2D
            {
                Name = "SakuraRemindOverlay",
                TopLevel = true,
                ZAsRelative = false,
                ZIndex = VfxZIndex
            };
            ui.AddChildSafely(overlay);
            overlay.GlobalPosition = Vector2.Zero;

            var facing = anchor.FacingSign == 0f ? 1f : anchor.FacingSign;
            var dialCentre = anchor.BodyCenter + new Vector2(
                facing * (anchor.BodySize.X * 0.5f + DialGap),
                -anchor.BodySize.Y * DialLift);
            var session = new RemindVfx(
                root,
                room,
                overlay,
                shaders.Dial,
                shaders.Relay,
                dialCentre,
                caster.Player is { } player && LocalContext.IsMe(player));
            session.StartClock();
            return session;
        }
        catch (Exception exception)
        {
            MainFile.Logger.Error($"Could not create Remind VFX: {exception}");
            root?.QueueFreeSafely();
            overlay?.QueueFreeSafely();
            return null;
        }
    }

    private async Task ReachMemoryAsync(int cardCount)
    {
        if (!IsActive() || _threadStart >= 0f || _outro >= 0f)
            return;

        // The Memory button is the local player's UI; a partner's dial only fuses.
        if (!_local
            || !PileExchangeVfx.TryGetPileCenter(Room, SakuraMemoryPile.PileType, out var pile))
        {
            _arrive = Math.Max(_t, Merge);
            return;
        }

        var start = Math.Max(_t, Merge + Frame);
        if (!await WaitActive(start - _t))
            return;

        var count = Math.Min(cardCount, MaxThreads);
        _threadStart = start;
        _arrive = ArriveTime(start, count);
        for (var index = 0; index < count; index++)
        {
            var line = SakuraMemoryRecallVisual.CreateThreadLine(_relayShader, ThreadWidth, index * 0.37f);
            line.Name = $"Thread{index}";
            line.Points = ThreadPoints(index, count, pile);
            ((ShaderMaterial)line.Material).SetShaderParameter("opacity", 0f);
            Root.AddChild(line);
            _threads.Add((line, (ShaderMaterial)line.Material, start + ThreadStagger * index));
        }

        var socket = SakuraMemoryRecallVisual.CreateSocket(
            _relayShader,
            Vector2.One * SocketSize,
            new Vector3(PileExchangeVfx.MemoryColor.R, PileExchangeVfx.MemoryColor.G, PileExchangeVfx.MemoryColor.B));
        socket.Name = "MemorySocket";
        _overlay.AddChild(socket);
        socket.GlobalPosition = pile - socket.Size * 0.5f;
        _socket = (ShaderMaterial)socket.Material;
        UpdateFrame();

        await WaitActive(_arrive - _t);
    }

    // From the dial down below the enemies' health bars and up into the Memory
    // button; threads fan apart in the middle and rejoin at the ends.
    private Vector2[] ThreadPoints(int index, int count, Vector2 pile)
    {
        var fan = (index - (count - 1) * 0.5f) * ThreadFan;
        var a = Root.ToLocal(_dialCentre);
        var b = Root.ToLocal(_dialCentre + new Vector2(220f, 430f + fan));
        var c = Root.ToLocal(pile + new Vector2(-460f, 235f + fan));
        var d = Root.ToLocal(pile);
        var points = new Vector2[49];
        for (var k = 0; k < points.Length; k++)
            points[k] = SakuraMemoryRecallVisual.Bezier(a, b, c, d, k / 48f);
        return points;
    }

    private void Split(CardModel chosen)
    {
        if (!IsActive() || _split is not null || _outro >= 0f || !_local)
            return;

        var time = chosen is Time;
        var body = new ColorRect
        {
            Name = "SplitHand",
            Color = Colors.White,
            Size = new Vector2(SplitHandLength + 40f, 48f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        body.Position = -body.Size * 0.5f;
        var handMaterial = new ShaderMaterial { Shader = _dial.Shader };
        handMaterial.SetShaderParameter("mode", 1f);
        handMaterial.SetShaderParameter("region_size", body.Size);
        handMaterial.SetShaderParameter("hand_kind", time ? 0f : 1f);
        handMaterial.SetShaderParameter("hand_length", SplitHandLength);
        handMaterial.SetShaderParameter("opacity", 0f);
        body.Material = handMaterial;
        var hand = new Node2D { Name = "SplitHandPivot" };
        hand.AddChild(body);
        _overlay.AddChild(hand);

        var stamp = SakuraMemoryRecallVisual.CreateSocket(
            _relayShader, Vector2.One * SocketSize, time ? TimeTint : RewindTint);
        stamp.Name = "SplitStamp";
        ((ShaderMaterial)stamp.Material).SetShaderParameter("opacity", 0f);
        _overlay.AddChild(stamp);

        _split = new SplitVisual(chosen, time, _t, hand, handMaterial, stamp);
        UpdateFrame();
    }

    private void BeginOutro()
    {
        if (!IsActive())
        {
            Dispose();
            return;
        }

        var after = _split is { } split
            ? split.Start + OutroAfterSplit
            : _arrive >= 0f
                ? _arrive + OutroAfterArrive
                : Merge + OutroAfterMerge;
        _outro = Math.Max(after, _t);
    }

    protected override void OnFrame(float delta)
    {
        _t += delta;
        UpdateFrame();
        if (_outro >= 0f && _t >= End())
            Dispose();
    }

    private float End()
    {
        var end = _outro + Shatter;
        if (_split is { } split)
            end = Math.Max(end, split.Start + SplitFlight + Stamp);
        if (_threads.Count > 0)
            end = Math.Max(end, _threads[^1].Start + ThreadReach + ThreadTail);
        if (_arrive >= 0f && _socket is not null)
            end = Math.Max(end, _arrive + SocketRipple);
        return end;
    }

    private void UpdateFrame()
    {
        var t = _t;
        var stepTime = Mathf.Floor(t * StepFrequency) / StepFrequency;

        var live = t >= RingStart && (_outro < 0f || t < _outro + Shatter);
        _dial.SetShaderParameter("opacity", live ? 1f : 0f);
        _dial.SetShaderParameter("step_time", stepTime);
        _dial.SetShaderParameter("reveal", EaseOut((t - RingStart) / (RingEnd - RingStart)));
        _dial.SetShaderParameter("hand_grow", EaseOut((t - HandGrowStart) / HandGrow));
        var raw = Math.Clamp((t - SweepStart) / (Merge - SweepStart), 0f, 1f);
        var s = EaseInOut(raw);
        var gold = Mathf.Pi * 0.5f + Mathf.Pi * s;
        var silver = Mathf.Pi * 0.5f - Mathf.Pi * s;
        var goldVisible = 1f;
        var silverVisible = 1f;
        if (_split is { } split)
        {
            // The chosen hand has left as its own node; the other swings back and fades.
            var back = EaseOut((t - split.Start) / SplitReturn);
            if (split.Time)
            {
                goldVisible = 0f;
                silver = -Mathf.Pi * 0.5f + 0.6f * back;
                silverVisible = 1f - back;
            }
            else
            {
                silverVisible = 0f;
                gold = Mathf.Pi * 1.5f - 0.6f * back;
                goldVisible = 1f - back;
            }
            UpdateSplit(split, t);
        }
        _dial.SetShaderParameter("gold_angle", gold);
        _dial.SetShaderParameter("silver_angle", silver);
        _dial.SetShaderParameter("gold_visible", goldVisible);
        _dial.SetShaderParameter("silver_visible", silverVisible);
        _dial.SetShaderParameter("smear", raw is > 0f and < 1f ? 0.42f * Mathf.Sin(Mathf.Pi * raw) : 0f);
        _dial.SetShaderParameter("fused", t >= Merge ? 1f : 0f);
        var sinceMerge = t < Merge ? -1 : (int)Mathf.Floor((t - Merge) / Frame + 0.0001f);
        _dial.SetShaderParameter("flash", sinceMerge is >= 0 and < FlashFrames ? 1f : 0f);
        _dial.SetShaderParameter("burst", t >= Merge ? Math.Clamp((t - Merge) / Burst, 0f, 1f) : 0f);
        _dial.SetShaderParameter("feather", t >= Merge ? Math.Clamp((t - Merge) / Feather, 0f, 1f) : 0f);
        _dial.SetShaderParameter("hold_dim", _arrive >= 0f ? Math.Clamp((t - _arrive) / HoldDim, 0f, 1f) : 0f);
        _dial.SetShaderParameter("shatter", _outro >= 0f ? Math.Clamp((t - _outro) / Shatter, 0f, 1f) : 0f);

        foreach (var (_, material, start) in _threads)
        {
            var head = EaseOut((t - start) / ThreadReach);
            var tail = EaseIn((t - start - ThreadReach) / ThreadTail);
            material.SetShaderParameter("head", head);
            material.SetShaderParameter("tail", tail);
            material.SetShaderParameter("step_time", stepTime);
            material.SetShaderParameter("opacity", head > 0f && tail < 1f ? 1f : 0f);
        }

        if (_socket is { } socket && _threadStart >= 0f)
        {
            // Tightens while the threads reach, ripples on the last arrival.
            var from = _threadStart + 0.06f;
            socket.SetShaderParameter("converge", t < _arrive ? Math.Clamp((t - from) / (_arrive - from), 0f, 1f) : 0f);
            socket.SetShaderParameter("ripple", t >= _arrive ? Math.Clamp((t - _arrive) / SocketRipple, 0f, 1f) : 0f);
        }
    }

    private void UpdateSplit(SplitVisual split, float t)
    {
        if (!TrySlotCentre(split.Card, out var centre, out var height))
            return;

        var from = _dialCentre + new Vector2(0f, -DialRadius * 0.45f);
        var to = centre + new Vector2(0f, -height * 0.18f);
        var lift = from + new Vector2(80f, -140f);
        var pre = to + new Vector2(-60f, -220f);
        var u = EaseInOut((t - split.Start) / SplitFlight);
        var p = SakuraMemoryRecallVisual.Bezier(from, lift, pre, to, u);
        var ahead = SakuraMemoryRecallVisual.Bezier(from, lift, pre, to, Math.Min(u + 0.02f, 1f));
        split.Hand.GlobalPosition = p;
        split.Hand.Rotation = ahead.DistanceTo(p) > 0.5f ? (ahead - p).Angle() : Mathf.Pi * 0.5f;
        split.Hand.Scale = Vector2.One * Mathf.Lerp(1.25f, 0.9f, u);
        var land = split.Start + SplitFlight;
        split.HandMaterial.SetShaderParameter("opacity", 1f - Math.Clamp((t - land) / 0.10f, 0f, 1f));
        split.Stamp.GlobalPosition = to - split.Stamp.Size * 0.5f;
        var stamp = (ShaderMaterial)split.Stamp.Material;
        stamp.SetShaderParameter("ripple", t >= land ? Math.Clamp((t - land) / Stamp, 0f, 1f) : 0f);
        stamp.SetShaderParameter("opacity", t >= land ? 1f : 0f);
    }

    // Where the card settles in the hand: its holder's target slot, while it may
    // still be easing in from wherever native code started it.
    private static bool TrySlotCentre(CardModel card, out Vector2 centre, out float height)
    {
        centre = default;
        height = 0f;
        if (NCard.FindOnTable(card) is not { } node || !GodotObject.IsInstanceValid(node))
            return false;

        var size = node.GetCurrentSize();
        var transform = node.GetGlobalTransform();
        var now = transform * (size * 0.5f);
        height = size.Y * transform.Scale.Y;
        centre = now;
        if (node.GetParent() is NHandCardHolder holder
            && holder.GetParent() is CanvasItem slots)
        {
            centre = slots.GetGlobalTransform() * holder.TargetPosition + (now - holder.GlobalPosition);
        }
        return true;
    }

    private void FreeOverlay()
    {
        if (GodotObject.IsInstanceValid(Root))
            Root.TreeExiting -= FreeOverlay;
        if (GodotObject.IsInstanceValid(_overlay) && !_overlay.IsQueuedForDeletion())
            _overlay.QueueFreeSafely();
    }

    private static float EaseOut(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return 1f - MathF.Pow(1f - t, 3f);
    }

    private static float EaseIn(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t * t * t;
    }

    private static float EaseInOut(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return -(MathF.Cos(MathF.PI * t) - 1f) * 0.5f;
    }

    private sealed record SplitVisual(
        CardModel Card,
        bool Time,
        float Start,
        Node2D Hand,
        ShaderMaterial HandMaterial,
        ColorRect Stamp);
}
