using SakuraMod.TestProtocol;

namespace SakuraMod.TestRunner;

public static class RuntimeProfile
{
    public static async Task WriteStrictSettingsAsync(
        RuntimeWorkspace workspace,
        string selfCheckDirectory,
        bool runSelfCheck = true)
    {
        var gameAccountRoot = Path.Combine(workspace.UserDataDirectory, "default", "1");
        Directory.CreateDirectory(gameAccountRoot);
        await SakuraTestProtocol.WriteAtomicAsync(Path.Combine(gameAccountRoot, "settings.save"), new
        {
            schema_version = 5,
            language = "eng",
            mod_settings = new
            {
                mods_enabled = true,
                mod_list = Array.Empty<object>()
            },
            seen_ea_disclaimer = true,
            skip_intro_logo = true
        });

        var ritsuRoot = Path.Combine(
            gameAccountRoot,
            "mod_data",
            "com.ritsukage.sts2-RitsuLib");
        Directory.CreateDirectory(ritsuRoot);
        await SakuraTestProtocol.WriteAtomicAsync(Path.Combine(ritsuRoot, "settings.json"), new
        {
            schema_version = 15,
            sync_mod_data_to_steam_cloud = false,
            debug_compatibility_mode = false,
            debug_compat_loc_table = false,
            debug_compat_unlock_epoch = false,
            debug_compat_ancient_architect = false,
            debug_log_viewer_enabled = false,
            debug_log_viewer_mirror_game_logs = false,
            debug_log_viewer_auto_open = false,
            debug_log_viewer_lan_access_enabled = false,
            harmony_patch_dump_output_path = string.Empty,
            harmony_patch_dump_on_first_main_menu = false,
            self_check_output_folder_path = selfCheckDirectory,
            self_check_on_first_main_menu = runSelfCheck,
            update_check_enabled = false,
            steam_workshop_auto_update_check_enabled = false,
            main_menu_mod_settings_button_enabled = false,
            modeldb_deterministic_sort_mode = "force",
            toast_enabled = false
        });

        var telemetryRoot = Path.Combine(ritsuRoot, "telemetry");
        Directory.CreateDirectory(telemetryRoot);
        await SakuraTestProtocol.WriteAtomicAsync(Path.Combine(telemetryRoot, "consent.json"), new
        {
            schema_version = 1,
            applicants = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["com.ritsukage.sts2-RitsuLib"] = DeniedConsent(),
                ["SakuraMod"] = DeniedConsent()
            }
        });
    }

    public static IReadOnlyDictionary<string, string?> CreateEnvironment(
        RuntimeWorkspace workspace,
        string requestPath) => new Dictionary<string, string?>
        {
            ["HOME"] = workspace.HomeDirectory,
            ["XDG_DATA_HOME"] = workspace.DataDirectory,
            ["XDG_CONFIG_HOME"] = workspace.ConfigDirectory,
            ["XDG_CACHE_HOME"] = workspace.CacheDirectory,
            ["LANG"] = "en_US.UTF-8",
            ["LC_ALL"] = "en_US.UTF-8",
            [SakuraTestProtocol.RequestEnvironmentVariable] = requestPath,
            ["DOTNET_EnableDiagnostics"] = "0"
        };

    private static object DeniedConsent() => new
    {
        consent = "Denied",
        granted_requests = Array.Empty<string>(),
        shared_contribution_sources = new Dictionary<string, string[]>()
    };
}
