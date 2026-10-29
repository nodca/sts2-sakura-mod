using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.TestSupport;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.FourthAct.Dark.Powers;
using SakuraMod.SakuraModCode.FourthAct.Wind.Powers;
using SakuraMod.SakuraModCode.FourthAct.Water.Powers;
using SakuraMod.SakuraModCode.FourthAct.Fire.Powers;
using SakuraMod.SakuraModCode.FourthAct.Earth.Powers;
using STS2RitsuLib;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.Patching.Models;
using System.Runtime.CompilerServices;

namespace SakuraMod.SakuraModCode.Character;

internal static class SakuraElementFacetProjection
{
    /// <summary>The 176 px board inside the 208 px root; the margin only holds glow.</summary>
    internal const float DiscRadiusRatio = 88f / 208f;

    /// <summary>Anime Lasin-board layout: water top, fire right, wind bottom, earth left.</summary>
    public static SakuraElement? FromOffset(float horizontal, float vertical, float radius)
    {
        if ((horizontal * horizontal) + (vertical * vertical) > radius * radius)
            return null;

        if (MathF.Abs(horizontal) > MathF.Abs(vertical))
            return horizontal >= 0f ? SakuraElement.Fire : SakuraElement.Earth;

        return vertical >= 0f ? SakuraElement.Wind : SakuraElement.Water;
    }
}

/// <summary>
/// Syaoran's Lasin board above Kinomoto Sakura's energy counter: the one persistent
/// presentation of the four element states. Each medallion shows whether its state
/// is up, how many turns it has left or that it is permanent, and whether a
/// sovereignty locks it. Trigger beams on the stage leave from these medallions.
/// </summary>
internal static class SakuraElementStateHud
{
    internal const string ScenePath = MainFile.ResPath + "/scenes/combat/sakura_element_state_hud.tscn";
    private const float MountHorizontalOffset = 8f;
    private const float MountGap = 12f;
    /// <summary>Transparent margin around the board that only holds medallion glow.</summary>
    private const float BoardMargin = 16f;
    private const float LightUpDuration = 0.45f;
    private const float FlashDuration = 0.36f;
    private static readonly Color LockedPlateTint = new(0.62f, 0.64f, 0.66f, 1f);
    private static readonly Color DialFlashColor = new(1.7f, 1.55f, 1.15f, 1f);
    private static readonly Color CrackPulseColor = new(1.25f, 1.18f, 0.82f, 1f);
    private static readonly ConditionalWeakTable<NCombatUi, HudState> MountedStates = new();
    private static readonly ConditionalWeakTable<Player, HudState> MountedPlayerStates = new();

    public static void Mount(NCombatUi ui, CombatState combatState)
    {
        if (TestMode.IsOn || ui.EnergyCounterContainer is not { } container)
            return;

        var player = LocalContext.GetMe(combatState);
        if (player is null || !SakuraStarterCompatibility.IsKinomotoSakura(player))
            return;

        Unmount(ui);
        var energyCounter = container.GetChildren().OfType<NEnergyCounter>().FirstOrDefault();
        if (energyCounter is null)
            throw new InvalidOperationException("Kinomoto Sakura element HUD requires the active energy counter.");

        var scene = ResourceLoader.Load<PackedScene>(ScenePath)
            ?? throw new InvalidOperationException($"Could not load Kinomoto Sakura element HUD scene: {ScenePath}");
        var root = scene.Instantiate<Control>();
        // The gap is measured from the visible board, not from the glow margin around it.
        root.Position = new Vector2(
            ((energyCounter.Size.X - root.Size.X) * 0.5f) + MountHorizontalOffset,
            -root.Size.Y + BoardMargin - MountGap);
        energyCounter.AddChildSafely(root);

        var state = new HudState(player, combatState, root);
        MountedStates.Add(ui, state);
        MountedPlayerStates.Add(player, state);
        root.TreeExiting += () => OnRootExiting(ui, state);
        state.Refresh(animate: false);
    }

