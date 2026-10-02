using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.TestSupport;
using SakuraMod.SakuraModCode.FourthAct.Visuals;
using SakuraMod.SakuraModCode.Powers;
using STS2RitsuLib.Scaffolding.Content;

namespace SakuraMod.SakuraModCode.Cards.Visuals;

internal partial class LabyrinthFieldBackgroundVisuals : Node
{
    private const string FieldNodeName = "LabyrinthFieldBackground";
    private const string LayerPath = "res://SakuraMod/scenes/backgrounds/labyrinth/labyrinth_base.tscn";
    private NCombatBackground _field = null!;
    private NCombatBackground _original = null!;
    private bool _originalVisible;
    private bool _restored;
    private Tween? _transition;
    private float _targetAlpha = -1f;

    internal static void Refresh(ICombatState combat)
    {
        if (TestMode.IsOn || NCombatRoom.Instance is not { Background: { } original } room
            || !room.CreatureNodes.Any(c => c.Entity.CombatState == combat)
            || original.GetParent() is not { } parent)
            return;

        var existing = parent.GetNodeOrNull<NCombatBackground>(FieldNodeName);
        if (!combat.Creatures.Any(c => c.GetPower<LabyrinthPower>() is not null))
        {
            existing?.GetNode<LabyrinthFieldBackgroundVisuals>(nameof(LabyrinthFieldBackgroundVisuals)).SetActive(false);
            return;
        }
        if (existing is not null)
        {
            existing.GetNode<LabyrinthFieldBackgroundVisuals>(nameof(LabyrinthFieldBackgroundVisuals)).SetActive(true);
            return;
        }

        NCombatBackground? field = null;
        try
        {
            var assets = CombatBackgroundAssetsFactory.Create(FourthActCombatBackgrounds.MainScenePath, [LayerPath], null);
            field = NCombatBackground.Create(assets);
            field.Name = FieldNodeName;
            field.Modulate = new Color(1, 1, 1, 0);
            var painting = field.GetNode<TextureRect>("Layer_00/LabyrinthBase/LabyrinthPainting");
            StaticCombatBackgroundVisuals.Attach(painting, room, field);
            var controller = new LabyrinthFieldBackgroundVisuals
            {
                Name = nameof(LabyrinthFieldBackgroundVisuals),
                _field = field,
                _original = original,
                _originalVisible = original.Visible
            };
            field.AddChild(controller);
            parent.AddChild(field);
            controller.SetActive(true);
        }
        catch (Exception exception)
        {
            if (GodotObject.IsInstanceValid(field))
                field!.QueueFree();
            MainFile.Logger.Error($"Labyrinth background failed: {exception}");
        }
    }

    public override void _Ready() => CombatManager.Instance.CombatEnded += OnCombatEnded;

    public override void _ExitTree()
    {
        CombatManager.Instance.CombatEnded -= OnCombatEnded;
        Restore(removeField: false);
    }

    private void OnCombatEnded(CombatRoom _) => Restore();

    private void SetActive(bool active)
    {
        var target = active ? 1f : 0f;
        if (_restored || _targetAlpha == target)
            return;
        _targetAlpha = target;
        _transition?.Kill();
        _original.Visible = _originalVisible;
        var duration = 0.5 * Math.Abs(target - _field.Modulate.A);
        _transition = CreateTween().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        _transition.TweenProperty(_field, "modulate:a", target, duration);
        _transition.TweenCallback(Callable.From(() =>
        {
            if (active)
                _original.Visible = false;
            else
                Restore();
        }));
    }

    private void Restore(bool removeField = true)
    {
        if (_restored)
            return;
        _restored = true;
        _transition?.Kill();
        _transition = null;
        if (GodotObject.IsInstanceValid(_original))
            _original.Visible = _originalVisible;
        if (removeField && GodotObject.IsInstanceValid(_field))
        {
            _field.Visible = false;
            _field.GetParent()?.RemoveChild(_field);
            _field.QueueFree();
        }
    }
}
