using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using SakuraMod.SakuraModCode.FourthAct.Fire;
using SakuraMod.SakuraModCode.FourthAct.Fire.Models;

namespace SakuraMod.SakuraModCode.FourthAct.Visuals;

internal partial class LightEternalDayVisuals : Node
{
    private Creature _owner = null!;
    private ShaderMaterial _plate = null!;
    private ShaderMaterial _sun = null!;
    private Tween? _transition;
    private float _empowered;
    private float _target;
    private double _ambientTime;
    private float _charge;
    private float _release = -1f;
    private bool _charging;
    private bool _stopped;

    internal static void Attach(TextureRect painting, NCombatRoom room)
    {
        if (painting.GetNodeOrNull<LightEternalDayVisuals>(nameof(LightEternalDayVisuals)) is not null
            || painting.Material is not ShaderMaterial plate
            || painting.GetNodeOrNull<TextureRect>("Sun")?.Material is not ShaderMaterial sun)
            return;

        var owner = room.CreatureNodes.Select(node => node.Entity)
            .FirstOrDefault(creature => creature.Monster is LightMonster);
        if (owner is null)
            return;

        painting.AddChild(new LightEternalDayVisuals
        {
            Name = nameof(LightEternalDayVisuals),
            _owner = owner,
            _plate = plate,
            _sun = sun
        });
    }

    internal static LightEternalDayVisuals? For(Creature owner)
    {
        var visuals = NCombatRoom.Instance?.Background?
            .FindChild(nameof(LightEternalDayVisuals), true, false) as LightEternalDayVisuals;
        return visuals is not null && GodotObject.IsInstanceValid(visuals)
            && !visuals._stopped && ReferenceEquals(visuals._owner, owner) ? visuals : null;
    }

    public override void _Ready()
    {
        _target = FireEnemyRules.IsLightEmpowered(_owner.CurrentHp, _owner.MaxHp) ? 1f : 0f;
        SetEmpowered(_target);
        _owner.CurrentHpChanged += OnHpChanged;
        _owner.MaxHpChanged += OnHpChanged;
        _owner.Died += OnDied;
        CombatManager.Instance.CombatEnded += OnCombatEnded;
        if (!_owner.IsAlive)
            Stop();
    }

    private void OnHpChanged(int previous, int current)
    {
        var target = FireEnemyRules.IsLightEmpowered(_owner.CurrentHp, _owner.MaxHp) ? 1f : 0f;
        if (_stopped || target == _target)
            return;
        _target = target;
        _transition?.Kill();
        _transition = CreateTween().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        _transition.TweenMethod(Callable.From<float>(SetEmpowered), _empowered, target, 0.8);
    }

    private void SetEmpowered(float value)
    {
        _empowered = value;
        _plate.SetShaderParameter("empowered", value);
        _sun.SetShaderParameter("empowered", value);
    }

    internal void BeginJudgment()
    {
        _charging = true;
        _charge = 0;
        _release = -1;
        UpdateJudgment();
    }

    internal void ReleaseJudgment()
    {
        // Native BeforeDamage runs for each target; emit one ring for the whole move.
        if (!_charging)
            return;
        _charging = false;
        _charge = 0;
        _release = 0;
        UpdateJudgment();
    }

    internal void CancelJudgmentCharge()
    {
        _charging = false;
        _charge = 0;
        UpdateJudgment();
    }

    public override void _Process(double delta)
    {
        _ambientTime = (_ambientTime + delta) % 48.0;
        _sun.SetShaderParameter("ambient_time", (float)_ambientTime);
        if (_charging)
            _charge = Mathf.Min(1f, _charge + (float)delta / 0.3f);
        if (_release >= 0)
        {
            _release += (float)delta / 1.1f;
            if (_release >= 1)
                _release = -1;
        }
        UpdateJudgment();
    }

    private void UpdateJudgment()
    {
        _sun.SetShaderParameter("judgment_charge", _charge);
        _sun.SetShaderParameter("judgment_release", _release);
    }

    private void OnDied(Creature _) => Stop();
    private void OnCombatEnded(CombatRoom _) => Stop();
    public override void _ExitTree() => Stop();

    private void Stop()
    {
        if (_stopped)
            return;
        _stopped = true;
        _owner.CurrentHpChanged -= OnHpChanged;
        _owner.MaxHpChanged -= OnHpChanged;
        _owner.Died -= OnDied;
        CombatManager.Instance.CombatEnded -= OnCombatEnded;
        _transition?.Kill();
        _transition = null;
        _charging = false;
        _charge = 0;
        _release = -1;
        UpdateJudgment();
        SetProcess(false);
    }
}
