using SakuraMod.SakuraModCode.Character;

public sealed class ElementStateVisualSuite
{
    [Fact]
    public void FixedElementSlotsStayStableAcrossBodySizes()
    {
        // Floor sits below the mount point. The earth inset is how far down its shader
        // draws its contact line inside its own rect. Earth and the ground ambient layers
        // stand on the visual ground line, which sits above the hitbox floor where the
        // HP/Block strip is.
        const float floorY = 20f;
        const float contactInset = 40.3f;
        const float groundLift = 44f;
        const float earthClearance = 4f;
        var standard = SakuraElementSlotLayout.FromBody(
            new Godot.Vector2(264f, 468f), 0f, floorY, contactInset);
        var chibi = SakuraElementSlotLayout.FromBody(
            new Godot.Vector2(264f, 354f), 0f, floorY, contactInset);
        var wideOutfit = SakuraElementSlotLayout.FromBody(
            new Godot.Vector2(330f, 383f), -6f, floorY, contactInset);

        // Fire holds the centre axis alone.
        RegressionTestHarness.Require(
            standard.Fire.X == 0f
            && chibi.Fire.X == 0f,
            "Expected fire to hold the centre axis at every body size.");

        // Earth stands just past the hitbox's right edge, measured from the real hitbox
        // rather than a fraction of a clamped width: the HP bar is exactly as wide as the
        // hitbox, and outfit hitboxes run wider than the clamp, so only the edge keeps the
        // cluster beside the legs at every outfit.
        RegressionTestHarness.Require(
            standard.Earth.X == 132f + earthClearance
            && chibi.Earth.X == 132f + earthClearance
            && wideOutfit.Earth.X == -6f + 165f + earthClearance,
            "Expected earth to stand a fixed clearance past the real hitbox's right edge.");

        // Wind and water hover in the open air on the left, the side that is dark and
        // empty at chest height and above; neither crosses the white costume. Wind is the
        // upper of the two and water stays above Kero's waist-height perch. FromBody takes
        // no facing argument, and these fixed signs stop one being added: FacingSign is
        // republished every frame, so a mirrored slot would jump across the character.
        foreach (var layout in new[] { standard, chibi })
            RegressionTestHarness.Require(
                layout.Wind.X < layout.Water.X
                && layout.Water.X < 0f
                && layout.Fire.Y < layout.Wind.Y
                && layout.Wind.Y < layout.Water.Y
                && layout.Water.Y < -25f,
                "Expected wind above water on the open left, fire above both, water clear of Kero.");

        // Ground-rooted marks derive from the real floor rather than from body height: a
        // fraction of body height cannot express "on the floor", because the mount
        // point's own height above the ground is not derivable from body size. Earth and
        // the two ground ambients share one visual ground line, and water's rings take
        // the left of the floor while earth's cracks take the right.
        RegressionTestHarness.Require(
            standard.Earth.Y == floorY - groundLift - contactInset
            && chibi.Earth.Y == floorY - groundLift - contactInset
            && standard.FireAmbient == new Godot.Vector2(0f, floorY - groundLift)
            && wideOutfit.FireAmbient == new Godot.Vector2(-6f, floorY - groundLift)
            && standard.WaterAmbient.Y == floorY - groundLift
            && standard.WaterAmbient.X < 0f
            && standard.WindAmbient.X == 0f
            && standard.WindAmbient.Y < 0f,
            "Expected earth and the ground ambients to share the visual ground line on opposite sides.");

        // A degenerate hitbox can report a floor level with or above the mount point.
        // The floor is clamped once and every ground slot derives from that shared safe
        // value, so a bad floor degrades them together.
        var degenerate = SakuraElementSlotLayout.FromBody(
            new Godot.Vector2(264f, 468f), 0f, -400f, contactInset);
        RegressionTestHarness.Require(
            degenerate.Earth.Y == -groundLift - contactInset
            && degenerate.FireAmbient.Y == -groundLift
            && degenerate.WaterAmbient.Y == -groundLift,
            "Expected a floor above the mount point to clamp once for every ground slot.");
    }

