using Godot;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
/// Shade's dragon exists only as a shadow on the floor. Everything here lives in
/// the floor plane: x is combat-canvas x, z is depth toward the camera, and the
/// session projects a point to <c>(x, floorY + Projection·z)</c>.
/// </summary>
/// <remarks>
/// Pure geometry and timing, no nodes, so the contract suite can pin arrival order
/// and on-screen bounds. The render plate (<c>scripts/render_shade_vfx.gd</c>)
/// draws this same geometry through <see cref="ShadeGroundDragonMask.ShowPlate"/>,
/// so it cannot drift from the game.
/// </remarks>
internal static class ShadeGroundDragonShape
{
    /// <summary>Floor squash of the projected shadow onto the screen.</summary>
    internal const float Projection = 0.5f;

    // --- Path ---------------------------------------------------------------
    private const float OriginBehindCaster = 40f;
    private const float CentreDepth = 70f;
    internal const float Length = 700f;
    private const float WaveAmplitude = 26f;
    private const float WaveLength = 120f;
    private const float WaveSettle = 160f;
    internal const float FormedHead = 170f;
    // Turns toward the camera; kept tight so the homeward lane and its wings stay above the screen bottom.
    private const float TurnRadius = 70f;
    private const float PastLastEnemy = 150f;
    private const float ViewportMargin = 210f;
    private const float MinTurnPastLastEnemy = 40f;
    private const float ArrivalLead = 30f;
    private const float HomeOffset = 30f;
    private const float StopOvershoot = 100f;
    private const float EmptyTargetReach = 600f;

    // --- Speeds ---------------------------------------------------------------
    internal const float SpeedOut = 2600f;
    internal const float SpeedOutCap = 4000f;
    internal const float SpeedBack = 3400f;
    private const float Acceleration = 0.06f;

    // --- Beats (body time, after the shared prelude) --------------------------
    internal const float PoolOpenEnd = 0.12f;
    internal const float FormStart = 0.04f;
    internal const float FormEnd = 0.36f;
    internal const float SpreadStart = 0.20f;
    internal const float SpreadEnd = 0.40f;
    internal const float SpreadCue = 0.38f;
    internal const float Go = 0.40f;
    /// <summary>Ceiling for the last Weak: gameplay must not wait longer than this for any enemy.</summary>
    internal const float LastArrivalCap = 0.95f;
    internal const float AbsorbDuration = 0.22f;
    private const float AbsorbRegrowPool = 0.25f;
    private const float PoolCloseDuration = 0.15f;
    private const float DissolveDelay = 0.08f;
    internal const float DissolveDuration = 0.32f;
    internal const float RippleDuration = 0.38f;
    internal const float FadeDuration = 0.06f;
    private const float FlapHz = 3.2f;
    private const float MoteInterval = 0.022f;
    private const float MoteLife = 0.55f;

    private const int BodySamples = 80;
    private const int DissolveBlobs = 70;

    internal readonly record struct Layout(
        float Facing,
        float X0,
        float XEnd,
        float SU,
        float STurnEnd,
        float SHome,
        float SpeedOut,
        bool Extra,
        float[] ArrivalS);

    internal readonly record struct Timeline(
        float[] Arrivals,
        float TurnAt,
        float HomeAt,
        float AbsorbEnd,
        float DissolveStart,
        float DissolveEnd,
        float TailLeavesPool,
        float End);

    internal readonly record struct Frame(Vector2 P, Vector2 T, Vector2 N);

    // Rim is how strongly a piece lights the water-blue glow around it (0..1):
    // the head and wings carry the silhouette, so they glow most; the trailing
    // body, legs and ink fade toward plain shadow.
    internal readonly record struct Stroke(Vector2[] Points, float StartWidth, float EndWidth, float Alpha, float Rim);

    internal readonly record struct Blob(Vector2 Centre, float Radius, float Alpha, float Rim);

