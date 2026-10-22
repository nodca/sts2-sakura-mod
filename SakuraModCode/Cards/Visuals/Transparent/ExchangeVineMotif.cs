using Godot;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
/// Exchange's motif, after its card face: a pink sakura and a white flower bloom
/// on the two piles, a vine grows in from both piles along a lazy S, and the
/// flowers trade places along it, shedding a few petals as they land. Plain
/// polygons and lines; no resources to load. Driven by <see cref="PileExchangeVfx"/>.
/// </summary>
internal sealed class ExchangeVineMotif : IPileExchangeMotif
{
    private const float Bloom = 0.22f;
    private const float VineFrom = 0.08f;
    private const float VineGrow = 0.30f;
    private const float TravelFrom = 0.40f;
    private const float Travel = 0.42f;
    private const float PetalFall = 0.35f;
    private const float FlowerRadius = 24f;
    private const float VineWidth = 7f;
    private const int VineSamples = 96;

    private static readonly Color SakuraPetal = new(0.97f, 0.74f, 0.80f);
    private static readonly Color SakuraTip = new(0.90f, 0.52f, 0.64f);
    private static readonly Color WhitePetal = new(0.99f, 0.98f, 0.95f);
    private static readonly Color WhiteTip = new(0.95f, 0.72f, 0.80f);
    private static readonly Color Stamen = new(1f, 0.84f, 0.30f);
    private static readonly Color Vine = new(0.52f, 0.76f, 0.36f);
    private static readonly Color Leaf = new(0.42f, 0.68f, 0.30f);
    private static readonly Color Ink = new(0.16f, 0.14f, 0.13f);

    private readonly Vector2[] _vine;
    private readonly List<(Line2D Line, bool FromEnd)> _lines = [];
    private readonly List<(Node2D Node, float S)> _sprigs = [];
    private readonly List<(Polygon2D Node, Vector2 From, Vector2 Drift, float Spin)> _petals = [];
    private readonly Node2D _sakura;
    private readonly Node2D _white;

    private ExchangeVineMotif(Node2D root, Vector2 first, Vector2 second, Vector2 towardCentre)
    {
        _vine = VinePath(first, second, towardCentre);
        foreach (var fromEnd in new[] { false, true })
        {
            _lines.Add((AddLine(root, Ink, VineWidth + 3f), fromEnd));
            _lines.Add((AddLine(root, Vine, VineWidth), fromEnd));
        }
        for (var i = 0; i < 6; i++)
            _sprigs.Add((CreateLeaf(root, i % 2 == 0 ? 1f : -1f), 0.16f + i * 0.136f));
        _sprigs.Add((CreateTendril(root, 1f), 0.30f));
        _sprigs.Add((CreateTendril(root, -1f), 0.70f));

        _sakura = CreateFlower(root, 5, SakuraPetal, SakuraTip, 1f);
        _white = CreateFlower(root, 8, WhitePetal, WhiteTip, 0.62f);
        for (var i = 0; i < 6; i++)
        {
            var fromSakura = i < 3;
            _petals.Add((
                CreatePetal(root, fromSakura ? SakuraPetal : WhitePetal),
                // Each flower sheds where it lands: the sakura on the second pile.
                fromSakura ? second : first,
                new Vector2(Random.Shared.NextSingle() * 56f - 28f, -18f - Random.Shared.NextSingle() * 18f),
                Random.Shared.NextSingle() * 6f - 3f));
        }
        Focus = PointOn(_vine, 0.5f);
        Advance(0f);
    }

    public Vector2 Focus { get; }

    /// <summary>Positions are in <paramref name="root"/>'s local space.</summary>
    internal static ExchangeVineMotif Create(Node2D root, Vector2 first, Vector2 second, Vector2 towardCentre) =>
        new(root, first, second, towardCentre);

