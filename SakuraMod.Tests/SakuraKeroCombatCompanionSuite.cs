using System.Buffers.Binary;
using System.Security.Cryptography;
using SakuraMod.SakuraModCode.Character;

public sealed class SakuraKeroCombatCompanionSuite
{
    private const string SelectedSourceHash =
        "29ab926e36e20ef5a10db55a69f83792a3d5a0d00481df288f703a4330d2488e";
    private const string RuntimeAssetHash =
        "3bb9be3246b720ddc2e147077fa74b7e4b18b44e70cf6dc3aff34d0d62dd26b9";

    [Fact]
    public void PresenceRequiresSakuraAndEitherQualifyingRelic()
    {
        RegressionTestHarness.Require(
            !SakuraKeroCombatCompanion.ShouldMount(false, false, false)
            && !SakuraKeroCombatCompanion.ShouldMount(false, true, false)
            && !SakuraKeroCombatCompanion.ShouldMount(false, false, true)
            && !SakuraKeroCombatCompanion.ShouldMount(true, false, false)
            && SakuraKeroCombatCompanion.ShouldMount(true, true, false)
            && SakuraKeroCombatCompanion.ShouldMount(true, false, true)
            && SakuraKeroCombatCompanion.ShouldMount(true, true, true),
            "Expected Kero only for Sakura with Cerberus or Ultimate Wand.");
    }

    [Fact]
    public void StandardAndChibiUseDistinctNamedLayoutsAndOneTexture()
    {
        var standard = SakuraKeroCombatCompanion.SelectLayout(useChibi: false);
        var chibi = SakuraKeroCombatCompanion.SelectLayout(useChibi: true);

        RegressionTestHarness.Require(
            standard == SakuraKeroCombatCompanion.StandardLayout
            && chibi == SakuraKeroCombatCompanion.ChibiLayout
            && standard.Offset == new Godot.Vector2(-190f, -20f)
            && standard.Scale == 0.22f
            && chibi.Offset == new Godot.Vector2(-155f, -25f)
            && chibi.Scale == 0.18f
            && standard != chibi
            && SakuraKeroCombatCompanion.TexturePath.EndsWith(
                "SakuraMod/images/charui/combat/kero_companion.png",
                StringComparison.Ordinal),
            "Expected one Kero texture with separately tuned Standard and Chibi profiles.");
    }

    [Fact]
    public void SelectedImageFourIsTheOnlyPublishedCombatDerivative()
    {
        const string sourceRelativePath =
            "research/kero-combat-companion/candidates/image_4-edit.png";
        const string runtimeRelativePath =
            "SakuraMod/images/charui/combat/kero_companion.png";

        var source = RegressionTestHarness.FindRepoFile(sourceRelativePath);
        var runtime = RegressionTestHarness.FindRepoFile(runtimeRelativePath);
        var header = File.ReadAllBytes(runtime).AsSpan(0, 26);
        var import = File.ReadAllText($"{runtime}.import");

        RegressionTestHarness.Require(
            Sha256(source) == SelectedSourceHash
            && Sha256(runtime) == RuntimeAssetHash,
            "Expected the combat texture to remain the approved Image #4 transparent derivative.");
        RegressionTestHarness.Require(
            header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
            && BinaryPrimitives.ReadInt32BigEndian(header[16..20]) == 639
            && BinaryPrimitives.ReadInt32BigEndian(header[20..24]) == 820
            && header[24] == 8
            && header[25] == 6,
            "Expected the Kero combat asset to remain a 639x820 8-bit RGBA PNG.");
        RegressionTestHarness.Require(
            import.Contains($"source_file=\"res://{runtimeRelativePath}\"", StringComparison.Ordinal)
            && import.Contains("mipmaps/generate=false", StringComparison.Ordinal),
            "Expected the Kero combat texture to retain its tracked non-mipmapped import.");
    }

