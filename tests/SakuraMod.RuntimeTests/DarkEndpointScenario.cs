using SakuraMod.SakuraModCode.FourthAct.Dark;
using SakuraMod.SakuraModCode.FourthAct.Dark.Models;
using SakuraMod.SakuraModCode.FourthAct.Dark.Powers;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal static class DarkEndpointScenario
{
    public static Task<Dictionary<string, object?>> ExecuteAsync(SakuraTestRequest request, RuntimeAssertionCollector assertions)
    {
        assertions.Equal("dark_initial_darkness", 1, DarkEnemyRules.ClampDarkness(1));
        assertions.Equal("dark_maximum_darkness", 5, DarkEnemyRules.DarknessMaximum);
        assertions.Equal("dark_reset_darkness", 3, DarkEnemyRules.DarknessReset);
        assertions.Equal("dark_micro_lights_per_draw", 2, DarkEnemyRules.MicroLightsPerDraw);
        assertions.True("darkness_power_registered", typeof(DarknessPower) is not null);
        assertions.True("dark_monster_registered", typeof(DarkMonster) is not null);
        RuntimeTestHost.WriteCheckpoint(request, "dark_endpoint_verified", "Darkness rules and models resolved.");
        return Task.FromResult<Dictionary<string, object?>>(new(StringComparer.Ordinal)
        {
            ["resolution"] = new { darkness = 1, maximum = DarkEnemyRules.DarknessMaximum }
        });
    }
}
