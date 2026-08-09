using STS2RitsuLib;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Utils.Persistence;

namespace SakuraMod.SakuraModCode;

public sealed class SakuraModConfig
{
    internal const string DataKey = "settings";
    internal const string PageId = "general";
    internal const string SectionId = "audio";
    internal const string GameplaySectionId = "gameplay";
    internal const string VoiceToggleId = "enable_sakura_voice";
    internal const string VoiceTitleKey = "SAKURAMOD-ENABLE_SAKURA_VOICE.title";
    internal const string VoiceDescriptionKey = "SAKURAMOD-ENABLE_SAKURA_VOICE.description";
    internal const string FourthActToggleId = "enable_fourth_act";
    internal const string FourthActTitleKey = "SAKURAMOD-ENABLE_FOURTH_ACT.title";
    internal const string FourthActDescriptionKey = "SAKURAMOD-ENABLE_FOURTH_ACT.description";

    internal static IModSettingsValueBinding<bool> UseChibiCombatArtBinding { get; } =
        ModSettingsBindings.WithDefault(
            ModSettingsBindings.Global<SakuraModConfig, bool>(
                MainFile.ModId,
                DataKey,
                static config => config.UseChibiCombatArt,
                static (config, value) => config.UseChibiCombatArt = value),
            static () => false);

    internal static IModSettingsValueBinding<bool> EnableSakuraVoiceBinding { get; } =
        ModSettingsBindings.WithDefault(
            ModSettingsBindings.Global<SakuraModConfig, bool>(
                MainFile.ModId,
                DataKey,
                static config => config.EnableSakuraVoice,
                static (config, value) => config.EnableSakuraVoice = value),
            static () => true);

    internal static IModSettingsValueBinding<bool> EnableCardBgmBinding { get; } =
        ModSettingsBindings.WithDefault(
            ModSettingsBindings.Global<SakuraModConfig, bool>(
                MainFile.ModId,
                DataKey,
                static config => config.EnableCardBgm,
                static (config, value) => config.EnableCardBgm = value),
            static () => true);

    internal static IModSettingsValueBinding<bool> EnableFourthActBinding { get; } =
        ModSettingsBindings.WithDefault(
            ModSettingsBindings.Global<SakuraModConfig, bool>(
                MainFile.ModId,
                DataKey,
                static config => config.EnableFourthAct,
                static (config, value) => config.EnableFourthAct = value),
            static () => false);

    public bool EnableSakuraVoice { get; set; } = true;
    public bool EnableCardBgm { get; set; } = true;
    public bool EnableFourthAct { get; set; }
    public bool UseChibiCombatArt { get; set; }

    internal static bool IsSakuraVoiceEnabled() => EnableSakuraVoiceBinding.Read();
    internal static bool IsCardBgmEnabled() => EnableCardBgmBinding.Read();
    internal static bool IsFourthActEnabled() => EnableFourthActBinding.Read();
    internal static bool IsChibiCombatArtEnabled() => UseChibiCombatArtBinding.Read();

    public static void Register()
    {
        using var registration = RitsuLibFramework.BeginModDataRegistration(MainFile.ModId);
        RitsuLibFramework.GetDataStore(MainFile.ModId).Register(
            DataKey,
            "settings.json",
            SaveScope.Global,
            static () => new SakuraModConfig(),
            autoCreateIfMissing: true);
        RitsuLibFramework.RegisterModSettings(MainFile.ModId, ConfigureSettingsPage, PageId);
    }

    internal static ModSettingsPage BuildSettingsPageForTests()
    {
        var builder = new ModSettingsPageBuilder(MainFile.ModId, PageId);
        ConfigureSettingsPage(builder);
        return builder.Build();
    }

    private static void ConfigureSettingsPage(ModSettingsPageBuilder page) =>
        page.WithTitle(ModSettingsText.Literal("SakuraMod"))
            .WithModDisplayName(ModSettingsText.Literal("SakuraMod"))
            .WithDescriptionHidden()
            .AddSection(
                SectionId,
                section => section.AddToggle(
                    VoiceToggleId,
                    ModSettingsText.LocString("settings_ui", VoiceTitleKey, "Sakura voice"),
                    EnableSakuraVoiceBinding,
                    ModSettingsText.LocString(
                        "settings_ui",
                        VoiceDescriptionKey,
                        "Play Sakura voice cues on the first eligible spell cards each combat.")))
            .AddSection(
                GameplaySectionId,
                section => section.AddToggle(
                    FourthActToggleId,
                    ModSettingsText.LocString("settings_ui", FourthActTitleKey, "Enable Act 4 (experimental)"),
                    EnableFourthActBinding,
                    ModSettingsText.LocString(
                        "settings_ui",
                        FourthActDescriptionKey,
                        "Enter SakuraMod's unfinished fourth act after Act 3.")));
}
