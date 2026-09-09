using Godot;

namespace SakuraMod.SakuraModCode.FourthAct.Visuals;

// Mounted explicitly from combat visuals; the layer itself uses only native Godot nodes.
internal partial class WaterAquariumVisuals : Node
{
    internal const string PaintingNodeName = "AquariumPainting";
    internal const string VideoNodeName = "TankMotion";
    private TextureRect _painting = null!;
    private Control _room = null!;
    private Control _background = null!;
    private VideoStreamPlayer _video = null!;

    public static void Attach(TextureRect painting, Control room, Control background)
    {
        if (painting.GetNodeOrNull<WaterAquariumVisuals>(nameof(WaterAquariumVisuals)) is not null)
            return;

        painting.AddChild(new WaterAquariumVisuals
        {
            Name = nameof(WaterAquariumVisuals),
            _painting = painting,
            _room = room,
            _background = background
        });
    }

    internal static float ImageScale(float roomWidth, float roomHeight) =>
        MathF.Min(roomWidth / 1920f, roomHeight / 1080f) * 1.2f;

    public override void _Ready()
    {
        Node branch = _painting;
        while (branch != _background && branch.GetParent() is { } parent)
        {
            foreach (var sibling in parent.GetChildren())
            {
                if (sibling != branch && sibling is CanvasItem item)
                    item.Visible = false;
            }
            branch = parent;
        }

        _video = _painting.GetNode<VideoStreamPlayer>(VideoNodeName);
        _video.Play();
        UpdateLayout();
        GD.Print("[SakuraMod][Aquarium] Motion controller ready; video started.");
    }

    public override void _Process(double delta) => UpdateLayout();

    private void UpdateLayout()
    {
        if (!GodotObject.IsInstanceValid(_room) || _room.Size.X <= 0 || _room.Size.Y <= 0)
            return;
        // The original 1600x900 camera remains centered inside a 2240x1120 overscan plate.
        var parentScale = _painting.GetGlobalTransform().Scale.Abs();
        if (parentScale.X <= 0 || parentScale.Y <= 0)
            return;
        var size = new Vector2(2240, 1120) * ImageScale(_room.Size.X, _room.Size.Y) / parentScale;
        if (!_painting.Size.IsEqualApprox(size))
        {
            _painting.Size = size;
            _painting.Position = -size / 2f;
        }
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_video))
            _video.Stop();
    }
}
