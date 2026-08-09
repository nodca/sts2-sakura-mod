using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

namespace SakuraMod.SakuraModCode.Character;

[HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.SelectCharacter))]
internal static class SakuraCharacterSelectOptionsPatch
{
    internal const string RootName = "SakuraCharacterSelectOptions";
    internal const string CombatArtLabelKey = "SAKURAMOD-COMBAT_ART.label";
    internal const string StandardKey = "SAKURAMOD-COMBAT_ART.standard";
    internal const string ChibiKey = "SAKURAMOD-COMBAT_ART.chibi";
    internal const string CardBgmLabelKey = "SAKURAMOD-CARD_BGM.label";
    internal const string VoiceOnKey = "SAKURAMOD-ENABLE_SAKURA_VOICE.on";
    internal const string VoiceOffKey = "SAKURAMOD-ENABLE_SAKURA_VOICE.off";

    private static readonly ConditionalWeakTable<NCharacterSelectScreen, OptionsState> States = new();

    [HarmonyPostfix]
    private static void SelectCharacterPostfix(
        NCharacterSelectScreen __instance,
        NCharacterSelectButton charSelectButton,
        CharacterModel characterModel)
    {
        try
        {
            var state = States.GetValue(__instance, static screen => new OptionsState(screen));
            var shouldShow = ShouldShow(
                SakuraStarterCompatibility.IsKinomotoSakuraCharacter(characterModel),
                charSelectButton.IsRandom,
                charSelectButton.IsLocked);
            state.SetVisibleFor(shouldShow ? charSelectButton : null);
        }
        catch (Exception exception)
        {
            MainFile.Logger.Error($"Failed to update Sakura character-select options: {exception}");
        }
    }

    internal static bool ShouldShow(bool isSakura, bool isRandom, bool isLocked) =>
        SakuraCombatArtFeature.IsEnabled && IsEligibleSelection(isSakura, isRandom, isLocked);

    internal static bool IsEligibleSelection(bool isSakura, bool isRandom, bool isLocked) =>
        isSakura && !isRandom && !isLocked;

    private sealed class OptionsState
    {
        private const int CornerRadius = 4;
        private const int DefaultBorderWidth = 1;
        private const int SelectedBorderWidth = 2;
        private const int FocusBorderWidth = 3;

        private static readonly Color GroupSurfaceColor = new("231f20d9");
        private static readonly Color GroupBorderColor = new("8f7770");
        private static readonly Color TextColor = new("e4d6cf");
        private static readonly Color SelectedTextColor = new("34262b");
        private static readonly Color SegmentSurfaceColor = new("312a2dcc");
        private static readonly Color SelectedSurfaceColor = new("edaec1f2");
        private static readonly Color FocusBorderColor = new("f5db88");
        private static readonly Vector2 GroupOffset = new(70f, -24f);

        private readonly Control _root;
        private readonly OptionSegment _standard;
        private readonly OptionSegment _chibi;
        private readonly OptionSegment _voiceToggle;
        private readonly OptionSegment _cardBgmToggle;
        private readonly Control _confirmButton;
        private readonly StartRunLobby _lobby;

        private NCharacterSelectButton? _characterButton;
        private NodePath _originalCharacterBottom = new();
        private NodePath _originalConfirmTop = new();

