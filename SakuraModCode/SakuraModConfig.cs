using STS2RitsuLib;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Utils.Persistence;

namespace SakuraMod.SakuraModCode;

public sealed class SakuraModConfig
{
    internal const string DataKey = "settings";
    internal const string PageId = "general";
    internal const string SectionId = "audio";
    internal const string VoiceToggleId = "enable_sakura_voice";
    internal const string VoiceTitleKey = "SAKURAMOD-ENABLE_SAKURA_VOICE.title";
    internal const string VoiceDescriptionKey = "SAKURAMOD-ENABLE_SAKURA_VOICE.description";

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
            static () => false);

    public bool EnableSakuraVoice { get; set; }
    public bool UseChibiCombatArt { get; set; }

    internal static bool IsSakuraVoiceEnabled() => EnableSakuraVoiceBinding.Read();
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
                        "Play Sakura voice cues on the first eligible spell cards each combat.")));
}
