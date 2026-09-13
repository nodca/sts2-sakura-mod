using Godot;

namespace SakuraMod.SakuraModCode.FourthAct.Visuals;

internal partial class StaticCombatBackgroundVisuals : Node
{
    internal const string RooftopPaintingNodeName = "RooftopPainting";
    internal const string LightPaintingNodeName = "LightEternalDayPainting";
    internal const string EarthPenguinParkPaintingNodeName = "EarthPenguinParkPainting";
    internal const float CanvasWidth = 2720f;
    internal const float CanvasHeight = 1360f;
    private TextureRect _painting = null!;
    private Control _room = null!;
    private Control _background = null!;

    public static void Attach(TextureRect painting, Control room, Control background)
    {
        if (painting.GetNodeOrNull<StaticCombatBackgroundVisuals>(nameof(StaticCombatBackgroundVisuals)) is not null)
            return;

        painting.AddChild(new StaticCombatBackgroundVisuals
        {
            Name = nameof(StaticCombatBackgroundVisuals),
            _painting = painting,
            _room = room,
            _background = background
        });
    }

    internal static float ImageScale(float roomWidth, float roomHeight) =>
        MathF.Min(roomWidth / 1920f, roomHeight / 1080f);

    public override void _Ready()
    {
        // Glory's own moving sky must not overlay this background instance.
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

        UpdateLayout();
    }

    public override void _Process(double delta) => UpdateLayout();

    private void UpdateLayout()
    {
        if (!GodotObject.IsInstanceValid(_room) || _room.Size.X <= 0 || _room.Size.Y <= 0)
            return;

        var parentScale = _painting.GetGlobalTransform().Scale.Abs();
        if (parentScale.X <= 0 || parentScale.Y <= 0)
            return;

        var size = new Vector2(CanvasWidth, CanvasHeight) * ImageScale(_room.Size.X, _room.Size.Y) / parentScale;
        if (!_painting.Size.IsEqualApprox(size))
        {
            _painting.Size = size;
            _painting.Position = -size / 2f;
        }
    }
}
