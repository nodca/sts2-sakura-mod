using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.Relics;
using SakuraMod.TestProtocol;
using SharpEnchantment = MegaCrit.Sts2.Core.Models.Enchantments.Sharp;

namespace SakuraMod.RuntimeTests;

internal static class SakuraAncientCardsScenario
{
    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions)
    {
        var context = await CombatScenarioContext.StartAsync(request);
        var player = context.Player;
        var starterSeal = player.Deck.Cards.OfType<SpellSeal>().Single();
        starterSeal.UpgradeInternal();
        CardCmd.Enchant(ModelDb.Enchantment<SharpEnchantment>().ToMutable(), starterSeal, 2m);

        var tooth = (ArchaicTooth)ModelDb.Relic<ArchaicTooth>().ToMutable();
        assertions.True("archaic_tooth_setup_accepts_spell_seal", tooth.SetupForPlayer(player));
        await RelicCmd.Obtain(tooth, player);
        var growingMagic = player.Deck.Cards.OfType<GrowingMagic>().Single();
        assertions.Equal("archaic_tooth_removes_spell_seal", 0, player.Deck.Cards.OfType<SpellSeal>().Count());
        assertions.Equal("archaic_tooth_preserves_upgrade", 1, growingMagic.CurrentUpgradeLevel);
        assertions.True(
            "archaic_tooth_preserves_enchantment",
            growingMagic.Enchantment is SharpEnchantment { Amount: 2 });
        assertions.True(
            "archaic_tooth_registers_transcendence_target",
            ArchaicTooth.TranscendenceCards.Any(card => card is GrowingMagic));

        var tome = (DustyTome)ModelDb.Relic<DustyTome>().ToMutable();
        tome.SetupForPlayer(player);
        assertions.Equal("dusty_tome_prefers_another_me", ModelDb.Card<AnotherMe>().Id, tome.AncientCard);
        await RelicCmd.Obtain(tome, player);
        var deckAnotherMe = player.Deck.Cards.OfType<AnotherMe>().Single();
        assertions.Equal("dusty_tome_upgrades_another_me", 1, deckAnotherMe.CurrentUpgradeLevel);
        assertions.Equal(
            "dusty_tome_upgraded_cost",
            1m,
            deckAnotherMe.EnergyCost.GetWithModifiers(CostModifiers.None));

        var combat = await context.EnterWeakSlimesCombatAsync();
        var playerCombat = player.PlayerCombatState
            ?? throw new InvalidOperationException("Player combat state is unavailable.");
        var clearHand = new RuntimeFixtureAction(
            player,
            async choiceContext =>
            {
                var hand = playerCombat.Hand.Cards.ToList();
                if (hand.Count > 0)
                    await CardCmd.Discard(choiceContext, hand);
            });
        await CombatScenarioContext.EnqueueAndWaitAsync(clearHand);

        var grantSpellTestEnergy = new RuntimeFixtureAction(
            player,
            _ => PlayerCmd.GainEnergy(10, player));
        await CombatScenarioContext.EnqueueAndWaitAsync(grantSpellTestEnergy);

        var spellSealTarget = combat.HittableEnemies.First();
        var combatSpellSeal = await CombatScenarioContext.AddGeneratedCardToHandAsync<SpellSeal>(combat, player);
        await CombatScenarioContext.PlayCardAsync(combatSpellSeal, spellSealTarget);
        assertions.True(
            "spell_seal_native_play_resolves",
            playerCombat.DiscardPile.Cards.Contains(combatSpellSeal));

        var releaseTarget = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowShield>(combat, player);
        var combatSpellRelease = await CombatScenarioContext.AddGeneratedCardToHandAsync<SpellRelease>(combat, player);
        var releaseSelector = new TestCardSelector();
        releaseSelector.PrepareToSelect([0]);
        using (CardSelectCmd.UseSelector(releaseSelector))
        {
            await CombatScenarioContext.PlayCardAsync(combatSpellRelease);
        }
        assertions.True("spell_release_applies_release", SakuraReleaseState.IsReleased(releaseTarget));
        assertions.Equal(
            "spell_release_sets_target_cost_to_zero",
            0m,
            releaseTarget.EnergyCost.GetWithModifiers(CostModifiers.Local));
        assertions.Equal("spell_release_selector_released", null, CardSelectCmd.Selector);

        await SetMagicAsync(player, 0);
        var anotherMe = await CombatScenarioContext.AddGeneratedCardToHandAsync<AnotherMe>(combat, player);
        await CombatScenarioContext.PlayCardAsync(anotherMe);
        var anotherMePower = player.Creature.GetPower<AnotherMePower>();
        assertions.True("another_me_applies_power", anotherMePower is not null);
        assertions.Equal("another_me_power_refund_amount", 5, anotherMePower?.Amount ?? 0);
        assertions.Equal(
            "another_me_grants_initial_magic",
            5,
            player.Creature.GetPower<ClassicMagicChargePower>()?.Amount ?? 0);

        await SetMagicAsync(player, 10);
        var firstSource = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowFlower>(combat, player);
        firstSource.UpgradeInternal();
        await CombatScenarioContext.PlayCardAsync(firstSource);
        assertions.Equal(
            "another_me_first_paid_extra_refunds_five",
            6,
            player.Creature.GetPower<ClassicMagicChargePower>()?.Amount ?? 0);

        await SetMagicAsync(player, 10);
        var secondSource = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowFlower>(combat, player);
        await CombatScenarioContext.PlayCardAsync(secondSource);
        assertions.Equal(
            "another_me_second_paid_extra_same_turn_also_refunds",
            6,
            player.Creature.GetPower<ClassicMagicChargePower>()?.Amount ?? 0);

        var grantFreeExtra = new RuntimeFixtureAction(
            player,
            async choiceContext =>
            {
                await PowerCmd.Apply<ClassicLockSakuraPower>(
                    choiceContext,
                    player.Creature,
                    1,
                    player.Creature,
                    null,
                    false);
            });
        await CombatScenarioContext.EnqueueAndWaitAsync(grantFreeExtra);
        await SetMagicAsync(player, 10);
        var freeSource = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowFlower>(combat, player);
        await CombatScenarioContext.PlayCardAsync(freeSource);
        assertions.Equal(
            "another_me_does_not_refund_sakura_lock_activation",
            11,
            player.Creature.GetPower<ClassicMagicChargePower>()?.Amount ?? 0);
        assertions.True(
            "another_me_sakura_lock_payment_is_consumed",
            player.Creature.GetPower<ClassicLockSakuraPower>() is null);

        await SetMagicAsync(player, 0);
        assertions.True("growing_magic_combat_has_surviving_enemy", combat.HittableEnemies.Count() > 1);
        var wandChargeBeforeGrowingMagic = player.GetRelic<ClassicSealedWandRelic>()?.ChargeAmount ?? -1;
        var target = combat.HittableEnemies.First();
        assertions.Equal("growing_magic_target_is_non_minion", false, SakuraEnemyRules.IsMinion(target));
        var prepareLethal = new RuntimeFixtureAction(
            player,
            async choiceContext =>
            {
                await CreatureCmd.Damage(
                    choiceContext,
                    target,
                    Math.Max(0, target.CurrentHp - 1),
                    ValueProp.Unblockable | ValueProp.Unpowered,
                    null,
                    null);
            });
        await CombatScenarioContext.EnqueueAndWaitAsync(prepareLethal);
        var combatGrowingMagic = await CombatScenarioContext.AddGeneratedCardToHandAsync<GrowingMagic>(combat, player);
        await CombatScenarioContext.PlayCardAsync(combatGrowingMagic, target);
        assertions.Equal(
            "growing_magic_lethal_magic",
            5,
            player.Creature.GetPower<ClassicMagicChargePower>()?.Amount ?? 0);
        assertions.Equal(
            "growing_magic_sealed_wand_charge",
            wandChargeBeforeGrowingMagic + 5,
            player.GetRelic<ClassicSealedWandRelic>()?.ChargeAmount ?? -1);

        var sealedWand = player.Relics
            .OfType<ClassicSealedWandRelic>()
            .Single(relic => relic.GetType() == typeof(ClassicSealedWandRelic));
        var sealedWandCharge = sealedWand.ChargeAmount;
        var orobas = (TouchOfOrobas)ModelDb.Relic<TouchOfOrobas>().ToMutable();
        assertions.Equal(
            "touch_of_orobas_ritsulib_mapping",
            ModelDb.Relic<ClassicStarWandRelic>().Id,
            orobas.GetUpgradedStarterRelic(sealedWand).Id);
        assertions.True("touch_of_orobas_selects_sealed_wand", orobas.SetupForPlayer(player));
        assertions.Equal("touch_of_orobas_starter", sealedWand.Id, orobas.StarterRelic);
        assertions.Equal(
            "touch_of_orobas_upgrade",
            ModelDb.Relic<ClassicStarWandRelic>().Id,
            orobas.UpgradedRelic);
        await RelicCmd.Obtain(orobas, player);
        var starWand = player.Relics
            .OfType<ClassicStarWandRelic>()
            .SingleOrDefault(relic => relic.GetType() == typeof(ClassicStarWandRelic));
        assertions.True("touch_of_orobas_replaces_sealed_wand", starWand is not null);
        assertions.True(
            "touch_of_orobas_removes_exact_sealed_wand",
            player.Relics.All(relic => relic.GetType() != typeof(ClassicSealedWandRelic)));
        assertions.Equal(
            "touch_of_orobas_preserves_charge",
            sealedWandCharge,
            starWand?.ChargeAmount ?? -1);

        RuntimeTestHost.WriteCheckpoint(
            request,
            "sakura_ancient_cards_verified",
            "RitsuLib relic mappings and Sakura Ancient card effects crossed native run/combat boundaries.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["archaic_tooth"] = new
            {
                transformed = growingMagic.Id.ToString(),
                growingMagic.CurrentUpgradeLevel,
                enchantment = growingMagic.Enchantment?.Id.ToString()
            },
            ["dusty_tome"] = new
            {
                selected = tome.AncientCard?.ToString(),
                deckAnotherMe.CurrentUpgradeLevel
            },
            ["touch_of_orobas"] = new
            {
                starter = orobas.StarterRelic?.ToString(),
                upgraded = orobas.UpgradedRelic?.ToString(),
                preserved_charge = starWand?.ChargeAmount
            },
            ["combat"] = new
            {
                sealed_wand_charge = player.GetRelic<ClassicSealedWandRelic>()?.ChargeAmount,
                another_me_refund = anotherMePower?.Amount,
                magic_after_growing_magic = player.Creature.GetPower<ClassicMagicChargePower>()?.Amount
            }
        };
    }

    private static async Task SetMagicAsync(MegaCrit.Sts2.Core.Entities.Players.Player player, int amount)
    {
        var action = new RuntimeFixtureAction(
            player,
            async choiceContext =>
            {
                await SakuraMagicCharge.SpendAllMagic(choiceContext, player);
                await SakuraMagicCharge.GainMagic(choiceContext, player, amount);
            });
        await CombatScenarioContext.EnqueueAndWaitAsync(action);
    }
}
