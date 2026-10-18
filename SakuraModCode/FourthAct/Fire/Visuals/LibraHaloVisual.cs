using Godot;
using MegaCrit.Sts2.Core.Assets;

namespace SakuraMod.SakuraModCode.FourthAct.Fire.Visuals;

// The living light behind one Pan, hung under its creature visuals so it follows every swing.
// It brightens, quickens and reddens as that Pan's points near 0 or 10.
internal sealed partial class LibraHaloVisual : Node2D
{
    internal const string ShaderPath = MainFile.ResPath + "/shaders/fourth_act/libra_halo.gdshader";

    private static readonly Color MoonColor = new(0.72f, 0.82f, 1f);
    private static readonly Color SunColor = new(1f, 0.74f, 0.32f);
    private static readonly Color Warning = new(1f, 0.32f, 0.24f);
    private static readonly StringName TimeParam = "u_time";
    private static readonly StringName IntensityParam = "intensity";
    private static readonly StringName ColorParam = "halo_color";

    private readonly bool _moon;
    private readonly ShaderMaterial _material;
    private float _time;
    private float _danger;
    private float _dangerTarget;
    private float _fade = 1f;
    private bool _fading;

    private LibraHaloVisual(bool moon, Shader shader, float size)
    {
        _moon = moon;
        Name = moon ? "MoonHalo" : "SunHalo";
        _material = new ShaderMaterial { Shader = shader };
        _material.SetShaderParameter("kind", moon ? 0f : 1f);
        AddChild(new ColorRect
        {
            Name = "Glow",
            Size = Vector2.One * size,
            Position = Vector2.One * -size * 0.5f,
            Material = _material,
            MouseFilter = Control.MouseFilterEnum.Ignore
        });
        Write();
    }

    // Optional: a missing shader only loses the halo, never the Pan.
    internal static LibraHaloVisual? TryCreate(bool moon, float size)
    {
        try
        {
            return new LibraHaloVisual(moon, PreloadManager.Cache.GetAsset<Shader>(ShaderPath), size);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"Libra halo failed open: {ex.Message}");
            return null;
        }
    }

    internal void SetPoints(int points) => _dangerTarget = Math.Max(0, Math.Abs(points - 5) - 1) / 4f;

    internal void FadeOut() => _fading = true;

    public override void _Process(double delta)
    {
        var step = (float)delta;
        _danger = Mathf.MoveToward(_danger, _dangerTarget, step * 1.5f);
        _time += step * (1f + 1.6f * _danger);
        if (_fading)
        {
            _fade = Mathf.MoveToward(_fade, 0f, step * 2.5f);
            if (_fade <= 0f)
            {
                QueueFree();
                return;
            }
        }
        Write();
    }

    private void Write()
    {
        _material.SetShaderParameter(TimeParam, _time);
        _material.SetShaderParameter(IntensityParam, (0.7f + 0.55f * _danger) * _fade);
        _material.SetShaderParameter(ColorParam, (_moon ? MoonColor : SunColor).Lerp(Warning, _danger * 0.65f));
    }
}
