using Godot;
using GodotFileAccess = Godot.FileAccess;
using GodotDirAccess = Godot.DirAccess;

namespace SakuraMod.SakuraModCode.Telemetry;

/// <summary>
/// Stores the per-install telemetry token in the game's user data directory
/// (user://), so it survives restarts and is scoped to the local install.
/// All Godot filesystem calls stay inside this class; the adapter depends on
/// the interface only, which keeps headless test suites engine-free.
/// </summary>
internal sealed class GodotInstallTokenStore : IInstallTokenStore
{
    internal const string UserPath = "user://sakuramod_telemetry_install_token.txt";

    public string? Load()
    {
        if (!GodotFileAccess.FileExists(UserPath))
            return null;
        using var file = GodotFileAccess.Open(UserPath, GodotFileAccess.ModeFlags.Read);
        if (file is null)
            return null;
        var token = file.GetAsText().Trim();
        return InstallTokenHttpTelemetryAdapter.IsWellFormedInstallToken(token) ? token : null;
    }

    public void Save(string token)
    {
        using var file = GodotFileAccess.Open(UserPath, GodotFileAccess.ModeFlags.Write);
        file?.StoreString(token);
    }

    public void Clear()
    {
        if (GodotFileAccess.FileExists(UserPath))
            GodotDirAccess.RemoveAbsolute(UserPath);
    }
}