    public void Advance(float t)
    {
        var fade = PileExchangeVfx.FadeAt(t);
        var half = EaseOut(Math.Clamp((t - VineFrom) / VineGrow, 0f, 1f)) * 0.5f;
        var cut = (int)MathF.Round(half * VineSamples);
        foreach (var (line, fromEnd) in _lines)
        {
            line.Points = fromEnd ? _vine[(VineSamples - cut)..] : _vine[..(cut + 1)];
            line.Visible = cut > 0 && fade > 0f;
            line.Modulate = new Color(1f, 1f, 1f, fade);
        }
        foreach (var (node, s) in _sprigs)
        {
            node.Visible = (s <= half || s >= 1f - half) && fade > 0f;
            node.Position = PointOn(_vine, s);
            node.Rotation = TangentOn(_vine, s).Angle();
            node.Modulate = new Color(1f, 1f, 1f, fade);
        }

        // Each flower blooms on its own pile and travels the vine to the other;
        // they pass each other at the middle.
        var bloom = PileExchangeVfx.Stepped(Math.Clamp(t / Bloom, 0f, 1f));
        var pop = MathF.Sin(MathF.PI * bloom * 0.5f) * (1f + 0.12f * MathF.Sin(MathF.PI * bloom));
        var travel = EaseInOut(Math.Clamp((t - TravelFrom) / Travel, 0f, 1f));
        Place(_sakura, PointOn(_vine, travel), travel * MathF.PI, pop, t > 0f && fade > 0f, fade);
        Place(_white, PointOn(_vine, 1f - travel), -travel * MathF.PI, pop, t > 0f && fade > 0f, fade);

        var landed = (t - (TravelFrom + Travel)) / PetalFall;
        foreach (var (node, from, drift, spin) in _petals)
        {
            node.Visible = landed > 0f && landed < 1f;
            if (!node.Visible)
                continue;
            node.Position = from + drift * landed + new Vector2(0f, 40f * landed * landed);
            node.Rotation = spin * landed;
            node.Modulate = new Color(1f, 1f, 1f, 1f - landed);
        }
    }

    private static void Place(Node2D flower, Vector2 position, float rotation, float scale, bool visible, float fade)
    {
        flower.Visible = visible;
        flower.Position = position;
        flower.Rotation = rotation;
        flower.Scale = Vector2.One * scale;
        flower.Modulate = new Color(1f, 1f, 1f, fade);
    }

    // A lazy S: up out of the first pile toward the screen centre, then a
    // softer counter-curve into the second.
    private static Vector2[] VinePath(Vector2 first, Vector2 second, Vector2 towardCentre)
    {
        var line = second - first;
        var distance = line.Length();
        var along = distance > 0.001f ? line / distance : Vector2.Right;
        var side = along.Orthogonal();
        if (side.Dot(towardCentre) < 0f)
            side = -side;
        var lift = Math.Clamp(distance * 0.18f, 60f, 220f);
        var c1 = first + line / 3f + side * lift;
        var c2 = first + line * (2f / 3f) - side * lift * 0.35f;
        var points = new Vector2[VineSamples + 1];
        for (var i = 0; i <= VineSamples; i++)
        {
            var u = i / (float)VineSamples;
            var v = 1f - u;
            points[i] = first * (v * v * v) + c1 * (3f * v * v * u) + c2 * (3f * v * u * u) + second * (u * u * u);
        }
        return points;
    }

    private static Line2D AddLine(Node2D root, Color color, float width)
    {
        var line = new Line2D
        {
            Width = width,
            DefaultColor = color,
            BeginCapMode = Line2D.LineCapMode.Round,
            EndCapMode = Line2D.LineCapMode.Round,
            JointMode = Line2D.LineJointMode.Round
        };
        root.AddChild(line);
        return line;
    }

    private static Node2D CreateFlower(Node2D root, int petals, Color fill, Color tip, float slender)
    {
        var flower = new Node2D { ZIndex = 3 };
        foreach (var (color, scale) in new[] { (Ink, 1.18f), (fill, 1f) })
        {
            for (var i = 0; i < petals; i++)
                flower.AddChild(new Polygon2D { Polygon = PetalShape(FlowerRadius * scale, slender), Color = color, Rotation = Mathf.Tau * i / petals });
        }
        for (var i = 0; i < petals; i++)
            flower.AddChild(new Polygon2D { Polygon = PetalTip(FlowerRadius, slender), Color = tip, Rotation = Mathf.Tau * i / petals });
        flower.AddChild(new Polygon2D { Polygon = Disc(FlowerRadius * 0.30f), Color = Ink });
        flower.AddChild(new Polygon2D { Polygon = Disc(FlowerRadius * 0.24f), Color = Stamen });
        root.AddChild(flower);
        return flower;
    }

    private static Polygon2D CreatePetal(Node2D root, Color color)
    {
        var petal = new Polygon2D { Polygon = PetalShape(9f, 1f), Color = color, ZIndex = 3, Visible = false };
        root.AddChild(petal);
        return petal;
    }

