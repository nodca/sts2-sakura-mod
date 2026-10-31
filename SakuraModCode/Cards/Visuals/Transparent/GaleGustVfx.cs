using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
/// Gale's gust: three card-art wind ribbons whip along different arcs from
/// Sakura through the target and curl away behind it, with two needle streaks,
/// a few carried petals and hairline cuts on impact. Each stroke is a window
/// sliding along its own curved path, so the ribbon bends with its route.
/// Motion is smooth by design (the one departure from the 12 Hz cadence):
/// stepping is what made the old crescent read as slow and heavy.
/// scripts/render_gale_vfx.gd mirrors the geometry and timeline below.
/// </summary>
internal sealed class GaleGustVfx : CelVfxSession
{
    internal const string ScenePath =
        MainFile.ResPath + "/scenes/combat/card_vfx/gale_gust_vfx.tscn";
    internal const string ShaderPath =
        MainFile.ResPath + "/shaders/card_vfx/gale_gust_stroke.gdshader";
    internal static IReadOnlyList<string> AssetPaths { get; } = [ScenePath, ShaderPath];

    private const float LaunchBodyBias = 0.72f;
    // The lead ribbon's head reaches the target this long after the prelude.
    private const float Arrival = 0.12f;
    // Ease-out power of every head: the gust starts at full speed and slows.
    private const float EasePower = 2f;
    private const int Segments = 96;
    private const float FlashDecay = 0.08f;
    private const float PetalTravel = 0.36f;
    private const float PetalSpin = 9f;
    private const float CutDraw = 0.035f;
    private const float CutEraseStart = 0.025f;
    private const float CutEraseEnd = 0.10f;
    private const float FinalFade = 0.06f;
    private const int VfxZIndex = 3000;

    private readonly record struct RibbonSpec(
        float Delay, float HalfWidth, float LaunchLift, float HitLift,
        float Sag, float Overshoot, float Rise, float Lag, float TipPx);

    private readonly record struct StreakSpec(
        float Delay, float HalfWidth, float Lift, float Overshoot, float Rise, float Sag, float Lag);

    private readonly record struct PetalSpec(int Ribbon, float Delay, float Offset, float Sway, float Phase);

    private readonly record struct CutSpec(float Angle, float LengthShare, Vector2 Offset, float Delay);

    // Lead ribbon arches above the line, the second arches high, the third
    // skims low: different arcs keep the gust from reading as speed lines.
    private static readonly RibbonSpec[] Ribbons =
    [
        new(0.000f, 34f, 10f, 6f, -95f, 250f, 190f, 0.080f, 90f),
        new(0.022f, 20f, 44f, 52f, -190f, 200f, 250f, 0.070f, 70f),
        new(0.040f, 15f, -30f, -40f, 70f, 300f, 110f, 0.065f, 60f),
    ];

    private static readonly StreakSpec[] Streaks =
    [
        new(0.008f, 2.0f, -78f, 360f, 30f, -30f, 0.045f),
        new(0.030f, 2.4f, 150f, 300f, 90f, -60f, 0.050f),
    ];

    private static readonly PetalSpec[] Petals =
    [
        new(0, 0.010f, -18f, 10f, 0.3f),
        new(1, 0.030f, 22f, 14f, 1.7f),
        new(0, 0.055f, 30f, 8f, 3.1f),
        new(2, 0.045f, -26f, 12f, 4.4f),
    ];

    private static readonly CutSpec[] Cuts =
    [
        new(0.95f, 1.25f, new Vector2(-6f, -14f), 0.000f),
        new(-1.05f, 1.05f, new Vector2(12f, 10f), 0.014f),
        new(0.55f, 0.85f, new Vector2(-18f, 34f), 0.028f),
    ];

    private static readonly Vector2[] PetalPolygon =
    [
        new(-7f, 0f), new(-3f, -3.8f), new(3f, -4f), new(7f, -1.6f),
        new(5.2f, 0f), new(7f, 1.6f), new(3f, 4f), new(-3f, 3.8f),
    ];

    private static bool _loadFailureLogged;

    private sealed class Stroke(ShaderMaterial material, Vector2[] points)
    {
        internal ShaderMaterial Material { get; } = material;
        internal Vector2[] Points { get; } = points;
        internal float Delay { get; init; }
        internal float Lag { get; init; }
        internal float Travel { get; init; }
    }

    private sealed class Petal(Polygon2D node, Vector2[] points, PetalSpec spec)
    {
        internal Polygon2D Node { get; } = node;
        internal Vector2[] Points { get; } = points;
        internal PetalSpec Spec { get; } = spec;
    }

