using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using NimbleEnchantment = MegaCrit.Sts2.Core.Models.Enchantments.Nimble;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.Relics;
using SakuraMod.SakuraModCode;
using STS2RitsuLib.RunData;

public sealed class CardMechanicsSuite
{
    [Fact]
    public void BurningSticksExcludesSpellTurnOnly()
    {
        RegressionTestHarness.Require(
            BurningSticksSpellTurnPatch.ShouldSkip(new SpellTurn())
            && !BurningSticksSpellTurnPatch.ShouldSkip(new ClowFirey()),
            "Expected Burning Sticks to skip only Spell Turn and preserve its normal Skill-card behavior for other cards.");

        var source = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Relics/BurningSticksSpellTurnPatch.cs"));
        RegressionTestHarness.Require(
            source.Contains("BurningSticks.AfterCardExhausted", StringComparison.Ordinal)
            && source.Contains("[HarmonyPrefix]", StringComparison.Ordinal),
            "Expected the Spell Turn exclusion to patch the native Burning Sticks exhaust hook.");
    }

    [Fact]
    public void SpellTurnLeavesCombatInsteadOfDiscardWhenPlayed()
    {
        RegressionTestHarness.Require(
            !new SpellTurn().Keywords.Contains(CardKeyword.Exhaust),
            "Expected Spell Turn to no longer advertise Exhaust.");

        RegressionTestHarness.Require(
            SakuraSourceCardText.KeywordTips(new SpellTurn()).Contains(SakuraKeywords.Purge),
            "Expected Spell Turn to source its Purge hover tip from the card text capability.");

        var englishCards = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/localization/eng/cards.json"));
        var chineseCards = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/localization/zhs/cards.json"));
        RegressionTestHarness.Require(
            chineseCards.Contains("[gold]符咒[/gold] [gold]移除[/gold]", StringComparison.Ordinal)
            && englishCards.Contains("[gold]Spell[/gold] [gold]Purge[/gold]", StringComparison.Ordinal),
            "Expected both Spell Turn descriptions to spell out the Purge keyword span like Clow Create.");

        RegressionTestHarness.Require(
            SpellTurn.ResultPileFor(PileType.Discard) == PileType.None
            && SpellTurn.ResultPileFor(PileType.Exhaust) == PileType.Exhaust,
            "Expected a played Spell Turn to leave the combat while still respecting forced exhaust.");
    }

    [Fact]
    public void SpellTurnPreservesCompatibleEnchantmentDuringTransformation()
    {
        var spellTurnSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Spells/SpellTurn.cs"));
        var transferSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/SakuraCardEnchantmentTransfer.cs"));
        RegressionTestHarness.Require(
            spellTurnSource.Contains("ResolveEnchantmentSource(selected, deckCard)", StringComparison.Ordinal)
            && spellTurnSource.Contains("SakuraCardEnchantmentTransfer.TransferEnchantment(enchantmentSource, deckReplacement)", StringComparison.Ordinal)
            && spellTurnSource.Contains("SakuraCardEnchantmentTransfer.TransferEnchantment(enchantmentSource, handCard)", StringComparison.Ordinal)
            && spellTurnSource.Contains("handCard.DeckVersion = transformedDeckCard", StringComparison.Ordinal)
            && !spellTurnSource.Contains("CardCmd.Enchant(", StringComparison.Ordinal)
            && !spellTurnSource.Contains("CardCmd.Upgrade(", StringComparison.Ordinal),
            "Expected Spell Turn to delegate enchantment transfer to the shared helper.");
        RegressionTestHarness.Require(
            transferSource.Contains("target.EnchantInternal(enchantmentCopy, enchantmentCopy.Amount)", StringComparison.Ordinal)
            && transferSource.Contains("enchantmentCopy.ModifyCard()", StringComparison.Ordinal)
            && transferSource.Contains("target.FinalizeUpgradeInternal()", StringComparison.Ordinal)
            && !transferSource.Contains("CanEnchant(", StringComparison.Ordinal)
            && !transferSource.Contains("CardCmd.Enchant(", StringComparison.Ordinal),
            "Expected enchantment transfer to copy existing state through EnchantInternal only.");
    }

    [Fact]
    public void EmptySpellCommunicatesThroughNativeKeywordsOnly()
    {
        RegressionTestHarness.Require(
            new SpellEmptySpell().CanonicalKeywords.SequenceEqual(
                [CardKeyword.Ethereal, CardKeyword.Exhaust]),
            "Expected Empty Spell to rely on its native Ethereal and Exhaust keywords instead of an invisible Purge marker.");
    }

    [Fact]
    public void GaleDescriptionCountsDownToItsNextDraw()
    {
        var gale = new Gale();
        var englishCards = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/localization/eng/cards.json"));
        var chineseCards = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/localization/zhs/cards.json"));

        RegressionTestHarness.Require(
            Enumerable.Range(0, 8)
                .Select(GaleRules.PlaysUntilNextDraw)
                .SequenceEqual([3, 2, 1, 3, 2, 1, 3, 2]),
            "Expected Gale to display the number of Gale plays remaining before its next two-card draw.");
        RegressionTestHarness.Require(
            englishCards.Contains("{PlaysUntilDraw:diff()}", StringComparison.Ordinal)
            && chineseCards.Contains("{PlaysUntilDraw:diff()}", StringComparison.Ordinal),
            "Expected both Gale descriptions to render the live plays-until-draw value.");
    }

    [Fact]
    public void RecordUsesBoundedNextTurnCardCopies()
    {
        var record = new SakuraMod.SakuraModCode.Cards.Record();
        var upgraded = RegressionTestHarness.MutableForCostTest(new SakuraMod.SakuraModCode.Cards.Record());
        upgraded.UpgradeInternal();
        var powerSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Powers/Transparent/RecordPower.cs"));
        var chineseCards = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/localization/zhs/cards.json"));
        var englishCards = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/localization/eng/cards.json"));

        var checks = new[]
        {
            record.EnergyCost.Canonical == 2,
            upgraded.EnergyCost.GetWithModifiers(CostModifiers.Local) == 1,
            record.Type == CardType.Skill,
            record.Rarity == CardRarity.Rare,
            record.CanonicalKeywords.SequenceEqual([SakuraKeywords.Water, CardKeyword.Exhaust]),
            new RecordPower().InstanceType == PowerInstanceType.Instanced,
            RegressionTestHarness.DeclaresMethod<RecordPower>("BeforeHandDraw"),
            RegressionTestHarness.DeclaresMethod<RecordPower>("AfterCardPlayed"),
            powerSource.Contains("MaxRecordedCards", StringComparison.Ordinal),
            powerSource.Contains("play.IsLastInSeries", StringComparison.Ordinal),
            powerSource.Contains("AddTemporaryGeneratedCardToHand", StringComparison.Ordinal),
            !powerSource.Contains("AddRememberedCardToHand", StringComparison.Ordinal),
            powerSource.Contains("freeThisTurn", StringComparison.Ordinal),
            chineseCards.Contains("至多 3 张牌", StringComparison.Ordinal),
            englishCards.Contains("Record up to 3 cards", StringComparison.Ordinal)
        };
        RegressionTestHarness.Require(checks.All(static check => check), "Expected Record to be a 2-cost Exhaust Skill that upgrades to 1 and materializes up to three Forgotten copies before the next hand draw.");
    }

    [Fact]
    public void SakuraEraseUsesTargetBasedPersistentHpLossContract()
    {
        var card = new SakuraErase();
        var power = new SakuraErasePower();

        RegressionTestHarness.Require(
            card.EnergyCost.Canonical == 1
            && card.Type == CardType.Skill
            && card.TargetType == TargetType.AnyEnemy
            && card.Elements == SakuraElementSet.Wind
            && card.CanonicalKeywords.SequenceEqual([CardKeyword.Innate, CardKeyword.Exhaust])
            && card.MaxUpgradeLevel == 0
            && card.DynamicVars["Percent"].IntValue == SakuraEraseRules.NormalHpLossPercent
            && card.DynamicVars["MaxHpLoss"].IntValue == SakuraEraseRules.EliteBossMaxHpLoss
            && SakuraThroughResolution.IsEligibleCard(card),
            "Expected Sakura Erase to be a 1-cost Innate Exhaust Skill that participates in target-scoped Through resolution.");

        RegressionTestHarness.Require(
            power.Type == PowerType.Debuff
            && power.StackType == PowerStackType.Single
            && RegressionTestHarness.DeclaresMethod<SakuraErasePower>("BeforeSideTurnEnd")
            && !RegressionTestHarness.DeclaresMethod<SakuraErasePower>("AfterSideTurnEnd"),
            "Expected Sakura Erase's non-stacking enemy debuff to deal HP loss before the enemy turn-end synchronization boundary.");

        RegressionTestHarness.Require(
            SakuraEraseRules.NormalHpLoss(100) == 33
            && SakuraEraseRules.NormalHpLoss(99) == 32
            && SakuraEraseRules.NormalHpLoss(3) == 1
            && SakuraEraseRules.NormalHpLoss(1) == 1
            && SakuraEraseRules.NormalHpLoss(0) == 0
            && SakuraEraseRules.NormalHpLoss(100, 0) == 0,
            "Expected Sakura Erase HP loss to floor 33% of Max HP with a minimum of 1 for positive values.");
    }

    [Fact]
    public void SakuraImmediateBlockCardsExposeNativeGainsBlockContract()
    {
        CardModel[] immediateBlockCards =
        [
            new ClowShield(),
            new SakuraShield(),
            new ClowMaze(),
            new SakuraMaze(),
            new ClowJump(),
            new ClowIllusion(),
            new ClowShadow(),
            new SakuraShadow(),
            new ClowCloud(),
            new SakuraCloud(),
            new ClowLibra(),
            new SakuraLibra(),
            new ClowFreeze(),
            new SakuraFreeze(),
            new SakuraSong(),
            new Spiral(),
            new Shade(),
            new Siege(),
            new Promise(),
            new Reflect(),
            new Flight()
        ];
        CardModel[] delayedOrNonBlockCards =
        [
            new ClowNothing(),
            new ClowVoice(),
            new Choice(),
            new Blank()
        ];
        var nimble = new NimbleEnchantment();

        RegressionTestHarness.Require(
            immediateBlockCards.All(static card => card.GainsBlock),
            "Expected every Sakura card that gains Block when played to expose the native GainsBlock contract.");
        RegressionTestHarness.Require(
            delayedOrNonBlockCards.All(static card => !card.GainsBlock),
            "Expected delayed and non-Block Sakura cards to remain outside the native immediate-Block contract.");
        RegressionTestHarness.Require(
            nimble.CanEnchant(new ClowShield())
            && !nimble.CanEnchant(new Choice()),
            "Expected Nimble to accept a Sakura Block Skill and reject a non-Block Skill.");
    }

    [Fact]
    public void SakuraAncientCardContractsRemainStable()
    {
        var growingMagic = new GrowingMagic();
        var upgradedGrowingMagic = RegressionTestHarness.MutableForCostTest(new GrowingMagic());
        upgradedGrowingMagic.UpgradeInternal();
        var anotherMe = new AnotherMe();
        var upgradedAnotherMe = RegressionTestHarness.MutableForCostTest(new AnotherMe());
        upgradedAnotherMe.UpgradeInternal();

        RegressionTestHarness.Require(
            growingMagic.Rarity == CardRarity.Ancient
            && growingMagic.Type == CardType.Attack
            && growingMagic.TargetType == TargetType.AnyEnemy
            && growingMagic.EnergyCost.Canonical == 1
            && growingMagic.CanonicalKeywords.SequenceEqual([CardKeyword.Retain])
            && growingMagic.DynamicVars.Damage.IntValue == 18
            && upgradedGrowingMagic.DynamicVars.Damage.IntValue == 24
            && !growingMagic.CanBeGeneratedInCombat,
            "Expected Growing Magic to be a 1-cost 18/24 Ancient Retain Attack excluded from combat generation.");
        RegressionTestHarness.Require(
            anotherMe.Rarity == CardRarity.Ancient
            && anotherMe.Type == CardType.Power
            && anotherMe.TargetType == TargetType.None
            && anotherMe.EnergyCost.Canonical == 2
            && upgradedAnotherMe.EnergyCost.GetWithModifiers(CostModifiers.None) == 1
            && anotherMe.DynamicVars["Magic"].IntValue == AnotherMe.MagicChargeAmount
            && !anotherMe.CanBeGeneratedInCombat
            && new AnotherMePower().StackType == PowerStackType.Single,
            "Expected Another Me to be a 2-to-1-cost Ancient Power that grants and refunds 5 Magic Charge.");
        RegressionTestHarness.Require(
            SakuraSealKillPolicy.IsSealCard(new SpellSeal())
            && SakuraSealKillPolicy.IsSealCard(growingMagic)
            && !SakuraSealKillPolicy.IsSealCard(anotherMe),
            "Expected Spell Seal and Growing Magic to share one Seal-kill identity policy.");
        RegressionTestHarness.Require(
            AnotherMePower.ShouldRefund(ownsCard: true, spentMagicCharge: true)
            && !AnotherMePower.ShouldRefund(ownsCard: false, spentMagicCharge: true)
            && !AnotherMePower.ShouldRefund(ownsCard: true, spentMagicCharge: false)
            && SakuraSourceCardText.ReferencesMagicChargeTip(anotherMe)
            && SakuraSourceCardText.ReferencesMagicChargeTip(growingMagic),
            "Expected Another Me to refund every owned paid Extra Effect without refunding free activations.");
    }

    [Fact]
    public void ThroughCardsExposePersistentPiercingContracts()
    {
        var clow = new ClowThrough();
        var upgradedClow = RegressionTestHarness.MutableForCostTest(new ClowThrough());
        upgradedClow.UpgradeInternal();
        var sakura = new SakuraThrough();

        RegressionTestHarness.Require(
            clow.EnergyCost.Canonical == 2
            && clow.Type == CardType.Power
            && clow.Rarity == CardRarity.Rare
            && clow.TargetType == TargetType.None
            && clow.Elements == SakuraElementSet.Earth
            && clow is not ISakuraExtraEffectCard
            && clow.DynamicVars["Rate"].IntValue == 50
            && upgradedClow.DynamicVars["Rate"].IntValue == 100
            && SakuraSourceCardText.ReferencesThroughTip(clow),
            "Expected Clow Through to be a 2-cost Rare Earth Power with a 50%/100% charge contribution and no Extra effect.");

        RegressionTestHarness.Require(
            sakura.EnergyCost.Canonical == 1
            && sakura.Type == CardType.Power
            && sakura.Rarity == CardRarity.Token
            && sakura.TargetType == TargetType.None
            && sakura.Elements == SakuraElementSet.Earth
            && sakura.MaxUpgradeLevel == 0
            && sakura.DynamicVars["Damage"].IntValue == 10
            && SakuraSourceCardText.ReferencesThroughTip(sakura),
            "Expected Sakura Through to be a non-upgradable 1-cost Earth Power with 10 bonus damage.");

        var power = new ClassicThroughPower();
        power.RegisterClowSource(upgraded: false);
        power.RegisterClowSource(upgraded: true);
        power.RegisterSakuraSource();
        RegressionTestHarness.Require(
            power.CalculateBonusDamage(5) == 17
            && power.CalculateBonusDamage(6) == 19,
            "Expected Through contributions to add, with base Clow charge halved using integer floor.");

        RegressionTestHarness.Require(
            SakuraThroughResolution.IsEligibleCard(new ClowSword())
            && SakuraThroughResolution.IsEligibleCard(new SakuraSword())
            && SakuraThroughResolution.IsEligibleCard(new Transfer())
            && SakuraThroughResolution.IsEligibleCard(new SpellHuoShen())
            && SakuraThroughResolution.IsEligibleCard(new GrowingMagic())
            && !SakuraThroughResolution.IsEligibleCard(new AnotherMe())
            && !SakuraThroughResolution.IsEligibleCard(new MegaCrit.Sts2.Core.Models.Cards.Neutralize()),
            "Expected Through to accept targeted Clow, Sakura, Clear, Spell, and Ancient cards while rejecting untargeted and original cards.");
    }

