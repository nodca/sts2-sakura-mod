using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Singleton;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Pooling;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using STS2RitsuLib.Settings;
using STS2RitsuLib;
using STS2RitsuLib.RunData;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Godot;
using STS2RitsuLib.Telemetry;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.FourthAct.Wind;
using SakuraMod.SakuraModCode.FourthAct.Wind.Encounters;
using SakuraMod.SakuraModCode.FourthAct.Wind.Models;
using SakuraMod.SakuraModCode.FourthAct.Wind.Powers;
using SakuraMod.SakuraModCode.FourthAct.Wind.CardState;
using SakuraMod.SakuraModCode.FourthAct.Dark;
using SakuraMod.SakuraModCode.FourthAct.Dark.Afflictions;
using SakuraMod.SakuraModCode.FourthAct.Dark.Cards;
using SakuraMod.SakuraModCode.FourthAct.Dark.Encounters;
using SakuraMod.SakuraModCode.FourthAct.Dark.Models;
using SakuraMod.SakuraModCode.FourthAct.Dark.Powers;
using SakuraMod.SakuraModCode.FourthAct.Routing;
using SakuraMod.SakuraModCode.FourthAct.Visuals;
using SakuraMod.SakuraModCode.Events;
using SakuraMod.SakuraModCode.Events.Models;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.Relics;
using SakuraMod.SakuraModCode.Telemetry;
using SakuraMod.SakuraModCode;
using SakuraMod.TestProtocol;
using System.Reflection;

namespace SakuraMod.RuntimeTests;

internal static class RuntimeSmokeScenario
{
    private const string SakuraHarmonyOwner = "SakuraMod";

    public static (SakuraRuntimeEnvironment Environment, Dictionary<string, object?> Snapshots, List<string> Artifacts)
        Execute(SakuraTestRequest request, RuntimeAssertionCollector assertions)
    {
        var environment = RuntimeEnvironmentCapture.Capture(request, assertions);
        RuntimeTestHost.WriteCheckpoint(request, "mods_verified", "Loaded mod identities and versions were inspected.");

        assertions.True("ritsulib_initialized", RitsuLibFramework.IsInitialized);
        assertions.True("ritsulib_active", RitsuLibFramework.IsActive);
        assertions.Equal("ritsulib_debug_compatibility", false, StrictRuntimeAdapter.IsRitsuDebugCompatibilityEnabled());
        assertions.True("sakura_voice_lifecycle_cleanup_registered", SakuraVoicePlayback.LifecycleCleanupRegistered);
        assertions.True("another_me_bgm_lifecycle_cleanup_registered", AnotherMeBgmPlayback.LifecycleCleanupRegistered);
        RuntimeTestHost.WriteCheckpoint(request, "ritsulib_verified", "RitsuLib health and strict mode were inspected.");
        var settings = InspectSettings(assertions);
        RuntimeTestHost.WriteCheckpoint(request, "settings_verified", "SakuraMod settings registration was inspected.");

        var patchSnapshot = InspectSakuraPatches();
        assertions.True("sakuramod_harmony_owner_present", patchSnapshot.PatchCount > 0, "No Harmony patch owned by SakuraMod was found.");
        assertions.Equal("sakuramod_duplicate_harmony_patches", 0, patchSnapshot.DuplicateCount);
        var actTransitionPatch = Harmony.GetPatchInfo(AccessTools.Method(
            typeof(RunManager),
            nameof(RunManager.EnterNextAct)));
        assertions.True(
            "fourth_act_transition_patch_owned_by_sakura",
            actTransitionPatch?.Prefixes.Any(static patch => patch.owner == SakuraHarmonyOwner) == true);
        var mapFactoryPatch = Harmony.GetPatchInfo(AccessTools.Method(
            typeof(ActModel),
            nameof(ActModel.CreateMap)));
        assertions.True(
            "fourth_act_map_factory_patch_owned_by_sakura",
            mapFactoryPatch?.Prefixes.Any(static patch => patch.owner == SakuraHarmonyOwner) == true);
        var saveCompatibilityPatch = Harmony.GetPatchInfo(AccessTools.Method(
            typeof(ActModel),
            nameof(ActModel.FromSave),
            [typeof(SerializableActModel)]));
        assertions.True(
            "fourth_act_save_compatibility_patch_owned_by_sakura",
            saveCompatibilityPatch?.Prefixes.Any(static patch => patch.owner == SakuraHarmonyOwner) == true);
        var terminalTransitionPatch = Harmony.GetPatchInfo(AccessTools.Method(
            typeof(NCombatUi),
            nameof(NCombatUi.ProceedWithoutRewards)));
        assertions.True(
            "fourth_act_terminal_transition_patch_owned_by_sakura",
            terminalTransitionPatch?.Prefixes.Any(static patch => patch.owner == SakuraHarmonyOwner) == true);
        var restoredTerminalTransitionPatch = Harmony.GetPatchInfo(AccessTools.Method(
            typeof(RunManager),
            nameof(RunManager.LoadIntoLatestMapCoord),
            [typeof(AbstractRoom)]));
        assertions.True(
            "fourth_act_restored_terminal_transition_patch_owned_by_sakura",
            restoredTerminalTransitionPatch?.Postfixes.Any(static patch => patch.owner == SakuraHarmonyOwner) == true);
        InspectTomoyoAncientAvailability(assertions);
        var standeeHitPatch = Harmony.GetPatchInfo(AccessTools.Method(
            typeof(NCreature),
            nameof(NCreature.SetAnimationTrigger),
            [typeof(string)]));
        assertions.True(
            "fourth_act_standee_hit_patch_owned_by_sakura",
            standeeHitPatch?.Postfixes.Any(static patch => patch.owner == SakuraHarmonyOwner) == true);
        var standeeDeathPatch = Harmony.GetPatchInfo(AccessTools.Method(
            typeof(NCreature),
            nameof(NCreature.StartDeathAnim),
            [typeof(bool)]));
        assertions.True(
            "fourth_act_standee_death_patch_owned_by_sakura",
            standeeDeathPatch?.Prefixes.Any(static patch => patch.owner == SakuraHarmonyOwner) == true);
        var combatArt = InspectSakuraCombatArt(assertions);
        RuntimeTestHost.WriteCheckpoint(request, "harmony_verified", "SakuraMod Harmony ownership was inspected.");
        var multiplayerScaling = InspectFourthActMultiplayerScaling(assertions);
        RuntimeTestHost.WriteCheckpoint(
            request,
            "fourth_act_multiplayer_scaling_verified",
            "Native creature HP scaling was inspected for act indices zero through three.");

        var modelIds = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["character"] = RequireModel(ModelDb.Character<ClassicSakura>()),
            ["clow_sword"] = RequireModel(ModelDb.Card<ClowSword>()),
            ["gale"] = RequireModel(ModelDb.Card<Gale>()),
            ["spell_seal"] = RequireModel(ModelDb.Card<SpellSeal>()),
            ["another_me"] = RequireModel(ModelDb.Card<AnotherMe>()),
            ["growing_magic"] = RequireModel(ModelDb.Card<GrowingMagic>()),
            ["another_me_power"] = RequireModel(ModelDb.Power<AnotherMePower>()),
            ["sealed_wand"] = RequireModel(ModelDb.Relic<ClassicSealedWandRelic>()),
            ["firey_power"] = RequireModel(ModelDb.Power<ClassicFireyPower>()),
            ["card_pool"] = RequireModel(ModelDb.CardPool<ClassicSakuraCardPool>()),
            ["fourth_act"] = RequireModel(ModelDb.Act<SakuraFourthAct>()),
            ["wind_fly_encounter"] = RequireModel(ModelDb.Encounter<FlyEncounter>()),
            ["wind_illusion_encounter"] = RequireModel(ModelDb.Encounter<IllusionEncounter>()),
            ["wind_windy_encounter"] = RequireModel(ModelDb.Encounter<WindyEncounter>()),
            ["wind_fly"] = RequireModel(ModelDb.Monster<FlyMonster>()),
            ["wind_illusion"] = RequireModel(ModelDb.Monster<IllusionMonster>()),
            ["wind_illusion_projection"] = RequireModel(ModelDb.Monster<IllusionProjectionMonster>()),
            ["wind_windy"] = RequireModel(ModelDb.Monster<WindyMonster>()),
            ["wind_dash"] = RequireModel(ModelDb.Monster<DashMonster>()),
            ["wind_float"] = RequireModel(ModelDb.Monster<FloatMonster>()),
            ["wind_sleep"] = RequireModel(ModelDb.Monster<SleepMonster>()),
            ["wind_illusion_identity"] = RequireModel(ModelDb.Power<IllusionIdentityPower>()),
            ["wind_illusion_projection_power"] = RequireModel(ModelDb.Power<IllusionProjectionPower>()),
            ["wind_sovereignty"] = RequireModel(ModelDb.Power<WindSovereigntyPower>()),
            ["wind_bind"] = RequireModel(ModelDb.Power<WindBindPower>()),
            ["wind_wall"] = RequireModel(ModelDb.Power<WindWallPower>()),
            ["windy_next_action_damage"] = RequireModel(ModelDb.Power<WindyNextActionDamagePower>()),
            ["windy_battle"] = RequireModel(ModelDb.Power<WindyBattlePower>()),
            ["wind_float_draw_counter"] = RequireModel(ModelDb.Power<FloatDrawCounterPower>()),
            ["wind_sleep_selection"] = RequireModel(ModelDb.Power<WindSleepSelectionPower>()),
            ["wind_sleep_wake"] = RequireModel(ModelDb.Power<WindSleepWakePower>()),
            ["dark_micro_light"] = RequireModel(ModelDb.Card<MicroLight>()),
            ["dark_confinement_affliction"] = RequireModel(ModelDb.Affliction<DarkConfinementAffliction>()),
            ["wind_sleeping_affliction"] = RequireModel(ModelDb.Affliction<SleepingAffliction>()),
            ["dark_encounter"] = RequireModel(ModelDb.Encounter<DarkEncounter>()),
            ["dark_monster"] = RequireModel(ModelDb.Monster<DarkMonster>()),
            ["dark_micro_light"] = RequireModel(ModelDb.Power<DarkLightPower>()),
            ["dark_night"] = RequireModel(ModelDb.Power<DarkNightPower>()),
            ["dark_veil"] = RequireModel(ModelDb.Power<DarkVeilPower>()),
            ["dark_sovereignty"] = RequireModel(ModelDb.Power<DarkSovereigntyPower>()),
            ["dark_battle"] = RequireModel(ModelDb.Power<DarkBattlePower>()),
            ["dark_confinement_selection"] = RequireModel(ModelDb.Power<DarkConfinementSelectionPower>())
        };
        foreach (var (name, id) in modelIds)
        {
            assertions.True($"modeldb_{name}", !string.IsNullOrWhiteSpace(id), "Resolved model has an empty id.");
        }
        RuntimeTestHost.WriteCheckpoint(request, "models_verified", "Representative ModelDb identities were resolved.");
        var fourthActMap = InspectFourthActMapCreation(assertions);
        RuntimeTestHost.WriteCheckpoint(request, "fourth_act_map_verified", "Fourth-act map creation bypassed StandardActMap.");
        var fourthActSaveCompatibility = InspectFourthActSaveCompatibility(assertions);
        RuntimeTestHost.WriteCheckpoint(
            request,
            "fourth_act_save_compatibility_verified",
            "Fourth-act omitted room collections were restored through native ActModel.FromSave.");
        var windEncounterScenes = InspectWindEncounterScenes(assertions);
        RuntimeTestHost.WriteCheckpoint(request, "wind_encounter_scenes_verified", "Programmatic Wind encounter slots were instantiated.");
        var windEncounterBackgrounds = InspectWindEncounterBackgrounds(assertions);
        RuntimeTestHost.WriteCheckpoint(
            request,
            "wind_encounter_backgrounds_verified",
            "Every Wind encounter prepared and consumed the rooftop background through RitsuLib.");
        var darkEncounterScene = InspectDarkEncounterScene(assertions);
        RuntimeTestHost.WriteCheckpoint(request, "dark_encounter_scene_verified", "The programmatic Dark encounter slot was instantiated.");

