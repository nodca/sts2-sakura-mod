using Godot;

namespace SakuraMod.SakuraModCode.FourthAct.Fire.Visuals;

// The beam is the crest's wings drawn out into a yoke: two tapering, gently rising arms with
// layered feather undersides, each ending in a hanging pendant (crescent for the Moon Pan, sun
// for the Sun Pan) that carries that Pan's points. Drawn in screen pixels under the crest.
internal sealed partial class LibraBeamVisual : Node2D
{
    internal const float TipX = 560f;
    private const float RootX = 70f;
    private const float Rise = 30f;
    private const float RootThickness = 16f;
    private const float TipThickness = 5f;
    private const int Segments = 28;
    private static readonly Color Outline = new("533724");
    private static readonly Color Gold = new("e8b94f");
    private static readonly Color HighlightGold = new("fff2b0");
    private static readonly Color ShadowGold = new("b07a2c");
    private static readonly Color Wash = new("f5a742");

    private readonly LibraPendantVisual _left;
    private readonly LibraPendantVisual _right;

    public LibraBeamVisual()
    {
        Name = "WingBeam";
        _left = new LibraPendantVisual(moon: true) { Position = Tip(-1f) };
        _right = new LibraPendantVisual(moon: false) { Position = Tip(1f) };
        AddChild(_left);
        AddChild(_right);
    }

    internal Vector2 LineAnchorGlobal(bool left) => (left ? _left : _right).LineAnchorGlobal;

    internal void SetPoints(int left, int right)
    {
        _left.SetPoints(left);
        _right.SetPoints(right);
    }

    internal void SetTension(string? side, float tension)
    {
        _left.SetTension(side == "LEFT" ? tension : 0f);
        _right.SetTension(side == "RIGHT" ? tension : 0f);
    }

    internal void Sway(float time)
    {
        _left.Rotation = Mathf.DegToRad(2.5f) * Mathf.Sin(time * 1.3f);
        _right.Rotation = Mathf.DegToRad(2.5f) * Mathf.Sin(time * 1.3f + 1.7f);
    }

    internal void SetPendantVisible(bool left, bool visible) => (left ? _left : _right).Visible = visible;

    public override void _Draw()
    {
        foreach (var side in new[] { -1f, 1f })
        {
            DrawFeather(side, 0.62f, 7f, 12f, 7f);
            DrawFeather(side, 0.34f, 4f, 8f, 5f);
            DrawArm(side);
        }
    }

    private void DrawArm(float side)
    {
        var top = new Vector2[Segments + 1];
        var bottom = new Vector2[Segments + 1];
        for (var i = 0; i <= Segments; i++)
        {
            var s = (float)i / Segments;
            var center = Centerline(side, s);
            var half = Thickness(s) * 0.5f;
            top[i] = center - new Vector2(0f, half);
            bottom[i] = center + new Vector2(0f, half);
        }

        DrawColoredPolygon(Band(top, bottom, 2.5f), Outline);
        var body = Band(top, bottom, 0f);
        var colors = new Color[body.Length];
        for (var i = 0; i < colors.Length; i++)
            colors[i] = i <= Segments ? HighlightGold.Lerp(Gold, 0.35f) : ShadowGold;
        DrawPolygon(body, colors);

        // Watercolor wash: soft, uneven blooms along the arm instead of hard-edged parts.
        var rng = new RandomNumberGenerator { Seed = side < 0f ? 41UL : 73UL };
        for (var i = 0; i < 22; i++)
        {
            var s = rng.RandfRange(0.12f, 0.96f);
            var offset = rng.RandfRange(-0.25f, 0.3f) * Thickness(s);
            var radius = rng.RandfRange(0.25f, 0.42f) * Thickness(s);
            var tint = i % 3 == 0 ? Wash with { A = 0.28f } : HighlightGold with { A = 0.32f };
            DrawCircle(Centerline(side, s) + new Vector2(0f, offset), radius, tint);
        }

        var highlight = new Vector2[Segments - 3];
        for (var i = 0; i < highlight.Length; i++)
        {
            var s = (float)(i + 2) / Segments;
            highlight[i] = Centerline(side, s) - new Vector2(0f, Thickness(s) * 0.22f);
        }
        DrawPolyline(highlight, HighlightGold with { A = 0.85f }, 1.6f, antialiased: true);
        DrawCurl(side);
    }

    // A thinner feather layer under the arm, bowing down and rejoining it like the crest's wings.
    private void DrawFeather(float side, float length, float drop, float width, float tipWidth)
    {
        const int Steps = 18;
        var top = new Vector2[Steps + 1];
        var bottom = new Vector2[Steps + 1];
        for (var i = 0; i <= Steps; i++)
        {
            var u = (float)i / Steps;
            var s = u * length;
            var center = Centerline(side, s) + new Vector2(0f, Thickness(s) * 0.3f + drop * Mathf.Sin(u * Mathf.Pi * 0.85f));
            var half = Mathf.Lerp(width, tipWidth * 0.3f, u) * 0.5f;
            top[i] = center - new Vector2(0f, half);
            bottom[i] = center + new Vector2(0f, half);
        }
        DrawColoredPolygon(Band(top, bottom, 2f), Outline);
        DrawColoredPolygon(Band(top, bottom, 0f), Gold.Lerp(ShadowGold, 0.3f));
    }

