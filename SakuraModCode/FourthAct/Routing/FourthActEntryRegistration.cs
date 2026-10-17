using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using SakuraMod.SakuraModCode.Character;
using STS2RitsuLib;
using STS2RitsuLib.Content;
using STS2RitsuLib.RunData;

namespace SakuraMod.SakuraModCode.FourthAct.Routing;

public sealed class FourthActEntryState
{
    public ulong HostNetId { get; set; }
    public bool Enabled { get; set; }
}

internal static class FourthActEntryRegistration
{
    internal const int FourthActSlotIndex = 3;
    internal const int ForcePriority = 100;
    private static RunSavedData<FourthActEntryState>? _runData;

    internal static void RegisterRunData()
    {
        if (_runData is not null)
            return;

        _runData = RitsuLibFramework.GetRunSavedDataStore(MainFile.ModId).Register<FourthActEntryState>(
            "fourth_act_entry_v1",
            options: new RunSavedDataOptions { SchemaVersion = 1, WritePolicy = RunSavedDataWritePolicy.WhenSet });
        RitsuLibFramework.SubscribeLifecycle<RunSavedDataLobbyStagingEvent>(
            static evt =>
            {
                if (evt.IsHost && evt.Reason == RunSavedDataLobbyStagingReason.Committing)
                {
                    _runData.Lobby.Set(evt.Lobby, new FourthActEntryState
                    {
                        HostNetId = evt.Lobby.LocalPlayer.id,
                        Enabled = SakuraModConfig.IsFourthActEnabled()
                    });
                }
            },
            replayCurrentState: false);
    }

    public static bool CanRegister(IEnumerable<FourthActRouteDefinition> routes) =>
        FourthActRouteResolver.Resolve(routes).HasCompleteRoutes;

    internal static bool CanRegister(FourthActRouteResolution resolution) =>
        resolution.HasCompleteRoutes;

    internal static bool CanEnter(bool fourthActEnabled, bool isSakuraHost) =>
        fourthActEnabled && isSakuraHost;

    internal static bool CanEnter(IRunState runState)
    {
        if (runState is RunState state && _runData is not null && _runData.TryGet(state, out var entry))
        {
            return CanEnter(entry.Enabled,
                runState.Players.FirstOrDefault(player => player.NetId == entry.HostNetId)?.Character);
        }

        // Old multiplayer saves lack the host's setting; never substitute a client's local preference.
        return runState.Players.Count == 1
            && CanEnter(SakuraModConfig.IsFourthActEnabled(), runState.Players[0].Character);
    }

    internal static bool CanEnter(bool fourthActEnabled, CharacterModel? hostCharacter) =>
        CanEnter(fourthActEnabled, hostCharacter is not null
            && SakuraStarterCompatibility.IsKinomotoSakuraCharacter(hostCharacter));

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
