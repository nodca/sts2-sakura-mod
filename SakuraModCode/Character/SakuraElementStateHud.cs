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
using STS2RitsuLib;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.Patching.Models;
using System.Runtime.CompilerServices;

namespace SakuraMod.SakuraModCode.Character;

[Flags]
internal enum SakuraActiveElementStates
{
    None = 0,
    Earthy = 1,
    Firey = 2,
    Watery = 4,
    Windy = 8,
}

internal static class SakuraElementStateProjection
{
    public static SakuraActiveElementStates FromActivity(
        bool earthy,
        bool firey,
        bool watery,
        bool windy)
    {
        var states = SakuraActiveElementStates.None;
        if (earthy)
            states |= SakuraActiveElementStates.Earthy;
        if (firey)
            states |= SakuraActiveElementStates.Firey;
        if (watery)
            states |= SakuraActiveElementStates.Watery;
        if (windy)
            states |= SakuraActiveElementStates.Windy;
        return states;
    }

    public static SakuraActiveElementStates Read(Player player) =>
        FromActivity(
            player.Creature.GetPower<ClassicEarthyPower>()?.Amount > 0,
            player.Creature.GetPower<ClassicFireyPower>()?.Amount > 0,
            player.Creature.GetPower<ClassicWateryPower>()?.Amount > 0,
            player.Creature.GetPower<ClassicWindyPower>()?.Amount > 0);

    public static SakuraActiveElementStates NewlyActive(
        SakuraActiveElementStates previous,
        SakuraActiveElementStates current) =>
        current & ~previous;
}

internal enum SakuraElementFacet
{
    Wind,
    Fire,
    Earth,
    Water,
}

internal static class SakuraElementFacetProjection
{
    internal const float DiscRadiusRatio = 232f / 512f;

    public static SakuraElementFacet? FromOffset(float horizontal, float vertical, float radius)
    {
        if ((horizontal * horizontal) + (vertical * vertical) > radius * radius)
            return null;

        if (MathF.Abs(horizontal) > MathF.Abs(vertical))
            return horizontal >= 0f ? SakuraElementFacet.Wind : SakuraElementFacet.Water;

        return vertical >= 0f ? SakuraElementFacet.Earth : SakuraElementFacet.Fire;
    }
}

internal static class SakuraElementStateHud
{
    internal const string ScenePath = MainFile.ResPath + "/scenes/combat/sakura_element_state_hud.tscn";
    private const float MountHorizontalOffset = 8f;
    private const float MountGap = 12f;
    private static readonly Color ActiveFacetColor = Colors.White;
    private static readonly Color InactiveFacetColor = new(0.62f, 0.64f, 0.66f, 0.86f);
    private static readonly Color PulseColor = new(1.45f, 1.3f, 0.82f, 1f);
    private static readonly ConditionalWeakTable<NCombatUi, HudState> MountedStates = new();

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
        root.Position = new Vector2(
            ((energyCounter.Size.X - root.Size.X) * 0.5f) + MountHorizontalOffset,
            -root.Size.Y - MountGap);
        energyCounter.AddChildSafely(root);