    [Fact]
    public void ElementSpiritsShareOneEdgeLanguage()
    {
        var common = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/shaders/card_vfx/sakura_element_state_common.gdshaderinc"));
        var fire = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/shaders/card_vfx/sakura_element_state_firey.gdshader"));
        var earth = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/shaders/card_vfx/sakura_element_state_earthy.gdshader"));
        var wind = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/shaders/card_vfx/sakura_element_state_windy.gdshader"));
        var water = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/shaders/card_vfx/sakura_element_state_watery.gdshader"));
        var visuals = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraElementStateVisuals.cs"));
        const string include =
            "#include \"res://SakuraMod/shaders/card_vfx/sakura_element_state_common.gdshaderinc\"";

        // Spirits sit on dark stages and against a white costume at once, where a dark
        // outline is invisible on one and heavy on the other. A bright inner rim and a
        // bounded SDF halo read on both, so the family owns exactly those two, once.
        RegressionTestHarness.Require(
            common.Contains("#ifndef SAKURA_ELEMENT_STATE_COMMON_INCLUDED", StringComparison.Ordinal)
            && common.Contains("cel_vfx.gdshaderinc", StringComparison.Ordinal)
            && common.Contains("ELEMENT_HALO_PX", StringComparison.Ordinal)
            && common.Contains("float element_halo(", StringComparison.Ordinal)
            && common.Contains("float element_rim(", StringComparison.Ordinal)
            && common.Contains("float element_ambient_level(", StringComparison.Ordinal)
            && !common.Contains("SCREEN_TEXTURE", StringComparison.Ordinal)
            && !System.Text.RegularExpressions.Regex.IsMatch(
                common, @"^\s*uniform\s", System.Text.RegularExpressions.RegexOptions.Multiline),
            "Expected one guarded, uniform-free edge include with a bounded halo and an inner rim.");

        // Fire is the reference the others are matched to: its shape stays, and it joins
        // the family through the shared halo alone.
        RegressionTestHarness.Require(
            fire.Contains(include, StringComparison.Ordinal)
            && fire.Contains("element_halo(", StringComparison.Ordinal)
            && earth.Contains(include, StringComparison.Ordinal)
            && earth.Contains("element_halo(", StringComparison.Ordinal)
            && earth.Contains("element_rim(", StringComparison.Ordinal)
            && wind.Contains(include, StringComparison.Ordinal)
            && wind.Contains("element_halo(", StringComparison.Ordinal)
            && wind.Contains("element_rim(", StringComparison.Ordinal)
            && water.Contains(include, StringComparison.Ordinal)
            && water.Contains("element_halo(", StringComparison.Ordinal)
            && water.Contains("element_rim(", StringComparison.Ordinal),
            "Expected every spirit to take its edges from the shared include.");

        RegressionTestHarness.Require(
            visuals.Contains("CommonShaderIncludePath", StringComparison.Ordinal)
            && visuals.Contains("FireAmbientShaderPath", StringComparison.Ordinal)
            && visuals.Contains("WindAmbientShaderPath", StringComparison.Ordinal)
            && visuals.Contains("WaterAmbientShaderPath", StringComparison.Ordinal),
            "Expected the shared include and the ambient shader in the element asset list.");
    }

