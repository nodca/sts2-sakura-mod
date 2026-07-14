using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using SakuraMod.SakuraModCode.Character;
using STS2RitsuLib.CardPiles;
using STS2RitsuLib.CardPiles.Nodes;

namespace SakuraMod.SakuraModCode.Cards;

internal static class SakuraMemoryPileButtonTransition
{
    internal const float SlideDistance = 150f;
    internal const double DurationSeconds = 0.5;

    internal static readonly Vector2 SlideOffset = new(SlideDistance, 0f);

    private static readonly ConditionalWeakTable<NModCardPileButton, TransitionState> States = new();
    private static readonly MethodInfo RelayoutMethod =
        AccessTools.DeclaredMethod(typeof(NModCardPileButton), "TryRelayoutCombatRow")
        ?? throw new MissingMethodException(typeof(NModCardPileButton).FullName, "TryRelayoutCombatRow");

    internal static Vector2 HiddenPosition(Vector2 shownPosition) =>
        shownPosition + SlideOffset;

    internal static void Apply(NModCardPileButton button, bool shouldShow)
    {
        var state = States.GetOrCreateValue(button);
        state.Apply(button, shouldShow);
    }

    internal static void HideUntilBound(NModCardPileButton button)
    {
        if (States.TryGetValue(button, out var state))
        {
            state.Apply(button, shouldShow: false);
            return;
        }

        button.Visible = false;
        button.MouseFilter = Control.MouseFilterEnum.Ignore;
        NHoverTipSet.Remove(button);
    }

    private static void Relayout(NModCardPileButton button)
    {
        if (button.IsInsideTree())
            RelayoutMethod.Invoke(button, null);
    }

    private sealed class TransitionState
    {
        private bool _initialized;
        private bool _shouldShow;
        private Vector2 _shownPosition;
        private Tween? _positionTween;

        internal void Apply(NModCardPileButton button, bool shouldShow)
        {
            if (!_initialized || !button.IsInsideTree())
            {
                ApplyInstant(button, shouldShow);
                return;
            }

            if (_shouldShow == shouldShow)
            {
                if (shouldShow && _positionTween is null)
                    _shownPosition = button.Position;
                return;
            }

            _shouldShow = shouldShow;
            var wasAnimating = _positionTween is not null;
            KillTween();
            if (shouldShow)
                AnimateIn(button);
            else
                AnimateOut(button, wasAnimating);
        }

        private void ApplyInstant(NModCardPileButton button, bool shouldShow)
        {
            KillTween();
            _initialized = true;
            _shouldShow = shouldShow;
            _shownPosition = button.Position;
            button.Visible = shouldShow;
            button.MouseFilter = shouldShow
                ? Control.MouseFilterEnum.Stop
                : Control.MouseFilterEnum.Ignore;
            if (!shouldShow)
                NHoverTipSet.Remove(button);
            Relayout(button);
            if (shouldShow)
                _shownPosition = button.Position;
        }

        private void AnimateIn(NModCardPileButton button)
        {
            var wasVisible = button.Visible;
            var startPosition = button.Position;
            button.Visible = true;
            button.MouseFilter = Control.MouseFilterEnum.Stop;
            if (!wasVisible)
            {
                Relayout(button);
                _shownPosition = button.Position;
                startPosition = HiddenPosition(_shownPosition);
            }
            button.Position = startPosition;

            var tween = button.CreateTween();
            _positionTween = tween;
            tween.TweenProperty(button, "position", _shownPosition, DurationSeconds)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Expo);
            tween.TweenCallback(Callable.From(() => FinishIn(button, tween)));
        }

        private void AnimateOut(NModCardPileButton button, bool wasAnimating)
        {
            if (!wasAnimating)
                _shownPosition = button.Position;

            button.MouseFilter = Control.MouseFilterEnum.Ignore;
            NHoverTipSet.Remove(button);

            var tween = button.CreateTween();
            _positionTween = tween;
            tween.TweenProperty(button, "position", HiddenPosition(_shownPosition), DurationSeconds)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Back);
            tween.TweenCallback(Callable.From(() => FinishOut(button, tween)));
        }

        private void FinishIn(NModCardPileButton button, Tween tween)
        {
            if (!ReferenceEquals(_positionTween, tween))
                return;

            _positionTween = null;
            if (!GodotObject.IsInstanceValid(button) || !_shouldShow)
                return;

            button.Position = _shownPosition;
        }

        private void FinishOut(NModCardPileButton button, Tween tween)
        {
            if (!ReferenceEquals(_positionTween, tween))
                return;

            _positionTween = null;
            if (!GodotObject.IsInstanceValid(button) || _shouldShow)
                return;

            button.Visible = false;
            button.Position = _shownPosition;
            Relayout(button);
        }

        private void KillTween()
        {
            if (_positionTween is not null && GodotObject.IsInstanceValid(_positionTween))
                _positionTween.Kill();
            _positionTween = null;
        }
    }
}

[HarmonyPatch(typeof(NModCardPileButton), "RefreshPileButtonVisibility")]
internal static class SakuraMemoryPileButtonVisibilityPatch
{
    [HarmonyPrefix]
    private static bool Prefix(
        NModCardPileButton __instance,
        Player? ____player,
        ModCardPile? ____pile)
    {
        if (__instance.Definition?.Id != SakuraMemoryPile.PileId)
            return true;

        if (____player is null || ____pile is null)
        {
            SakuraMemoryPileButtonTransition.HideUntilBound(__instance);
            return false;
        }

        var shouldShow = SakuraMemoryPile.IsButtonVisible(
            SakuraStarterCompatibility.IsKinomotoSakura(____player),
            CombatManager.Instance.IsInProgress,
            ____pile.Cards.Count);
        SakuraMemoryPileButtonTransition.Apply(__instance, shouldShow);
        return false;
    }
}
