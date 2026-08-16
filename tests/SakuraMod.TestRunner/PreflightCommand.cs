using SakuraMod.TestProtocol;

namespace SakuraMod.TestRunner;

public static class PreflightCommand
{
    public static async Task<int> RunAsync(string repoRoot)
    {
        try
        {
            var prerequisites = RuntimePreflight.Inspect(repoRoot);
            Console.WriteLine($"[preflight] PASS: STS2 {prerequisites.GameVersion}, RitsuLib {prerequisites.RitsuVersion}, SakuraMod {prerequisites.SakuraVersion}");
            Console.WriteLine($"[preflight] game: {prerequisites.GameRoot}");
            Console.WriteLine($"[preflight] RitsuLib: {prerequisites.RitsuPackageRoot}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"[preflight] FAIL: {exception.Message}");
            return 1;
        }
    }
}