    public static void Unmount(NCombatUi ui)
    {
        if (!MountedStates.TryGetValue(ui, out var state))
            return;

        MountedStates.Remove(ui);
        MountedPlayerStates.Remove(state.Player);
        state.Dispose();
        if (GodotObject.IsInstanceValid(state.Root) && !state.Root.IsQueuedForDeletion())
            state.Root.QueueFreeSafely();
    }

    private static void OnRootExiting(NCombatUi ui, HudState state)
    {
        if (MountedStates.TryGetValue(ui, out var mounted) && ReferenceEquals(mounted, state))
            MountedStates.Remove(ui);
        MountedPlayerStates.Remove(state.Player);
        state.Dispose();
    }

    internal static void NotifyPrevented(Player? player, SakuraElementSet prevented)
    {
        if (player is not null
            && prevented != SakuraElementSet.None
            && MountedPlayerStates.TryGetValue(player, out var state))
        {
            state.PulseLocks(prevented);
        }
    }

    /// <summary>Presentation-only: the medallion and the dial flash as a trigger beat starts.</summary>
    internal static void NotifyTriggered(Player? player, SakuraElement element)
    {
        if (player is not null && MountedPlayerStates.TryGetValue(player, out var state))
            state.Flash(element);
    }

    /// <summary>
    /// Where a trigger beam starts. False when this player has no live HUD on this
    /// machine — a teammate, or a HUD already torn down — and the caller falls back.
    /// </summary>
    internal static bool TryGetMedallionCenter(Player? player, SakuraElement element, out Vector2 globalPosition)
    {
        globalPosition = default;
        return player is not null
            && MountedPlayerStates.TryGetValue(player, out var state)
            && state.TryGetMedallionCenter(element, out globalPosition);
    }

    private sealed class HudState : IDisposable
    {
        private readonly Player _player;
        private readonly Creature _creature;
        private readonly CombatState _combatState;
        private readonly IReadOnlyList<ElementSlot> _slots;
        private readonly TextureRect _dial;
        private readonly ShaderMaterial _sheenMaterial;
        private readonly HashSet<Creature> _observedEnemies = [];
        private SakuraElementSet _lockedStates;
        private SakuraElement? _hoveredFacet;
        private Tween? _dialTween;
        private bool _disposed;

        public HudState(Player player, CombatState combatState, Control root)
        {
            _player = player;
            _creature = player.Creature;
            _combatState = combatState;
            Root = root;
            _dial = root.GetNode<TextureRect>("%Dial");
            var board = root.GetNode<TextureRect>("%Board");
            _sheenMaterial = board.Material?.Duplicate() as ShaderMaterial
                ?? throw new InvalidOperationException("Kinomoto Sakura element HUD board requires a ShaderMaterial.");
            board.Material = _sheenMaterial;
            _slots =
            [
                new(root.GetNode<Control>("%WaterMedallion"), SakuraElement.Water),
                new(root.GetNode<Control>("%FireMedallion"), SakuraElement.Fire),
                new(root.GetNode<Control>("%WindMedallion"), SakuraElement.Wind),
                new(root.GetNode<Control>("%EarthMedallion"), SakuraElement.Earth),
            ];
            Root.GuiInput += OnGuiInput;
            Root.MouseExited += OnMouseExited;
            _creature.PowerApplied += OnPowerApplied;
            _creature.PowerIncreased += OnPowerIncreased;
            _creature.PowerDecreased += OnPowerDecreased;
            _creature.PowerRemoved += OnPowerRemoved;
            _combatState.CreaturesChanged += OnCreaturesChanged;
            RebindEnemies();
        }

        public Control Root { get; }
        public Player Player => _player;

        public void Refresh(bool animate)
        {
            if (_disposed)
                return;

            var active = SakuraElementState.ReadActive(_player);
            var locks = SakuraElementState.ReadLocks(_combatState);
            var vfx = SakuraModConfig.IsCardVfxEnabled();
            _sheenMaterial.SetShaderParameter("sheen_strength", vfx ? 1f : 0f);
            foreach (var slot in _slots)
            {
                slot.Apply(
                    active.HasElement(slot.Element),
                    SakuraElementState.IsPermanent(_player, slot.Element),
                    SakuraElementState.ReadTurns(_player, slot.Element),
                    locks.HasElement(slot.Element),
                    animate && vfx,
                    vfx);
            }
            _lockedStates = locks;
        }