    internal sealed class Shapes
    {
        internal readonly List<(Vector2[] Points, float Alpha, float Rim)> Polygons = [];
        internal readonly List<Stroke> Strokes = [];
        internal readonly List<Blob> Blobs = [];

        internal void Clear()
        {
            Polygons.Clear();
            Strokes.Clear();
            Blobs.Clear();
        }
    }

    // --- Layout ---------------------------------------------------------------

    /// <param name="casterFloorX">Caster's floor x.</param>
    /// <param name="facing">+1 when the enemies are to the right of the caster.</param>
    /// <param name="enemyXs">Each target's floor x, in target order.</param>
    /// <param name="viewLeft">Visible canvas left edge.</param>
    /// <param name="viewRight">Visible canvas right edge.</param>
    /// <param name="extra">Whether the dragon turns and swims home.</param>
    internal static Layout For(
        float casterFloorX,
        float facing,
        IReadOnlyList<float> enemyXs,
        float viewLeft,
        float viewRight,
        bool extra)
    {
        facing = facing < 0f ? -1f : 1f;
        var x0 = casterFloorX - OriginBehindCaster * facing;
        var arrivalS = enemyXs
            .Select(x => Math.Max(FormedHead, facing * (x - x0) - ArrivalLead))
            .ToArray();
        var lastS = arrivalS.Length > 0 ? arrivalS.Max() : EmptyTargetReach;

        // Turn inside the screen; never before the head has passed the last enemy.
        var edge = facing > 0f ? viewRight - ViewportMargin : viewLeft + ViewportMargin;
        var wantedEnd = x0 + facing * (lastS + ArrivalLead + PastLastEnemy);
        var clampedEnd = facing > 0f ? Math.Min(wantedEnd, edge) : Math.Max(wantedEnd, edge);
        var su = Math.Max(facing * (clampedEnd - x0), lastS + MinTurnPastLastEnemy);
        var xEnd = x0 + facing * su;
        var sTurnEnd = su + MathF.PI * TurnRadius;
        var sHome = sTurnEnd + Math.Max(0f, su - HomeOffset);

        // Widely spaced enemies would hold gameplay too long at the base speed.
        var needed = (lastS - FormedHead) / (LastArrivalCap - Go - Acceleration * 0.5f);
        var speed = Math.Clamp(Math.Max(SpeedOut, needed), SpeedOut, SpeedOutCap);

        return new Layout(facing, x0, xEnd, su, sTurnEnd, sHome, speed, extra, arrivalS);
    }

    internal static Vector2 PathPoint(float s, in Layout layout)
    {
        if (!layout.Extra || s <= layout.SU)
        {
            var settle = layout.Extra ? Math.Clamp((layout.SU - s) / WaveSettle, 0f, 1f) : 1f;
            return new Vector2(
                layout.X0 + layout.Facing * s,
                CentreDepth + WaveAmplitude * MathF.Sin(s / WaveLength) * settle);
        }

        if (s <= layout.STurnEnd)
        {
            var a = (s - layout.SU) / TurnRadius;
            return new Vector2(
                layout.XEnd + layout.Facing * TurnRadius * MathF.Sin(a),
                CentreDepth + TurnRadius * (1f - MathF.Cos(a)));
        }

        var u = s - layout.STurnEnd;
        var home = Math.Max(1f, layout.SHome - layout.STurnEnd);
        var p = Math.Clamp(u / home, 0f, 1f);
        return new Vector2(
            layout.XEnd - layout.Facing * u,
            CentreDepth + 2f * TurnRadius * (1f - EaseInOut(p)) + 18f * MathF.Sin(u / WaveLength) * (1f - p));
    }

    internal static Frame FrameAt(float s, in Layout layout)
    {
        var a = PathPoint(s - 2f, layout);
        var b = PathPoint(s + 2f, layout);
        var tangent = b - a;
        tangent = tangent.LengthSquared() > 1e-6f ? tangent.Normalized() : new Vector2(layout.Facing, 0f);
        return new Frame(PathPoint(s, layout), tangent, new Vector2(-tangent.Y, tangent.X));
    }

