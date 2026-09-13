using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using SakuraMod.SakuraModCode.FourthAct.Fire;
using SakuraMod.SakuraModCode.FourthAct.Fire.Encounters;
using SakuraMod.SakuraModCode.FourthAct.Fire.Models;
using SakuraMod.SakuraModCode.FourthAct.Routing;
using SakuraMod.SakuraModCode.FourthAct.Visuals;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal static class LightEternalDayScenario
{
    public static async Task<Dictionary<string, object?>> ExecuteAsync(SakuraTestRequest request, RuntimeAssertionCollector assertions)
    {
        if (request.Phase == "read")
            return await ReadAsync(request, assertions);
        if (request.Phase != "write" || string.IsNullOrWhiteSpace(request.PriorSnapshotPath))
            throw new InvalidDataException("Light scenario requires write/read phases and snapshot path.");
        ActModel[] acts = [.. ActModel.GetDefaultList(), ModelDb.Act<SakuraFourthAct>()];
        var context = await CombatScenarioContext.StartAsync(request, shouldSave: true, acts: acts);
        var combat = await context.EnterLightCombatAsync();
        var light = combat.Enemies.Single(c => c.Monster is LightMonster);
        var painting = (TextureRect)NCombatRoom.Instance!.FindChild("LightEternalDayPainting", true, false);
        var plate = (ShaderMaterial)painting.Material;
        var sun = (ShaderMaterial)painting.GetNode<TextureRect>("Sun").Material;
        float Progress() => plate.GetShaderParameter("empowered").AsSingle();
        Task WaitForProgress(float target) => CombatScenarioContext.WaitUntilAsync(
            () => Mathf.IsEqualApprox(Progress(), target), "Light lighting " + target);
        assertions.Equal("light_native_container", "NCombatBackground", NCombatRoom.Instance.Background!.GetType().Name);
        assertions.Equal("light_shader", FourthActCombatBackgrounds.LightEternalDayShaderPath, plate.Shader.ResourcePath);
        assertions.True("light_opening_hand", context.Player.PlayerCombatState!.Hand.Cards.Count >= 5);
        assertions.Equal("light_starts_normal", 0f, Progress());

        // Labeled setup: drive actual Creature HP events without playing a damage card.
        var threshold = (int)(light.MaxHp * 0.6m);
        light.SetCurrentHpInternal(threshold);
        await WaitForProgress(1);
        light.SetCurrentHpInternal(threshold + 1);
        await WaitForProgress(0);
        light.SetCurrentHpInternal(threshold - 10);
        await NGame.Instance!.ToSignal(NGame.Instance.GetTree().CreateTimer(.15), SceneTreeTimer.SignalName.Timeout);
        assertions.True("light_transition_in_flight", Progress() > 0 && Progress() < 1);
        light.SetCurrentHpInternal(light.MaxHp);
        await WaitForProgress(0);
        assertions.Equal("light_sun_and_plate_agree", 0f, sun.GetShaderParameter("empowered").AsSingle());

        // Labeled setup: extra player HP lets all three native enemy moves resolve.
        var playerCreature = context.Player.Creature;
        playerCreature.SetMaxHpInternal(1000);
        playerCreature.SetCurrentHpInternal(1000);
        await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(context.Player); // Radiance
        await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(context.Player); // Benediction
        var chargeSeen = false;
        var ringSeen = false;
        var damageSeen = false;
        var ringAtDamage = -1f;
        var actualDamage = 0;
        var expectedDamage = 0;
        void OnHpChanged(int previous, int current)
        {
            if (current >= previous)
                return;
            damageSeen = true;
            ringAtDamage = sun.GetShaderParameter("judgment_release").AsSingle();
            actualDamage = previous - current;
            expectedDamage = FireEnemyRules.JudgmentDamage(context.Player.PlayerCombatState!.Hand.Cards.Count, false);
        }
        playerCreature.CurrentHpChanged += OnHpChanged;
        var previousAutoSlayerCheck = NonInteractiveMode.AutoSlayerCheck;
        NonInteractiveMode.AutoSlayerCheck = static () => false;
        try
        {
            assertions.True("light_uses_native_animation_timing", !NonInteractiveMode.IsActive);
            var turn = CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(context.Player);
            while (!turn.IsCompleted)
            {
                chargeSeen |= sun.GetShaderParameter("judgment_charge").AsSingle() > 0;
                ringSeen |= sun.GetShaderParameter("judgment_release").AsSingle() >= 0;
                await NGame.Instance.AwaitProcessFrame();
            }
            await turn;
        }
        finally
        {
            NonInteractiveMode.AutoSlayerCheck = previousAutoSlayerCheck;
            playerCreature.CurrentHpChanged -= OnHpChanged;
        }
        assertions.True("light_judgment_charge_seen", chargeSeen);
        assertions.True("light_judgment_ring_seen", ringSeen);
        assertions.True("light_judgment_damage_seen", damageSeen);
        assertions.Equal("light_ring_begins_at_native_damage", 0f, ringAtDamage);
        assertions.Equal("light_judgment_resolution_damage", expectedDamage, actualDamage);
        await CombatScenarioContext.WaitUntilAsync(
            () => sun.GetShaderParameter("judgment_release").AsSingle() < 0, "Light ring finished");

        // Exit during an HP transition; retaining materials catches leaked callbacks.
        light.SetCurrentHpInternal(threshold);
        await context.EnterWeakCrawlerCombatAsync();
        assertions.True("light_painting_freed", !GodotObject.IsInstanceValid(painting));
        assertions.True("light_absent_next_room", NCombatRoom.Instance!.FindChild("LightEternalDayVisuals", true, false) is null);
        var stopped = Progress();
        var stoppedTime = sun.GetShaderParameter("ambient_time").AsSingle();
        light.SetCurrentHpInternal(light.MaxHp);
        await NGame.Instance.ToSignal(NGame.Instance.GetTree().CreateTimer(.9), SceneTreeTimer.SignalName.Timeout);
        assertions.Equal("light_no_hp_callback_after_exit", stopped, Progress());
        assertions.Equal("light_ambient_stops_on_exit", stoppedTime, sun.GetShaderParameter("ambient_time").AsSingle());
        assertions.Equal("light_no_ring_after_exit", -1f, sun.GetShaderParameter("judgment_release").AsSingle());
        // Save fixtures need a real visited map coordinate; debug-only rooms return to the map on load.
        await RunManager.Instance.EnterAct(FourthActEntryRegistration.FourthActSlotIndex, doTransition: false);
        var map = (SakuraFourthActMap)context.Run.Map;
        var routeIndex = map.Routes.ToList().FindIndex(route => route.Endpoint.EncounterType == typeof(LightEncounter));
        assertions.True("light_save_route_available", routeIndex >= 0);
        assertions.True("light_save_route_fixture", context.Run.AddVisitedMapCoord(new MapCoord(routeIndex * 2, 4)));
        await RunManager.Instance.EnterMapCoord(map.BossMapPoint.coord);
        await CombatScenarioContext.WaitUntilAsync(() => CombatManager.Instance.IsInProgress
            && context.Player.PlayerCombatState?.Phase == PlayerTurnPhase.Play, "Light endpoint on native map");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        var freshCombat = CombatManager.Instance.DebugOnlyGetState()!;
        var fresh = (TextureRect)NCombatRoom.Instance!.FindChild("LightEternalDayPainting", true, false);
        assertions.Equal("light_reentry_normal", 0f, ((ShaderMaterial)fresh.Material).GetShaderParameter("empowered").AsSingle());
        assertions.True("light_material_isolated", fresh.Material.GetInstanceId() != plate.GetInstanceId());
        var freshLight = freshCombat.Enemies.Single(c => c.Monster is LightMonster);
        var snapshot = new LightSceneSnapshot(freshCombat.Encounter!.Id.ToString(), freshLight.MaxHp,
            context.Player.PlayerCombatState!.Hand.Cards.Count);
        freshLight.SetCurrentHpInternal((int)(freshLight.MaxHp * .5m));
        await CombatScenarioContext.WaitUntilAsync(() =>
            Mathf.IsEqualApprox(((ShaderMaterial)fresh.Material).GetShaderParameter("empowered").AsSingle(), 1f),
            "Light empowered before save");
        SakuraTestProtocol.WriteAtomic(request.PriorSnapshotPath, snapshot);
        await SaveManager.Instance.SaveRun(preFinishedRoom: null, saveProgress: false);
        assertions.True("light_save_readable", SaveManager.Instance.LoadRunSave().Success);
        RuntimeTestHost.WriteCheckpoint(request, "light_scene_verified", "Native Light HP transitions, Judgment damage timing, exit and reentry passed.");
        return new(StringComparer.Ordinal)
        {
            ["light_scene"] = new { threshold, chargeSeen, ringSeen, ringAtDamage, actualDamage,
                setup = "Direct native HP events and extra player HP", native_three_turns = true, isolated_reentry = true }
        };
    }

    private static async Task<Dictionary<string, object?>> ReadAsync(SakuraTestRequest request, RuntimeAssertionCollector assertions)
    {
        var expected = SakuraTestProtocol.Read<LightSceneSnapshot>(request.PriorSnapshotPath!);
        var read = SaveManager.Instance.LoadRunSave();
        assertions.True("light_load_success", read.Success);
        var save = read.SaveData ?? throw new InvalidDataException("Missing saved Light combat.");
        var run = RunState.FromSerializable(save);
        await RunManager.Instance.SetUpSavedSingleplayer(run, save);
        var game = NGame.Instance!;
        game.ReactionContainer.InitializeNetworking(new NetSingleplayerGameService());
        await game.LoadRun(run, save.PreFinishedRoom);
        RunManager.Instance.CombatReplayWriter.IsEnabled = false;
        await CombatScenarioContext.WaitUntilAsync(() => CombatManager.Instance.IsInProgress
            && run.Players.Single().PlayerCombatState?.Phase == PlayerTurnPhase.Play, "loaded Light opening hand");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        var combat = CombatManager.Instance.DebugOnlyGetState()!;
        var light = combat.Enemies.Single(c => c.Monster is LightMonster);
        var painting = (TextureRect)NCombatRoom.Instance!.FindChild("LightEternalDayPainting", true, false);
        assertions.Equal("light_loaded_encounter", expected.EncounterId, combat.Encounter!.Id.ToString());
        assertions.Equal("light_loaded_opening_hand", expected.OpeningHand, run.Players.Single().PlayerCombatState!.Hand.Cards.Count);
        // Native combat saves restart enemies; presentation reconstructs from that live HP.
        assertions.Equal("light_loaded_hp_reinitialized", expected.MaxHp, light.CurrentHp);
        assertions.Equal("light_loaded_state_from_live_hp", 0f, ((ShaderMaterial)painting.Material).GetShaderParameter("empowered").AsSingle());
        assertions.Equal("light_loaded_no_transient_ring", -1f,
            ((ShaderMaterial)painting.GetNode<TextureRect>("Sun").Material).GetShaderParameter("judgment_release").AsSingle());
        return new(StringComparer.Ordinal) { ["restored"] = expected, ["lighting_from_live_hp"] = true };
    }
}

internal sealed record LightSceneSnapshot(string EncounterId, int MaxHp, int OpeningHand);
