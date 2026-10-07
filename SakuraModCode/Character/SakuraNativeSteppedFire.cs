using Godot;

namespace SakuraMod.SakuraModCode.Character;

/// <summary>
/// The fire element spirit, built from the game's own stepped fire: the three-layer
/// stack the rest-site campfire uses (<c>SteppedFireMix</c>, <c>SteppedFireAdd</c>,
/// <c>SteppedFireAdd1</c> in <c>overgrowth_rest_site.tscn</c>), recoloured from the
/// rest site's green to fire, plus the native soft light and cinders.
/// </summary>
/// <remarks>
/// A hand-written SDF flame was tried first and read as an icon next to the stage
/// art; the native stepped fire is the fire the game itself draws everywhere — rest
/// sites, boss and act backgrounds — so it belongs on these stages by construction.
/// <para>
/// Every resource lives in the game PCK, not the mod's, so nothing here can be
/// referenced from a mod-owned <c>.tscn</c>: the mod export project does not mount
/// the game PCK. They are loaded by path at runtime instead, and a missing one makes
/// <see cref="TryCreate"/> return null — the fire spirit is then simply absent, the
/// HUD still shows the state, and gameplay is untouched.
/// </para>
/// <para>
/// Motion follows <c>NRestSiteFireVfx</c>: the body flickers its vertical scale and
/// sways its skew on randomised Tween legs, while the shaders scroll their own noise.
/// The body is a child of <see cref="Root"/>, so the controller's summon, trigger,
/// reveal and dismiss can drive Root's scale, position and modulate without fighting
/// the idle motion.
/// </para>
/// </remarks>
internal sealed class SakuraNativeSteppedFire
{
    internal const string FlatShaderPath = "res://shaders/vfx/vfx_stepped_shader_fire_flat.tres";
    internal const string AddShaderPath = "res://shaders/vfx/vfx_stepped_shader_fire_add.tres";
    internal const string ShapeTexturePath = "res://images/vfx/fire/fire_base_campfire.png";
    internal const string BasicNoisePath = "res://images/vfx/fire/basic_fire_noise.png";
    internal const string TriangleNoisePath = "res://images/vfx/fire/triangle_noise_tile.png";
    internal const string ZigzagDistortionPath = "res://images/vfx/fire/zigzag_fire_distortion.png";
    internal const string BottomMaskPath = "res://images/vfx/fire/fire_bottom_mask.png";
    internal const string LightTexturePath = "res://images/vfx/light.png";
    internal const string CinderTexturePath = "res://images/vfx/fire/cinder_particle.png";
    internal const string AdditiveMaterialPath = "res://themes/canvas_item_material_additive_shared.tres";

    /// <summary>
    /// Scale of the whole native stack. The rest-site campfire is drawn at about 0.3;
    /// at 0.17 the flame stands about 75px tall, matching the other three spirits.
    /// </summary>
    internal const float StackScale = 0.17f;
    // NRestSiteFireVfx's exported defaults.
    private const float MinFlickerScale = 0.85f;
    private const float MaxFlickerScale = 1.05f;
    private const float MinFlickerTime = 0.3f;
    private const float MaxFlickerTime = 0.5f;
    private const float MaxSkew = 0.1f;
    private const float MinSkewTime = 0.8f;
    private const float MaxSkewTime = 1.5f;
    // Visual work is fixed, never scaled by combat state.
    private const int CinderCount = 4;
    /// <summary>Resting strength of the soft light behind the flame.</summary>
    internal const float GlowRestAlpha = 0.5f;

    private readonly Node2D _body;
    private Tween? _flicker;
    private Tween? _sway;
    private bool _stopped;

    internal Node2D Root { get; }
    internal Sprite2D Glow { get; }

    private SakuraNativeSteppedFire(Node2D root, Node2D body, Sprite2D glow)
    {
        Root = root;
        _body = body;
        Glow = glow;
    }