    [Fact]
    public void ThroughPropagationIgnoresImmutableCardSources()
    {
        var immutableCard = new ClowSword();

        RegressionTestHarness.Require(
            !immutableCard.IsMutable
            && SakuraThroughResolution.ExpandDamageTargets(
                Array.Empty<Creature>(),
                immutableCard) is null,
            "Expected scoped Through propagation to fail open without reading Owner from an immutable card source.");
    }

    [Fact]
    public void SakuraBigIncreasesAttackDamageBySixtySixPercent()
    {
        RegressionTestHarness.Require(
            ClassicBigPower.DamageMultiplier == 1.66m,
            "Expected Sakura Big to multiply outgoing Attack damage by 1.66.");
    }

    [Fact]
    public void SakuraFloatAddsBlockFromEveryPositiveBlockSource()
    {
        var card = new SakuraFloat();
        RegressionTestHarness.Require(
            card.CanonicalKeywords.Contains(CardKeyword.Innate)
            && card.DynamicVars["Magic"].IntValue == 2,
            "Expected Sakura Float to be Innate and grant 2 additional Block per Block event.");
        RegressionTestHarness.Require(
            ClassicFloatSakuraPower.ExtraBlock(ownsTarget: true, block: 5m, amount: 2) == 2m
            && ClassicFloatSakuraPower.ExtraBlock(ownsTarget: true, block: 0m, amount: 2) == 0m
            && ClassicFloatSakuraPower.ExtraBlock(ownsTarget: false, block: 5m, amount: 2) == 0m,
            "Expected Sakura Float to modify every positive owner Block event without creating Block from zero.");
    }

    [Fact]
    public void RequestedCardRaritiesRemainStable()
    {
        RegressionTestHarness.Require(
            new ClowRain().Rarity == CardRarity.Uncommon
            && new Hail().Rarity == CardRarity.Uncommon
            && new Lucid().Rarity == CardRarity.Uncommon
            && new Rewind().Rarity == CardRarity.Rare
            && new SakuraMod.SakuraModCode.Cards.Action().Rarity == CardRarity.Uncommon
            && new Kindness().Rarity == CardRarity.Rare
            && new ClowIllusion().Rarity == CardRarity.Uncommon,
            "Expected the requested Clow and Transparent card rarities to remain stable.");
    }

    [Fact]
    public void ClearCardEffectsExcludeTurnButKeepOtherSpellCardsEligible()
    {
        RegressionTestHarness.Require(
            !SakuraSourceCardRules.CanBeTargetedByClearCardEffects(new SpellTurn())
            && SakuraSourceCardRules.CanBeTargetedByClearCardEffects(new SpellRelease())
            && SakuraSourceCardRules.CanBeTargetedByClearCardEffects(new SpellEmptySpell()),
            "Expected Clear Card effects to exclude Turn while leaving non-conversion Spell cards eligible.");
    }

    [Fact]
    public void ExchangePileSwapLeavesSpellTurnInPlace()
    {
        RegressionTestHarness.Require(
            !ExchangeRules.ParticipatesInPileExchange(new SpellTurn())
            && ExchangeRules.ParticipatesInPileExchange(new ClowFirey()),
            "Expected the Exchange pile swap to leave Spell Turn in its pile like the other Clear Card gates.");

        var source = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Transparent/Exchange.cs"));
        RegressionTestHarness.Require(
            source.Contains("Where(ExchangeRules.ParticipatesInPileExchange)", StringComparison.Ordinal),
            "Expected the Exchange pile swap to filter both piles through the Spell Turn gate.");
    }

    [Fact]
    public void ReversalIsZeroCostAttackWithoutBaseDamageOrNormalDraw()
    {
        var reversal = new Reversal();
        var upgradedReversal = RegressionTestHarness.MutableForCostTest(new Reversal());
        upgradedReversal.UpgradeInternal();
        RegressionTestHarness.Require(
            reversal.EnergyCost.Canonical == 0
            && reversal.Type == CardType.Attack
            && reversal.DynamicVars.Damage.IntValue == 0
            && reversal.DynamicVars.Damage is ReversalDamageVar
            && reversal.DynamicVars["PileDamage"] is not DamageVar
            && !reversal.DynamicVars.ContainsKey("Cards")
            && reversal.DynamicVars["PileCardsPerDamage"].IntValue == 3
            && upgradedReversal.DynamicVars["PileCardsPerDamage"].IntValue == 2
            && ReversalRules.TotalDamage(0, 8, 3, 1) == 2
            && ReversalRules.TotalDamage(0, 8, 2, 1) == 4,
            "Expected Reversal to be a 0-cost Attack with no base damage or normal draw, dynamically scaling every 3/2 exchanged cards.");
    }

    [Fact]
    public void ExchangeConnectsDistinctLightweightVisualsForBothEffects()
    {
        var exchangeSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Transparent/Exchange.cs"));
        var cardExchangeVfx = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/CardStateExchangeVfx.cs"));
        var pileExchangeVfx = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/PileExchangeVfx.cs"));

        RegressionTestHarness.Require(
            exchangeSource.Contains("CardStateExchangeVfx.Play(first, second", StringComparison.Ordinal)
            && exchangeSource.Contains("SakuraActions.SelectUpToFromCards(", StringComparison.Ordinal)
            && exchangeSource.Contains("PileExchangeVfx.Play(", StringComparison.Ordinal),
            "Expected Exchange to use native four-region selection and distinct card-state/pile exchange visuals.");
        RegressionTestHarness.Require(
            cardExchangeVfx.Contains("TestMode.IsOn", StringComparison.Ordinal)
            && pileExchangeVfx.Contains("TestMode.IsOn", StringComparison.Ordinal)
            && !cardExchangeVfx.Contains("IsCardVfxEnabled", StringComparison.Ordinal)
            && !pileExchangeVfx.Contains("IsCardVfxEnabled", StringComparison.Ordinal),
            "Expected both lightweight Exchange visuals to skip tests without depending on the optional card-VFX setting.");
    }

    [Fact]
    public void ExchangeSnapshotsBothSelectedPilesBeforeOrderedMovement()
    {
        var exchangeSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Transparent/Exchange.cs"));
        var firstSnapshot = exchangeSource.IndexOf("var firstCards =", StringComparison.Ordinal);
        var secondSnapshot = exchangeSource.IndexOf("var secondCards =", StringComparison.Ordinal);
        var firstMove = exchangeSource.IndexOf("foreach (var card in firstCards)", StringComparison.Ordinal);
        var secondMove = exchangeSource.IndexOf("foreach (var card in secondCards)", StringComparison.Ordinal);

        RegressionTestHarness.Require(
            firstSnapshot >= 0
            && secondSnapshot > firstSnapshot
            && firstMove > secondSnapshot
            && secondMove > firstMove
            && exchangeSource.Split("CardPilePosition.Bottom", StringSplitOptions.None).Length - 1 == 2
            && !exchangeSource.Contains("CardPileCmd.Shuffle", StringComparison.Ordinal),
            "Expected Exchange to snapshot both piles before moving either one, preserve order at the bottom, and never shuffle.");
    }

    [Fact]
    public void ExistingPileMovementRefreshesBothPileEnds()
    {
        var actionsSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/SakuraActions.cs"));

        RegressionTestHarness.Require(
            actionsSource.Contains("var sourcePile = card.Pile;", StringComparison.Ordinal)
            && actionsSource.Contains("sourcePile.InvokeCardRemoveFinished();", StringComparison.Ordinal)
            && actionsSource.Contains("destinationPile.InvokeCardAddFinished();", StringComparison.Ordinal),
            "Expected silent existing-card movement to retain the source pile and signal both source removal and destination addition for cached pile counts.");
    }

    [Fact]
    public void SakuraLockRetains()
    {
        RegressionTestHarness.Require(
            new SakuraLock().CanonicalKeywords.Contains(CardKeyword.Retain),
            "Expected Sakura Lock to Retain.");
    }

    [Fact]
    public void SakuraDashRetains()
    {
        RegressionTestHarness.Require(
            new SakuraDash().CanonicalKeywords.SequenceEqual([CardKeyword.Retain, CardKeyword.Exhaust])
            && new ClowDash().CanonicalKeywords.SequenceEqual([CardKeyword.Exhaust]),
            "Expected only Sakura Dash to Retain while both forms Exhaust after use.");
    }

    [Fact]
    public void ClowFlyIsAnInnateOneShotMagicChargeStarter()
    {
        var baseCard = new ClowFly();
        var upgradedCard = RegressionTestHarness.MutableForCostTest(new ClowFly());
        upgradedCard.UpgradeInternal();
        var source = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/ClowSakura/Fly.cs"));

        RegressionTestHarness.Require(
            baseCard.EnergyCost.Canonical == 0
            && baseCard.Rarity == CardRarity.Uncommon
            && baseCard.CanonicalKeywords.SequenceEqual([CardKeyword.Innate, CardKeyword.Exhaust])
            && baseCard.DynamicVars.Cards.IntValue == 2
            && baseCard.DynamicVars["Magic"].IntValue == 1
            && upgradedCard.DynamicVars.Cards.IntValue == 3
            && upgradedCard.DynamicVars["Magic"].IntValue == 2
            && source.Contains("SakuraMagicCharge.GainMagic(choiceContext, Owner, ReleasedMagic(), this)", StringComparison.Ordinal)
            && !source.Contains("SpendUpToMagic", StringComparison.Ordinal),
            "Expected the Uncommon Clow Fly to cost 0, be Innate and Exhaust, draw 2/3, gain 1/2 explicit Magic Charge, and never spend Magic Charge.");
    }

    [Fact]
    public void ClowFightGeneratesStandardFightAtCurrentUpgradeLevel()
    {
        var source = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/ClowSakura/Fight.cs"));
        var englishCards = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/localization/eng/cards.json"));
        var chineseCards = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/localization/zhs/cards.json"));

        RegressionTestHarness.Require(
            source.Contains("combatState.CreateCard<ClowFight>(Owner)", StringComparison.Ordinal)
            && source.Contains("generatedFight.CurrentUpgradeLevel < CurrentUpgradeLevel", StringComparison.Ordinal)
            && source.Contains("CardPileCmd.AddGeneratedCardToCombat(", StringComparison.Ordinal)
            && !source.Contains("CreateClone()", StringComparison.Ordinal),
            "Expected Clow Fight to generate a standard Fight at the current upgrade level without inheriting temporary instance state such as Release's 0 cost.");
        RegressionTestHarness.Require(
            englishCards.Contains("{IfUpgraded:show:Fight+|Fight}", StringComparison.Ordinal)
            && chineseCards.Contains("{IfUpgraded:show:斗+|斗}", StringComparison.Ordinal)
            && englishCards.Contains("[gold]Extra:[/gold] Gain 2", StringComparison.Ordinal)
            && chineseCards.Contains("[gold]额外效果：[/gold]获得 2", StringComparison.Ordinal),
            "Expected Clow Fight's text to use the native generated-card hand wording for a standard Fight or Fight+.");
    }

    [Fact]
    public void SakuraFightGeneratesForgottenUpgradedFightsAndStrengthensThem()
    {
        var card = new SakuraFight();
        var power = new SakuraFightPower();
        var fightSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/ClowSakura/Fight.cs"));
        var powerSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Powers/SourceCards/SakuraFightPower.cs"));
        var chineseCards = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/localization/zhs/cards.json"));

        RegressionTestHarness.Require(
            card.EnergyCost.Canonical == 1
            && card.Type == CardType.Power
            && card.TargetType == TargetType.None
            && power.Type == PowerType.Buff
            && power.StackType == PowerStackType.Counter,
            "Expected Sakura Fight to be a 1-cost stacking Power.");
        RegressionTestHarness.Require(
            powerSource.Contains("BeforeHandDraw", StringComparison.Ordinal)
            && powerSource.Contains("fight.UpgradeInternal();", StringComparison.Ordinal)
            && powerSource.Contains("AddTemporaryGeneratedCardToHand", StringComparison.Ordinal)
            && fightSource.Contains("GetPower<SakuraFightPower>()?.Amount", StringComparison.Ordinal)
            && chineseCards.Contains("拥有[red]遗忘[/red]的[gold]斗+[/gold]", StringComparison.Ordinal),
            "Expected Sakura Fight to generate Forgotten Fight+ cards and add its stacks to Fight's temporary Strength.");
    }

    [Fact]
    public void SakuraPowerGrantsEightStrength()
    {
        RegressionTestHarness.Require(
            new SakuraPower().DynamicVars["StrengthPower"].IntValue == 8,
            "Expected Sakura Power to grant 8 Strength.");
    }

    [Fact]
    public void SakuraCreateStacksThreeLimitedEliteOrBossRelicRewards()
    {
        var options = SakuraCreateLegacy.CreateOptions();
        var createSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/ClowSakura/Create.cs"));
        var runHooksSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraRunHooks.cs"));
        var chineseCards = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/localization/zhs/cards.json"));

        RegressionTestHarness.Require(
            SakuraCreateLegacy.RunSavedDataKey == "sakura_create_victory_rewards_v1"
            && options.SchemaVersion == 1
            && options.WritePolicy == RunSavedDataWritePolicy.WhenSet,
            "Expected Sakura Create's remaining reward count to use versioned per-player run data.");
        RegressionTestHarness.Require(
            SakuraCreateLegacy.RewardsPerUse == 3
            && SakuraCreateLegacy.RemainingAfterVictory(3, RoomType.Boss) == 2
            && SakuraCreateLegacy.RemainingAfterVictory(3, RoomType.Elite) == 2
            && SakuraCreateLegacy.RemainingAfterVictory(3, RoomType.Monster) == 3
            && SakuraCreateLegacy.RemainingAfterVictory(0, RoomType.Boss) == 0,
            "Expected each Sakura Create use to add three rewards consumed one at a time by Elite or Boss victories.");
        RegressionTestHarness.Require(
            createSource.Contains("SakuraCreateLegacy.AddRewards(Owner);", StringComparison.Ordinal)
            && runHooksSource.Contains("SubscribeLifecycle<CombatVictoryEvent>", StringComparison.Ordinal)
            && runHooksSource.Contains("TryConsumeReward(player, evt.Room.RoomType)", StringComparison.Ordinal)
            && runHooksSource.Contains("AddExclusiveOrNormalRelicReward(player)", StringComparison.Ordinal)
            && chineseCards.Contains("下 3 次击败首领或精英时", StringComparison.Ordinal),
            "Expected Sakura Create to stack and resolve three persistent character-exclusive Elite or Boss rewards.");
    }

