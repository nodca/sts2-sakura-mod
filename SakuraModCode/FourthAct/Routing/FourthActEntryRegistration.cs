using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using SakuraMod.SakuraModCode.Character;
using STS2RitsuLib.Content;

namespace SakuraMod.SakuraModCode.FourthAct.Routing;

internal static class FourthActEntryRegistration
{
    internal const int FourthActSlotIndex = 3;
    internal const int ForcePriority = 100;

    public static bool CanRegister(IEnumerable<FourthActRouteDefinition> routes) =>
        FourthActRouteResolver.Resolve(routes).HasCompleteRoutes;

    internal static bool CanRegister(FourthActRouteResolution resolution) =>
        resolution.HasCompleteRoutes;

    internal static bool CanEnter(bool fourthActEnabled, bool isSakuraRun) =>
        fourthActEnabled && isSakuraRun;

    internal static bool CanEnter(IRunState runState) =>
        CanEnter(
            SakuraModConfig.IsFourthActEnabled(),
            SakuraStarterCompatibility.IsKinomotoSakuraRun(runState));

    public static bool RegisterIfComplete<TAct>(
        ModContentRegistry registry,
        IEnumerable<FourthActRouteDefinition> routes)
        where TAct : ActModel
    {
        var resolution = FourthActRouteResolver.Resolve(routes);
        return RegisterIfComplete<TAct>(registry, resolution);
    }

    internal static bool RegisterIfComplete<TAct>(
        ModContentRegistry registry,
        FourthActRouteResolution resolution)
        where TAct : ActModel
    {
        if (!CanRegister(resolution))
            return false;

        registry.RegisterAct<TAct>();
        registry.RegisterActEnterForce<TAct>(
            FourthActSlotIndex,
            ForcePriority,
            static context => CanEnter(context.RunState));
        return true;
    }
}
