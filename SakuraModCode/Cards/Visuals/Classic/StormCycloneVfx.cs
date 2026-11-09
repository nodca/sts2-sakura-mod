using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
/// Storm's cyclone, shared by both cards in two forms.
/// <list type="bullet">
/// <item>Funnel (Clow Storm): one residual wind per Wind card played earlier this
/// turn curls into a funnel that wraps the target (or, with the Extra effect,
/// the whole enemy line). Every hit tightens it, flashes it and leaves a cut.</item>
/// <item>Vortex (Sakura Storm): a flat pink vortex above the enemies flings one
/// blade per random hit, as wide as the rolled damage, with a petal burst.</item>
/// </list>
/// Each strand is drawn twice: the far half in the back combat VFX container
/// and the near half in front, so the wind reads as wrapping the enemies. Hits
/// are cues, never waits. scripts/render_storm_vfx.gd is the look-development
/// plate this was tuned on.
/// </summary>
internal sealed class StormCycloneVfx : CelVfxSession
{
    internal const string ScenePath =
        MainFile.ResPath + "/scenes/combat/card_vfx/storm_cyclone_vfx.tscn";
    internal const string ShaderPath =
        MainFile.ResPath + "/shaders/card_vfx/storm_cyclone_stroke.gdshader";
    internal static IReadOnlyList<string> AssetPaths { get; } = [ScenePath, ShaderPath];

    internal enum Form
    {
        Funnel,
        Vortex,
    }

    /// <summary>Residual winds drawn at most; the funnel stops growing here too.</summary>
    internal const int MaxResidualWinds = 3;

    /// <summary>
    /// Pre-damage beat after the shared prelude. Fixed per form: neither the
    /// Wind count nor the enemy count may lengthen the wait before damage.
    /// </summary>
    internal const float FunnelLead = 0.30f;
    internal const float VortexLead = 0.24f;

    internal const float DissolveTime = 0.28f;
    private const float DissolveStagger = 0.03f;
    private const int Segments = 96;
    private const int StrandVertexCount = (Segments + 1) * 2;
    private const float EasePower = 2f;
    private const float StrandStart = 0.06f;
    private const float FormGrow = 0.22f;
    private const float FormStagger = 0.03f;
    private const float WispGrow = 0.16f;
    private const float VortexGrow = 0.22f;
    private const float VortexStagger = 0.04f;
    private const float ResidualStagger = 0.035f;
    private const float ResidualTravel = 0.24f;
    private const float ResidualLag = 0.10f;
    private const float PulseDecay = 0.06f;
    private const float CutDraw = 0.035f;
    private const float CutEraseStart = 0.025f;
    private const float CutEraseEnd = 0.10f;
    private const float FlingTravel = 0.12f;
    private const float FlingLag = 0.06f;
    private const float BladeArrival = 0.05f;
    private const float BladeLag = 0.13f;
    private const float BladeLean = 230f;
    private const float BurstLife = 0.42f;
    private const float BurstGravity = 900f;
    private const int MaxTransientStrokes = 24;
    private const int MaxBurstPetals = 48;

    private static readonly float[] CutAngles = [0.95f, -1.05f, 0.45f, -0.55f, 1.25f, -0.3f, 0.7f];
    private static readonly Vector2[] CutOffsets =
    [
        new(-6f, -14f), new(12f, 10f), new(-18f, 30f), new(16f, -30f), new(0f, 8f), new(-10f, -4f),
        new(8f, 20f),
    ];
    private static readonly Vector2[] ResidualLifts = [new(-10f, -95f), new(15f, 30f), new(-35f, -20f)];

    private static readonly Vector2[] PetalPolygon =
    [
        new(-7f, 0f), new(-3f, -3.8f), new(3f, -4f), new(7f, -1.6f),
        new(5.2f, 0f), new(7f, 1.6f), new(3f, 4f), new(-3f, 3.8f),
    ];
    private static readonly Vector2[] LeafPolygon =
    [
        new(-8f, 0f), new(-3f, -3.2f), new(4f, -2.6f), new(8f, 0f), new(4f, 2.6f), new(-3f, 3.2f),
    ];

    private readonly record struct Palette(Color Edge, Color Body, Color Shade);

    private static readonly Palette ClowPalette =
        new(new Color(1f, 1f, 1f), new Color(0.92f, 0.97f, 1f), new Color(0.36f, 0.56f, 0.78f));
    private static readonly Palette SakuraPalette =
        new(new Color(1f, 1f, 1f), new Color(1f, 0.96f, 0.98f), new Color(1f, 0.66f, 0.82f));
    private static readonly Color BackTint = new(0.80f, 0.85f, 0.93f);
    private static readonly Vector2[] StrandUvs = BuildStrandUvs();

    private readonly record struct FunnelShape(
        float Cx, float Rb, float Rt, float BaseY, float TopY, float K, float Omega, float Sway);

    private readonly record struct VortexShape(float Cx, float Cy, float R, float K, float Omega, float Twist);

    private static bool _loadFailureLogged;

    private sealed class Strand(ShaderMaterial material, float halfWidth, ArrayMesh front, ArrayMesh back)
    {
        internal ShaderMaterial Material { get; } = material;
        internal float HalfWidth { get; } = halfWidth;
        internal ArrayMesh Front { get; } = front;
        internal ArrayMesh Back { get; } = back;
        internal Vector2[] Points { get; } = new Vector2[Segments + 1];
        internal float[] Depths { get; } = new float[Segments + 1];
        internal Vector2[] Vertices { get; } = new Vector2[StrandVertexCount];
        internal Color[] FrontColors { get; } = new Color[StrandVertexCount];
        internal Color[] BackColors { get; } = new Color[StrandVertexCount];
        internal int Index { get; init; }
        internal float Phase { get; init; }
        internal float Turns { get; init; }
        internal float Delay { get; init; }
        internal float Grow { get; init; }
        internal float V0 { get; init; }
        internal float V1 { get; init; } = 1f;
        internal float Flare { get; init; }
        internal float DissolveShare { get; init; } = 1f;
    }

