using Godot;

namespace SakuraMod.SakuraModCode.FourthAct.Fire.Visuals;

// Balance-style indicator engraved on the lower half of the crest's ring: 11 steps whose ends
// are the lethal 0/10 marks, and a hanging needle per Pan. Drawn in screen pixels around the
// ring's centre; the controller feeds it projected points only.
internal sealed partial class LibraDialVisual : Node2D
{
    // The ring's centre and band in central.png texels.
    internal static readonly Vector2 RingCenterTexels = new(-2f, 28.5f);
    private const float RingInnerTexels = 171f;
    private const float RingOuterTexels = 222f;
    private const float StepDegrees = 11f;
    private static readonly Color Outline = new("533724");
    private static readonly Color Gold = new("e8b94f");
    private static readonly Color Warning = new("ff765e");
    private static readonly Color Lethal = new("b3122c");
    private static readonly Color SunMarker = new("ffb347");
    private static readonly Color MoonMarker = new("bcd4ff");
    private static readonly Color[] ZoneColors =
    [
        new("fff2b0"),
        new("f6d98a"),
        new("f0a24a"),
        new("e8743a"),
        new("d8452e"),
        new("b3122c")
    ];

    private readonly float _inner;
    private readonly float _outer;
    private float _sun = 5f;
    private float _moon = 5f;
    private float _tension;

    public LibraDialVisual(float centralScale)
    {
        Name = "PendulumDial";
        _inner = RingInnerTexels * centralScale;
        _outer = RingOuterTexels * centralScale;
    }

    // Sun Pan reads its own points; Moon Pan reads mirrored so both coincide while balanced.
    internal void SetPoints(int left, int right) => SetMarkers(10 - left, right);

    internal void SetMarkers(float moon, float sun)
    {
        _moon = moon;
        _sun = sun;
        QueueRedraw();
    }

    internal (float Moon, float Sun) Markers => (_moon, _sun);

    internal void SetTension(float tension)
    {
        if (Mathf.IsEqualApprox(tension, _tension))
            return;
        _tension = tension;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var band = _outer - _inner;
        var inlay = _outer - band * 0.22f;
        var start = Angle(0f) + Mathf.DegToRad(StepDegrees * 0.5f);
        var end = Angle(10f) - Mathf.DegToRad(StepDegrees * 0.5f);
        DrawArc(Vector2.Zero, inlay, end, start, 48, Outline with { A = 0.85f }, 7f, antialiased: true);
        for (var step = 0; step <= 10; step++)
        {
            var from = Angle(step) + Mathf.DegToRad(StepDegrees * 0.5f);
            var to = Angle(step) - Mathf.DegToRad(StepDegrees * 0.5f);
            DrawArc(Vector2.Zero, inlay, to, from, 6, ZoneColors[Math.Abs(step - 5)], 4f, antialiased: true);
        }

        for (var step = 0; step <= 10; step++)
        {
            var direction = Direction(step);
            var major = step is 0 or 5 or 10;
            var inner = _inner + band * (major ? 0.2f : 0.45f);
            DrawLine(direction * inner, direction * (_outer - 1f), Outline, major ? 3.5f : 2f, antialiased: true);
        }
        foreach (var lethal in new[] { 0f, 10f })
        {
            var center = Direction(lethal) * (_outer + 8f);
            DrawCircle(center, 6.5f, Outline);
            DrawCircle(center, 4.8f, Lethal);
            DrawCircle(center + new Vector2(-1.2f, -1.2f), 1.3f, new Color("ffb0be"));
        }

        if (Mathf.IsEqualApprox(_moon, _sun))
            DrawMarker(_sun, Gold.Lerp(Warning, _tension), 1f);
        else
        {
            DrawMarker(_moon, MoonMarker, 0.8f);
            DrawMarker(_sun, SunMarker.Lerp(Warning, _tension), 1f);
        }
    }

    // A slim needle from the ring's centre with a pointer resting on the scale.
    private void DrawMarker(float value, Color color, float size)
    {
        var direction = Direction(Mathf.Clamp(value, 0f, 10f));
        var normal = new Vector2(-direction.Y, direction.X);
        var tip = direction * (_outer - 2f);
        var tail = direction * (_inner - 4f);
        DrawLine(direction * (_inner * 0.55f), tail, Outline, 4.5f * size, antialiased: true);
        DrawLine(direction * (_inner * 0.55f), tail, color, 2f * size, antialiased: true);
        Vector2[] outline = [tip + direction * 3f, tail + normal * 8.5f * size, tail - normal * 8.5f * size];
        Vector2[] core = [tip, tail + normal * 6f * size + direction * 1.5f, tail - normal * 6f * size + direction * 1.5f];
        DrawColoredPolygon(outline, Outline);
        DrawColoredPolygon(core, color);
    }

    // Points rise toward the right: a heavier Sun Pan swings the needle toward the Sun side.
    private static float Angle(float value) => Mathf.DegToRad(90f - (value - 5f) * StepDegrees);
    private static Vector2 Direction(float value) => Vector2.FromAngle(Angle(value));
}
