using Godot;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.TestSupport;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;

namespace SakuraMod.SakuraModCode.Character;

internal static partial class SakuraGlowVisual
{
    internal const string ShaderPath =
        MainFile.ResPath + "/shaders/card_vfx/sakura_glow_mote.gdshader";
    internal static IReadOnlyList<string> AssetPaths { get; } = [ShaderPath];

    private const string RootName = "SakuraTheGlow";
    private const int MoteCount = 5;
    private const float MoteSize = 54f;
    private const float MaxAlpha = 0.72f;
    private const float TriggerAlpha = 0.90f;
    private const float FadeDuration = 0.18f;
    private const float TriggerDuration = 0.82f;
    private const float TriggerDecay = 2.4f;
    private const int GlowZIndex = 2;
    private static readonly Color CoreColor = new("d7fff0");
    private static readonly Color HaloColor = new("4fd6a0");
    private static readonly ConditionalWeakTable<Creature, GlowState> States = [];

    internal static void Mount(NCreature creatureNode)
    {
        if (TestMode.IsOn
            || !SakuraModConfig.IsCardVfxEnabled()
            || creatureNode.Entity.Player is not { Character: ClassicSakura } player
            || player.Creature.CombatState is not { } combatState
            || NCombatRoom.Instance is not { CombatVfxContainer: { } container }
            || !GodotObject.IsInstanceValid(container))
        {
            return;
        }

        if (container.GetNodeOrNull<GlowRoot>(RootName) is not null
            || States.TryGetValue(player.Creature, out _))
        {
            return;
        }

        GlowRoot? root = null;
        try
        {
            var shader = PreloadManager.Cache.GetAsset<Shader>(ShaderPath);
            if (shader is null)
                return;

            root = BuildRoot(shader);
            container.AddChildSafely(root);
            container.MoveChildSafely(root, container.GetChildCount() - 1);

            var state = new GlowState(root, player, creatureNode, combatState);
            States.Add(player.Creature, state);
            state.Start();
        }
        catch (Exception exception)
        {
            States.Remove(player.Creature);
            root?.QueueFreeSafely();
            MainFile.Logger.Error($"Could not mount The Glow visual: {exception}");
        }
    }

    internal static void NotifyMagicChargeGained(Creature creature, int amount)
    {
        if (amount <= 0 || TestMode.IsOn || !SakuraModConfig.IsCardVfxEnabled())
            return;

        if (States.TryGetValue(creature, out var state))
            state.Trigger(amount);
    }

    private static GlowRoot BuildRoot(Shader shader)
    {
        var root = new GlowRoot
        {
            Name = RootName,
            ZAsRelative = false,
            ZIndex = GlowZIndex,
            Modulate = Colors.Transparent,
            Visible = false
        };

        for (var index = 0; index < MoteCount; index++)
        {
            var carrier = new Node2D { Name = $"Mote{index + 1}" };
            var body = new ColorRect
            {
                Name = "Light",
                Size = Vector2.One * MoteSize,
                Position = Vector2.One * (-MoteSize * 0.5f),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Material = new ShaderMaterial { Shader = shader }
            };
            carrier.AddChild(body);
            root.AddChild(carrier);
        }

        return root;
    }

    private sealed partial class GlowRoot : Node2D
    {
        internal Action<float>? Tick;

        public override void _Process(double delta) => Tick?.Invoke((float)delta);
    }

    private sealed class GlowState : IDisposable
    {
        private readonly GlowRoot _root;
        private readonly Player _player;
        private readonly Creature _creature;
        private readonly NCreature _creatureNode;
        private readonly ICombatState _combatState;
        private readonly Mote[] _motes;
        private Tween? _fadeTween;
        private float _elapsed;
        private float _trigger;
        private float _transientRemaining;
        private float _targetAlpha;
        private bool _persistent;
        private bool _disposed;

        internal GlowState(
            GlowRoot root,
            Player player,
            NCreature creatureNode,
            ICombatState combatState)
        {
            _root = root;
            _player = player;
            _creature = player.Creature;
            _creatureNode = creatureNode;
            _combatState = combatState;
            _motes = BuildMotes(root);
        }

        internal void Start()
        {
            _creature.PowerApplied += OnPowerApplied;
            _creature.PowerIncreased += OnPowerIncreased;
            _creature.PowerDecreased += OnPowerDecreased;
            _creature.PowerRemoved += OnPowerRemoved;
            _creature.Died += OnCreatureDied;
            CombatManager.Instance.CombatEnded += OnCombatEnded;
            _root.TreeExiting += OnTreeExiting;
            _root.Tick = Tick;
            RefreshActivation();
        }

        internal void Trigger(int amount)
        {
            if (_disposed || !IsCurrentMount())
                return;

            _trigger = Mathf.Clamp(_trigger + 0.18f + amount * 0.05f, 0f, 1f);
            _transientRemaining = Math.Max(_transientRemaining, TriggerDuration);
            SetTargetAlpha(TriggerAlpha);
        }