    [Fact]
    public void AmbientLayersStayFaintBoundedAndOptional()
    {
        var scene = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/scenes/combat/sakura_element_state_visuals.tscn"));
        var visuals = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraElementStateVisuals.cs"));
        var firstSpiritSlot = scene.IndexOf("[node name=\"FireSlot\" type=\"Node2D\" parent=\".\"]", StringComparison.Ordinal);

        // Ambient is the "the air has changed" layer and must sit under every spirit, so
        // each ambient slot comes before the first spirit slot among the root's children.
        foreach (var (slot, rect, shaderName, budget) in new[]
                 {
                     ("FireAmbientSlot", "FireyAmbient", "firey_ambient", "EMBER_COUNT"),
                     ("WindAmbientSlot", "WindyAmbient", "windy_ambient", "STRAND_COUNT"),
                     ("WaterAmbientSlot", "WateryAmbient", "watery_ambient", "RING_COUNT")
                 })
        {
            var shader = File.ReadAllText(RegressionTestHarness.FindRepoFile(
                $"SakuraMod/shaders/card_vfx/sakura_element_state_{shaderName}.gdshader"));
            var slotIndex = scene.IndexOf($"[node name=\"{slot}\" type=\"Node2D\" parent=\".\"]", StringComparison.Ordinal);
            RegressionTestHarness.Require(
                slotIndex >= 0
                && slotIndex < firstSpiritSlot
                && scene.Contains($"[node name=\"{rect}\" type=\"ColorRect\" parent=\"{slot}\"]", StringComparison.Ordinal),
                $"Expected {rect} to draw beneath every spirit.");
            RegressionTestHarness.Require(
                shader.Contains("uniform float state_alpha", StringComparison.Ordinal)
                && shader.Contains("uniform float ambient_alpha", StringComparison.Ordinal)
                && shader.Contains("uniform float ambient_boost", StringComparison.Ordinal)
                && shader.Contains("element_ambient_level(", StringComparison.Ordinal)
                && shader.Contains(budget, StringComparison.Ordinal),
                $"Expected {rect} to be bounded and driven by state, rest and boost levels.");
        }

        // Rest levels stay faint so four states can share the screen, and each one-shot
        // boost is a held, killable tween so a trigger landing mid-fall restarts cleanly.
        foreach (var element in new[] { "Fire", "Earth", "Wind", "Water" })
            RegressionTestHarness.Require(
                System.Text.RegularExpressions.Regex.IsMatch(
                    visuals, $@"{element}AmbientRestAlpha = 0\.[2-5]\d*f;")
                && visuals.Contains($"private Tween? _{element.ToLowerInvariant()}BoostTween;", StringComparison.Ordinal)
                && visuals.Contains($"KillTween(ref _{element.ToLowerInvariant()}BoostTween);", StringComparison.Ordinal),
                $"Expected a faint {element} ambient rest level and a killable boost tween.");

        // Ambient is atmosphere: with optional card VFX off it rests at zero while the
        // spirits keep the state readable.
        var preference = visuals[visuals.IndexOf("private void ApplyAmbientPreference()", StringComparison.Ordinal)..];
        preference = preference[..preference.IndexOf("private void PlayAmbientBoost(", StringComparison.Ordinal)];
        RegressionTestHarness.Require(
            preference.Contains("SakuraModConfig.IsCardVfxEnabled()", StringComparison.Ordinal)
            && preference.Contains("_fireAmbient.Visible = enabled;", StringComparison.Ordinal)
            && preference.Contains("_windAmbient.Visible = enabled;", StringComparison.Ordinal)
            && preference.Contains("_waterAmbient.Visible = enabled;", StringComparison.Ordinal)
            && preference.Contains("enabled ? EarthAmbientRestAlpha : 0f", StringComparison.Ordinal),
            "Expected ambient layers to follow the optional card-VFX switch.");

        // Wind's gust is the one intermittent ambient: a standing streak over the white
        // costume is exactly what made the old wind mark vanish.
        var gust = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/shaders/card_vfx/sakura_element_state_windy_ambient.gdshader"));
        RegressionTestHarness.Require(
            gust.Contains("GUST_PERIOD", StringComparison.Ordinal)
            && gust.Contains("GUST_SPAN", StringComparison.Ordinal),
            "Expected wind's ambient to pass as a periodic gust rather than stand on the body.");
    }

