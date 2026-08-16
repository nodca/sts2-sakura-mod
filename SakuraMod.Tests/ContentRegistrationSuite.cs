using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.CardPiles;
using STS2RitsuLib.Content;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.Relics;
using SakuraMod.SakuraModCode;
using System.Buffers.Binary;

public sealed class ContentRegistrationSuite
{
    [Fact]
    public void RegisteredModelsHaveLocalizationAndExcludeRemovedCards()
    {
        var registeredCardTypes = RegressionTestData.RegisteredCardTypes;
        var registeredPowerTypes = RegressionTestData.RegisteredPowerTypes;

        RegressionTestHarness.Require(
            RegressionTestHarness.RegisteredModelEntry(typeof(ClowSword)) == "SAKURA_MOD_CARD_CLOW_SWORD",
            "Expected registered-card localization checks to follow RitsuLib's default public entry rule.");
        RegressionTestHarness.Require(
            RegressionTestHarness.RegisteredModelEntry(typeof(ClassicShieldWardPower)) == "SAKURA_MOD_POWER_CLASSIC_SHIELD_WARD_POWER",
            "Expected registered-power localization checks to follow RitsuLib's default public entry rule.");
        RegressionTestHarness.RequireRegisteredCardLocalizationKeys("SakuraMod/localization/eng/cards.json", registeredCardTypes);
        RegressionTestHarness.RequireRegisteredCardLocalizationKeys("SakuraMod/localization/zhs/cards.json", registeredCardTypes);
        RegressionTestHarness.RequireRegisteredPowerLocalizationKeys("SakuraMod/localization/eng/powers.json", registeredPowerTypes);
        RegressionTestHarness.RequireRegisteredPowerLocalizationKeys("SakuraMod/localization/zhs/powers.json", registeredPowerTypes);
        RegressionTestHarness.RequireRegisteredCharacterLocalizationKeys("SakuraMod/localization/eng/characters.json", [typeof(ClassicSakura)]);
        RegressionTestHarness.RequireRegisteredCharacterLocalizationKeys("SakuraMod/localization/zhs/characters.json", [typeof(ClassicSakura)]);
        RegressionTestHarness.RequireRegisteredRelicLocalizationKeys("SakuraMod/localization/eng/relics.json", SakuraRelicCatalog.AllRelicTypes());
        RegressionTestHarness.RequireRegisteredRelicLocalizationKeys("SakuraMod/localization/zhs/relics.json", SakuraRelicCatalog.AllRelicTypes());
        RegressionTestHarness.RequireNoRemovedCardTypes(
            "registered Sakura card types",
            registeredCardTypes,
            RegressionTestData.RemovedClearSupportCardTypeNames);
        RegressionTestHarness.RequireNoRemovedCardTypes(
            "registered Sakura card types",
            registeredCardTypes,
            RegressionTestData.RemovedAncientCardTypeNames);
        RegressionTestHarness.RequireNoLocalizationPrefixes(
            "SakuraMod/localization/eng/cards.json",
            RegressionTestData.RemovedAncientCardLocalizationPrefixes);
        RegressionTestHarness.RequireNoLocalizationPrefixes(
            "SakuraMod/localization/zhs/cards.json",
            RegressionTestData.RemovedAncientCardLocalizationPrefixes);

    }

    [Fact]
    public void StarterRaritiesRemainStable()
    {
        RegressionTestHarness.Require(new Gale().Rarity == CardRarity.Common, "Expected Gale to be a Common Transparent Card, not a Basic starter.");
        RegressionTestHarness.Require(new Siege().Rarity == CardRarity.Uncommon, "Expected Siege to be an Uncommon Transparent Card, not a Basic starter.");

    }