    // --- Timing ---------------------------------------------------------------

    /// <summary>Body time at which the head reaches arc length <paramref name="s"/> on the way out.</summary>
    internal static float TimeAtOut(float s, in Layout layout)
    {
        var d = s - FormedHead;
        if (d <= 0f)
            return Go;
        var accelerating = layout.SpeedOut * Acceleration * 0.5f;
        return d <= accelerating
            ? Go + MathF.Sqrt(2f * Acceleration * d / layout.SpeedOut)
            : Go + Acceleration * 0.5f + d / layout.SpeedOut;
    }

    internal static Timeline TimelineFor(in Layout layout)
    {
        var arrivals = new float[layout.ArrivalS.Length];
        for (var i = 0; i < arrivals.Length; i++)
            arrivals[i] = TimeAtOut(layout.ArrivalS[i], layout);

        var turnAt = TimeAtOut(layout.SU, layout);
        var tailLeaves = TimeAtOut(Length + 120f, layout);
        var lastRipple = arrivals.Length > 0 ? arrivals.Max() + RippleDuration : 0f;
        if (layout.Extra)
        {
            var homeAt = turnAt + (layout.SHome - layout.SU) / SpeedBack;
            var absorbEnd = homeAt + AbsorbDuration;
            var end = Math.Max(absorbEnd + PoolCloseDuration + 0.03f, lastRipple);
            return new Timeline(arrivals, turnAt, homeAt, absorbEnd, float.NaN, float.NaN, tailLeaves, end);
        }

        var dissolveStart = turnAt + DissolveDelay;
        var dissolveEnd = dissolveStart + DissolveDuration;
        return new Timeline(
            arrivals, turnAt, float.NaN, float.NaN, dissolveStart, dissolveEnd, tailLeaves,
            Math.Max(dissolveEnd + 0.08f, lastRipple));
    }

    internal static float HeadS(float t, in Layout layout, in Timeline timeline)
    {
        if (t <= Go)
            return FormedHead * EaseOut(Segment(t, FormStart, Go));

        var tau = t - Go;
        var s = FormedHead + (tau < Acceleration
            ? layout.SpeedOut * tau * tau / (2f * Acceleration)
            : layout.SpeedOut * (tau - Acceleration * 0.5f));

        if (!layout.Extra)
        {
            if (s > layout.SU)
                s = layout.SU + StopOvershoot * EaseOut(Math.Clamp((s - layout.SU) / (StopOvershoot * 2.2f), 0f, 1f));
            return Math.Min(s, layout.SU + StopOvershoot);
        }

        if (s > layout.SU)
            s = layout.SU + SpeedBack * (t - timeline.TurnAt);
        return Math.Min(s, layout.SHome);
    }

    internal static float VisibleLength(float t, in Layout layout, in Timeline timeline)
    {
        var length = Length * EaseOut(Segment(t, FormStart, FormEnd));
        if (layout.Extra)
            length *= 1f - EaseIn(Segment(t, timeline.HomeAt, timeline.AbsorbEnd));
        return length;
    }

    internal static float Spread(float t) => EaseOut(Segment(t, SpreadStart, SpreadEnd));

    /// <summary>Wing beat at 3.2 Hz, inside the cel ceiling for periodic motion.</summary>
    internal static float Flap(float t) =>
        t > Go ? 0.82f + 0.18f * MathF.Cos((t - Go) * MathF.Tau * FlapHz) : 1f;