    [Fact]
    public void HybridFireResourcesExposeBoundedThreeLayerContract()
    {
        var scene = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/scenes/combat/sakura_element_state_visuals.tscn"));
        var shader = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/shaders/card_vfx/sakura_element_state_firey.gdshader"));

        foreach (var slot in new[] { "FireSlot", "WindSlot", "EarthSlot", "WaterSlot" })
            RegressionTestHarness.Require(
                scene.Contains($"[node name=\"{slot}\" type=\"Node2D\" parent=\".\"]", StringComparison.Ordinal),
                $"Expected the fixed element scene to reserve {slot}.");

        RegressionTestHarness.Require(
            scene.Contains("[node name=\"FireyEmber\" type=\"ColorRect\" parent=\"FireSlot\"]", StringComparison.Ordinal)
            && scene.Contains("mouse_filter", StringComparison.Ordinal)
            && shader.Contains("uniform float state_alpha", StringComparison.Ordinal)
            && shader.Contains("uniform float summon_progress", StringComparison.Ordinal)
            && shader.Contains("uniform float trigger_progress", StringComparison.Ordinal),
            "Expected the hybrid fire ember to live in FireSlot, ignore the mouse, and carry its own state controls.");
    }

    [Fact]
    public void HybridWindResourcesExposeAVortexWithRibbons()
    {
        var scene = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/scenes/combat/sakura_element_state_visuals.tscn"));
        var shader = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/shaders/card_vfx/sakura_element_state_windy.gdshader"));

        RegressionTestHarness.Require(
            scene.Contains("[node name=\"WindySpirit\" type=\"ColorRect\" parent=\"WindSlot\"]", StringComparison.Ordinal)
            && scene.Contains("sakura_element_state_windy.gdshader", StringComparison.Ordinal),
            "Expected the wind spirit to live in the reserved WindSlot with its own shader.");

        RegressionTestHarness.Require(
            shader.Contains("uniform float state_alpha", StringComparison.Ordinal)
            && shader.Contains("uniform float summon_progress", StringComparison.Ordinal)
            && shader.Contains("uniform float trigger_progress", StringComparison.Ordinal),
            "Expected the wind spirit to carry its own state controls.");

        // A vortex is one body whose arms are the motion, so it can spin forever without
        // reading as discrete objects circling a centre — the failure every orbit of
        // carried shapes had. Ribbons trailing off it say which way the air goes.
        RegressionTestHarness.Require(
            shader.Contains("SPIN_HZ", StringComparison.Ordinal)
            && shader.Contains("SPIRAL_TIGHT", StringComparison.Ordinal)
            && shader.Contains("vec2 ribbon(", StringComparison.Ordinal)
            && !shader.Contains("float petal(", StringComparison.Ordinal),
            "Expected a spinning spiral vortex with trailing ribbons rather than carried petals.");

        // Visual work stays a fixed constant, never scaled by combat state.
        RegressionTestHarness.Require(
            shader.Contains("ARM_COUNT", StringComparison.Ordinal)
            && shader.Contains("RIBBON_COUNT", StringComparison.Ordinal),
            "Expected wind visual work to remain explicitly bounded.");
    }

    [Fact]
    public void HybridWaterResourcesExposeASwimmingFishTailedSpirit()
    {
        var scene = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/scenes/combat/sakura_element_state_visuals.tscn"));
        var shader = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/shaders/card_vfx/sakura_element_state_watery.gdshader"));

        RegressionTestHarness.Require(
            scene.Contains("[node name=\"WaterySpirit\" type=\"ColorRect\" parent=\"WaterSlot\"]", StringComparison.Ordinal)
            && scene.Contains("sakura_element_state_watery.gdshader", StringComparison.Ordinal),
            "Expected the water spirit to live in the reserved WaterSlot with its own shader.");

        RegressionTestHarness.Require(
            shader.Contains("uniform float state_alpha", StringComparison.Ordinal)
            && shader.Contains("uniform float summon_progress", StringComparison.Ordinal)
            && shader.Contains("uniform float trigger_progress", StringComparison.Ordinal),
            "Expected the water spirit to carry its own state controls.");

        // Water's failure mode is the glowing blue orb. Swimming — a beating tail and a
        // body turned into its heading — is what only something made of water does.
        // The swim path is Gerono's figure-eight, which has no period seam to jump at.
        RegressionTestHarness.Require(
            shader.Contains("vec2 gerono(", StringComparison.Ordinal)
            && shader.Contains("SWIM_HZ", StringComparison.Ordinal)
            && shader.Contains("TAIL_HZ", StringComparison.Ordinal),
            "Expected a continuous figure-eight swim with a beating tail.");

        // Swimming left would turn the spirit upside down, so its frame mirrors to keep
        // its back up.
        RegressionTestHarness.Require(
            shader.Contains("heading.x < 0.0 ? -1.0 : 1.0", StringComparison.Ordinal),
            "Expected the swim frame to keep the spirit's back up in both directions.");

        // Body and tail fuse, so the tail flexes out of the body. Water may use the
        // shared smooth union; earth may not. Shared math, not a local copy.
        RegressionTestHarness.Require(
            shader.Contains("cel_smin(", StringComparison.Ordinal)
            && !shader.Contains("float smin(", StringComparison.Ordinal),
            "Expected the shared smooth union to join body and tail.");

        RegressionTestHarness.Require(
            shader.Contains("BUBBLE_COUNT", StringComparison.Ordinal)
            && shader.Contains("GATHER_COUNT", StringComparison.Ordinal),
            "Expected water visual work to remain explicitly bounded.");
    }

