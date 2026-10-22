using Godot;
using MegaCrit.Sts2.Core.Assets;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
/// Reversal's motif, after its card face: beside each pile a small card split on
/// its diagonal rises (light half with dark crystals, dark half with light
/// ones), and a wipe along the diagonal turns it into its own negative. Driven
/// by <see cref="PileExchangeVfx"/>; the shader owns no clock.
/// </summary>
internal sealed class ReversalPlateMotif : IPileExchangeMotif
{
    internal const string ShaderPath = MainFile.ResPath + "/shaders/card_vfx/reversal_plate.gdshader";
    internal static IReadOnlyList<string> AssetPaths { get; } = [ShaderPath];

    private const float RiseIn = 0.18f;
    private const float FlipFrom = 0.40f;
    private const float FlipTime = 0.30f;
    // Beside the pile, toward the screen centre: stacked piles leave no room above.
    private const float Offset = 84f;
    private const float Lift = 12f;
    private static readonly Vector2 Region = new(72f, 88f);
    private static readonly Vector2 PlateHalf = new(24f, 32f);

    private readonly List<(ColorRect Rect, ShaderMaterial Material)> _plates = [];

    private ReversalPlateMotif(Node2D root, Shader shader, Vector2[] piles, float centreX)
    {
        foreach (var pile in piles)
        {
            var inward = MathF.Sign(centreX - pile.X);
            // Every uniform is written now: a never-set uniform reads as Nil
            // (cel-vfx-silent-visual.md).
            var material = new ShaderMaterial { Shader = shader };
            material.SetShaderParameter("region_size", Region);
            material.SetShaderParameter("plate_half", PlateHalf);
            material.SetShaderParameter("flip", 0f);
            material.SetShaderParameter("alpha", 0f);
            material.SetShaderParameter("light_colour", new Color(0.80f, 0.93f, 0.91f));
            material.SetShaderParameter("dark_colour", new Color(0.24f, 0.25f, 0.31f));
            material.SetShaderParameter("ink_colour", new Color(0.10f, 0.10f, 0.13f));
            material.SetShaderParameter("frame_colour", new Color(0.98f, 0.87f, 0.52f));
            var rect = new ColorRect
            {
                Name = "ReversalPlate",
                Size = Region,
                PivotOffset = Region * 0.5f,
                Position = pile + new Vector2(inward * Offset, -Lift) - Region * 0.5f,
                Color = Colors.White,
                Material = material,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ZIndex = 3
            };
            root.AddChild(rect);
            _plates.Add((rect, material));
        }
        Focus = (piles[0] + piles[1]) * 0.5f;
        Advance(0f);
    }

    public Vector2 Focus { get; }

    internal static Shader? TryGetShader() => PreloadManager.Cache.GetAsset<Shader>(ShaderPath);

    /// <summary>Positions are in <paramref name="root"/>'s local space; <paramref name="centreX"/> too.</summary>
    internal static ReversalPlateMotif Create(Node2D root, Shader shader, Vector2 first, Vector2 second, float centreX) =>
        new(root, shader, [first, second], centreX);

    public void Advance(float t)
    {
        var rise = PileExchangeVfx.Stepped(Math.Clamp(t / RiseIn, 0f, 1f));
        var x = Math.Clamp((t - FlipFrom) / FlipTime, 0f, 1f);
        var flip = x < 0.5f ? 4f * x * x * x : 1f - MathF.Pow(-2f * x + 2f, 3f) * 0.5f;
        var fade = PileExchangeVfx.FadeAt(t);
        foreach (var (rect, material) in _plates)
        {
            rect.Visible = t > 0f && fade > 0f;
            rect.Scale = Vector2.One * Mathf.Lerp(0.6f, 1f, rise);
            material.SetShaderParameter("flip", flip);
            material.SetShaderParameter("alpha", rise * fade);
        }
    }
}
