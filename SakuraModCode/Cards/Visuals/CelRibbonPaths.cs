using Godot;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
/// A ribbon's centre line, resampled to even arc length so that the shader's
/// <c>s</c> (0 at the start, 1 at the end) moves at constant speed. A twist is a
/// half turn: the ribbon narrows, shows its other face, and widens again.
/// </summary>
internal readonly record struct RibbonPath(Vector2[] Points, float[] Twists, bool Closed)
{
    internal float Length
    {
        get
        {
            var length = 0f;
            for (var i = 1; i < Points.Length; i++)
                length += Points[i - 1].DistanceTo(Points[i]);
            return length;
        }
    }

    internal Vector2 PointAt(float s)
    {
        var scaled = Math.Clamp(s, 0f, 1f) * (Points.Length - 1);
        var index = Math.Min((int)scaled, Points.Length - 2);
        return Points[index].Lerp(Points[index + 1], scaled - index);
    }

    internal Vector2 TangentAt(float s)
    {
        var scaled = Math.Clamp(s, 0f, 1f) * (Points.Length - 1);
        var index = Math.Min((int)scaled, Points.Length - 2);
        return (Points[index + 1] - Points[index]).Normalized();
    }

    internal float FacingAt(float s) => CelRibbonPaths.Facing(s, Twists);
}

/// <summary>
/// The pile exchange ribbon and where its arch begins and ends (as arc-length
/// fractions), so that cards can step on and off the coils.
/// </summary>
internal readonly record struct PileExchangeRibbon(RibbonPath Path, float Departure, float Arrival);

/// <summary>Triangle-strip arrays for a <see cref="RibbonPath"/>: two vertices per sample.</summary>
internal readonly record struct RibbonStrip(Vector2[] Vertices, Vector2[] Uvs, Color[] Colors);

/// <summary>
/// Pure geometry for the cel ribbon shared by the pile exchange and the Loop.
/// No nodes and no resources, so the shapes are testable without Godot running.
/// </summary>
internal static class CelRibbonPaths
{
    internal const int DefaultSegments = 96;
    private const float SamplePixels = 5f;
    // Arc-length share of one half turn. Wide enough that the narrowing reads
    // at HUD size, narrow enough that each face still gets a long run.
    internal const float TwistSpan = 0.08f;
    // A twist pinches the ribbon to this share of its width, never to a line,
    // so the ink on both edges stays separate.
    internal const float MinTwistWidth = 0.18f;

    /// <summary>+1 shows the front face, -1 the back; a twist passes through 0.</summary>
    internal static float Facing(float s, IReadOnlyList<float> twists)
    {
        var turned = 0f;
        foreach (var twist in twists)
            turned += SmoothStep(twist - TwistSpan * 0.5f, twist + TwistSpan * 0.5f, s);
        return Mathf.Cos(Mathf.Pi * turned);
    }

    /// <summary>
    /// The pile exchange: a coil around the first pile, one arch to the second
    /// pile with a half turn at its middle, and a coil around the second pile.
    /// The arch bulges to whichever side of the pile line <paramref name="bulgeHint"/>
    /// points (toward the screen centre), so stacked piles do not arch along
    /// their own line.
    /// </summary>
    internal static PileExchangeRibbon PileExchange(
        Vector2 first,
        Vector2 second,
        float coilRadius,
        Vector2 bulgeHint)
    {
        const float coilTurns = 0.8f;
        var line = second - first;
        var distance = line.Length();
        var along = distance > 0.001f ? line / distance : Vector2.Right;
        var side = along.Orthogonal();
        if (side.Dot(bulgeHint) < 0f)
            side = -side;
        var bulge = Math.Clamp(distance * 0.2f, 70f, 300f);

        // The arch leaves the first coil heading out and toward the bulge, and
        // reaches the second coil heading in and away from it. A cubic's middle
        // reaches 3/4 of its handles' sideways offset, hence the 4/3.
        var outHandle = along * distance * 0.25f + side * bulge * (4f / 3f);
        var inHandle = along * distance * 0.25f - side * bulge * (4f / 3f);
        var leave = outHandle.Normalized();
        var arrive = inHandle.Normalized();
        var start = Coil(first, coilRadius, leave, side, coilTurns, leaving: true);
        var end = Coil(second, coilRadius, arrive, side, coilTurns, leaving: false);
        var departure = start[^1];
        var arrival = end[0];
        var arch = Cubic(departure, departure + outHandle, arrival - inHandle, arrival, 48);

        var startLength = PolylineLength(start);
        var archLength = PolylineLength(arch);
        var total = startLength + archLength + PolylineLength(end);
        if (total <= 0f)
            total = 1f;
        var twist = (startLength + archLength * 0.5f) / total;

        var centre = start.Concat(arch.Skip(1)).Concat(end.Skip(1)).ToArray();
        return new PileExchangeRibbon(
            new RibbonPath(Resample(centre, SegmentsFor(total)), [twist], Closed: false),
            startLength / total,
            (startLength + archLength) / total);
    }