        var localization = InspectLocalization(assertions);
        RuntimeTestHost.WriteCheckpoint(request, "localization_verified", "English and Simplified Chinese localization was inspected.");
        var resources = InspectResources(assertions);
        RuntimeTestHost.WriteCheckpoint(request, "resources_verified", "Representative Godot resources were loaded.");
        var enchantmentVisuals = InspectEnchantmentVisuals(assertions);
        RuntimeTestHost.WriteCheckpoint(request, "enchantment_visuals_verified", "Native enchantment controls were inspected on pooled Sakura cards.");

        var selfCheckDirectory = Path.Combine(request.ArtifactRoot, "self-check");
        var selfCheck = SelfCheckReportReader.ReadLatest(selfCheckDirectory);
        assertions.True("self_check_framework_active", selfCheck.FrameworkActive);
        assertions.True("self_check_framework_initialized", selfCheck.FrameworkInitialized);
        assertions.True("self_check_harmony_dump", selfCheck.HarmonyDumpPassed);
        assertions.Equal("self_check_sakuramod_failures", 0, selfCheck.SakuraFailures);
        assertions.Equal("self_check_character_asset_failures", 0, selfCheck.CharacterAssetFailures);
        assertions.Equal("self_check_localization_failures", 0, selfCheck.LocalizationFailures);
        RuntimeTestHost.WriteCheckpoint(request, "self_check_verified", "RitsuLib self-check report was parsed.");