    [Fact]
    public void CompanionUsesOneReadyHookAndSymmetricEventDrivenCleanup()
    {
        var source = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraKeroCombatCompanion.cs"));
        var readyPatch = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraMagicChargeAuraVisual.cs"));
        var markPower = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Powers/SourceCards/ClassicCerberusMarkPower.cs"));
        var relicActions = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Relics/Models/SakuraRelicCombatActions.cs"));

        RegressionTestHarness.Require(
            readyPatch.Contains("SakuraMagicChargeAuraVisual.Mount(__instance);", StringComparison.Ordinal)
            && readyPatch.Contains("SakuraKeroCombatCompanion.Mount(__instance);", StringComparison.Ordinal)
            && !source.Contains("HarmonyPatch", StringComparison.Ordinal)
            && source.Contains("CompanionNodeName = \"SakuraKeroCombatCompanion\"", StringComparison.Ordinal)
            && source.Contains("GetNodeOrNull<Node2D>(CompanionNodeName)", StringComparison.Ordinal),
            "Expected Kero to share the existing creature-ready patch and guard duplicate mounts by stable name.");

        foreach (var subscription in new[]
        {
            "ClassicCerberusMarkPower.MarkApplied",
            "_creature.Died",
            "CombatManager.Instance.CombatEnded",
            "_root.TreeExiting"
        })
        {
            RegressionTestHarness.Require(
                source.Contains($"{subscription} +=", StringComparison.Ordinal)
                && source.Contains($"{subscription} -=", StringComparison.Ordinal),
                $"Expected symmetric Kero lifecycle subscription for {subscription}.");
        }

        RegressionTestHarness.Require(
            markPower.Contains("public override Task AfterApplied", StringComparison.Ordinal)
            && markPower.Contains("NotifyApplied(applier);", StringComparison.Ordinal)
            && markPower.Contains("MarkApplied?.Invoke(applier);", StringComparison.Ordinal)
            && relicActions.Contains(
                "previousMarkAmount = target.GetPower<ClassicCerberusMarkPower>()?.Amount",
                StringComparison.Ordinal)
            && relicActions.Contains(
                "appliedMark.Amount != previousAmount",
                StringComparison.Ordinal)
            && relicActions.Contains(
                "ClassicCerberusMarkPower.NotifyApplied(relic.Owner.Creature);",
                StringComparison.Ordinal)
            && source.Contains("ReferenceEquals(applier, _creature)", StringComparison.Ordinal)
            && source.Contains("ReferenceEquals(_creature.CombatState, _combatState)", StringComparison.Ordinal)
            && source.Contains("KillTween(ref _reactionTween);", StringComparison.Ordinal)
            && source.Contains("_idleRoot.CreateTween().SetLoops()", StringComparison.Ordinal)
            && source.Contains("_reactionRoot.CreateTween()", StringComparison.Ordinal)
            && !source.Contains("AwaitProcessFrame", StringComparison.Ordinal)
            && !source.Contains("GlobalPosition", StringComparison.Ordinal)
            && !source.Contains("CancellationToken", StringComparison.Ordinal)
            && !source.Contains("Audio", StringComparison.Ordinal)
            && !source.Contains("CreatureCmd", StringComparison.Ordinal)
            && !source.Contains("PowerCmd", StringComparison.Ordinal)
            && !source.Contains("CardCmd", StringComparison.Ordinal)
            && !source.Contains("SavedAttachedState", StringComparison.Ordinal)
            && source.Contains(
                "SelectLayout(SakuraCombatArtPreference.IsChibi(player))",
                StringComparison.Ordinal)
            && !source.Contains("SakuraModConfig", StringComparison.Ordinal)
            && !source.Contains("Control", StringComparison.Ordinal),
            "Expected exact-applier reactions after new or stacked marks, with bounded node-owned Tweens and no polling, audio, gameplay commands, saved state, or hitbox Controls.");
    }

    private static string Sha256(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
}