    [Fact]
    public void HybridEarthResourcesExposeARootedCrystalClusterAndAPalisade()
    {
        var scene = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/scenes/combat/sakura_element_state_visuals.tscn"));
        var shader = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/shaders/card_vfx/sakura_element_state_earthy.gdshader"));
        var visuals = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraElementStateVisuals.cs"));

        RegressionTestHarness.Require(
            scene.Contains("[node name=\"EarthySpire\" type=\"ColorRect\" parent=\"EarthSlot\"]", StringComparison.Ordinal)
            && scene.Contains("sakura_element_state_earthy.gdshader", StringComparison.Ordinal),
            "Expected the earth spirit to live in the reserved EarthSlot with its own shader.");

        RegressionTestHarness.Require(
            shader.Contains("uniform float state_alpha", StringComparison.Ordinal)
            && shader.Contains("uniform float summon_progress", StringComparison.Ordinal)
            && shader.Contains("uniform float trigger_progress", StringComparison.Ordinal)
            && shader.Contains("uniform float ambient_alpha", StringComparison.Ordinal)
            && shader.Contains("uniform float ambient_boost", StringComparison.Ordinal)
            && shader.Contains("uniform float facing", StringComparison.Ordinal),
            "Expected the earth spirit to carry its state, ambient and facing controls.");

        // Rooted and upright. Stage rocks are wide and low; prism crystals with short
        // pitched roofs break the ground line and cannot be read as scenery, where long
        // thin tips read as thorns.
        RegressionTestHarness.Require(
            shader.Contains("vec2 crystal(", StringComparison.Ordinal)
            && shader.Contains("TIP_SLOPE", StringComparison.Ordinal)
            && shader.Contains("VEIN_GOLD", StringComparison.Ordinal)
            && shader.Contains("FACE_LIGHT", StringComparison.Ordinal)
            && shader.Contains("RIM_GOLD", StringComparison.Ordinal),
            "Expected a gold-veined prism crystal cluster with lit and shaded faces.");

        // A rooted body may not translate: life is spent as vein breath, rising motes and
        // a pebble hop whose envelope is zero outside a short span, so the rest of the
        // period is exactly still and the wrap is silent.
        RegressionTestHarness.Require(
            shader.Contains("VEIN_HZ", StringComparison.Ordinal)
            && shader.Contains("HOP_PERIOD", StringComparison.Ordinal)
            && shader.Contains("HOP_SPAN", StringComparison.Ordinal)
            && shader.Contains("step(hopLocal, HOP_SPAN)", StringComparison.Ordinal),
            "Expected rooted idle life as breath, motes and a bounded discrete hop.");

        // C# and the shader each hold one end of the same fact. If they drift, the slot is
        // raised by the wrong distance and the cluster floats.
        RegressionTestHarness.Require(
            shader.Contains("const float CONTACT_Y = 0.36;", StringComparison.Ordinal)
            && visuals.Contains("EarthContactSurfaceFraction = 0.36f;", StringComparison.Ordinal)
            && visuals.Contains("GroundVisualLift = 44f;", StringComparison.Ordinal)
            && !visuals.Contains("EarthVisualLift", StringComparison.Ordinal),
            "Expected the earth contact fraction to agree with the controller on the visual ground.");

        // The wall is the spirit's own material: a palisade of the same crystals, joined
        // by plain min because rigid bodies do not fuse. It rises, snaps, holds perfectly
        // still and shatters, because Block is enduring, not hitting.
        RegressionTestHarness.Require(
            shader.Contains("WALL_SHARD_COUNT", StringComparison.Ordinal)
            && shader.Contains("WALL_SNAP_START", StringComparison.Ordinal)
            && shader.Contains("WALL_HOLD_START", StringComparison.Ordinal)
            && shader.Contains("WALL_HOLD_END", StringComparison.Ordinal)
            && !shader.Contains("cel_smin(", StringComparison.Ordinal),
            "Expected a crystal palisade that rises, snaps, holds and shatters without fusing.");

        // Shared math, not a second copy of it.
        RegressionTestHarness.Require(
            shader.Contains("cel_tapered_segment(", StringComparison.Ordinal)
            && shader.Contains("cel_hash11(", StringComparison.Ordinal)
            && !shader.Contains("float smin(", StringComparison.Ordinal)
            && !shader.Contains("float hash21(", StringComparison.Ordinal),
            "Expected earth to reuse the shared cel operators rather than restating them.");

        // Visual work stays a fixed constant, never scaled by combat state or Block.
        RegressionTestHarness.Require(
            shader.Contains("SHARD_COUNT", StringComparison.Ordinal)
            && shader.Contains("MOTE_COUNT", StringComparison.Ordinal)
            && shader.Contains("DEBRIS_COUNT", StringComparison.Ordinal),
            "Expected earth visual work to remain explicitly bounded.");
    }

