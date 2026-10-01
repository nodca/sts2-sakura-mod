using SakuraMod.SakuraModCode;
using SakuraMod.SakuraModCode.Cards;

public sealed class SilentQuietVisualSuite
{
    [Fact]
    public void SilentRouteDeclaresTheVeilShaderBesideTheMagicCircle()
    {
        foreach (var card in new MegaCrit.Sts2.Core.Models.CardModel[] { new ClowSilent(), new SakuraSilent() })
        {
            var declared = SakuraCardVfxAssets.RunAssetPaths(card).ToArray();
            // Era-backed cards also carry the shared magic circle; nothing else.
            RegressionTestHarness.Require(
                SilentQuietVisual.AssetPaths.All(declared.Contains)
                && declared.Except(SilentQuietVisual.AssetPaths).All(SakuraMagicCirclePresenter.AssetPaths.Contains)
                && declared.Length == declared.Distinct(StringComparer.Ordinal).Count(),
                $"Expected {card.GetType().Name} to declare the Silent veil shader plus only the shared magic circle.");
        }
    }

    [Fact]
    public void SilentShadersSpeakTheSharedCelLanguage()
    {
        foreach (var path in new[]
                 {
                     "SakuraMod/shaders/card_vfx/silent_veil.gdshader"
                 })
        {
            var shader = File.ReadAllText(RegressionTestHarness.FindRepoFile(path));
            RegressionTestHarness.Require(
                shader.Contains("#include \"res://SakuraMod/shaders/card_vfx/cel_vfx.gdshaderinc\"", StringComparison.Ordinal)
                && shader.Contains("cel_ink(", StringComparison.Ordinal)
                && shader.Contains("cel_step_clock(", StringComparison.Ordinal)
                && !shader.Contains("SCREEN_UV", StringComparison.Ordinal),
                $"Expected {path} to take its ink and stepped clock from the shared include and to leave the scene untouched.");
        }
    }

    [Fact]
    public void VeilStaysInsideTheWaterSlotAndKeepsRoomForItsRings()
    {
        foreach (var body in new[] { new Godot.Vector2(120f, 260f), new Godot.Vector2(180f, 360f) })
        {
            var anchor = new CelVfxGeometry.CasterAnchor(Godot.Vector2.Zero, Godot.Vector2.Zero, body, 1f, false);
            var layout = SilentQuietVisual.Layout.From(anchor);

            // Water owns the floor at -0.58 of the body width; the veil's half-width
            // must stay inside it so the two persistent marks never overlap.
            RegressionTestHarness.Require(
                layout.VeilRadii.X < body.X * 0.58f,
                "Expected the veil to stay narrower than the water state's floor slot.");
            RegressionTestHarness.Require(
                layout.VeilSize.X > layout.VeilRadii.X * 2f && layout.VeilSize.Y > layout.VeilRadii.Y * 2f,
                "Expected padding around the veil for its gathering and swallow rings.");
        }
    }

    [Fact]
    public void SilentGameplayKeepsVisualNotificationsNonAuthoritative()
    {
        var card = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/ClowSakura/Silent.cs"));
        var visuals = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Visuals/Classic/SilentQuietVisual.cs"));

        // Every Silent play summons the veil only after its Buffer exists, and
        // never awaits the presentation.
        RegressionTestHarness.Require(
            card.Split("SilentQuietVisual.NotifyVeilSummoned(Owner.Creature);").Length - 1 == 3
            && !card.Contains("await SilentQuietVisual", StringComparison.Ordinal),
            "Expected all three Silent plays to notify the veil synchronously.");
        foreach (var block in card.Split("protected override async Task Play").Skip(1))
        {
            if (!block.Contains("NotifyVeilSummoned", StringComparison.Ordinal))
                continue;
            RegressionTestHarness.Require(
                block.IndexOf("ApplyPower<BufferPower>", StringComparison.Ordinal)
                    < block.IndexOf("NotifyVeilSummoned", StringComparison.Ordinal),
                "Expected the veil to be summoned after the Buffer it reads has been applied.");
        }

        // The restriction stays purely gameplay: no Silent visual hook on its Powers.
        foreach (var path in new[]
                 {
                     "SakuraModCode/Powers/SourceCards/ClassicSilentPendingPower.cs",
                     "SakuraModCode/Powers/SourceCards/ClassicSilentNoAttackPower.cs"
                 })
        {
            RegressionTestHarness.Require(
                !File.ReadAllText(RegressionTestHarness.FindRepoFile(path)).Contains("SilentQuietVisual", StringComparison.Ordinal),
                $"Expected {path} to carry no Silent veil hook.");
        }

        // The visual reads Powers; it never changes them.
        RegressionTestHarness.Require(
            !visuals.Contains("PowerCmd", StringComparison.Ordinal)
            && !visuals.Contains("CreatureCmd", StringComparison.Ordinal)
            && !visuals.Contains("CardCmd", StringComparison.Ordinal)
            && visuals.Contains("GetPowerAmount<BufferPower>()", StringComparison.Ordinal),
            "Expected the Silent veil to read the synced Buffer and issue no gameplay commands.");
    }

    [Fact]
    public void SilentVisualIsGatedAndReleasesEverySubscription()
    {
        var visuals = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Visuals/Classic/SilentQuietVisual.cs"));

        RegressionTestHarness.Require(
            visuals.Contains("TestMode.IsOn", StringComparison.Ordinal)
            && visuals.Contains("SakuraModConfig.IsCardVfxEnabled()", StringComparison.Ordinal)
            && visuals.Contains("MouseFilter = Control.MouseFilterEnum.Ignore", StringComparison.Ordinal),
            "Expected the Silent visual to respect the optional VFX gate and never take Sakura's input.");

        // A ShaderMaterial reports an unset uniform as Nil; tweening it as a
        // property crashed the game on the first play. Uniforms are driven through
        // method tweeners with explicit ends instead.
        RegressionTestHarness.Require(
            !visuals.Contains("TweenProperty(", StringComparison.Ordinal)
            && visuals.Contains("TweenMethod(", StringComparison.Ordinal)
            && visuals.Contains("InitialFloats", StringComparison.Ordinal),
            "Expected the veil to drive shader uniforms through method tweeners from initialised values.");

        foreach (var subscription in new[]
                 {
                     "_creature.PowerApplied", "_creature.PowerIncreased", "_creature.PowerDecreased",
                     "_creature.PowerRemoved", "_creature.Died", "CombatManager.Instance.CombatEnded",
                     "_root.TreeExiting"
                 })
        {
            RegressionTestHarness.Require(
                visuals.Contains($"{subscription} +=", StringComparison.Ordinal)
                && visuals.Contains($"{subscription} -=", StringComparison.Ordinal),
                $"Expected {subscription} to be released on dispose.");
        }
    }
}
