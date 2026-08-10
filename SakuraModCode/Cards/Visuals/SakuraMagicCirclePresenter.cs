using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.TestSupport;
using SakuraMod.SakuraModCode.Character;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
/// Owns one renewable magic circle per caster for the current combat room.
/// Eligible card plays trigger it but never acquire a cleanup lease on it.
/// </summary>
internal sealed partial class SakuraMagicCirclePresenter : Node2D
{
    internal const string NodeName = "SakuraMagicCirclePresenter";
    internal const string WandPreludeShaderPath =
        MainFile.ResPath + "/shaders/card_vfx/cel_wand_prelude.gdshader";
    internal const string MagicCircleInkPath =
        MainFile.ResPath + "/images/card_vfx/magic_circles/magic_circle_ink.png";
    internal const string MagicCircleKnockoutPath =
        MainFile.ResPath + "/images/card_vfx/magic_circles/magic_circle_knockout.png";
    internal static IReadOnlyList<string> AssetPaths { get; } =
        [WandPreludeShaderPath, MagicCircleInkPath, MagicCircleKnockoutPath];

    private const float SpinDecayDuration = 0.28f;
    private const float EnterDuration = 0.12f;
    private const float ColourTransitionDuration = 0.15f;
    private const float FadeOutStart = 0.85f;
    private const float Lifetime = 1.15f;
    private const float MagicCircleDiameter = 760f;
    private const float MagicCircleRadius = 340f;
    private const float MagicCircleEnterScale = 0.78f;
    private const float MagicCirclePulseScale = 1.04f;
    private const float MagicCircleExitScale = 0.82f;
    private const float MagicCircleFloorBias = 0.62f;
    private const int MagicCircleZIndex = -1;

    private static readonly Vector4 InitialLayerSpeeds = new(1.20f, -0.80f, 0f, 0.32f);
    private static readonly Vector4 SettleLayerSpeeds = new(0.30f, -0.20f, 0f, 0.08f);
    private static Shader? _wandPreludeShader;
    private static Texture2D? _magicCircleInk;
    private static Texture2D? _magicCircleKnockout;
    private static bool _showFailureLogged;

    private readonly NCombatRoom _room;
    private readonly Shader _shader;
    private readonly Texture2D _ink;
    private readonly Texture2D _knockout;
    private readonly Dictionary<Creature, CircleState> _states =
        new(ReferenceEqualityComparer.Instance);
    private readonly List<Creature> _expiredCasters = [];
    private bool _disposed;

    private SakuraMagicCirclePresenter(
        NCombatRoom room,
        Shader shader,
        Texture2D ink,
        Texture2D knockout)
    {
        _room = room;
        _shader = shader;
        _ink = ink;
        _knockout = knockout;
        Name = NodeName;
    }

    internal static void PreloadResources()
    {
        if (!TestMode.IsOn)
            _ = LoadResources();
    }

    internal static (Shader Shader, Texture2D Ink, Texture2D Knockout) LoadResources()
    {
        _wandPreludeShader ??= ResourceLoader.Load<Shader>(
            WandPreludeShaderPath,
            null,
            ResourceLoader.CacheMode.Reuse)
            ?? throw new InvalidOperationException($"Could not load {WandPreludeShaderPath}.");
        _magicCircleInk ??= ResourceLoader.Load<Texture2D>(
            MagicCircleInkPath,
            null,
            ResourceLoader.CacheMode.Reuse)
            ?? throw new InvalidOperationException($"Could not load {MagicCircleInkPath}.");
        _magicCircleKnockout ??= ResourceLoader.Load<Texture2D>(
            MagicCircleKnockoutPath,
            null,
            ResourceLoader.CacheMode.Reuse)
            ?? throw new InvalidOperationException($"Could not load {MagicCircleKnockoutPath}.");
        return (_wandPreludeShader, _magicCircleInk, _magicCircleKnockout);
    }

    internal static bool TryShowOrRefresh(Creature? caster, SourceEraClass era)
    {
        if (TestMode.IsOn || caster is null)
            return false;

        try
        {
            if (NCombatRoom.Instance is not { } room
                || room.CombatVfxContainer is null
                || room.GetCreatureNode(caster) is not { } casterNode)
            {
                return false;
            }

            var (shader, ink, knockout) = LoadResources();
            ShowOrRefresh(room, casterNode, era, shader, ink, knockout);
            return true;
        }
        catch (Exception exception)
        {
            if (!_showFailureLogged)
            {
                _showFailureLogged = true;
                MainFile.Logger.Error($"Could not show Sakura magic circle: {exception}");
            }

            return false;
        }
    }