    // A teardrop leaf leaning off the vine on one side.
    private static Node2D CreateLeaf(Node2D root, float side)
    {
        var leaf = new Node2D { ZIndex = 1 };
        foreach (var (color, grow) in new[] { (Ink, 2f), (Leaf, 0f) })
        {
            var length = 26f + grow;
            var width = 9f + grow * 0.6f;
            var points = new List<Vector2>();
            for (var i = 0; i <= 8; i++)
            {
                var t = i / 8f;
                points.Add(new Vector2(length * t - grow * 0.5f, -width * MathF.Sin(MathF.PI * t) * (1f - 0.35f * t)));
            }
            for (var i = 7; i > 0; i--)
            {
                var t = i / 8f;
                points.Add(new Vector2(length * t - grow * 0.5f, width * 0.35f * MathF.Sin(MathF.PI * t)));
            }
            leaf.AddChild(new Polygon2D
            {
                Polygon = [.. points],
                Color = color,
                Rotation = -0.65f * side,
                Scale = new Vector2(1f, side)
            });
        }
        root.AddChild(leaf);
        return leaf;
    }

    // A short stalk off the vine, then an inward spiral, as the card's tendrils.
    private static Node2D CreateTendril(Node2D root, float side)
    {
        var tendril = new Node2D { ZIndex = 1 };
        var stalk = new Vector2(6f, -10f * side);
        var centre = stalk + new Vector2(10f, -4f * side);
        var start = (stalk - centre).Angle();
        var points = new List<Vector2> { Vector2.Zero, stalk };
        for (var i = 1; i <= 32; i++)
        {
            var t = i / 32f;
            points.Add(centre + Vector2.FromAngle(start + t * Mathf.Tau * 1.5f * side) * Mathf.Lerp(10.8f, 2.5f, t));
        }
        foreach (var (color, width) in new[] { (Ink, 4.5f), (Vine, 2.5f) })
        {
            tendril.AddChild(new Line2D
            {
                Points = [.. points],
                Width = width,
                DefaultColor = color,
                BeginCapMode = Line2D.LineCapMode.Round,
                EndCapMode = Line2D.LineCapMode.Round
            });
        }
        root.AddChild(tendril);
        return tendril;
    }

    // A petal pointing up with the sakura notch at its tip.
    private static Vector2[] PetalShape(float radius, float slender)
    {
        var w = 0.42f * slender;
        return
        [
            Vector2.Zero,
            new Vector2(-w * 0.8f, -0.30f) * radius,
            new Vector2(-w, -0.62f) * radius,
            new Vector2(-w * 0.7f, -0.92f) * radius,
            new Vector2(-w * 0.2f, -1f) * radius,
            new Vector2(0f, -0.86f) * radius,
            new Vector2(w * 0.2f, -1f) * radius,
            new Vector2(w * 0.7f, -0.92f) * radius,
            new Vector2(w, -0.62f) * radius,
            new Vector2(w * 0.8f, -0.30f) * radius
        ];
    }

    private static Vector2[] PetalTip(float radius, float slender)
    {
        var w = 0.42f * slender;
        return
        [
            new Vector2(-w * 0.75f, -0.80f) * radius,
            new Vector2(-w * 0.2f, -0.95f) * radius,
            new Vector2(0f, -0.83f) * radius,
            new Vector2(w * 0.2f, -0.95f) * radius,
            new Vector2(w * 0.75f, -0.80f) * radius,
            new Vector2(0f, -0.70f) * radius
        ];
    }

    private static Vector2[] Disc(float radius)
    {
        var points = new Vector2[20];
        for (var i = 0; i < points.Length; i++)
            points[i] = Vector2.FromAngle(Mathf.Tau * i / points.Length) * radius;
        return points;
    }

    private static Vector2 PointOn(Vector2[] points, float s)
    {
        var scaled = Math.Clamp(s, 0f, 1f) * (points.Length - 1);
        var index = Math.Min((int)scaled, points.Length - 2);
        return points[index].Lerp(points[index + 1], scaled - index);
    }

    private static Vector2 TangentOn(Vector2[] points, float s)
    {
        var scaled = Math.Clamp(s, 0f, 1f) * (points.Length - 1);
        var index = Math.Min((int)scaled, points.Length - 2);
        return (points[index + 1] - points[index]).Normalized();
    }

    private static float EaseOut(float x) => 1f - (1f - x) * (1f - x);

    private static float EaseInOut(float x) =>
        x < 0.5f ? 4f * x * x * x : 1f - MathF.Pow(-2f * x + 2f, 3f) * 0.5f;
}