        public void PulseLocks(SakuraElementSet locks)
        {
            if (_disposed)
                return;
            foreach (var slot in _slots.Where(slot => locks.HasElement(slot.Element)))
                slot.PulseLock();
        }

        public void Flash(SakuraElement element)
        {
            if (_disposed || !_dial.IsInsideTree())
                return;

            _slots.First(slot => slot.Element == element).Flash();
            KillTween(ref _dialTween);
            _dial.SelfModulate = DialFlashColor;
            _dialTween = _dial.CreateTween();
            _dialTween.TweenProperty(_dial, "self_modulate", Colors.White, FlashDuration * 1.4f)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Sine);
        }

        public bool TryGetMedallionCenter(SakuraElement element, out Vector2 globalPosition)
        {
            globalPosition = default;
            if (_disposed || !GodotObject.IsInstanceValid(Root) || !Root.IsInsideTree())
                return false;

            globalPosition = _slots.First(slot => slot.Element == element).GlobalCenter;
            return true;
        }

        private void OnPowerApplied(PowerModel power) => RefreshForPlayerPower(power);
        private void OnPowerIncreased(PowerModel power, int _, bool __) => RefreshForPlayerPower(power);
        private void OnPowerDecreased(PowerModel power, bool _) => RefreshForPlayerPower(power);
        private void OnPowerRemoved(PowerModel power) => RefreshForPlayerPower(power);

        private void RefreshForPlayerPower(PowerModel power)
        {
            if (_disposed
                || !GodotObject.IsInstanceValid(Root)
                || !Root.IsInsideTree()
                || !ReferenceEquals(_creature.CombatState, _combatState)
                || !(SakuraElementState.IsTriggerPower(power) || power is SakuraPermanentElementPower))
            {
                return;
            }
            Refresh(animate: true);
        }

        private void OnCreaturesChanged(ICombatState _) => RebindEnemies();

        private void RebindEnemies()
        {
            foreach (var enemy in _observedEnemies.ToArray())
                UnsubscribeEnemy(enemy);
            _observedEnemies.Clear();

            foreach (var enemy in _combatState.Enemies)
            {
                _observedEnemies.Add(enemy);
                enemy.PowerApplied += OnEnemyPowerApplied;
                enemy.PowerIncreased += OnEnemyPowerIncreased;
                enemy.PowerDecreased += OnEnemyPowerDecreased;
                enemy.PowerRemoved += OnEnemyPowerRemoved;
                enemy.Died += OnEnemyDied;
            }
            Refresh(animate: false);
        }

        private void OnEnemyPowerApplied(PowerModel power) => RefreshForSovereignty(power);
        private void OnEnemyPowerIncreased(PowerModel power, int _, bool __) => RefreshForSovereignty(power);
        private void OnEnemyPowerDecreased(PowerModel power, bool _) => RefreshForSovereignty(power);
        private void OnEnemyPowerRemoved(PowerModel power) => RefreshForSovereignty(power);
        private void OnEnemyDied(Creature _) => Refresh(animate: false);

        private void RefreshForSovereignty(PowerModel power)
        {
            if (power is WindSovereigntyPower or DarkSovereigntyPower or WaterSovereigntyPower or FireSovereigntyPower or LightSovereigntyPower or EarthSovereigntyPower)
                Refresh(animate: false);
        }