    private static void ShowOrRefresh(
        NCombatRoom room,
        NCreature casterNode,
        SourceEraClass era,
        Shader shader,
        Texture2D ink,
        Texture2D knockout)
    {
        ArgumentNullException.ThrowIfNull(room);
        ArgumentNullException.ThrowIfNull(casterNode);
        ArgumentNullException.ThrowIfNull(shader);
        ArgumentNullException.ThrowIfNull(ink);
        ArgumentNullException.ThrowIfNull(knockout);

        var container = room.CombatVfxContainer;
        var presenter = container.GetNodeOrNull<SakuraMagicCirclePresenter>(NodeName);
        if (presenter is null
            || presenter._disposed
            || !ReferenceEquals(presenter._room, room)
            || !GodotObject.IsInstanceValid(presenter)
            || presenter.IsQueuedForDeletion())
        {
            presenter = new SakuraMagicCirclePresenter(room, shader, ink, knockout);
            container.AddChildSafely(presenter);
        }

        presenter.Refresh(casterNode, era);
    }

    internal static Color ColourFor(SourceEraClass era) => era switch
    {
        SourceEraClass.Clow => new Color(1f, 0.94f, 0.62f),
        SourceEraClass.Sakura => new Color(1f, 0.78f, 0.94f),
        SourceEraClass.Clear => new Color(0.88f, 1f, 0.8f),
        _ => throw new ArgumentOutOfRangeException(nameof(era), era, "Unknown magic-circle era.")
    };

    public override void _Ready()
    {
        CombatManager.Instance.CombatEnded += OnCombatEnded;
    }

    public override void _Process(double delta)
    {
        if (_disposed)
            return;

        _expiredCasters.Clear();
        foreach (var (caster, state) in _states)
        {
            if (!state.Update((float)delta))
                _expiredCasters.Add(caster);
        }

        foreach (var caster in _expiredCasters)
            RemoveState(caster);
    }

    public override void _ExitTree()
    {
        Cleanup(queueFreeChildren: false);
    }

    private void Refresh(NCreature casterNode, SourceEraClass era)
    {
        if (_disposed
            || !GodotObject.IsInstanceValid(casterNode)
            || !casterNode.IsInsideTree())
        {
            return;
        }

        var caster = casterNode.Entity;
        if (_states.TryGetValue(caster, out var existing))
        {
            if (existing.Matches(casterNode))
            {
                existing.Refresh(ColourFor(era));
                return;
            }

            RemoveState(caster);
        }

        var state = new CircleState(
            this,
            caster,
            casterNode,
            _shader,
            _ink,
            _knockout,
            ColourFor(era));
        _states.Add(caster, state);
    }

    private void RemoveState(Creature caster)
    {
        if (!_states.Remove(caster, out var state))
            return;

        state.Dispose(queueFree: true);
    }

    private void OnCombatEnded(CombatRoom _)
    {
        if (GodotObject.IsInstanceValid(this) && !IsQueuedForDeletion())
            this.QueueFreeSafely();
    }

    private void Cleanup(bool queueFreeChildren)
    {
        if (_disposed)
            return;

        _disposed = true;
        CombatManager.Instance.CombatEnded -= OnCombatEnded;
        foreach (var state in _states.Values)
            state.Dispose(queueFreeChildren);
        _states.Clear();
        _expiredCasters.Clear();
    }

    private static Vector2 ResolveMagicCircleCenter(NCreature casterNode)
    {
        if (CelVfxGeometry.ResolveCaster(casterNode) is not { } anchor)
            return Vector2.Zero;

        return new Vector2(
            anchor.BodyCenter.X,
            Mathf.Lerp(anchor.BodyCenter.Y, anchor.Floor.Y, MagicCircleFloorBias));
    }

    private static float EaseInOutSine(float progress) =>
        -(Mathf.Cos(Mathf.Pi * Mathf.Clamp(progress, 0f, 1f)) - 1f) * 0.5f;

    private sealed class CircleState
    {
        private readonly Creature _caster;
        private readonly NCreature _casterNode;
        private readonly Node2D _anchor;
        private readonly ColorRect _circle;
        private readonly ShaderMaterial _material;
        private Vector4 _phases;
        private float _spinAge;
        private float _triggerAge;
        private float _visibility;
        private float _entryVisibility;
        private float _scale = MagicCircleEnterScale;
        private float _entryScale = MagicCircleEnterScale;
        private Color _colour;
        private Color _colourStart;
        private Color _colourTarget;
        private float _colourAge;
        private bool _isRetrigger;
        private bool _disposed;

