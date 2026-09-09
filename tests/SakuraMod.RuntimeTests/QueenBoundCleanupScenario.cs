using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Afflictions;
using MegaCrit.Sts2.Core.Models.Powers;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal static class QueenBoundCleanupScenario
{
    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions)
    {
        var context = await CombatScenarioContext.StartAsync(request);
        var combat = await context.EnterWeakSlimesCombatAsync();
        var player = context.Player;
        var first = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowShield>(combat, player);
        var second = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowFlower>(combat, player);
        var clear = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowThrough>(combat, player);
        var jump = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowJump>(combat, player);
        var drawBound = combat.CreateCard<ClowShield>(player);
        var exhaustBound = combat.CreateCard<ClowShield>(player);
        var otherAffliction = combat.CreateCard<ClowShield>(player);
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choice =>
        {
            await PlayerCmd.GainEnergy(20, player);
            await PowerCmd.Apply<WeakPower>(choice, player.Creature, 2, combat.Enemies.First(), null);
            await PowerCmd.Apply<ChainsOfBindingPower>(choice, player.Creature, 3, combat.Enemies.First(), null);
            await CardCmd.Afflict<Bound>(first, 3);
            await CardCmd.Afflict<Bound>(second, 3);
            await CardCmd.Afflict<Bound>(clear, 3);
            await CardPileCmd.Add(drawBound, PileType.Draw);
            await CardPileCmd.Add(exhaustBound, PileType.Exhaust);
            await CardPileCmd.Add(otherAffliction, PileType.Exhaust);
            await CardCmd.Afflict<Bound>(drawBound, 3);
            await CardCmd.Afflict<Bound>(exhaustBound, 3);
            await CardCmd.Afflict<Tainted>(otherAffliction, 1);
        }));
        assertions.True("bound_first_initially_playable", first.CanPlay());
        assertions.True("bound_second_initially_playable", second.CanPlay());
        await CombatScenarioContext.PlayCardAsync(first);
        assertions.True("bound_native_limit_blocks_second", !second.CanPlay());
        assertions.True("bound_native_limit_blocks_third", !clear.CanPlay());
        assertions.True("bound_does_not_block_unafflicted_jump", jump.CanPlay());

        await CombatScenarioContext.PlayCardAsync(jump);
        assertions.True("jump_selects_whitelisted_weak", !player.Creature.HasPower<WeakPower>());
        assertions.True("jump_preserves_chains_power", player.Creature.HasPower<ChainsOfBindingPower>());
        assertions.True("jump_preserves_bound_in_discard", first.Affliction is Bound);
        assertions.True("jump_preserves_bound_in_hand", second.Affliction is Bound && clear.Affliction is Bound);
        assertions.True("jump_preserves_bound_in_draw_and_exhaust", drawBound.Affliction is Bound && exhaustBound.Affliction is Bound);
        assertions.True("jump_preserves_unrelated_affliction", otherAffliction.Affliction is Tainted);
        assertions.True("jump_does_not_release_second_card", !second.CanPlay());
        assertions.True("jump_does_not_release_third_card", !clear.CanPlay());
        await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
        assertions.True("no_orphan_bound_next_turn",
            exhaustBound.Affliction is null && first.Affliction is null);

        await VerifyRemoval<Blank>("blank", beforePlay: true, turns: 0, magic: 20);
        await VerifyRemoval<ClowJump>("activated_jump", beforePlay: true, turns: 0, magic: 20);
        await VerifyRemoval<SakuraJump>("sakura_jump", beforePlay: false, turns: 1);
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player,
            _ => PowerCmd.Remove(player.Creature.GetPower<ClassicJumpPower>())));
        await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player,
            _ => SakuraActions.RemovePowerWithCardCleanup(player.Creature.GetPower<ChainsOfBindingPower>()!)));
        await VerifyRemoval<ClowReturn>("return", beforePlay: false, turns: 2);

        // With no cleansing effect, vanilla still owns the normal turn-end cleanup.
        await ApplyBinding();
        await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
        assertions.True("native_turn_end_clears_exhaust_bound", exhaustBound.Affliction is null);
        assertions.True("native_turn_end_keeps_chains_power", player.Creature.HasPower<ChainsOfBindingPower>());

        return new Dictionary<string, object?>
        {
            ["fixture"] = "Native ChainsOfBindingPower(3), Bound on three cards; native PlayCardAction for Shield, Jump, Flower, Through; native turn boundary.",
            ["bound_after_jump"] = new CardModel[] { first, second, clear }.Count(card => card.Affliction is Bound)
        };

        async Task ApplyBinding()
        {
            await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choice =>
            {
                if (!player.Creature.HasPower<ChainsOfBindingPower>())
                    await PowerCmd.Apply<ChainsOfBindingPower>(choice, player.Creature, 3, combat.Enemies.First(), null);
                await CardCmd.Afflict<Bound>(exhaustBound, 3);
            }));
        }

        async Task VerifyRemoval<T>(string name, bool beforePlay, int turns, int magic = 0) where T : CardModel
        {
            await CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, async choice =>
            {
                await PlayerCmd.GainEnergy(20, player);
                await CreatureCmd.GainMaxHp(player.Creature, 50);
                await PowerCmd.Remove(player.Creature.GetPower<ClassicMagicChargePower>());
                if (magic > 0)
                    await PowerCmd.Apply<ClassicMagicChargePower>(choice, player.Creature, magic, player.Creature, null);
            }));
            var card = await CombatScenarioContext.AddGeneratedCardToHandAsync<T>(combat, player);
            if (beforePlay)
                await ApplyBinding();
            await CombatScenarioContext.PlayCardAsync(card);
            if (!beforePlay)
                await ApplyBinding();
            for (var turn = 0; turn < turns; turn++)
                await CombatScenarioContext.EndTurnAndWaitForNextPlayAsync(player);
            var isReturn = typeof(T) == typeof(ClowReturn);
            assertions.Equal($"{name}_binding_power_presence", !isReturn, player.Creature.HasPower<ChainsOfBindingPower>());
            if (turns == 0)
                assertions.True($"{name}_preserves_exhaust_bound", exhaustBound.Affliction is Bound);
            else
                assertions.True($"{name}_exhaust_bound_cleared", exhaustBound.Affliction is null);
            assertions.True($"{name}_preserved_other_affliction", otherAffliction.Affliction is Tainted);
        }
    }
}
