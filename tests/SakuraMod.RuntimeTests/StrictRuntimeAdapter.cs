using STS2RitsuLib;
using System.Reflection;

namespace SakuraMod.RuntimeTests;

internal static class StrictRuntimeAdapter
{
    // RitsuLib exposes framework health publicly but not the effective debug-compatibility switch.
    // Keep that one version-specific check here and fail closed if the internal signature changes.
    public static bool IsRitsuDebugCompatibilityEnabled()
    {
        var settingsType = typeof(RitsuLibFramework).Assembly.GetType(
            "STS2RitsuLib.Data.RitsuLibSettingsStore",
            throwOnError: true)!;
        var method = settingsType.GetMethod(
            "IsDebugCompatibilityMasterEnabled",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(settingsType.FullName, "IsDebugCompatibilityMasterEnabled");
        if (method.ReturnType != typeof(bool) || method.GetParameters().Length != 0)
        {
            throw new MissingMethodException(
                settingsType.FullName,
                "IsDebugCompatibilityMasterEnabled(): bool");
        }

        return (bool)(method.Invoke(null, null)
            ?? throw new InvalidOperationException("RitsuLib strict-mode query returned null."));
    }
}
