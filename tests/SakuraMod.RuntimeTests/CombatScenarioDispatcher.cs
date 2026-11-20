using SakuraMod.TestProtocol;
using static SakuraMod.TestProtocol.SakuraCombatScenarios;

namespace SakuraMod.RuntimeTests;

internal static class CombatScenarioDispatcher
{
    private delegate Task<Dictionary<string, object?>> ExecuteScenario(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions);

    private static readonly Lazy<Dictionary<string, ExecuteScenario>> Bindings = new(CreateBindings);

    public static async Task<(
        SakuraRuntimeEnvironment Environment,
        Dictionary<string, object?> Snapshots,
        List<string> Artifacts)> ExecuteAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions)
    {
        var bindings = Bindings.Value;
        var environment = RuntimeEnvironmentCapture.Capture(request, assertions);
        RuntimeTestHost.WriteCheckpoint(
            request,
            "environment_verified",
            "Loaded runtime identities were verified before combat setup.");

        if (!bindings.TryGetValue(request.ScenarioId, out var execute))
            throw new NotSupportedException(
                $"Combat scenario '{request.ScenarioId}' is not implemented by this host.");

        return (environment, await execute(request, assertions), []);
    }

    private static Dictionary<string, ExecuteScenario> CreateBindings()
    {
        var bindings = new Dictionary<string, ExecuteScenario>(StringComparer.Ordinal)
        {
            { StarterRun, StarterRunScenario.ExecuteAsync },
            { ExtraEffectChoice, ExtraEffectChoiceScenario.ExecuteAsync },
            { ExtraEffectPlay, ExtraEffectPlayScenario.ExecuteAsync },
            { ExchangeFourPileSelection, ExchangePileScenario.ExecuteAsync },
            { ManifestTemporary, ManifestTemporaryScenario.ExecuteAsync },
            { GeneratedPileMemory, GeneratedPileMemoryScenario.ExecuteAsync },
            { ForgottenCleanupWithoutTransparent, ForgottenCleanupWithoutTransparentScenario.ExecuteAsync },
            { ElementTurnCleanup, ElementTurnCleanupScenario.ExecuteAsync },
            { DreamTurnRestoration, DreamTurnRestorationScenario.ExecuteAsync },
            { SpellTurnTransformation, SpellTurnTransformationScenario.ExecuteAsync },
            { WandTurnPersistence, WandTurnPersistenceScenario.ExecuteAsync },
            { LabyrinthField, LabyrinthFieldScenario.ExecuteAsync },
            { MagicChargeThresholds, MagicChargeThresholdScenario.ExecuteAsync },
            { FrostbiteThreshold, FrostbiteThresholdScenario.ExecuteAsync },
            { ClassicTimeIntegration, ClassicTimeIntegrationScenario.ExecuteAsync },
            { CombatTransitionCleanup, CombatTransitionCleanupScenario.ExecuteAsync },
            { DarkSelectionCombatReentry, DarkSelectionCombatReentryScenario.ExecuteAsync },
            { QueenBoundCleanup, QueenBoundCleanupScenario.ExecuteAsync },
            { FourthActCardAfflictions, FourthActCardAfflictionScenario.ExecuteAsync },
            { DarkEndpoint, DarkEndpointScenario.ExecuteAsync },
            { LightEternalDay, LightEternalDayScenario.ExecuteAsync },
            { SaveLoadRestoration, SaveLoadRestorationScenario.ExecuteAsync },
            { FourthActSaveLoad, FourthActSaveLoadScenario.ExecuteAsync },
            { FourthActTerminalTransition, FourthActTerminalTransitionScenario.ExecuteLiveAsync },
            { FourthActFinishedCombatTransition, FourthActTerminalTransitionScenario.ExecuteFinishedCombatAsync },
        };

        var missing = All.Except(bindings.Keys, StringComparer.Ordinal).ToArray();
        var unknown = bindings.Keys.Except(All, StringComparer.Ordinal).ToArray();
        if (missing.Length != 0 || unknown.Length != 0)
            throw new InvalidOperationException(
                $"Invalid combat scenario bindings. Missing: [{string.Join(", ", missing)}]. "
                + $"Unknown: [{string.Join(", ", unknown)}].");

        return bindings;
    }
}
