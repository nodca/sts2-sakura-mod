using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.Relics;
using SakuraMod.SakuraModCode;

public sealed class CardMechanicsSuite
{
    [Fact]
    public void ElementSpellAddedEffectsExposeTheirMechanicTips()
    {
        Assert.True(SakuraSourceCardText.ReferencesMagicChargeTip(new SpellHuoShen()));
        Assert.Contains(SakuraKeywords.Frostbite, SakuraSourceCardText.KeywordTips(new SpellShuiLong()));
        Assert.DoesNotContain(SakuraKeywords.Stabilize, SakuraSourceCardText.KeywordTips(new SpellLeiDi()));
        Assert.DoesNotContain(SakuraKeywords.Stabilize, SakuraSourceCardText.KeywordTips(new SpellFengHua()));
    }

    [Fact]
    public void SynchronizedPairsAreIndependentAcrossTemplateAndCardClones()
    {
        var template = new SynchronizedCardPairModifier();
        var first = (SynchronizedCardPairModifier)template.MutableClone();
        var second = (SynchronizedCardPairModifier)template.MutableClone();
        var pairsField = typeof(SynchronizedCardPairModifier).GetField("_pairedCards",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var firstPairs = (List<CardModel>)pairsField.GetValue(first)!;
        var secondPairs = (List<CardModel>)pairsField.GetValue(second)!;
        var pairedCard = new Choice();
        firstPairs.Add(pairedCard);

        Assert.Empty(secondPairs);
        Assert.Empty((List<CardModel>)pairsField.GetValue(template)!);

        var copy = (SynchronizedCardPairModifier)first.MutableClone();
        var copiedPairs = (List<CardModel>)pairsField.GetValue(copy)!;
        Assert.Equal(new[] { pairedCard }, copiedPairs);
        copiedPairs.Clear();
        Assert.Equal(new[] { pairedCard }, firstPairs);
    }

    [Fact]
    public void BubblesPreviewFiltersAndDeduplicatesWithoutReadingNonCombatTargets()
    {
        var amountField = typeof(PowerModel).GetField("_amount",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var strength = new StrengthPower();
        var buffer = new BufferPower();
        var powers = new PowerModel[] { strength, new StrengthPower(), new SandpitPower(), buffer, new DexterityPower() };
        foreach (var power in powers)
            amountField.SetValue(power, power is DexterityPower ? -2 : 2);

        Assert.Equal(new PowerModel[] { strength, buffer }, SakuraSourceCardText.BubblesPreviewBuffs(new SakuraBubbles(), powers));
        Assert.Equal(new PowerModel[] { strength }, SakuraSourceCardText.BubblesPreviewBuffs(new ClowBubbles(), powers));
        Assert.Empty(SakuraSourceCardText.BubblesPreviewBuffs(new SakuraBubbles(), [powers[2], powers[4]]));
        Assert.Equal("SAKURAMOD-BUBBLES.previewNone",
            SakuraSourceCardText.BubblesBuffDescription(new ClowBubbles(), null)?.LocEntryKey);
        Assert.Equal("SAKURAMOD-BUBBLES.previewNone",
            SakuraSourceCardText.BubblesBuffDescription(new SakuraBubbles(), null)?.LocEntryKey);
        Assert.Null(SakuraSourceCardText.BubblesBuffDescription(new ClowJump(), null));
    }

    [Fact]
    public void BubblesDispelIncludesIndependentBuffsButProtectsEncounterState()
    {
        var amountField = typeof(PowerModel).GetField("_amount",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var candidates = new PowerModel[]
        {
            new StrengthPower(), new HighVoltagePower(), new BufferPower(), new PersonalHivePower(),
            new SandpitPower(), new SwipePower(), new HeistPower(), new PossessStrengthPower(),
            new MinionPower(), new AdaptablePower(), new FlutterPower(), new RavenousPower(),
            new SkittishPower(), new PainfulStabsPower(), new HardenedShellPower(), new WeakPower(),
            new ClassicMagicChargePower()
        };
        foreach (var power in candidates)
            amountField.SetValue(power, 2);

        Assert.Equal(candidates.Take(4), candidates.Where(SakuraPowerRules.IsBubblesRemovableBuff));
        foreach (var power in new PowerModel[] { new StrengthPower(), new DexterityPower() })
        {
            foreach (var amount in new[] { -2, 0, 2 })
            {
                amountField.SetValue(power, amount);
                Assert.Equal(amount > 0, SakuraPowerRules.IsBubblesRemovableBuff(power));
            }
        }

        Assert.Equal(candidates.Take(1), candidates.Where(SakuraPowerRules.IsBubblesBasicBuff));
        var ritual = new RitualPower();
        var intangible = new IntangiblePower();
        amountField.SetValue(ritual, 2);
        amountField.SetValue(intangible, 2);
        Assert.True(SakuraPowerRules.IsBubblesBasicBuff(ritual));
        Assert.False(SakuraPowerRules.IsBubblesBasicBuff(intangible));
        Assert.True(SakuraPowerRules.IsBubblesRemovableBuff(intangible));
        Assert.Contains(SakuraCardHoverTips.BubblesBasicBuffTipKey, SakuraSourceCardText.StaticTipKeys(new ClowBubbles()));
        Assert.DoesNotContain(SakuraCardHoverTips.BubblesBuffTipKey, SakuraSourceCardText.StaticTipKeys(new ClowBubbles()));
        Assert.Contains(SakuraCardHoverTips.BubblesBuffTipKey, SakuraSourceCardText.StaticTipKeys(new SakuraBubbles()));
    }

    [Fact]
    public void CleansingUsesExplicitNativeWhitelistAndSharedHoverTips()
    {
        foreach (var power in new PowerModel[] { new WeakPower(), new VulnerablePower(), new FrailPower(), new PoisonPower() })
            Assert.True(SakuraActions.IsCleansableDebuff(power));
        foreach (var power in new PowerModel[] { new TenderPower(), new ChainsOfBindingPower(), new ClassicMagicChargePower() })
            Assert.False(SakuraActions.IsCleansableDebuff(power));
        Assert.Contains(SakuraCardHoverTips.DebuffTipKey, SakuraSourceCardText.StaticTipKeys(new ClowJump()));
        Assert.Contains(SakuraCardHoverTips.DebuffTipKey, SakuraSourceCardText.StaticTipKeys(new SakuraJump()));
        Assert.Contains(SakuraCardHoverTips.ReleaseTipKey, SakuraSourceCardText.StaticTipKeys(new SpellRelease()));
        Assert.DoesNotContain(SakuraCardHoverTips.DebuffTipKey, SakuraCardHoverTips.StaticTipKeys(new Blank()));
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
        RegressionTestHarness.Require(
            SpellTurn.ResultPileFor(PileType.Discard) == PileType.None
            && SpellTurn.ResultPileFor(PileType.Exhaust) == PileType.Exhaust,
            "Expected a played Spell Turn to leave the combat while still respecting forced exhaust.");
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
    public void ClassicWaterySynergiesExcludeSelfAndCountAcrossFamilies()
    {
        var clowCloud = new ClowCloud();
        var hand = new List<CardModel>
        {
            clowCloud,
            new ClowWatery(),
            new SakuraWatery(),
            new Reflect(),
            new Gale()
        };
        RegressionTestHarness.Require(
            SakuraCloudEffects.CountWateryCards(hand, clowCloud) == 3
            && SakuraCloudEffects.CountWateryCards(hand.Where(card => card != clowCloud).ToList(), clowCloud) == 3,
            "Expected Clow Cloud to count other Watery cards and ignore itself in both preview and resolution.");

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
            SakuraSnowRules.CountsAsWateryCard(new ClowWatery())
            && SakuraSnowRules.CountsAsWateryCard(new SakuraWatery())
            && SakuraSnowRules.CountsAsWateryCard(new Reflect())
            && SakuraSnowRules.CountsAsWateryCard(new SpellShuiLong())
            && !SakuraSnowRules.CountsAsWateryCard(new Gale()),
            "Expected Snow rules to count every Watery card across families.");
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
    public void SwordJadeReleaseDoublesAppearSpellCount()
    {
        foreach (var upgraded in new[] { false, true })
        {
            var appear = RegressionTestHarness.MutableForCostTest(new Appear());
            if (upgraded)
                appear.UpgradeInternal();

            Assert.True(appear.DynamicVars.TryGetValue("Cards", out var count));
            var baseCount = count.IntValue;
            SakuraReleaseState.Apply(appear, ClassicSwordJadeRelic.ReleaseRate);
            Assert.Equal(baseCount * 2, count.IntValue);
            SakuraReleaseState.Reset(appear);
            Assert.Equal(baseCount, count.IntValue);
        }
    }

    [Fact]
    public async Task SpellReleaseScalesAndResetsTransparentCardValues()
    {
        var gale = RegressionTestHarness.MutableForCostTest(new Gale());
        var reflect = RegressionTestHarness.MutableForCostTest(new Reflect());
        var baseDamage = gale.DynamicVars.Damage.IntValue;
        var baseCards = gale.DynamicVars["Cards"].IntValue;
        var baseBlock = reflect.DynamicVars.Block.IntValue;

        SakuraReleaseState.Apply(gale, 0.5f);
        SakuraReleaseState.Apply(reflect, 0.5f);

        Assert.Equal(baseDamage + (int)Math.Floor(baseDamage * 0.5f), gale.DynamicVars.Damage.IntValue);
        Assert.Equal(baseCards + (int)Math.Floor(baseCards * 0.5f), gale.DynamicVars["Cards"].IntValue);
        Assert.Equal(baseBlock + (int)Math.Floor(baseBlock * 0.5f), reflect.DynamicVars.Block.IntValue);
        Assert.True(gale.Keywords.Contains(CardKeyword.Exhaust));
        Assert.True(gale.Keywords.Contains(CardKeyword.Ethereal));

        await gale.AfterCardExhausted(null!, gale, causedByEthereal: true);
        Assert.False(SpellRelease.CanRelease(gale));
        SakuraReleaseState.Apply(gale, 0.5f);
        Assert.Equal(baseDamage + (int)Math.Floor(baseDamage * 0.5f), gale.DynamicVars.Damage.IntValue);

        SakuraReleaseState.Reset(gale);
        SakuraReleaseState.Reset(reflect);
        Assert.Equal(baseDamage, gale.DynamicVars.Damage.IntValue);
        Assert.Equal(baseCards, gale.DynamicVars["Cards"].IntValue);
        Assert.Equal(baseBlock, reflect.DynamicVars.Block.IntValue);
        Assert.False(gale.Keywords.Contains(CardKeyword.Exhaust));
        Assert.False(gale.Keywords.Contains(CardKeyword.Ethereal));
    }

    [Fact]
    public void GravitationReturnsKeepThePlayedLocalCostUntilEndOfTurn()
    {
        var discounted = RegressionTestHarness.MutableForCostTest(new Transfer());
        var canonical = discounted.EnergyCost.Canonical;
        discounted.EnergyCost.SetThisTurnOrUntilPlayed(0, true);
        GravitationHoldPower.PreservePlayedLocalCost(discounted, discounted.EnergyCost.GetWithModifiers(CostModifiers.Local));
        discounted.EnergyCost.AfterCardPlayedCleanup();
        var localCost = discounted.EnergyCost.GetWithModifiers(CostModifiers.Local);

        Assert.Equal(0, localCost);
        Assert.True(GravitationHoldPower.TryIncreaseReturnedCardCost(discounted, 1, localCost, out var returnedCost));
        Assert.Equal(1, returnedCost);
        discounted.EnergyCost.EndOfTurnCleanup();
        Assert.Equal(canonical, discounted.EnergyCost.GetWithModifiers(CostModifiers.Local));

        var undiscounted = RegressionTestHarness.MutableForCostTest(new Transfer());
        GravitationHoldPower.PreservePlayedLocalCost(undiscounted, undiscounted.EnergyCost.GetWithModifiers(CostModifiers.Local));
        undiscounted.EnergyCost.AfterCardPlayedCleanup();
        Assert.Equal(canonical, undiscounted.EnergyCost.GetWithModifiers(CostModifiers.Local));
        undiscounted.EnergyCost.UpgradeBy(-1);
        Assert.Equal(canonical - 1, undiscounted.EnergyCost.GetWithModifiers(CostModifiers.Local));
    }

    [Fact]
    public void ReleaseTracksIndividualCardsAndCopiesWithoutStacking()
    {
        var first = RegressionTestHarness.MutableForCostTest(new Gale());
        var second = RegressionTestHarness.MutableForCostTest(new Gale());
        var baseDamage = first.DynamicVars.Damage.IntValue;
        first.EnergyCost.SetThisTurnOrUntilPlayed(0, true);
        SakuraReleaseState.Apply(first, 0.5f);

        Assert.False(SpellRelease.CanRelease(first));
        Assert.True(SpellRelease.CanRelease(second));
        first.EnergyCost.AfterCardPlayedCleanup();
        Assert.Equal(0, first.EnergyCost.GetWithModifiers(CostModifiers.Local));
        first.EnergyCost.EndOfTurnCleanup();
        Assert.Equal(first.EnergyCost.Canonical, first.EnergyCost.GetWithModifiers(CostModifiers.Local));
        SakuraReleaseState.Apply(second, 0.5f);
        Assert.Equal(first.DynamicVars.Damage.IntValue, second.DynamicVars.Damage.IntValue);

        var copy = RegressionTestHarness.MutableForCostTest(new Gale());
        foreach (var (name, variable) in first.DynamicVars)
            copy.DynamicVars[name].BaseValue = variable.BaseValue;
        SakuraReleaseState.CopyState(first, copy);
        SakuraReleaseState.Apply(copy, 0.5f);
        Assert.Equal(first.DynamicVars.Damage.IntValue, copy.DynamicVars.Damage.IntValue);
        Assert.False(SpellRelease.CanRelease(copy));

        SakuraReleaseState.Reset(copy);
        Assert.Equal(baseDamage, copy.DynamicVars.Damage.IntValue);
        Assert.True(SpellRelease.CanRelease(copy));
        Assert.False(SpellRelease.CanRelease(first));
    }

    [Fact]
    public void SpellReleaseUsesOneValuePathAndPreservesAdjustedValueOrdering()
    {
        var clowSword = RegressionTestHarness.MutableForCostTest(new ClowSword());
        var baseDamage = clowSword.DynamicVars.Damage.IntValue;

        SakuraReleaseState.Apply(clowSword, 0.5f);

        var adjustedBase = (int)Math.Floor(baseDamage * 1.6m);
        RegressionTestHarness.Require(
            clowSword.DynamicVars.Damage.IntValue == baseDamage + (int)Math.Floor(baseDamage * 0.5f)
            && SakuraSourceCardValues.EffectiveValue(clowSword, clowSword.DynamicVars.Damage)
                == clowSword.DynamicVars.Damage.IntValue
            && SakuraReleaseState.AdjustedReleasedValue(
                clowSword,
                clowSword.DynamicVars.Damage.Name,
                clowSword.DynamicVars.Damage.IntValue,
                value => (int)Math.Floor(value * 1.6m)) == adjustedBase + (int)Math.Floor(adjustedBase * 0.5f),
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
        var canonicalClowSword = new ClowSword();
        var mappedSword = SakuraSourceCardRules.CreateMirrorCopySource(
            sakuraSword,
            static _ => true,
            CreateCanonical,
            static _ => throw new InvalidOperationException("Sakura mappings must not clone their source."));

        RegressionTestHarness.Require(
            mappedSword is ClowSword
            && mappedSword.DynamicVars.Damage.IntValue == canonicalClowSword.DynamicVars.Damage.IntValue
            && mappedSword.EnergyCost.Canonical == canonicalClowSword.EnergyCost.Canonical,
            "Expected Mirror to map Sakura Sword to a canonical Clow Sword without mutating the source.");

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
    public void ElementSetTwinCostReflectionAndDreamFreeLifecycleRemainStable()
    {
        RegressionTestHarness.Require(
            (SakuraElementSet.Wind | SakuraElementSet.Fire).HasElement(SakuraElement.Wind)
            && (SakuraElementSet.Wind | SakuraElementSet.Fire).HasElement(SakuraElement.Fire),
            "Expected Sakura element sets to retain each declared element identity.");

        Assert.Equal(8, ReflectionPower.ReflectedDamage(17, 1));
        Assert.Equal(17, ReflectionPower.ReflectedDamage(17, 2));
        Assert.Equal(25, ReflectionPower.ReflectedDamage(17, 3));
        Assert.Equal(5, ReflectionPower.ReflectedDamage(10, 1));
        Assert.Equal(10, ReflectionPower.ReflectedDamage(10, 2));
        Assert.Equal(15, ReflectionPower.ReflectedDamage(10, 3));

        var clowTwin = new ClowTwin();
        var clowTwinLocalCost = clowTwin.EnergyCost.GetWithModifiers(CostModifiers.Local);
        RegressionTestHarness.Require(
            ClassicTwinSakuraPower.TryIncreaseClowCardCost(clowTwin, amount: 1, clowTwinLocalCost, out var increasedTwinCost)
            && increasedTwinCost == clowTwinLocalCost + 1
            && clowTwin.EnergyCost.GetWithModifiers(CostModifiers.Local) == clowTwinLocalCost
            && ClassicTwinSakuraPower.TryIncreaseClowCardCost(clowTwin, amount: 2, clowTwinLocalCost, out var stackedTwinCost)
            && stackedTwinCost == clowTwinLocalCost + 2
            && !ClassicTwinSakuraPower.TryIncreaseClowCardCost(new SakuraTwin(), amount: 1, currentCost: 1, out var sakuraTwinCost)
            && sakuraTwinCost == 1,
            "Expected Sakura Twin power cost calculation to scale only Clow cards without mutating source cost.");

        var dreamGeneratedThrough = RegressionTestHarness.MutableForCostTest(new ClowThrough());
        SakuraMagicCharge.SetFreeForRestOfTurn(dreamGeneratedThrough);
        RegressionTestHarness.Require(
            dreamGeneratedThrough.EnergyCost.GetWithModifiers(CostModifiers.Local) == 0
            && !dreamGeneratedThrough.EnergyCost.AfterCardPlayedCleanup()
            && dreamGeneratedThrough.EnergyCost.GetWithModifiers(CostModifiers.Local) == 0
            && dreamGeneratedThrough.EnergyCost.EndOfTurnCleanup()
            && dreamGeneratedThrough.EnergyCost.GetWithModifiers(CostModifiers.Local) == dreamGeneratedThrough.EnergyCost.Canonical,
            "Expected Dream-generated free cost to persist through play until end-of-turn cleanup.");
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
            && SakuraSourceCardText.GeneratedSpellPreviewType(new SakuraEarthy()) == typeof(SpellLeiDi)
            && SakuraSourceCardText.GeneratedSpellPreviewType(new SakuraFirey()) == typeof(SpellHuoShen)
            && SakuraSourceCardText.GeneratedSpellPreviewType(new SakuraWatery()) == typeof(SpellShuiLong)
            && SakuraSourceCardText.GeneratedSpellPreviewType(new SakuraWindy()) == typeof(SpellFengHua)
            && SakuraSourceCardText.GeneratedSpellPreviewType(new ClowMove()) == typeof(SpellEmptySpell)
            && SakuraSourceCardText.GeneratedSpellPreviewType(new SakuraMove()) == typeof(SpellEmptySpell)
            && SakuraSourceCardText.GeneratedSpellPreviewType(new ClowSword()) is null,
            "Expected element-state and Move cards to preview their generated spell cards.");

        RegressionTestHarness.Require(
            SakuraDescriptionRegion.IsExtraEffectDescriptionLineForTests("[gold]额外效果：[/gold]抽 1 张牌。")
            && SakuraDescriptionRegion.IsExtraEffectDescriptionLineForTests("[gold]Extra:[/gold] Draw 1 card.")
            && !SakuraDescriptionRegion.IsExtraEffectDescriptionLineForTests("[gold]解封：[/gold]抽 1 张牌。")
            && !SakuraDescriptionRegion.IsExtraEffectDescriptionLineForTests("[gold]Release:[/gold] Draw 1 card."),
            "Expected the shared description region to recognize current extra-effect description headers only.");
    }

    [Fact]
    public void SakuraLightAndLibraCoreCalculationsRemainValid()
    {
        RegressionTestHarness.Require(
            ClassicLightSakuraPower.IsStatusOrCurse(CardType.Status)
            && ClassicLightSakuraPower.IsStatusOrCurse(CardType.Curse)
            && !ClassicLightSakuraPower.IsStatusOrCurse(CardType.Skill)
            && RegressionTestHarness.DeclaresMethod<ClassicLightSakuraPower>("TryModifyKeywordsInCombat")
            && RegressionTestHarness.DeclaresMethod<ClassicLightSakuraPower>("ModifyCardPlayResultLocation")
            && RegressionTestHarness.DeclaresMethod<ClassicLightSakuraPower>("AfterCardPlayed")
            && !RegressionTestHarness.DeclaresMethod<ClassicLightSakuraPower>("TryModifyEnergyCostInCombat"),
            "Expected Sakura Light to make Status and Curse cards playable and Exhausting without changing their costs.");

        RegressionTestHarness.Require(
            SakuraLibra.EnergyFromCharge(-1) == 0
            && SakuraLibra.EnergyFromCharge(0) == 0
            && SakuraLibra.EnergyFromCharge(2) == 0
            && SakuraLibra.EnergyFromCharge(3) == 1
            && SakuraLibra.EnergyFromCharge(8) == 2
            && SakuraLibra.EnergyFromCharge(9) == 3,
            "Expected Sakura Libra to convert every 3 Magic Charge into 1 Energy.");
    }

    [Fact]
    public void CardDamageHelpersRequireCardPlayContext()
    {
        var sourceAttackMethods = typeof(SakuraCardModel)
            .GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly)
            .Where(static method => method.Name is "DealDamage" or "DealDamageToEnemies" or "DealDamageToRandomEnemies" or "DealDamageHit")
            .ToList();
        static bool DeclaresResultPileMethod(Type type) => type.GetMethod(
            "GetResultLocationForCardPlay",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly) is not null;

        RegressionTestHarness.Require(
            sourceAttackMethods.Count == 5
            && sourceAttackMethods.All(static method => method.GetParameters().Any(static parameter => parameter.ParameterType == typeof(CardPlay)))
            && !DeclaresResultPileMethod(typeof(Transfer))
            && DeclaresResultPileMethod(typeof(ClowRain))
            && !DeclaresResultPileMethod(typeof(ClowTime))
            && DeclaresResultPileMethod(typeof(ClowFlower))
            && DeclaresResultPileMethod(typeof(SakuraFlower)),
            "Expected shared card-damage helpers to require CardPlay and result-location overrides to belong to the intended cards.");
    }
}