    [Fact]
    public void EarthGameplayKeepsVisualNotificationsNonAuthoritative()
    {
        var power = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Powers/SourceCards/ClassicEarthyPower.cs"));
        var cards = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/ClowSakura/Earthy.cs"));
        var visuals = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraElementStateVisuals.cs"));

        RegressionTestHarness.Require(
            power.IndexOf("NotifyEarthTriggered(Owner);", StringComparison.Ordinal)
                < power.IndexOf("await CreatureCmd.GainBlock(", StringComparison.Ordinal)
            && cards.Contains("NotifyIconicEarthyPlayed", StringComparison.Ordinal),
            "Expected gameplay to own the Block while notifying visuals in place.");

        RegressionTestHarness.Require(
            !visuals.Contains("CreatureCmd", StringComparison.Ordinal)
            && !visuals.Contains("GainBlock", StringComparison.Ordinal)
            && !visuals.Contains("SakuraPowerValueProps", StringComparison.Ordinal),
            "Expected earth visuals to never grant Block or restate the trigger rule.");

        RegressionTestHarness.Require(
            visuals.Contains("_earthEntryTween", StringComparison.Ordinal)
            && visuals.Contains("_earthExitTween", StringComparison.Ordinal)
            && visuals.Contains("RefreshEarth", StringComparison.Ordinal),
            "Expected earth to own separate tweens so the four elements never cancel each other.");

        // Earth is the one element with no counter to reach: every earth card played
        // under the state triggers, so consecutive plays land inside a wall that is still
        // forming. Without a field to kill, several tweens would drive one
        // trigger_progress at once and drag the wall back to its start mid-formation.
        RegressionTestHarness.Require(
            !power.Contains("_counter", StringComparison.Ordinal)
            && visuals.Contains("_earthTriggerTween", StringComparison.Ordinal)
            && visuals.Contains("KillTween", StringComparison.Ordinal),
            "Expected the counterless earth trigger to own a killable tween for re-entry.");

        // Settling the drive at 0 rather than 1 is what makes a kill safe at any point:
        // an interrupted beat can never leave the wall standing half-formed.
        var triggerBody = visuals[visuals.IndexOf("internal void PlayEarthTrigger()", StringComparison.Ordinal)..];
        triggerBody = triggerBody[..triggerBody.IndexOf("internal void PlayWaterTrigger()", StringComparison.Ordinal)];
        RegressionTestHarness.Require(
            triggerBody.Contains("_earthMaterial.SetShaderParameter(\"trigger_progress\", 0f);", StringComparison.Ordinal)
            && triggerBody.LastIndexOf("\"trigger_progress\", 0f", StringComparison.Ordinal)
                > triggerBody.IndexOf("0f, 1f, EarthTriggerDuration", StringComparison.Ordinal),
            "Expected the earth trigger to settle back at zero so an interrupted wall cannot persist.");

        // Facing is sampled once per beat. SyncFlip republishes the sign every frame, so
        // reading it live would let the wall mirror itself mid-formation.
        RegressionTestHarness.Require(
            visuals.Contains("ResolveFacingSign", StringComparison.Ordinal)
            && !visuals.Contains("_Process", StringComparison.Ordinal),
            "Expected facing to be sampled per beat rather than polled per frame.");

        // Block lands on the character, so the wall in the slot is the whole statement.
        // Fire, wind and water each fly to where their payout appears; earth has nowhere
        // to fly to, and inventing a target would be presentation asserting a causality
        // the gameplay does not have.
        RegressionTestHarness.Require(
            !visuals.Contains("SakuraEarthyTrigger", StringComparison.Ordinal),
            "Expected the earth trigger to stay in its slot without a projectile.");
    }