    /// <summary>
    /// Builds the native stack, or returns null when any native resource is missing.
    /// </summary>
    internal static SakuraNativeSteppedFire? TryCreate()
    {
        var flat = ResourceLoader.Load<Shader>(FlatShaderPath);
        var add = ResourceLoader.Load<Shader>(AddShaderPath);
        var shape = ResourceLoader.Load<Texture2D>(ShapeTexturePath);
        var basicNoise = ResourceLoader.Load<Texture2D>(BasicNoisePath);
        var triangleNoise = ResourceLoader.Load<Texture2D>(TriangleNoisePath);
        var zigzag = ResourceLoader.Load<Texture2D>(ZigzagDistortionPath);
        var bottomMask = ResourceLoader.Load<Texture2D>(BottomMaskPath);
        var light = ResourceLoader.Load<Texture2D>(LightTexturePath);
        var cinder = ResourceLoader.Load<Texture2D>(CinderTexturePath);
        var additive = ResourceLoader.Load<Material>(AdditiveMaterialPath);
        if (flat is null || add is null || shape is null || basicNoise is null || triangleNoise is null
            || zigzag is null || bottomMask is null || light is null || cinder is null || additive is null)
        {
            MainFile.Logger.Error("Could not load the native stepped fire resources; the fire element spirit is hidden.");
            return null;
        }

        var root = new Node2D { Name = "FireyFlame" };
        var glow = new Sprite2D
        {
            Name = "Glow",
            Texture = light,
            Material = additive,
            Position = new Vector2(0f, -36f),
            Scale = Vector2.One * 0.34f,
            Modulate = new Color(1f, 0.45f, 0.12f, GlowRestAlpha)
        };
        root.AddChild(glow);

        var body = new Node2D { Name = "Body", Scale = Vector2.One * StackScale };
        root.AddChild(body);
        var textures = new LayerTextures(basicNoise, triangleNoise, zigzag, bottomMask);
        foreach (var layer in Layers)
            body.AddChild(CreateLayer(layer, layer.Additive ? add : flat, shape, textures));

        root.AddChild(CreateCinders(cinder, additive));
        return new SakuraNativeSteppedFire(root, body, glow);
    }

    internal void StartIdle()
    {
        _stopped = false;
        Flicker();
        Sway();
    }

    internal void StopIdle()
    {
        _stopped = true;
        KillTween(ref _flicker);
        KillTween(ref _sway);
    }

