using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.TestSupport;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Classic.Cards;
using SakuraMod.SakuraModCode.Classic.Powers;
using System.Runtime.CompilerServices;

namespace SakuraMod.SakuraModCode.Classic.Character;

internal static class SakuraCombatResourceHud
{
    internal static void Mount(NCombatUi ui, CombatState combatState)
    {
        SakuraElementStateHud.Mount(ui, combatState);
        SakuraMagicChargeHud.Mount(ui, combatState);
    }

    internal static void Unmount(NCombatUi ui)
    {
        SakuraMagicChargeHud.Unmount(ui);
        SakuraElementStateHud.Unmount(ui);
    }
}

internal static class SakuraMagicChargeHud
{
    internal const string ScenePath = MainFile.ResPath + "/scenes/combat/sakura_magic_charge_hud.tscn";
    private const float MountHorizontalOffset = -50f;
    private const float MountVerticalOffset = 40f;
    private const float LiquidFillDuration = 0.34f;
    private const string LiquidFillParameterName = "fill_ratio";
    private static readonly Vector2 BaseScale = new(0.8f, 0.8f);
    private static readonly Color ZeroColor = new(0.46f, 0.49f, 0.52f, 0.62f);
    private static readonly Color LowColor = new(0.66f, 0.78f, 0.79f, 0.9f);
    private static readonly Color ResonantColor = new(0.76f, 1.08f, 1.04f, 1f);
    private static readonly Color SpentColor = new(0.6f, 0.64f, 0.66f, 0.78f);
    private static readonly Color FullColor = new(1.14f, 1.06f, 0.72f, 1f);
    private static readonly Color LockedColor = new(0.74f, 0.67f, 0.62f, 0.82f);
    private static readonly Color ResonantGlow = new(0.55f, 1.15f, 1.1f, 0.82f);
    private static readonly Color FullGlow = new(1.2f, 1.04f, 0.54f, 0.9f);
    private static readonly ConditionalWeakTable<NCombatUi, HudState> MountedStates = new();

    internal static float LiquidFillFor(int amount) =>
        Math.Clamp(amount / (float)ClassicSakuraMagic.ExtraEffectCost, 0f, 1f);

    internal static void Mount(NCombatUi ui, CombatState combatState)
    {
        if (TestMode.IsOn || ui.EnergyCounterContainer is not { } container)
            return;

        var player = LocalContext.GetMe(combatState);
        if (player is null || !SakuraStarterCompatibility.IsKinomotoSakura(player))
            return;

        Unmount(ui);
        var energyCounter = container.GetChildren().OfType<NEnergyCounter>().FirstOrDefault();
        if (energyCounter is null)
            throw new InvalidOperationException("Kinomoto Sakura Magic Charge HUD requires the active energy counter.");

        var scene = ResourceLoader.Load<PackedScene>(ScenePath)
            ?? throw new InvalidOperationException($"Could not load Kinomoto Sakura Magic Charge HUD scene: {ScenePath}");
        var root = scene.Instantiate<Control>();
        root.Position = new Vector2(MountHorizontalOffset, MountVerticalOffset);
        energyCounter.AddChildSafely(root);

        var state = new HudState(player, combatState, root);
        MountedStates.Add(ui, state);
        root.TreeExiting += () => OnRootExiting(ui, state);
        state.Refresh(animate: false);
    }

    internal static void Unmount(NCombatUi ui)
    {
        if (!MountedStates.TryGetValue(ui, out var state))
            return;

        MountedStates.Remove(ui);
        state.Dispose();
        if (GodotObject.IsInstanceValid(state.Root) && !state.Root.IsQueuedForDeletion())
            state.Root.QueueFreeSafely();
    }

    private static void OnRootExiting(NCombatUi ui, HudState state)
    {
        if (MountedStates.TryGetValue(ui, out var mounted) && ReferenceEquals(mounted, state))
            MountedStates.Remove(ui);
        state.Dispose();
    }

    private sealed class HudState : IDisposable
    {
        private readonly Player _player;
        private readonly Creature _creature;
        private readonly CombatState _combatState;
        private readonly TextureRect _glow;
        private readonly TextureRect _emblem;
        private readonly TextureRect _chargeLiquid;
        private readonly ShaderMaterial _chargeLiquidMaterial;
        private readonly Label _amount;
        private ClassicMagicChargePower? _chargePower;
        private SakuraMagicChargeProjection? _projection;
        private Tween? _liquidTween;
        private Tween? _pulseTween;
        private float _displayedLiquidFill;
        private bool _disposed;

        internal HudState(Player player, CombatState combatState, Control root)
        {
            _player = player;
            _creature = player.Creature;
            _combatState = combatState;
            Root = root;
            _glow = root.GetNode<TextureRect>("%Glow");
            _emblem = root.GetNode<TextureRect>("%Emblem");
            _chargeLiquid = root.GetNode<TextureRect>("%ChargeLiquid");
            _chargeLiquidMaterial = _chargeLiquid.Material as ShaderMaterial
                ?? throw new InvalidOperationException("Kinomoto Sakura Magic Charge liquid requires its scene-owned ShaderMaterial.");
            _amount = root.GetNode<Label>("%Amount");

            Root.MouseEntered += ShowHoverTip;
            Root.MouseExited += HideHoverTip;
            _creature.PowerApplied += OnPowerApplied;
            _creature.PowerIncreased += OnPowerIncreased;
            _creature.PowerDecreased += OnPowerDecreased;
            _creature.PowerRemoved += OnPowerRemoved;
        }

        internal Control Root { get; }

