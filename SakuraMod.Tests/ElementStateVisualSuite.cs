using SakuraMod.SakuraModCode.Character;

public sealed class ElementStateVisualSuite
{
    [Fact]
    public void EarthWallSlotStaysBesideTheHitbox()
    {
        // Floor sits below the mount point. The inset is how far down the wall shader
        // draws its contact line inside its own rect. The wall stands on the visual
        // ground line, which sits above the hitbox floor where the HP/Block strip is.
        const float floorY = 20f;
        const float contactInset = 40.3f;
        const float groundLift = 44f;
        const float earthClearance = 4f;
        var standard = SakuraElementSlotLayout.EarthFromBody(new Godot.Vector2(264f, 468f), 0f, floorY, contactInset);
        var chibi = SakuraElementSlotLayout.EarthFromBody(new Godot.Vector2(264f, 354f), 0f, floorY, contactInset);
        var wideOutfit = SakuraElementSlotLayout.EarthFromBody(new Godot.Vector2(330f, 383f), -6f, floorY, contactInset);

        // Measured from the real hitbox edge rather than a fraction of a clamped width:
        // the HP bar is exactly as wide as the hitbox, and outfit hitboxes run wide.
        RegressionTestHarness.Require(
            standard.X == 132f + earthClearance
            && chibi.X == 132f + earthClearance
            && wideOutfit.X == -6f + 165f + earthClearance
            && standard.Y == floorY - groundLift - contactInset
            && chibi.Y == floorY - groundLift - contactInset,
            "Expected the earth wall a fixed clearance past the hitbox's right edge, on the visual ground.");

        // A floor reported above the mount point degrades to mount height instead of
        // flinging the wall up through the standee.
        var degenerate = SakuraElementSlotLayout.EarthFromBody(new Godot.Vector2(264f, 468f), 0f, -400f, contactInset);
        RegressionTestHarness.Require(
            degenerate.Y == -groundLift - contactInset && SakuraElementSlotLayout.GroundVisualLift == groundLift,
            "Expected a floor above the mount point to clamp to the mount height.");
    }

    [Fact]
    public void StageKeepsNoPersistentElementMarks()
    {
        var scene = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/scenes/combat/sakura_element_state_visuals.tscn"));
        var visuals = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraElementStateVisuals.cs"));

        // The Lasin-board HUD is the only persistent presentation; the stage holds
        // nothing but the earth wall, hidden until a trigger raises it.
        RegressionTestHarness.Require(
            scene.Contains("[node name=\"EarthSlot\" type=\"Node2D\" parent=\".\"]", StringComparison.Ordinal)
            && scene.Contains("[node name=\"EarthWall\" type=\"ColorRect\" parent=\"EarthSlot\"]", StringComparison.Ordinal)
            && scene[scene.IndexOf("[node name=\"EarthWall\"", StringComparison.Ordinal)..].Contains("visible = false", StringComparison.Ordinal)
            && !scene.Contains("Spirit", StringComparison.Ordinal)
            && !scene.Contains("Ambient", StringComparison.Ordinal)
            && !scene.Contains("FireSlot", StringComparison.Ordinal),
            "Expected the stage scene to hold only the hidden earth wall.");

        // With nothing persistent to keep in sync, the stage never follows Power changes.
        RegressionTestHarness.Require(
            !visuals.Contains("PowerApplied", StringComparison.Ordinal)
            && !visuals.Contains("ReadActive", StringComparison.Ordinal)
            && !visuals.Contains("SakuraNativeSteppedFire", StringComparison.Ordinal)
            && !visuals.Contains("NotifyIconic", StringComparison.Ordinal),
            "Expected the stage visuals to carry no persistent state, spirits, or entry beats.");

        foreach (var card in new[] { "Firey", "Windy", "Watery", "Earthy" })
        {
            var source = File.ReadAllText(RegressionTestHarness.FindRepoFile($"SakuraModCode/Cards/ClowSakura/{card}.cs"));
            RegressionTestHarness.Require(
                !source.Contains("NotifyIconic", StringComparison.Ordinal),
                $"Expected {card} to leave entry feedback to the HUD's power-driven light-up.");
        }

        foreach (var retired in new[] { "windy", "watery", "firey_ambient", "windy_ambient", "watery_ambient" })
        {
            RegressionTestHarness.Require(
                !File.Exists(Path.Combine(
                    Path.GetDirectoryName(RegressionTestHarness.FindRepoFile(
                        "SakuraMod/shaders/card_vfx/sakura_element_state_earthy.gdshader"))!,
                    $"sakura_element_state_{retired}.gdshader")),
                $"Expected the retired {retired} element shader to be gone.");
        }
    }

