namespace SakuraMod.TestProtocol;

internal static class SakuraCombatScenarios
{
    private static readonly OrderedDictionary<string, bool> SaveLoadById = new(StringComparer.Ordinal);

    public static readonly string StarterRun = Register("starter-run");
    public static readonly string ClowShieldSingleplayer = Register("clow-shield-singleplayer");
    public static readonly string ClowMistSlippery = Register("clow-mist-slippery");
    public static readonly string SleepWake = Register("sleep-wake");
    public static readonly string SleepTunneler = Register("sleep-tunneler");
    public static readonly string ClowFlyAirborne = Register("clow-fly-airborne");
    public static readonly string ExtraEffectChoice = Register("extra-effect-choice");
    public static readonly string ExtraEffectPlay = Register("extra-effect-play");
    public static readonly string ExchangeFourPileSelection = Register("exchange-four-pile-selection");
    public static readonly string ManifestTemporary = Register("manifest-temporary");
    public static readonly string GeneratedPileMemory = Register("generated-pile-memory");
    public static readonly string RemindRecallsOriginals = Register("remind-recalls-originals");
    public static readonly string AppearPowerManifest = Register("appear-power-manifest");
    public static readonly string RepairMemoryHeal = Register("repair-memory-heal");
    public static readonly string ForgottenCleanupWithoutTransparent = Register("forgotten-cleanup-without-transparent");
    public static readonly string ElementTurnCleanup = Register("element-turn-cleanup");
    public static readonly string DreamTurnRestoration = Register("dream-turn-restoration");
    public static readonly string SpellTurnTransformation = Register("spell-turn-transformation");
    public static readonly string WandTurnPersistence = Register("wand-turn-persistence");
    public static readonly string LabyrinthField = Register("labyrinth-field");
    public static readonly string MagicChargeThresholds = Register("magic-charge-thresholds");
    public static readonly string SakuraAncientCards = Register("sakura-ancient-cards");
    public static readonly string SiegeEndTurn = Register("siege-end-turn");
    public static readonly string FreezeTurnCoverage = Register("freeze-turn-coverage");
    public static readonly string FrostbiteThreshold = Register("frostbite-threshold");
    public static readonly string SakuraTimeFreeze = Register("sakura-time-freeze");
    public static readonly string ClowTimeStasis = Register("clow-time-stasis");
    public static readonly string ClassicTimeIntegration = Register("classic-time-integration");
    public static readonly string SnowFrostbite = Register("snow-frostbite");
    public static readonly string SaveLoadRestoration = Register("save-load-restoration", isSaveLoad: true);
    public static readonly string FourthActSaveLoad = Register("fourth-act-save-load", isSaveLoad: true);
    public static readonly string FourthActTerminalTransition = Register("fourth-act-terminal-transition");
    public static readonly string FourthActFinishedCombatTransition = Register("fourth-act-finished-combat-transition", isSaveLoad: true);
    public static readonly string CombatTransitionCleanup = Register("combat-transition-cleanup");
    public static readonly string DarkSelectionCombatReentry = Register("dark-selection-combat-reentry");
    public static readonly string ThroughPoison = Register("through-poison");
    public static readonly string ChangeSwap = Register("change-swap");
    public static readonly string LucidSearch = Register("lucid-search");
    public static readonly string AquaSweep = Register("aqua-sweep");
    public static readonly string SakuraErase = Register("sakura-erase");
    public static readonly string AfflictionVisualLayout = Register("affliction-visual-layout");
    public static readonly string CardLayoutPooledReuse = Register("card-layout-pooled-reuse");
    public static readonly string QueenBoundCleanup = Register("queen-bound-cleanup");
    public static readonly string FourthActCardAfflictions = Register("fourth-act-card-afflictions");
    public static readonly string FourthActAudio = Register("fourth-act-audio");
    public static readonly string CleansingStats = Register("cleansing-stats");
    public static readonly string DarkEndpoint = Register("dark-endpoint");
    public static readonly string LightEternalDay = Register("light-eternal-day", isSaveLoad: true);
    public static readonly string WindyBindDraw = Register("windy-bind-draw");

    public static IReadOnlyList<string> All => SaveLoadById.Keys;

    public static bool IsSaveLoad(string scenarioId) => SaveLoadById[scenarioId];

    private static string Register(string id, bool isSaveLoad = false)
    {
        SaveLoadById.Add(id, isSaveLoad);
        return id;
    }
}