        private void UnsubscribeEnemy(Creature enemy)
        {
            enemy.PowerApplied -= OnEnemyPowerApplied;
            enemy.PowerIncreased -= OnEnemyPowerIncreased;
            enemy.PowerDecreased -= OnEnemyPowerDecreased;
            enemy.PowerRemoved -= OnEnemyPowerRemoved;
            enemy.Died -= OnEnemyDied;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _creature.PowerApplied -= OnPowerApplied;
            _creature.PowerIncreased -= OnPowerIncreased;
            _creature.PowerDecreased -= OnPowerDecreased;
            _creature.PowerRemoved -= OnPowerRemoved;
            _combatState.CreaturesChanged -= OnCreaturesChanged;
            foreach (var enemy in _observedEnemies)
                UnsubscribeEnemy(enemy);
            _observedEnemies.Clear();
            Root.GuiInput -= OnGuiInput;
            Root.MouseExited -= OnMouseExited;
            HideHoverTip();
            KillTween(ref _dialTween);
            foreach (var slot in _slots)
                slot.Dispose();
        }

        private void OnGuiInput(InputEvent inputEvent)
        {
            if (inputEvent is not InputEventMouseMotion motion)
                return;

            var center = Root.Size * 0.5f;
            var radius = MathF.Min(Root.Size.X, Root.Size.Y) * SakuraElementFacetProjection.DiscRadiusRatio;
            var facet = SakuraElementFacetProjection.FromOffset(
                motion.Position.X - center.X,
                motion.Position.Y - center.Y,
                radius);
            if (_hoveredFacet == facet)
                return;

            _hoveredFacet = facet;
            if (facet is { } hoveredFacet)
                ShowHoverTip(hoveredFacet);
            else
                HideHoverTip();
        }

        private void OnMouseExited()
        {
            _hoveredFacet = null;
            HideHoverTip();
        }

        private void ShowHoverTip(SakuraElement facet)
        {
            HideHoverTip();
            var key = SakuraSourceCardText.ElementStateTipKey(facet);
            var tips = new List<IHoverTip>
            {
                new HoverTip(
                    new LocString("static_hover_tips", $"{key}.title"),
                    new LocString("static_hover_tips", $"{key}.description"))
            };
            if (_lockedStates.HasElement(facet))
            {
                if (facet == SakuraElement.Wind
                    && _combatState.Enemies.Any(static enemy => enemy.IsAlive && enemy.HasPower<WindSovereigntyPower>()))
                {
                    tips.Add(HoverTipFactory.FromPower<WindSovereigntyPower>());
                }
                if (_combatState.Enemies.Any(static enemy => enemy.IsAlive && enemy.HasPower<DarkSovereigntyPower>()))
                    tips.Add(HoverTipFactory.FromPower<DarkSovereigntyPower>());
                if (facet == SakuraElement.Water
                    && _combatState.Enemies.Any(static enemy => enemy.IsAlive && enemy.HasPower<WaterSovereigntyPower>()))
                {
                    tips.Add(HoverTipFactory.FromPower<WaterSovereigntyPower>());
                }
                if (facet == SakuraElement.Fire
                    && _combatState.Enemies.Any(static enemy => enemy.IsAlive && enemy.HasPower<FireSovereigntyPower>()))
                    tips.Add(HoverTipFactory.FromPower<FireSovereigntyPower>());
                if (facet is SakuraElement.Fire or SakuraElement.Earth
                    && _combatState.Enemies.Any(static enemy => enemy.IsAlive && enemy.HasPower<LightSovereigntyPower>()))
                    tips.Add(HoverTipFactory.FromPower<LightSovereigntyPower>());
                if (facet == SakuraElement.Earth
                    && _combatState.Enemies.Any(static enemy => enemy.IsAlive && enemy.HasPower<EarthSovereigntyPower>()))
                    tips.Add(HoverTipFactory.FromPower<EarthSovereigntyPower>());
            }
            NHoverTipSet.CreateAndShow(Root, tips, HoverTipAlignment.Right);
        }