    [Fact]
    public void EarthWallSharesTheEdgeLanguage()
    {
        var common = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/shaders/card_vfx/sakura_element_state_common.gdshaderinc"));
        var earth = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/shaders/card_vfx/sakura_element_state_earthy.gdshader"));
        var visuals = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraElementStateVisuals.cs"));

        // The wall plays against dark stages and a white costume at once: a bright inner
        // rim and a bounded SDF halo read on both.
        RegressionTestHarness.Require(
            common.Contains("#ifndef SAKURA_ELEMENT_STATE_COMMON_INCLUDED", StringComparison.Ordinal)
            && common.Contains("float element_halo(", StringComparison.Ordinal)
            && common.Contains("float element_rim(", StringComparison.Ordinal)
            && !common.Contains("element_ambient_level", StringComparison.Ordinal)
            && !common.Contains("SCREEN_TEXTURE", StringComparison.Ordinal)
            && !System.Text.RegularExpressions.Regex.IsMatch(
                common, @"^\s*uniform\s", System.Text.RegularExpressions.RegexOptions.Multiline),
            "Expected one guarded, uniform-free edge include with a bounded halo and an inner rim.");

        // One-shot only: the wall is driven by trigger_progress and draws nothing at rest.
        RegressionTestHarness.Require(
            earth.Contains("#include \"res://SakuraMod/shaders/card_vfx/sakura_element_state_common.gdshaderinc\"", StringComparison.Ordinal)
            && earth.Contains("element_halo(", StringComparison.Ordinal)
            && earth.Contains("element_rim(", StringComparison.Ordinal)
            && earth.Contains("uniform float trigger_progress", StringComparison.Ordinal)
            && earth.Contains("const float CONTACT_Y = 0.36;", StringComparison.Ordinal)
            && !earth.Contains("state_alpha", StringComparison.Ordinal)
            && !earth.Contains("summon_progress", StringComparison.Ordinal)
            && !earth.Contains("ambient_alpha", StringComparison.Ordinal)
            && visuals.Contains("EarthContactSurfaceFraction = 0.36f", StringComparison.Ordinal),
            "Expected the earth shader to draw only the trigger wall, rooted on the shared contact line.");

        RegressionTestHarness.Require(
            visuals.Contains("[ScenePath, EarthShaderPath, CommonShaderIncludePath]", StringComparison.Ordinal),
            "Expected the element asset list to name exactly the scene, the wall shader, and its include.");
    }

    [Fact]
    public void EarthGameplayKeepsVisualNotificationsNonAuthoritative()
    {
        var power = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Powers/SourceCards/ClassicEarthyPower.cs"));
        var visuals = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraElementStateVisuals.cs"));

        RegressionTestHarness.Require(
            power.IndexOf("NotifyEarthTriggered(Owner);", StringComparison.Ordinal)
                < power.IndexOf("await CreatureCmd.GainBlock(", StringComparison.Ordinal)
            && !visuals.Contains("CreatureCmd", StringComparison.Ordinal)
            && !visuals.Contains("GainBlock", StringComparison.Ordinal),
            "Expected gameplay to own the Block while notifying visuals in place.");

        // Every earth card played under the state triggers, so consecutive plays land
        // inside a wall that is still forming: the tween must be killable, and the drive
        // must settle at 0 so an interrupted beat never leaves the wall half-formed.
        var triggerBody = visuals[visuals.IndexOf("internal void PlayEarthTrigger()", StringComparison.Ordinal)..];
        triggerBody = triggerBody[..triggerBody.IndexOf("private bool IsCurrentMount()", StringComparison.Ordinal)];
        RegressionTestHarness.Require(
            triggerBody.Contains("KillTween(ref _earthTriggerTween);", StringComparison.Ordinal)
            && triggerBody.LastIndexOf("\"trigger_progress\", 0f", StringComparison.Ordinal)
                > triggerBody.IndexOf("0f, 1f, EarthTriggerDuration", StringComparison.Ordinal)
            && triggerBody.Contains("_wall.Visible = false;", StringComparison.Ordinal)
            && triggerBody.Contains("ResolveFacingSign()", StringComparison.Ordinal),
            "Expected a killable earth beat that samples facing once and settles hidden at zero.");
    }

