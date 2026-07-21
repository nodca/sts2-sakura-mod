using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

namespace SakuraMod.SakuraModCode.Character;

[HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.SelectCharacter))]
internal static class SakuraCombatArtSelectorPatch
{
    internal const string RootName = "SakuraCombatArtSelector";
    internal const string LabelKey = "SAKURAMOD-COMBAT_ART.label";
    internal const string StandardKey = "SAKURAMOD-COMBAT_ART.standard";
    internal const string ChibiKey = "SAKURAMOD-COMBAT_ART.chibi";

    private static readonly ConditionalWeakTable<NCharacterSelectScreen, SelectorState> States = new();

    [HarmonyPostfix]
    private static void SelectCharacterPostfix(
        NCharacterSelectScreen __instance,
        NCharacterSelectButton charSelectButton,
        CharacterModel characterModel)
    {
        try
        {
            var state = States.GetValue(__instance, static screen => new SelectorState(screen));
            var shouldShow = ShouldShow(
                SakuraStarterCompatibility.IsKinomotoSakuraCharacter(characterModel),
                charSelectButton.IsRandom,
                charSelectButton.IsLocked);
            state.SetVisibleFor(shouldShow ? charSelectButton : null);
        }
        catch (Exception exception)
        {
            MainFile.Logger.Error($"Failed to update Sakura combat-art selector: {exception}");
        }
    }

    internal static bool ShouldShow(bool isSakura, bool isRandom, bool isLocked) =>
        SakuraCombatArtFeature.IsEnabled && IsEligibleSelection(isSakura, isRandom, isLocked);

    internal static bool IsEligibleSelection(bool isSakura, bool isRandom, bool isLocked) =>
        isSakura && !isRandom && !isLocked;

    private sealed class SelectorState
    {
        private static readonly Color TextColor = new("d9c9c1");
        private static readonly Color SelectedTextColor = new("34262b");
        private static readonly Color BackgroundColor = new("231f20d9");
        private static readonly Color SelectedBackgroundColor = new("f3b7c9e8");
        private static readonly Color BorderColor = new("a98e80");
        private static readonly Color FocusBorderColor = new("f5db88");
        private static readonly Vector2 SelectorOffset = new(70f, -24f);

        private readonly Control _root;
        private readonly NButton _standardButton;
        private readonly NButton _chibiButton;
        private readonly Label _standardLabel;
        private readonly Label _chibiLabel;
        private readonly Panel _standardPanel;
        private readonly Panel _chibiPanel;
        private readonly Control _confirmButton;

        private NCharacterSelectButton? _characterButton;
        private NodePath _originalCharacterBottom = new();
        private NodePath _originalConfirmTop = new();
        private bool _standardFocused;
        private bool _chibiFocused;