        private void Tick(float delta)
        {
            if (_disposed)
                return;
            if (!SakuraModConfig.IsCardVfxEnabled() || !IsCurrentMount())
            {
                DisposeAndFree();
                return;
            }

            _elapsed += Math.Max(0f, delta);
            _transientRemaining = Math.Max(0f, _transientRemaining - Math.Max(0f, delta));
            _trigger = Mathf.MoveToward(_trigger, 0f, Math.Max(0f, delta) * TriggerDecay);
            if (!_persistent && _transientRemaining <= 0f)
                SetTargetAlpha(0f);

            if (CelVfxGeometry.ResolveCaster(_creatureNode) is not { } anchor)
                return;

            _root.GlobalPosition = new Vector2(
                anchor.BodyCenter.X,
                Mathf.Lerp(anchor.BodyCenter.Y, anchor.Floor.Y, 0.34f));
            var facing = anchor.FacingSign;
            for (var index = 0; index < _motes.Length; index++)
            {
                var mote = _motes[index];
                var angle = mote.Phase + _elapsed * mote.Speed;
                var offset = new Vector2(
                    Mathf.Cos(angle) * mote.Radius * facing,
                    Mathf.Sin(angle) * mote.Radius * 0.38f
                        + Mathf.Sin(_elapsed * 0.42f + mote.Phase) * mote.VerticalDrift);
                mote.Carrier.Position = offset;
                mote.Material.SetShaderParameter("elapsed", _elapsed);
                mote.Material.SetShaderParameter("phase", mote.Phase);
                mote.Material.SetShaderParameter("trigger", _trigger);
                mote.Material.SetShaderParameter("core_color", CoreColor);
                mote.Material.SetShaderParameter("halo_color", HaloColor);
                mote.Material.SetShaderParameter("twinkle_speed", mote.TwinkleSpeed);
                mote.Material.SetShaderParameter("twinkle_depth", 0.16f + _trigger * 0.12f);
                mote.Material.SetShaderParameter("glint_strength", 0.20f + _trigger * 0.24f);
            }
        }

        private void OnPowerApplied(PowerModel power) => RefreshForPower(power);

        private void OnPowerIncreased(PowerModel power, int _, bool __) => RefreshForPower(power);

        private void OnPowerDecreased(PowerModel power, bool _) => RefreshForPower(power);

        private void OnPowerRemoved(PowerModel power) => RefreshForPower(power);

        private void RefreshForPower(PowerModel power)
        {
            if (power is ClassicGlowPower)
                RefreshActivation();
        }

        private void RefreshActivation()
        {
            if (_disposed)
                return;
            if (!IsCurrentMount())
            {
                DisposeAndFree();
                return;
            }

            _persistent = _creature.GetPower<ClassicGlowPower>() is { Amount: > 0 };
            SetTargetAlpha(_persistent ? MaxAlpha : _transientRemaining > 0f ? TriggerAlpha : 0f);
        }

        private void SetTargetAlpha(float targetAlpha)
        {
            if (_disposed || !GodotObject.IsInstanceValid(_root))
                return;

            targetAlpha = Mathf.Clamp(targetAlpha, 0f, 1f);
            if (Mathf.IsEqualApprox(_targetAlpha, targetAlpha))
                return;

            _targetAlpha = targetAlpha;
            if (_fadeTween is { } current && current.IsValid())
                current.Kill();

            var currentAlpha = _root.Modulate.A;
            var duration = Mathf.Max(0.04f, Mathf.Abs(targetAlpha - currentAlpha) * FadeDuration);
            _fadeTween = _root.CreateTween();
            _fadeTween.TweenProperty(_root, "modulate:a", targetAlpha, duration)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(targetAlpha > currentAlpha ? Tween.EaseType.Out : Tween.EaseType.In);
            _fadeTween.TweenCallback(Callable.From(() =>
            {
                if (!_disposed && Mathf.IsEqualApprox(_targetAlpha, targetAlpha))
                    _root.Visible = targetAlpha > 0.001f;
            }));
            _root.Visible = currentAlpha > 0.001f || targetAlpha > 0.001f;
        }

        private bool IsCurrentMount() =>
            GodotObject.IsInstanceValid(_root)
            && _root.IsInsideTree()
            && GodotObject.IsInstanceValid(_root.GetParent())
            && GodotObject.IsInstanceValid(_creatureNode)
            && _creatureNode.IsInsideTree()
            && ReferenceEquals(_creatureNode.Entity.Player, _player)
            && ReferenceEquals(_creature.CombatState, _combatState);

        private void OnCreatureDied(Creature creature)
        {
            if (ReferenceEquals(creature, _creature))
                DisposeAndFree();
        }

        private void OnCombatEnded(CombatRoom _) => DisposeAndFree();

        private void OnTreeExiting() => Dispose();

        private void DisposeAndFree()
        {
            Dispose();
            if (GodotObject.IsInstanceValid(_root) && !_root.IsQueuedForDeletion())
                _root.QueueFreeSafely();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            States.Remove(_creature);
            _creature.PowerApplied -= OnPowerApplied;
            _creature.PowerIncreased -= OnPowerIncreased;
            _creature.PowerDecreased -= OnPowerDecreased;
            _creature.PowerRemoved -= OnPowerRemoved;
            _creature.Died -= OnCreatureDied;
            CombatManager.Instance.CombatEnded -= OnCombatEnded;
            _root.TreeExiting -= OnTreeExiting;
            _root.Tick = null;
            if (_fadeTween is { } tween && tween.IsValid())
                tween.Kill();
            _fadeTween = null;
        }

        private static Mote[] BuildMotes(GlowRoot root)
        {
            var motes = new Mote[MoteCount];
            for (var index = 0; index < MoteCount; index++)
            {
                var carrier = root.GetNode<Node2D>($"Mote{index + 1}");
                var body = carrier.GetNode<ColorRect>("Light");
                motes[index] = new Mote(
                    carrier,
                    body.Material as ShaderMaterial
                        ?? throw new InvalidOperationException("The Glow mote requires a ShaderMaterial."),
                    index,
                    0.75f + index * 0.11f,
                    34f + (index % 3) * 8f,
                    8f + (index % 2) * 4f,
                    0.92f + index * 0.08f);
            }

            return motes;
        }
    }

    private sealed record Mote(
        Node2D Carrier,
        ShaderMaterial Material,
        int Index,
        float Speed,
        float Radius,
        float VerticalDrift,
        float TwinkleSpeed)
    {
        internal float Phase => Index * 1.2566371f + 0.37f;
    }
}
