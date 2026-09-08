using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using SakuraMod.SakuraModCode.FourthAct.Dark;
using SakuraMod.SakuraModCode.FourthAct.Dark.Cards;
using SakuraMod.SakuraModCode.FourthAct.Dark.Models;
using SakuraMod.SakuraModCode.FourthAct.Dark.Powers;
using SakuraMod.SakuraModCode.FourthAct.Visuals;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal static class DarkEndpointScenario
{
    public static async Task<Dictionary<string, object?>> ExecuteAsync(SakuraTestRequest request, RuntimeAssertionCollector assertions)
    {
        assertions.Equal("dark_initial_darkness", 1, DarkEnemyRules.ClampDarkness(1));
        assertions.Equal("dark_maximum_darkness", 5, DarkEnemyRules.DarknessMaximum);
        assertions.Equal("dark_reset_darkness", 3, DarkEnemyRules.DarknessReset);
        assertions.Equal("dark_micro_lights_per_draw", 2, DarkEnemyRules.MicroLightsPerDraw);
        assertions.True("darkness_power_registered", typeof(DarknessPower) is not null);
        assertions.True("dark_monster_registered", typeof(DarkMonster) is not null);
        var context = await CombatScenarioContext.StartAsync(request);
        var combat = await context.EnterDarkCombatAsync();
        var dark = combat.Enemies.Single(c => c.Monster is DarkMonster);
        await CombatScenarioContext.WaitUntilAsync(
            () => NCombatRoom.Instance?.FindChild("ShrinePetals", true, false) is not null,
            "dark shrine presentation mount");
        var painting = (TextureRect)NCombatRoom.Instance!.FindChild(
            FourthActCombatBackgrounds.EternalNightOverlayNodeName, true, false);
        var material = (ShaderMaterial)painting.Material;
        float Progress() => material.GetShaderParameter(FourthActCombatBackgrounds.EternalNightProgressParameterName).AsSingle();
        assertions.Equal("dark_shrine_shader", FourthActCombatBackgrounds.EclipseShaderPath, material.Shader.ResourcePath);
        assertions.True("dark_shrine_opening_hand", context.Player.PlayerCombatState!.Hand.Cards.Count >= 5);
        await WaitForProgress(1);
        var power = dark.GetPower<DarknessPower>()!;
        var choice = new ThrowingPlayerChoiceContext();
        await PowerCmd.ModifyAmount(choice, power, 4, dark, null, true);
        await WaitForProgress(5);
        assertions.True("dark_five_keeps_painting", painting.Visible);

        var microLight = context.Player.PlayerCombatState.Hand.Cards.OfType<MicroLight>().FirstOrDefault()
            ?? await CombatScenarioContext.AddGeneratedCardToHandAsync<MicroLight>(combat, context.Player);
        await CombatScenarioContext.PlayCardAsync(microLight);
        assertions.Equal("dark_micro_light_lowers_power", 4, power.Amount);
        await WaitForProgress(4);
        await PowerCmd.ModifyAmount(choice, power, 1, dark, null, true);
        await WaitForProgress(5);
        await PowerCmd.ModifyAmount(choice, power, -2, dark, null, true);
        await WaitForProgress(3);
        assertions.Equal("dark_five_to_three", 3f, Progress());

        await PowerCmd.ModifyAmount(choice, power, 2, dark, null, true);
        await NGame.Instance!.ToSignal(NGame.Instance.GetTree().CreateTimer(.15), SceneTreeTimer.SignalName.Timeout);
        await PowerCmd.ModifyAmount(choice, power, -3, dark, null, true);
        await WaitForProgress(2);
        assertions.Equal("dark_interrupted_transition_restores", 2f, Progress());
        await context.EnterWeakCrawlerCombatAsync();
        assertions.True("dark_shrine_freed_on_exit", !GodotObject.IsInstanceValid(painting));
        assertions.True("dark_shrine_no_next_room_particles", NCombatRoom.Instance!.FindChild("ShrinePetals", true, false) is null);
        RuntimeTestHost.WriteCheckpoint(request, "dark_endpoint_verified", "Native Dark combat, micro-light action, reversible lighting and teardown passed.");
        return new(StringComparer.Ordinal)
        {
            ["resolution"] = new { initial = 1, maximum = 5, reset = 3, interrupted = 2, native_micro_light = true }
        };

        Task WaitForProgress(float expected) => CombatScenarioContext.WaitUntilAsync(
            () => Mathf.IsEqualApprox(Progress(), expected), "dark lighting progress " + expected);
    }
}