        var state = new HudState(player, combatState, root);
        MountedStates.Add(ui, state);
        root.TreeExiting += () => OnRootExiting(ui, state);
        state.Refresh(animateNewlyActive: false);
    }

    public static void Unmount(NCombatUi ui)
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
        private readonly IReadOnlyList<ElementSlot> _slots;
        private SakuraActiveElementStates _activeStates;
        private SakuraElementFacet? _hoveredFacet;
        private bool _disposed;

        public HudState(Player player, CombatState combatState, Control root)
        {
            _player = player;
            _creature = player.Creature;
            _combatState = combatState;
            Root = root;
            _slots =
            [
                new(root.GetNode<TextureRect>("%WindFacet"), SakuraElementFacet.Wind, SakuraActiveElementStates.Windy, SakuraElement.Wind),
                new(root.GetNode<TextureRect>("%FireFacet"), SakuraElementFacet.Fire, SakuraActiveElementStates.Firey, SakuraElement.Fire),
                new(root.GetNode<TextureRect>("%EarthFacet"), SakuraElementFacet.Earth, SakuraActiveElementStates.Earthy, SakuraElement.Earth),
                new(root.GetNode<TextureRect>("%WaterFacet"), SakuraElementFacet.Water, SakuraActiveElementStates.Watery, SakuraElement.Water),
            ];
            Root.GuiInput += OnGuiInput;
            Root.MouseExited += OnMouseExited;
            _creature.PowerApplied += OnPowerApplied;
            _creature.PowerIncreased += OnPowerIncreased;
            _creature.PowerDecreased += OnPowerDecreased;
            _creature.PowerRemoved += OnPowerRemoved;
        }

        public Control Root { get; }

        public void Refresh(bool animateNewlyActive)
        {
            if (_disposed)
                return;

            var next = SakuraElementStateProjection.Read(_player);
            if (next == _activeStates && animateNewlyActive)
                return;

            var newlyActive = animateNewlyActive
                ? SakuraElementStateProjection.NewlyActive(_activeStates, next)
                : SakuraActiveElementStates.None;
            foreach (var slot in _slots)
                slot.Apply(next.HasFlag(slot.State), newlyActive.HasFlag(slot.State));
            _activeStates = next;
        }

        private void OnPowerApplied(PowerModel power) =>
            RefreshForPower(power);

        private void OnPowerIncreased(PowerModel power, int _, bool __) =>
            RefreshForPower(power);

        private void OnPowerDecreased(PowerModel power, bool _) =>
            RefreshForPower(power);

        private void OnPowerRemoved(PowerModel power) =>
            RefreshForPower(power);

        private void RefreshForPower(PowerModel power)
        {
            if (_disposed
                || !GodotObject.IsInstanceValid(Root)
                || !Root.IsInsideTree()
                || !ReferenceEquals(_creature.CombatState, _combatState)
                || !IsElementStatePower(power))
            {
                return;
            }

            Refresh(animateNewlyActive: true);
        }

        private static bool IsElementStatePower(PowerModel power) =>
            power is ClassicEarthyPower
                or ClassicFireyPower
                or ClassicWateryPower
                or ClassicWindyPower;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _creature.PowerApplied -= OnPowerApplied;
            _creature.PowerIncreased -= OnPowerIncreased;
            _creature.PowerDecreased -= OnPowerDecreased;
            _creature.PowerRemoved -= OnPowerRemoved;
            Root.GuiInput -= OnGuiInput;
            Root.MouseExited -= OnMouseExited;
            HideHoverTip();
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

        private void ShowHoverTip(SakuraElementFacet facet)
        {
            HideHoverTip();
            var element = _slots.First(slot => slot.Facet == facet).Element;
            var key = SakuraSourceCardText.ElementStateTipKey(element);
            var tip = new HoverTip(
                new LocString("static_hover_tips", $"{key}.title"),
                new LocString("static_hover_tips", $"{key}.description"));
            NHoverTipSet.CreateAndShow(Root, tip, HoverTipAlignment.Right);
        }

        private void HideHoverTip() =>
            NHoverTipSet.Remove(Root);
    }

    private sealed class ElementSlot : IDisposable
    {
        private readonly TextureRect _facet;
        private Tween? _pulseTween;

        public ElementSlot(
            TextureRect facet,
            SakuraElementFacet facetId,
            SakuraActiveElementStates state,
            SakuraElement element)
        {
            _facet = facet;
            Facet = facetId;
            State = state;
            Element = element;
        }

        public SakuraElementFacet Facet { get; }
        public SakuraActiveElementStates State { get; }
        public SakuraElement Element { get; }

        public void Apply(bool isActive, bool pulse)
        {
            _facet.Modulate = isActive ? ActiveFacetColor : InactiveFacetColor;
            if (pulse)
                Pulse();
            else
                ResetPulse();
        }

        public void Dispose() =>
            ResetPulse();

        private void Pulse()
        {
            ResetPulse();
            if (!_facet.IsInsideTree())
                return;

            _facet.SelfModulate = PulseColor;
            _pulseTween = _facet.CreateTween();
            _pulseTween.TweenProperty(_facet, "self_modulate", Colors.White, 0.22f)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Sine);
        }

        private void ResetPulse()
        {
            if (_pulseTween is { } tween && tween.IsValid())
                tween.Kill();
            _pulseTween = null;
            _facet.SelfModulate = Colors.White;
        }
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