    private void Flicker()
    {
        if (_stopped || !GodotObject.IsInstanceValid(_body))
            return;
        var low = new Vector2(StackScale, StackScale * Range(MinFlickerScale, 1f));
        var high = new Vector2(StackScale, StackScale * Range(1f, MaxFlickerScale));
        _flicker = _body.CreateTween();
        _flicker.TweenProperty(_body, "scale", low, Range(MinFlickerTime, MaxFlickerTime))
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.InOut);
        _flicker.TweenProperty(_body, "scale", high, Range(MinFlickerTime, MaxFlickerTime))
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.InOut);
        _flicker.TweenCallback(Callable.From(Flicker));
    }

    private void Sway()
    {
        if (_stopped || !GodotObject.IsInstanceValid(_body))
            return;
        _sway = _body.CreateTween();
        _sway.TweenProperty(_body, "skew", Range(-MaxSkew, 0f), Range(MinSkewTime, MaxSkewTime))
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        _sway.TweenProperty(_body, "skew", Range(0f, MaxSkew), Range(MinSkewTime, MaxSkewTime))
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        _sway.TweenCallback(Callable.From(Sway));
    }

    private static float Range(float min, float max) => min + Random.Shared.NextSingle() * (max - min);

    private static void KillTween(ref Tween? tween)
    {
        if (tween is { } current && current.IsValid())
            current.Kill();
        tween = null;
    }

    private readonly record struct LayerTextures(
        Texture2D BasicNoise,
        Texture2D TriangleNoise,
        Texture2D Zigzag,
        Texture2D BottomMask);

    /// <summary>
    /// One layer of the rest-site stack. Transforms, steps, noise and distortion are
    /// the rest site's own values; only the colours are changed, green to fire.
    /// </summary>
    private readonly record struct Layer(
        string Name,
        bool Additive,
        Vector2 Position,
        Vector2 Scale,
        Color OuterColor,
        Color InnerColor,
        Vector2 OuterStep,
        Vector2 InnerColorStep,
        float Noise1Strength,
        Vector2 Noise1Scaling,
        Vector2 Noise1Panning,
        float Noise2Strength,
        Vector2 Noise2Panning,
        Vector2 NoiseMaskScale,
        Vector2 NoiseMaskOffset,
        float Distortion2Strength,
        Vector2 Distortion1Scale,
        Vector2 DistortionMaskScale,
        Vector2 DistortionMaskOffset);

    private static readonly Layer[] Layers =
    [
        new("SteppedFireMix", false,
            new(-12.8249f, -173.695f), new(1.14924f, 1.14924f),
            new(1f, 0.42f, 0.06f), new(0.78f, 0.13f, 0.03f),
            new(0.2f, 0.22f), new(0.26f, 0.75f),
            1f, new(2f, 2f), new(1f, 0.7f),
            1f, new(-0.2f, 0.9f),
            new(1f, 1.4f), new(0f, 0.235f),
            0.16f, new(1f, 1f), new(1f, 0.6f), new(0f, 0.47f)),
        new("SteppedFireAdd", true,
            new(-0.500137f, -149.62f), new(1.07511f, 1.03187f),
            new(0.30f, 0.13f, 0.03f), new(0f, 0f, 0f),
            new(0.9f, 0.95f), new(0.745f, 0.995f),
            0.76f, new(1f, 1f), new(0.8f, 0.7f),
            1.005f, new(0.4f, 0.6f),
            new(1f, 2.01f), new(0f, 0.045f),
            0.16f, new(1f, 0.7f), new(1f, 0.6f), new(0f, 0.46f)),
        new("SteppedFireAdd1", true,
            new(14.7219f, -107.999f), new(0.576841f, 0.756003f),
            new(1f, 0.86f, 0.45f), new(0f, 0f, 0f),
            new(0.6f, 0.99f), new(0.975f, 0.99f),
            1f, new(1.2f, 1.2f), new(0.6f, 0.8f),
            1f, new(0.3f, 1f),
            new(1f, 2f), new(0f, -0.255f),
            0.15f, new(1f, 1f), new(1f, 1.2f), new(0f, -0.12f)),
    ];

    private static Sprite2D CreateLayer(Layer layer, Shader shader, Texture2D shape, LayerTextures textures)
    {
        var material = new ShaderMaterial { Shader = shader };
        material.SetShaderParameter("OuterColor", layer.OuterColor);
        material.SetShaderParameter("InnerColor", layer.InnerColor);
        material.SetShaderParameter("OuterStep", layer.OuterStep);
        material.SetShaderParameter("InnerColorStep", layer.InnerColorStep);
        material.SetShaderParameter("Noise1Texture", textures.TriangleNoise);
        material.SetShaderParameter("Noise1Strength", layer.Noise1Strength);
        material.SetShaderParameter("Noise1Scaling", layer.Noise1Scaling);
        material.SetShaderParameter("Noise1Panning", layer.Noise1Panning);
        material.SetShaderParameter("Noise2Texture", textures.BasicNoise);
        material.SetShaderParameter("Noise2Strength", layer.Noise2Strength);
        material.SetShaderParameter("Noise2Scaling", new Vector2(1.4f, 1.4f));
        material.SetShaderParameter("Noise2Panning", layer.Noise2Panning);
        material.SetShaderParameter("NoiseMask", textures.BottomMask);
        material.SetShaderParameter("InvertNoiseMask", true);
        material.SetShaderParameter("NoiseMaskScale", layer.NoiseMaskScale);
        material.SetShaderParameter("NoiseMaskOffset", layer.NoiseMaskOffset);
        material.SetShaderParameter("Distortion1Texture", textures.TriangleNoise);
        material.SetShaderParameter("Distortion1Strength", 0.095f);
        material.SetShaderParameter("Distortion1Scale", layer.Distortion1Scale);
        material.SetShaderParameter("Distortion1Panning", new Vector2(0.25f, 1f));
        material.SetShaderParameter("Distortion2Texture", textures.Zigzag);
        material.SetShaderParameter("Distortion2Strength", layer.Distortion2Strength);
        material.SetShaderParameter("Distortion2Scale", Vector2.One);
        material.SetShaderParameter("Distortion2Panning", new Vector2(0f, 0.85f));
        material.SetShaderParameter("DistortionMask", textures.BottomMask);
        material.SetShaderParameter("DistortionMaskScale", layer.DistortionMaskScale);
        material.SetShaderParameter("DistortionMaskOffset", layer.DistortionMaskOffset);
        return new Sprite2D
        {
            Name = layer.Name,
            Texture = shape,
            Material = material,
            Position = layer.Position,
            Scale = layer.Scale
        };
    }

    /// <summary>
    /// A few cinders lifting off the tip, the rest site's <c>sparks big</c> at spirit
    /// scale. CPU particles with a fixed count, not the rest site's GPU emitter.
    /// </summary>
    private static CpuParticles2D CreateCinders(Texture2D cinder, Material additive)
    {
        var fade = new Gradient();
        fade.SetColor(0, new Color(1f, 0.72f, 0.3f, 0.95f));
        fade.SetColor(1, new Color(1f, 0.35f, 0.08f, 0f));
        return new CpuParticles2D
        {
            Name = "Cinders",
            Texture = cinder,
            Material = additive,
            Position = new Vector2(0f, -52f),
            Amount = CinderCount,
            Lifetime = 1.6,
            Preprocess = 1.6,
            EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
            EmissionRectExtents = new Vector2(10f, 4f),
            Direction = Vector2.Up,
            Spread = 25f,
            Gravity = new Vector2(0f, -12f),
            InitialVelocityMin = 14f,
            InitialVelocityMax = 26f,
            ScaleAmountMin = 0.18f,
            ScaleAmountMax = 0.32f,
            ColorRamp = fade
        };
    }
}