        private void HideHoverTip() => NHoverTipSet.Remove(Root);
    }

    /// <summary>
    /// One medallion. Plate is always shown; Lit, Glow, the turn count and the
    /// permanent ring follow the state; Crack follows the sovereignty lock.
    /// </summary>
    private sealed class ElementSlot : IDisposable
    {
        private readonly Control _medallion;
        private readonly TextureRect _plate;
        private readonly TextureRect _glow;
        private readonly TextureRect _lit;
        private readonly TextureRect _permanentRing;
        private readonly TextureRect _crack;
        private readonly Label _turns;
        private readonly ShaderMaterial _litMaterial;
        private readonly ShaderMaterial _glowMaterial;
        private Tween? _lightTween;
        private Tween? _crackPulseTween;
        private bool _isActive;
        private bool _isPermanent;
        private int _turnCount;

        public ElementSlot(Control medallion, SakuraElement element)
        {
            _medallion = medallion;
            Element = element;
            _plate = medallion.GetNode<TextureRect>("Plate");
            _glow = medallion.GetNode<TextureRect>("Glow");
            _lit = medallion.GetNode<TextureRect>("Lit");
            _permanentRing = medallion.GetNode<TextureRect>("PermanentRing");
            _crack = medallion.GetNode<TextureRect>("Crack");
            _turns = medallion.GetNode<Label>("Turns");
            _litMaterial = _lit.Material?.Duplicate() as ShaderMaterial
                ?? throw new InvalidOperationException($"Kinomoto Sakura {element} medallion requires a ShaderMaterial.");
            _lit.Material = _litMaterial;
            _glowMaterial = _glow.Material?.Duplicate() as ShaderMaterial
                ?? throw new InvalidOperationException($"Kinomoto Sakura {element} glow requires a ShaderMaterial.");
            _glow.Material = _glowMaterial;
        }

        public SakuraElement Element { get; }

        public Vector2 GlobalCenter => _medallion.GetGlobalRect().GetCenter();

        public void Apply(bool isActive, bool isPermanent, int turns, bool isLocked, bool animate, bool vfx)
        {
            var lightUp = animate && isActive && !_isActive;
            var renewed = animate && isActive && _isActive && (turns > _turnCount || (isPermanent && !_isPermanent));
            _isActive = isActive;
            _isPermanent = isPermanent;
            _turnCount = turns;

            _lit.Visible = isActive;
            _glow.Visible = isActive;
            _permanentRing.Visible = isActive && isPermanent;
            _turns.Visible = isActive && !isPermanent;
            _turns.Text = turns.ToString();
            _crack.Visible = isLocked;
            _plate.Modulate = isLocked && !isActive ? LockedPlateTint : Colors.White;
            _litMaterial.SetShaderParameter("flow_strength", vfx ? 1f : 0f);
            _glowMaterial.SetShaderParameter("breath", vfx ? 1f : 0f);

            if (lightUp)
                LightUp();
            else if (renewed)
                Flash();
            else if (!isActive)
                ResetLight();
        }

        public void Flash()
        {
            if (!_isActive || !_medallion.IsInsideTree())
                return;

            KillTween(ref _lightTween);
            _litMaterial.SetShaderParameter("light_progress", 1f);
            _litMaterial.SetShaderParameter("flash", 1f);
            _medallion.Scale = Vector2.One * 1.14f;
            _lightTween = _medallion.CreateTween().SetParallel();
            _lightTween.TweenMethod(Callable.From<float>(value => _litMaterial.SetShaderParameter("flash", value)),
                1f, 0f, FlashDuration);
            _lightTween.TweenProperty(_medallion, "scale", Vector2.One, FlashDuration)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
        }

        public void PulseLock()
        {
            if (!_crack.Visible || !_crack.IsInsideTree()
                || _crackPulseTween is { } active && active.IsValid() && active.IsRunning())
            {
                return;
            }

            _crackPulseTween = _crack.CreateTween();
            _crackPulseTween.TweenProperty(_crack, "scale", Vector2.One * 0.9f, 0.08f)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
            _crackPulseTween.Parallel().TweenProperty(_crack, "self_modulate", CrackPulseColor, 0.08f);
            _crackPulseTween.TweenProperty(_crack, "scale", Vector2.One, 0.12f)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
            _crackPulseTween.Parallel().TweenProperty(_crack, "self_modulate", Colors.White, 0.12f);
        }

        public void Dispose()
        {
            ResetLight();
            KillTween(ref _crackPulseTween);
            _crack.Scale = Vector2.One;
            _crack.SelfModulate = Colors.White;
        }

        /// <summary>The light spreads out from the centre, then the medallion settles.</summary>
        private void LightUp()
        {
            if (!_medallion.IsInsideTree())
                return;

            KillTween(ref _lightTween);
            _litMaterial.SetShaderParameter("light_progress", 0f);
            _litMaterial.SetShaderParameter("flash", 0f);
            _lightTween = _medallion.CreateTween().SetParallel();
            _lightTween.TweenMethod(Callable.From<float>(value => _litMaterial.SetShaderParameter("light_progress", value)),
                    0f, 1f, LightUpDuration)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Sine);
            _lightTween.TweenProperty(_medallion, "scale", Vector2.One * 1.12f, LightUpDuration * 0.4f)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
            _lightTween.TweenProperty(_medallion, "scale", Vector2.One, LightUpDuration * 0.6f)
                .SetDelay(LightUpDuration * 0.4f)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
        }

        private void ResetLight()
        {
            KillTween(ref _lightTween);
            _litMaterial.SetShaderParameter("light_progress", 1f);
            _litMaterial.SetShaderParameter("flash", 0f);
            _medallion.Scale = Vector2.One;
        }
    }

    private static void KillTween(ref Tween? tween)
    {
        if (tween is { } current && current.IsValid())
            current.Kill();
        tween = null;
    }
}