    internal static float PoolOpen(float t, in Layout layout, in Timeline timeline)
    {
        var pool = EaseOut(Segment(t, 0f, PoolOpenEnd));
        pool *= 1f - 0.75f * EaseInOut(Segment(t, timeline.TailLeavesPool - 0.1f, timeline.TailLeavesPool + 0.15f));
        if (layout.Extra)
        {
            var regrow = EaseOut(Segment(t, timeline.HomeAt - AbsorbRegrowPool, timeline.HomeAt))
                * (1f - EaseIn(Segment(t, timeline.AbsorbEnd, timeline.AbsorbEnd + PoolCloseDuration)));
            pool = Math.Max(pool, regrow);
        }
        return pool;
    }

    internal static float BodyAlpha(float t, in Layout layout, in Timeline timeline) =>
        layout.Extra ? 1f : 1f - Segment(t, timeline.DissolveStart, timeline.DissolveEnd);

    /// <summary>Ripple progress in [0, 1) at enemy <paramref name="index"/>, or a negative value when none is drawn.</summary>
    internal static float Ripple(float t, int index, in Timeline timeline)
    {
        var k = (t - timeline.Arrivals[index]) / RippleDuration;
        return k is > 0f and < 1f ? k : -1f;
    }

    // --- Shapes ---------------------------------------------------------------

    // The dragon is drawn as its side profile in (along, height): along runs
    // forward down the body, height is above the ground. Height is cast onto the
    // floor along a fixed direction — away from the camera and leaning with the
    // travel — so the shape reads as the long shadow of a dragon flying past.
    private const float ShadowLength = 1.35f;
    private const float ShadowLean = 0.45f;

    /// <summary>Height of the spine: neck arched above a level body, tail near the ground.</summary>
    internal static float SpineHeight(float u)
    {
        if (u < 0.10f) return Lerp(178f, 205f, u / 0.10f);
        if (u < 0.27f)
        {
            var k = (u - 0.10f) / 0.17f;
            return Lerp(205f, 78f, k * k * (3f - 2f * k));
        }
        if (u < 0.55f) return Lerp(78f, 62f, (u - 0.27f) / 0.28f);
        return Lerp(62f, 18f, (u - 0.55f) / 0.45f);
    }

    /// <summary>Half the body's depth in profile.</summary>
    internal static float BodyHalfDepth(float u)
    {
        if (u < 0.04f) return Lerp(17f, 12f, u / 0.04f);
        if (u < 0.24f) return 11f + 3f * MathF.Sin((u - 0.04f) / 0.20f * MathF.PI);
        if (u < 0.34f) return Lerp(13f, 34f, (u - 0.24f) / 0.10f);
        if (u < 0.50f) return Lerp(34f, 26f, (u - 0.34f) / 0.16f);
        return Lerp(26f, 3f, MathF.Pow((u - 0.50f) / 0.50f, 0.8f));
    }

    private const int PoolSegments = 32;
    private const float NeckRim = 0.85f;
    private const float TailRim = 0.25f;
    private const float LegRim = 0.55f;
    private const float PoolRim = 0.5f;
    private const float InkRim = 0.4f;
    private const float WingRoot = 0.30f;
    private static readonly (float U, float Scale)[] Legs = [(0.31f, 1.0f), (0.50f, 1.25f)];
    // Legs paddle in opposite phase, one cycle per stride of head travel (about
    // 3 Hz at the outbound speed), and trail back as the dragon speeds up.
    private const float LegStride = 850f;
    private const float LegSwing = 0.45f;
    private const float LegTuck = 0.35f;
    private const float LegFullSwingSpeed = 1200f;
    // The far wing sits further back and lower, so the two read apart in the shadow.
    private static readonly (float Along, float Height)[] WingLayers = [(-78f, 0.82f), (0f, 1.0f)];

    // Raised bat wing in (along, height): a bowed leading edge up to the wrist,
    // then four finger tips. Between tips the membrane is a quadratic arc pulled
    // in toward the wrist — the scalloped trailing edge is what makes a bat wing
    // read as a wing rather than a shard.
    private static readonly Vector2[] Wing = BuildWing();
    private const int WingArcSegments = 6;