    [Fact]
    public void SakuraSandIsAnUnexhaustedPoisonSkill()
    {
        var sand = new SakuraSand();
        var sandSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/ClowSakura/Sand.cs"));
        var englishCards = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/localization/eng/cards.json"));
        var chineseCards = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/localization/zhs/cards.json"));

        RegressionTestHarness.Require(
            sand.EnergyCost.Canonical == 1
            && sand.Type == CardType.Skill
            && sand.TargetType == TargetType.None
            && !sand.CanonicalKeywords.Contains(CardKeyword.Exhaust)
            && sand.DynamicVars["Triggers"].IntValue == 2
            && sand.DynamicVars["PoisonPower"].IntValue == 1
            && sand.DynamicVars["Applications"].IntValue == 9
            && sandSource.IndexOf("TriggerCurrentPoison", StringComparison.Ordinal)
                < sandSource.IndexOf("ApplyPowerToEnemies<PoisonPower>", StringComparison.Ordinal)
            && englishCards.Contains("{Triggers:diff()}", StringComparison.Ordinal)
            && englishCards.Contains("{PoisonPower:diff()}", StringComparison.Ordinal)
            && englishCards.Contains("{Applications:diff()}", StringComparison.Ordinal)
            && chineseCards.Contains("{Triggers:diff()}", StringComparison.Ordinal)
            && chineseCards.Contains("{PoisonPower:diff()}", StringComparison.Ordinal)
            && chineseCards.Contains("{Applications:diff()}", StringComparison.Ordinal),
            "Expected Sakura Sand to be a 1-cost unexhausted Skill that triggers Poison twice before applying 1 Poison 9 times to all enemies.");
    }

    [Fact]
    public void SakuraSweetHealsTenPercentOfMaxHp()
    {
        RegressionTestHarness.Require(
            new SakuraSweet().DynamicVars["Magic"].IntValue == 10
            && ClassicSweetPower.HealAmount(80, 10) == 8
            && ClassicSweetPower.HealAmount(99, 10) == 9,
            "Expected Sakura Sweet to heal 10% of max HP, rounded down.");
    }

    [Fact]
    public void SweetCardsTargetEveryLivingPlayer()
    {
        var clowSweet = new ClowSweet();
        var sweetSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/ClowSakura/Sweet.cs"));
        var sourceCardRules = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/SakuraSourceCard.cs"));
        var englishCards = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/localization/eng/cards.json"));
        var chineseCards = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/localization/zhs/cards.json"));

        RegressionTestHarness.Require(
            clowSweet.EnergyCost.Canonical == 1,
            $"Expected Clow Sweet to cost 1, found {clowSweet.EnergyCost.Canonical}.");
        RegressionTestHarness.Require(
            clowSweet.DynamicVars.Heal.IntValue == 5,
            $"Expected Clow Sweet Heal to be 5, found {clowSweet.DynamicVars.Heal.IntValue}.");
        RegressionTestHarness.Require(
            new SakuraSweet().EnergyCost.Canonical == 2,
            $"Expected Sakura Sweet to cost 2, found {new SakuraSweet().EnergyCost.Canonical}.");
        RegressionTestHarness.Require(
            clowSweet.MultiplayerConstraint == CardMultiplayerConstraint.MultiplayerOnly
            && new SakuraSweet().MultiplayerConstraint == CardMultiplayerConstraint.MultiplayerOnly,
            "Expected both Sweet forms to use STS2's native multiplayer-only card constraint.");
        RegressionTestHarness.Require(
            sourceCardRules.Contains(
                "GetUnlockedCards(owner.UnlockState, owner.RunState.CardMultiplayerConstraint)",
                StringComparison.Ordinal),
            "Expected custom Clow generation to honor STS2's native player-count filter.");
        RegressionTestHarness.Require(
            sweetSource.Contains(".Where(static player => player.Creature.IsAlive)", StringComparison.Ordinal),
            "Expected Sweet cards to filter living players.");
        RegressionTestHarness.Require(
            sweetSource.Contains("EnergyCost.UpgradeBy(-1)", StringComparison.Ordinal),
            "Expected Clow Sweet to preserve its existing cost-upgrade declaration.");
        RegressionTestHarness.Require(
            sweetSource.Contains("CreatureCmd.Heal", StringComparison.Ordinal),
            "Expected Clow Sweet to heal each target.");
        RegressionTestHarness.Require(
            sweetSource.Contains("PowerCmd.Apply<RegenPower>", StringComparison.Ordinal)
            && sweetSource.Contains("PowerCmd.Apply<ClassicSweetPower>", StringComparison.Ordinal),
            "Expected both Sweet forms to apply their powers to a target collection.");
        RegressionTestHarness.Require(
            englishCards.Contains("All players heal {Heal:diff()}", StringComparison.Ordinal)
            && englishCards.Contains("all players gain {Heal:diff()}", StringComparison.Ordinal)
            && englishCards.Contains("At the start of each player's turn", StringComparison.Ordinal),
            "Expected English Sweet descriptions to expose their party scope.");
        RegressionTestHarness.Require(
            chineseCards.Contains("所有玩家各回复 {Heal:diff()}", StringComparison.Ordinal)
            && chineseCards.Contains("所有玩家各获得 {Heal:diff()}", StringComparison.Ordinal)
            && chineseCards.Contains("所有玩家的回合开始时", StringComparison.Ordinal),
            "Expected Chinese Sweet descriptions to expose their party scope.");
    }

    [Fact]
    public void ClassicCloudValuesAndStateReferenceRemainStable()
    {
        var clowCloud = new ClowCloud();
        var upgradedClowCloud = RegressionTestHarness.MutableForCostTest(new ClowCloud());
        upgradedClowCloud.UpgradeInternal();
        var sakuraCloud = new SakuraCloud();

        RegressionTestHarness.Require(
            clowCloud.EnergyCost.Canonical == 1
            && clowCloud.Rarity == CardRarity.Common
            && clowCloud.Elements == SakuraElementSet.Water
            && clowCloud.DynamicVars.CalculationBase.IntValue == 5
            && clowCloud.DynamicVars.CalculationExtra.IntValue == 5
            && upgradedClowCloud.DynamicVars.CalculationBase.IntValue == 8
            && upgradedClowCloud.DynamicVars.CalculationExtra.IntValue == 8
            && sakuraCloud.DynamicVars.CalculationBase.IntValue == 7
            && sakuraCloud.DynamicVars.CalculationExtra.IntValue == 3,
            "Expected Clow Cloud to block for 5 plus 5 per Watery card (8/8 upgraded) and Sakura Cloud to block for 7 plus 3 per Watery card.");
        RegressionTestHarness.Require(
            SakuraSourceCardText.ElementStatesReferencedBy(clowCloud).SequenceEqual([SakuraElement.Water]),
            "Expected Clow Cloud to expose the Watery-state hover tip used by its conditional Rain generation.");
    }

    [Fact]
    public void ClassicWaterySynergiesIncludeTransparentCards()
    {
        RegressionTestHarness.Require(
            SakuraArrow.CountDistinctCardTypes([
                new ClowSword(),
                new ClowSword(),
                new SakuraSword(),
                new Reflect(),
                new Reflect(),
                new SpellShuiLong(),
                new MegaCrit.Sts2.Core.Models.Cards.Clumsy(),
                new MegaCrit.Sts2.Core.Models.Cards.Clumsy()
            ]) == 5,
            "Expected Sakura Arrow to count each distinct discarded card type once across mod and vanilla card families.");

        RegressionTestHarness.Require(
            SakuraCloudEffects.CountWateryCards([
                new ClowWatery(),
                new SakuraWatery(),
                new Reflect(),
                new SpellShuiLong(),
                new Gale()
            ]) == 4,
            "Expected both Cloud forms to count Transparent and Spell cards through the shared Watery element projection.");

        RegressionTestHarness.Require(
            SakuraSnowRules.CountsAsWateryCard(new ClowWatery())
            && SakuraSnowRules.CountsAsWateryCard(new SakuraWatery())
            && SakuraSnowRules.CountsAsWateryCard(new Reflect())
            && SakuraSnowRules.CountsAsWateryCard(new SpellShuiLong())
            && !SakuraSnowRules.CountsAsWateryCard(new Gale()),
            "Expected both Snow forms to count every Watery card.");
    }

    [Fact]
    public void ClowShotDealsTwoHitsAndGainsVigor()
    {
        var baseCard = new ClowShot();
        var upgradedCard = RegressionTestHarness.MutableForCostTest(new ClowShot());
        upgradedCard.UpgradeInternal();

        RegressionTestHarness.Require(
            baseCard.DynamicVars.Damage.IntValue == 4
            && baseCard.DynamicVars["Hits"].IntValue == 2
            && baseCard.DynamicVars["VigorPower"].IntValue == 2
            && !baseCard.DynamicVars.ContainsKey("PoisonPower")
            && upgradedCard.DynamicVars.Damage.IntValue == 5
            && upgradedCard.DynamicVars["Hits"].IntValue == 2
            && upgradedCard.DynamicVars["VigorPower"].IntValue == 2,
            "Expected Clow Shot to deal 4 (5) damage twice, gain 2 Vigor, and no longer apply Poison.");
    }

    [Fact]
    public void TrueOrFalseUpgradeRemovesExhaustWithoutIncreasingValues()
    {
        var baseCard = new TrueOrFalse();
        var upgradedCard = RegressionTestHarness.MutableForCostTest(new TrueOrFalse());
        upgradedCard.UpgradeInternal();
        var upgradedDrawChoice = RegressionTestHarness.MutableForCostTest(new TrueOrFalseDrawChoice());
        upgradedDrawChoice.UpgradeInternal();
        var upgradedEnergyChoice = RegressionTestHarness.MutableForCostTest(new TrueOrFalseEnergyChoice());
        upgradedEnergyChoice.UpgradeInternal();

        RegressionTestHarness.Require(
            baseCard.Keywords.Contains(CardKeyword.Exhaust)
            && !upgradedCard.Keywords.Contains(CardKeyword.Exhaust)
            && baseCard.DynamicVars.Cards.IntValue == 2
            && upgradedCard.DynamicVars.Cards.IntValue == 2
            && baseCard.DynamicVars.Energy.IntValue == 1
            && upgradedCard.DynamicVars.Energy.IntValue == 1
            && upgradedDrawChoice.DynamicVars.Cards.IntValue == 2
            && upgradedEnergyChoice.DynamicVars.Energy.IntValue == 1
            && upgradedEnergyChoice.CanonicalKeywords.Contains(SakuraKeywords.Stabilize),
            "Expected True or False to Exhaust only before upgrading and keep its 2-card/1-Energy values after upgrading.");
    }

    [Fact]
    public void SpellReleaseTargetsAllThreeSourceCardEras()
    {
        RegressionTestHarness.Require(
            SpellRelease.CanRelease(new ClowSword())
            && SpellRelease.CanRelease(new SakuraSword())
            && SpellRelease.CanRelease(new Gale())
            && !SpellRelease.CanRelease(new SpellSeal())
            && !SpellRelease.CanRelease(new MegaCrit.Sts2.Core.Models.Cards.Clumsy()),
            "Expected Release to target Clow, Sakura, and Clear Cards without broadening to Spell or vanilla cards.");
    }

    [Fact]
    public async Task SpellReleaseScalesAndResetsAllTransparentCardValues()
    {
        var gale = RegressionTestHarness.MutableForCostTest(new Gale());
        var reflect = RegressionTestHarness.MutableForCostTest(new Reflect());
        var trueOrFalse = RegressionTestHarness.MutableForCostTest(new TrueOrFalse());

        SakuraReleaseState.Apply(gale, 0.5f);
        SakuraReleaseState.Apply(reflect, 0.5f);
        SakuraReleaseState.Apply(trueOrFalse, 0.5f);

        RegressionTestHarness.Require(
            gale.DynamicVars.Damage.IntValue == 9
            && gale.DynamicVars["Cards"].IntValue == 3
            && gale.DynamicVars["ExtraCopies"].IntValue == 3
            && reflect.DynamicVars.Block.IntValue == 7
            && trueOrFalse.DynamicVars.Cards.IntValue == 3
            && trueOrFalse.DynamicVars.Energy.IntValue == 1
            && gale.Keywords.Contains(CardKeyword.Exhaust)
            && gale.Keywords.Contains(CardKeyword.Ethereal),
            "Expected Release to scale every Transparent Card dynamic value using the existing floor rule.");

        await gale.AfterCardExhausted(null!, gale, causedByEthereal: true);
        SakuraReleaseState.Reset(reflect);
        SakuraReleaseState.Reset(trueOrFalse);

        RegressionTestHarness.Require(
            gale.DynamicVars.Damage.IntValue == 6
            && gale.DynamicVars["Cards"].IntValue == 2
            && gale.DynamicVars["ExtraCopies"].IntValue == 2
            && reflect.DynamicVars.Block.IntValue == 5
            && trueOrFalse.DynamicVars.Cards.IntValue == 2
            && trueOrFalse.DynamicVars.Energy.IntValue == 1
            && !gale.Keywords.Contains(CardKeyword.Exhaust)
            && !gale.Keywords.Contains(CardKeyword.Ethereal),
            "Expected Release reset to restore Transparent Card values and temporary keywords.");
    }

    [Fact]
    public void SpellReleaseUsesOneValuePathAndPreservesAdjustedValueOrdering()
    {
        var clowSword = RegressionTestHarness.MutableForCostTest(new ClowSword());
        var baseDamage = clowSword.DynamicVars.Damage.IntValue;

        SakuraReleaseState.Apply(clowSword, 0.5f);

        RegressionTestHarness.Require(
            clowSword.DynamicVars.Damage.IntValue == baseDamage + (int)Math.Floor(baseDamage * 0.5f)
            && SakuraSourceCardValues.EffectiveValue(clowSword, clowSword.DynamicVars.Damage)
                == clowSword.DynamicVars.Damage.IntValue
            && SakuraReleaseState.AdjustedReleasedValue(
                clowSword,
                clowSword.DynamicVars.Damage.Name,
                clowSword.DynamicVars.Damage.IntValue,
                value => (int)Math.Floor(value * 1.6m)) == 13,
            "Expected every source era to use direct Release scaling while preserving adjustment-before-Release ordering.");

        SakuraReleaseState.Reset(clowSword);
    }

    [Fact]
    public void ClowMirrorSakuraMappingsUseCanonicalTargetValues()
    {
        static CardModel CreateCanonical(Type type) =>
            (CardModel)(Activator.CreateInstance(type)
                ?? throw new InvalidOperationException($"Could not create {type.Name}."));

        var sakuraSword = new SakuraSword();
        var mappedSword = SakuraSourceCardRules.CreateMirrorCopySource(
            sakuraSword,
            static _ => true,
            CreateCanonical,
            static _ => throw new InvalidOperationException("Sakura mappings must not clone their source."));

        RegressionTestHarness.Require(
            mappedSword is ClowSword
            && mappedSword.DynamicVars.Damage.IntValue == 6
            && mappedSword.EnergyCost.Canonical == 1
            && sakuraSword.DynamicVars.Damage.IntValue == 16,
            "Expected Mirror to map Sakura Sword to a canonical 1-cost, 6-damage Clow Sword without mutating the source.");

        var clowSword = new ClowSword();
        var expectedClone = new ClowSword();
        var clonedSword = SakuraSourceCardRules.CreateMirrorCopySource(
            clowSword,
            static _ => false,
            static _ => throw new InvalidOperationException("Same-model Mirror copies must not use the cross-model factory."),
            source => ReferenceEquals(source, clowSword)
                ? expectedClone
                : throw new InvalidOperationException("Mirror cloned the wrong source card."));
        RegressionTestHarness.Require(
            ReferenceEquals(clonedSword, expectedClone),
            "Expected Mirror to keep using the source clone path for a selected Clow Card.");

        RegressionTestHarness.Require(
            SakuraSourceCardRules.CreateMirrorCopySource(
                new SakuraLove(),
                static _ => false,
                CreateCanonical,
                static _ => throw new InvalidOperationException("Love mapping must not clone its source.")) is SpellEmptySpell
            && SakuraSourceCardRules.CreateMirrorCopySource(
                new SakuraHope(),
                static _ => false,
                CreateCanonical,
                static _ => throw new InvalidOperationException("Hope mapping must not clone its source.")) is ClowNothing,
            "Expected Mirror's Love and Hope mappings to remain canonical Empty Spell and Clow Nothing cards.");
    }