    [Fact]
    public void WaterGameplayKeepsVisualNotificationsNonAuthoritative()
    {
        var power = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Powers/SourceCards/ClassicWateryPower.cs"));
        var cards = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/ClowSakura/Watery.cs"));
        var visuals = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraElementStateVisuals.cs"));

        RegressionTestHarness.Require(
            power.Contains("TriggerCounter", StringComparison.Ordinal)
            && power.Contains("SavedAttachedState", StringComparison.Ordinal)
            && power.IndexOf("NotifyWaterTriggered(Owner);", StringComparison.Ordinal)
                < power.IndexOf("await PlayerCmd.GainEnergy(1, Owner.Player!);", StringComparison.Ordinal)
            && cards.Contains("NotifyIconicWateryPlayed", StringComparison.Ordinal),
            "Expected gameplay to own the synced trigger counter and the energy gain while notifying visuals in place.");

        RegressionTestHarness.Require(
            !visuals.Contains("PlayerCmd", StringComparison.Ordinal)
            && !visuals.Contains("EnergyTrigger", StringComparison.Ordinal)
            && !visuals.Contains("GainEnergy", StringComparison.Ordinal),
            "Expected water visuals to never grant energy or restate the trigger rule.");

        RegressionTestHarness.Require(
            visuals.Contains("_waterEntryTween", StringComparison.Ordinal)
            && visuals.Contains("_waterExitTween", StringComparison.Ordinal)
            && visuals.Contains("RefreshWater", StringComparison.Ordinal),
            "Expected water to own separate tweens so the three elements never cancel each other.");
    }