    private static Vector2[] BuildWing()
    {
        var points = new List<Vector2>();
        AddArc(points, new(24, 0), new(56, 92), new(36, 190));             // leading edge, bowed forward
        points.Add(new(14, 310));                                           // first finger, straight bone
        AddArc(points, new(14, 310), new(-26, 236), new(-72, 296));         // membranes between fingers
        AddArc(points, new(-72, 296), new(-92, 206), new(-142, 252));
        AddArc(points, new(-142, 252), new(-152, 164), new(-214, 184));
        AddArc(points, new(-214, 184), new(-110, 128), new(-80, 8));        // last membrane back to the body
        return [.. points];
    }

    /// <summary>Appends the arc from <paramref name="from"/> (exclusive) through to <paramref name="to"/>.</summary>
    private static void AddArc(List<Vector2> points, Vector2 from, Vector2 control, Vector2 to)
    {
        if (points.Count == 0)
            points.Add(from);
        for (var i = 1; i <= WingArcSegments; i++)
        {
            var k = i / (float)WingArcSegments;
            points.Add(from * ((1f - k) * (1f - k)) + control * (2f * k * (1f - k)) + to * (k * k));
        }
    }

    private static readonly Vector2[] Snout =
    [
        new(-8, 14), new(20, 16), new(52, 8), new(82, -2), new(90, -8), new(62, -8), new(80, -20),
        new(46, -16), new(10, -16), new(-6, -8)
    ];

    private static readonly Vector2[][] Horns =
    [
        [new(0, 12), new(-58, 56), new(-40, 36), new(-10, 6)],
        [new(14, 14), new(-26, 64), new(-12, 40), new(6, 10)]
    ];

    private static readonly Vector2[] Foot =
    [
        new(12, 4), new(20, -40), new(36, -54), new(22, -56), new(30, -68), new(10, -60), new(6, -70),
        new(-4, -56), new(-10, 4)
    ];

    /// <summary>A side-profile point, cast onto the floor plane.</summary>
    internal static Vector2 Cast(in Frame frame, float along, float height)
    {
        var lean = frame.T.X >= 0f ? ShadowLean : -ShadowLean;
        return frame.P + frame.T * along + new Vector2(lean, -1f) * (ShadowLength * height);
    }

    private readonly record struct Station(Frame Frame, float Height, float HalfDepth);

    private static Station StationAt(float head, float d, float grown, in Layout layout)
    {
        var u = d / Length;
        var wave = u > 0.55f ? 10f * MathF.Sin(u * 14f - head * 0.03f) : 0f;
        return new Station(FrameAt(head - d, layout), SpineHeight(u) * grown + wave, BodyHalfDepth(u) * grown);
    }

    private static Vector2[] Place(in Station station, Vector2[] profile, float alongScale = 1f, float heightBase = 0f)
    {
        var points = new Vector2[profile.Length];
        for (var i = 0; i < profile.Length; i++)
            points[i] = Cast(station.Frame, profile[i].X * alongScale, heightBase + profile[i].Y * alongScale);
        return points;
    }

    /// <summary>
    /// Every floor-plane shape for body time <paramref name="t"/>. The body is a
    /// strip of quads, never one polygon: on the turn its outline crosses itself,
    /// and a single self-intersecting polygon does not triangulate.
    /// </summary>
    internal static void Build(float t, in Layout layout, in Timeline timeline, float seed, Shapes output)
    {
        output.Clear();
        var pool = PoolOpen(t, layout, timeline);
        if (pool > 0.01f)
            output.Polygons.Add((Ellipse(new Vector2(layout.X0 + 40f * layout.Facing, 6f), 150f * pool, 70f * pool), 1f, PoolRim));

        var alpha = BodyAlpha(t, layout, timeline);
        var head = HeadS(t, layout, timeline);
        var visible = VisibleLength(t, layout, timeline);
        if (alpha > 0f && visible >= 4f)
        {
            var speed = MathF.Abs(HeadS(t + 0.01f, layout, timeline) - HeadS(Math.Max(0f, t - 0.01f), layout, timeline))
                / (t + 0.01f - Math.Max(0f, t - 0.01f));
            BuildDragon(head, visible, Spread(t), Flap(t), speed, alpha, layout, output);
        }

        if (!layout.Extra && t > timeline.DissolveStart)
            BuildDissolve(t, head, layout, timeline, seed, output);

        BuildMotes(t, layout, timeline, seed, output);
    }