        internal void Refresh(bool animate)
        {
            if (_disposed)
                return;

            BindChargePower(_creature.GetPower<ClassicMagicChargePower>());
            var next = SakuraMagicChargeProjection.From(
                _chargePower?.Amount ?? 0,
                _chargePower?.ArmedOpportunityGeneration > 0,
                SakuraExtraEffectTransaction.CanActivate(_player));
            if (next == _projection)
                return;

            var previous = _projection;
            _projection = next;
            var shouldAnimate = animate && previous is not null;
            Apply(next, shouldAnimate);
            if (shouldAnimate)
                Pulse(next.State);
        }

        private void Apply(SakuraMagicChargeProjection projection, bool animate)
        {
            _amount.Text = projection.Amount.ToString();
            _amount.AddThemeFontSizeOverride("font_size", projection.Amount >= 100 ? 25 : projection.Amount >= 10 ? 31 : 36);
            SetLiquidFill(LiquidFillFor(projection.Amount), animate);

            var color = projection.State switch
            {
                SakuraMagicChargeHudState.Zero => ZeroColor,
                SakuraMagicChargeHudState.Low => LowColor,
                SakuraMagicChargeHudState.ResonantReady => ResonantColor,
                SakuraMagicChargeHudState.ResonantSpent => SpentColor,
                SakuraMagicChargeHudState.FullReady => FullColor,
                SakuraMagicChargeHudState.FullLocked => LockedColor,
                _ => Colors.White,
            };
            _emblem.Modulate = color;
            _chargeLiquid.Modulate = projection.State switch
            {
                SakuraMagicChargeHudState.ResonantSpent => SpentColor,
                SakuraMagicChargeHudState.FullLocked => LockedColor,
                _ => Colors.White,
            };
            _amount.Modulate = color;

            _glow.Visible = projection.State is SakuraMagicChargeHudState.ResonantReady
                or SakuraMagicChargeHudState.FullReady;
            _glow.Modulate = projection.State == SakuraMagicChargeHudState.FullReady
                ? FullGlow
                : ResonantGlow;
        }

        private void SetLiquidFill(float target, bool animate)
        {
            ResetLiquidTween();
            if (!animate || !Root.IsInsideTree() || Mathf.IsEqualApprox(_displayedLiquidFill, target))
            {
                SetDisplayedLiquidFill(target);
                return;
            }

            _liquidTween = Root.CreateTween();
            _liquidTween.TweenMethod(
                    Callable.From<float>(SetDisplayedLiquidFill),
                    _displayedLiquidFill,
                    target,
                    LiquidFillDuration)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Sine);
        }

        private void SetDisplayedLiquidFill(float value)
        {
            _displayedLiquidFill = value;
            _chargeLiquidMaterial.SetShaderParameter(LiquidFillParameterName, value);
        }

        private void Pulse(SakuraMagicChargeHudState state)
        {
            ResetPulse();
            if (!Root.IsInsideTree())
                return;

            var pulseScale = state is SakuraMagicChargeHudState.ResonantReady or SakuraMagicChargeHudState.FullReady
                ? 1.14f
                : 1.07f;
            Root.Scale = BaseScale * pulseScale;
            _pulseTween = Root.CreateTween();
            _pulseTween.TweenProperty(Root, "scale", BaseScale, 0.2f)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Back);
        }

        private void OnPowerApplied(PowerModel power) => RefreshForPower(power);
        private void OnPowerIncreased(PowerModel power, int _, bool __) => RefreshForPower(power);
        private void OnPowerDecreased(PowerModel power, bool _) => RefreshForPower(power);
        private void OnPowerRemoved(PowerModel power) => RefreshForPower(power);

        private void RefreshForPower(PowerModel power)
        {
            if (_disposed
                || !GodotObject.IsInstanceValid(Root)
                || !Root.IsInsideTree()
                || !ReferenceEquals(_creature.CombatState, _combatState)
                || power is not (ClassicMagicChargePower or ClassicLockPower))
            {
                return;
            }

            Refresh(animate: true);
        }

        private void BindChargePower(ClassicMagicChargePower? power)
        {
            if (ReferenceEquals(_chargePower, power))
                return;

            if (_chargePower is not null)
                _chargePower.ProjectionChanged -= OnProjectionChanged;
            _chargePower = power;
            if (_chargePower is not null)
                _chargePower.ProjectionChanged += OnProjectionChanged;
        }

        private void OnProjectionChanged() => Refresh(animate: true);

        private void ShowHoverTip() =>
            NHoverTipSet.CreateAndShow(
                Root,
                HoverTipFactory.FromPower<ClassicMagicChargePower>(),
                HoverTipAlignment.Right);

        private void HideHoverTip() => NHoverTipSet.Remove(Root);

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            BindChargePower(null);
            _creature.PowerApplied -= OnPowerApplied;
            _creature.PowerIncreased -= OnPowerIncreased;
            _creature.PowerDecreased -= OnPowerDecreased;
            _creature.PowerRemoved -= OnPowerRemoved;
            Root.MouseEntered -= ShowHoverTip;
            Root.MouseExited -= HideHoverTip;
            HideHoverTip();
            ResetLiquidTween();
            ResetPulse();
        }

        private void ResetLiquidTween()
        {
            if (_liquidTween is { } tween && tween.IsValid())
                tween.Kill();
            _liquidTween = null;
        }

        private void ResetPulse()
        {
            if (_pulseTween is { } tween && tween.IsValid())
                tween.Kill();
            _pulseTween = null;
            Root.Scale = BaseScale;
        }
    }
}
