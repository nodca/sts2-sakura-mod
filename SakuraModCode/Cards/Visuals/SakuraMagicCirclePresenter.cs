using Godot;
using MegaCrit.Sts2.Core.Assets;
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
    internal const string ClowInkPath = MainFile.ResPath + "/images/card_vfx/magic_circles/clow_ink.png";
    internal const string ClowKnockoutPath = MainFile.ResPath + "/images/card_vfx/magic_circles/clow_knockout.png";
    internal const string ClearInkPath = MainFile.ResPath + "/images/card_vfx/magic_circles/clear_ink.png";
    internal const string ClearKnockoutPath = MainFile.ResPath + "/images/card_vfx/magic_circles/clear_knockout.png";
    internal static IReadOnlyList<string> AssetPaths { get; } =
        [WandPreludeShaderPath, MagicCircleInkPath, MagicCircleKnockoutPath,
            ClowInkPath, ClowKnockoutPath, ClearInkPath, ClearKnockoutPath];

    private const float MagicCircleDiameter = 760f;
    private const float MagicCircleRadius = 340f;
    private const float MagicCircleFloorBias = 0.62f;
    private const int MagicCircleZIndex = -1;

    private static bool _showFailureLogged;

    private readonly NCombatRoom _room;
    private readonly Dictionary<Creature, CircleState> _states =
        new(ReferenceEqualityComparer.Instance);
    private readonly List<Creature> _expiredCasters = [];
    private bool _disposed;

    private SakuraMagicCirclePresenter(NCombatRoom room)
    {
        _room = room;
        Name = NodeName;
    }

    internal static (Shader Shader, Texture2D Ink, Texture2D Knockout) LoadResources() =>
        (
            PreloadManager.Cache.GetAsset<Shader>(WandPreludeShaderPath),
            PreloadManager.Cache.GetAsset<Texture2D>(MagicCircleInkPath),
            PreloadManager.Cache.GetAsset<Texture2D>(MagicCircleKnockoutPath)
        );

    internal static (string Ink, string Knockout) MaskPathsFor(SourceEraClass era) => era switch
    {
        SourceEraClass.Clow => (ClowInkPath, ClowKnockoutPath),
        SourceEraClass.Clear => (ClearInkPath, ClearKnockoutPath),
        SourceEraClass.Sakura => (MagicCircleInkPath, MagicCircleKnockoutPath),
        _ => throw new ArgumentOutOfRangeException(nameof(era), era, "Unknown magic-circle era.")
    };

    internal static (Shader Shader, Texture2D Ink, Texture2D Knockout) LoadResources(SourceEraClass era)
    {
        var paths = MaskPathsFor(era);
        return (PreloadManager.Cache.GetAsset<Shader>(WandPreludeShaderPath),
            PreloadManager.Cache.GetAsset<Texture2D>(paths.Ink),
            PreloadManager.Cache.GetAsset<Texture2D>(paths.Knockout));
    }

    internal static bool TryShowOrRefresh(Creature? caster, SourceEraClass era)
    {
        if (!SakuraModConfig.IsCardVfxEnabled() || TestMode.IsOn || caster is null)
            return false;

        try
        {
            if (NCombatRoom.Instance is not { } room
                || room.CombatVfxContainer is null
                || room.GetCreatureNode(caster) is not { } casterNode)
            {
                return false;
            }

            var (shader, ink, knockout) = LoadResources(era);
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
            presenter = new SakuraMagicCirclePresenter(room);
            container.AddChildSafely(presenter);
        }

        presenter.Refresh(casterNode, era, shader, ink, knockout);
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

    private void Refresh(NCreature casterNode, SourceEraClass era, Shader shader, Texture2D ink, Texture2D knockout)
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
                existing.Refresh(era, ink, knockout);
                return;
            }

            RemoveState(caster);
        }

        var state = new CircleState(
            this,
            caster,
            casterNode,
            shader,
            ink,
            knockout,
            era);
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

    private sealed class CircleState
    {
        private readonly Creature _caster;
        private readonly NCreature _casterNode;
        private readonly Node2D _anchor;
        private ColorRect _circle;
        private ShaderMaterial _material;
        private ColorRect _outgoing;
        private ShaderMaterial _outgoingMaterial;
        private SakuraMagicCircleMotion _motion;
        private float _transitionAge = SakuraMagicCircleMotion.TransitionDuration;
        private float _outgoingVisibility;
        private bool _disposed;

        public CircleState(
            Node parent,
            Creature caster,
            NCreature casterNode,
            Shader shader,
            Texture2D ink,
            Texture2D knockout,
            SourceEraClass era)
        {
            _caster = caster;
            _casterNode = casterNode;
            _motion = new SakuraMagicCircleMotion(era);

            _anchor = new Node2D
            {
                Name = $"SakuraCelWandPreludeMagicCircleAnchor_{caster.GetHashCode():X8}",
                ZAsRelative = false,
                ZIndex = MagicCircleZIndex
            };
            try
            {
                parent.AddChildSafely(_anchor);
                (_circle, _material) = CreateSurface(shader);
                (_outgoing, _outgoingMaterial) = CreateSurface(shader);
                _outgoing.Visible = false;
                Configure(era, ink, knockout);
                ApplyMotion();
                _anchor.GlobalPosition = ResolveMagicCircleCenter(casterNode);
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

        private (ColorRect, ShaderMaterial) CreateSurface(Shader shader)
        {
            var circle = new ColorRect
            {
                Name = "SakuraCelWandPreludeMagicCircle",
                Size = Vector2.One * MagicCircleDiameter,
                Position = Vector2.One * MagicCircleDiameter * -0.5f,
                PivotOffset = Vector2.One * MagicCircleDiameter * 0.5f,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Material = new ShaderMaterial { Shader = shader }
            };
            _anchor.AddChildSafely(circle);
            var material = CelVfxGeometry.DuplicateMaterial(circle, "shared magic circle");
            material.SetShaderParameter("region_size", circle.Size);
            material.SetShaderParameter("magic_circle_enabled", 1f);
            material.SetShaderParameter("magic_circle_visibility", 0f);
            material.SetShaderParameter("magic_circle_radius", MagicCircleRadius);
            material.SetShaderParameter("speed_lines_enabled", 0f);
            return (circle, material);
        }

        private void Configure(SourceEraClass era, Texture2D ink, Texture2D knockout)
        {
            _material.SetShaderParameter("magic_circle_ink", ink);
            _material.SetShaderParameter("magic_circle_knockout", knockout);
            _material.SetShaderParameter("magic_circle_colour", ColourFor(era));
            _material.SetShaderParameter("magic_circle_polish", era == SourceEraClass.Sakura ? 0f : 1f);
        }

        public void Refresh(SourceEraClass era, Texture2D ink, Texture2D knockout)
        {
            if (_disposed)
                return;

            if (_motion.Era == era)
            {
                _motion.Refresh();
                return;
            }

            // Two reusable surfaces bound rapid cross-era plays. Keep the last visible
            // outgoing pose if another play arrives before the new rings are visible.
            if (_motion.Visibility * _motion.Gates.X > 0.001f)
            {
                (_circle, _outgoing) = (_outgoing, _circle);
                (_material, _outgoingMaterial) = (_outgoingMaterial, _material);
                _outgoingVisibility = _motion.Visibility;
            }
            else
                _outgoingVisibility *= 1f - SakuraMagicCircleMotion.Ease(
                    _transitionAge / SakuraMagicCircleMotion.TransitionDuration);
            _transitionAge = 0f;
            _motion = new SakuraMagicCircleMotion(era, transition: true);
            Configure(era, ink, knockout);
            ApplyMotion();
            _outgoing.Visible = _outgoingVisibility > 0f;
            _circle.Visible = true;
            _anchor.MoveChild(_outgoing, 0);
        }

        public bool Update(float delta)
        {
            if (!IsActive())
                return false;

            if (!float.IsFinite(delta) || delta < 0f)
                delta = 0f;
            _motion.Advance(delta);
            _transitionAge += delta;
            _outgoingMaterial.SetShaderParameter("magic_circle_visibility", _outgoingVisibility
                * (1f - SakuraMagicCircleMotion.Ease(_transitionAge / SakuraMagicCircleMotion.TransitionDuration)));
            _outgoing.Visible = _transitionAge < SakuraMagicCircleMotion.TransitionDuration;
            _anchor.GlobalPosition = ResolveMagicCircleCenter(_casterNode);
            ApplyMotion();
            return _motion.IsAlive;
        }

        private void ApplyMotion()
        {
            _material.SetShaderParameter("magic_circle_layer_phases", _motion.Phases);
            _material.SetShaderParameter("magic_circle_visibility", _motion.Visibility);
            _material.SetShaderParameter("magic_circle_layer_visibility", _motion.Gates);
            _material.SetShaderParameter("magic_circle_pulse", _motion.Pulse);
            _circle.Scale = Vector2.One * _motion.Scale;
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
            && GodotObject.IsInstanceValid(_material)
            && GodotObject.IsInstanceValid(_outgoing)
            && GodotObject.IsInstanceValid(_outgoingMaterial);
    }
}
