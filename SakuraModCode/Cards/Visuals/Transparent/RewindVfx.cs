using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
/// The Clear card Rewind, drawn from its card art: the gold pocket watch opens
/// beside Sakura, its crown clicks and the star hand spins once counter-clockwise
/// while the mirror-script numerals light. When the hand stops the card's move cue
/// returns; each card coming back from Exhaust waits hidden in its hand slot while
/// a water stream carries ash and embers from the Exhaust button to it, then a
/// charred-paper silhouette un-burns from the top down. With the extra effect the
/// gem throws a red spark onto each cost as the burn front uncovers it. The
/// session owns presentation only; the card owns the selection and every move.
/// </summary>
internal sealed class RewindVfx : CelVfxSession
{
    internal const string ShaderPath =
        MainFile.ResPath + "/shaders/card_vfx/rewind_watch.gdshader";
    internal static IReadOnlyList<string> AssetPaths { get; } = [ShaderPath];

    // Keep in step with scripts/render_rewind_vfx.gd (seconds after the card resolves).
    private const float Frame = 1f / StepFrequency;
    internal const float WatchPopStart = 0.18f;
    internal const float WatchPop = 0.12f;
    internal const float CrownClick = 0.30f;
    internal const float SpinStart = 0.38f;
    internal const float SpinEnd = 0.80f;
    internal const float Pulse = 0.36f;
    internal const float StreamLead = Frame;
    internal const float CardStagger = 0.07f;
    internal const float CardStaggerCap = 0.5f;
    internal const float StreamReach = 0.26f;
    internal const float StreamTail = 0.16f;
    internal const float Assemble = 0.10f;
    internal const float Unburn = 0.55f;
    internal const float Sheen = 0.30f;
    internal const float SparkReveal = 0.26f;
    internal const float SparkFlight = 0.22f;
    internal const float CostPop = 0.30f;
    internal const float OutroAfterStop = 0.30f;
    internal const float OutroAfterRestore = 0.12f;
    internal const float Vanish = 0.24f;

    private const float WatchRadius = 70f;
    private const float WatchRegion = 320f;
    // The watch stands this far past the caster's front edge, above her centre.
    private const float WatchGap = 110f;
    private const float WatchLift = 0.39f;
    private const float StreamWidth = 36f;
    private const float SparkRegion = 120f;
    private const float EdgePad = 8f;
    private const float CoverCorner = 18f;
    // A returning card waits this long for native code to give it a hand holder.
    private const float HolderWait = 0.5f;
    private const int VfxZIndex = 3000;

    private static readonly Dictionary<CardModel, (RewindVfx Session, int Index, int Count, bool Free)> Pending =
        new(ReferenceEqualityComparer.Instance);
    private static ImageTexture? _lineTexture;

    private readonly Shader _shader;
    private readonly Node2D _overlay;
    private readonly ColorRect _watchRect;
    private readonly ShaderMaterial _watch;
    private readonly Vector2 _watchCentre;
    private readonly bool _local;
    private readonly List<Return> _returns = [];
    private float _t;
    private float _stop = -1f;
    private bool _outroRequested;
    private float _outro = -1f;

    private RewindVfx(
        Node2D root,
        NCombatRoom room,
        Node2D overlay,
        Shader shader,
        Vector2 watchCentre,
        bool local)
        : base(root, room)
    {
        _overlay = overlay;
        _shader = shader;
        _watchCentre = watchCentre;
        _local = local;
        Root.TreeExiting += ReleaseNodes;

        var size = Vector2.One * WatchRegion;
        _watchRect = new ColorRect
        {
            Name = "Watch",
            Color = Colors.White,
            Size = size,
            PivotOffset = size * 0.5f,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Scale = Vector2.One * 0.01f
        };
        _watch = Material(0f, size);
        _watch.SetShaderParameter("watch_radius", WatchRadius);
        _watch.SetShaderParameter("seed", 0.29f);
        _watchRect.Material = _watch;
        Root.AddChild(_watchRect);
        _watchRect.GlobalPosition = watchCentre - size * 0.5f;
    }

    protected override IEnumerable<ShaderMaterial> Materials => [];