    private enum StrokeKind
    {
        Travel,
        Cut,
    }

    private sealed class Stroke(MeshInstance2D node, ShaderMaterial material, StrokeKind kind, float start)
    {
        internal MeshInstance2D Node { get; } = node;
        internal ShaderMaterial Material { get; } = material;
        internal StrokeKind Kind { get; } = kind;
        internal float Start { get; } = start;
        internal float Travel { get; init; } = 1f;
        internal float Lag { get; init; }
        internal float End => Kind == StrokeKind.Cut ? Start + CutEraseEnd : Start + Lag + Travel;
    }

    private sealed class Orbiter(Polygon2D node)
    {
        internal Polygon2D Node { get; } = node;
        internal float Level { get; init; }
        internal float Phase { get; init; }
        internal float Radius { get; init; }
        internal float Spin { get; init; }
        internal float Fall { get; init; }
    }

    private sealed class BurstPetal(Polygon2D node, Vector2 origin, Vector2 velocity, float start, float spin)
    {
        internal Polygon2D Node { get; } = node;
        internal Vector2 Origin { get; } = origin;
        internal Vector2 Velocity { get; } = velocity;
        internal float Start { get; } = start;
        internal float Spin { get; } = spin;
    }

    private readonly Form _form;
    private readonly Palette _palette;
    private readonly Node2D _backRoot;
    private readonly Node2D _frontStrands;
    private readonly Node2D _strokeLayer;
    private readonly Node2D _bitLayer;
    private readonly MeshInstance2D _template;
    private readonly List<ShaderMaterial> _materials = [];
    private readonly List<Strand> _strands = [];
    private readonly List<Strand> _wisps = [];
    private readonly List<Stroke> _residual = [];
    private readonly List<Stroke> _transient = [];
    private readonly List<Orbiter> _orbiters = [];
    private readonly List<BurstPetal> _bursts = [];
    private readonly Dictionary<Creature, CelVfxGeometry.TargetGeometry> _targets = [];
    private readonly Random _random = new();
    private readonly FunnelShape _funnel;
    private readonly VortexShape _vortex;
    private readonly bool _wideFunnel;
    private float _time;
    private float? _formAt;
    private float? _dissolveAt;
    private float _lastPulseAt = -10f;
    private float _lastFlingAt = -10f;
    private int _hitCount;
    private bool _backReleased;

    private StormCycloneVfx(
        Node2D root,
        Node2D backRoot,
        NCombatRoom room,
        Form form,
        IReadOnlyList<Creature> targets,
        CelVfxGeometry.CasterAnchor? caster,
        int windCards,
        bool wideFunnel)
        : base(root, room)
    {
        _form = form;
        _palette = form == Form.Funnel ? ClowPalette : SakuraPalette;
        _backRoot = backRoot;
        _wideFunnel = wideFunnel;

        // Both roots stay in combat-container coordinates: every point below is
        // built directly in that space from the hitbox rectangles.
        root.GlobalPosition = Vector2.Zero;
        root.Scale = Vector2.One;
        backRoot.GlobalPosition = Vector2.Zero;
        backRoot.Scale = Vector2.One;

        _template = root.GetNode<MeshInstance2D>("%StrokeTemplate");
        _frontStrands = root.GetNode<Node2D>("%Strands");
        _strokeLayer = root.GetNode<Node2D>("%Strokes");
        _bitLayer = root.GetNode<Node2D>("%Bits");

        var envelopes = new List<CelVfxGeometry.TargetGeometry>(targets.Count);
        for (var index = 0; index < targets.Count; index++)
        {
            if (_targets.ContainsKey(targets[index]))
                continue;
            var geometry = CelVfxGeometry.Resolve(room, targets[index], index, Budget);
            _targets.Add(targets[index], geometry);
            envelopes.Add(geometry);
        }

        var viewport = room.CombatVfxContainer.GetViewportRect();
        var wind = Math.Clamp(windCards, 0, MaxResidualWinds);
        if (form == Form.Funnel)
        {
            _funnel = LayoutFunnel(envelopes, viewport, wind, wideFunnel);
            BuildFunnel(wind);
            if (caster is { } anchor)
                BuildResidualWinds(anchor, wind);
        }
        else
        {
            _vortex = LayoutVortex(envelopes, viewport);
            BuildVortex();
        }

        UpdateFrame(0f);

        CombatManager.Instance.CombatEnded += OnStormCombatEnded;
        root.TreeExiting += ReleaseBackRoot;
        backRoot.TreeExiting += OnBackTreeExiting;
    }

    private static CelVfxGeometry.GeometryBudget Budget => new(
        HorizontalPadding: 0f,
        VerticalPadding: 0f,
        MinWidth: 80f,
        MinHeight: 90f,
        MaxWidth: 360f,
        MaxHeight: 460f,
        FallbackWidth: 150f,
        FallbackHeight: 200f,
        FloorClearance: 0f,
        MaxViewportWidthFraction: 0.24f,
        MaxViewportHeightFraction: 0.50f);

    protected override IEnumerable<ShaderMaterial> Materials => _materials;

