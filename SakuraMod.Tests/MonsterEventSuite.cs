using SakuraMod.SakuraModCode.Events;
using SakuraMod.SakuraModCode.Relics;

public sealed class MonsterEventSuite
{
    [Fact]
    public void MonsterEventUsesBothFirstActRoutesAndEventOnlyRelic()
    {
        var registration = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Events/SakuraEventRegistration.cs"));
        var eventSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Events/Models/ClassicMonsterEvent.cs"));
        var relicSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Relics/Models/ClassicMonsterRelic.cs"));

        RegressionTestHarness.Require(
            registration.Contains("RegisterActEvent<Overgrowth, ClassicMonsterEvent>()", StringComparison.Ordinal)
            && registration.Contains("RegisterActEvent<Underdocks, ClassicMonsterEvent>()", StringComparison.Ordinal),
            "Expected Monster to be registered in both first-act routes.");
        RegressionTestHarness.Require(
            eventSource.Contains("EventAssetProfile AssetProfile", StringComparison.Ordinal)
            && eventSource.Contains("events/monster_event.png", StringComparison.Ordinal)
            && !eventSource.Contains("BackgroundScenePath", StringComparison.Ordinal)
            && eventSource.Contains("SakuraStarterCompatibility.IsKinomotoSakuraRun(runState)", StringComparison.Ordinal)
            && eventSource.Contains("PlayerCmd.GainGold(GoldReward, player)", StringComparison.Ordinal)
            && eventSource.Contains("CreatureCmd.Heal(player.Creature, HealAmount)", StringComparison.Ordinal),
            "Expected Monster event to retain its background, Sakura-only gate, and alternate option rewards.");
        RegressionTestHarness.Require(
            relicSource.Contains("CreatureCmd.GainMaxHp", StringComparison.Ordinal)
            && relicSource.Contains("StrengthPower", StringComparison.Ordinal)
            && relicSource.Contains("-DynamicVars[\"DexterityPower\"].IntValue", StringComparison.Ordinal),
            "Expected Monster relic to apply max HP, strength, and negative dexterity through native commands.");
    }

    [Fact]
    public void MonsterRelicKeepsTheApprovedNumbers()
    {
        var relicSource = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Relics/Models/ClassicMonsterRelic.cs"));
        RegressionTestHarness.Require(
            relicSource.Contains("MaxHpGain = 6", StringComparison.Ordinal)
            && relicSource.Contains("StrengthGain = 2", StringComparison.Ordinal)
            && relicSource.Contains("DexterityLoss = 1", StringComparison.Ordinal),
            "Expected Monster to retain the approved 6 Max HP, 2 Strength, and 1 Dexterity loss values.");
    }
}