    // The watch waits behind the native selection grid for as long as the player takes.
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
            "Rewind",
            () => TryCreate(caster),
            session => session.PlayCelPrelude(card, caster),
            scope => resolveGameplay(new Cues(scope)),
            session => session.BeginOutro(),
            session => session.Dispose());
    }

    internal sealed class Cues(CueScope<RewindVfx> scope)
    {
        /// <summary>Returns when the hand stops; the moves resolve after it.</summary>
        internal Task StopAsync() =>
            scope.InvokeAsync("stop", session => session.StopAsync());

        /// <summary>
        /// Marks a card about to move from Exhaust to the hand, so its new node takes the
        /// reverse burn instead of the native flight. Pair with <see cref="Unmark"/>.
        /// </summary>
        internal void Mark(CardModel card, int index, int count, bool free)
        {
            ArgumentNullException.ThrowIfNull(card);
            scope.Invoke("mark", session => session.Mark(card, index, count, free));
        }

        /// <summary>Always runs, with or without presentation.</summary>
        internal static void Unmark(CardModel card) => Pending.Remove(card);
    }

    /// <summary>When the <paramref name="index"/>-th of <paramref name="count"/> streams may leave after the stop.</summary>
    internal static float StreamDelay(int index, int count) =>
        StreamLead + Math.Min(CardStagger, CardStaggerCap / Math.Max(count - 1, 1)) * index;

    /// <summary>The postfix on native card node creation, beside the Memory recall.</summary>
    internal static void AfterNativeCardNodeCreated(
        CardModel card,
        NCard? node,
        PileType targetPileType,
        bool owningPlayerIsLocal)
    {
        if (!Pending.Remove(card, out var order)
            || node is null
            || !owningPlayerIsLocal
            || targetPileType != PileType.Hand
            || TestMode.IsOn
            || !GodotObject.IsInstanceValid(node))
        {
            return;
        }

        order.Session.BeginReturn(node, order.Index, order.Count, order.Free);
    }

    private static RewindVfx? TryCreate(Creature caster)
    {
        if (!TryPrepare(
                "Rewind",
                static () => PreloadManager.Cache.GetAsset<Shader>(ShaderPath),
                out var room,
                out _,
                out var shader)
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
                Name = "SakuraRewindVfx",
                ZAsRelative = false,
                ZIndex = VfxZIndex
            };
            room.CombatVfxContainer.AddChildSafely(root);
            root.GlobalPosition = Vector2.Zero;
            overlay = new Node2D
            {
                Name = "SakuraRewindOverlay",
                TopLevel = true,
                ZAsRelative = false,
                ZIndex = VfxZIndex
            };
            ui.AddChildSafely(overlay);
            overlay.GlobalPosition = Vector2.Zero;

            var facing = anchor.FacingSign == 0f ? 1f : anchor.FacingSign;
            var watchCentre = anchor.BodyCenter + new Vector2(
                facing * (anchor.BodySize.X * 0.5f + WatchGap),
                -anchor.BodySize.Y * WatchLift);
            var session = new RewindVfx(
                root,
                room,
                overlay,
                shader,
                watchCentre,
                caster.Player is { } player && LocalContext.IsMe(player));
            session.StartClock();
            return session;
        }
        catch (Exception exception)
        {
            MainFile.Logger.Error($"Could not create Rewind VFX: {exception}");
            root?.QueueFreeSafely();
            overlay?.QueueFreeSafely();
            return null;
        }
    }

    private ShaderMaterial Material(float mode, Vector2 size)
    {
        var material = new ShaderMaterial { Shader = _shader };
        material.SetShaderParameter("mode", mode);
        material.SetShaderParameter("region_size", size);
        material.SetShaderParameter("opacity", 0f);
        return material;
    }

    private async Task StopAsync()
    {
        if (!IsActive() || _stop >= 0f)
            return;
        await WaitActive(SpinEnd - _t);
        _stop = Math.Max(_t, SpinEnd);
    }

    private void Mark(CardModel card, int index, int count, bool free)
    {
        // The Exhaust button is the local player's UI; Instant mode skips the return.
        if (!IsActive() || !_local || SakuraMemoryVisualTiming.CurrentTimeScale() <= 0f)
            return;
        Pending[card] = (this, index, count, free);
    }

    private void BeginReturn(NCard node, int index, int count, bool free)
    {
        if (!IsActive() || _outro >= 0f)
            return;

        node.Position = PileType.Hand.GetTargetPosition(node);
        node.Modulate = Colors.Transparent;
        var stop = _stop >= 0f ? _stop : Math.Max(_t, SpinEnd);
        _returns.Add(new Return(node, free, _t, stop + StreamDelay(index, count)));
    }

    private void BeginOutro()
    {
        if (!IsActive())
        {
            Dispose();
            return;
        }
        _outroRequested = true;
    }

    protected override void OnFrame(float delta)
    {
        _t += delta;
        UpdateFrame();
        if (_outro >= 0f && _t >= _outro + Vanish && _returns.All(static entry => entry.Finished))
            Dispose();
    }

    private void UpdateFrame()
    {
        var t = _t;
        var stepTime = Mathf.Floor(t * StepFrequency) / StepFrequency;

        var gem = 0f;
        foreach (var entry in _returns)
            gem = Math.Max(gem, UpdateReturn(entry, t, stepTime));

        if (_outroRequested && _outro < 0f && _returns.All(static entry => entry.Started || entry.Finished))
        {
            var outro = (_stop >= 0f ? _stop : SpinEnd) + OutroAfterStop;
            foreach (var entry in _returns.Where(static entry => entry.Started))
            {
                outro = Math.Max(outro, entry.RestoreEnd + OutroAfterRestore);
                if (entry.Free)
                    outro = Math.Max(outro, entry.SparkLand + OutroAfterRestore);
            }
            _outro = Math.Max(outro, t);
        }

        var live = t >= WatchPopStart && (_outro < 0f || t < _outro + Vanish);
        _watch.SetShaderParameter("opacity", live ? 1f : 0f);
        _watch.SetShaderParameter("step_time", stepTime);
        _watchRect.Scale = Vector2.One * Math.Max(EaseOutBack((t - WatchPopStart) / WatchPop), 0.01f);
        var sinceClick = t < CrownClick ? -1 : (int)Mathf.Floor((t - CrownClick) / Frame + 0.0001f);
        var crown = sinceClick switch
        {
            0 or 1 => 1f,
            2 => 1f - Math.Clamp((t - CrownClick - 2f * Frame) / Frame, 0f, 1f),
            _ => 0f
        };
        _watch.SetShaderParameter("crown", crown);
        var raw = Math.Clamp((t - SpinStart) / (SpinEnd - SpinStart), 0f, 1f);
        var spin = EaseInOut(raw);
        _watch.SetShaderParameter("hand_angle", -Mathf.Pi * 0.5f - Mathf.Tau * spin);
        _watch.SetShaderParameter("smear", raw is > 0f and < 1f ? 0.55f * Mathf.Sin(Mathf.Pi * raw) : 0f);
        _watch.SetShaderParameter("glyph_lit", spin);
        _watch.SetShaderParameter("pulse", t >= SpinEnd ? Math.Clamp((t - SpinEnd) / Pulse, 0f, 1f) : 0f);
        _watch.SetShaderParameter("vanish", _outro >= 0f ? Math.Clamp((t - _outro) / Vanish, 0f, 1f) : 0f);
        _watch.SetShaderParameter("gem_flash", gem);
    }

    /// <summary>Advances one returning card; returns the gem flare it asks for.</summary>
    private float UpdateReturn(Return entry, float t, float stepTime)
    {
        if (entry.Finished)
            return 0f;
        if (!GodotObject.IsInstanceValid(entry.Node))
        {
            Finish(entry);
            return 0f;
        }

        if (!entry.Started)
        {
            if (t < entry.Earliest || HolderOf(entry.Node) is not { } found)
            {
                if (t > Math.Max(entry.Earliest, entry.CreatedAt) + HolderWait)
                    Finish(entry);
                return 0f;
            }
            if (!PileExchangeVfx.TryGetPileCenter(Room, PileType.Exhaust, out var pile))
            {
                Finish(entry);
                return 0f;
            }
            entry.Begin(found, pile, Math.Max(t, entry.Earliest), CreateStream(entry.Seed));
        }

        // A card that left the hand (played, discarded) is shown at once.
        if (!GodotObject.IsInstanceValid(entry.Holder) || HolderOf(entry.Node) != entry.Holder)
        {
            Finish(entry);
            return 0f;
        }
        if (!entry.Revealed)
            entry.Holder!.Position = entry.Holder.TargetPosition;

        var body = GodotObject.IsInstanceValid(entry.Node.Body) ? entry.Node.Body : (Control)entry.Node;
        var bodyTransform = body.GetGlobalTransform();
        var centre = bodyTransform * (body.Size * 0.5f);
        var landing = centre + new Vector2(0f, -body.Size.Y * bodyTransform.Scale.Y * 0.12f);

        if (entry.Stream is { } line && GodotObject.IsInstanceValid(line))
        {
            var points = StreamPoints(entry.Pile, landing);
            line.Points = points;
            var stream = (ShaderMaterial)line.Material;
            var head = EaseOut((t - entry.Start) / StreamReach);
            var tail = EaseIn((t - entry.Land) / StreamTail);
            stream.SetShaderParameter("line_length", PolylineLength(points));
            stream.SetShaderParameter("head", head);
            stream.SetShaderParameter("tail", tail);
            stream.SetShaderParameter("step_time", stepTime);
            stream.SetShaderParameter("opacity", head > 0f && tail < 1f ? 1f : 0f);
            if (tail >= 1f)
                FreeSafely(line);
        }

        if (t >= entry.Land)
        {
            entry.Cover ??= CreateCover(entry.Seed);
            entry.Cover.Anchor.GlobalTransform = bodyTransform;
            entry.Cover.Rect.Position = -Vector2.One * EdgePad;
            entry.Cover.Rect.Size = body.Size + Vector2.One * EdgePad * 2f;
            var cover = entry.Cover.Material;
            cover.SetShaderParameter("region_size", entry.Cover.Rect.Size);
            cover.SetShaderParameter("step_time", stepTime);
            cover.SetShaderParameter("assemble", EaseOut((t - entry.Land) / Assemble));
            cover.SetShaderParameter("restore", EaseInOut((t - entry.Land - Assemble) / Unburn));
            cover.SetShaderParameter("sheen", t >= entry.RestoreEnd ? Math.Clamp((t - entry.RestoreEnd) / Sheen, 0f, 1f) : 0f);
            cover.SetShaderParameter("opacity", 1f);
        }

        // The face appears only under a fully gathered char.
        if (!entry.Revealed && t >= entry.Land + Assemble)
        {
            entry.Node.Modulate = Colors.White;
            entry.Revealed = true;
        }

        var gem = 0f;
        if (entry.Free)
        {
            var cost = CostCentre(entry.Node, body, bodyTransform);
            if (t >= entry.SparkStart && t < entry.SparkLand + 0.1f)
                gem = Math.Clamp((t - entry.SparkStart) / 0.25f, 0f, 1f);
            if (t >= entry.SparkStart)
            {
                entry.Spark ??= CreateSpark();
                var u = EaseInOut((t - entry.SparkStart) / SparkFlight);
                entry.Spark.Pivot.GlobalPosition = Bezier(
                    _watchCentre, _watchCentre + new Vector2(140f, -120f), cost + new Vector2(-80f, -200f), cost, u);
                entry.Spark.Flight.SetShaderParameter("opacity", t < entry.SparkLand ? 1f : 0f);
                entry.Spark.PopRect.GlobalPosition = cost - entry.Spark.PopRect.Size * 0.5f;
                entry.Spark.Pop.SetShaderParameter("spark_pop", t >= entry.SparkLand ? Math.Clamp((t - entry.SparkLand) / CostPop, 0f, 1f) : 0f);
                entry.Spark.Pop.SetShaderParameter("opacity", t >= entry.SparkLand ? 1f : 0f);
            }
        }

        var end = entry.RestoreEnd + Sheen;
        if (entry.Free)
            end = Math.Max(end, entry.SparkLand + CostPop);
        if (t >= end)
            Finish(entry);
        return gem;
    }

    private static NHandCardHolder? HolderOf(NCard node) =>
        GodotObject.IsInstanceValid(node) ? node.GetParent() as NHandCardHolder : null;

    // The cost icon's centre, or the top-left corner where a cost sits when it is hidden.
    private static Vector2 CostCentre(NCard node, Control body, Transform2D bodyTransform)
    {
        if (SakuraCardFaceNodes.From(node).EnergyIcon is { } icon
            && GodotObject.IsInstanceValid(icon)
            && icon.IsVisibleInTree())
        {
            return icon.GetGlobalTransform() * (icon.Size * 0.5f);
        }
        return bodyTransform * (body.Size * new Vector2(0.12f, 0.07f));
    }

    // From the Exhaust button left above the End Turn button, then down into the slot.
    private static Vector2[] StreamPoints(Vector2 pile, Vector2 landing)
    {
        var b = pile + new Vector2(-220f, 10f);
        var c = landing + new Vector2(300f, -140f);
        var points = new Vector2[41];
        for (var k = 0; k < points.Length; k++)
            points[k] = Bezier(pile, b, c, landing, k / 40f);
        return points;
    }

    private static float PolylineLength(Vector2[] points)
    {
        var total = 0f;
        for (var k = 1; k < points.Length; k++)
            total += points[k - 1].DistanceTo(points[k]);
        return total;
    }

    private Line2D CreateStream(float seed)
    {
        _lineTexture ??= ImageTexture.CreateFromImage(Image.CreateEmpty(4, 4, false, Image.Format.Rgba8));
        var material = Material(1f, Vector2.One);
        material.SetShaderParameter("seed", seed);
        material.SetShaderParameter("line_width", StreamWidth);
        var line = new Line2D
        {
            Name = "EmberStream",
            Width = StreamWidth,
            Texture = _lineTexture,
            TextureMode = Line2D.LineTextureMode.Stretch,
            JointMode = Line2D.LineJointMode.Round,
            Material = material
        };
        _overlay.AddChild(line);
        return line;
    }

    private CoverVisual CreateCover(float seed)
    {
        var anchor = new Node2D { Name = "AshAnchor" };
        var rect = new ColorRect
        {
            Name = "AshCover",
            Color = Colors.White,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        var material = Material(2f, Vector2.One);
        material.SetShaderParameter("seed", seed);
        material.SetShaderParameter("edge_pad", EdgePad);
        material.SetShaderParameter("corner_radius", CoverCorner);
        rect.Material = material;
        anchor.AddChild(rect);
        _overlay.AddChild(anchor);
        return new CoverVisual(anchor, rect, material);
    }

    private SparkVisual CreateSpark()
    {
        var size = Vector2.One * SparkRegion;
        var pivot = new Node2D { Name = "GemSpark" };
        var flightRect = new ColorRect { Color = Colors.White, Size = size, Position = -size * 0.5f, MouseFilter = Control.MouseFilterEnum.Ignore };
        var flight = Material(3f, size);
        flight.SetShaderParameter("spark_on", 1f);
        flightRect.Material = flight;
        pivot.AddChild(flightRect);
        _overlay.AddChild(pivot);
        var popRect = new ColorRect { Name = "CostPop", Color = Colors.White, Size = size, MouseFilter = Control.MouseFilterEnum.Ignore };
        var pop = Material(3f, size);
        popRect.Material = pop;
        _overlay.AddChild(popRect);
        return new SparkVisual(pivot, flight, popRect, pop);
    }

    // A return that ends early (or the session ending) shows its card at once.
    private static void Finish(Return entry)
    {
        if (entry.Finished)
            return;
        entry.Finished = true;
        if (!entry.Revealed && GodotObject.IsInstanceValid(entry.Node))
            entry.Node.Modulate = Colors.White;
        entry.Revealed = true;
        FreeSafely(entry.Stream);
        FreeSafely(entry.Cover?.Anchor);
        FreeSafely(entry.Spark?.Pivot);
        FreeSafely(entry.Spark?.PopRect);
    }

    private void ReleaseNodes()
    {
        if (GodotObject.IsInstanceValid(Root))
            Root.TreeExiting -= ReleaseNodes;
        foreach (var entry in _returns)
            Finish(entry);
        foreach (var card in Pending.Where(pair => ReferenceEquals(pair.Value.Session, this)).Select(static pair => pair.Key).ToList())
            Pending.Remove(card);
        FreeSafely(_overlay);
    }

    private static void FreeSafely(Node? node)
    {
        if (node is not null && GodotObject.IsInstanceValid(node) && !node.IsQueuedForDeletion())
            node.QueueFreeSafely();
    }

    private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
    {
        var s = 1f - t;
        return s * s * s * a + 3f * s * s * t * b + 3f * s * t * t * c + t * t * t * d;
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

    private static float EaseOutBack(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        const float c = 1.9f;
        return 1f + (c + 1f) * MathF.Pow(t - 1f, 3f) + c * MathF.Pow(t - 1f, 2f);
    }

    private sealed record CoverVisual(Node2D Anchor, ColorRect Rect, ShaderMaterial Material);

    private sealed record SparkVisual(Node2D Pivot, ShaderMaterial Flight, ColorRect PopRect, ShaderMaterial Pop);

    private sealed class Return(NCard node, bool free, float createdAt, float earliest)
    {
        internal NCard Node { get; } = node;
        internal bool Free { get; } = free;
        internal float CreatedAt { get; } = createdAt;
        internal float Earliest { get; } = earliest;
        internal float Seed { get; } = (float)Random.Shared.NextDouble();
        internal bool Started { get; private set; }
        internal bool Finished { get; set; }
        internal bool Revealed { get; set; }
        internal NHandCardHolder? Holder { get; private set; }
        internal Vector2 Pile { get; private set; }
        internal float Start { get; private set; }
        internal Line2D? Stream { get; private set; }
        internal CoverVisual? Cover { get; set; }
        internal SparkVisual? Spark { get; set; }
        internal float Land => Start + StreamReach;
        internal float RestoreEnd => Land + Assemble + Unburn;
        internal float SparkStart => Land + Assemble + Unburn * SparkReveal - SparkFlight;
        internal float SparkLand => SparkStart + SparkFlight;

        internal void Begin(NHandCardHolder holder, Vector2 pile, float start, Line2D stream)
        {
            Holder = holder;
            Pile = pile;
            Start = start;
            Stream = stream;
            Started = true;
        }
    }
}