    private static void BuildDragon(
        float head, float visible, float spread, float flap, float speed, float alpha, in Layout layout, Shapes output)
    {
        var grown = 0.35f + 0.65f * Math.Min(1f, visible / Length);
        Span<Vector2> upper = stackalloc Vector2[BodySamples + 1];
        Span<Vector2> lower = stackalloc Vector2[BodySamples + 1];
        for (var i = 0; i <= BodySamples; i++)
        {
            var station = StationAt(head, visible * i / BodySamples, grown, layout);
            upper[i] = Cast(station.Frame, 0f, station.Height + station.HalfDepth);
            lower[i] = Cast(station.Frame, 0f, Math.Max(0f, station.Height - station.HalfDepth));
        }
        for (var i = 0; i < BodySamples; i++)
            output.Polygons.Add(([upper[i], upper[i + 1], lower[i + 1], lower[i]], alpha,
                Lerp(NeckRim, TailRim, (float)i / BodySamples)));

        // Head: snout reaching forward and a little down, open jaw, swept horns, whiskers.
        var top = StationAt(head, 0f, grown, layout);
        output.Polygons.Add((Place(top, Snout, heightBase: top.Height), alpha, 1f));
        foreach (var horn in Horns)
            output.Polygons.Add((Place(top, horn, heightBase: top.Height), alpha, 1f));
        for (var w = 0; w < 2; w++)
        {
            var whisker = new Vector2[9];
            for (var i = 0; i < whisker.Length; i++)
            {
                var k = i / 8f;
                var height = top.Height - 12f - k * (40f + w * 25f) + 10f * MathF.Sin(k * 6f + head * 0.02f + w);
                whisker[i] = Cast(top.Frame, 60f - k * 150f, height);
            }
            output.Strokes.Add(new Stroke(whisker, 5f, 1.5f, alpha, 1f));
        }

        if (visible > Length * WingRoot && spread > 0.01f)
        {
            var root = StationAt(head, Length * WingRoot, grown, layout);
            var span = spread * flap;
            foreach (var (along, heightScale) in WingLayers)
            {
                var wing = new Vector2[Wing.Length];
                for (var i = 0; i < Wing.Length; i++)
                {
                    wing[i] = Cast(root.Frame, Wing[i].X * (0.7f + 0.3f * span) + along,
                        root.Height + root.HalfDepth - 4f + Wing[i].Y * span * heightScale);
                }
                output.Polygons.Add((wing, alpha, 1f));
            }
        }

        var swing = LegSwing * Math.Min(1f, speed / LegFullSwingSpeed);
        var tuck = LegTuck * Math.Min(1f, speed / SpeedBack);
        for (var leg = 0; leg < Legs.Length; leg++)
        {
            var (u, scale) = Legs[leg];
            if (visible <= Length * u)
                continue;
            var station = StationAt(head, Length * u, grown, layout);
            var angle = swing * MathF.Sin(MathF.Tau * head / LegStride + MathF.PI * leg) - tuck;
            output.Polygons.Add((Place(station, Rotate(Foot, angle), scale, station.Height - station.HalfDepth),
                alpha, LegRim));
        }

        // Spade at the tail tip, close to the ground.
        var tail = StationAt(head, visible, grown, layout);
        output.Polygons.Add((
        [
            Cast(tail.Frame, 6f, tail.Height), Cast(tail.Frame, -22f, tail.Height + 14f),
            Cast(tail.Frame, -50f, tail.Height), Cast(tail.Frame, -22f, Math.Max(0f, tail.Height - 12f))
        ], alpha, TailRim));
    }

