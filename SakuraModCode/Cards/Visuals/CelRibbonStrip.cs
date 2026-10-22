using Godot;
using MegaCrit.Sts2.Core.Assets;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
/// One cel ribbon on screen: a <see cref="MeshInstance2D"/> triangle strip with
/// its own <see cref="ShaderMaterial"/>. The owner adds <see cref="Node"/> to its
/// tree and drives the uniforms (reveal window, flash, alpha) from its own
/// tween or session clock; the strip holds no clock and frees with its parent.
/// </summary>
internal sealed class CelRibbonStrip
{
    internal const string ShaderPath = MainFile.ResPath + "/shaders/card_vfx/cel_ribbon.gdshader";
    internal static IReadOnlyList<string> AssetPaths { get; } = [ShaderPath];

    private readonly ShaderMaterial _material;

    private CelRibbonStrip(MeshInstance2D node, ShaderMaterial material, RibbonPath path)
    {
        Node = node;
        _material = material;
        Path = path;
    }

    internal MeshInstance2D Node { get; }
    internal RibbonPath Path { get; }

    /// <summary>The cached shader, or null when it was not preloaded with the run.</summary>
    internal static Shader? TryGetShader() =>
        PreloadManager.Cache.GetAsset<Shader>(ShaderPath);

    internal static CelRibbonStrip Create(
        Shader shader,
        RibbonPath path,
        float halfWidth,
        Color front,
        Color back,
        Color ink,
        string name = "CelRibbon")
    {
        var strip = CelRibbonPaths.BuildStrip(path, halfWidth);
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = strip.Vertices;
        arrays[(int)Mesh.ArrayType.TexUV] = strip.Uvs;
        arrays[(int)Mesh.ArrayType.Color] = strip.Colors;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.TriangleStrip, arrays);

        // Every uniform is written now: a tween on a never-set uniform starts
        // from Nil and crashes (cel-vfx-silent-visual.md).
        var material = new ShaderMaterial { Shader = shader };
        material.SetShaderParameter("front_colour", front);
        material.SetShaderParameter("back_colour", back);
        material.SetShaderParameter("ink_colour", ink);
        material.SetShaderParameter("head_start", 0f);
        material.SetShaderParameter("head_end", 0f);
        material.SetShaderParameter("tail_start", 0f);
        material.SetShaderParameter("tail_end", 0f);
        material.SetShaderParameter("flash_at", path.Twists.Length > 0 ? path.Twists[0] : 0.5f);
        material.SetShaderParameter("flash", 0f);
        material.SetShaderParameter("alpha", 1f);
        material.SetShaderParameter("swap_from", path.Twists.Length > 0 ? path.Twists[0] : 0.5f);
        material.SetShaderParameter("swap", 0f);

        var node = new MeshInstance2D { Name = name, Mesh = mesh, Material = material };
        return new CelRibbonStrip(node, material, path);
    }

    /// <summary>Drawn in from the start to <paramref name="fromStart"/> and from the end back by <paramref name="fromEnd"/>.</summary>
    internal void SetDrawn(float fromStart, float fromEnd = 0f)
    {
        _material.SetShaderParameter("head_start", Math.Clamp(fromStart, 0f, 1f));
        _material.SetShaderParameter("head_end", Math.Clamp(fromEnd, 0f, 1f));
    }

    /// <summary>Erased from the start up to <paramref name="fromStart"/> and from the end back by <paramref name="fromEnd"/>.</summary>
    internal void SetErased(float fromStart, float fromEnd = 0f)
    {
        _material.SetShaderParameter("tail_start", Math.Clamp(fromStart, 0f, 1f));
        _material.SetShaderParameter("tail_end", Math.Clamp(fromEnd, 0f, 1f));
    }

    internal void SetFlash(float at, float amount)
    {
        _material.SetShaderParameter("flash_at", Math.Clamp(at, 0f, 1f));
        _material.SetShaderParameter("flash", Math.Clamp(amount, 0f, 1f));
    }

    /// <summary>
    /// The faces trade colours from <paramref name="from"/> outward; <paramref name="amount"/>
    /// 1 has reached the farther end.
    /// </summary>
    internal void SetSwap(float from, float amount)
    {
        _material.SetShaderParameter("swap_from", Math.Clamp(from, 0f, 1f));
        _material.SetShaderParameter("swap", Math.Clamp(amount, 0f, 1f));
    }

    internal void SetAlpha(float alpha) =>
        _material.SetShaderParameter("alpha", Math.Clamp(alpha, 0f, 1f));

    internal Vector2 PointAt(float s) => Node.Position + Path.PointAt(s);
    internal Vector2 TangentAt(float s) => Path.TangentAt(s);
    internal float FacingAt(float s) => Path.FacingAt(s);
}