        var snapshots = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["models"] = modelIds,
            ["fourth_act_map"] = fourthActMap,
            ["fourth_act_save_compatibility"] = fourthActSaveCompatibility,
            ["wind_encounter_scenes"] = windEncounterScenes,
            ["wind_encounter_backgrounds"] = windEncounterBackgrounds,
            ["dark_encounter_scene"] = darkEncounterScene,
            ["fourth_act_multiplayer_scaling"] = multiplayerScaling,
            ["settings"] = settings,
            ["sakura_combat_art"] = combatArt,
            ["localization"] = localization,
            ["resources"] = resources,
            ["enchantment_visuals"] = enchantmentVisuals,
            ["harmony"] = new { patchSnapshot.PatchedMethodCount, patchSnapshot.PatchCount, patchSnapshot.DuplicateCount },
            ["self_check"] = selfCheck
        };
        return (environment, snapshots, [selfCheck.ZipPath]);
    }

    private static object InspectSakuraCombatArt(RuntimeAssertionCollector assertions)
    {
        var optionsPatchInfo = Harmony.GetPatchInfo(AccessTools.Method(
            typeof(NCharacterSelectScreen),
            nameof(NCharacterSelectScreen.SelectCharacter),
            [typeof(NCharacterSelectButton), typeof(CharacterModel)]));
        assertions.True(
            "sakura_character_select_options_patch_owned_by_sakura",
            optionsPatchInfo?.Postfixes.Any(static patch =>
                patch.owner == SakuraHarmonyOwner
                && patch.PatchMethod.DeclaringType == typeof(SakuraCharacterSelectOptionsPatch)) == true);

        var patchInfo = Harmony.GetPatchInfo(AccessTools.Method(
            typeof(Creature),
            nameof(Creature.CreateVisuals),
            Type.EmptyTypes));
        assertions.True(
            "sakura_combat_art_player_visual_patch_owned_by_sakura",
            patchInfo?.Prefixes.Any(static patch => patch.owner == SakuraHarmonyOwner) == true);

        var slot = typeof(SakuraCombatArtPreference)
            .GetField("_runData", BindingFlags.NonPublic | BindingFlags.Static)
            ?.GetValue(null) as PlayerRunSavedData<SakuraCombatArtState>;
        assertions.True(
            "sakura_combat_art_per_player_slot_registered",
            slot is not null,
            "The combat_art_v1 per-player run-data slot was not registered.");

        var standardPlayer = Player.CreateForNewRun<ClassicSakura>(UnlockState.all, 101UL);
        var chibiPlayer = Player.CreateForNewRun<ClassicSakura>(UnlockState.all, 202UL);
        var runState = RunState.CreateForTest(
            players: [standardPlayer, chibiPlayer],
            seed: "SAKURA_COMBAT_ART_RUNTIME");
        slot!.Set(
            runState,
            standardPlayer.NetId,
            new SakuraCombatArtState { UseChibi = false });
        slot.Set(
            runState,
            chibiPlayer.NetId,
            new SakuraCombatArtState { UseChibi = true });

        assertions.Equal(
            "sakura_combat_art_standard_player_selection",
            false,
            SakuraCombatArtPreference.IsChibi(standardPlayer));
        assertions.Equal(
            "sakura_combat_art_chibi_player_selection",
            true,
            SakuraCombatArtPreference.IsChibi(chibiPlayer));

        return new
        {
            slot = SakuraCombatArtPreference.RunSavedDataKey,
            standard_player = standardPlayer.NetId,
            standard_use_chibi = SakuraCombatArtPreference.IsChibi(standardPlayer),
            chibi_player = chibiPlayer.NetId,
            chibi_use_chibi = SakuraCombatArtPreference.IsChibi(chibiPlayer)
        };
    }

    private static object InspectFourthActMapCreation(RuntimeAssertionCollector assertions)
    {
        var runState = MegaCrit.Sts2.Core.Runs.RunState.CreateForTest(
            acts: [ModelDb.Act<SakuraFourthAct>()]);
        var map = runState.Act.CreateMap(runState, replaceTreasureWithElites: false);
        assertions.True("fourth_act_create_map_returns_custom_map", map is SakuraFourthActMap);
        return new
        {
            type = map.GetType().FullName,
            routes = (map as SakuraFourthActMap)?.Routes.Count
        };
    }

    private static object InspectFourthActSaveCompatibility(RuntimeAssertionCollector assertions)
    {
        var rooms = new SerializableRoomSet
        {
            EventIds = null!,
            NormalEncounterIds = null!,
            EliteEncounterIds = null!
        };
        var save = new SerializableActModel
        {
            Id = ModelDb.GetId<SakuraFourthAct>(),
            SerializableRooms = rooms
        };

        var restored = ActModel.FromSave(save);
        assertions.True("fourth_act_from_save_restores_model", restored is SakuraFourthAct);
        assertions.True("fourth_act_from_save_restores_events", rooms.EventIds is not null);
        assertions.True("fourth_act_from_save_restores_normal_encounters", rooms.NormalEncounterIds is not null);
        assertions.True("fourth_act_from_save_restores_elite_encounters", rooms.EliteEncounterIds is not null);

        var vanillaRooms = new SerializableRoomSet
        {
            EventIds = null!,
            NormalEncounterIds = null!,
            EliteEncounterIds = null!
        };
        SakuraFourthActSaveCompatibility.NormalizeFourthAct(new SerializableActModel
        {
            Id = ModelDb.GetId<Glory>(),
            SerializableRooms = vanillaRooms
        });
        assertions.True(
            "fourth_act_save_compatibility_ignores_vanilla",
            vanillaRooms.EventIds is null
            && vanillaRooms.NormalEncounterIds is null
            && vanillaRooms.EliteEncounterIds is null);

        return new
        {
            restored_type = restored.GetType().FullName,
            event_count = rooms.EventIds!.Count,
            normal_count = rooms.NormalEncounterIds!.Count,
            elite_count = rooms.EliteEncounterIds!.Count
        };
    }

    private static Dictionary<string, decimal> InspectFourthActMultiplayerScaling(
        RuntimeAssertionCollector assertions)
    {
        var method = AccessTools.Method(
            typeof(MultiplayerScalingModel),
            nameof(MultiplayerScalingModel.GetMultiplayerScaling),
            [typeof(EncounterModel), typeof(int)]);
        var patchInfo = Harmony.GetPatchInfo(method);
        assertions.True(
            "fourth_act_scaling_patch_owned_by_sakura",
            patchInfo?.Prefixes.Any(static patch => patch.owner == SakuraHarmonyOwner) == true);

        var elite = ModelDb.Encounter<FlyEncounter>();
        var boss = ModelDb.Encounter<WindyEncounter>();
        var values = new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            ["act_1_elite_two_players"] = Creature.ScaleHpForMultiplayer(100m, elite, 2, 0),
            ["act_2_elite_two_players"] = Creature.ScaleHpForMultiplayer(100m, elite, 2, 1),
            ["act_3_elite_two_players"] = Creature.ScaleHpForMultiplayer(100m, elite, 2, 2),
            ["act_3_boss_two_players"] = Creature.ScaleHpForMultiplayer(100m, boss, 2, 2),
            ["act_4_elite_two_players"] = Creature.ScaleHpForMultiplayer(100m, elite, 2, 3),
            ["act_4_boss_two_players"] = Creature.ScaleHpForMultiplayer(100m, boss, 2, 3),
            ["act_4_elite_solo"] = Creature.ScaleHpForMultiplayer(100m, elite, 1, 3)
        };

        assertions.Equal("act_1_multiplayer_hp", 220m, values["act_1_elite_two_players"]);
        assertions.Equal("act_2_multiplayer_hp", 240m, values["act_2_elite_two_players"]);
        assertions.Equal("act_3_elite_multiplayer_hp", 240m, values["act_3_elite_two_players"]);
        assertions.Equal("act_3_boss_multiplayer_hp", 260m, values["act_3_boss_two_players"]);
        assertions.Equal("act_4_elite_multiplayer_hp", 240m, values["act_4_elite_two_players"]);
        assertions.Equal("act_4_boss_multiplayer_hp", 260m, values["act_4_boss_two_players"]);
        assertions.Equal("act_4_solo_hp", 100m, values["act_4_elite_solo"]);
        return values;
    }

    private static Dictionary<string, string[]> InspectWindEncounterScenes(RuntimeAssertionCollector assertions)
    {
        var scenes = new Dictionary<string, string[]>(StringComparer.Ordinal);
        Inspect<FlyEncounter>("fly", ["CENTER"]);
        Inspect<IllusionEncounter>("illusion", ["LEFT", "CENTER", "RIGHT"]);
        Inspect<WindyEncounter>("windy", ["ATTENDANT", "BOSS"]);
        return scenes;

        void Inspect<TEncounter>(string name, string[] expectedSlots)
            where TEncounter : WindEncounterTemplate
        {
            var encounter = ModelDb.Encounter<TEncounter>();
            assertions.True($"wind_{name}_has_scene", encounter.HasScene);
            assertions.True(
                $"wind_{name}_declared_slots",
                expectedSlots.SequenceEqual(encounter.Slots),
                $"Expected [{string.Join(", ", expectedSlots)}], found [{string.Join(", ", encounter.Slots)}].");

            Control? scene = null;
            try
            {
                scene = encounter.CreateScene();
                var actualSlots = scene.GetChildren()
                    .OfType<Marker2D>()
                    .Select(static marker => marker.Name.ToString())
                    .ToArray();
                assertions.True(
                    $"wind_{name}_runtime_slots",
                    expectedSlots.SequenceEqual(actualSlots),
                    $"Expected [{string.Join(", ", expectedSlots)}], found [{string.Join(", ", actualSlots)}].");
                scenes[name] = actualSlots;
            }
            finally
            {
                scene?.Dispose();
            }
        }
    }

    private static Dictionary<string, string[]> InspectWindEncounterBackgrounds(
        RuntimeAssertionCollector assertions)
    {
        var mainScene = ResourceLoader.Load<PackedScene>(FourthActCombatBackgrounds.MainScenePath);
        assertions.True("wind_background_main_scene_loaded", mainScene is not null);
        using var mainSceneInstance = mainScene?.Instantiate();
        assertions.True(
            "wind_background_main_scene_type",
            mainSceneInstance is NCombatBackground,
            mainSceneInstance?.GetType().FullName ?? "Scene instantiation returned null.");

        var prepare = AccessTools.Method(
            typeof(ModEncounterTemplate),
            "PrepareProgrammaticCombatBackground");
        var consume = AccessTools.Method(
            typeof(ModEncounterTemplate),
            "ConsumeProgrammaticCombatBackgroundSlot");
        assertions.True("wind_background_prepare_method_resolved", prepare is not null);
        assertions.True("wind_background_consume_method_resolved", consume is not null);

        var snapshots = new Dictionary<string, string[]>(StringComparer.Ordinal);
        Inspect(ModelDb.Encounter<FlyEncounter>(), "fly");
        Inspect(ModelDb.Encounter<IllusionEncounter>(), "illusion");
        Inspect(ModelDb.Encounter<WindyEncounter>(), "windy");
        return snapshots;

        void Inspect(ModEncounterTemplate encounter, string name)
        {
            prepare!.Invoke(encounter, [ModelDb.Act<SakuraFourthAct>(), new Rng(0x53414B55u)]);
            var assets = consume!.Invoke(encounter, null) as BackgroundAssets;
            assertions.True($"wind_{name}_background_prepared", assets is not null);
            assertions.Equal(
                $"wind_{name}_background_scene",
                FourthActCombatBackgrounds.MainScenePath,
                assets?.BackgroundScenePath);
            assertions.True(
                $"wind_{name}_background_layers",
                assets?.BgLayers.SequenceEqual(FourthActCombatBackgrounds.WindRooftopLayers) == true,
                assets is null ? "No background assets." : string.Join(", ", assets.BgLayers));
            assertions.Equal<string?>($"wind_{name}_background_foreground", null, assets?.FgLayer);
            snapshots[name] = assets?.AssetPaths.ToArray() ?? [];
        }
    }

    private static string[] InspectDarkEncounterScene(RuntimeAssertionCollector assertions)
    {
        var encounter = ModelDb.Encounter<DarkEncounter>();
        assertions.True("dark_has_scene", encounter.HasScene);
        assertions.True(
            "dark_declared_slots",
            encounter.Slots.SequenceEqual(["BOSS"]),
            $"Expected [BOSS], found [{string.Join(", ", encounter.Slots)}].");

        var prepare = AccessTools.Method(
            typeof(ModEncounterTemplate),
            "PrepareProgrammaticCombatBackground");
        var consume = AccessTools.Method(
            typeof(ModEncounterTemplate),
            "ConsumeProgrammaticCombatBackgroundSlot");
        assertions.True("dark_background_prepare_method_resolved", prepare is not null);
        assertions.True("dark_background_consume_method_resolved", consume is not null);
        prepare!.Invoke(encounter, [ModelDb.Act<SakuraFourthAct>(), new Rng(0x4441524Bu)]);
        var assets = consume!.Invoke(encounter, null) as BackgroundAssets;
        assertions.True("dark_background_prepared", assets is not null);
        assertions.Equal(
            "dark_background_scene",
            FourthActCombatBackgrounds.MainScenePath,
            assets?.BackgroundScenePath);
        assertions.True(
            "dark_background_layers",
            assets?.BgLayers.SequenceEqual(FourthActCombatBackgrounds.DarkStageLayers) == true,
            assets is null ? "No background assets." : string.Join(", ", assets.BgLayers));
        assertions.Equal<string?>("dark_background_foreground", null, assets?.FgLayer);

        Control? scene = null;
        try
        {
            scene = encounter.CreateScene();
            var actualSlots = scene.GetChildren()
                .OfType<Marker2D>()
                .Select(static marker => marker.Name.ToString())
                .ToArray();
            assertions.True(
                "dark_runtime_slots",
                actualSlots.SequenceEqual(["BOSS"]),
                $"Expected [BOSS], found [{string.Join(", ", actualSlots)}].");
            return actualSlots;
        }
        finally
        {
            scene?.Dispose();
        }
    }

    private static (int PatchedMethodCount, int PatchCount, int DuplicateCount) InspectSakuraPatches()
    {
        var entries = new List<string>();
        var patchedMethods = 0;
        foreach (var method in Harmony.GetAllPatchedMethods())
        {
            var info = Harmony.GetPatchInfo(method);
            if (info is null || !info.Owners.Contains(SakuraHarmonyOwner))
            {
                continue;
            }

            patchedMethods++;
            Add(entries, method, "prefix", info.Prefixes);
            Add(entries, method, "postfix", info.Postfixes);
            Add(entries, method, "transpiler", info.Transpilers);
            Add(entries, method, "finalizer", info.Finalizers);
        }

        var duplicateCount = entries.GroupBy(value => value, StringComparer.Ordinal).Sum(group => Math.Max(0, group.Count() - 1));
        return (patchedMethods, entries.Count, duplicateCount);

        static void Add(List<string> target, MethodBase original, string kind, IEnumerable<Patch> patches)
        {
            foreach (var patch in patches.Where(patch => patch.owner == SakuraHarmonyOwner))
            {
                target.Add($"{original.Module.ModuleVersionId}:{original.MetadataToken}:{kind}:" +
                           $"{patch.PatchMethod.Module.ModuleVersionId}:{patch.PatchMethod.MetadataToken}");
            }
        }
    }

    private static void InspectTomoyoAncientAvailability(RuntimeAssertionCollector assertions)
    {
        var method = AccessTools.Method(
            typeof(Hive),
            nameof(Hive.GetUnlockedAncients),
            [typeof(UnlockState)]);
        var patchInfo = Harmony.GetPatchInfo(method);
        var availabilityPatch = patchInfo?.Postfixes.SingleOrDefault(static patch =>
            patch.owner == SakuraHarmonyOwner
            && patch.PatchMethod.DeclaringType == typeof(TomoyoAncientAvailabilityPatch));
        assertions.True(
            "tomoyo_ancient_availability_patch_owned_by_sakura",
            availabilityPatch is not null);
        assertions.Equal(
            "tomoyo_ancient_availability_patch_runs_after_ritsulib",
            Priority.Last,
            availabilityPatch?.priority);

        var tomoyo = ModelDb.AncientEvent<ClassicTomoyoAncientCostumes>();
        var orobas = ModelDb.AncientEvent<Orobas>();
        AncientEventModel[] candidates = [orobas, tomoyo];
        var sakuraRun = RunState.CreateForTest(
            players: [Player.CreateForNewRun<ClassicSakura>(UnlockState.all, 303UL)],
            seed: "TOMOYO_SAKURA_RUNTIME");
        var regentRun = RunState.CreateForTest(
            players: [Player.CreateForNewRun<Regent>(UnlockState.all, 404UL)],
            seed: "TOMOYO_REGENT_RUNTIME");

        assertions.Equal(
            "tomoyo_ancient_available_for_sakura",
            1,
            TomoyoAncientAvailability.FilterForRun(candidates, sakuraRun).Count(static ancient =>
                ancient is ClassicTomoyoAncientCostumes));
        var regentCandidates = TomoyoAncientAvailability.FilterForRun(candidates, regentRun).ToArray();
        assertions.Equal(
            "tomoyo_ancient_unavailable_for_regent",
            0,
            regentCandidates.Count(static ancient => ancient is ClassicTomoyoAncientCostumes));
        assertions.Equal(
            "tomoyo_filter_preserves_other_ancients",
            1,
            regentCandidates.Count(static ancient => ancient is Orobas));
    }

    private static Dictionary<string, Dictionary<string, string>> InspectLocalization(RuntimeAssertionCollector assertions)
    {
        var originalLanguage = LocManager.Instance.Language;
        var snapshot = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        try
        {
            foreach (var language in new[] { "eng", "zhs" })
            {
                LocManager.Instance.SetLanguage(language);
                var values = new Dictionary<string, string>(StringComparer.Ordinal);
                var localizationKeys = new (string Table, string Key)[]
                {
                             ("settings_ui", SakuraModConfig.VoiceTitleKey),
                             ("settings_ui", SakuraModConfig.VoiceDescriptionKey),
                             ("settings_ui", SakuraCharacterSelectOptionsPatch.CombatArtLabelKey),
                             ("settings_ui", SakuraCharacterSelectOptionsPatch.StandardKey),
                             ("settings_ui", SakuraCharacterSelectOptionsPatch.ChibiKey),
                             ("settings_ui", SakuraCharacterSelectOptionsPatch.CardBgmLabelKey),
                             ("settings_ui", SakuraCharacterSelectOptionsPatch.CardVfxLabelKey),
                             ("settings_ui", SakuraCharacterSelectOptionsPatch.VoiceOnKey),
                             ("settings_ui", SakuraCharacterSelectOptionsPatch.VoiceOffKey),
                             ("characters", "SAKURA_MOD_CHARACTER_CLASSIC_SAKURA.title"),
                             ("acts", "SAKURA_MOD_ACT_SAKURA_FOURTH_ACT.title"),
                             ("cards", "SAKURA_MOD_CARD_CLOW_SWORD.title"),
                             ("cards", "SAKURA_MOD_CARD_GROWING_MAGIC.title"),
                             ("cards", "SAKURA_MOD_CARD_ANOTHER_ME.title"),
                             ("relics", "SAKURA_MOD_RELIC_CLASSIC_SEALED_WAND_RELIC.title"),
                             ("powers", "SAKURA_MOD_POWER_CLASSIC_FIREY_POWER.title"),
                             ("powers", "SAKURA_MOD_POWER_ANOTHER_ME_POWER.title"),
                             ("powers", "SAKURA_MOD_POWER_ILLUSION_IDENTITY_POWER.title"),
                             ("powers", "SAKURA_MOD_POWER_WIND_BIND_POWER.title"),
                             ("powers", "SAKURA_MOD_POWER_WIND_WALL_POWER.title"),
                             ("cards", "SAKURA_MOD_CARD_MICRO_LIGHT.title"),
                             ("monsters", "SAKURA_MOD_MONSTER_DARK_MONSTER.name"),
                             ("powers", "SAKURA_MOD_POWER_DARK_LIGHT_POWER.title"),
                             ("afflictions", "SAKURA_MOD_AFFLICTION_DARK_CONFINEMENT_AFFLICTION.title"),
                             ("afflictions", "SAKURA_MOD_AFFLICTION_SLEEPING_AFFLICTION.title")
                };
                foreach (var (table, key) in localizationKeys.Concat(
                             WindEnemyCatalog.MonsterTypes.Select(static type =>
                                 ("monsters", $"{ModelDb.GetId(type).Entry}.name"))))
                {
                    var exists = LocString.Exists(table, key);
                    assertions.True($"localization_{language}_{table}_{key}_exists", exists);
                    if (!exists)
                    {
                        continue;
                    }

                    var value = new LocString(table, key).GetFormattedText();
                    var usable = !string.IsNullOrWhiteSpace(value)
                        && !value.Contains("MISSING", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(value, key, StringComparison.Ordinal);
                    assertions.True($"localization_{language}_{table}_{key}_usable", usable, value);
                    values[$"{table}/{key}"] = value;
                }

                snapshot[language] = values;
            }
        }
        finally
        {
            LocManager.Instance.SetLanguage(originalLanguage);
        }

        return snapshot;
    }

    private static Dictionary<string, string> InspectResources(RuntimeAssertionCollector assertions)
    {
        var resources = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["combat_scene"] = "res://SakuraMod/scenes/combat/sakura_element_state_hud.tscn",
            ["magic_charge_scene"] = SakuraMagicChargeHud.ScenePath,
            ["spell_turn_scene"] = SpellTurnTransformationVfx.ScenePath,
            ["aqua_water_sphere_scene"] = AquaWaterSphereVfx.ScenePath,
            ["aqua_water_sphere_target_scene"] = AquaWaterSphereVfx.TargetScenePath,
            ["aqua_water_sphere_shader"] = AquaWaterSphereVfx.ShaderPath,
            ["character_scene"] = "res://SakuraMod/scenes/screens/char_select/sakura_character_select_background.tscn",
            ["rest_site_scene"] = "res://SakuraMod/scenes/rest_site/sakura_rest_site_character.tscn",
            ["texture"] = "res://SakuraMod/images/relics/sealed_wand.png",
            ["rest_site_sakura_open"] = "res://SakuraMod/images/charui/rest_site/sakura_open.png",
            ["rest_site_sakura_closed"] = "res://SakuraMod/images/charui/rest_site/sakura_closed.png",
            ["rest_site_kero"] = "res://SakuraMod/images/charui/rest_site/kero_sleeping.png",
            ["kero_combat_companion"] = SakuraKeroCombatCompanion.TexturePath,
            ["rest_site_staff"] = "res://SakuraMod/images/charui/rest_site/sakura_staff.png",
            ["spell_turn_texture"] = SpellTurnTransformationVfx.LuminPath,
            ["growing_magic_portrait"] = "res://SakuraMod/images/card_portraits/ancient/growing_magic.png",
            ["another_me_portrait"] = "res://SakuraMod/images/card_portraits/ancient/another_me.png",
            ["another_me_power_icon"] = "res://SakuraMod/images/powers/another_me.png",
            ["another_me_bgm"] = AnotherMeBgmPlayback.ResourcePath,
            ["release_voice"] = SakuraVoicePlayback.ReleaseVoicePath,
            ["seal_voice"] = SakuraVoicePlayback.SealVoicePath,
            ["spell_turn_audio"] = SpellTurnTransformationVfx.TurnAudioPath,
            ["dark_standee"] = DarkEnemyAssets.Standee,
            ["dark_action"] = DarkEnemyAssets.Action,
            ["dark_confinement_overlay"] = DarkEnemyAssets.ConfinementOverlay,
            ["sleeping_affliction_overlay"] = SleepingAffliction.OverlayScenePath,
            ["micro_light_portrait"] = "res://SakuraMod/images/cards/clear_cards/MICRO_LIGHT.png",
            ["dark_stage_layer"] = FourthActCombatBackgrounds.DarkStageLayerPath,
            ["eternal_night_region_mask"] = FourthActCombatBackgrounds.EternalNightRegionMaskPath,
            ["eternal_night_shader"] = FourthActCombatBackgrounds.EternalNightShaderPath
        };
        for (var index = 0; index < WindEnemyAssets.All.Count; index++)
            resources[$"wind_enemy_{index}"] = WindEnemyAssets.All[index];

        var windyAssets = ModelDb.Encounter<WindyEncounter>().AssetProfile;
        for (var index = 0; index < windyAssets.MapNodeAssetPaths!.Length; index++)
            resources[$"windy_map_node_{index}"] = windyAssets.MapNodeAssetPaths[index];
        resources["windy_run_history"] = windyAssets.RunHistoryIconPath!;
        resources["windy_run_history_outline"] = windyAssets.RunHistoryIconOutlinePath!;
        var darkAssets = ModelDb.Encounter<DarkEncounter>().AssetProfile;
        for (var index = 0; index < darkAssets.MapNodeAssetPaths!.Length; index++)
            resources[$"dark_map_node_{index}"] = darkAssets.MapNodeAssetPaths[index];
        resources["dark_run_history"] = darkAssets.RunHistoryIconPath!;
        resources["dark_run_history_outline"] = darkAssets.RunHistoryIconOutlinePath!;

        foreach (var (name, path) in resources)
        {
            assertions.True($"resource_{name}_exists", ResourceLoader.Exists(path), path);
            Godot.Resource? resource = null;
            try
            {
                resource = ResourceLoader.Load(path);
                assertions.True($"resource_{name}_loads", resource is not null, path);
            }
            finally
            {
                resource?.Dispose();
            }
        }

        var spellTurnScene = ResourceLoader.Load<PackedScene>(SpellTurnTransformationVfx.ScenePath);
        Control? classicTurnRoot = null;
        try
        {
            classicTurnRoot = spellTurnScene?.Instantiate<Control>();
            var mist = classicTurnRoot?.GetNodeOrNull<TextureRect>("%Mist");
            assertions.Equal<TextureRect.StretchModeEnum?>(
                "spell_turn_mist_keeps_native_silhouette_size",
                TextureRect.StretchModeEnum.KeepCentered,
                mist?.StretchMode);
        }
        finally
        {
            classicTurnRoot?.Dispose();
            spellTurnScene?.Dispose();
        }

        var aquaScene = ResourceLoader.Load<PackedScene>(AquaWaterSphereVfx.ScenePath);
        var aquaTargetScene = ResourceLoader.Load<PackedScene>(AquaWaterSphereVfx.TargetScenePath);
        var aquaShader = ResourceLoader.Load<Shader>(AquaWaterSphereVfx.ShaderPath);
        Node2D? aquaRoot = null;
        Node2D? firstAquaTarget = null;
        Node2D? secondAquaTarget = null;
        try
        {
            assertions.True(
                "aqua_water_sphere_resources_are_preloaded",
                AquaWaterSphereVfx.ResourcesArePreloaded);
            aquaRoot = aquaScene?.Instantiate<Node2D>();
            assertions.True("aqua_water_sphere_scene_instantiates", aquaRoot is not null, AquaWaterSphereVfx.ScenePath);
            assertions.True(
                "aqua_water_sphere_stable_back_buffer",
                aquaRoot?.GetNodeOrNull<BackBufferCopy>("StableCombatFrame")?.CopyMode
                    == BackBufferCopy.CopyModeEnum.Viewport);
            assertions.True(
                "aqua_crest_region_is_scene_authored",
                aquaRoot?.GetNodeOrNull<ColorRect>("Crest/CrestBody")?.Material is ShaderMaterial
                && aquaRoot.GetNodeOrNull<Node2D>("Debris") is not null);
            assertions.True(
                "aqua_water_bird_is_retired",
                aquaRoot?.GetNodeOrNull<Node2D>("WaterBird") is null
                && aquaRoot?.GetNodeOrNull<Node2D>("BindingStreams") is null);
            assertions.True("aqua_water_sphere_shader_loads", aquaShader is not null, AquaWaterSphereVfx.ShaderPath);

            firstAquaTarget = aquaTargetScene?.Instantiate<Node2D>();
            secondAquaTarget = aquaTargetScene?.Instantiate<Node2D>();
            var firstMaterial = firstAquaTarget?.GetNodeOrNull<ColorRect>("WaterBody")?.Material as ShaderMaterial;
            var secondMaterial = secondAquaTarget?.GetNodeOrNull<ColorRect>("WaterBody")?.Material as ShaderMaterial;
            assertions.True(
                "aqua_water_sphere_targets_instantiate",
                firstAquaTarget is not null && secondAquaTarget is not null,
                AquaWaterSphereVfx.TargetScenePath);
            assertions.True(
                "aqua_water_sphere_materials_are_independent",
                firstMaterial is not null
                && secondMaterial is not null
                && !ReferenceEquals(firstMaterial, secondMaterial));
            firstMaterial?.SetShaderParameter("formation", 0.73f);
            assertions.Equal(
                "aqua_water_sphere_material_parameters_are_independent",
                0f,
                secondMaterial?.GetShaderParameter("formation").AsSingle() ?? -1f);
            firstMaterial?.SetShaderParameter("freeze", 1f);
            assertions.Equal(
                "aqua_water_freeze_parameter_is_independent",
                0f,
                secondMaterial?.GetShaderParameter("freeze").AsSingle() ?? -1f);
            assertions.True(
                "aqua_water_region_size_is_writable",
                firstAquaTarget?.GetNodeOrNull<ColorRect>("WaterBody") is not null
                && firstMaterial?.GetShaderParameter("region_size").AsVector2() != Vector2.Zero);
        }
        finally
        {
            aquaRoot?.Dispose();
            firstAquaTarget?.Dispose();
            secondAquaTarget?.Dispose();
            aquaScene?.Dispose();
            aquaTargetScene?.Dispose();
            aquaShader?.Dispose();
        }

        var magicChargeScene = ResourceLoader.Load<PackedScene>(SakuraMagicChargeHud.ScenePath);
        Control? magicChargeRoot = null;
        try
        {
            magicChargeRoot = magicChargeScene?.Instantiate<Control>();
            assertions.True(
                "magic_charge_scene_instantiates",
                magicChargeRoot is not null,
                SakuraMagicChargeHud.ScenePath);
            assertions.Equal("magic_charge_root_size", new Vector2(128f, 128f), magicChargeRoot?.Size ?? Vector2.Zero);
            assertions.Equal("magic_charge_root_scale", new Vector2(0.8f, 0.8f), magicChargeRoot?.Scale ?? Vector2.Zero);
            assertions.True("magic_charge_glow_node", magicChargeRoot?.GetNodeOrNull<TextureRect>("%Glow") is not null);
            assertions.True("magic_charge_emblem_node", magicChargeRoot?.GetNodeOrNull<TextureRect>("%Emblem") is not null);
            var chargeLiquid = magicChargeRoot?.GetNodeOrNull<TextureRect>("%ChargeLiquid");
            assertions.True("magic_charge_liquid_node", chargeLiquid is not null);
            assertions.True("magic_charge_liquid_texture", chargeLiquid?.Texture is not null);
            assertions.True("magic_charge_liquid_material", chargeLiquid?.Material is ShaderMaterial);
            assertions.Equal(
                "magic_charge_liquid_default_fill",
                0f,
                (chargeLiquid?.Material as ShaderMaterial)?.GetShaderParameter("fill_ratio").AsSingle() ?? -1f);
            assertions.True("magic_charge_amount_node", magicChargeRoot?.GetNodeOrNull<Label>("%Amount") is not null);
        }
        finally
        {
            magicChargeRoot?.Dispose();
            magicChargeScene?.Dispose();
        }

        var darkStageScene = ResourceLoader.Load<PackedScene>(FourthActCombatBackgrounds.DarkStageLayerPath);
        Control? darkStageRoot = null;
        Control? secondDarkStageRoot = null;
        try
        {
            darkStageRoot = darkStageScene?.Instantiate<Control>();
            var overlay = darkStageRoot?.FindChild(
                FourthActCombatBackgrounds.EternalNightOverlayNodeName,
                recursive: true,
                owned: false) as TextureRect;
            var material = overlay?.Material as ShaderMaterial;
            assertions.True("dark_night_scene_instantiates", darkStageRoot is not null);
            assertions.True("dark_night_overlay_node", overlay is not null);
            assertions.True("dark_night_overlay_material", material is not null);
            assertions.True("dark_night_overlay_starts_hidden", overlay?.Visible == false);
            var regionMask = ResourceLoader.Load<Texture2D>(FourthActCombatBackgrounds.EternalNightRegionMaskPath);
            assertions.Equal("dark_night_mask_width", 2048, regionMask?.GetWidth() ?? -1);
            assertions.Equal("dark_night_mask_height", 960, regionMask?.GetHeight() ?? -1);
            assertions.Equal(
                "dark_night_default_progress",
                0f,
                material?.GetShaderParameter(FourthActCombatBackgrounds.EternalNightProgressParameterName).AsSingle() ?? -1f);
            material?.SetShaderParameter(FourthActCombatBackgrounds.EternalNightProgressParameterName, 5f);

            secondDarkStageRoot = darkStageScene?.Instantiate<Control>();
            var secondOverlay = secondDarkStageRoot?.FindChild(
                FourthActCombatBackgrounds.EternalNightOverlayNodeName,
                recursive: true,
                owned: false) as TextureRect;
            assertions.Equal(
                "dark_night_scene_material_is_local",
                0f,
                (secondOverlay?.Material as ShaderMaterial)?
                    .GetShaderParameter(FourthActCombatBackgrounds.EternalNightProgressParameterName).AsSingle() ?? -1f);
        }
        finally
        {
            darkStageRoot?.Dispose();
            secondDarkStageRoot?.Dispose();
            darkStageScene?.Dispose();
        }

        NRestSiteCharacter? restSiteCharacter = null;
        try
        {
            const string restSiteScenePath =
                "res://SakuraMod/scenes/rest_site/sakura_rest_site_character.tscn";
            restSiteCharacter = RitsuGodotNodeFactories.CreateFromScenePath<NRestSiteCharacter>(restSiteScenePath);
            assertions.True("rest_site_scene_converts", restSiteCharacter is not null, restSiteScenePath);
            assertions.True(
                "rest_site_control_root",
                restSiteCharacter?.GetNodeOrNull<Control>("ControlRoot") is not null);
            assertions.True(
                "rest_site_hitbox",
                restSiteCharacter?.GetNodeOrNull<Control>("%Hitbox") is not null);
            assertions.True(
                "rest_site_thought_bubble_anchors",
                restSiteCharacter?.GetNodeOrNull<Control>("%ThoughtBubbleLeft") is not null
                && restSiteCharacter.GetNodeOrNull<Control>("%ThoughtBubbleRight") is not null);
            assertions.True(
                "rest_site_animation_layers",
                restSiteCharacter?.GetNodeOrNull<Sprite2D>("ControlRoot/Visuals/SakuraRoot/SakuraOpen")?.Texture is not null
                && restSiteCharacter.GetNodeOrNull<Sprite2D>("ControlRoot/Visuals/SakuraRoot/SakuraClosed")?.Texture is not null
                && restSiteCharacter.GetNodeOrNull<Sprite2D>("ControlRoot/Visuals/StaffRoot/Staff")?.Texture is not null
                && restSiteCharacter.GetNodeOrNull<Sprite2D>("ControlRoot/Visuals/KeroRoot/Kero")?.Texture is not null);
            var animationPlayer = restSiteCharacter?.GetNodeOrNull<AnimationPlayer>("ControlRoot/AnimationPlayer");
            assertions.True(
                "rest_site_animation_player",
                animationPlayer?.HasAnimation("rest_idle") == true);
            assertions.True(
                "rest_site_selection_reticle",
                restSiteCharacter?.FindChild("*Reticle*", recursive: true, owned: false) is not null);
        }
        finally
        {
            restSiteCharacter?.Dispose();
        }

        assertions.Equal(
            "runtime_test_resource_namespace_absent",
            false,
            ResourceLoader.Exists("res://SakuraMod.RuntimeTests/fixture.tscn"));
        assertions.Equal(
            "tests_resource_namespace_absent",
            false,
            ResourceLoader.Exists("res://tests/SakuraMod.RuntimeTests/fixture.tscn"));
        InspectSharedCelResourceRecovery(assertions);
        return resources;
    }

    private static void InspectSharedCelResourceRecovery(RuntimeAssertionCollector assertions)
    {
        var first = SakuraMagicCirclePresenter.LoadResources();
        assertions.True(
            "shared_cel_resources_initially_valid",
            GodotObject.IsInstanceValid(first.Shader)
            && GodotObject.IsInstanceValid(first.Ink)
            && GodotObject.IsInstanceValid(first.Knockout));

        first.Shader.Dispose();
        first.Ink.Dispose();
        first.Knockout.Dispose();
        assertions.True(
            "shared_cel_resources_disposed",
            !GodotObject.IsInstanceValid(first.Shader)
            && !GodotObject.IsInstanceValid(first.Ink)
            && !GodotObject.IsInstanceValid(first.Knockout));

        var recovered = SakuraMagicCirclePresenter.LoadResources();
        assertions.True(
            "shared_cel_resources_recovered",
            GodotObject.IsInstanceValid(recovered.Shader)
            && GodotObject.IsInstanceValid(recovered.Ink)
            && GodotObject.IsInstanceValid(recovered.Knockout));
        assertions.True(
            "shared_cel_resources_replace_disposed_instances",
            !ReferenceEquals(first.Shader, recovered.Shader)
            && !ReferenceEquals(first.Ink, recovered.Ink)
            && !ReferenceEquals(first.Knockout, recovered.Knockout));
    }

    private static Dictionary<string, string> InspectSettings(RuntimeAssertionCollector assertions)
    {
        var page = RitsuLibFramework.GetRegisteredModSettings().SingleOrDefault(candidate =>
            candidate.ModId == MainFile.ModId && candidate.Id == SakuraModConfig.PageId);
        assertions.True("settings_sakura_page_registered", page is not null, SakuraModConfig.PageId);

        var section = page?.Sections.SingleOrDefault(candidate => candidate.Id == SakuraModConfig.SectionId);
        assertions.True("settings_sakura_audio_section_registered", section is not null, SakuraModConfig.SectionId);

        var toggle = section?.Entries.OfType<ToggleModSettingsEntryDefinition>().SingleOrDefault(candidate =>
            candidate.Id == SakuraModConfig.VoiceToggleId);
        assertions.True("settings_sakura_voice_toggle_registered", toggle is not null, SakuraModConfig.VoiceToggleId);
        assertions.True(
            "settings_sakura_voice_binding_shared_with_character_select",
            ReferenceEquals(toggle?.Binding, SakuraModConfig.EnableSakuraVoiceBinding));
        assertions.True(
            "settings_sakura_voice_default_on",
            toggle?.Binding is IDefaultModSettingsValueBinding<bool> defaults && defaults.CreateDefaultValue());
        assertions.Equal("settings_sakura_voice_binding_reads", true, toggle?.Binding.Read() ?? false);
        assertions.True(
            "settings_sakura_card_bgm_default_on",
            new SakuraModConfig().EnableCardBgm
            && SakuraModConfig.EnableCardBgmBinding is IDefaultModSettingsValueBinding<bool> cardBgmDefaults
            && cardBgmDefaults.CreateDefaultValue());
        assertions.Equal(
            "settings_sakura_card_bgm_binding_reads",
            true,
            SakuraModConfig.EnableCardBgmBinding.Read());
        assertions.True(
            "settings_sakura_card_vfx_default_on",
            new SakuraModConfig().EnableCardVfx
            && SakuraModConfig.EnableCardVfxBinding is IDefaultModSettingsValueBinding<bool> cardVfxDefaults
            && cardVfxDefaults.CreateDefaultValue());
        assertions.Equal(
            "settings_sakura_card_vfx_binding_reads",
            true,
            SakuraModConfig.EnableCardVfxBinding.Read());

        var gameplaySection = page?.Sections.SingleOrDefault(
            candidate => candidate.Id == SakuraModConfig.GameplaySectionId);
        assertions.True(
            "settings_sakura_gameplay_section_registered",
            gameplaySection is not null,
            SakuraModConfig.GameplaySectionId);

        var fourthActToggle = gameplaySection?.Entries
            .OfType<ToggleModSettingsEntryDefinition>()
            .SingleOrDefault(candidate => candidate.Id == SakuraModConfig.FourthActToggleId);
        assertions.True(
            "settings_sakura_fourth_act_toggle_registered",
            fourthActToggle is not null,
            SakuraModConfig.FourthActToggleId);
        assertions.True(
            "settings_sakura_fourth_act_default_off",
            fourthActToggle?.Binding is IDefaultModSettingsValueBinding<bool> fourthActDefaults
            && !fourthActDefaults.CreateDefaultValue());
        assertions.Equal(
            "settings_sakura_fourth_act_binding_reads",
            false,
            fourthActToggle?.Binding.Read() ?? true);

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["page"] = page?.Id ?? string.Empty,
            ["section"] = section?.Id ?? string.Empty,
            ["toggle"] = toggle?.Id ?? string.Empty,
            ["card_bgm"] = SakuraModConfig.EnableCardBgmBinding.Read().ToString(),
            ["card_vfx"] = SakuraModConfig.EnableCardVfxBinding.Read().ToString(),
            ["gameplay_section"] = gameplaySection?.Id ?? string.Empty,
            ["fourth_act_toggle"] = fourthActToggle?.Id ?? string.Empty
        };
    }

    private static Dictionary<string, string> InspectEnchantmentVisuals(RuntimeAssertionCollector assertions)
    {
        NCard? vanillaNode = null;
        NCard? classicNode = null;
        NCard? clearNode = null;
        NCard? reusedNode = null;
        try
        {
            vanillaNode = CreateAttachedEnchantedCard(ModelDb.Card<Bash>().ToMutable());
            var vanillaTabPosition = vanillaNode.EnchantmentTab.Position;
            var vanillaOverridePosition = vanillaNode.EnchantmentVfxOverride.Position;
            var vanillaOverrideSize = vanillaNode.EnchantmentVfxOverride.Size;
            var vanillaTabSnapshot = DescribeControl(vanillaNode.EnchantmentTab);
            var vanillaOverrideSnapshot = DescribeControl(vanillaNode.EnchantmentVfxOverride);

            classicNode = CreateAttachedThenEnchantCard(ModelDb.Card<ClowSword>().ToMutable());
            AssertEnchantmentLayout(
                assertions,
                "classic_live_change",
                classicNode,
                SakuraCardGeometry.ClassicLayout,
                vanillaTabPosition,
                vanillaOverridePosition,
                vanillaOverrideSize);
            classicNode.ActivateRewardScreenGlow();
            var classicTabSnapshot = DescribeControl(classicNode.EnchantmentTab);
            var classicOverrideSnapshot = DescribeControl(classicNode.EnchantmentVfxOverride);
            AssertEnchantmentLayout(
                assertions,
                "classic_reward_glow",
                classicNode,
                SakuraCardGeometry.ClassicLayout,
                vanillaTabPosition,
                vanillaOverridePosition,
                vanillaOverrideSize);

            clearNode = CreateAttachedThenEnchantCard(ModelDb.Card<Gale>().ToMutable());
            AssertEnchantmentLayout(
                assertions,
                "clear_live_change",
                clearNode,
                SakuraCardGeometry.ClearLayout,
                vanillaTabPosition,
                vanillaOverridePosition,
                vanillaOverrideSize);
            clearNode.ActivateRewardScreenGlow();
            var clearTabSnapshot = DescribeControl(clearNode.EnchantmentTab);
            var clearOverrideSnapshot = DescribeControl(clearNode.EnchantmentVfxOverride);
            AssertEnchantmentLayout(
                assertions,
                "clear_reward_glow",
                clearNode,
                SakuraCardGeometry.ClearLayout,
                vanillaTabPosition,
                vanillaOverridePosition,
                vanillaOverrideSize);

            ReleaseCard(classicNode);
            var releasedClassicNode = classicNode;
            classicNode = null;
            reusedNode = CreateAttachedCard(ModelDb.Card<Bash>().ToMutable());
            assertions.True(
                "enchantment_pool_reuses_classic_node",
                ReferenceEquals(releasedClassicNode, reusedNode),
                "Expected the LIFO NCard pool to return the just-released Classic card node.");
            assertions.Equal("enchantment_pool_clears_tab_visibility", false, reusedNode.EnchantmentTab.Visible);
            assertions.Equal("enchantment_pool_clears_vfx_visibility", false, reusedNode.EnchantmentVfxOverride.Visible);
            assertions.Equal("enchantment_pool_restores_vfx_position", vanillaOverridePosition, reusedNode.EnchantmentVfxOverride.Position);
            assertions.Equal("enchantment_pool_restores_vfx_size", vanillaOverrideSize, reusedNode.EnchantmentVfxOverride.Size);

            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["vanilla_tab_position"] = vanillaTabPosition.ToString(),
                ["vanilla_vfx_box"] = $"{vanillaOverridePosition}:{vanillaOverrideSize}",
                ["vanilla_tab"] = vanillaTabSnapshot,
                ["vanilla_vfx"] = vanillaOverrideSnapshot,
                ["classic_tab"] = classicTabSnapshot,
                ["classic_vfx"] = classicOverrideSnapshot,
                ["clear_tab"] = clearTabSnapshot,
                ["clear_vfx"] = clearOverrideSnapshot
            };
        }
        finally
        {
            ReleaseCard(reusedNode);
            ReleaseCard(clearNode);
            ReleaseCard(classicNode);
            ReleaseCard(vanillaNode);
        }
    }

    private static void AssertEnchantmentLayout(
        RuntimeAssertionCollector assertions,
        string layoutName,
        NCard card,
        SakuraCardLayoutGeometry geometry,
        Vector2 vanillaTabPosition,
        Vector2 vanillaOverridePosition,
        Vector2 vanillaOverrideSize)
    {
        var rootBox = new Rect2(
            (SakuraCardGeometry.VanillaLayoutSize - geometry.RootSize) * 0.5f,
            geometry.RootSize);
        var expectedTabPosition = SakuraCardGeometry.MapNativeCenteredOverlayPosition(vanillaTabPosition);
        var expectedOverridePosition = SakuraCardGeometry.MapNativeCenteredOverlayPosition(vanillaOverridePosition);

        assertions.Equal($"enchantment_{layoutName}_tab_visible", true, card.EnchantmentTab.Visible);
        assertions.Equal($"enchantment_{layoutName}_body_position", rootBox.Position, card.Body.Position);
        assertions.Equal($"enchantment_{layoutName}_body_size", rootBox.Size, card.Body.Size);
        assertions.Equal($"enchantment_{layoutName}_tab_position", expectedTabPosition, card.EnchantmentTab.Position);
        assertions.Equal($"enchantment_{layoutName}_vfx_position", expectedOverridePosition, card.EnchantmentVfxOverride.Position);
        assertions.Equal($"enchantment_{layoutName}_vfx_size", vanillaOverrideSize, card.EnchantmentVfxOverride.Size);
    }

    private static NCard CreateAttachedEnchantedCard(CardModel model)
    {
        CardCmd.Enchant<Sharp>(model, 1m);
        return CreateAttachedCard(model);
    }

    private static NCard CreateAttachedThenEnchantCard(CardModel model)
    {
        var card = CreateAttachedCard(model);
        CardCmd.Enchant<Sharp>(model, 1m);
        return card;
    }

    private static NCard CreateAttachedCard(CardModel model)
    {
        var card = NCard.Create(model) ?? throw new InvalidOperationException($"Could not create NCard for {model.Id}.");
        if (Engine.GetMainLoop() is not SceneTree tree)
            throw new InvalidOperationException("Godot main loop is not a SceneTree.");

        tree.Root.AddChild(card);
        card.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
        return card;
    }

    private static void ReleaseCard(NCard? card)
    {
        if (card is null)
            return;

        card.GetParent()?.RemoveChild(card);
        NodePool.Free(card);
    }

    private static string DescribeControl(Control control) =>
        $"parent={control.GetParent()?.Name};pos={control.Position};global={control.GlobalPosition};" +
        $"size={control.Size};anchors=({control.AnchorLeft},{control.AnchorTop},{control.AnchorRight},{control.AnchorBottom});" +
        $"offsets=({control.OffsetLeft},{control.OffsetTop},{control.OffsetRight},{control.OffsetBottom})";

    private static string RequireModel(AbstractModel model) => model.Id.ToString();
}