    protected override float MaximumLifetime => 15f;

    /// <summary>
    /// Where a rolled hit sits inside the card's damage range, 0 at the
    /// minimum and 1 at the maximum. A degenerate range reads as full strength.
    /// </summary>
    internal static float Strength(int amount, int min, int max) =>
        max <= min ? 1f : Math.Clamp((amount - min) / (float)(max - min), 0f, 1f);

    /// <summary>Strands in the funnel for a given number of earlier Wind cards.</summary>
    internal static int FunnelStrandCount(int windCards) =>
        3 + Math.Min(Math.Clamp(windCards, 0, MaxResidualWinds), 2);

    internal static Task PlayFunnelAsync(
        CardModel card,
        Creature caster,
        IReadOnlyList<Creature> targets,
        int windCards,
        bool wholeLine,
        Func<Cues, Task> resolveGameplay) =>
        PlayOrResolveAsync(card, caster, targets, Form.Funnel, windCards, wholeLine, resolveGameplay);

    internal static Task PlayVortexAsync(
        CardModel card,
        Creature caster,
        IReadOnlyList<Creature> targets,
        Func<Cues, Task> resolveGameplay) =>
        PlayOrResolveAsync(card, caster, targets, Form.Vortex, 0, false, resolveGameplay);

    private static Task PlayOrResolveAsync(
        CardModel card,
        Creature caster,
        IReadOnlyList<Creature> targets,
        Form form,
        int windCards,
        bool wholeLine,
        Func<Cues, Task> resolveGameplay)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(caster);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(resolveGameplay);