    [Fact]
    public void PowerGeneratedBlockAndDamageUseNativeValueSemantics()
    {
        RegressionTestHarness.Require(
            SakuraPowerValueProps.Block == ValueProp.Unpowered
            && SakuraPowerValueProps.Damage == ValueProp.Unpowered
            && SakuraPowerValueProps.HpLoss == (ValueProp.Unblockable | ValueProp.Unpowered),
            "Expected power-generated Block, damage, and HP loss to use native unpowered value properties.");

        var powersRoot = Path.GetDirectoryName(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Powers/SakuraPowerModel.cs"))!;
        var classicPowersSource = string.Join(
            '\n',
            Directory.EnumerateFiles(Path.Join(powersRoot, "SourceCards"), "*.cs").Select(File.ReadAllText));
        var transparentPowersSource = string.Join(
            '\n',
            Directory.EnumerateFiles(Path.Join(powersRoot, "Transparent"), "*.cs").Select(File.ReadAllText));
        var powerSources = $"{classicPowersSource}\n{transparentPowersSource}";
        RegressionTestHarness.Require(
            System.Text.RegularExpressions.Regex.Matches(
                powerSources,
                "CreatureCmd\\.GainBlock\\([^;]*SakuraPowerValueProps\\.Block",
                System.Text.RegularExpressions.RegexOptions.Singleline).Count == 7
            && System.Text.RegularExpressions.Regex.Matches(
                powerSources,
                "CreatureCmd\\.GainBlock\\(",
                System.Text.RegularExpressions.RegexOptions.Singleline).Count == 7,
            "Expected every SakuraMod power-generated Block call to use the shared unpowered Block contract without a card source.");
        RegressionTestHarness.Require(
            transparentPowersSource.Contains("SakuraPowerValueProps.Damage", StringComparison.Ordinal)
            && classicPowersSource.Contains("SakuraPowerValueProps.Damage", StringComparison.Ordinal)
            && classicPowersSource.Contains("SakuraPowerValueProps.HpLoss", StringComparison.Ordinal),
            "Expected Reflection, Firey, and Nothing damage to use native power value properties without a card source.");
    }

    [Fact]
    public void SharedKeywordElementExchangeAndDrawRulesRemainStable()
    {
        RegressionTestHarness.Require(
            SakuraCardHoverTips.KeywordTips(new Gale()).Contains(SakuraKeywords.Wind),
            "Expected Sakura card hover capability decisions to include static element keyword tips.");
        RegressionTestHarness.Require(
            SakuraCardHoverTips.KeywordTips(new Hail()).Contains(SakuraKeywords.Frostbite),
            "Expected Hail hover tips to explain Frostbite.");
        RegressionTestHarness.Require(
            SakuraCardHoverTips.ShouldIncludeFreezePowerTip(SakuraCardHoverTips.KeywordTips(new Hail()))
            && !SakuraCardHoverTips.ShouldIncludeFreezePowerTip(SakuraCardHoverTips.KeywordTips(new Gale())),
            "Expected Frostbite cards, but not unrelated cards, to include the Freeze power hover tip.");
        RegressionTestHarness.Require(
            SakuraSourceCardText.KeywordTips(new ClowSnow()).Contains(SakuraKeywords.Frostbite)
            && SakuraSourceCardText.KeywordTips(new SakuraSnow()).Contains(SakuraKeywords.Frostbite),
            "Expected both Snow forms to explain Frostbite.");
        RegressionTestHarness.Require(
            SakuraCardHoverTips.ShouldIncludeFreezePowerTip(SakuraSourceCardText.KeywordTips(new ClowFreeze()))
            && SakuraCardHoverTips.ShouldIncludeFreezePowerTip(SakuraSourceCardText.KeywordTips(new ClowSnow()))
            && SakuraCardHoverTips.ShouldIncludeFreezePowerTip(SakuraSourceCardText.KeywordTips(new SakuraSnow())),
            "Expected every source card that references Frostbite to include the Freeze power hover tip.");
        RegressionTestHarness.Require(
            SakuraCardHoverTips.StaticTipKeys(new Remind()).Contains(SakuraCardHoverTips.RemindTipKey),
            "Expected Remind hover tips to explain its record-recall rules.");
        RegressionTestHarness.Require(
            SakuraCardHoverTips.StaticTipKeys(new Remind()).Contains(SakuraMemoryPile.PileId)
            && SakuraCardHoverTips.StaticTipKeys(new Spiral()).Contains(SakuraMemoryPile.PileId),
            "Expected Remind and Spiral hover tips to explain the Memory pile.");
        CardModel[] forgottenReferenceCards =
        [
            new Dreaming(),
            new Exchange(),
            new Spiral(),
            new Blank(),
            new TrueOrFalse(),
            new TrueOrFalseDrawChoice()
        ];
        RegressionTestHarness.Require(
            forgottenReferenceCards.All(card =>
                SakuraCardHoverTips.StaticTipKeys(card).Contains(SakuraCardHoverTips.TemporaryTipKey)),
            "Expected every card whose description names Forgotten to contribute the Forgotten hover tip.");

        var gravitation = new Gravitation();
        var upgradedGravitation = RegressionTestHarness.MutableForCostTest(new Gravitation());
        upgradedGravitation.UpgradeInternal();
        RegressionTestHarness.Require(
            gravitation.EnergyCost.Canonical == 0
            && gravitation.Rarity == CardRarity.Rare
            && gravitation.Type == CardType.Skill
            && gravitation.TargetType == TargetType.Self
            && gravitation.CanonicalKeywords.SequenceEqual([SakuraKeywords.Earth])
            && SakuraCardModel.HasMagicChargeExtraEffect(gravitation)
            && upgradedGravitation.Keywords.Contains(CardKeyword.Retain)
            && new GravitationHoldPower().StackType == PowerStackType.Single
            && GravitationHoldPower.TryIncreaseReturnedCardCost(gravitation, 1, 0, out var firstReturnCost)
            && firstReturnCost == 1
            && GravitationHoldPower.TryIncreaseReturnedCardCost(gravitation, 2, 1, out var repeatedReturnCost)
            && repeatedReturnCost == 3
            && RegressionTestHarness.DeclaresMethod<GravitationHoldPower>("ModifyCardPlayResultPileTypeAndPosition")
            && RegressionTestHarness.DeclaresMethod<GravitationHoldPower>("AfterModifyingCardPlayResultPileOrPosition")
            && RegressionTestHarness.DeclaresMethod<GravitationHoldPower>("TryModifyEnergyCostInCombat")
            && RegressionTestHarness.DeclaresMethod<GravitationHoldPower>("AfterSideTurnEnd")
            && RegressionTestHarness.DeclaresMethod<GravitationHoldPower>("AfterApplied")
            && RegressionTestHarness.DeclaresMethod<GravitationHoldPower>("AfterRemoved"),
            "Expected Gravitation to be a 0-cost rare Earth replay engine whose returned cards cost 1 more each time and which gains Retain on upgrade.");

        RegressionTestHarness.Require(
            SakuraActions.ElementSetOf(new Gale()) == SakuraElementSet.Wind
            && SakuraActions.ElementSetOf(new Reflect()) == SakuraElementSet.Water
            && SakuraActions.ElementSetOf(new Blaze()) == SakuraElementSet.Fire
            && SakuraActions.ElementSetOf(new Gravitation()) == SakuraElementSet.Earth,
            "Expected Transparent Card elements to map into Classic element identities.");

        RegressionTestHarness.Require(
            SakuraActions.HasExchangeableEnergyCost(new Gale())
            && SakuraActions.HasExchangeableEnergyCost(new SpellSeal())
            && SakuraActions.HasExchangeableEnergyCost(new Remind())
            && !SakuraActions.HasExchangeableEnergyCost(new SpellHuoShen())
            && !SakuraActions.HasExchangeableEnergyCost(new MegaCrit.Sts2.Core.Models.Cards.Clumsy()),
            "Expected Exchange to accept only cards with displayed, fixed Energy costs.");

        var exchange = new Exchange();
        var upgradedExchange = RegressionTestHarness.MutableForCostTest(new Exchange());
        upgradedExchange.UpgradeInternal();
        RegressionTestHarness.Require(
            exchange.EnergyCost.Canonical == 0
            && exchange.CanonicalKeywords.Contains(CardKeyword.Exhaust)
            && !upgradedExchange.Keywords.Contains(CardKeyword.Exhaust),
            "Expected Exchange to cost 0 and Exhaust, with the upgrade removing Exhaust.");

        RegressionTestHarness.Require(
            Synchronize.CanSynchronize(new Synchronize(), new Gale())
            && !Synchronize.CanSynchronize(new Synchronize(), new SakuraLove()),
            "Expected Synchronize to accept ordinary cards and reject cards with Unplayable.");
        RegressionTestHarness.Require(
            SynchronizedCardPairModifier.AutoPlayPileTypes.SequenceEqual(
                [PileType.Hand, PileType.Draw, PileType.Discard]),
            "Expected synchronized cards to auto-play from hand, draw, or discard, but not from other piles.");

        RegressionTestHarness.Require(
            !GaleRules.ShouldDrawAfterPlay(-1)
            && !GaleRules.ShouldDrawAfterPlay(0)
            && !GaleRules.ShouldDrawAfterPlay(1)
            && !GaleRules.ShouldDrawAfterPlay(2)
            && GaleRules.ShouldDrawAfterPlay(3)
            && !GaleRules.ShouldDrawAfterPlay(4)
            && GaleRules.ShouldDrawAfterPlay(6),
            "Expected Gale to draw after every third owner-played Gale this combat.");
    }

    [Fact]
    public void CoreAttackDefenseAndControlCardContractsRemainStable()
    {
        var blaze = new Blaze();
        var upgradedBlaze = RegressionTestHarness.MutableForCostTest(new Blaze());
        upgradedBlaze.UpgradeInternal();
        RegressionTestHarness.Require(
            blaze.EnergyCost.Canonical == 3
            && blaze.Type == CardType.Attack
            && blaze.Rarity == CardRarity.Rare
            && blaze.CanonicalKeywords.SequenceEqual([SakuraKeywords.Fire])
            && Blaze.MaxCardsToExhaust == 3
            && blaze.DynamicVars.CalculationBase.IntValue == 27
            && blaze.DynamicVars.ExtraDamage.IntValue == 2
            && upgradedBlaze.DynamicVars.CalculationBase.IntValue == 32
            && upgradedBlaze.DynamicVars.ExtraDamage.IntValue == 3
            && !typeof(MainFile).Assembly.GetTypes().Any(static type => type.Name == "SakuraBurnPower"),
            "Expected Blaze to cost 3, Exhaust up to 3 cards, deal 27/32 damage, scale by 2/3 per exhausted card, and double that increment with Extra.");

        var gale = new Gale();
        var upgradedGale = RegressionTestHarness.MutableForCostTest(new Gale());
        upgradedGale.UpgradeInternal();
        RegressionTestHarness.Require(
            gale.EnergyCost.Canonical == 0
            && gale.Rarity == CardRarity.Common
            && gale.Type == CardType.Attack
            && gale.CanonicalKeywords.SequenceEqual([SakuraKeywords.Wind])
            && gale.DynamicVars.Damage.IntValue == 6
            && gale.DynamicVars["Cards"].IntValue == 2
            && gale.DynamicVars["ExtraCopies"].IntValue == 2
            && GaleRules.CountsAsGale(gale)
            && upgradedGale.DynamicVars.Damage.IntValue == 9,
            "Expected Gale to cost 0, deal 6/9 damage, draw 2 every third play, and create 2 copies with Extra.");

        var upgradedAqua = RegressionTestHarness.MutableForCostTest(new Aqua());
        upgradedAqua.UpgradeInternal();
        RegressionTestHarness.Require(
            new Aqua().EnergyCost.Canonical == 0
            && !new Aqua().CanonicalKeywords.Contains(CardKeyword.Exhaust)
            && new Aqua().DynamicVars.Damage.IntValue == 4
            && new Aqua().DynamicVars.Energy.IntValue == 1
            && SakuraCardHoverTips.KeywordTips(new Aqua()).Contains(SakuraKeywords.Frostbite)
            && AquaRules.DrawCount(0) == 0
            && AquaRules.DrawCount(1) == 1
            && AquaRules.DrawCount(2) == 2
            && AquaRules.FrostbiteEnemyCount([]) == 0
            && AquaRules.FrostbiteEnemyForPresentation([]) is null
            && upgradedAqua.DynamicVars.Damage.IntValue == 7,
            "Expected Aqua to cost 0, deal 4/7 AOE damage, draw 1 per Frostbite enemy, and grant 1 Energy per Frostbite enemy with Extra.");

        var spiral = new Spiral();
        var upgradedSpiral = RegressionTestHarness.MutableForCostTest(new Spiral());
        upgradedSpiral.UpgradeInternal();
        var spiralSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Transparent/Spiral.cs"));
        var spiralNextTurnPowerSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Powers/Transparent/SpiralNextTurnPower.cs"));
        var chineseCards = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/localization/zhs/cards.json"));
        var englishCards = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/localization/eng/cards.json"));
        RegressionTestHarness.Require(
            spiral.EnergyCost.Canonical == 1
            && spiral.Rarity == CardRarity.Uncommon
            && spiral.Type == CardType.Attack
            && spiral.TargetType == TargetType.AnyEnemy
            && spiral.CanonicalKeywords.SequenceEqual([SakuraKeywords.Wind])
            && spiral.DynamicVars.Damage.IntValue == 6
            && spiral.DynamicVars.Damage is SpiralDamageVar
            && spiral.DynamicVars.Block.IntValue == 3
            && spiral.DynamicVars.Block is SpiralBlockVar
            && spiral.DynamicVars["MemoryScale"].IntValue == 1
            && spiral.DynamicVars["NextTurnCopies"].IntValue == 1
            && spiral.DynamicVars["ExtraCopies"].IntValue == 3
            && SpiralRules.OutputWithMemory(6, 0) == 6
            && SpiralRules.OutputWithMemory(6, 2) == 8
            && SpiralRules.OutputWithMemory(6, 4) == 10
            && SpiralRules.OutputWithMemory(6, 6) == 12
            && upgradedSpiral.DynamicVars.Damage.IntValue == 6
            && upgradedSpiral.DynamicVars.Block.IntValue == 3
            && SpiralRules.OutputWithMemory(upgradedSpiral.DynamicVars.Damage.IntValue, 4) == 10
            && new SpiralNextTurnPower().InstanceType == PowerInstanceType.Instanced
            && new SpiralNextTurnPower().StackType == PowerStackType.Counter
            && !new SpiralNextTurnPower().IsVisible
            && RegressionTestHarness.DeclaresMethod<SpiralNextTurnPower>("BeforeHandDraw")
            && !RegressionTestHarness.DeclaresMethod<SpiralNextTurnPower>("AfterPlayerTurnStart"),
            "Expected Spiral and Spiral+ to deal 6 damage and gain 3 Block plus Memory, with the upgrade value supplied by a one-shot next-hand-draw Power.");
        RegressionTestHarness.Require(
            spiralSource.Contains("AddTemporaryGeneratedCardsToHand<Spiral>", StringComparison.Ordinal)
            && spiralSource.Contains("freeThisTurn: true", StringComparison.Ordinal)
            && !spiralSource.Contains("AddTemporaryCopyToHand", StringComparison.Ordinal)
            && spiralNextTurnPowerSource.Contains("combatState.CreateCard<Spiral>(player)", StringComparison.Ordinal)
            && !spiralNextTurnPowerSource.Contains("UpgradeInternal();", StringComparison.Ordinal)
            && spiralNextTurnPowerSource.Contains("Pile = PileType.Draw", StringComparison.Ordinal)
            && spiralNextTurnPowerSource.Contains("Position = CardPilePosition.Top", StringComparison.Ordinal)
            && spiralNextTurnPowerSource.Contains("AddTemporary = true", StringComparison.Ordinal)
            && !spiralNextTurnPowerSource.Contains("AddTemporaryGeneratedCardToHand", StringComparison.Ordinal)
            && !spiralNextTurnPowerSource.Contains("AddTemporaryCopyToHand", StringComparison.Ordinal)
            && chineseCards.Contains("将 {NextTurnCopies:diff()} 张带有[red]遗忘[/red]的[gold]螺旋[/gold]置于抽牌堆顶", StringComparison.Ordinal)
            && chineseCards.Contains("生成 {ExtraCopies:diff()} 张[gold]螺旋[/gold]，并给予[red]遗忘[/red]，本回合能耗为 0", StringComparison.Ordinal)
            && englishCards.Contains("on top of your draw pile", StringComparison.Ordinal)
            && englishCards.Contains("They cost 0 this turn", StringComparison.Ordinal),
            "Expected Spiral+ to put a base Forgotten Spiral on top before next turn's draw, while Extra generates three base Forgotten Spirals that cost 0 this turn.");

        var blockedHit = new DamageResult(null!, ValueProp.Move) { UnblockedDamage = 0 };
        var unblockedHit = new DamageResult(null!, ValueProp.Move) { UnblockedDamage = 1 };
        RegressionTestHarness.Require(
            !SakuraSnowRules.ShouldApplyFrostbite(blockedHit)
            && SakuraSnowRules.ShouldApplyFrostbite(unblockedHit),
            "Expected Snow Frostbite to trigger once from a positive unblocked damage result, not damage magnitude.");
        RegressionTestHarness.Require(
            SakuraFrostbitePower.ConvertToFreeze(5) == (0, 5)
            && SakuraFrostbitePower.ConvertToFreeze(6) == (1, 0)
            && SakuraFrostbitePower.ConvertToFreeze(11) == (1, 5)
            && SakuraFrostbitePower.ConvertToFreeze(12) == (2, 0)
            && SakuraFrostbitePower.ConvertToFreeze(14) == (2, 2)
            && SakuraFrostbitePower.ConvertToFreeze(17) == (2, 5)
            && ClassicFreezePower.BlockGain == 5
            && new ClassicFreezePower().StackType == PowerStackType.Counter
            && RegressionTestHarness.DeclaresMethod<ClassicFreezePower>("AfterSideTurnEnd")
            && !RegressionTestHarness.DeclaresMethod<ClassicFreezePower>("BeforeSideTurnStart"),
            "Expected every 6 Frostbite to become 1 multi-turn Freeze stack while preserving the remainder.");

        var clowFlower = new ClowFlower();
        var upgradedClowFlower = RegressionTestHarness.MutableForCostTest(new ClowFlower());
        upgradedClowFlower.UpgradeInternal();
        var sakuraFlower = new SakuraFlower();
        var exhaustKeywords = new HashSet<CardKeyword> { CardKeyword.Exhaust };
        RegressionTestHarness.Require(
            clowFlower.EnergyCost.Canonical == 0
            && clowFlower.Rarity == CardRarity.Uncommon
            && clowFlower.Type == CardType.Skill
            && clowFlower.TargetType == TargetType.None
            && clowFlower.Elements == SakuraElementSet.Earth
            && clowFlower.CanonicalKeywords.SequenceEqual([CardKeyword.Exhaust])
            && clowFlower.DynamicVars.Energy.IntValue == 2
            && ClowFlower.ExtraEnergy == 2
            && upgradedClowFlower.Keywords.Contains(CardKeyword.Exhaust)
            && upgradedClowFlower.Keywords.Contains(CardKeyword.Retain)
            && upgradedClowFlower.DynamicVars.Energy.IntValue == 3,
            "Expected Clow Flower to cost 0, gain 2 Energy, Exhaust, gain 2 additional Energy with Extra, and upgrade to 3 Energy with Retain.");
        RegressionTestHarness.Require(
            sakuraFlower.Rarity == CardRarity.Token
            && sakuraFlower.EnergyCost.Canonical == 0
            && sakuraFlower.Type == CardType.Skill
            && sakuraFlower.TargetType == TargetType.None
            && sakuraFlower.Elements == SakuraElementSet.Earth
            && sakuraFlower.CanonicalKeywords.SequenceEqual([CardKeyword.Retain, CardKeyword.Exhaust])
            && sakuraFlower.DynamicVars.Energy.IntValue == 5,
            "Expected Sakura Flower to Retain, Exhaust, and gain 5 Energy.");
        RegressionTestHarness.Require(
            FlowerRules.TryRemoveExhaust(clowFlower, clowFlower, true, exhaustKeywords)
            && !exhaustKeywords.Contains(CardKeyword.Exhaust)
            && !FlowerRules.TryRemoveExhaust(
                clowFlower,
                sakuraFlower,
                true,
                new HashSet<CardKeyword> { CardKeyword.Exhaust })
            && FlowerRules.ResultPileForPlay(true, PileType.Exhaust) == PileType.Discard
            && FlowerRules.ResultPileForPlay(false, PileType.Exhaust) == PileType.Exhaust
            && FlowerRules.ResultPileForPlay(true, PileType.None) == PileType.None
            && SakuraSourceCardText.ElementStatesReferencedBy(clowFlower).SequenceEqual([SakuraElement.Earth])
            && SakuraSourceCardText.ElementStatesReferencedBy(sakuraFlower).SequenceEqual([SakuraElement.Earth]),
            "Expected each Flower to avoid Exhaust only for its own play that began in Earthy state and to expose the Earthy-state hover tip.");

        var siege = new Siege();
        var upgradedSiege = RegressionTestHarness.MutableForCostTest(new Siege());
        upgradedSiege.UpgradeInternal();
        RegressionTestHarness.Require(
            siege.EnergyCost.Canonical == 0
            && siege.Type == CardType.Skill
            && siege.Rarity == CardRarity.Uncommon
            && siege.TargetType == TargetType.Self
            && siege.CanonicalKeywords.Contains(SakuraKeywords.Earth)
            && SakuraCardModel.HasMagicChargeExtraEffect(siege)
            && siege.DynamicVars.Block.IntValue == 3
            && siege.DynamicVars.Block is BlockVar
            && !siege.DynamicVars.ContainsKey("ExtraBlock")
            && siege.DynamicVars.Weak.IntValue == 1
            && SiegeRules.BlockPerEnemy == 2
            && SiegeRules.BlockAmount(3, 0) == 3
            && SiegeRules.BlockAmount(3, 1) == 5
            && SiegeRules.BlockAmount(3, 2) == 7
            && !SiegeRules.ShouldTrigger(0)
            && SiegeRules.ShouldTrigger(1)
            && SiegeRules.ExtraDamage(-1) == 0
            && SiegeRules.ExtraDamage(9) == 9
            && upgradedSiege.DynamicVars.Block.IntValue == 5
            && upgradedSiege.DynamicVars.Weak.IntValue == 1
            && new SiegePendingPower().StackType == PowerStackType.Counter
            && new SiegePendingPower().IsVisible
            && RegressionTestHarness.DeclaresMethod<SiegePendingPower>("BeforeSideTurnEnd")
            && RegressionTestHarness.DeclaresMethod<SiegePendingPower>("AfterSideTurnEnd"),
            "Expected Siege to be a zero-cost Uncommon that gains 3-to-5 base Block plus 2 per enemy, resolves its lethal Extra damage before the enemy turn-end synchronization boundary, then applies Weak and removes itself after the native side-turn tick without persistent combat growth.");

        var shade = new Shade();
        var upgradedShade = RegressionTestHarness.MutableForCostTest(new Shade());
        upgradedShade.UpgradeInternal();
        RegressionTestHarness.Require(
            shade.EnergyCost.Canonical == 2
            && shade.Type == CardType.Skill
            && shade.Rarity == CardRarity.Common
            && shade.TargetType == TargetType.AllEnemies
            && shade.CanonicalKeywords.SequenceEqual([SakuraKeywords.Water])
            && SakuraCardModel.HasMagicChargeExtraEffect(shade)
            && shade.DynamicVars.Block.IntValue == 12
            && shade.DynamicVars.Weak.IntValue == 1
            && upgradedShade.DynamicVars.Block.IntValue == 14
            && upgradedShade.DynamicVars.Weak.IntValue == 2,
            "Expected Shade to cost 2, gain 12 Block, apply 1 Weak to all enemies, retain Block with Extra, and upgrade to 14 Block and 2 Weak.");

        var flight = new Flight();
        var upgradedFlight = RegressionTestHarness.MutableForCostTest(new Flight());
        upgradedFlight.UpgradeInternal();
        RegressionTestHarness.Require(
            flight.DynamicVars.Block.IntValue == 6
            && flight.DynamicVars["SakuraTemporaryDexterityPower"].IntValue == 2
            && flight.DynamicVars.Energy.IntValue == 2
            && upgradedFlight.DynamicVars.Block.IntValue == 9
            && upgradedFlight.DynamicVars["SakuraTemporaryDexterityPower"].IntValue == 3
            && upgradedFlight.DynamicVars.Energy.IntValue == 2,
            "Expected Flight to gain 6 Block and 2 Temporary Dexterity, upgrade to 9 Block and 3 Temporary Dexterity, and retain its 2-Energy Extra Effect.");

        var snooze = new Snooze();
        var upgradedSnooze = RegressionTestHarness.MutableForCostTest(new Snooze());
        upgradedSnooze.UpgradeInternal();
        RegressionTestHarness.Require(
            snooze.EnergyCost.Canonical == 1
            && snooze.Type == CardType.Skill
            && snooze.Rarity == CardRarity.Common
            && snooze.TargetType == TargetType.AnyEnemy
            && snooze.CanonicalKeywords.SequenceEqual([SakuraKeywords.Wind])
            && SakuraCardModel.HasMagicChargeExtraEffect(snooze)
            && snooze.DynamicVars.Weak.IntValue == 1
            && snooze.DynamicVars.Vulnerable.IntValue == 1
            && snooze.DynamicVars.Cards.IntValue == 1
            && upgradedSnooze.DynamicVars.Weak.IntValue == 2
            && upgradedSnooze.DynamicVars.Vulnerable.IntValue == 2
            && upgradedSnooze.DynamicVars.Cards.IntValue == 1,
            "Expected Snooze to apply 1 Weak and Vulnerable to one enemy, draw 1, affect all enemies with Extra, and upgrade both debuffs to 2.");

        var breakCard = new Break();
        var upgradedBreak = RegressionTestHarness.MutableForCostTest(new Break());
        upgradedBreak.UpgradeInternal();
        RegressionTestHarness.Require(
            breakCard.DynamicVars.Damage.IntValue == 7
            && upgradedBreak.DynamicVars.Damage.IntValue == 10,
            "Expected Break to deal 7 damage and upgrade to 10 before its conditional Block payoff.");

        var upgradedHail = RegressionTestHarness.MutableForCostTest(new Hail());
        upgradedHail.UpgradeInternal();
        var hailSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Transparent/Hail.cs"));
        RegressionTestHarness.Require(
            new Hail().TargetType == TargetType.AllEnemies
            && new Hail().DynamicVars.Damage.IntValue == 6
            && new Hail().DynamicVars["SakuraFrostbitePower"].IntValue == 1
            && new Hail().DynamicVars["Magic"].IntValue == 5
            && new Hail().DynamicVars["BonusDamage"].IntValue == 2
            && upgradedHail.DynamicVars.Damage.IntValue == 8
            && upgradedHail.DynamicVars["SakuraFrostbitePower"].IntValue == 2
            && !SakuraCardModel.HasMagicChargeExtraEffect(new Hail())
            && HailRules.TotalDamage(new Hail(), spentMagic: 4) == 14
            && HailRules.TotalDamage(upgradedHail, spentMagic: 5) == 18
            && hailSource.Contains("CaptureOpportunity", StringComparison.Ordinal)
            && hailSource.Contains("TryApplyCapturedOpportunity", StringComparison.Ordinal)
            && hailSource.IndexOf("TryApplyCapturedOpportunity", StringComparison.Ordinal)
                < hailSource.IndexOf("SpendUpToMagic", StringComparison.Ordinal)
            && hailSource.Contains("SpendUpToMagic", StringComparison.Ordinal),
            "Expected Hail to lock element state before spending Magic Charge, deal 6/8 damage to all enemies, apply 1/2 Frostbite, and spend up to 5 Magic Charge for +2 damage each.");

        var change = new ClowChange();
        var upgradedChange = RegressionTestHarness.MutableForCostTest(new ClowChange());
        upgradedChange.UpgradeInternal();
        var changeSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/ClowSakura/Change.cs"));
        RegressionTestHarness.Require(
            change.EnergyCost.Canonical == 1
            && change.Rarity == CardRarity.Common
            && change.DynamicVars.Cards.IntValue == 2
            && change.DynamicVars["Magic"].IntValue == 2
            && upgradedChange.Keywords.Contains(CardKeyword.Retain)
            && ChangeRules.DrawCount(discarded: true, baseDraw: 2, spentMagic: 0) == 2
            && ChangeRules.DrawCount(discarded: true, baseDraw: 2, spentMagic: 2) == 4
            && ChangeRules.DrawCount(discarded: false, baseDraw: 2, spentMagic: 2) == 2
            && changeSource.Contains("CaptureOpportunity", StringComparison.Ordinal)
            && changeSource.Contains("TryApplyCapturedOpportunity", StringComparison.Ordinal)
            && changeSource.Contains("SpendUpToMagic", StringComparison.Ordinal),
            "Expected Clow Change to discard up to 1 card for 2 draws, spend up to 2 Magic Charge for +1 draw each, lock element state before spending, and upgrade to Retain.");

        var swing = new Swing();
        var upgradedSwing = RegressionTestHarness.MutableForCostTest(new Swing());
        upgradedSwing.UpgradeInternal();
        var swingPowerSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Powers/Transparent/SwingDamageWindowPower.cs"));
        var swingChinese = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/localization/zhs/cards.json"));
        var swingEnglish = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/localization/eng/cards.json"));
        RegressionTestHarness.Require(
            swing.DynamicVars.Damage.IntValue == 12
            && upgradedSwing.DynamicVars.Damage.IntValue == 16
            && SwingDamageWindowPower.RequestedMultiplier(extraEffect: false) == 2
            && SwingDamageWindowPower.RequestedMultiplier(extraEffect: true) == 3
            && SwingDamageWindowPower.HighestMultiplier(0, 2) == 2
            && SwingDamageWindowPower.HighestMultiplier(2, 2) == 2
            && SwingDamageWindowPower.HighestMultiplier(2, 3) == 3
            && SwingDamageWindowPower.HighestMultiplier(3, 2) == 3
            && RegressionTestHarness.DeclaresMethod<SwingDamageWindowPower>("ModifyDamageMultiplicative")
            && RegressionTestHarness.DeclaresMethod<SwingDamageWindowPower>("AfterSideTurnEnd")
            && swingPowerSource.Contains("props.IsPoweredAttack()", StringComparison.Ordinal)
            && swingPowerSource.Contains("cardSource is { Type: CardType.Attack }", StringComparison.Ordinal)
            && swingPowerSource.Contains("cardSource.Owner?.Creature == owner", StringComparison.Ordinal)
            && swingPowerSource.Contains("GetScaledAmountForMultiplayer", StringComparison.Ordinal)
            && swingChinese.Contains("你的本次伤害和本回合内的后续攻击", StringComparison.Ordinal)
            && swingEnglish.Contains("Your damage this time and later Attacks this turn", StringComparison.Ordinal),
            "Expected Swing to deal 12/16 damage and open a 2x/3x owner-scoped Weak damage window for Powered Attacks.");

        var struggle = new Struggle();
        var upgradedStruggle = RegressionTestHarness.MutableForCostTest(new Struggle());
        upgradedStruggle.UpgradeInternal();
        var struggleSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Transparent/Struggle.cs"));
        RegressionTestHarness.Require(
            struggle.EnergyCost.Canonical == 2
            && !struggle.EnergyCost.CostsX
            && struggle.Type == CardType.Attack
            && struggle.Rarity == CardRarity.Uncommon
            && struggle.CanonicalKeywords.SequenceEqual([SakuraKeywords.Fire])
            && SakuraCardModel.HasMagicChargeExtraEffect(struggle)
            && struggle.DynamicVars.Damage.IntValue == 16
            && struggle.DynamicVars.ExtraDamage.IntValue == 8
            && upgradedStruggle.EnergyCost.GetWithModifiers(CostModifiers.Local) == 2
            && upgradedStruggle.DynamicVars.Damage.IntValue == 20
            && !StruggleRules.IsOtherAttack(struggle, struggle)
            && StruggleRules.IsOtherAttack(struggle, new Struggle())
            && StruggleRules.IsOtherAttack(struggle, new Hail())
            && !StruggleRules.IsOtherAttack(struggle, new Flight())
            && RegressionTestHarness.DeclaresMethod<Struggle>("ModifyDamageAdditive")
            && struggleSource.Contains("cardSource == this", StringComparison.Ordinal)
            && struggleSource.Contains("props.IsPoweredAttack()", StringComparison.Ordinal)
            && struggleSource.Contains("GetPower<StrengthPower>()?.Amount", StringComparison.Ordinal)
            && RegressionTestHarness.DeclaresMethod<Struggle>("AfterCardEnteredCombat")
            && RegressionTestHarness.DeclaresMethod<Struggle>("AfterCardPlayed")
            && swingChinese.Contains("[gold]力量[/gold]对本牌造成伤害的影响翻倍。", StringComparison.Ordinal)
            && swingEnglish.Contains("[gold]Strength[/gold]'s effect on this card's damage is doubled.", StringComparison.Ordinal),
            "Expected Struggle to cost 2, deal 16 damage plus 8 with Extra, receive Strength twice, discount for other Attacks this turn, and upgrade to 20 damage.");

        var blade = new Blade();
        var upgradedBlade = RegressionTestHarness.MutableForCostTest(new Blade());
        upgradedBlade.UpgradeInternal();
        RegressionTestHarness.Require(
            blade.EnergyCost.Canonical == 2
            && blade.DynamicVars.CalculationBase.IntValue == 7
            && blade.DynamicVars.ExtraDamage.IntValue == 2
            && blade.DynamicVars.CalculatedDamage is CalculatedDamageVar
            && blade.DynamicVars["Hits"].IntValue == 2
            && BladeRules.HitCount(blade, 2) == 2
            && BladeRules.DamageBonusCount(0) == 0
            && BladeRules.DamageBonusCount(1) == 0
            && BladeRules.DamageBonusCount(2) == 1
            && BladeRules.DamageBonusCount(3) == 1
            && BladeRules.DamageBonusCount(4) == 2
            && BladeRules.CountsForDamageBonus(new ClowSword())
            && BladeRules.CountsForDamageBonus(new SakuraSword())
            && BladeRules.CountsForDamageBonus(new Blade())
            && !BladeRules.CountsForDamageBonus(new Hail())
            && upgradedBlade.DynamicVars.CalculationBase.IntValue == 8
            && upgradedBlade.DynamicVars.ExtraDamage.IntValue == 4,
            "Expected Blade to cost 2, upgrade from calculated 7 damage plus 2 per Sword or Blade pair to 8 damage plus 4 per pair, and add 2 attacks for Extra.");

        var mirage = new Mirage();
        var upgradedMirage = RegressionTestHarness.MutableForCostTest(new Mirage());
        upgradedMirage.UpgradeInternal();
        RegressionTestHarness.Require(
            mirage.Rarity == CardRarity.Rare
            && mirage.EnergyCost.Canonical == 1
            && upgradedMirage.EnergyCost.GetWithModifiers(CostModifiers.Local) == 1
            && mirage.TargetType == TargetType.AnyEnemy
            && mirage.CanonicalKeywords.Contains(SakuraKeywords.Water)
            && mirage.CanonicalKeywords.Contains(CardKeyword.Exhaust)
            && !mirage.Keywords.Contains(CardKeyword.Retain)
            && upgradedMirage.Keywords.Contains(CardKeyword.Retain)
            && new MiragePower().Type == PowerType.Debuff
            && new MiragePower().StackType == PowerStackType.Single
            && RegressionTestHarness.DeclaresMethod<MiragePower>("ModifyDamageMultiplicative")
            && RegressionTestHarness.DeclaresMethod<MiragePower>("AfterSideTurnEnd")
            && !typeof(MainFile).Assembly.GetTypes().Any(static type => type.Name is "MirageImage" or "MirageImagePower"),
            "Expected Mirage to be a 1-cost Rare that targets enemies, Exhausts, affects all enemies with Extra, and gains Retain on upgrade.");

        var labyrinth = new Labyrinth();
        var upgradedLabyrinth = RegressionTestHarness.MutableForCostTest(new Labyrinth());
        upgradedLabyrinth.UpgradeInternal();
        RegressionTestHarness.Require(
            labyrinth.Type == CardType.Power
            && labyrinth.TargetType == TargetType.None
            && labyrinth.Rarity == CardRarity.Rare
            && labyrinth.CanonicalKeywords.Contains(SakuraKeywords.Earth)
            && !labyrinth.CanonicalKeywords.Contains(CardKeyword.Exhaust)
            && !SakuraCardModel.HasMagicChargeExtraEffect(labyrinth)
            && SakuraCardHoverTips.StaticTipKeys(labyrinth).Contains(SakuraCardHoverTips.LabyrinthTipKey)
            && upgradedLabyrinth.Keywords.Contains(CardKeyword.Retain)
            && new LabyrinthPower().Type == PowerType.Buff
            && new LabyrinthPower().StackType == PowerStackType.Single
            && new LabyrinthIntent().IntentType == IntentType.Stun
            && new LabyrinthIntent().HasIntentTip
            && new LabyrinthIntent().GetAnimation([], null!) == "hidden"
            && new LabyrinthReleaseWarningIntent().HasIntentTip
            && new LabyrinthReleaseWarningIntent().GetAnimation([], null!) == "hidden"
            && RegressionTestHarness.DeclaresMethod<LabyrinthPower>("BeforeCardPlayed")
            && RegressionTestHarness.DeclaresMethod<LabyrinthPower>("AfterCardPlayed")
            && RegressionTestHarness.DeclaresMethod<LabyrinthPower>("ShouldAllowHitting")
            && RegressionTestHarness.DeclaresMethod<LabyrinthPower>("ModifyDamageMultiplicative")
            && !RegressionTestHarness.DeclaresMethod<LabyrinthPower>("ShouldAllowTargeting")
            && !LabyrinthPower.AllowsCardInteraction(new Gale(), isTrapped: true, isAlive: true)
            && !LabyrinthPower.AllowsCardInteraction(new Aqua(), isTrapped: true, isAlive: true)
            && LabyrinthPower.AllowsCardInteraction(new Shade(), isTrapped: true, isAlive: true)
            && LabyrinthPower.AllowsCardInteraction(null, isTrapped: true, isAlive: true)
            && LabyrinthPower.AllowsCardInteraction(new Gale(), isTrapped: false, isAlive: true)
            && LabyrinthPower.AllowsCardInteraction(new Gale(), isTrapped: true, isAlive: false)
            && RegressionTestHarness.DeclaresMethod<LabyrinthPower>("BeforeSideTurnStart")
            && RegressionTestHarness.DeclaresMethod<LabyrinthPower>("AfterPlayerTurnStart")
            && RegressionTestHarness.DeclaresMethod<LabyrinthPower>("AfterSideTurnEnd")
            && RegressionTestHarness.DeclaresMethod<LabyrinthPower>("AfterDeath"),
            "Expected Labyrinth enemies to be unaffected by Attack cards while their turns are suppressed, with one enemy released at each eligible turn end and Retain gained on upgrade.");
    }

    [Fact]
    public void BlankGrantsForgottenAndDrawsNextTurnForNewTargets()
    {
        var blank = new Blank();
        var upgradedBlank = RegressionTestHarness.MutableForCostTest(new Blank());
        upgradedBlank.UpgradeInternal();

        RegressionTestHarness.Require(
            blank.EnergyCost.Canonical == 1
            && blank.Rarity == CardRarity.Rare
            && blank.Type == CardType.Skill
            && blank.TargetType == TargetType.Self
            && blank.CanonicalKeywords.SequenceEqual([SakuraKeywords.Earth, CardKeyword.Exhaust])
            && !blank.GainsBlock
            && upgradedBlank.EnergyCost.GetWithModifiers(CostModifiers.Local) == 0,
            "Expected Blank to be a 1-cost Rare Earth Exhaust card that upgrades to 0 cost without Block.");
        RegressionTestHarness.Require(
            Blank.TargetPileTypes.SequenceEqual([PileType.Hand, PileType.Draw, PileType.Discard])
            && Blank.CanGainForgotten(CardType.Status, isForgotten: false)
            && Blank.CanGainForgotten(CardType.Curse, isForgotten: false)
            && !Blank.CanGainForgotten(CardType.Status, isForgotten: true)
            && !Blank.CanGainForgotten(CardType.Skill, isForgotten: false),
            "Expected Blank to grant Forgotten once to Status and Curse cards in hand, draw, and discard only.");

        var blankSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Transparent/Blank.cs"));
        RegressionTestHarness.Require(
            blankSource.Contains("SakuraForgotten.GrantTemporary(choiceContext, card)", StringComparison.Ordinal)
            && blankSource.Contains("PowerCmd.Apply<DrawCardsNextTurnPower>", StringComparison.Ordinal)
            && blankSource.Contains("EnergyCost.UpgradeBy(-1)", StringComparison.Ordinal)
            && !blankSource.Contains("GainBlock", StringComparison.Ordinal)
            && blankSource.IndexOf("PowerCmd.Apply<DrawCardsNextTurnPower>", StringComparison.Ordinal)
                < blankSource.IndexOf("if (activation.IsActive)", StringComparison.Ordinal),
            "Expected Blank's ordinary effect to use the shared Forgotten grant path and apply vanilla next-turn draw before the Extra branch.");

        var chineseCards = File.ReadAllText(RegressionTestHarness.FindRepoFile("SakuraMod/localization/zhs/cards.json"));
        var englishCards = File.ReadAllText(RegressionTestHarness.FindRepoFile("SakuraMod/localization/eng/cards.json"));
        RegressionTestHarness.Require(
            chineseCards.Contains("状态牌与诅咒牌获得[red]遗忘[/red]", StringComparison.Ordinal)
            && chineseCards.Contains("下回合额外抽 1 张牌", StringComparison.Ordinal)
            && !chineseCards.Contains("SAKURA_MOD_CARD_BLANK.description\": \"获得 {Block:diff()}", StringComparison.Ordinal)
            && englishCards.Contains("Status and Curse cards gain [red]Forgotten[/red]", StringComparison.Ordinal)
            && englishCards.Contains("draw 1 additional card next turn", StringComparison.Ordinal)
            && !englishCards.Contains("SAKURA_MOD_CARD_BLANK.description\": \"Gain {Block:diff()}", StringComparison.Ordinal),
            "Expected both localizations to describe Blank's Forgotten and next-turn draw effects without Block.");
    }

    [Fact]
    public void RewindSelectsMoreCardsOnUpgradeInsteadOfCostReduction()
    {
        var rewind = new Rewind();
        var upgradedRewind = RegressionTestHarness.MutableForCostTest(new Rewind());
        upgradedRewind.UpgradeInternal();

        RegressionTestHarness.Require(
            rewind.EnergyCost.Canonical == 1
            && rewind.Rarity == CardRarity.Rare
            && rewind.CanonicalKeywords.SequenceEqual([SakuraKeywords.Earth, CardKeyword.Exhaust])
            && rewind.DynamicVars.Cards.IntValue == 1
            && upgradedRewind.EnergyCost.GetWithModifiers(CostModifiers.Local) == 1
            && upgradedRewind.DynamicVars.Cards.IntValue == 2,
            "Expected Rewind to stay 1-cost on upgrade and select 1 then 2 exhaust cards.");

        var rewindSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Transparent/Rewind.cs"));
        RegressionTestHarness.Require(
            rewindSource.Contains("SelectUpToFromCards", StringComparison.Ordinal)
            && rewindSource.Contains("MoveExistingCardToHand", StringComparison.Ordinal)
            && rewindSource.Contains("SetToFreeThisTurn", StringComparison.Ordinal)
            && rewindSource.Contains("DynamicVars.Cards.UpgradeValueBy(1)", StringComparison.Ordinal)
            && !rewindSource.Contains("EnergyCost.UpgradeBy(-1)", StringComparison.Ordinal),
            "Expected Rewind upgrade to increase exhaust selection count while keeping Extra free-this-turn.");

        var chineseCards = File.ReadAllText(RegressionTestHarness.FindRepoFile("SakuraMod/localization/zhs/cards.json"));
        var englishCards = File.ReadAllText(RegressionTestHarness.FindRepoFile("SakuraMod/localization/eng/cards.json"));
        RegressionTestHarness.Require(
            chineseCards.Contains("选择[gold]消耗堆[/gold]中的 {Cards:diff()} 张牌放入手牌", StringComparison.Ordinal)
            && englishCards.Contains("Choose {Cards:diff()} card(s) from your [gold]exhaust pile[/gold]", StringComparison.Ordinal),
            "Expected Rewind localizations to gold the exhaust pile and use Cards:diff().");
    }

    [Fact]
    public void GenerationRecoveryFreezeAndTimeCardContractsRemainStable()
    {
        var kindness = new Kindness();
        var upgradedKindness = RegressionTestHarness.MutableForCostTest(new Kindness());
        upgradedKindness.UpgradeInternal();
        RegressionTestHarness.Require(
            kindness.Rarity == CardRarity.Rare
            && kindness.CanonicalKeywords.Contains(SakuraKeywords.Earth)
            && kindness.CanonicalKeywords.Contains(CardKeyword.Exhaust)
            && SakuraCardModel.HasMagicChargeExtraEffect(kindness)
            && kindness.EnergyCost.Canonical == 1
            && upgradedKindness.EnergyCost.GetWithModifiers(CostModifiers.Local) == 0
            && new KindnessPower().Type == PowerType.Buff
            && new KindnessPower().StackType == PowerStackType.Counter
            && new KindnessPower().InstanceType == PowerInstanceType.Instanced
            && RegressionTestHarness.DeclaresMethod<KindnessPower>("ModifyCardPlayResultPileTypeAndPosition")
            && RegressionTestHarness.DeclaresMethod<KindnessPower>("AfterModifyingCardPlayResultPileOrPosition")
            && RegressionTestHarness.DeclaresMethod<KindnessPower>("AfterCardPlayed")
            && RegressionTestHarness.DeclaresMethod<KindnessPower>("RegisterPendingEffect"),
            "Expected Kindness to return the next Exhausted card, set an Extra-returned card to 0 cost for this turn, and upgrade from 1 to 0 cost.");

        var appear = new Appear();
        var upgradedAppear = RegressionTestHarness.MutableForCostTest(new Appear());
        upgradedAppear.UpgradeInternal();
        RegressionTestHarness.Require(
            appear.Rarity == CardRarity.Common
            && appear.CanonicalKeywords.Contains(SakuraKeywords.Wind)
            && appear.CanonicalKeywords.Contains(SakuraKeywords.Manifest)
            && SakuraCardModel.HasMagicChargeExtraEffect(appear)
            && SakuraTransparentCardCatalog.TransparentCardTypes.Count == 36
            && SakuraTransparentCardCatalog.TransparentCardTypes.Contains(typeof(Appear))
            && appear.EnergyCost.Canonical == 0
            && upgradedAppear.EnergyCost.GetWithModifiers(CostModifiers.Local) == 0
            && !appear.IsUpgraded
            && upgradedAppear.IsUpgraded,
            "Expected Appear to cost 0, choose a Temporary Clear Card copy, upgrade the copy when upgraded, and keep Extra Effect.");

        RegressionTestHarness.Require(
            new SakuraDream().EnergyCost.Canonical == 0,
            "Expected Sakura Dream to cost 0.");

        var promise = new Promise();
        var upgradedPromise = RegressionTestHarness.MutableForCostTest(new Promise());
        upgradedPromise.UpgradeInternal();
        RegressionTestHarness.Require(
            promise.CanonicalKeywords.Contains(SakuraKeywords.Earth)
            && !promise.CanonicalKeywords.Contains(SakuraKeywords.Manifest)
            && SakuraCardModel.HasMagicChargeExtraEffect(promise)
            && promise.DynamicVars.Block.IntValue == 8
            && promise.DynamicVars["PromiseManifestPower"].IntValue == 1
            && promise.DynamicVars["PlatingPower"].IntValue == 4
            && upgradedPromise.DynamicVars.Block.IntValue == 8
            && upgradedPromise.DynamicVars["PromiseManifestPower"].IntValue == 2
            && new PromiseManifestPower().StackType == PowerStackType.Counter
            && RegressionTestHarness.DeclaresMethod<PromiseManifestPower>("ModifyHandDraw")
            && RegressionTestHarness.DeclaresMethod<PromiseManifestPower>("AfterEnergyReset")
            && RegressionTestHarness.DeclaresMethod<PromiseManifestPower>("AfterPlayerTurnStart"),
            "Expected Promise to gain 8 Block, grant 4 Plating with Extra, and upgrade its safe-next-turn reward from 1 to 2 draw and Energy.");

        var dreaming = new Dreaming();
        var upgradedDreaming = RegressionTestHarness.MutableForCostTest(new Dreaming());
        upgradedDreaming.UpgradeInternal();
        var dreamingPowerSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Powers/Transparent/DreamingPower.cs"));
        var sakuraActionsSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/SakuraActions.cs"));
        RegressionTestHarness.Require(
            dreaming.Type == CardType.Power
            && dreaming.Rarity == CardRarity.Rare
            && dreaming.CanonicalKeywords.Contains(SakuraKeywords.Water)
            && !dreaming.CanonicalKeywords.Contains(SakuraKeywords.Manifest)
            && !SakuraCardModel.HasMagicChargeExtraEffect(dreaming)
            && dreaming.EnergyCost.Canonical == 2
            && upgradedDreaming.EnergyCost.GetWithModifiers(CostModifiers.Local) == 2
            && upgradedDreaming.Keywords.Contains(CardKeyword.Innate)
            && RegressionTestHarness.DeclaresMethod<DreamingPower>("BeforeHandDrawLate")
            && !RegressionTestHarness.DeclaresMethod<DreamingPower>("BeforeHandDraw")
            && dreamingPowerSource.Contains("ChooseAndMoveDrawPileCardToTop", StringComparison.Ordinal)
            && sakuraActionsSource.Contains("SelectFromCardPreviews(owner, context, drawPile.Cards", StringComparison.Ordinal)
            && !dreamingPowerSource.Contains("lookCount: 5", StringComparison.Ordinal),
            "Expected Dreaming to choose from the full draw pile after ordinary pre-draw hooks, stay at 2 cost, and gain Innate on upgrade.");

        var upgradedClowFreeze = RegressionTestHarness.MutableForCostTest(new ClowFreeze());
        upgradedClowFreeze.UpgradeInternal();
        RegressionTestHarness.Require(
            new ClowFreeze().DynamicVars.Damage.IntValue == 14
            && new ClowFreeze().DynamicVars.Block.IntValue == 6
            && new ClowFreeze().DynamicVars["SakuraFrostbitePower"].IntValue == 2
            && SakuraFreezeRules.DoubledApplicationAmount(0, 2) == 4
            && SakuraFreezeRules.DoubledApplicationAmount(5, 2) == 9
            && upgradedClowFreeze.DynamicVars.Damage.IntValue == 18
            && upgradedClowFreeze.DynamicVars.Block.IntValue == 8,
            "Expected Clow Freeze to deal 14 damage, gain 6 Block, apply 2 Frostbite, atomically double Frostbite with Extra, and upgrade to 18/8.");

        var sakuraFreeze = new SakuraFreeze();
        RegressionTestHarness.Require(
            sakuraFreeze.DynamicVars.Damage.IntValue == 22
            && sakuraFreeze.DynamicVars.Block.IntValue == 10
            && sakuraFreeze.DynamicVars["SakuraFrostbitePower"].IntValue == 3
            && sakuraFreeze.DynamicVars["ExtraFrostbite"].IntValue == 3
            && SakuraFreezeRules.FrostbiteAmount(3, 3, isEliteOrBossTarget: true) == 3
            && SakuraFreezeRules.FrostbiteAmount(3, 3, isEliteOrBossTarget: false) == 6,
            "Expected Sakura Freeze to apply 3 Frostbite to Elite or Boss targets and 6 to other targets.");

        var time = new Time();
        var upgradedTime = RegressionTestHarness.MutableForCostTest(new Time());
        upgradedTime.UpgradeInternal();
        var clowTime = new ClowTime();
        var upgradedClowTime = RegressionTestHarness.MutableForCostTest(new ClowTime());
        upgradedClowTime.UpgradeInternal();
        var transparentTimeSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Transparent/Time.cs"));
        var timePairSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/ClowSakura/Time.cs"));
        var clowTimeSource = timePairSource[..timePairSource.IndexOf(
            "public class SakuraTime",
            StringComparison.Ordinal)];
        var sakuraTimeSource = timePairSource[timePairSource.IndexOf(
            "public class SakuraTime",
            StringComparison.Ordinal)..];
        RegressionTestHarness.Require(
            time.TargetType == TargetType.Self
            && time.CanonicalKeywords.Contains(SakuraKeywords.Fire)
            && time.CanonicalKeywords.Contains(CardKeyword.Exhaust)
            && time.EnergyCost.Canonical == 3
            && upgradedTime.EnergyCost.GetWithModifiers(CostModifiers.Local) == 2
            && clowTime.TargetType == TargetType.None
            && clowTime.Elements == SakuraElementSet.Water
            && clowTime.CanonicalKeywords.Contains(CardKeyword.Exhaust)
            && !clowTime.CanonicalKeywords.Contains(CardKeyword.Retain)
            && clowTime.EnergyCost.Canonical == 1
            && upgradedClowTime.Keywords.Contains(CardKeyword.Retain)
            && upgradedClowTime.EnergyCost.GetWithModifiers(CostModifiers.Local) == 1
            && !clowTimeSource.Contains("AddVoid", StringComparison.Ordinal)
            && transparentTimeSource.Contains("PlayerCmd.EndTurn", StringComparison.Ordinal)
            && !clowTimeSource.Contains("PlayerCmd.EndTurn", StringComparison.Ordinal)
            && !sakuraTimeSource.Contains("PlayerCmd.EndTurn", StringComparison.Ordinal)
            && RegressionTestHarness.DeclaresMethod<TimeStopPower>("PreserveCurrentTurnState")
            && RegressionTestHarness.DeclaresMethod<TimeStopPower>("ShouldFlush")
            && RegressionTestHarness.DeclaresMethod<TimeStopPower>("ShouldClearBlock")
            && RegressionTestHarness.DeclaresMethod<TimeStopPower>("ShouldPlayerResetEnergy")
            && RegressionTestHarness.DeclaresMethod<SakuraElementStatePower>("PreserveForNextTurn"),
            "Expected only Transparent Time to end the turn; Clow Time should preserve hand/resources without ending it, add no Void, gain Retain when upgraded, and avoid Exhaust on Extra.");
    }

    [Fact]
    public void ReflectionPowerLifecycleContractsRemainStable()
    {
        var reflectionSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Powers/Transparent/ReflectionPower.cs"));
        RegressionTestHarness.Require(
            RegressionTestHarness.DeclaresMethod<ReflectionPower>("AfterDamageReceived")
            && RegressionTestHarness.DeclaresMethod<ReflectionPower>("AfterSideTurnEnd")
            && reflectionSource.Contains(
                "var reflectionDamage = ReflectedDamage(damageResult.TotalDamage, (int)Amount);",
                StringComparison.Ordinal)
            && reflectionSource.Contains("damageProps.IsPoweredAttack()", StringComparison.Ordinal)
            && reflectionSource.Contains(
                "side == CombatSide.Enemy && Owner.Side == CombatSide.Player && Amount > 0",
                StringComparison.Ordinal)
            && System.Text.RegularExpressions.Regex.Matches(
                reflectionSource,
                "PowerCmd\\.Decrement\\(this\\)").Count == 2,
            "Expected ReflectionPower to reflect each powered damage result, consume one stack per hit, and decay one stack at enemy turn end.");
    }

    [Fact]
    public void ElementCostAndExtraEffectStateContractsRemainStable()
    {
        RegressionTestHarness.Require(
            (SakuraElementSet.Wind | SakuraElementSet.Fire).HasElement(SakuraElement.Wind)
            && (SakuraElementSet.Wind | SakuraElementSet.Fire).HasElement(SakuraElement.Fire),
            "Expected Sakura element sets to retain each declared element identity.");

        RegressionTestHarness.RequireNoRemovedCardTypes(
            "registered Sakura power types",
            RegressionTestData.RegisteredPowerTypes,
            RegressionTestData.RemovedClearElementPowerTypeNames);
        RegressionTestHarness.Require(
            !typeof(MainFile).Assembly.GetTypes().Any(static type => type.Name == "SakuraElementCompass"),
            "Expected the old Clear element compass UI type to be removed.");

        var reflect = new Reflect();
        var upgradedReflect = RegressionTestHarness.MutableForCostTest(new Reflect());
        upgradedReflect.UpgradeInternal();
        RegressionTestHarness.Require(
            reflect.EnergyCost.Canonical == 1
            && reflect.Rarity == CardRarity.Common
            && reflect.CanonicalKeywords.SequenceEqual([SakuraKeywords.Water])
            && reflect.DynamicVars.Block.IntValue == 5
            && upgradedReflect.DynamicVars.Block.IntValue == 8
            && reflect.DynamicVars["ReflectionPower"].IntValue == 2
            && upgradedReflect.DynamicVars["ReflectionPower"].IntValue == 3
            && reflect.DynamicVars["ExtraReflection"].IntValue == 2
            && SakuraCardHoverTips.StaticTipKeys(reflect).Contains(SakuraCardHoverTips.ReflectionTipKey)
            && SakuraCardHoverTips.StaticTipKeys(upgradedReflect).Contains(SakuraCardHoverTips.ReflectionTipKey)
            && new ReflectionPower().StackType == PowerStackType.Counter
            && ReflectionPower.ReflectedDamage(17, 1) == 8
            && ReflectionPower.ReflectedDamage(17, 2) == 17
            && ReflectionPower.ReflectedDamage(17, 3) == 25
            && ReflectionPower.ReflectedDamage(10, 3) == 15
            && ReflectionPower.ReflectedDamage(10, 2) == 10
            && ReflectionPower.ReflectedDamage(10, 1) == 5
            && !typeof(MainFile).Assembly.GetTypes().Any(static type => type.Name == "StrongReflectionPower"),
            "Expected Reflect to be a Common 1-cost card with 5/8 Block, 2/3 scaling Reflection stacks, and 2 additional stacks through Extra.");

        var clowTwin = new ClowTwin();
        var clowTwinLocalCost = clowTwin.EnergyCost.GetWithModifiers(CostModifiers.Local);
        RegressionTestHarness.Require(
            RegressionTestHarness.DeclaresMethod<ClassicTwinPower>("AfterModifyingCardPlayCount"),
            "Expected Clow Twin to consume its per-turn duplication only after the native play-count query is committed.");
        RegressionTestHarness.Require(
            ClassicTwinSakuraPower.TryIncreaseClowCardCost(clowTwin, amount: 1, clowTwinLocalCost, out var increasedTwinCost),
            "Expected Sakura Twin power to increase Clow card costs.");
        RegressionTestHarness.Require(increasedTwinCost == clowTwinLocalCost + 1, "Expected Sakura Twin power to add only 1 cost per stack.");
        RegressionTestHarness.Require(
            clowTwin.EnergyCost.GetWithModifiers(CostModifiers.Local) == clowTwinLocalCost,
            "Expected Sakura Twin power cost calculation not to mutate local combat cost.");

        RegressionTestHarness.Require(
            ClassicTwinSakuraPower.TryIncreaseClowCardCost(clowTwin, amount: 2, clowTwinLocalCost, out var stackedTwinCost),
            "Expected stacked Sakura Twin power to increase Clow card costs.");
        RegressionTestHarness.Require(stackedTwinCost == clowTwinLocalCost + 2, "Expected Sakura Twin stacked cost increase to match its stack amount.");
        RegressionTestHarness.Require(
            !ClassicTwinSakuraPower.TryIncreaseClowCardCost(new SakuraTwin(), amount: 1, currentCost: 1, out var sakuraTwinCost),
            "Expected Sakura Twin power not to increase Sakura card costs.");
        RegressionTestHarness.Require(sakuraTwinCost == 1, "Expected non-Clow card cost to remain unchanged.");

        var dreamGeneratedThrough = RegressionTestHarness.MutableForCostTest(new ClowThrough());
        SakuraMagicCharge.SetFreeForRestOfTurn(dreamGeneratedThrough);
        RegressionTestHarness.Require(
            dreamGeneratedThrough.EnergyCost.GetWithModifiers(CostModifiers.Local) == 0,
            "Expected Dream-generated Clow cards to be free this turn.");
        RegressionTestHarness.Require(
            !dreamGeneratedThrough.EnergyCost.AfterCardPlayedCleanup(),
            "Expected Dream-generated free cost not to expire during card-play cleanup.");
        RegressionTestHarness.Require(
            dreamGeneratedThrough.EnergyCost.GetWithModifiers(CostModifiers.Local) == 0,
            "Expected Dream-generated free cost to stay stable until end of turn.");
        RegressionTestHarness.Require(
            dreamGeneratedThrough.EnergyCost.EndOfTurnCleanup(),
            "Expected Dream-generated free cost to expire at end of turn.");
        RegressionTestHarness.Require(
            dreamGeneratedThrough.EnergyCost.GetWithModifiers(CostModifiers.Local) == dreamGeneratedThrough.EnergyCost.Canonical,
            "Expected Dream-generated card cost to return to canonical after end-of-turn cleanup.");

        RegressionTestHarness.Require(
            SakuraSourceCardText.ShouldShowMagicChargeExtraDescription(new ClowSword()),
            "Expected canonical Classic Clow cards to show complete Extra Effect reference text without reading Owner.");
        RegressionTestHarness.Require(
            SakuraCardModel.ShouldShowMagicChargeExtraEffectDescription(new Gale()),
            "Expected canonical Transparent Cards to show complete Extra Effect reference text without reading Owner.");
        RegressionTestHarness.Require(
            SakuraCardModel.HasMagicChargeExtraEffect(new Gale()),
            "Expected Gale to retain its Magic Charge extra effect.");
        RegressionTestHarness.Require(
            SakuraCardModel.HasMagicChargeExtraEffect(new Exchange())
            && !SakuraCardModel.HasMagicChargeExtraEffect(new Synchronize())
            && SakuraCardModel.HasMagicChargeExtraEffect(new Remind()),
            "Expected Exchange and Remind to support Extra effects while Synchronize remains a non-Extra card.");
    }

    [Fact]
    public void ClassicAndClearHoverTextContractsRemainStable()
    {
        RegressionTestHarness.Require(
            SakuraSourceCardText.CounterpartPreviewIdentity(new ClowSword()) == SourceCardIdentity.Sword,
            "Expected Clow hover tips to request the matching Sakura counterpart preview.");

        RegressionTestHarness.Require(
            SakuraSourceCardText.GeneratedSpellPreviewType(new ClowEarthy()) == typeof(SpellLeiDi)
            && SakuraSourceCardText.GeneratedSpellPreviewType(new ClowFirey()) == typeof(SpellHuoShen)
            && SakuraSourceCardText.GeneratedSpellPreviewType(new ClowWatery()) == typeof(SpellShuiLong)
            && SakuraSourceCardText.GeneratedSpellPreviewType(new ClowWindy()) == typeof(SpellFengHua)
            && SakuraSourceCardText.GeneratedSpellPreviewType(new ClowSword()) is null,
            "Expected the four Clow element-state cards to preview only their generated spell cards.");
        RegressionTestHarness.Require(
            new ClowEarthy().CanonicalKeywords.Contains(CardKeyword.Retain)
            && new ClowFirey().CanonicalKeywords.Contains(CardKeyword.Retain)
            && new ClowWatery().CanonicalKeywords.Contains(CardKeyword.Retain)
            && new ClowWindy().CanonicalKeywords.Contains(CardKeyword.Retain),
            "Expected the four Clow element-state cards to retain by default.");

        RegressionTestHarness.Require(
            SakuraSourceCardText.StaticTipKeys(new SpellHuoShen()).Count() >= 2,
            "Expected Classic spell hover tips to include spell-family and element-family tips.");
        RegressionTestHarness.Require(
            SakuraSourceCardText.StaticTipKeys(new ClowShield()).Contains("SAKURAMOD-FIREY_CARD")
            && !SakuraSourceCardText.ElementStatesReferencedBy(new ClowShield()).Any(),
            "Expected ordinary Classic element cards to show element context without adding element-state tips.");
        RegressionTestHarness.Require(
            SakuraSourceCardText.ElementStatesReferencedBy(new ClowFirey()).Contains(SakuraElement.Fire),
            "Expected Classic cards that explicitly enter an element state to explain that state.");
        RegressionTestHarness.Require(
            SakuraSourceCardText.ElementStatesReferencedBy(new SakuraWave()).Count() == 4,
            "Expected Sakura Wave hover text to reference all four Classic element states.");
        RegressionTestHarness.Require(
            SakuraSourceCardText.ReferencesMagicChargeTip(new ClowLock())
            && SakuraSourceCardText.ReferencesMagicChargeTip(new ClowChange()),
            "Expected Clow Lock and Change hover tips to reference Magic Charge.");
        RegressionTestHarness.Require(
            SakuraSourceCardText.KeywordTips(new SakuraShield()).Contains(SakuraKeywords.SakuraCard),
            "Expected Sakura card hover tips to explain Sakura Card void generation.");
        RegressionTestHarness.Require(
            SakuraSourceCardText.KeywordTips(new ClowFreeze()).Contains(SakuraKeywords.Frostbite),
            "Expected Clow Freeze hover tips to explain Frostbite.");
        RegressionTestHarness.Require(
            SakuraSourceCardText.KeywordTips(new ClowCreate()).Contains(SakuraKeywords.Removable)
            && SakuraSourceCardText.KeywordTips(new ClowCreate()).Contains(SakuraKeywords.EntityLimited),
            "Expected Clow Create hover tips to explain generated card removal and entity limits.");
        RegressionTestHarness.Require(
            SakuraSourceCardText.KeywordTips(new SakuraReturn()).Contains(SakuraKeywords.Removable),
            "Expected Sakura Return hover tips to explain that the card is removed from the deck after use.");
        RegressionTestHarness.Require(
            SakuraSourceCardText.ReferencesSleepTip(new ClowSleep())
            && SakuraSourceCardText.ReferencesSleepTip(new SakuraSleep()),
            "Expected Clow Sleep and Sakura Sleep hover tips to explain the Sleep power.");
        RegressionTestHarness.Require(
            SakuraSourceCardText.ReferencesBlurTip(new ClowShadow())
            && !SakuraSourceCardText.ReferencesBlurTip(new SakuraShadow()),
            "Expected Clow Shadow hover tips to explain Blur, but not Sakura Shadow.");
        RegressionTestHarness.Require(
            SakuraSourceCardText.ReferencesIntangibleTip(new SakuraShadow())
            && !SakuraSourceCardText.ReferencesIntangibleTip(new ClowShadow()),
            "Expected Sakura Shadow hover tips to explain Intangible, but not Clow Shadow.");
        RegressionTestHarness.Require(
            SakuraSourceCardText.ReferencesBufferTip(new ClowSilent())
            && SakuraSourceCardText.ReferencesBufferTip(new SakuraSilent()),
            "Expected Clow Silent and Sakura Silent hover tips to explain Buffer.");
        RegressionTestHarness.Require(
            SakuraSourceCardText.CounterpartPreviewIdentity(new ClowVoice()) == SourceCardIdentity.Voice
            && SakuraSourceCardText.StaticTipKeys(new ClowVoice()).Any()
            && SakuraSourceCardText.KeywordTips(new ClowVoice()).Count() >= 2,
            "Expected Clow Voice hover tips to include element, counterpart, Invisible, and Echo context.");

        RegressionTestHarness.Require(
            SakuraDescriptionRegion.IsExtraEffectDescriptionLineForTests("[gold]额外效果：[/gold]抽 1 张牌。"),
            "Expected the shared description region to recognize Chinese extra-effect description headers.");
        RegressionTestHarness.Require(
            SakuraDescriptionRegion.IsExtraEffectDescriptionLineForTests("[gold]Extra:[/gold] Draw 1 card."),
            "Expected the shared description region to recognize English extra-effect description headers.");
        RegressionTestHarness.Require(
            !SakuraDescriptionRegion.IsExtraEffectDescriptionLineForTests("[gold]解封：[/gold]抽 1 张牌。"),
            "Expected old Chinese Release headers not to be treated as current extra-effect headers.");
        RegressionTestHarness.Require(
            !SakuraDescriptionRegion.IsExtraEffectDescriptionLineForTests("[gold]Release:[/gold] Draw 1 card."),
            "Expected old English Release headers not to be treated as current extra-effect headers.");
    }

    [Fact]
    public void ClassicShieldAndWoodPowerContractsRemainStable()
    {
        RegressionTestHarness.Require(
            RegressionTestHarness.DeclaresMethod<ClassicShieldWardPower>("BeforeSideTurnEnd"),
            "Expected Metallicize to gain Block from the end-of-turn hook.");
        RegressionTestHarness.Require(
            !RegressionTestHarness.DeclaresMethod<ClassicShieldWardPower>("AfterPlayerTurnStart"),
            "Expected Metallicize not to gain Block from the start-of-turn hook.");

        var clowShield = new ClowShield();
        var upgradedClowShield = RegressionTestHarness.MutableForCostTest(new ClowShield());
        upgradedClowShield.UpgradeInternal();
        RegressionTestHarness.Require(
            clowShield.DynamicVars["ClassicShieldWardPower"].IntValue == 3
            && upgradedClowShield.DynamicVars["ClassicShieldWardPower"].IntValue == 3,
            "Expected Clow Shield Extra to grant 3 Metallicize without changing on upgrade.");

        var sakuraShield = new SakuraShield();
        RegressionTestHarness.Require(
            sakuraShield.DynamicVars.Block.IntValue == 14
            && sakuraShield.DynamicVars["Magic"].IntValue == 25
            && SakuraShield.CurrentHpBlock(80, 25) == 20
            && SakuraShield.CurrentHpBlock(79, 25) == 19,
            "Expected Sakura Shield to gain 14 Block plus 25% of current HP, rounded down.");

        var clowWood = new ClowWood();
        var upgradedClowWood = RegressionTestHarness.MutableForCostTest(new ClowWood());
        upgradedClowWood.UpgradeInternal();
        var sakuraWood = new SakuraWood();
        RegressionTestHarness.Require(
            clowWood.Type == CardType.Power
            && clowWood.TargetType == TargetType.None
            && clowWood.Rarity == CardRarity.Uncommon
            && clowWood.EnergyCost.Canonical == 1
            && clowWood.DynamicVars["ThornsPower"].IntValue == 2
            && clowWood.DynamicVars["PoisonPower"].IntValue == 3
            && clowWood.DynamicVars["StrengthLoss"].IntValue == 2
            && upgradedClowWood.DynamicVars["ThornsPower"].IntValue == 4
            && upgradedClowWood.DynamicVars["PoisonPower"].IntValue == 3
            && upgradedClowWood.DynamicVars["StrengthLoss"].IntValue == 2
            && !SakuraCardModel.HasMagicChargeExtraEffect(clowWood)
            && sakuraWood.Type == CardType.Power
            && sakuraWood.TargetType == TargetType.None
            && sakuraWood.Rarity == CardRarity.Token
            && sakuraWood.EnergyCost.Canonical == 1
            && sakuraWood.DynamicVars["ThornsPower"].IntValue == 4
            && sakuraWood.DynamicVars["PoisonPower"].IntValue == 2
            && sakuraWood.DynamicVars["StrengthLoss"].IntValue == 4
            && new ClassicWoodPower().Type == PowerType.Buff
            && new ClassicSakuraWoodPower().Type == PowerType.Buff
            && RegressionTestHarness.DeclaresMethod<ClassicWoodPower>("BeforeSideTurnStart")
            && RegressionTestHarness.DeclaresMethod<PoisonPower>("AfterSideTurnStart"),
            "Expected Clow Wood to seed 3 Poison or reduce Strength, Sakura Wood to apply 2 Poison and reduce Strength before native Poison resolves, and both cards to use the requested costs and types.");
    }

    [Fact]
    public void SakuraLightTurnsStatusAndCurseCardsIntoAPlayEngineWithoutChangingTheirCosts()
    {
        var card = new SakuraLight();

        RegressionTestHarness.Require(
            card.Type == CardType.Power
            && card.EnergyCost.Canonical == 1
            && ClassicLightSakuraPower.IsStatusOrCurse(CardType.Status)
            && ClassicLightSakuraPower.IsStatusOrCurse(CardType.Curse)
            && !ClassicLightSakuraPower.IsStatusOrCurse(CardType.Skill)
            && RegressionTestHarness.DeclaresMethod<ClassicLightSakuraPower>("TryModifyKeywordsInCombat")
            && RegressionTestHarness.DeclaresMethod<ClassicLightSakuraPower>("ModifyCardPlayResultPileTypeAndPosition")
            && RegressionTestHarness.DeclaresMethod<ClassicLightSakuraPower>("AfterCardPlayed")
            && !RegressionTestHarness.DeclaresMethod<ClassicLightSakuraPower>("TryModifyEnergyCostInCombat"),
            "Expected Sakura Light to be a 1-cost Power that makes Status and Curse cards playable and Exhausting, then draws and heals without changing their costs.");
    }

    [Fact]
    public void SakuraLibraConvertsEveryThreeMagicChargeIntoEnergy()
    {
        var card = new SakuraLibra();

        RegressionTestHarness.Require(
            card.EnergyCost.Canonical == 0
            && card.DynamicVars.Block.IntValue == 3
            && card.DynamicVars["Magic"].IntValue == 3
            && card.DynamicVars["Energy"].IntValue == 1
            && SakuraLibra.EnergyFromCharge(-1) == 0
            && SakuraLibra.EnergyFromCharge(0) == 0
            && SakuraLibra.EnergyFromCharge(2) == 0
            && SakuraLibra.EnergyFromCharge(3) == 1
            && SakuraLibra.EnergyFromCharge(8) == 2
            && SakuraLibra.EnergyFromCharge(9) == 3,
            "Expected Sakura Libra to cost 0, gain 3 Block per Magic Charge, and gain 1 Energy for every 3 Magic Charge spent.");
    }

    [Fact]
    public void ChoiceAndAppearKeepManifestedCombatCostContracts()
    {
        var choice = new Choice();
        var upgradedChoice = RegressionTestHarness.MutableForCostTest(new Choice());
        upgradedChoice.UpgradeInternal();
        var choiceSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Transparent/Choice.cs"));
        var appearSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Transparent/Appear.cs"));
        var chineseCards = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/localization/zhs/cards.json"));
        var englishCards = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/localization/eng/cards.json"));

        RegressionTestHarness.Require(
            choice.EnergyCost.Canonical == 0
            && choice.Rarity == CardRarity.Rare
            && choice.DynamicVars["ManifestCards"].IntValue == 1
            && choice.DynamicVars["DrawCards"].IntValue == 2
            && upgradedChoice.DynamicVars["ManifestCards"].IntValue == 2
            && upgradedChoice.DynamicVars["DrawCards"].IntValue == 3
            && choiceSource.Contains("if (activation.IsActive)", StringComparison.Ordinal)
            && choiceSource.Contains("BaseValue", StringComparison.Ordinal)
            && choiceSource.Contains("Manifest(choiceContext", StringComparison.Ordinal)
            && choiceSource.Contains("Draw(choiceContext", StringComparison.Ordinal)
            && choiceSource.Contains("SetThisCombat", StringComparison.Ordinal)
            && appearSource.Contains("SetThisCombat(0, reduceOnly: true)", StringComparison.Ordinal)
            && appearSource.Contains("upgraded: IsUpgraded", StringComparison.Ordinal)
            && chineseCards.Contains("所显现的牌本场战斗内能耗减少 1", StringComparison.Ordinal)
            && chineseCards.Contains("执行上述两项", StringComparison.Ordinal)
            && chineseCards.Contains("本场战斗内能耗变为 0", StringComparison.Ordinal)
            && englishCards.Contains("cost 1 less for the rest of combat", StringComparison.Ordinal)
            && englishCards.Contains("Do both options", StringComparison.Ordinal)
            && englishCards.Contains("cost 0 for the rest of combat", StringComparison.Ordinal),
            "Expected Choice to upgrade both branches, execute both during Extra, and keep manifested cards at their combat cost reductions; Appear should use the same combat-wide Extra cost state.");
    }
}