    [Fact]
    public void WindGameplayKeepsVisualNotificationsNonAuthoritative()
    {
        var power = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Powers/SourceCards/ClassicWindyPower.cs"));
        var cards = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/ClowSakura/Windy.cs"));
        var visuals = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraElementStateVisuals.cs"));

        RegressionTestHarness.Require(
            power.Contains("TriggerCounter", StringComparison.Ordinal)
            && power.Contains("SavedAttachedState", StringComparison.Ordinal)
            && power.IndexOf("NotifyWindTriggered(Owner);", StringComparison.Ordinal)
                < power.IndexOf("await CardPileCmd.Draw(choiceContext, 1, Owner.Player!, false);", StringComparison.Ordinal)
            && cards.Contains("NotifyIconicWindyPlayed", StringComparison.Ordinal),
            "Expected gameplay to own the synced trigger counter and draw while notifying visuals in place.");

        RegressionTestHarness.Require(
            !visuals.Contains("CardPileCmd", StringComparison.Ordinal)
            && !visuals.Contains("DrawTrigger", StringComparison.Ordinal)
            && !visuals.Contains("TriggerCounter", StringComparison.Ordinal),
            "Expected wind visuals to never draw, read the counter, or restate the trigger rule.");

        RegressionTestHarness.Require(
            visuals.Contains("_windEntryTween", StringComparison.Ordinal)
            && visuals.Contains("_windExitTween", StringComparison.Ordinal)
            && visuals.Contains("RefreshFire", StringComparison.Ordinal)
            && visuals.Contains("RefreshWind", StringComparison.Ordinal),
            "Expected wind to own separate tweens so fire and wind never cancel each other.");
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
    public void FireGameplayKeepsVisualNotificationsNonAuthoritative()
    {
        var power = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Powers/SourceCards/ClassicFireyPower.cs"));
        var cards = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/ClowSakura/Firey.cs"));
        var visuals = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraElementStateVisuals.cs"));

        // Gameplay owns enemy capture and notifies the visuals in place.
        RegressionTestHarness.Require(
            power.Contains("HittableEnemies", StringComparison.Ordinal)
            && power.Contains("targets", StringComparison.Ordinal)
            && power.Contains("NotifyFireTriggered", StringComparison.Ordinal)
            && cards.Contains("NotifyIconicFireyPlayed", StringComparison.Ordinal),
            "Expected gameplay to own enemy target capture while notifying visuals in place.");

        // The fire flight is config-gated, state-resolved, and bounded in target count.
        RegressionTestHarness.Require(
            visuals.Contains("SakuraModConfig.IsCardVfxEnabled", StringComparison.Ordinal)
            && visuals.Contains("States.TryGetValue", StringComparison.Ordinal)
            && visuals.Contains("MaxTriggerTargets", StringComparison.Ordinal),
            "Expected the fire flight to stay optional and bounded.");

        // Flight paths and spark rings come from named helpers with a named radius.
        RegressionTestHarness.Require(
            visuals.Contains("QuadraticBezier", StringComparison.Ordinal)
            && visuals.Contains("BezierPoints", StringComparison.Ordinal)
            && visuals.Contains("TrailPoints", StringComparison.Ordinal)
            && visuals.Contains("CirclePoints", StringComparison.Ordinal)
            && visuals.Contains("FireTriggerSparkRadius", StringComparison.Ordinal),
            "Expected the fire flight to build from named bezier and point helpers.");

        // Visuals never touch enemy selection or damage.
        RegressionTestHarness.Require(
            !visuals.Contains("HittableEnemies", StringComparison.Ordinal)
            && !visuals.Contains("CreatureCmd.Damage", StringComparison.Ordinal),
            "Expected fire visuals to stay non-authoritative over targets and damage.");
    }

    [Fact]
    public void ElementStateVisualsUseLifecycleCleanupWithoutPolling()
    {
        var source = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraElementStateVisuals.cs"));

        foreach (var eventName in new[] { "PowerApplied", "PowerIncreased", "PowerDecreased", "PowerRemoved" })
            RegressionTestHarness.Require(
                source.Contains($"_creature.{eventName} +=", StringComparison.Ordinal)
                && source.Contains($"_creature.{eventName} -=", StringComparison.Ordinal),
                $"Expected element visual state to unsubscribe Creature.{eventName}.");

        RegressionTestHarness.Require(
            source.Contains("CombatManager.Instance.CombatEnded += OnCombatEnded", StringComparison.Ordinal)
            && source.Contains("CombatManager.Instance.CombatEnded -= OnCombatEnded", StringComparison.Ordinal)
            && source.Contains("_root.TreeExiting += OnTreeExiting", StringComparison.Ordinal)
            && source.Contains("_root.TreeExiting -= OnTreeExiting", StringComparison.Ordinal)
            && source.Contains("_creature.Died += OnDied", StringComparison.Ordinal)
            && source.Contains("_creature.Died -= OnDied", StringComparison.Ordinal)
            && !source.Contains("_Process", StringComparison.Ordinal)
            && !source.Contains("GpuParticles", StringComparison.Ordinal),
            "Expected event-owned cleanup without frame polling or unbounded particles.");
    }
}
