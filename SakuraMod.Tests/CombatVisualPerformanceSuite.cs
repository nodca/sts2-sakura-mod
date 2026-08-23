using SakuraMod.SakuraModCode.Cards;

public sealed class CombatVisualPerformanceSuite
{
    [Fact]
    public void SpellTurnTransformationKeepsSourceTimelineAndBoundedVisualWork()
    {
        RegressionTestHarness.Require(
            SpellTurnTransformationTimeline.EnlargeEnd < 1f
            && SpellTurnTransformationTimeline.SwitchStart < 2f
            && SpellTurnTransformationTimeline.RevealStart < 4f
            && SpellTurnTransformationTimeline.ShrinkStart < 5f
            && SpellTurnTransformationTimeline.TotalDuration < 5f,
            "Expected the Spell Turn transformation to keep every stage and the total duration within their visual time budget.");
        RegressionTestHarness.Require(
            SpellTurnTransformationTimeline.GatherParticleCount == 256
            && SpellTurnTransformationTimeline.DiffusionParticleCount == 48
            && SpellTurnTransformationTimeline.LuminStripCount == 72,
            "Expected Spell Turn visual work to remain explicitly bounded.");
        RegressionTestHarness.Require(
            SpellTurnTransformationTimeline.LoweredPitchScale == 0.4f
            && SpellTurnTransformationTimeline.CompletionAudioPaths.Length == 3
            && SpellTurnTransformationTimeline.CompletionAudioPaths.Select(Path.GetFileName).SequenceEqual(
                [
                    "SOTE_SFX_Buff_1_v1.ogg",
                    "SOTE_SFX_Buff_2_v1.ogg",
                    "SOTE_SFX_Buff_3_v1.ogg"
                ]),
            "Expected Spell Turn to preserve the lowered opening pitch and random source completion cues.");

        var source = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Visuals/Classic/SpellTurnTransformationVfx.cs"));
        RegressionTestHarness.Require(
            source.Contains("CombatEnded +=", StringComparison.Ordinal)
            && source.Contains("CombatEnded -=", StringComparison.Ordinal)
            && source.Contains("TreeExiting +=", StringComparison.Ordinal)
            && source.Contains("TreeExiting -=", StringComparison.Ordinal)
            && source.Contains("CardCmd", StringComparison.Ordinal) == false
            && source.Contains("HarmonyPatch", StringComparison.Ordinal) == false
            && source.Contains("System.Reflection", StringComparison.Ordinal) == false,
            "Expected the Spell Turn VFX to own cleanup without mutating gameplay or adding global patches/reflection.");
    }

    [Fact]
    public void ElementStateHudUsesFilteredPowerLifecycleEvents()
    {
        var source = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraElementStateHud.cs"));

        foreach (var eventName in new[] { "PowerApplied", "PowerIncreased", "PowerDecreased", "PowerRemoved" })
        {
            RegressionTestHarness.Require(
                source.Contains($"_creature.{eventName} +=", StringComparison.Ordinal)
                && source.Contains($"_creature.{eventName} -=", StringComparison.Ordinal),
                $"Expected the Element State HUD to subscribe and unsubscribe Creature.{eventName}.");
        }

        var owner = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraElementState.cs"));
        RegressionTestHarness.Require(
            source.Contains("IsTriggerPower", StringComparison.Ordinal)
            && owner.Contains("ClassicEarthyPower", StringComparison.Ordinal)
            && owner.Contains("ClassicFireyPower", StringComparison.Ordinal)
            && owner.Contains("ClassicWateryPower", StringComparison.Ordinal)
            && owner.Contains("ClassicWindyPower", StringComparison.Ordinal)
            && source.Contains("Refresh", StringComparison.Ordinal)
            && !source.Contains("AwaitProcessFrame", StringComparison.Ordinal)
            && !source.Contains("RefreshUntilUnmounted", StringComparison.Ordinal)
            && !source.Contains("CancellationToken", StringComparison.Ordinal)
            && !source.Contains("TaskHelper.RunSafely", StringComparison.Ordinal),
            "Expected the HUD to project once, then refresh only for relevant Power events without frame polling.");
    }

