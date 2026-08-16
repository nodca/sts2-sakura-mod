using MegaCrit.Sts2.Core.Debug;
using STS2RitsuLib.Compat;
using STS2RitsuLib;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal static class RuntimeEnvironmentCapture
{
    public static SakuraRuntimeEnvironment Capture(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions)
    {
        var loadedMods = RitsuModManager.GetKnownMods()
            .Where(mod => mod.IsLoaded)
            .OrderBy(mod => mod.Id, StringComparer.Ordinal)
            .ToArray();
        var loadedIds = loadedMods.Select(mod => mod.Id).ToArray();
        assertions.Equal(
            "loaded_mod_ids",
            "STS2-RitsuLib, SakuraMod, SakuraMod.RuntimeTests",
            string.Join(", ", loadedIds));
        RequireVersion(assertions, loadedMods, "STS2-RitsuLib", request.ExpectedRitsuVersion);
        RequireVersion(assertions, loadedMods, "SakuraMod", request.ExpectedSakuraVersion);
        RequireVersion(assertions, loadedMods, RuntimeTestMod.ModId, "1.0.0");

        var gameVersion = NormalizeVersion(ReleaseInfoManager.Instance.ReleaseInfo?.Version);
        assertions.Equal("game_version", NormalizeVersion(request.ExpectedGameVersion), gameVersion);
        return new SakuraRuntimeEnvironment(
            gameVersion,
            request.ExpectedRitsuVersion,
            request.ExpectedSakuraVersion,
            typeof(RuntimeTestMod).Assembly.GetName().Version?.ToString() ?? "unknown",
            System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            loadedMods.Select(mod => new SakuraLoadedMod(
                mod.Id,
                mod.Version,
                mod.State.ToString(),
                mod.Source.ToString(),
                mod.AssemblyName,
                mod.AssemblyVersion)).ToArray());
    }

    private static void RequireVersion(
        RuntimeAssertionCollector assertions,
        IReadOnlyList<RitsuModInfo> mods,
        string id,
        string expected)
    {
        var matches = mods.Where(mod => mod.Id == id).ToArray();
        assertions.Equal($"{id}_loaded_once", 1, matches.Length);
        if (matches.Length == 1)
        {
            assertions.Equal(
                $"{id}_version",
                NormalizeVersion(expected),
                NormalizeVersion(matches[0].Version));
        }
    }

    private static string NormalizeVersion(string? version) =>
        string.IsNullOrWhiteSpace(version) ? "unknown" : version.Trim().TrimStart('v');
}