    // The arm's end flicks upward into a small feather curl above the pendant.
    private void DrawCurl(float side)
    {
        var tip = Tip(side);
        Vector2[] curl =
        [
            tip + new Vector2(side * -6f, -1f),
            tip + new Vector2(side * 6f, -6f),
            tip + new Vector2(side * 10f, -16f),
            tip + new Vector2(side * 4f, -24f),
            tip + new Vector2(side * -3f, -21f)
        ];
        DrawPolyline(curl, Outline, 6f, antialiased: true);
        DrawPolyline(curl, Gold, 3f, antialiased: true);
        DrawCircle(curl[^1], 3.2f, Outline);
        DrawCircle(curl[^1], 2f, HighlightGold);
    }

    private static Vector2[] Band(Vector2[] top, Vector2[] bottom, float grow)
    {
        var points = new Vector2[top.Length + bottom.Length];
        for (var i = 0; i < top.Length; i++)
            points[i] = top[i] - new Vector2(0f, grow);
        for (var i = 0; i < bottom.Length; i++)
            points[top.Length + i] = bottom[bottom.Length - 1 - i] + new Vector2(0f, grow);
        return points;
    }

    private static Vector2 Centerline(float side, float s) =>
        new(side * Mathf.Lerp(RootX, TipX, s), -Rise * Mathf.Pow(s, 1.8f));

    private static float Thickness(float s) => Mathf.Lerp(RootThickness, TipThickness, Mathf.Pow(s, 0.8f));

    private static Vector2 Tip(float side) => Centerline(side, 1f);
}

// A pendant hung from a beam tip: a gold medallion framed by a crescent or sun rays, showing
// that Pan's points. Its bottom is where the suspension line starts.
internal sealed partial class LibraPendantVisual : Node2D
{
    private const float Drop = 30f;
    private const float Radius = 21f;
    private static readonly Color Outline = new("533724");
    private static readonly Color Gold = new("e8b94f");
    private static readonly Color HighlightGold = new("fff2b0");
    private static readonly Color Silver = new("dfe6f7");
    private static readonly Color Warning = new("ff765e");

    private readonly bool _moon;
    private readonly Label _label;
    private float _tension;

    public LibraPendantVisual(bool moon)
    {
        _moon = moon;
        Name = moon ? "MoonPendant" : "SunPendant";
        _label = new Label
        {
            Name = "Points",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Size = new Vector2(60f, 44f),
            Position = new Vector2(-30f, Drop - 22f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _label.AddThemeFontSizeOverride("font_size", 28);
        _label.AddThemeColorOverride("font_outline_color", new Color("1e120a"));
        _label.AddThemeConstantOverride("outline_size", 8);
        AddChild(_label);
        SetPoints(5);
    }

    internal Vector2 LineAnchorGlobal => ToGlobal(new Vector2(0f, Drop + Radius + 2f));

    internal void SetPoints(int points) => _label.Text = points.ToString();

    internal void SetTension(float tension)
    {
        if (Mathf.IsEqualApprox(tension, _tension) && _label.HasThemeColorOverride("font_color"))
            return;
        _tension = tension;
        _label.AddThemeColorOverride("font_color", HighlightGold.Lerp(Warning, tension));
        QueueRedraw();
    }

    public override void _Draw()
    {
        var center = new Vector2(0f, Drop);
        DrawLine(Vector2.Zero, center - new Vector2(0f, Radius), Outline, 5f, antialiased: true);
        DrawLine(Vector2.Zero, center - new Vector2(0f, Radius), Gold, 2.5f, antialiased: true);
        if (_moon)
            DrawCrescent(center);
        else
            DrawRays(center);

        var rim = Gold.Lerp(Warning, _tension * 0.7f);
        DrawCircle(center, Radius + 2.5f, Outline);
        DrawCircle(center, Radius, rim);
        DrawArc(center, Radius - 1.5f, Mathf.Pi * 1.1f, Mathf.Pi * 1.7f, 12, HighlightGold, 2f, antialiased: true);
        DrawCircle(center, Radius - 4.5f, _moon ? new Color("1d2a4a") : new Color("4a160c"));
    }

    private void DrawCrescent(Vector2 center)
    {
        const int Steps = 20;
        var points = new List<Vector2>();
        for (var i = 0; i <= Steps; i++)
            points.Add(center + Vector2.FromAngle(Mathf.Lerp(Mathf.Pi * 0.55f, Mathf.Pi * 1.45f, (float)i / Steps)) * (Radius + 11f));
        var inner = center + new Vector2(5f, 0f);
        for (var i = Steps; i >= 0; i--)
            points.Add(inner + Vector2.FromAngle(Mathf.Lerp(Mathf.Pi * 0.6f, Mathf.Pi * 1.4f, (float)i / Steps)) * (Radius + 2f));
        var shape = points.ToArray();
        DrawPolyline([.. shape, shape[0]], Outline, 3f, antialiased: true);
        DrawColoredPolygon(shape, Silver);
    }

    private void DrawRays(Vector2 center)
    {
        for (var i = 0; i < 12; i++)
        {
            var angle = Mathf.Tau * i / 12f + Mathf.Pi / 12f;
            var length = i % 2 == 0 ? 14f : 8f;
            var direction = Vector2.FromAngle(angle);
            var normal = new Vector2(-direction.Y, direction.X);
            var baseCenter = center + direction * (Radius - 1f);
            Vector2[] ray = [baseCenter + normal * 4.5f, center + direction * (Radius + length), baseCenter - normal * 4.5f];
            Vector2[] rayOutline = [baseCenter + normal * 6.5f, center + direction * (Radius + length + 2.5f), baseCenter - normal * 6.5f];
            DrawColoredPolygon(rayOutline, Outline);
            DrawColoredPolygon(ray, Gold.Lightened(0.15f));
        }
    }
}