internal static class SakuraCombatResourceHudPatchRegistration
{
    public static void Register()
    {
        var patcher = RitsuLibFramework.CreatePatcher(MainFile.ModId, "combat-resource-hud", "combat resource HUD");
        patcher.RegisterPatch<SakuraCombatResourceHudActivatePatch>();
        patcher.RegisterPatch<SakuraCombatResourceHudAnimOutPatch>();
        patcher.RegisterPatch<SakuraCombatResourceHudDeactivatePatch>();

        if (!RitsuLibFramework.ApplyRequiredPatcher(
                patcher,
                static () => MainFile.Logger.Error("Required Sakura combat resource HUD patches failed."),
                "Required Sakura combat resource HUD patches failed. SakuraMod initialization will stop."))
        {
            throw new InvalidOperationException("Required Sakura combat resource HUD patches failed.");
        }
    }
}

internal sealed class SakuraCombatResourceHudActivatePatch : IPatchMethod
{
    public static string PatchId => "sakura_combat_resource_hud_activate";
    public static string Description => "Mount Sakura's combat resource HUDs after combat UI activation";
    public static bool IsCritical => true;

    public static ModPatchTarget[] GetTargets() =>
    [
        PatchTarget.Method<NCombatUi>(nameof(NCombatUi.Activate), typeof(CombatState))
    ];

    public static void Postfix(NCombatUi __instance, CombatState state) =>
        SakuraCombatResourceHud.Mount(__instance, state);
}

internal sealed class SakuraCombatResourceHudAnimOutPatch : IPatchMethod
{
    public static string PatchId => "sakura_combat_resource_hud_anim_out";
    public static string Description => "Unmount Sakura's combat resource HUDs before combat UI animation out";
    public static bool IsCritical => true;

    public static ModPatchTarget[] GetTargets() =>
    [
        PatchTarget.Method<NCombatUi>(nameof(NCombatUi.AnimOut), Type.EmptyTypes)
    ];

    public static void Prefix(NCombatUi __instance) =>
        SakuraCombatResourceHud.Unmount(__instance);
}

internal sealed class SakuraCombatResourceHudDeactivatePatch : IPatchMethod
{
    public static string PatchId => "sakura_combat_resource_hud_deactivate";
    public static string Description => "Unmount Sakura's combat resource HUDs before combat UI deactivation";
    public static bool IsCritical => true;

    public static ModPatchTarget[] GetTargets() =>
    [
        PatchTarget.Method<NCombatUi>(nameof(NCombatUi.Deactivate), Type.EmptyTypes)
    ];

    public static void Prefix(NCombatUi __instance) =>
        SakuraCombatResourceHud.Unmount(__instance);
}