        internal SelectorState(NCharacterSelectScreen screen)
        {
            var container = screen.GetNode<VBoxContainer>("InfoPanel/VBoxContainer");
            var hpGoldSpacer = container.GetNode<Control>("HpGoldSpacer");
            _confirmButton = screen.GetNode<Control>("ConfirmButton");

            _root = new Control
            {
                Name = RootName,
                CustomMinimumSize = new Vector2(0f, 46f),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                Visible = false,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            var content = new HBoxContainer
            {
                Name = "Content",
                Alignment = BoxContainer.AlignmentMode.Center,
                MouseFilter = Control.MouseFilterEnum.Pass
            };
            content.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            content.OffsetLeft = SelectorOffset.X;
            content.OffsetTop = SelectorOffset.Y;
            content.OffsetRight = SelectorOffset.X;
            content.OffsetBottom = SelectorOffset.Y;
            content.AddThemeConstantOverride("separation", 12);
            _root.AddChild(content);

            var title = new Label
            {
                Text = Localized(LabelKey),
                CustomMinimumSize = new Vector2(108f, 42f),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            title.AddThemeColorOverride("font_color", TextColor);
            title.AddThemeFontSizeOverride("font_size", 20);
            content.AddChild(title);

            (_standardButton, _standardPanel, _standardLabel) = CreateButton(Localized(StandardKey));
            (_chibiButton, _chibiPanel, _chibiLabel) = CreateButton(Localized(ChibiKey));
            _standardButton.Name = "Standard";
            _chibiButton.Name = "Chibi";
            content.AddChild(_standardButton);
            content.AddChild(_chibiButton);

            _standardButton.Released += _ => Select(useChibi: false);
            _chibiButton.Released += _ => Select(useChibi: true);
            _standardButton.Focused += _ => SetFocused(standard: true, focused: true);
            _standardButton.Unfocused += _ => SetFocused(standard: true, focused: false);
            _chibiButton.Focused += _ => SetFocused(standard: false, focused: true);
            _chibiButton.Unfocused += _ => SetFocused(standard: false, focused: false);

            container.AddChild(_root);
            container.MoveChild(_root, hpGoldSpacer.GetIndex());
        }

        internal void SetVisibleFor(NCharacterSelectButton? characterButton)
        {
            RestoreFocusNeighbors();
            _characterButton = characterButton;
            _root.Visible = characterButton is not null;
            if (characterButton is null)
                return;

            _originalCharacterBottom = characterButton.FocusNeighborBottom;
            _originalConfirmTop = _confirmButton.FocusNeighborTop;
            Refresh();
        }

        private void Select(bool useChibi)
        {
            SakuraModConfig.UseChibiCombatArtBinding.Write(useChibi);
            Refresh();
        }

        private void Refresh()
        {
            if (_characterButton is null)
                return;

            var useChibi = SakuraModConfig.IsChibiCombatArtEnabled();
            ApplyButtonStyle(_standardPanel, _standardLabel, selected: !useChibi, _standardFocused);
            ApplyButtonStyle(_chibiPanel, _chibiLabel, selected: useChibi, _chibiFocused);

            var selectedButton = useChibi ? _chibiButton : _standardButton;
            var characterPath = _characterButton.GetPath();
            var confirmPath = _confirmButton.GetPath();
            var standardPath = _standardButton.GetPath();
            var chibiPath = _chibiButton.GetPath();

            _characterButton.FocusNeighborBottom = selectedButton.GetPath();
            _confirmButton.FocusNeighborTop = selectedButton.GetPath();
            _standardButton.FocusNeighborTop = characterPath;
            _standardButton.FocusNeighborBottom = confirmPath;
            _standardButton.FocusNeighborLeft = chibiPath;
            _standardButton.FocusNeighborRight = chibiPath;
            _chibiButton.FocusNeighborTop = characterPath;
            _chibiButton.FocusNeighborBottom = confirmPath;
            _chibiButton.FocusNeighborLeft = standardPath;
            _chibiButton.FocusNeighborRight = standardPath;
        }

        private void RestoreFocusNeighbors()
        {
            if (_characterButton is null)
                return;

            _characterButton.FocusNeighborBottom = _originalCharacterBottom;
            _confirmButton.FocusNeighborTop = _originalConfirmTop;
            _characterButton = null;
        }

        private void SetFocused(bool standard, bool focused)
        {
            if (standard)
                _standardFocused = focused;
            else
                _chibiFocused = focused;
            Refresh();
        }

        private static (NButton Button, Panel Panel, Label Label) CreateButton(string text)
        {
            var button = new NButton
            {
                CustomMinimumSize = new Vector2(112f, 42f),
                FocusMode = Control.FocusModeEnum.All,
                MouseDefaultCursorShape = Control.CursorShape.PointingHand
            };
            var panel = new Panel
            {
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            button.AddChild(panel);

            var label = new Label
            {
                Text = text,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            label.AddThemeFontSizeOverride("font_size", 19);
            button.AddChild(label);
            return (button, panel, label);
        }

        private static void ApplyButtonStyle(
            Panel panel,
            Label label,
            bool selected,
            bool focused)
        {
            var style = new StyleBoxFlat
            {
                BgColor = selected ? SelectedBackgroundColor : BackgroundColor,
                BorderColor = focused ? FocusBorderColor : BorderColor,
                BorderWidthLeft = focused ? 3 : selected ? 2 : 1,
                BorderWidthTop = focused ? 3 : selected ? 2 : 1,
                BorderWidthRight = focused ? 3 : selected ? 2 : 1,
                BorderWidthBottom = focused ? 3 : selected ? 2 : 1,
                CornerRadiusTopLeft = 4,
                CornerRadiusTopRight = 4,
                CornerRadiusBottomLeft = 4,
                CornerRadiusBottomRight = 4
            };
            panel.AddThemeStyleboxOverride("panel", style);
            label.AddThemeColorOverride("font_color", selected ? SelectedTextColor : TextColor);
        }

        private static string Localized(string key) =>
            new LocString("settings_ui", key).GetFormattedText();
    }
}