    [Fact]
    public void FourthActPersistentFeedbackUsesBoundedEventOwnedSessions()
    {
        var source = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Visuals/FourthActCombatFeedbackVisuals.cs"));
        RegressionTestHarness.Require(
            source.Contains("WindWallBarrier", StringComparison.Ordinal)
            && source.Contains("DarkVeilMembrane", StringComparison.Ordinal)
            && source.Contains("DarkVeilRemnants", StringComparison.Ordinal)
            && source.Contains("Name = \"WindWallImpact\"", StringComparison.Ordinal),
            "Expected Wind Wall and Dark Veil to own their named persistent visual nodes.");
        RegressionTestHarness.Require(
            source.Contains("CombatEnded +=", StringComparison.Ordinal)
            && source.Contains("CombatEnded -=", StringComparison.Ordinal)
            && source.Contains("Died +=", StringComparison.Ordinal)
            && source.Contains("Died -=", StringComparison.Ordinal)
            && source.Contains("TreeExiting +=", StringComparison.Ordinal)
            && source.Contains("TreeExiting -=", StringComparison.Ordinal)
            && source.Contains("Sessions.Remove", StringComparison.Ordinal),
            "Expected the persistent sessions to be event-owned and removed on combat or tree exit.");
        RegressionTestHarness.Require(
            source.Contains("AddChild", StringComparison.Ordinal)
            && source.Contains("FreeNode", StringComparison.Ordinal)
            && !source.Contains("_Process", StringComparison.Ordinal)
            && !source.Contains("GpuParticles", StringComparison.Ordinal),
            "Expected bounded one-shot visuals without polling or continuous particles.");
    }

    [Fact]
    public void IllusionOcclusionCoversVacatedDeclaredSlots()
    {
        var source = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Wind/Visuals/IllusionVisualPatches.cs"));
        RegressionTestHarness.Require(
            source.Contains("CaptureDeclaredPositions", StringComparison.Ordinal)
            && source.Contains("TryGetValue", StringComparison.Ordinal)
            && source.Contains("AddRange", StringComparison.Ordinal),
            "Expected reshuffle and Reweave occlusion to cover declared slots even after projections leave combat.");
    }

    [Fact]
    public void IllusionFeedbackCleansOcclusionAndSuppressesLivingBodyHurt()
    {
        var illusion = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Wind/Visuals/IllusionVisualPatches.cs"));
        var standee = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Visuals/SakuraStandeeActionController.cs"));

        RegressionTestHarness.Require(
            illusion.Contains("QueueFreeSafely", StringComparison.Ordinal)
            && standee.Contains("IllusionProjectionMonster", StringComparison.Ordinal),
            "Expected Illusion occlusion to clean up on failures and projections to skip the shared living-body hurt clip.");
    }

    [Fact]
    public void MagicChargeAuraUsesPowerEventsAndNodeOwnedTweens()
    {
        var source = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraMagicChargeAuraVisual.cs"));

        foreach (var eventName in new[] { "PowerApplied", "PowerIncreased", "PowerDecreased", "PowerRemoved" })
        {
            RegressionTestHarness.Require(
                source.Contains($"_creature.{eventName} +=", StringComparison.Ordinal)
                && source.Contains($"_creature.{eventName} -=", StringComparison.Ordinal),
                $"Expected the Magic Charge aura to subscribe and unsubscribe Creature.{eventName}.");
        }

        RegressionTestHarness.Require(
            source.Contains("ClassicMagicChargePower", StringComparison.Ordinal)
            && source.Contains("ClassicLockPower", StringComparison.Ordinal)
            && source.Contains("Visuals.VfxSpawnPosition", StringComparison.Ordinal)
            && source.Contains("AuraOffset", StringComparison.Ordinal)
            && source.Contains("ZIndex", StringComparison.Ordinal),
            "Expected the aura to inherit the native VFX transform and remain above the combat background.");
        RegressionTestHarness.Require(
            source.Contains("SetLoops", StringComparison.Ordinal)
            && source.Contains("TweenMethod", StringComparison.Ordinal)
            && source.Contains("SetTrans", StringComparison.Ordinal)
            && source.Contains("FadeDuration", StringComparison.Ordinal),
            "Expected the aura pulse to run on bounded node-owned Tweens.");
        RegressionTestHarness.Require(
            source.Contains("Died +=", StringComparison.Ordinal)
            && source.Contains("Died -=", StringComparison.Ordinal)
            && source.Contains("CombatEnded +=", StringComparison.Ordinal)
            && source.Contains("CombatEnded -=", StringComparison.Ordinal)
            && source.Contains("TreeExiting +=", StringComparison.Ordinal)
            && source.Contains("TreeExiting -=", StringComparison.Ordinal),
            "Expected the aura to subscribe and unsubscribe its lifecycle events symmetrically.");
        RegressionTestHarness.Require(
            !source.Contains("AwaitProcessFrame", StringComparison.Ordinal)
            && !source.Contains("AnimateAura", StringComparison.Ordinal)
            && !source.Contains("MoveToward", StringComparison.Ordinal)
            && !source.Contains("CancellationToken", StringComparison.Ordinal)
            && !source.Contains("TaskHelper.RunSafely", StringComparison.Ordinal),
            "Expected the aura to avoid frame polling and ad-hoc animation helpers.");
    }
}