        public CircleState(
            Node parent,
            Creature caster,
            NCreature casterNode,
            Shader shader,
            Texture2D ink,
            Texture2D knockout,
            Color colour)
        {
            _caster = caster;
            _casterNode = casterNode;
            _colour = colour;
            _colourStart = colour;
            _colourTarget = colour;

            _anchor = new Node2D
            {
                Name = $"SakuraCelWandPreludeMagicCircleAnchor_{caster.GetHashCode():X8}",
                ZAsRelative = false,
                ZIndex = MagicCircleZIndex
            };
            try
            {
                parent.AddChildSafely(_anchor);
                _circle = new ColorRect
                {
                    Name = "SakuraCelWandPreludeMagicCircle",
                    Size = Vector2.One * MagicCircleDiameter,
                    Position = Vector2.One * MagicCircleDiameter * -0.5f,
                    PivotOffset = Vector2.One * MagicCircleDiameter * 0.5f,
                    Scale = Vector2.One * MagicCircleEnterScale,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    Material = new ShaderMaterial { Shader = shader }
                };
                _anchor.AddChildSafely(_circle);

                _material = CelVfxGeometry.DuplicateMaterial(_circle, "shared wand prelude magic circle");
                _material.SetShaderParameter("region_size", _circle.Size);
                _material.SetShaderParameter("magic_circle_ink", ink);
                _material.SetShaderParameter("magic_circle_knockout", knockout);
                _material.SetShaderParameter("magic_circle_colour", colour);
                _material.SetShaderParameter("magic_circle_enabled", 1f);
                _material.SetShaderParameter("magic_circle_visibility", 0f);
                _material.SetShaderParameter("magic_circle_radius", MagicCircleRadius);
                _material.SetShaderParameter("magic_circle_layer_phases", Vector4.Zero);
                _material.SetShaderParameter("speed_lines_enabled", 0f);
                _anchor.GlobalPosition = ResolveMagicCircleCenter(casterNode);
                Refresh(colour);
            }
            catch
            {
                if (GodotObject.IsInstanceValid(_anchor) && !_anchor.IsQueuedForDeletion())
                    _anchor.QueueFreeSafely();
                throw;
            }
        }

        public bool Matches(NCreature casterNode) =>
            !_disposed
            && ReferenceEquals(_casterNode, casterNode)
            && ReferenceEquals(casterNode.Entity, _caster);

        public void Refresh(Color colour)
        {
            if (_disposed)
                return;

            _isRetrigger = _visibility > 0.001f;
            _entryVisibility = _visibility;
            _entryScale = _scale;
            _triggerAge = 0f;
            _spinAge = 0f;
            _colourAge = 0f;
            _colourStart = _colour;
            _colourTarget = colour;
        }

        public bool Update(float delta)
        {
            if (!IsActive())
                return false;

            delta = Math.Max(0f, delta);
            _triggerAge += delta;
            _colourAge += delta;
            UpdateMotion(delta);
            UpdateEnvelope();
            UpdateColour();
            _anchor.GlobalPosition = ResolveMagicCircleCenter(_casterNode);

            _material.SetShaderParameter("magic_circle_layer_phases", _phases);
            _material.SetShaderParameter("magic_circle_visibility", _visibility);
            _material.SetShaderParameter("magic_circle_colour", _colour);
            _circle.Scale = Vector2.One * _scale;
            return _triggerAge < Lifetime;
        }

        public void Dispose(bool queueFree)
        {
            if (_disposed)
                return;

            _disposed = true;
            if (queueFree
                && GodotObject.IsInstanceValid(_anchor)
                && !_anchor.IsQueuedForDeletion())
            {
                _anchor.QueueFreeSafely();
            }
        }

        private bool IsActive() =>
            !_disposed
            && GodotObject.IsInstanceValid(_casterNode)
            && _casterNode.IsInsideTree()
            && ReferenceEquals(_casterNode.Entity, _caster)
            && GodotObject.IsInstanceValid(_anchor)
            && _anchor.IsInsideTree()
            && !_anchor.IsQueuedForDeletion()
            && GodotObject.IsInstanceValid(_circle)
            && GodotObject.IsInstanceValid(_material);

        private void UpdateMotion(float delta)
        {
            var nextSpinAge = _spinAge + delta;
            var decayIntegral = SpinDecayDuration
                * (Mathf.Exp(-_spinAge / SpinDecayDuration)
                    - Mathf.Exp(-nextSpinAge / SpinDecayDuration));
            _phases += SettleLayerSpeeds * delta
                + (InitialLayerSpeeds - SettleLayerSpeeds) * decayIntegral;
            _spinAge = nextSpinAge;
        }

        private void UpdateEnvelope()
        {
            if (_triggerAge < EnterDuration)
            {
                var progress = EaseInOutSine(_triggerAge / EnterDuration);
                _visibility = Mathf.Lerp(_entryVisibility, 1f, progress);
                if (_isRetrigger)
                {
                    var pulseProgress = progress * 2f;
                    _scale = pulseProgress < 1f
                        ? Mathf.Lerp(_entryScale, MagicCirclePulseScale, EaseInOutSine(pulseProgress))
                        : Mathf.Lerp(MagicCirclePulseScale, 1f, EaseInOutSine(pulseProgress - 1f));
                }
                else
                {
                    _scale = Mathf.Lerp(_entryScale, 1f, progress);
                }
                return;
            }

            if (_triggerAge < FadeOutStart)
            {
                _visibility = 1f;
                _scale = 1f;
                return;
            }

            var fadeProgress = EaseInOutSine(
                (_triggerAge - FadeOutStart) / (Lifetime - FadeOutStart));
            _visibility = 1f - fadeProgress;
            _scale = Mathf.Lerp(1f, MagicCircleExitScale, fadeProgress);
        }

        private void UpdateColour()
        {
            var progress = EaseInOutSine(_colourAge / ColourTransitionDuration);
            _colour = _colourStart.Lerp(_colourTarget, progress);
        }
    }
}
