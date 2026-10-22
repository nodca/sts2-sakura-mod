using Godot;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
/// The ring that spreads at an enemy's feet as the head passes under it: a thin
/// water-blue ring with a darker ink ring inside, drawn on the floor plane.
/// </summary>
internal sealed partial class ShadeGroundDragonRipples : Node2D
{
    private static readonly Color Rim = new(0.498f, 0.902f, 0.937f);
    private static readonly Color Ink = new(0.008f, 0.016f, 0.024f);
    private const float RingDepth = 10f;

    private readonly List<(float X, float K)> _rings = [];

    internal void Show(float t, float[] enemyXs, in ShadeGroundDragonShape.Timeline timeline)
    {
        _rings.Clear();
        for (var i = 0; i < enemyXs.Length; i++)
        {
            var k = ShadeGroundDragonShape.Ripple(t, i, timeline);
            if (k >= 0f)
                _rings.Add((enemyXs[i], k));
        }
        QueueRedraw();
    }

    /// <summary>Render-plate entry; see <see cref="ShadeGroundDragonMask.ShowPlate"/>.</summary>
    public void ShowPlate(
        float t, float casterX, float facing, float[] enemyXs, float viewLeft, float viewRight, bool extra)
    {
        var layout = ShadeGroundDragonShape.For(casterX, facing, enemyXs, viewLeft, viewRight, extra);
        Show(t, enemyXs, ShadeGroundDragonShape.TimelineFor(layout));
    }

    public override void _Draw()
    {
        foreach (var (x, k) in _rings)
        {
            var radius = Mathf.Lerp(30f, 105f, ShadeGroundDragonShape.EaseOut(k));
            var centre = new Vector2(x, RingDepth);
            DrawArc(centre, radius, 0f, Mathf.Tau, 48, new Color(Rim, 1f - k), 4f, true);
            DrawArc(centre, radius * 0.8f, 0f, Mathf.Tau, 48, new Color(Ink, 0.6f * (1f - k)), 12f, true);
        }
    }
}