    /// <summary>
    /// The Loop card's upright figure eight (a lemniscate of Gerono), written
    /// the way a hand writes an 8: from the top, down the upper lobe's left
    /// side, through the crossing, round the bottom and back up. The half turns
    /// sit at the top and the bottom, so the back face runs the first half and
    /// the front face the second; the front strand crosses last and so lies on
    /// top at the crossing, as on the card face.
    /// </summary>
    internal static RibbonPath FigureEight(Vector2 centre, float height, float width, int segments = DefaultSegments)
    {
        const int samples = 256;
        var points = new Vector2[samples + 1];
        for (var i = 0; i <= samples; i++)
        {
            var t = Mathf.Tau * i / samples;
            // Screen y grows downward.
            points[i] = centre + new Vector2(
                -width * Mathf.Sin(t) * Mathf.Cos(t),
                -height * 0.5f * Mathf.Cos(t));
        }
        // The top pinch is shared by both ends of the closed path.
        return new RibbonPath(Resample(points, segments), [0f, 0.5f, 1f], Closed: true);
    }

    /// <summary>A smooth arch between two points, for tails and threads.</summary>
    internal static RibbonPath Arch(Vector2 from, Vector2 to, Vector2 bulge, int segments = 32)
    {
        var mid = (from + to) * 0.5f + bulge;
        return new RibbonPath(
            Resample(Cubic(from, from.Lerp(mid, 0.66f), to.Lerp(mid, 0.66f), to, 32), segments),
            [],
            Closed: false);
    }

    // Pile lines span the whole HUD while the coils are small, so the sample
    // count follows the length: about one sample per SamplePixels.
    private static int SegmentsFor(float length) =>
        Math.Clamp((int)(length / SamplePixels), DefaultSegments, 640);

    internal static RibbonStrip BuildStrip(RibbonPath path, float halfWidth)
    {
        var count = path.Points.Length;
        var vertices = new Vector2[count * 2];
        var uvs = new Vector2[count * 2];
        var colors = new Color[count * 2];
        for (var i = 0; i < count; i++)
        {
            var s = i / (float)(count - 1);
            var tangent = Tangent(path, i);
            var normal = tangent.Orthogonal();
            var facing = Facing(s, path.Twists);
            var width = halfWidth * Math.Max(Math.Abs(facing), MinTwistWidth);
            vertices[i * 2] = path.Points[i] + normal * width;
            vertices[i * 2 + 1] = path.Points[i] - normal * width;
            uvs[i * 2] = new Vector2(s, 0f);
            uvs[i * 2 + 1] = new Vector2(s, 1f);
            var encoded = new Color(facing * 0.5f + 0.5f, 0f, 0f, 1f);
            colors[i * 2] = encoded;
            colors[i * 2 + 1] = encoded;
        }
        return new RibbonStrip(vertices, uvs, colors);
    }

    internal static Vector2[] Resample(IReadOnlyList<Vector2> points, int segments)
    {
        var cumulative = new float[points.Count];
        for (var i = 1; i < points.Count; i++)
            cumulative[i] = cumulative[i - 1] + points[i - 1].DistanceTo(points[i]);
        var total = cumulative[^1];

        var result = new Vector2[segments + 1];
        var index = 1;
        for (var i = 0; i <= segments; i++)
        {
            var target = total * i / segments;
            while (index < points.Count - 1 && cumulative[index] < target)
                index++;
            var span = cumulative[index] - cumulative[index - 1];
            var t = span > 0f ? (target - cumulative[index - 1]) / span : 0f;
            result[i] = points[index - 1].Lerp(points[index], Math.Clamp(t, 0f, 1f));
        }
        return result;
    }

    // A coil that ends (leaving) or starts (arriving) on the point where the
    // circle's tangent equals `heading`, on the pile's bulge side, so the
    // ribbon unrolls over the top of a pile instead of from under it.
    private static Vector2[] Coil(
        Vector2 centre,
        float radius,
        Vector2 heading,
        Vector2 outward,
        float turns,
        bool leaving)
    {
        const int samples = 40;
        var radial = heading.Orthogonal();
        if (radial.Dot(outward) < 0f)
            radial = -radial;
        var anchor = radial.Angle();
        // Increasing angle runs along (-sin, cos); pick the direction that
        // matches the heading at the anchor.
        var sign = new Vector2(-Mathf.Sin(anchor), Mathf.Cos(anchor)).Dot(heading) >= 0f ? 1f : -1f;
        var sweep = Mathf.Tau * turns * sign;
        var from = leaving ? anchor - sweep : anchor;

        var points = new Vector2[samples + 1];
        for (var i = 0; i <= samples; i++)
        {
            var angle = from + sweep * i / samples;
            points[i] = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }
        return points;
    }

    private static Vector2[] Cubic(Vector2 a, Vector2 b, Vector2 c, Vector2 d, int samples)
    {
        var points = new Vector2[samples + 1];
        for (var i = 0; i <= samples; i++)
        {
            var t = i / (float)samples;
            var u = 1f - t;
            points[i] = a * (u * u * u) + b * (3f * u * u * t) + c * (3f * u * t * t) + d * (t * t * t);
        }
        return points;
    }

    private static Vector2 Tangent(RibbonPath path, int i)
    {
        var points = path.Points;
        var last = points.Length - 1;
        // A closed path repeats its first point at the end; step over it.
        var previous = i > 0 ? points[i - 1] : path.Closed ? points[last - 1] : points[0];
        var next = i < last ? points[i + 1] : path.Closed ? points[1] : points[last];
        var tangent = next - previous;
        return tangent.LengthSquared() > 0f ? tangent.Normalized() : Vector2.Right;
    }

    private static float PolylineLength(IReadOnlyList<Vector2> points)
    {
        var length = 0f;
        for (var i = 1; i < points.Count; i++)
            length += points[i - 1].DistanceTo(points[i]);
        return length;
    }

    private static float SmoothStep(float from, float to, float x)
    {
        var t = Math.Clamp((x - from) / (to - from), 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}