    private readonly List<ShaderMaterial> _materials = [];
    private readonly List<Stroke> _ribbons = [];
    private readonly List<Stroke> _streaks = [];
    private readonly List<Stroke> _cuts = [];
    private readonly List<Petal> _petals = [];
    private readonly Vector2 _up;
    private readonly float _end;
    private float _time;
    private bool _impacted;
    private bool _faded;

    private GaleGustVfx(
        Node2D root,
        NCombatRoom room,
        CelVfxGeometry.CasterAnchor caster,
        CelVfxGeometry.TargetGeometry target)
        : base(root, room)
    {
        // The root stays in combat-container coordinates; every stroke is
        // built directly in that space from the runtime anchors.
        root.GlobalPosition = Vector2.Zero;
        root.Scale = Vector2.One;

        var template = root.GetNode<MeshInstance2D>("%StrokeTemplate");
        var launch = new Vector2(
            caster.BodyCenter.X + caster.FacingSign * caster.BodySize.X * 0.5f,
            caster.Floor.Y + (caster.BodyCenter.Y - caster.Floor.Y) * LaunchBodyBias);
        var centre = target.Center;
        var toTarget = centre - launch;
        var dir = toTarget.LengthSquared() > 1f ? toTarget.Normalized() : Vector2.Right;
        var up = dir.Orthogonal();
        if (up.Y > 0f)
            up = -up;
        _up = up;
        var distance = launch.DistanceTo(centre);
        // CelRibbonPaths puts UV.y = 0 on the tangent's Orthogonal() side; the
        // bright edge always faces screen-up whichever way the gust travels.
        var cleanSide = dir.Orthogonal().Dot(up) > 0f ? 0f : 1f;

        var ribbonLayer = root.GetNode<Node2D>("%Ribbons");
        foreach (var spec in Ribbons)
        {
            var points = RibbonPath(spec, launch, centre, dir, up, distance);
            var material = AddStroke(ribbonLayer, template, points, spec.HalfWidth, cleanSide);
            SetParameters(material,
                ("tip_px", spec.TipPx), ("tail_pow", 1.3f), ("line_px", 2f), ("wave_amp", 0.42f),
                ("wave_px", 95f), ("body_alpha", 0.62f), ("softness", 0.7f), ("fade_px", 220f));
            var hit = ClosestS(points, centre + up * spec.HitLift);
            _ribbons.Add(new Stroke(material, points)
            {
                Delay = spec.Delay, Lag = spec.Lag, Travel = TravelTime(hit, Arrival)
            });
        }

        var streakLayer = root.GetNode<Node2D>("%Streaks");
        foreach (var spec in Streaks)
        {
            var points = StreakPath(spec, launch, centre, dir, up);
            var material = AddStroke(streakLayer, template, points, spec.HalfWidth, cleanSide);
            SetParameters(material,
                ("tip_px", 30f), ("tail_pow", 1f), ("line_px", 4f), ("wave_amp", 0f),
                ("body_alpha", 0.7f), ("softness", 0.2f));
            _streaks.Add(new Stroke(material, points)
            {
                Delay = spec.Delay, Lag = spec.Lag, Travel = TravelTime(ClosestS(points, centre), Arrival * 0.9f)
            });
        }

        var cutLayer = root.GetNode<Node2D>("%Cuts");
        var targetWidth = Math.Max(target.Size.X, 120f);
        foreach (var spec in Cuts)
        {
            var axis = dir.Rotated(spec.Angle);
            var half = targetWidth * spec.LengthShare * 0.5f;
            var mid = centre + spec.Offset;
            var points = CelRibbonPaths.Resample([mid - axis * half, mid + axis * half], 24);
            var material = AddStroke(cutLayer, template, points, 3.2f, cleanSide);
            SetParameters(material,
                ("tip_px", 40f), ("tail_pow", 1f), ("line_px", 6f), ("wave_amp", 0f),
                ("body_alpha", 1f), ("softness", 0f));
            _cuts.Add(new Stroke(material, points) { Delay = spec.Delay });
        }

        var petalLayer = root.GetNode<Node2D>("%Petals");
        foreach (var spec in Petals)
        {
            var node = new Polygon2D
            {
                Polygon = PetalPolygon,
                Color = new Color(1f, 0.84f, 0.90f, 0.95f),
                Visible = false,
            };
            petalLayer.AddChild(node);
            _petals.Add(new Petal(node, _ribbons[spec.Ribbon].Points, spec));
        }

        _end = Math.Max(
            _ribbons.Concat(_streaks).Max(static stroke => stroke.Delay + stroke.Travel + stroke.Lag),
            Petals.Max(static petal => petal.Delay + PetalTravel));
        SetTimeline(0f);
    }

