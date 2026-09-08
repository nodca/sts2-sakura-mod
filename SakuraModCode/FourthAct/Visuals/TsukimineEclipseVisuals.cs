using Godot;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace SakuraMod.SakuraModCode.FourthAct.Visuals;

// Attached from the established combat-visual mount, not a mod-scene C# bridge.
internal partial class TsukimineEclipseVisuals : Node
{
    private TextureRect _painting = null!;
    private ShaderMaterial _material = null!;
    private NCombatRoom _room = null!;
    private CpuParticles2D _petals = null!;
    private double _ambientTime;
    private bool _ambientEnabled = true;

    public static TsukimineEclipseVisuals Attach(TextureRect painting, NCombatRoom room)
    {
        if (painting.GetNodeOrNull<TsukimineEclipseVisuals>(nameof(TsukimineEclipseVisuals)) is { } existing)
        {
            existing._ambientEnabled = true;
            existing._petals.Emitting = true;
            existing._petals.Visible = true;
            return existing;
        }

        var controller = new TsukimineEclipseVisuals
        {
            Name = nameof(TsukimineEclipseVisuals),
            _painting = painting,
            _material = (ShaderMaterial)painting.Material,
            _room = room
        };
        painting.AddChild(controller);
        return controller;
    }

    public override void _Ready()
    {
        // Keep the native container and parallax, but remove its unrelated artwork.
        Node branch = _painting;
        while (branch != _room.Background && branch.GetParent() is { } parent)
        {
            foreach (var sibling in parent.GetChildren())
            {
                if (sibling != branch && sibling is CanvasItem item)
                    item.Visible = false;
            }
            branch = parent;
        }
        _petals = new CpuParticles2D
        {
            Name = "ShrinePetals",
            Texture = ResourceLoader.Load<Texture2D>(FourthActCombatBackgrounds.EclipsePetalTexturePath),
            Amount = 24,
            Lifetime = 24,
            Preprocess = 18,
            LocalCoords = true,
            EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
            EmissionRectExtents = new Vector2(650, 35),
            Direction = new Vector2(0.35f, 1),
            Spread = 22,
            Gravity = new Vector2(0.5f, 1.0f),
            InitialVelocityMin = 17,
            InitialVelocityMax = 29,
            AngularVelocityMin = -35,
            AngularVelocityMax = 45,
            ScaleAmountMin = 0.12f,
            ScaleAmountMax = 0.24f,
            Color = new Color(0.86f, 0.73f, 0.78f, 0.6f)
        };
        _painting.AddChild(_petals);
        UpdateLayout();
    }

    public override void _Process(double delta)
    {
        UpdateLayout();
        if (!_ambientEnabled)
            return;
        var progress = _material.GetShaderParameter(FourthActCombatBackgrounds.EternalNightProgressParameterName).AsSingle();
        var intensity = Mathf.Clamp((progress - 1f) / 4f, 0f, 1f);
        _ambientTime += delta * Mathf.Lerp(1f, 0.3f, intensity);
        _material.SetShaderParameter("ambient_time", _ambientTime);
        _petals.SpeedScale = Mathf.Lerp(1f, 0.3f, intensity);
        _petals.Modulate = new Color(1, 1, 1, Mathf.Lerp(1f, 0.12f, intensity));
    }

    private void UpdateLayout()
    {
        if (!GodotObject.IsInstanceValid(_room) || _room.Size.X <= 0 || _room.Size.Y <= 0)
            return;
        // All ratios crop the same camera sensor; parent parallax remains native.
        var imageScale = Mathf.Min(_room.Size.X / 1920f, _room.Size.Y / 1080f);
        var parentScale = _painting.GetGlobalTransform().Scale;
        var target = new Vector2(2708, 1328) * imageScale / parentScale;
        if (!_painting.Size.IsEqualApprox(target))
        {
            _painting.Size = target;
            _painting.Position = -target / 2f;
            _petals.Position = new Vector2(target.X * 0.30f, target.Y * 0.13f);
            _petals.Scale = target / new Vector2(2708, 1328);
        }
    }

    public void StopAmbient()
    {
        _ambientEnabled = false;
        _petals.Emitting = false;
        _petals.Visible = false;
    }
}
