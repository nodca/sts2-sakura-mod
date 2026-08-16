using SakuraMod.SakuraModCode.Cards;

public sealed class CombatVisualPerformanceSuite
{
    [Fact]
    public void SpellTurnTransformationKeepsSourceTimelineAndBoundedVisualWork()
    {
        RegressionTestHarness.Require(
            SpellTurnTransformationTimeline.EnlargeEnd == 0.25f
            && SpellTurnTransformationTimeline.SwitchStart == 1.95f
            && SpellTurnTransformationTimeline.RevealStart == 3.95f
            && SpellTurnTransformationTimeline.ShrinkStart == 4.45f
            && SpellTurnTransformationTimeline.TotalDuration == 4.95f,
            "Expected Spell Turn to preserve the source 4.95-second timeline.");
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
            source.Contains("CombatManager.Instance.CombatEnded += OnCombatEnded", StringComparison.Ordinal)
            && source.Contains("CombatManager.Instance.CombatEnded -= OnCombatEnded", StringComparison.Ordinal)
            && source.Contains("_root.TreeExiting += OnTreeExiting", StringComparison.Ordinal)
            && source.Contains("_root.TreeExiting -= OnTreeExiting", StringComparison.Ordinal)
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

        RegressionTestHarness.Require(
            source.Contains("power is ClassicEarthyPower", StringComparison.Ordinal)
            && source.Contains("or ClassicFireyPower", StringComparison.Ordinal)
            && source.Contains("or ClassicWateryPower", StringComparison.Ordinal)
            && source.Contains("or ClassicWindyPower", StringComparison.Ordinal)
            && source.Contains("state.Refresh(animateNewlyActive: false)", StringComparison.Ordinal)
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
            && source.Contains("CombatManager.Instance.CombatEnded += OnCombatEnded", StringComparison.Ordinal)
            && source.Contains("CombatManager.Instance.CombatEnded -= OnCombatEnded", StringComparison.Ordinal)
            && source.Contains("_creature.Died += OnDied", StringComparison.Ordinal)
            && source.Contains("_creature.Died -= OnDied", StringComparison.Ordinal)
            && source.Contains("_root.TreeExiting += OnTreeExiting", StringComparison.Ordinal)
            && source.Contains("_root.TreeExiting -= OnTreeExiting", StringComparison.Ordinal)
            && source.Contains("Sessions.Remove(creatureNode.Entity)", StringComparison.Ordinal)
            && source.Contains("Name = \"WindWallImpact\"", StringComparison.Ordinal)
            && source.Contains("_root.AddChild(_wallImpact)", StringComparison.Ordinal)
            && source.Contains("FreeNode(ref _wallImpact)", StringComparison.Ordinal)
            && !source.Contains("_Process", StringComparison.Ordinal)
            && !source.Contains("GpuParticles", StringComparison.Ordinal),
            "Expected Wind Wall and Dark Veil to use event-owned persistent sessions with bounded one-shots and no polling or continuous particles.");
    }

    [Fact]
    public void IllusionOcclusionCoversVacatedDeclaredSlots()
    {
        var source = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/FourthAct/Wind/Visuals/IllusionVisualPatches.cs"));
        RegressionTestHarness.Require(
            source.Contains("CaptureDeclaredPositions(realBody);", StringComparison.Ordinal)
            && source.Contains("DeclaredPositions.TryGetValue(realBody, out var declaredPositions)", StringComparison.Ordinal)
            && source.Contains("points.AddRange(declaredPositions.Values", StringComparison.Ordinal),
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
            illusion.Contains("finally", StringComparison.Ordinal)
            && illusion.Contains("root.QueueFreeSafely();", StringComparison.Ordinal)
            && standee.Contains("is not SakuraMod.SakuraModCode.FourthAct.Wind.Models.IllusionProjectionMonster", StringComparison.Ordinal),
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
            source.Contains("power is ClassicMagicChargePower or ClassicLockPower", StringComparison.Ordinal)
            && source.Contains("Visuals.VfxSpawnPosition", StringComparison.Ordinal)
            && source.Contains("Position = AuraOffset", StringComparison.Ordinal)
            && source.Contains("ZIndex = 0", StringComparison.Ordinal)
            && source.Contains("CreateTween().SetLoops()", StringComparison.Ordinal)
            && source.Contains("TweenMethod", StringComparison.Ordinal)
            && source.Contains("SetTrans(Tween.TransitionType.Linear)", StringComparison.Ordinal)
            && source.Contains("Mathf.Abs(targetAlpha - currentAlpha) * FadeDuration", StringComparison.Ordinal)
            && source.Contains("_creature.Died += OnCreatureDied", StringComparison.Ordinal)
            && source.Contains("_creature.Died -= OnCreatureDied", StringComparison.Ordinal)
            && source.Contains("CombatManager.Instance.CombatEnded += OnCombatEnded", StringComparison.Ordinal)
            && source.Contains("CombatManager.Instance.CombatEnded -= OnCombatEnded", StringComparison.Ordinal)
            && source.Contains("_aura.TreeExiting += OnTreeExiting", StringComparison.Ordinal)
            && source.Contains("_aura.TreeExiting -= OnTreeExiting", StringComparison.Ordinal)
            && !source.Contains("AwaitProcessFrame", StringComparison.Ordinal)
            && !source.Contains("AnimateAura", StringComparison.Ordinal)
            && !source.Contains("MoveToward", StringComparison.Ordinal)
            && !source.Contains("CancellationToken", StringComparison.Ordinal)
            && !source.Contains("TaskHelper.RunSafely", StringComparison.Ordinal),
            "Expected the aura to inherit the native VFX transform, remain above the combat background, and use bounded event/Tween ownership without polling.");
    }
}