    private static void BuildDissolve(float t, float head, in Layout layout, in Timeline timeline, float seed, Shapes output)
    {
        var k = Segment(t, timeline.DissolveStart, timeline.DissolveEnd);
        if (k >= 1f)
            return;
        for (var i = 0; i < DissolveBlobs; i++)
        {
            var f = FrameAt(head - Length * Hash(i, seed), layout);
            var offset = (Hash(i + 3, seed) - 0.5f) * 240f * (0.4f + k);
            var drift = (Hash(i + 5, seed) - 0.5f) * 80f * k;
            var radius = (6f + Hash(i + 9, seed) * 10f) * (1f - k * 0.6f) * 1.3f;
            output.Blobs.Add(new Blob(f.P + f.N * offset + new Vector2(drift, 0f), radius, 1f - k, InkRim));
        }
    }

    /// <summary>
    /// Ink motes shed from the tail and wing roots. Each mote is a pure function of
    /// its spawn index, so any frame can be drawn without history.
    /// </summary>
    private static void BuildMotes(float t, in Layout layout, in Timeline timeline, float seed, Shapes output)
    {
        for (var k = 0; ; k++)
        {
            var born = Go + k * MoteInterval;
            if (born > t)
                break;
            var age = t - born;
            if (age > MoteLife)
                continue;
            var visible = VisibleLength(born, layout, timeline);
            if (visible < 40f)
                continue;

            var fromTail = Hash(k, seed) < 0.5f;
            var d = fromTail ? visible : visible * 0.24f + 10f;
            var f = FrameAt(HeadS(born, layout, timeline) - d, layout);
            var offset = (Hash(k + 7, seed) - 0.5f) * (fromTail ? 30f : 260f);
            var radius = (4f + Hash(k + 3, seed) * 7f) * (1f - age / MoteLife) * 1.3f;
            output.Blobs.Add(new Blob(f.P + f.N * offset, radius, BodyAlpha(t, layout, timeline), InkRim));
        }
    }

    // --- Helpers --------------------------------------------------------------

    /// <summary>Rotates a profile about its hip; positive swings the toes forward.</summary>
    private static Vector2[] Rotate(Vector2[] profile, float angle)
    {
        var (sin, cos) = MathF.SinCos(angle);
        var points = new Vector2[profile.Length];
        for (var i = 0; i < profile.Length; i++)
        {
            var p = profile[i];
            points[i] = new Vector2(p.X * cos - p.Y * sin, p.X * sin + p.Y * cos);
        }
        return points;
    }

    private static Vector2[] Ellipse(Vector2 centre, float rx, float rz)
    {
        var points = new Vector2[PoolSegments];
        for (var i = 0; i < PoolSegments; i++)
        {
            var a = MathF.Tau * i / PoolSegments;
            points[i] = centre + new Vector2(MathF.Cos(a) * rx, MathF.Sin(a) * rz);
        }
        return points;
    }

    internal static float Segment(float t, float start, float end) =>
        end <= start ? (t >= end ? 1f : 0f) : Math.Clamp((t - start) / (end - start), 0f, 1f);

    private static float Lerp(float a, float b, float x) => a + (b - a) * x;
    internal static float EaseOut(float x) => 1f - MathF.Pow(1f - x, 3f);
    private static float EaseIn(float x) => x * x * x;
    private static float EaseInOut(float x) => x < 0.5f ? 4f * x * x * x : 1f - MathF.Pow(-2f * x + 2f, 3f) / 2f;

    private static float Hash(int i, float seed)
    {
        var s = MathF.Sin(i * 127.1f + seed * 311.7f) * 43758.5453f;
        return s - MathF.Floor(s);
    }
}
