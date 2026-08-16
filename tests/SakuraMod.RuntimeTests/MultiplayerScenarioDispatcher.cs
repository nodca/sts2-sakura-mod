using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal static class MultiplayerScenarioDispatcher
{
    public static async Task<(
        SakuraRuntimeEnvironment Environment,
        Dictionary<string, object?> Snapshots,
        List<string> Artifacts)> ExecuteAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions)
    {
        var environment = RuntimeEnvironmentCapture.Capture(request, assertions);
        RuntimeTestHost.WriteCheckpoint(
            request,
            "environment_verified",
            "Loaded runtime identities were verified before multiplayer setup.");
        var snapshots = request.ScenarioId switch
        {
            SakuraMultiplayerScenarios.ClowDefensivePowers => await ClowDefensivePowersMultiplayerScenario.ExecuteAsync(
                request, environment, assertions),
            SakuraMultiplayerScenarios.ClowSilentHost
                or SakuraMultiplayerScenarios.ClowSilentClient
                or SakuraMultiplayerScenarios.ClowShieldHost
                or SakuraMultiplayerScenarios.ClowShieldClient
                or SakuraMultiplayerScenarios.ClowShieldWard =>
                await ClowDefensivePowerFocusedMultiplayerScenario.ExecuteAsync(
                    request, environment, assertions),
            SakuraMultiplayerScenarios.SealedWandCharge => await SealedWandChargeMultiplayerScenario.ExecuteAsync(
                request, environment, assertions),
            SakuraMultiplayerScenarios.TurnEndDamageSync => await TurnEndDamageSyncMultiplayerScenario.ExecuteAsync(
                request, environment, assertions),
            SakuraMultiplayerScenarios.ThreePlayerDefensivePowers =>
                await ClowDefensivePowersMultiplayerScenario.ExecuteAsync(request, environment, assertions),
            SakuraMultiplayerScenarios.ThreePlayerRepairJump =>
                await ThreePlayerRepairJumpMultiplayerScenario.ExecuteAsync(request, environment, assertions),
            SakuraMultiplayerScenarios.ThreePlayerRepairJumpLoad =>
                await ThreePlayerRepairJumpLoadMultiplayerScenario.ExecuteAsync(request, environment, assertions),
            SakuraMultiplayerScenarios.ThreePlayerMirrorCopy =>
                await ThreePlayerMirrorCopyMultiplayerScenario.ExecuteAsync(request, environment, assertions),
            _ => throw new NotSupportedException(
                $"Multiplayer scenario '{request.ScenarioId}' is not implemented by this host.")
        };
        var artifacts = Directory.Exists(request.Multiplayer!.CoordinationRoot)
            ? Directory.EnumerateFiles(request.Multiplayer.CoordinationRoot)
                .Order(StringComparer.Ordinal)
                .ToList()
            : [];
        return (environment, snapshots, artifacts);
    }
}
