using Godot;

namespace SakuraMod.SakuraModCode.FourthAct.Visuals;

// The packed particle scene uses the same 1920x1080 coordinates as its art preview.
internal partial class TokyoTowerPetalVisuals : Node
{
    private TextureRect _painting = null!;
    private Node2D _petals = null!;

    public static void Attach(TextureRect painting)
    {
        if (painting.GetNodeOrNull<TokyoTowerPetalVisuals>(nameof(TokyoTowerPetalVisuals)) is not null)
            return;

        painting.AddChild(new TokyoTowerPetalVisuals
        {
            Name = nameof(TokyoTowerPetalVisuals),
            _painting = painting
        });
    }

    public override void _Ready()
    {
        _petals = _painting.GetNode<Node2D>("Petals");
        _painting.Resized += UpdateLayout;
        UpdateLayout();
    }

    public override void _ExitTree() => _painting.Resized -= UpdateLayout;

    private void UpdateLayout()
    {
        var scale = _painting.Size / new Vector2(StaticCombatBackgroundVisuals.CanvasWidth,
            StaticCombatBackgroundVisuals.CanvasHeight);
        _petals.Scale = scale;
        _petals.Position = new Vector2(400, 140) * scale;
    }
}