    [Fact]
    public void TriggerBeamsLeaveTheirMedallionWithoutOwningGameplay()
    {
        var visuals = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraElementStateVisuals.cs"));
        var fire = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Powers/SourceCards/ClassicFireyPower.cs"));
        var wind = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Powers/SourceCards/ClassicWindyPower.cs"));
        var water = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Powers/SourceCards/ClassicWateryPower.cs"));

        RegressionTestHarness.Require(
            fire.Contains("HittableEnemies", StringComparison.Ordinal)
            && fire.Contains("NotifyFireTriggered", StringComparison.Ordinal)
            && wind.IndexOf("NotifyWindTriggered(Owner);", StringComparison.Ordinal)
                < wind.IndexOf("await CardPileCmd.Draw(choiceContext, 1, Owner.Player!, false);", StringComparison.Ordinal)
            && water.IndexOf("NotifyWaterTriggered(Owner);", StringComparison.Ordinal)
                < water.IndexOf("await PlayerCmd.GainEnergy(1, Owner.Player!);", StringComparison.Ordinal),
            "Expected gameplay to own targets, the draw, and the energy while notifying visuals in place.");

        RegressionTestHarness.Require(
            !visuals.Contains("HittableEnemies", StringComparison.Ordinal)
            && !visuals.Contains("CreatureCmd.Damage", StringComparison.Ordinal)
            && !visuals.Contains("CardPileCmd", StringComparison.Ordinal)
            && !visuals.Contains("PlayerCmd", StringComparison.Ordinal)
            && !visuals.Contains("TriggerCounter", StringComparison.Ordinal),
            "Expected trigger visuals to never select targets, deal damage, draw, or grant energy.");

        // The local player's beam leaves its HUD medallion; a teammate has no HUD on this
        // machine, so theirs falls back to the character. Fire stays bounded in targets.
        RegressionTestHarness.Require(
            visuals.Contains("SakuraElementStateHud.TryGetMedallionCenter(owner.Player, element, out var medallion)", StringComparison.Ordinal)
            && visuals.Contains("node.Visuals.VfxSpawnPosition.GlobalPosition", StringComparison.Ordinal)
            && visuals.Contains("MaxTriggerTargets", StringComparison.Ordinal),
            "Expected beams to start at the medallion, fall back to the character, and stay bounded.");

        foreach (var element in new[] { "Fire", "Wind", "Water", "Earth" })
        {
            var notify = visuals[visuals.IndexOf($"internal static void Notify{element}Triggered(", StringComparison.Ordinal)..];
            notify = notify[..notify.IndexOf("\n    }", StringComparison.Ordinal)];
            RegressionTestHarness.Require(
                notify.Contains("SakuraModConfig.IsCardVfxEnabled()", StringComparison.Ordinal)
                && notify.Contains($"SakuraElementStateHud.NotifyTriggered(owner.Player, SakuraElement.{element});", StringComparison.Ordinal),
                $"Expected the {element} trigger to be config-gated and to flash its medallion.");
        }
    }

    [Fact]
    public void ElementStateTriggerEvaluationStaysSynchronized()
    {
        var power = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Powers/SourceCards/SakuraElementStatePower.cs"));

        // A Before/After flag pair desyncs silently when a play is cancelled or
        // nested (AutoPlay), and a one-sided TriggerElement is a multiplayer
        // checksum divergence: triggers must evaluate directly from the play.
        RegressionTestHarness.Require(
            !power.Contains("_wasActiveForCardPlayed", StringComparison.Ordinal)
            && !power.Contains("BeforeCardPlayed", StringComparison.Ordinal)
            && power.Contains("play.Card?.Owner?.Creature != Owner", StringComparison.Ordinal),
            "Expected element triggers to evaluate directly from the synced play instead of a Before/After flag pair.");

        // Entering a state is part of resolving the play that grants it, so that play records
        // the entry and the trigger it would otherwise fire on itself consumes the record.
        // Without this, Clow Sword's own Magic Charge entry pays off on the same play.
        var elementState = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraElementState.cs"));
        var sourceCard = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/SakuraSourceCard.cs"));
        RegressionTestHarness.Require(
            power.Contains("ConsumeEnteredByPlay", StringComparison.Ordinal)
            && elementState.Contains("MarkEnteredByPlay", StringComparison.Ordinal)
            && sourceCard.Contains("EnterElementState", StringComparison.Ordinal),
            "Expected the play that enters an element state to be recorded so the state does not "
            + "trigger on the play that granted it.");

        // Trigger counters gate synced commands (Draw / GainEnergy), so they must
        // live in the synced SavedAttachedState, not plain fields.
        var wind = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Powers/SourceCards/ClassicWindyPower.cs"));
        var watery = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Powers/SourceCards/ClassicWateryPower.cs"));
        RegressionTestHarness.Require(
            wind.Contains("SavedAttachedState", StringComparison.Ordinal)
            && !wind.Contains("private int _counter", StringComparison.Ordinal)
            && watery.Contains("SavedAttachedState", StringComparison.Ordinal)
            && !watery.Contains("private int _counter", StringComparison.Ordinal),
            "Expected Windy/Watery trigger counters to be synced via SavedAttachedState instead of plain fields.");
    }

    [Fact]
    public void ElementStateVisualsUseLifecycleCleanupWithoutPolling()
    {
        var source = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraElementStateVisuals.cs"));

        RegressionTestHarness.Require(
            source.Contains("CombatManager.Instance.CombatEnded += OnCombatEnded", StringComparison.Ordinal)
            && source.Contains("CombatManager.Instance.CombatEnded -= OnCombatEnded", StringComparison.Ordinal)
            && source.Contains("_root.TreeExiting += OnTreeExiting", StringComparison.Ordinal)
            && source.Contains("_root.TreeExiting -= OnTreeExiting", StringComparison.Ordinal)
            && source.Contains("_creature.Died += OnDied", StringComparison.Ordinal)
            && source.Contains("_creature.Died -= OnDied", StringComparison.Ordinal)
            && source.Contains("tween.Chain().TweenCallback(Callable.From(root.QueueFreeSafely));", StringComparison.Ordinal)
            && !source.Contains("_Process", StringComparison.Ordinal)
            && !source.Contains("GpuParticles", StringComparison.Ordinal),
            "Expected event-owned cleanup, self-freeing beams, and no frame polling or unbounded particles.");
    }
}
