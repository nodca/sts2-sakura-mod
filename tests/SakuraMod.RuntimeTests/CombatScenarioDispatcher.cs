using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal static class CombatScenarioDispatcher
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
            "Loaded runtime identities were verified before combat setup.");

        var snapshots = request.ScenarioId switch
        {
            "starter-run" => await StarterRunScenario.ExecuteAsync(request, assertions),
            "clow-shield-singleplayer" => await ClowShieldSingleplayerScenario.ExecuteAsync(request, assertions),
            "clow-mist-slippery" => await ClowMistSlipperyScenario.ExecuteAsync(request, assertions),
            "extra-effect-choice" => await ExtraEffectChoiceScenario.ExecuteAsync(request, assertions),
            "extra-effect-play" => await ExtraEffectPlayScenario.ExecuteAsync(request, assertions),
            "exchange-four-pile-selection" => await ExchangePileScenario.ExecuteAsync(request, assertions),
            "manifest-temporary" => await ManifestTemporaryScenario.ExecuteAsync(request, assertions),
            "generated-pile-memory" => await GeneratedPileMemoryScenario.ExecuteAsync(request, assertions),
            "element-turn-cleanup" => await ElementTurnCleanupScenario.ExecuteAsync(request, assertions),
            "dream-turn-restoration" => await DreamTurnRestorationScenario.ExecuteAsync(request, assertions),
            "spell-turn-transformation" => await SpellTurnTransformationScenario.ExecuteAsync(request, assertions),
            "labyrinth-attack-immunity" => await LabyrinthAttackImmunityScenario.ExecuteAsync(request, assertions),
            "magic-charge-thresholds" => await MagicChargeThresholdScenario.ExecuteAsync(request, assertions),
            "sakura-ancient-cards" => await SakuraAncientCardsScenario.ExecuteAsync(request, assertions),
            "siege-end-turn" => await SiegeEndTurnScenario.ExecuteAsync(request, assertions),
            "combat-transition-cleanup" => await CombatTransitionCleanupScenario.ExecuteAsync(request, assertions),
            "dark-selection-combat-reentry" => await DarkSelectionCombatReentryScenario.ExecuteAsync(request, assertions),
            "through-piercing" => await ThroughPiercingScenario.ExecuteAsync(request, assertions),
            "sakura-erase" => await SakuraEraseScenario.ExecuteAsync(request, assertions),
            "affliction-visual-layout" => await AfflictionVisualLayoutScenario.ExecuteAsync(request, assertions),
            "dark-endpoint" => await DarkEndpointScenario.ExecuteAsync(request, assertions),
            "windy-bind-draw" => await WindyBindDrawScenario.ExecuteAsync(request, assertions),
            "repair-jump-regeneration" => await RepairJumpRegenerationScenario.ExecuteAsync(request, assertions),
            "save-load-restoration" => await SaveLoadRestorationScenario.ExecuteAsync(request, assertions),
            "fourth-act-save-load" => await FourthActSaveLoadScenario.ExecuteAsync(request, assertions),
            "fourth-act-terminal-transition" => await FourthActTerminalTransitionScenario.ExecuteLiveAsync(request, assertions),
            "fourth-act-finished-combat-transition" => await FourthActTerminalTransitionScenario.ExecuteFinishedCombatAsync(request, assertions),
            _ => throw new NotSupportedException(
                $"Combat scenario '{request.ScenarioId}' is not implemented by this host.")
        };
        return (environment, snapshots, []);
    }
}