        return CelVfxSession.PlayOrResolveAsync(
            "Storm cyclone",
            () => TryCreate(caster, targets, form, windCards, wholeLine),
            session => session.PlayPrelude(card, caster),
            scope => resolveGameplay(new Cues(scope)),
            session => session.BeginDissolve(),
            session => session.DisposePresentation());
    }

    internal sealed class Cues(CueScope<StormCycloneVfx> scope)
    {
        /// <summary>
        /// The native hit effect to keep when the cyclone is not drawing, so a
        /// player with card VFX off (or a failed load) still sees every hit land.
        /// </summary>
        internal string? FallbackHitVfx => scope.IsLive ? null : SakuraNativeHitFx.Slash;

        /// <summary>A funnel hit on <paramref name="target"/>.</summary>
        internal void Hit(Creature target)
        {
            ArgumentNullException.ThrowIfNull(target);
            scope.Invoke("hit", session => session.Hit(target));
        }

        /// <summary>A vortex blade at <paramref name="target"/>, <paramref name="strength"/> in 0..1.</summary>
        internal void Strike(Creature target, float strength)
        {
            ArgumentNullException.ThrowIfNull(target);
            scope.Invoke("strike", session => session.Strike(target, strength));
        }
    }

    private static StormCycloneVfx? TryCreate(
        Creature caster,
        IReadOnlyList<Creature> targets,
        Form form,
        int windCards,
        bool wholeLine)
    {
        if (targets.Count == 0
            || !TryPrepare("Storm cyclone", LoadScene, out var room, out _, out var scene))
        {
            return null;
        }

        Node2D? root = null;
        Node2D? backRoot = null;
        try
        {
            root = scene.Instantiate<Node2D>();
            root.Name = "SakuraStormCycloneVfx";
            room.CombatVfxContainer.AddChildSafely(root);

            backRoot = new Node2D { Name = "SakuraStormCycloneBackVfx" };
            room.BackCombatVfxContainer.AddChildSafely(backRoot);

            var anchor = CelVfxGeometry.ResolveCaster(room.GetCreatureNode(caster));
            var session = new StormCycloneVfx(root, backRoot, room, form, targets, anchor, windCards, wholeLine);
            session.StartClock();
            return session;
        }
        catch (Exception exception)
        {
            LogLoadFailure(exception);
            root?.QueueFreeSafely();
            backRoot?.QueueFreeSafely();
            return null;
        }
    }

    private async Task<bool> PlayPrelude(CardModel card, Creature caster)
    {
        if (!await PlayCelPrelude(card, caster))
            return false;

        _formAt = _time;
        return await WaitActive(_form == Form.Funnel ? FunnelLead : VortexLead);
    }

    // --- Cues ------------------------------------------------------------------

    private void Hit(Creature target)
    {
        if (!IsActive() || _dissolveAt is not null)
            return;

        // The Extra effect strikes the whole line in one hit: each creature
        // reports here in the same frame, so the squeeze and fling happen once.
        var sameBeat = _lastPulseAt == _time;
        _lastPulseAt = _time;
        var index = _hitCount++;
        if (_targets.TryGetValue(target, out var geometry))
            AddCut(geometry, index, 1f, 4.2f);
        if (!sameBeat && _lastFlingAt != _time)
        {
            _lastFlingAt = _time;
            AddFling(index);
        }
    }

    private void Strike(Creature target, float strength)
    {
        if (!IsActive() || _dissolveAt is not null || !_targets.TryGetValue(target, out var geometry))
            return;

        _lastPulseAt = _time;
        strength = Math.Clamp(strength, 0f, 1f);
        var index = _hitCount++;
        var v = _vortex;
        var lean = index % 2 == 0 ? BladeLean : -BladeLean;
        var theta = MathF.Acos(Math.Clamp((geometry.Center.X - lean - v.Cx) / v.R, -0.95f, 0.95f));
        var start = new Vector2(v.Cx + v.R * MathF.Cos(theta), v.Cy + v.R * v.K * MathF.Sin(theta));
        var end = geometry.Center + new Vector2(RandomRange(-20f, 20f), RandomRange(-30f, 10f));
        var dir = (end - start).LengthSquared() > 1f ? (end - start).Normalized() : Vector2.Down;
        var side = dir.Orthogonal() * (index % 2 == 0 ? 22f : -22f);
        var over = end + dir * 70f;
        var span = start.DistanceTo(over);
        var path = CelRibbonPaths.Resample(
            Cubic(start, start + dir * span * 0.33f + side, over - dir * span * 0.33f + side * 0.4f, over, 24),
            Segments);

        if (CanAddTransient())
        {
            var travel = TravelTime(ClosestS(path, end), BladeArrival);
            var material = AddStroke(_strokeLayer, path, Mathf.Lerp(6f, 16f, strength), out var node);
            SetParameters(material,
                ("tip_px", 60f + 40f * strength), ("tail_pow", 1.1f), ("line_px", 2.5f + 2f * strength),
                ("wave_amp", 0.25f), ("wave_px", 60f), ("body_alpha", 0.9f), ("softness", 0.35f));
            _transient.Add(new Stroke(node, material, StrokeKind.Travel, _time)
            {
                Travel = travel, Lag = BladeLag
            });
        }

        AddCut(geometry, index, Mathf.Lerp(0.6f, 1.2f, strength), Mathf.Lerp(3.2f, 5f, strength), end);

        var petals = 3 + (int)MathF.Round(5f * strength);
        for (var i = 0; i < petals && _bursts.Count < MaxBurstPetals; i++)
        {
            var node = new Polygon2D
            {
                Polygon = PetalPolygon,
                Color = i % 2 == 0 ? new Color(1f, 0.80f, 0.88f) : new Color(1f, 0.93f, 0.96f),
                Scale = Vector2.One * RandomRange(1.5f, 2.2f),
                Visible = false,
            };
            _bitLayer.AddChild(node);
            var velocity = dir.Rotated(RandomRange(-1.1f, 1.1f)) * RandomRange(260f, 480f) + new Vector2(0f, -180f);
            _bursts.Add(new BurstPetal(node, end, velocity, _time, RandomRange(-14f, 14f)));
        }
    }

    private void BeginDissolve()
    {
        if (!IsActive())
        {
            DisposePresentation();
            return;
        }

        // The cyclone dissolves on its own clock; gameplay never waits for it.
        _dissolveAt ??= _time;
    }

    // --- Layout ----------------------------------------------------------------

    private static FunnelShape LayoutFunnel(
        IReadOnlyList<CelVfxGeometry.TargetGeometry> targets, Rect2 viewport, int wind, bool wide)
    {
        if (!wide)
        {
            var target = targets[0];
            var w = target.Size.X;
            var h = target.Size.Y;
            var floor = target.Center.Y + h * 0.5f;
            return new FunnelShape(
                target.Center.X, w * 0.24f + 6f * wind, w * 0.64f + 14f * wind, floor - 8f,
                floor - h * 1.22f - 20f * wind, 0.24f, 10f, 22f);
        }

        var left = targets.Min(static t => t.Center.X - t.Size.X * 0.5f);
        var right = targets.Max(static t => t.Center.X + t.Size.X * 0.5f);
        var bottom = targets.Max(static t => t.Center.Y + t.Size.Y * 0.5f);
        var tallest = targets.Max(static t => t.Size.Y);
        var width = Math.Max(right - left, 160f);
        var cx = (left + right) * 0.5f;
        // The top ring stays inside the viewport on both sides.
        var room = Math.Min(cx - viewport.Position.X, viewport.End.X - cx) - 60f;
        var rb = width * 0.47f;
        var rt = Math.Max(rb, Math.Min(width * 0.55f, room));
        return new FunnelShape(cx, rb, rt, bottom - 6f, bottom - tallest * 1.08f - 14f * wind, 0.17f, 6.5f, 24f);
    }

    private static VortexShape LayoutVortex(IReadOnlyList<CelVfxGeometry.TargetGeometry> targets, Rect2 viewport)
    {
        var left = targets.Min(static t => t.Center.X - t.Size.X * 0.5f);
        var right = targets.Max(static t => t.Center.X + t.Size.X * 0.5f);
        var top = targets.Min(static t => t.Center.Y - t.Size.Y * 0.5f);
        var cx = (left + right) * 0.5f;
        var room = Math.Min(cx - viewport.Position.X, viewport.End.X - cx) - 40f;
        var radius = Math.Clamp(Math.Max(260f, (right - left) * 0.55f), 180f, Math.Max(180f, room));
        var cy = Math.Max(viewport.Position.Y + 150f, top - 150f);
        return new VortexShape(cx, cy, radius, 0.30f, -5f, 0.9f);
    }

    private void BuildFunnel(int wind)
    {
        var count = FunnelStrandCount(wind);
        var halfWidth = (_wideFunnel ? 15f : 11f) + 1.5f * wind;
        for (var j = 0; j < count; j++)
        {
            var phase = MathF.Tau * j / count + RandomRange(-0.2f, 0.2f);
            var turns = (_wideFunnel ? 1.25f : 1.7f) + 0.12f * j;
            var strand = AddStrand(halfWidth * (1f - 0.10f * j), j, phase, turns,
                StrandStart + FormStagger * j, FormGrow, 0f, 1f, 0f);
            SetParameters(strand.Material,
                ("tip_px", 170f), ("tail_pow", 1.2f), ("line_px", 2f), ("wave_amp", 0.42f), ("wave_px", 80f),
                ("body_alpha", 0.62f), ("softness", 0.7f), ("fade_px", 120f), ("end_fade_px", 90f));
            _strands.Add(strand);

            // A thin needle splits off each strand near the rim and peels away.
            var wisp = AddStrand(3f, j, phase + 0.35f, turns,
                StrandStart + FormStagger * j + FormGrow * 0.75f, WispGrow, 0.72f, 1.16f,
                _wideFunnel ? 0.18f : 0.32f, dissolveShare: 0.6f);
            SetParameters(wisp.Material,
                ("tip_px", 70f), ("tail_pow", 1f), ("line_px", 3f), ("wave_amp", 0f), ("body_alpha", 0.8f),
                ("softness", 0.2f), ("end_fade_px", 70f));
            _wisps.Add(wisp);
        }

        for (var i = 0; i < 10 + 4 * wind; i++)
        {
            var node = new Polygon2D
            {
                Polygon = LeafPolygon,
                Color = i % 3 != 0 ? new Color(0.80f, 0.88f, 0.80f, 0.95f) : new Color(0.93f, 0.92f, 0.84f, 0.95f),
                Visible = false,
            };
            _bitLayer.AddChild(node);
            _orbiters.Add(new Orbiter(node)
            {
                Level = RandomRange(0.12f, 0.95f), Phase = RandomRange(0f, MathF.Tau),
                Radius = RandomRange(1f, 1.18f), Spin = RandomRange(6f, 12f),
            });
        }
    }

    private void BuildVortex()
    {
        for (var j = 0; j < 3; j++)
        {
            var arm = AddStrand(17f - 3f * j, j, MathF.Tau * j / 3f, 0f, VortexStagger * j, VortexGrow, 0f, 1f, 0f);
            SetParameters(arm.Material,
                ("tip_px", 80f), ("tail_pow", 1.2f), ("line_px", 2f), ("wave_amp", 0.42f), ("wave_px", 85f),
                ("body_alpha", 0.75f), ("softness", 0.7f), ("fade_px", 120f), ("end_fade_px", 60f));
            _strands.Add(arm);
        }

        for (var i = 0; i < 16; i++)
        {
            var node = new Polygon2D
            {
                Polygon = PetalPolygon,
                Color = new Color(1f, 0.82f, 0.89f, 0.95f),
                Visible = false,
            };
            _bitLayer.AddChild(node);
            _orbiters.Add(new Orbiter(node)
            {
                Level = RandomRange(0.25f, 1.08f), Phase = RandomRange(0f, MathF.Tau),
                Spin = RandomRange(6f, 12f), Fall = RandomRange(0f, 1f),
            });
        }
    }

    private void BuildResidualWinds(CelVfxGeometry.CasterAnchor caster, int wind)
    {
        var f = _funnel;
        var launch = new Vector2(
            caster.BodyCenter.X + caster.FacingSign * caster.BodySize.X * 0.5f,
            caster.Floor.Y + (caster.BodyCenter.Y - caster.Floor.Y) * 0.72f);
        // The wind joins the funnel base on the side facing the caster and
        // wraps around its near (lower) half.
        var sign = f.Cx >= launch.X ? 1f : -1f;
        var joinTheta = sign > 0f ? MathF.PI : 0f;
        var endTheta = sign > 0f ? 0.15f : MathF.PI - 0.15f;
        var ringY = f.BaseY - 6f;
        var join = new Vector2(f.Cx + f.Rb * 1.05f * MathF.Cos(joinTheta), ringY);
        for (var i = 0; i < wind; i++)
        {
            var start = launch + new Vector2(ResidualLifts[i].X * sign, ResidualLifts[i].Y);
            var span = start.DistanceTo(join);
            var arch = -140f - 50f * i;
            var approach = Cubic(start, start + new Vector2(sign * span * 0.35f, arch),
                join + new Vector2(-sign * span * 0.3f, arch * 0.4f), join, 40);
            var points = new List<Vector2>(approach[..^1]);
            for (var a = 0; a <= 12; a++)
            {
                var theta = Mathf.Lerp(joinTheta, endTheta, a / 12f);
                points.Add(new Vector2(f.Cx + f.Rb * 1.05f * MathF.Cos(theta), ringY + f.Rb * f.K * MathF.Sin(theta)));
            }

            var material = AddStroke(_strokeLayer, CelRibbonPaths.Resample(points, Segments), 8f, out var node);
            SetParameters(material,
                ("tip_px", 50f), ("tail_pow", 1.3f), ("line_px", 1.8f), ("wave_amp", 0.4f), ("wave_px", 70f),
                ("body_alpha", 0.6f), ("softness", 0.7f), ("fade_px", 160f));
            _residual.Add(new Stroke(node, material, StrokeKind.Travel, ResidualStagger * i)
            {
                Travel = ResidualTravel, Lag = ResidualLag
            });
        }
    }

    // --- Per-hit strokes -----------------------------------------------------

    private bool CanAddTransient() => _transient.Count < MaxTransientStrokes;

    private void AddCut(
        CelVfxGeometry.TargetGeometry target, int index, float lengthShare, float halfWidth, Vector2? at = null)
    {
        if (!CanAddTransient())
            return;

        var axis = Vector2.Right.Rotated(CutAngles[index % CutAngles.Length]);
        var half = target.Size.X * 0.5f * lengthShare * (1.1f - 0.08f * (index % 3));
        var mid = at ?? target.Center + CutOffsets[index % CutOffsets.Length];
        var material = AddStroke(_strokeLayer, CelRibbonPaths.Resample([mid - axis * half, mid + axis * half], 24),
            halfWidth, out var node);
        SetParameters(material,
            ("tip_px", 40f), ("tail_pow", 1f), ("line_px", 6f), ("wave_amp", 0f), ("body_alpha", 1f),
            ("softness", 0f));
        _transient.Add(new Stroke(node, material, StrokeKind.Cut, _time));
    }

    private void AddFling(int index)
    {
        if (!CanAddTransient())
            return;

        var f = _funnel;
        var side = index % 2 == 0 ? 1f : -1f;
        var level = 0.45f + 0.1f * (index % 3);
        var theta = MathF.PI * 0.5f - side * 0.9f;
        var rim = FunnelPoint(f, level, theta, _time, 1f);
        var outward = new Vector2(MathF.Cos(theta), -0.3f).Normalized();
        var tangent = new Vector2(-MathF.Sin(theta), f.K * MathF.Cos(theta)).Normalized() * MathF.Sign(f.Omega);
        var dir = (outward * 0.65f + tangent * 0.75f).Normalized();
        var reach = _wideFunnel ? 300f : 220f;
        var finish = rim + dir * reach + new Vector2(0f, -50f);
        var path = CelRibbonPaths.Resample(
            Cubic(rim, rim + tangent * reach * 0.4f, finish - dir * reach * 0.3f + new Vector2(0f, 20f), finish, 24),
            Segments);
        var material = AddStroke(_strokeLayer, path, 2.6f, out var node);
        SetParameters(material,
            ("tip_px", 36f), ("tail_pow", 1f), ("line_px", 4f), ("wave_amp", 0f), ("body_alpha", 0.75f),
            ("softness", 0.2f));
        _transient.Add(new Stroke(node, material, StrokeKind.Travel, _time) { Travel = FlingTravel, Lag = FlingLag });
    }

    // --- Frame -----------------------------------------------------------------

    protected override void OnFrame(float delta)
    {
        _time += delta;
        UpdateFrame(_time);

        if (_dissolveAt is { } dissolveAt
            && _time >= dissolveAt + DissolveTime + DissolveStagger * 5f
            && _transient.Count == 0
            && _bursts.Count == 0)
        {
            DisposePresentation();
        }
    }

    private void UpdateFrame(float time)
    {
        var pulse = _lastPulseAt <= time ? MathF.Exp(-(time - _lastPulseAt) / PulseDecay) : 0f;
        var formed = _formAt is { } formAt ? time - formAt : -1f;
        var dissolving = _dissolveAt is { } dissolveAt ? time - dissolveAt : -1f;
        var fade = 1f - Math.Clamp(dissolving / DissolveTime, 0f, 1f);

        var lead = 0f;
        foreach (var strand in _strands)
        {
            Sample(strand, time, pulse);
            var head = StrandHead(strand, formed);
            if (strand.Index == 0)
                lead = head;
            ApplyWindow(strand, head, dissolving, _form == Form.Funnel ? pulse : pulse * 0.6f);
        }

        foreach (var wisp in _wisps)
        {
            Sample(wisp, time, pulse);
            ApplyWindow(wisp, StrandHead(wisp, formed), dissolving, pulse);
        }

        foreach (var stroke in _residual)
            Window(stroke.Material, formed - stroke.Start, stroke.Travel, stroke.Lag);

        for (var i = _transient.Count - 1; i >= 0; i--)
        {
            var stroke = _transient[i];
            var local = time - stroke.Start;
            if (stroke.Kind == StrokeKind.Cut)
            {
                stroke.Material.SetShaderParameter("head", Math.Clamp(local / CutDraw, 0f, 1f));
                stroke.Material.SetShaderParameter(
                    "tail", Math.Clamp((local - CutEraseStart) / (CutEraseEnd - CutEraseStart), 0f, 1f));
            }
            else
            {
                Window(stroke.Material, local, stroke.Travel, stroke.Lag);
            }

            if (time >= stroke.End)
            {
                _transient.RemoveAt(i);
                _materials.Remove(stroke.Material);
                stroke.Node.QueueFreeSafely();
            }
        }

        UpdateOrbiters(time, pulse, lead, dissolving, fade);
        UpdateBursts(time);
    }

    private static float StrandHead(Strand strand, float formed) =>
        formed > strand.Delay ? Ease((formed - strand.Delay) / strand.Grow) : 0f;

    private static void ApplyWindow(Strand strand, float head, float dissolving, float flash)
    {
        var after = dissolving - DissolveStagger * strand.Index;
        var tail = dissolving >= 0f && after > 0f ? Ease(after / (DissolveTime * strand.DissolveShare)) : 0f;
        strand.Material.SetShaderParameter("head", head);
        strand.Material.SetShaderParameter("tail", tail);
        strand.Material.SetShaderParameter("flash", flash);
    }

    private void UpdateOrbiters(float time, float pulse, float lead, float dissolving, float fade)
    {
        foreach (var orbiter in _orbiters)
        {
            Vector2 position;
            float theta;
            if (_form == Form.Funnel)
            {
                var f = _funnel;
                var level = orbiter.Level + Math.Max(dissolving, 0f) * 1.2f;
                theta = orbiter.Phase + f.Omega * 0.9f * time;
                var point = FunnelPoint(f, level, theta, time, pulse);
                position = new Vector2(f.Cx + (point.X - f.Cx) * orbiter.Radius, point.Y);
            }
            else
            {
                var v = _vortex;
                theta = orbiter.Phase + v.Omega * 1.2f * time;
                var r = v.R * orbiter.Level;
                var drop = Math.Max(dissolving, 0f) * 260f * (0.5f + orbiter.Fall);
                position = new Vector2(v.Cx + r * MathF.Cos(theta), v.Cy + r * v.K * MathF.Sin(theta) + drop);
            }

            var near = Mathf.SmoothStep(-0.35f, 0.35f, MathF.Sin(theta));
            var reveal = _form == Form.Funnel
                ? Math.Clamp((lead - orbiter.Level) * 5f, 0f, 1f)
                : Math.Clamp((lead - orbiter.Level * 0.8f) * 4f, 0f, 1f);
            var alpha = reveal * fade * Mathf.Lerp(_form == Form.Funnel ? 0.35f : 0.4f, 1f, near);
            orbiter.Node.Visible = alpha > 0.001f;
            orbiter.Node.Position = position;
            orbiter.Node.Rotation = orbiter.Phase + time * orbiter.Spin;
            orbiter.Node.Modulate = new Color(1f, 1f, 1f, alpha);
        }
    }

    private void UpdateBursts(float time)
    {
        for (var i = _bursts.Count - 1; i >= 0; i--)
        {
            var petal = _bursts[i];
            var since = time - petal.Start;
            if (since >= BurstLife)
            {
                _bursts.RemoveAt(i);
                petal.Node.QueueFreeSafely();
                continue;
            }

            petal.Node.Visible = since >= 0f;
            petal.Node.Position = petal.Origin + petal.Velocity * since
                + new Vector2(0f, 0.5f * BurstGravity * since * since);
            petal.Node.Rotation = since * petal.Spin;
            petal.Node.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.SmoothStep(BurstLife * 0.5f, BurstLife, since));
        }
    }

    // --- Strands -----------------------------------------------------------------

    private static Vector2[] BuildStrandUvs()
    {
        var uvs = new Vector2[StrandVertexCount];
        for (var i = 0; i <= Segments; i++)
        {
            var s = i / (float)Segments;
            uvs[i * 2] = new Vector2(s, 0f);
            uvs[i * 2 + 1] = new Vector2(s, 1f);
        }
        return uvs;
    }

    private Strand AddStrand(
        float halfWidth, int index, float phase, float turns, float delay, float grow,
        float v0, float v1, float flare, float dissolveShare = 1f)
    {
        var material = NewMaterial(halfWidth);
        var front = new ArrayMesh();
        var back = new ArrayMesh();
        _frontStrands.AddChild(new MeshInstance2D { Mesh = front, Material = material });
        _backRoot.AddChild(new MeshInstance2D { Mesh = back, Material = material });
        return new Strand(material, halfWidth, front, back)
        {
            Index = index, Phase = phase, Turns = turns, Delay = delay, Grow = grow,
            V0 = v0, V1 = v1, Flare = flare, DissolveShare = dissolveShare,
        };
    }

    private void Sample(Strand strand, float time, float pulse)
    {
        for (var i = 0; i <= Segments; i++)
        {
            var u = i / (float)Segments;
            float theta;
            Vector2 point;
            if (_form == Form.Funnel)
            {
                var f = _funnel;
                var level = Mathf.Lerp(strand.V0, strand.V1, u);
                theta = strand.Phase + strand.Turns * MathF.Tau * level + f.Omega * time;
                point = FunnelPoint(f, level, theta, time, pulse);
                if (strand.Flare > 0f)
                {
                    // Peel outward and up past the rim, so the top splits into wisps.
                    var peel = Mathf.SmoothStep(0.9f, strand.V1, level);
                    point.X = f.Cx + (point.X - f.Cx) * (1f + strand.Flare * peel);
                    point.Y -= 60f * strand.Flare * peel;
                }
            }
            else
            {
                var v = _vortex;
                var r = v.R * (0.10f + 0.90f * u) * (1f - 0.08f * pulse);
                theta = strand.Phase + v.Omega * time + v.Twist * MathF.Tau * u;
                point = new Vector2(v.Cx + r * MathF.Cos(theta), v.Cy + r * v.K * MathF.Sin(theta));
            }

            strand.Points[i] = point;
            strand.Depths[i] = MathF.Sin(theta);
        }

        var count = strand.Points.Length;
        var vertices = strand.Vertices;
        var front = strand.FrontColors;
        var back = strand.BackColors;
        var length = 0f;
        for (var i = 0; i < count; i++)
        {
            if (i > 0)
                length += strand.Points[i - 1].DistanceTo(strand.Points[i]);
            var tangent = strand.Points[Math.Min(i + 1, count - 1)] - strand.Points[Math.Max(i - 1, 0)];
            var normal = tangent.LengthSquared() > 0f ? tangent.Normalized().Orthogonal() : Vector2.Up;
            var depth = strand.Depths[i];
            // Edge-on at the silhouette (sin = 0) the band narrows, like a twisted ribbon.
            var halfWidth = strand.HalfWidth * Mathf.Lerp(0.35f, 1f, MathF.Abs(depth));
            vertices[i * 2] = strand.Points[i] + normal * halfWidth;
            vertices[i * 2 + 1] = strand.Points[i] - normal * halfWidth;
            var near = Mathf.SmoothStep(-0.35f, 0.35f, depth);
            front[i * 2] = front[i * 2 + 1] = new Color(1f, 1f, 1f, near);
            back[i * 2] = back[i * 2 + 1] = new Color(BackTint, (1f - near) * 0.55f);
        }

        Rebuild(strand.Front, vertices, front);
        Rebuild(strand.Back, vertices, back);
        strand.Material.SetShaderParameter("path_length_px", Math.Max(length, 1f));
    }

    private static void Rebuild(ArrayMesh mesh, Vector2[] vertices, Color[] colors)
    {
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.TexUV] = StrandUvs;
        arrays[(int)Mesh.ArrayType.Color] = colors;
        mesh.ClearSurfaces();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.TriangleStrip, arrays);
    }

    private static Vector2 FunnelPoint(FunnelShape f, float level, float theta, float time, float pulse)
    {
        var radius = Mathf.Lerp(f.Rb, f.Rt, MathF.Pow(Math.Max(level, 0f), 1.25f))
            * (1f - 0.16f * pulse * (1f - 0.5f * level));
        var y = Mathf.Lerp(f.BaseY, f.TopY, level);
        var sway = f.Sway * MathF.Sin(level * 2.6f + time * 2.4f) * level;
        return new Vector2(f.Cx + sway + radius * MathF.Cos(theta), y + radius * f.K * MathF.Sin(theta));
    }

    // --- Materials ---------------------------------------------------------------

    private ShaderMaterial NewMaterial(float halfWidth)
    {
        // Every uniform any frame touches is written now: a never-set uniform
        // starts from Nil (cel-vfx-silent-visual.md).
        var material = CelVfxGeometry.DuplicateMaterial(_template, "Storm stroke");
        material.SetShaderParameter("edge_colour", _palette.Edge);
        material.SetShaderParameter("body_colour", _palette.Body);
        material.SetShaderParameter("shade_colour", _palette.Shade);
        SetParameters(material,
            ("head", 0f), ("tail", 0f), ("path_length_px", 1f), ("half_width_px", halfWidth),
            ("clean_side", 0f), ("fade_px", 0f), ("end_fade_px", 0f), ("flash", 0f), ("alpha", 1f),
            ("seed", (float)_random.NextDouble()), ("elapsed", 0f));
        _materials.Add(material);
        return material;
    }

    private ShaderMaterial AddStroke(Node2D layer, Vector2[] points, float halfWidth, out MeshInstance2D node)
    {
        var strip = CelRibbonPaths.BuildStrip(new RibbonPath(points, [], Closed: false), halfWidth);
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = strip.Vertices;
        arrays[(int)Mesh.ArrayType.TexUV] = strip.Uvs;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.TriangleStrip, arrays);

        var material = NewMaterial(halfWidth);
        material.SetShaderParameter("path_length_px", Math.Max(Length(points), 1f));
        node = new MeshInstance2D { Mesh = mesh, Material = material };
        layer.AddChild(node);
        return material;
    }

    private static void SetParameters(ShaderMaterial material, params (string Name, float Value)[] values)
    {
        foreach (var (name, value) in values)
            material.SetShaderParameter(name, value);
    }

    // --- Math --------------------------------------------------------------------

    private float RandomRange(float min, float max) => min + (float)_random.NextDouble() * (max - min);

    private static void Window(ShaderMaterial material, float local, float travel, float lag)
    {
        material.SetShaderParameter("head", local > 0f ? Ease(local / travel) : 0f);
        material.SetShaderParameter("tail", local > lag ? Ease((local - lag) / travel) : 0f);
    }

    private static Vector2[] Cubic(Vector2 a, Vector2 b, Vector2 c, Vector2 d, int samples)
    {
        var points = new Vector2[samples + 1];
        for (var i = 0; i <= samples; i++)
        {
            var t = i / (float)samples;
            var u = 1f - t;
            points[i] = u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
        }
        return points;
    }

    private static float Length(Vector2[] points)
    {
        var length = 0f;
        for (var i = 1; i < points.Length; i++)
            length += points[i - 1].DistanceTo(points[i]);
        return length;
    }

    private static float ClosestS(Vector2[] points, Vector2 target)
    {
        var best = 0;
        for (var i = 1; i < points.Length; i++)
        {
            if (points[i].DistanceSquaredTo(target) < points[best].DistanceSquaredTo(target))
                best = i;
        }
        return best / (float)(points.Length - 1);
    }

    private static float Ease(float u) => 1f - MathF.Pow(1f - Math.Clamp(u, 0f, 1f), EasePower);

    // Travel time that puts the head on `hit` after `arrival` seconds.
    private static float TravelTime(float hit, float arrival) =>
        arrival / (1f - MathF.Pow(1f - Math.Clamp(hit, 0.05f, 0.95f), 1f / EasePower));

    // --- Lifecycle ---------------------------------------------------------------

    private void DisposePresentation()
    {
        ReleaseBackRoot();
        Dispose();
    }

    private void OnStormCombatEnded(CombatRoom _) => ReleaseBackRoot();
    private void OnBackTreeExiting() => DisposePresentation();

    private void ReleaseBackRoot()
    {
        if (_backReleased)
            return;

        _backReleased = true;
        CombatManager.Instance.CombatEnded -= OnStormCombatEnded;
        if (GodotObject.IsInstanceValid(Root))
            Root.TreeExiting -= ReleaseBackRoot;
        if (GodotObject.IsInstanceValid(_backRoot))
        {
            _backRoot.TreeExiting -= OnBackTreeExiting;
            if (!_backRoot.IsQueuedForDeletion())
                _backRoot.QueueFreeSafely();
        }
    }

    private static PackedScene LoadScene() => PreloadManager.Cache.GetScene(ScenePath);

    private static void LogLoadFailure(Exception exception)
    {
        if (_loadFailureLogged)
            return;

        _loadFailureLogged = true;
        MainFile.Logger.Error(
            $"Could not create Storm cyclone VFX from {ScenePath} and {ShaderPath}: {exception}");
    }
}
