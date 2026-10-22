using Godot;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
/// Draws the floor-plane shapes into the mask viewport: alpha is coverage (the
/// ink), red is each piece's rim weight (how much glow it casts). Its own
/// transform carries the floor squash, so shapes are drawn in floor units.
/// </summary>
internal sealed partial class ShadeGroundDragonMask : Node2D
{
    private readonly ShadeGroundDragonShape.Shapes _shapes = new();

    internal void Show(
        float t,
        in ShadeGroundDragonShape.Layout layout,
        in ShadeGroundDragonShape.Timeline timeline,
        float seed)
    {
        ShadeGroundDragonShape.Build(t, layout, timeline, seed, _shapes);
        QueueRedraw();
    }

    /// <summary>
    /// Render-plate entry for <c>scripts/render_shade_vfx.gd</c>, which draws the
    /// shipped geometry through this node rather than porting it: GDScript cannot
    /// build the C# layout structs, so it passes primitives.
    /// </summary>
    /// <returns>The spread cue, the timeline end, then each enemy's arrival.</returns>
    public float[] ShowPlate(
        float t, float casterX, float facing, float[] enemyXs, float viewLeft, float viewRight, bool extra, float seed)
    {
        var layout = ShadeGroundDragonShape.For(casterX, facing, enemyXs, viewLeft, viewRight, extra);
        var timeline = ShadeGroundDragonShape.TimelineFor(layout);
        Show(t, layout, timeline, seed);
        return [ShadeGroundDragonShape.SpreadCue, timeline.End, .. timeline.Arrivals];
    }

    public override void _Draw()
    {
        foreach (var (points, alpha, rim) in _shapes.Polygons)
        {
            // A degenerate piece (a body quad while the shadow is still a point)
            // does not triangulate; skip it rather than let Godot log every frame.
            if (Geometry2D.TriangulatePolygon(points).Length == 0)
                continue;
            DrawColoredPolygon(points, Mask(rim, alpha));
        }

        foreach (var stroke in _shapes.Strokes)
        {
            var colour = Mask(stroke.Rim, stroke.Alpha);
            for (var i = 0; i < stroke.Points.Length - 1; i++)
            {
                var k = i / (float)(stroke.Points.Length - 2);
                DrawLine(stroke.Points[i], stroke.Points[i + 1], colour,
                    Mathf.Lerp(stroke.StartWidth, stroke.EndWidth, k), true);
            }
        }

        foreach (var blob in _shapes.Blobs)
            DrawCircle(blob.Centre, blob.Radius, Mask(blob.Rim, blob.Alpha));
    }

    private static Color Mask(float rim, float alpha) => new(rim, 1f, 1f, alpha);
}
