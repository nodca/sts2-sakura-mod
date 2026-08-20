using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal static class LabyrinthAttackImmunityScenario
{
    private const int FixtureMagicCharge = 10;

    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions)
    {
        var context = await CombatScenarioContext.StartAsync(request);
        var combat = await context.EnterWeakCrawlerCombatAsync();
        var player = context.Player;
        var target = combat.Enemies.First(static enemy => enemy.IsAlive);

        var fixtureAction = new RuntimeFixtureAction(
            player,
            async choiceContext =>
            {
                var power = await PowerCmd.Apply<LabyrinthPower>(
                    choiceContext,
                    player.Creature,
                    1,
                    player.Creature,
                    null,
                    silent: true);
                if (power is null)
                    throw new InvalidOperationException("LabyrinthPower was not applied.");
                await power.Enter([target]);
                await PowerCmd.Apply<ClassicMagicChargePower>(
                    choiceContext,
                    player.Creature,
                    FixtureMagicCharge,
                    player.Creature,
                    null,
                    silent: true);
            });
        await CombatScenarioContext.EnqueueAndWaitAsync(fixtureAction);

        var sword = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowSword>(combat, player);
        assertions.Equal("clow_sword_is_attack", CardType.Attack, sword.Type);
        var hpBefore = target.CurrentHp;
        var extraEffectTriggersBefore = SakuraActions.ExtraEffectTriggerCountThisTurn(player);
        await CombatScenarioContext.PlayCardAsync(sword, target);
        var hpAfter = target.CurrentHp;
        var extraEffectTriggersAfter = SakuraActions.ExtraEffectTriggerCountThisTurn(player);

        assertions.Equal(
            "clow_sword_extra_effect_triggered",
            1,
            extraEffectTriggersAfter - extraEffectTriggersBefore);
        assertions.Equal("labyrinth_blocks_clow_sword_damage", hpBefore, hpAfter);

        var swing = await CombatScenarioContext.AddGeneratedCardToHandAsync<Swing>(combat, player);
        var hpBeforeSwing = target.CurrentHp;
        await CombatScenarioContext.PlayCardAsync(swing);
        var hpAfterSwing = target.CurrentHp;
        assertions.Equal("labyrinth_blocks_swing_damage", hpBeforeSwing, hpAfterSwing);
        assertions.Equal("labyrinth_blocks_swing_weak", null, target.GetPower<WeakPower>());
        assertions.Equal("swing_damage_window", 2, player.Creature.GetPower<SwingDamageWindowPower>()?.Amount ?? 0);
        RuntimeTestHost.WriteCheckpoint(
            request,
            "labyrinth_attack_immunity_verified",
            "Clow Sword and Swing resolved through PlayCardAction without affecting the enemy in Labyrinth.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["fixture"] = new
            {
                seed = request.Seed.ToString("X16"),
                card = typeof(ClowSword).FullName,
                target = target.Monster?.Id.Entry,
                magic_charge = FixtureMagicCharge
            },
            ["before"] = new
            {
                target_hp = hpBefore,
                extra_effect_triggers = extraEffectTriggersBefore
            },
            ["after_sword"] = new
            {
                target_hp = hpAfter,
                extra_effect_triggers = extraEffectTriggersAfter
            },
            ["after_swing"] = new
            {
                target_hp = hpAfterSwing,
                weak = target.GetPower<WeakPower>()?.Amount,
                swing_window = player.Creature.GetPower<SwingDamageWindowPower>()?.Amount
            }
        };
    }
}