    [Fact]
    public void MemoryPileUsesTheNativeCombatOnlySakuraUiContract()
    {
        var memoryIcon = RegressionTestHarness.FindRepoFile("SakuraMod/images/card_piles/memory.png");
        var memoryIconHeader = File.ReadAllBytes(memoryIcon).AsSpan(0, 26);
        RegressionTestHarness.Require(
            File.Exists($"{memoryIcon}.import")
            && memoryIconHeader[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
            && BinaryPrimitives.ReadInt32BigEndian(memoryIconHeader[16..20]) == 512
            && BinaryPrimitives.ReadInt32BigEndian(memoryIconHeader[20..24]) == 512
            && memoryIconHeader[24] == 8
            && memoryIconHeader[25] == 6,
            "Expected Memory to use its tracked 512x512 RGBA card-pile icon.");

        var spec = SakuraMemoryPile.RegistrationSpec;
        RegressionTestHarness.Require(
            SakuraMemoryPile.PileId == "SAKURA_MOD_CARDPILE_MEMORY"
            && spec.Scope == ModCardPileScope.CombatOnly
            && spec.Style == ModCardPileUiStyle.BottomRight
            && spec.Anchor.Kind == ModCardPileAnchorKind.BottomRightPrimary
            && spec.Anchor.Offset == SakuraMemoryPile.UiOffsetAboveExhaust
            && spec.IconPath == SakuraMemoryPile.IconPath
            && spec.VisibleWhen is not null
            && spec.OnOpen is null
            && SakuraMemoryPile.IsButtonVisible(true, true, 1)
            && !SakuraMemoryPile.IsButtonVisible(true, true, 0)
            && !SakuraMemoryPile.IsButtonVisible(true, false, 1)
            && !SakuraMemoryPile.IsButtonVisible(false, true, 1),
            "Expected Memory to appear above Exhaust only for a non-empty Kinomoto Sakura pile during combat.");
    }

    [Fact]
    public void KinomotoSakuraOwnsTheCombinedHostAndClassicPresentation()
    {
        var kinomotoSakura = new ClassicSakura();
        RegressionTestHarness.Require(
            SakuraStarterCompatibility.IsKinomotoSakuraCharacter(kinomotoSakura),
            "Expected Kinomoto Sakura predicate to target ClassicSakura as the only host.");
        const string classicEnergyCounterScene =
            "SakuraMod/scenes/combat/energy_counters/sakura_energy_counter.tscn";
        RegressionTestHarness.Require(
            kinomotoSakura.CustomEnergyCounterPath.EndsWith(
                "SakuraMod/scenes/combat/energy_counters/sakura_energy_counter.tscn",
                StringComparison.Ordinal),
            "Expected Kinomoto Sakura to use the Classic Sakura energy counter scene.");
        RegressionTestHarness.Require(
            kinomotoSakura.CustomMerchantAnimPath.EndsWith(
                "SakuraMod/scenes/merchant/sakura_merchant_character.tscn",
                StringComparison.Ordinal),
            "Expected Kinomoto Sakura to use the Sakura merchant standee scene.");
        RegressionTestHarness.Require(
            kinomotoSakura.CustomRestSiteAnimPath.EndsWith(
                "SakuraMod/scenes/rest_site/sakura_rest_site_character.tscn",
                StringComparison.Ordinal),
            "Expected Kinomoto Sakura to use the animated Sakura rest-site scene.");
        RegressionTestHarness.Require(
            kinomotoSakura.CustomCharacterSelectBgPath.EndsWith(
                "SakuraMod/scenes/screens/char_select/sakura_character_select_background.tscn",
                StringComparison.Ordinal),
            "Expected Kinomoto Sakura to use the Classic Sakura character-select background scene.");
        RegressionTestHarness.Require(
            typeof(MainFile).Assembly.GetType(
                "SakuraMod.SakuraModCode.Character.SakuraCharacterSelectBackgroundPatch") is not null,
            "Expected Kinomoto Sakura to retain the RitsuLib character-select background sizing adapter.");
        RegressionTestHarness.Require(
            kinomotoSakura.EnergyLabelOutlineColor == new Color("322a22"),
            "Expected the Classic Sakura energy label to retain its legacy outline color.");
        RegressionTestHarness.RequireClassicEnergyCounterScene(classicEnergyCounterScene);
        var hostPoolTypes = typeof(ClassicSakura).BaseType?.GetGenericArguments();
        RegressionTestHarness.Require(
            hostPoolTypes is { Length: 3 }
            && hostPoolTypes[0] == typeof(ClassicSakuraCardPool)
            && hostPoolTypes[1] == typeof(ClassicSakuraRelicPool)
            && hostPoolTypes[2] == typeof(ClassicSakuraPotionPool),
            "Expected Kinomoto Sakura to own the combined card pool and Classic relic set.");
        RegressionTestHarness.Require(
            typeof(MainFile).Assembly.GetType("SakuraMod.SakuraModCode.Character.SakuraMod") is null
            && typeof(MainFile).Assembly.GetType("SakuraMod.SakuraModCode.Character.SakuraModCardPool") is null
            && typeof(MainFile).Assembly.GetType("SakuraMod.SakuraModCode.Character.SakuraModRelicPool") is null
            && typeof(MainFile).Assembly.GetType("SakuraMod.SakuraModCode.Character.SakuraModPotionPool") is null
            && typeof(MainFile).Assembly.GetType("SakuraMod.SakuraModCode.Character.SakuraCaptureRunHooks") is null
            && typeof(MainFile).Assembly.GetType("SakuraMod.SakuraModCode.Character.SakuraCaptureRewardHandoff") is null,
            "Expected the legacy Clear host, pools, and host-only Capture route to be absent.");
    }

}