    private static CelVfxGeometry.GeometryBudget Budget => new(
        HorizontalPadding: 0f,
        VerticalPadding: 0f,
        MinWidth: 80f,
        MinHeight: 90f,
        MaxWidth: 320f,
        MaxHeight: 420f,
        FallbackWidth: 150f,
        FallbackHeight: 180f,
        FloorClearance: 0f,
        MaxViewportWidthFraction: 0.22f,
        MaxViewportHeightFraction: 0.45f);

    protected override IEnumerable<ShaderMaterial> Materials => _materials;

    protected override float MaximumLifetime => 4.0f;

    internal static Task PlayOrResolveAsync(
        CardModel card,
        Creature attacker,
        Creature target,
        Func<Cues, Task> resolveGameplay)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(resolveGameplay);

        return CelVfxSession.PlayOrResolveAsync(
            "Gale gust",
            () => TryCreate(attacker, target),
            session => session.PlayPrelude(card, attacker),
            scope => resolveGameplay(new Cues(scope)),
            session => session.FadeAndDispose(),
            session => session.Dispose());
    }

    internal sealed class Cues(CueScope<GaleGustVfx> scope)
    {
        internal void Impact() => scope.Invoke("impact", static session => session.Impact());
    }

    private static GaleGustVfx? TryCreate(Creature attacker, Creature target)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(target);
        if (!TryPrepare("Gale gust", LoadScene, out var room, out _, out var scene))
            return null;

        Node2D? root = null;
        try
        {
            if (CelVfxGeometry.ResolveCaster(room.GetCreatureNode(attacker)) is not { } caster)
                return null;

            root = scene.Instantiate<Node2D>();
            root.Name = "SakuraGaleGustVfx";
            root.ZAsRelative = false;
            root.ZIndex = VfxZIndex;
            room.CombatVfxContainer.AddChildSafely(root);

            var geometry = CelVfxGeometry.Resolve(room, target, 0, Budget);
            var session = new GaleGustVfx(root, room, caster, geometry);
            session.StartClock();
            return session;
        }
        catch (Exception exception)
        {
            LogLoadFailure(exception);
            root?.QueueFreeSafely();
            return null;
        }
    }

    private async Task<bool> PlayPrelude(CardModel card, Creature? caster)
    {
        if (!await PlayCelPrelude(card, caster))
            return false;

        var timeline = Track(Root.CreateTween());
        timeline.TweenMethod(Callable.From<float>(SetTimeline), 0f, _end, _end)
            .SetTrans(Tween.TransitionType.Linear);
        return await WaitActive(Arrival);
    }

    private void Impact()
    {
        if (_impacted || !IsActive())
            return;

        _impacted = true;
        var impact = Track(Root.CreateTween());
        var duration = Cuts.Max(static cut => cut.Delay) + CutEraseEnd;
        impact.TweenMethod(Callable.From<float>(SetImpact), 0f, duration, duration)
            .SetTrans(Tween.TransitionType.Linear);
    }

    private void FadeAndDispose()
    {
        if (_faded || !IsActive())
        {
            Dispose();
            return;
        }

        // The gust finishes on its own timeline; gameplay never waits for it.
        _faded = true;
        var fade = Track(Root.CreateTween());
        fade.TweenInterval(Math.Max(0f, _end - _time));
        fade.TweenProperty(Root, "modulate:a", 0f, FinalFade);
        fade.TweenCallback(Callable.From(Dispose));
    }

    private void SetTimeline(float time)
    {
        _time = time;
        foreach (var stroke in _ribbons.Concat(_streaks))
        {
            var local = time - stroke.Delay;
            stroke.Material.SetShaderParameter("head", local > 0f ? Ease(local / stroke.Travel) : 0f);
            stroke.Material.SetShaderParameter(
                "tail", local > stroke.Lag ? Ease((local - stroke.Lag) / stroke.Travel) : 0f);
        }

        foreach (var petal in _petals)
        {
            var local = time - petal.Spec.Delay;
            var u = Math.Clamp(local / PetalTravel, 0f, 1f);
            var s = Mathf.Lerp(0.04f, 1f, 1f - MathF.Pow(1f - u, 1.5f));
            petal.Node.Visible = local > 0f;
            petal.Node.Position = PointAt(petal.Points, s)
                + _up * (petal.Spec.Offset + MathF.Sin(local * 18f + petal.Spec.Phase) * petal.Spec.Sway);
            petal.Node.Rotation = petal.Spec.Phase + local * PetalSpin;
            petal.Node.Modulate = new Color(1f, 1f, 1f,
                Math.Clamp(local / 0.03f, 0f, 1f) * (1f - Mathf.SmoothStep(0.65f, 1f, u)));
        }
    }

    private void SetImpact(float sinceImpact)
    {
        var flash = Math.Clamp(1f - sinceImpact / FlashDecay, 0f, 1f);
        foreach (var stroke in _ribbons)
            stroke.Material.SetShaderParameter("flash", flash);

        foreach (var stroke in _cuts)
        {
            var local = sinceImpact - stroke.Delay;
            stroke.Material.SetShaderParameter("head", Math.Clamp(local / CutDraw, 0f, 1f));
            stroke.Material.SetShaderParameter(
                "tail", Math.Clamp((local - CutEraseStart) / (CutEraseEnd - CutEraseStart), 0f, 1f));
        }
    }

    private ShaderMaterial AddStroke(
        Node2D layer,
        MeshInstance2D template,
        Vector2[] points,
        float halfWidth,
        float cleanSide)
    {
        var strip = CelRibbonPaths.BuildStrip(new RibbonPath(points, [], Closed: false), halfWidth);
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = strip.Vertices;
        arrays[(int)Mesh.ArrayType.TexUV] = strip.Uvs;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.TriangleStrip, arrays);

        // Every uniform the timeline touches is written now: a never-set
        // uniform starts from Nil (cel-vfx-silent-visual.md).
        var material = CelVfxGeometry.DuplicateMaterial(template, "Gale stroke");
        SetParameters(material,
            ("head", 0f), ("tail", 0f), ("path_length_px", Length(points)),
            ("half_width_px", halfWidth), ("clean_side", cleanSide), ("fade_px", 0f),
            ("flash", 0f), ("alpha", 1f), ("seed", (float)Random.Shared.NextDouble()),
            ("elapsed", 0f));
        _materials.Add(material);
        layer.AddChild(new MeshInstance2D { Mesh = mesh, Material = material });
        return material;
    }

    private static void SetParameters(ShaderMaterial material, params (string Name, float Value)[] values)
    {
        foreach (var (name, value) in values)
            material.SetShaderParameter(name, value);
    }

    private static Vector2[] RibbonPath(
        RibbonSpec spec, Vector2 launch, Vector2 centre, Vector2 dir, Vector2 up, float distance)
    {
        // Two cubics: an arc into the target (above or below the line by Sag),
        // then on past it, curling upward as the gust spends itself.
        var start = launch + up * spec.LaunchLift;
        var hit = centre + up * spec.HitLift;
        var finish = centre + dir * spec.Overshoot + up * spec.Rise;
        var approach = Cubic(start, start + dir * distance * 0.35f - up * spec.Sag,
            hit - dir * distance * 0.30f - up * spec.Sag * 0.3f, hit, 32);
        var curl = Cubic(hit, hit + dir * spec.Overshoot * 0.6f, finish - up * spec.Rise * 0.55f, finish, 24);
        return CelRibbonPaths.Resample([.. approach[..^1], .. curl], Segments);
    }

    private static Vector2[] StreakPath(StreakSpec spec, Vector2 launch, Vector2 centre, Vector2 dir, Vector2 up)
    {
        var start = launch - dir * 40f + up * spec.Lift;
        var finish = centre + dir * spec.Overshoot + up * (spec.Lift * 0.6f + spec.Rise);
        var span = start.DistanceTo(finish);
        return CelRibbonPaths.Resample(
            Cubic(start, start + dir * span * 0.33f - up * spec.Sag, finish - dir * span * 0.33f - up * spec.Sag,
                finish, 24),
            Segments);
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

    private static Vector2 PointAt(Vector2[] points, float s)
    {
        var scaled = Math.Clamp(s, 0f, 1f) * (points.Length - 1);
        var index = Math.Min((int)scaled, points.Length - 2);
        return points[index].Lerp(points[index + 1], scaled - index);
    }

    private static float Ease(float u) => 1f - MathF.Pow(1f - Math.Clamp(u, 0f, 1f), EasePower);

    // Travel time that puts the head on `hit` after `arrival` seconds.
    private static float TravelTime(float hit, float arrival) =>
        arrival / (1f - MathF.Pow(1f - Math.Clamp(hit, 0.05f, 0.95f), 1f / EasePower));

    private static PackedScene LoadScene() =>
        PreloadManager.Cache.GetScene(ScenePath);

    private static void LogLoadFailure(Exception exception)
    {
        if (_loadFailureLogged)
            return;

        _loadFailureLogged = true;
        MainFile.Logger.Error(
            $"Could not create Gale gust VFX from {ScenePath} and {ShaderPath}: {exception}");
    }
}