        internal OptionsState(NCharacterSelectScreen screen)
        {
            _lobby = screen.Lobby;
            var container = screen.GetNode<VBoxContainer>("InfoPanel/VBoxContainer");
            var hpGoldSpacer = container.GetNode<Control>("HpGoldSpacer");
            _confirmButton = screen.GetNode<Control>("ConfirmButton");

            _root = new Control
            {
                Name = RootName,
                CustomMinimumSize = new Vector2(0f, 96f),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                Visible = false,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };

            var center = new CenterContainer
            {
                Name = "Center",
                MouseFilter = Control.MouseFilterEnum.Pass
            };
            center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            center.OffsetLeft = GroupOffset.X;
            center.OffsetTop = GroupOffset.Y;
            center.OffsetRight = GroupOffset.X;
            center.OffsetBottom = GroupOffset.Y;
            _root.AddChild(center);

            var surface = new PanelContainer
            {
                Name = "Surface",
                CustomMinimumSize = new Vector2(370f, 94f),
                MouseFilter = Control.MouseFilterEnum.Pass
            };
            surface.AddThemeStyleboxOverride("panel", CreateGroupStyle());
            center.AddChild(surface);

            var inset = new MarginContainer
            {
                Name = "Inset",
                MouseFilter = Control.MouseFilterEnum.Pass
            };
            inset.AddThemeConstantOverride("margin_left", 12);
            inset.AddThemeConstantOverride("margin_top", 6);
            inset.AddThemeConstantOverride("margin_right", 12);
            inset.AddThemeConstantOverride("margin_bottom", 6);
            surface.AddChild(inset);

            var rows = new VBoxContainer
            {
                Name = "Rows",
                MouseFilter = Control.MouseFilterEnum.Pass
            };
            rows.AddThemeConstantOverride("separation", 6);
            inset.AddChild(rows);

            var combatArtChoices = CreateChoiceRow(rows, Localized(CombatArtLabelKey));
            _standard = CreateSegment("Standard", Localized(StandardKey), () => SelectCombatArt(useChibi: false));
            _chibi = CreateSegment("Chibi", Localized(ChibiKey), () => SelectCombatArt(useChibi: true));
            combatArtChoices.AddChild(_standard.Button);
            combatArtChoices.AddChild(_chibi.Button);

            var audioChoices = CreateAudioRow(rows);
            _voiceToggle = CreateSegment("VoiceToggle", string.Empty, ToggleVoice, width: 170f, fontSize: 17);
            _cardBgmToggle = CreateSegment("CardBgmToggle", string.Empty, ToggleCardBgm, width: 170f, fontSize: 17);
            audioChoices.AddChild(_voiceToggle.Button);
            audioChoices.AddChild(_cardBgmToggle.Button);

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

        private void SelectCombatArt(bool useChibi)
        {
            SakuraCombatArtPreference.SetLocalLobbyPreference(_lobby, useChibi);
            Refresh();
        }

        private void ToggleVoice()
        {
            SakuraModConfig.EnableSakuraVoiceBinding.Write(
                !SakuraModConfig.EnableSakuraVoiceBinding.Read());
            Refresh();
        }

        private void ToggleCardBgm()
        {
            SakuraModConfig.EnableCardBgmBinding.Write(
                !SakuraModConfig.EnableCardBgmBinding.Read());
            Refresh();
        }

        private void Refresh()
        {
            if (_characterButton is null)
                return;

            var useChibi = SakuraCombatArtPreference.GetOrInitializeLocalLobbyPreference(_lobby);
            var voiceEnabled = SakuraModConfig.EnableSakuraVoiceBinding.Read();
            var cardBgmEnabled = SakuraModConfig.EnableCardBgmBinding.Read();
            _standard.Selected = !useChibi;
            _chibi.Selected = useChibi;
            UpdateToggle(_voiceToggle, SakuraModConfig.VoiceTitleKey, voiceEnabled);
            UpdateToggle(_cardBgmToggle, CardBgmLabelKey, cardBgmEnabled);
            ApplySegmentStyle(_standard);
            ApplySegmentStyle(_chibi);
            ApplySegmentStyle(_voiceToggle);
            ApplySegmentStyle(_cardBgmToggle);
            RefreshFocusNeighbors(useChibi);
        }

        private void RefreshFocusNeighbors(bool useChibi)
        {
            if (_characterButton is null)
                return;

            var selectedCombatArt = useChibi ? _chibi.Button : _standard.Button;
            var characterPath = _characterButton.GetPath();
            var confirmPath = _confirmButton.GetPath();
            var standardPath = _standard.Button.GetPath();
            var chibiPath = _chibi.Button.GetPath();
            var voiceTogglePath = _voiceToggle.Button.GetPath();
            var cardBgmTogglePath = _cardBgmToggle.Button.GetPath();

            _characterButton.FocusNeighborBottom = selectedCombatArt.GetPath();
            _confirmButton.FocusNeighborTop = cardBgmTogglePath;

            SetFocusNeighbors(_standard.Button, chibiPath, chibiPath, characterPath, voiceTogglePath);
            SetFocusNeighbors(_chibi.Button, standardPath, standardPath, characterPath, cardBgmTogglePath);
            SetFocusNeighbors(_voiceToggle.Button, cardBgmTogglePath, cardBgmTogglePath, standardPath, confirmPath);
            SetFocusNeighbors(_cardBgmToggle.Button, voiceTogglePath, voiceTogglePath, chibiPath, confirmPath);
        }

        private void RestoreFocusNeighbors()
        {
            if (_characterButton is null)
                return;

            _characterButton.FocusNeighborBottom = _originalCharacterBottom;
            _confirmButton.FocusNeighborTop = _originalConfirmTop;
            _characterButton = null;
        }

        private static HBoxContainer CreateChoiceRow(VBoxContainer rows, string labelText)
        {
            var row = new HBoxContainer
            {
                CustomMinimumSize = new Vector2(0f, 38f),
                MouseFilter = Control.MouseFilterEnum.Pass
            };
            row.AddThemeConstantOverride("separation", 12);
            rows.AddChild(row);

            var label = new Label
            {
                Text = labelText,
                CustomMinimumSize = new Vector2(108f, 38f),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            label.AddThemeColorOverride("font_color", TextColor);
            label.AddThemeFontSizeOverride("font_size", 19);
            row.AddChild(label);

            var choices = new HBoxContainer
            {
                CustomMinimumSize = new Vector2(226f, 38f),
                MouseFilter = Control.MouseFilterEnum.Pass
            };
            choices.AddThemeConstantOverride("separation", 2);
            row.AddChild(choices);
            return choices;
        }

        private static HBoxContainer CreateAudioRow(VBoxContainer rows)
        {
            var row = new HBoxContainer
            {
                Name = "AudioChoices",
                CustomMinimumSize = new Vector2(0f, 38f),
                MouseFilter = Control.MouseFilterEnum.Pass
            };
            row.AddThemeConstantOverride("separation", 6);
            rows.AddChild(row);
            return row;
        }

        private OptionSegment CreateSegment(
            string name,
            string text,
            Action select,
            float width = 112f,
            int fontSize = 19)
        {
            var button = new NButton
            {
                Name = name,
                CustomMinimumSize = new Vector2(width, 38f),
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
            label.AddThemeFontSizeOverride("font_size", fontSize);
            button.AddChild(label);

            var segment = new OptionSegment(button, panel, label);
            button.Released += _ =>
            {
                segment.Pressed = false;
                select();
            };
            button.Focused += _ => SetFocused(segment, focused: true);
            button.Unfocused += _ => SetFocused(segment, focused: false);
            button.MousePressed += _ => SetPressed(segment, pressed: true);
            button.MouseReleased += _ => SetPressed(segment, pressed: false);
            return segment;
        }

        private static void UpdateToggle(OptionSegment segment, string labelKey, bool enabled)
        {
            segment.Selected = enabled;
            segment.Label.Text = $"{Localized(labelKey)}: {Localized(enabled ? VoiceOnKey : VoiceOffKey)}";
        }

        private static void SetFocusNeighbors(
            Control control,
            NodePath left,
            NodePath right,
            NodePath top,
            NodePath bottom)
        {
            control.FocusNeighborLeft = left;
            control.FocusNeighborRight = right;
            control.FocusNeighborTop = top;
            control.FocusNeighborBottom = bottom;
        }

        private void SetFocused(OptionSegment segment, bool focused)
        {
            segment.Focused = focused;
            ApplySegmentStyle(segment);
        }

        private void SetPressed(OptionSegment segment, bool pressed)
        {
            segment.Pressed = pressed;
            ApplySegmentStyle(segment);
        }

        private static void ApplySegmentStyle(OptionSegment segment)
        {
            var surfaceColor = segment.Selected ? SelectedSurfaceColor : SegmentSurfaceColor;
            if (segment.Pressed)
                surfaceColor = surfaceColor.Darkened(0.08f);

            var borderWidth = segment.Focused
                ? FocusBorderWidth
                : segment.Selected
                    ? SelectedBorderWidth
                    : DefaultBorderWidth;
            var style = new StyleBoxFlat
            {
                BgColor = surfaceColor,
                BorderColor = segment.Focused ? FocusBorderColor : GroupBorderColor,
                BorderWidthLeft = borderWidth,
                BorderWidthTop = borderWidth,
                BorderWidthRight = borderWidth,
                BorderWidthBottom = borderWidth,
                CornerRadiusTopLeft = CornerRadius,
                CornerRadiusTopRight = CornerRadius,
                CornerRadiusBottomLeft = CornerRadius,
                CornerRadiusBottomRight = CornerRadius
            };
            segment.Panel.AddThemeStyleboxOverride("panel", style);
            segment.Label.AddThemeColorOverride(
                "font_color",
                segment.Selected ? SelectedTextColor : TextColor);
        }

        private static StyleBoxFlat CreateGroupStyle() => new()
        {
            BgColor = GroupSurfaceColor,
            BorderColor = GroupBorderColor,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = CornerRadius,
            CornerRadiusTopRight = CornerRadius,
            CornerRadiusBottomLeft = CornerRadius,
            CornerRadiusBottomRight = CornerRadius
        };

        private static string Localized(string key) =>
            new LocString("settings_ui", key).GetFormattedText();
    }

    private sealed class OptionSegment(NButton button, Panel panel, Label label)
    {
        internal NButton Button { get; } = button;
        internal Panel Panel { get; } = panel;
        internal Label Label { get; } = label;
        internal bool Selected { get; set; }
        internal bool Focused { get; set; }
        internal bool Pressed { get; set; }
    }
}
