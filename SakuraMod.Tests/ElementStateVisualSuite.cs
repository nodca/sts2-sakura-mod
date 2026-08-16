using SakuraMod.SakuraModCode.Character;

public sealed class ElementStateVisualSuite
{
    [Fact]
    public void FixedElementSlotsStayStableAcrossBodySizes()
    {
        // Floor sits below the mount point. Each inset is how far down the matching
        // shader draws its own ground line inside its own rect: water's surface, and
        // earth's contact line. Water is then lifted slightly further so its mark stays
        // readable above the feet and HP bar; earth keeps its contact line grounded but
        // adds a separate slot-level lift to clear the combat status strip.
        const float floorY = 20f;
        const float surfaceInset = 18.4f;
        const float contactInset = 21.6f;
        const float earthLift = 32f;
        var standard = SakuraElementSlotLayout.FromBody(
            new Godot.Vector2(120f, 260f), floorY, surfaceInset, contactInset);
        var chibi = SakuraElementSlotLayout.FromBody(
            new Godot.Vector2(180f, 360f), floorY, surfaceInset, contactInset);

        // Fire holds the centre axis alone. The two ground marks take opposite sides so
        // neither has to yield size to the other: water left, earth right. Wind crosses
        // the body with a small leftward bias so its rise reads against the standee
        // rather than splitting it.
        RegressionTestHarness.Require(
            standard.Fire.X == 0f
            && standard.Water.X < 0f
            && standard.Earth.X > 0f
            && chibi.Fire.X == 0f
            && chibi.Water.X < 0f
            && chibi.Earth.X > 0f,
            "Expected fire on the centre axis with water and earth on opposite ground sides.");

        // Neither ground slot may flip with the standee. FacingSign is republished every
        // frame by the idle controllers, so a mirrored slot would make a persistent mark
        // jump across the character mid-combat; only one-shot beats may face, and earth's
        // wall does that inside its own shader. FromBody taking no facing argument is
        // what enforces this, and these fixed signs are what stop one being added.
        RegressionTestHarness.Require(
            standard.Earth.X == 120f * 0.42f
            && chibi.Earth.X == 180f * 0.42f
            && standard.Water.X == -120f * 0.58f
            && chibi.Water.X == -180f * 0.58f,
            "Expected the ground slots to sit at fixed signed offsets that never mirror.");

        // Earth is inboard of water: it is short and sits where the HP bar and Block
        // readout are, so it stays nearer the feet than the taller water mark.
        RegressionTestHarness.Require(
            standard.Earth.X < Math.Abs(standard.Water.X)
            && chibi.Earth.X < Math.Abs(chibi.Water.X),
            "Expected earth to stay inboard of the water slot.");

        // The wind bias must stay a bias: left of centre, but nowhere near the water
        // slot it would otherwise collide with.
        RegressionTestHarness.Require(
            standard.Wind.X < 0f
            && standard.Wind.X > standard.Water.X * 0.5f
            && chibi.Wind.X < 0f
            && chibi.Wind.X > chibi.Water.X * 0.5f,
            "Expected wind biased left of centre while staying clear of the water slot.");

        // The stacking order the slots encode must survive any body size: the wind
        // rise has to clear the ember above it and stay above the ground marks.
        RegressionTestHarness.Require(
            standard.Fire.Y < standard.Wind.Y
            && standard.Wind.Y < standard.Earth.Y
            && chibi.Fire.Y < chibi.Wind.Y
            && chibi.Wind.Y < chibi.Earth.Y,
            "Expected fire above wind above the ground on the shared axis at every body size.");

        // Both ground marks derive from the real floor rather than from body height, and
        // for the same reason: a fraction of body height cannot express "on the floor",
        // because the mount point's own height above the ground is not derivable from
        // body size. That is why three rounds of tuning such a fraction never reached the
        // ground, and earth was one of them (-0.08 of body height, roughly hip level)
        // until it became the slot that had to draw a crack in the floor. These
        // assertions are what stop a future change from going back to a fraction.
        RegressionTestHarness.Require(
            standard.Water.Y == floorY - surfaceInset - 30f
            && chibi.Water.Y == floorY - surfaceInset - 30f
            && standard.Earth.Y == floorY - contactInset - earthLift
            && chibi.Earth.Y == floorY - contactInset - earthLift,
            "Expected the water mark to use the floor while the earth mark uses its deliberate visual lift.");

        // Earth's deliberate visual lift now places its stones above the water mark,
        // keeping the raised stones clear of the pool and the HP/Block strip.
        RegressionTestHarness.Require(
            standard.Earth.Y < standard.Water.Y
            && chibi.Earth.Y < chibi.Water.Y,
            "Expected the lifted earth mark to stay above the water mark.");

        // A degenerate hitbox can report a floor level with or above the mount point.
        // The floor is clamped once and both ground slots derive from that shared safe
        // value, so a bad floor degrades them together instead of dragging one to
        // wherever the other happens to sit.
        var degenerate = SakuraElementSlotLayout.FromBody(
            new Godot.Vector2(120f, 260f), -400f, surfaceInset, contactInset);
        RegressionTestHarness.Require(
            degenerate.Water.Y == -surfaceInset - 30f
            && degenerate.Earth.Y == -contactInset - earthLift
            && degenerate.Earth.Y < degenerate.Water.Y,
            "Expected a floor above the mount point to clamp once and keep both ground marks ordered.");
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
            && scene.Contains("mouse_filter = 2", StringComparison.Ordinal)
            && shader.Contains("uniform float state_alpha", StringComparison.Ordinal)
            && shader.Contains("uniform float summon_progress", StringComparison.Ordinal)
            && shader.Contains("uniform float trigger_progress", StringComparison.Ordinal)
            && shader.Contains("float coreBody", StringComparison.Ordinal)
            && shader.Contains("float tongue1", StringComparison.Ordinal)
            && shader.Contains("float tongue5", StringComparison.Ordinal)
            && shader.Contains("float trailLeft", StringComparison.Ordinal)
            && shader.Contains("float trailRight", StringComparison.Ordinal)
            && shader.Contains("float ellipse_sway", StringComparison.Ordinal)
            && shader.Contains("TIME * 0.9", StringComparison.Ordinal)
            && shader.Contains("vec3(0.96, 0.15, 0.03)", StringComparison.Ordinal),
            "Expected the hybrid fire shader to expose a compact ember with asymmetric dancing tongues and flickering trails.");
    }

    [Fact]
    public void HybridWindResourcesExposeContinuousPseudoDepthCirculation()
    {
        var scene = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/scenes/combat/sakura_element_state_visuals.tscn"));
        var shader = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/shaders/card_vfx/sakura_element_state_windy.gdshader"));

        RegressionTestHarness.Require(
            scene.Contains("[node name=\"WindyCurrents\" type=\"ColorRect\" parent=\"WindSlot\"]", StringComparison.Ordinal)
            && scene.Contains("sakura_element_state_windy.gdshader", StringComparison.Ordinal),
            "Expected the wind mark to live in the reserved WindSlot with its own shader.");

        RegressionTestHarness.Require(
            shader.Contains("uniform float state_alpha", StringComparison.Ordinal)
            && shader.Contains("uniform float summon_progress", StringComparison.Ordinal)
            && shader.Contains("uniform float trigger_progress", StringComparison.Ordinal)
            && shader.Contains("RISE_HZ", StringComparison.Ordinal)
            && shader.Contains("float t = TIME + seed;", StringComparison.Ordinal),
            "Expected the wind mark to rise on a continuous clock with its own state controls.");

        // The flow must rise and dissipate, never travel a closed loop. A closed
        // orbit reads as "a few objects going around a centre" whatever shape rides
        // it, which is the failure the earlier revisions shared.
        RegressionTestHarness.Require(
            shader.Contains("SPIRAL_TURNS", StringComparison.Ordinal)
            && shader.Contains("float rise = fract(t * RISE_HZ", StringComparison.Ordinal)
            && shader.Contains("float y = mix(halfHeight * 0.9, -halfHeight * 0.94, rise);", StringComparison.Ordinal)
            && shader.Contains("float env = smoothstep(0.0, 0.14, rise)", StringComparison.Ordinal),
            "Expected an upward spiral that fades in low and dissipates before the top edge.");

        // Petals, not abstract specks: air is inferred from what it carries. The
        // notch is the detail that names the silhouette.
        RegressionTestHarness.Require(
            shader.Contains("float petal(", StringComparison.Ordinal)
            && shader.Contains("float notch", StringComparison.Ordinal)
            && shader.Contains("float tumble", StringComparison.Ordinal),
            "Expected a tumbling petal silhouette carried by the flow.");

        // No true occlusion is reachable from this mount point, so the turn is sold
        // by foreshortening: petals compress edge-on and dim on the spiral's far half.
        RegressionTestHarness.Require(
            shader.Contains("float facing = cos(angle);", StringComparison.Ordinal)
            && shader.Contains("float flatten = mix(0.24, 1.0, abs(facing));", StringComparison.Ordinal)
            && shader.Contains("float depthFade = mix(0.32, 1.0, near);", StringComparison.Ordinal),
            "Expected foreshortening and dimming to stand in for unavailable occlusion.");

        // The petals stay cyan-white; pink and gold remain Sakura's own signature.
        RegressionTestHarness.Require(
            shader.Contains("vec3(0.58, 0.86, 0.96)", StringComparison.Ordinal)
            && shader.Contains("vec3(0.86, 0.99, 1.0)", StringComparison.Ordinal),
            "Expected the wind petals to stay in the pale cyan range.");

        // Visual work stays a fixed constant, never scaled by combat state.
        RegressionTestHarness.Require(
            shader.Contains("const int PETAL_COUNT = 5;", StringComparison.Ordinal),
            "Expected wind visual work to remain explicitly bounded.");
    }

    [Fact]
    public void HybridWaterResourcesExposeLiquidCoalescenceAndPool()
    {
        var scene = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/scenes/combat/sakura_element_state_visuals.tscn"));
        var shader = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/shaders/card_vfx/sakura_element_state_watery.gdshader"));

        RegressionTestHarness.Require(
            scene.Contains("[node name=\"WateryDroplets\" type=\"ColorRect\" parent=\"WaterSlot\"]", StringComparison.Ordinal)
            && scene.Contains("sakura_element_state_watery.gdshader", StringComparison.Ordinal),
            "Expected the water mark to live in the reserved WaterSlot with its own shader.");

        RegressionTestHarness.Require(
            shader.Contains("uniform float state_alpha", StringComparison.Ordinal)
            && shader.Contains("uniform float summon_progress", StringComparison.Ordinal)
            && shader.Contains("uniform float trigger_progress", StringComparison.Ordinal)
            && shader.Contains("float t = TIME + seed;", StringComparison.Ordinal),
            "Expected the water mark to run on a continuous clock with its own state controls.");

        // The liquid bridge is the load-bearing detail. A rounded body with a tip and
        // a short tail describes a comet just as well; drawing together into a neck is
        // the one thing only a liquid does, so the union must be a smooth minimum
        // rather than a max that would leave two separate blobs.
        RegressionTestHarness.Require(
            shader.Contains("float smin(", StringComparison.Ordinal)
            && shader.Contains("float bodies = smin(dropA, dropB", StringComparison.Ordinal),
            "Expected a polynomial smooth-minimum union so approaching droplets grow a neck.");

        // Water is the one element with a horizontal plane: fire rises, wind orbits,
        // earth is fragments. The pool is what separates this from two blue spheres.
        RegressionTestHarness.Require(
            shader.Contains("float pool", StringComparison.Ordinal)
            && shader.Contains("POOL_Y", StringComparison.Ordinal),
            "Expected a narrow pool line giving water its horizontal plane.");

        // Chase path is a closed continuous curve, unlike wind: a liquid pair circling
        // each other is correct, and Gerono's figure-eight has no seam to jump at.
        RegressionTestHarness.Require(
            shader.Contains("vec2 gerono(", StringComparison.Ordinal)
            && shader.Contains("CHASE_HZ", StringComparison.Ordinal),
            "Expected a continuous figure-eight chase without a period seam.");

        // Overshoot on merge is surface tension made visible, and is what separates
        // the trigger's full merge from the idle bridge that now happens on its own.
        RegressionTestHarness.Require(
            shader.Contains("float rebound", StringComparison.Ordinal),
            "Expected the merge to overshoot before settling.");
    }

    [Fact]
    public void HybridEarthResourcesExposeStillStonesAndASnappedWall()
    {
        var scene = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/scenes/combat/sakura_element_state_visuals.tscn"));
        var shader = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/shaders/card_vfx/sakura_element_state_earthy.gdshader"));
        var visuals = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Character/SakuraElementStateVisuals.cs"));

        RegressionTestHarness.Require(
            scene.Contains("[node name=\"EarthyFragments\" type=\"ColorRect\" parent=\"EarthSlot\"]", StringComparison.Ordinal)
            && scene.Contains("sakura_element_state_earthy.gdshader", StringComparison.Ordinal),
            "Expected the earth mark to live in the reserved EarthSlot with its own shader.");

        RegressionTestHarness.Require(
            shader.Contains("uniform float state_alpha", StringComparison.Ordinal)
            && shader.Contains("uniform float summon_progress", StringComparison.Ordinal)
            && shader.Contains("uniform float trigger_progress", StringComparison.Ordinal)
            && shader.Contains("uniform float facing", StringComparison.Ordinal),
            "Expected the earth mark to carry its own state controls plus a facing sign.");

        // The load-bearing decision: stone forbids idle motion. Fire flickers, wind
        // rises and water falls because each is motion its material permits; a stone
        // doing any of them reads as a floating brown polygon. The envelope must be zero
        // outside a short span, which is what makes the rest of the period exactly still
        // and the period wrap silent. A bare sin() on the resting position is precisely
        // what this must never become.
        RegressionTestHarness.Require(
            shader.Contains("const float SETTLE_PERIOD", StringComparison.Ordinal)
            && shader.Contains("const float SETTLE_SPAN", StringComparison.Ordinal)
            && shader.Contains("float settleEnv = smoothstep(0.0, 0.05, settleLocal)", StringComparison.Ordinal)
            && shader.Contains("* (1.0 - smoothstep(0.05, SETTLE_SPAN, settleLocal))", StringComparison.Ordinal)
            && shader.Contains("float settleWhich = mod(floor(cycle)", StringComparison.Ordinal),
            "Expected motion spent as a bounded discrete settle rather than continuous drift.");

        // The ground has to be real, or every cue measured from it floats: the crack, the
        // contact shadows, and stones resting on something. Anything below the contact
        // line is underground and clipped, which is what makes the summon read as a heave
        // rather than a fade-in.
        RegressionTestHarness.Require(
            shader.Contains("const float CONTACT_Y", StringComparison.Ordinal)
            && shader.Contains("float aboveGround = 1.0 - smoothstep(0.0, 1.5, px.y - contactY);", StringComparison.Ordinal)
            && shader.Contains("shadows = min(shadows", StringComparison.Ordinal)
            && shader.Contains("crackCoverage", StringComparison.Ordinal),
            "Expected a real contact line with a ground clip, contact shadows and a crack.");

        // C# and the shader each hold one end of the same fact. If they drift, the slot is
        // raised by the wrong distance and the stones float again.
        RegressionTestHarness.Require(
            shader.Contains("const float CONTACT_Y = 0.30;", StringComparison.Ordinal)
            && visuals.Contains("EarthContactSurfaceFraction = 0.30f;", StringComparison.Ordinal)
            && visuals.Contains("EarthVisualLift = 32f;", StringComparison.Ordinal),
            "Expected the earth contact fraction and deliberate visual lift to agree with the controller.");

        // The wall is a morph to its own trapezoid, not a smooth union of the stones:
        // cel_smin on rock would round it into the fluid look and read as mud. Rigid
        // bodies do not fuse, so the join softness stays small and the wall arrives by
        // mix() instead.
        RegressionTestHarness.Require(
            shader.Contains("cel_tapered_segment(", StringComparison.Ordinal)
            && shader.Contains("float body = mix(stones, wall, snap);", StringComparison.Ordinal),
            "Expected the wall to be a morph to a tapered trapezoid rather than a fluid union.");

        // Block is enduring, not hitting, so the wall snaps and then holds perfectly
        // still. A freeze shorter than the detail clock is not a freeze at all.
        RegressionTestHarness.Require(
            shader.Contains("const float WALL_SNAP_START", StringComparison.Ordinal)
            && shader.Contains("const float WALL_HOLD_START", StringComparison.Ordinal)
            && shader.Contains("const float WALL_HOLD_END", StringComparison.Ordinal),
            "Expected the wall to gather, snap, hold and shatter as explicit beats.");

        const float earthTriggerDuration = 0.38f;
        const float holdSpan = (0.82f - 0.56f) * earthTriggerDuration;
        RegressionTestHarness.Require(
            visuals.Contains("EarthTriggerDuration = 0.38f;", StringComparison.Ordinal)
            && shader.Contains("const float WALL_HOLD_START = 0.56;", StringComparison.Ordinal)
            && shader.Contains("const float WALL_HOLD_END = 0.82;", StringComparison.Ordinal)
            && holdSpan > 1f / 12f,
            "Expected the wall's dead hold to outlast one CEL_STEP_HZ tick.");

        // Ochre is darker than the blue that already needed a bright rim to survive this
        // game's dark stages, so the deepest band must not also own the outline. Pale gold
        // top faces and a lit contact edge carry the silhouette instead.
        RegressionTestHarness.Require(
            shader.Contains("const vec3 TOP_GOLD", StringComparison.Ordinal)
            && shader.Contains("const vec3 INK_OCHRE", StringComparison.Ordinal)
            && shader.Contains("float topRim", StringComparison.Ordinal)
            && shader.Contains("float contactEdge", StringComparison.Ordinal)
            && shader.Contains("cel_bands3(TOP_GOLD, BODY_OCHRE, DEEP_OCHRE", StringComparison.Ordinal),
            "Expected bright top faces and a lit contact edge to carry the silhouette.");

        // Shared cel math, not a second copy of it. Water carries its own smin, which is
        // exactly the duplication the shared include exists to prevent.
        RegressionTestHarness.Require(
            shader.Contains("#include \"res://SakuraMod/shaders/card_vfx/cel_vfx.gdshaderinc\"", StringComparison.Ordinal)
            && shader.Contains("cel_facet(", StringComparison.Ordinal)
            && shader.Contains("cel_ellipse(", StringComparison.Ordinal)
            && shader.Contains("cel_hash11(", StringComparison.Ordinal)
            && !shader.Contains("float smin(", StringComparison.Ordinal)
            && !shader.Contains("float hash21(", StringComparison.Ordinal),
            "Expected earth to reuse the shared cel operators rather than restating them.");

        // Visual work stays a fixed constant, never scaled by combat state or Block.
        RegressionTestHarness.Require(
            shader.Contains("const int STONE_COUNT = 3;", StringComparison.Ordinal)
            && shader.Contains("const int DEBRIS_COUNT = 6;", StringComparison.Ordinal),
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
            && cards.Contains("NotifyIconicEarthyPlayed(Owner.Creature);", StringComparison.Ordinal),
            "Expected gameplay to own the Block while notifying visuals in place.");

        RegressionTestHarness.Require(
            !visuals.Contains("CreatureCmd", StringComparison.Ordinal)
            && !visuals.Contains("GainBlock", StringComparison.Ordinal)
            && !visuals.Contains("SakuraPowerValueProps", StringComparison.Ordinal),
            "Expected earth visuals to never grant Block or restate the trigger rule.");

        RegressionTestHarness.Require(
            visuals.Contains("_earthEntryTween", StringComparison.Ordinal)
            && visuals.Contains("_earthExitTween", StringComparison.Ordinal)
            && visuals.Contains("private void RefreshEarth(", StringComparison.Ordinal),
            "Expected earth to own separate tweens so the four elements never cancel each other.");

        // Earth is the one element with no counter to reach: every earth card played
        // under the state triggers, so consecutive plays land inside a wall that is still
        // forming. Without a field to kill, several tweens would drive one
        // trigger_progress at once and drag the wall back to its start mid-formation.
        RegressionTestHarness.Require(
            !power.Contains("_counter", StringComparison.Ordinal)
            && visuals.Contains("private Tween? _earthTriggerTween;", StringComparison.Ordinal)
            && visuals.Contains("KillTween(ref _earthTriggerTween);", StringComparison.Ordinal),
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
            visuals.Contains("_earthMaterial.SetShaderParameter(\"facing\", ResolveFacingSign());", StringComparison.Ordinal)
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
            power.Contains("_counter -= EnergyTrigger;", StringComparison.Ordinal)
            && power.IndexOf("NotifyWaterTriggered(Owner);", StringComparison.Ordinal)
                < power.IndexOf("await PlayerCmd.GainEnergy(1, Owner.Player!);", StringComparison.Ordinal)
            && cards.Contains("NotifyIconicWateryPlayed(Owner.Creature);", StringComparison.Ordinal),
            "Expected gameplay to own the counter and the energy gain while notifying visuals in place.");

        RegressionTestHarness.Require(
            !visuals.Contains("PlayerCmd", StringComparison.Ordinal)
            && !visuals.Contains("EnergyTrigger", StringComparison.Ordinal)
            && !visuals.Contains("GainEnergy", StringComparison.Ordinal),
            "Expected water visuals to never grant energy or restate the trigger rule.");

        RegressionTestHarness.Require(
            visuals.Contains("_waterEntryTween", StringComparison.Ordinal)
            && visuals.Contains("_waterExitTween", StringComparison.Ordinal)
            && visuals.Contains("private void RefreshWater(", StringComparison.Ordinal),
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
            power.Contains("_counter -= DrawTrigger;", StringComparison.Ordinal)
            && power.IndexOf("NotifyWindTriggered(Owner);", StringComparison.Ordinal)
                < power.IndexOf("await CardPileCmd.Draw(choiceContext, 1, Owner.Player!, false);", StringComparison.Ordinal)
            && cards.Contains("NotifyIconicWindyPlayed(Owner.Creature);", StringComparison.Ordinal),
            "Expected gameplay to own the counter and draw while notifying visuals in place.");

        RegressionTestHarness.Require(
            !visuals.Contains("CardPileCmd", StringComparison.Ordinal)
            && !visuals.Contains("DrawTrigger", StringComparison.Ordinal)
            && !visuals.Contains("_counter", StringComparison.Ordinal),
            "Expected wind visuals to never draw, read the counter, or restate the trigger rule.");

        RegressionTestHarness.Require(
            visuals.Contains("_windEntryTween", StringComparison.Ordinal)
            && visuals.Contains("_windExitTween", StringComparison.Ordinal)
            && visuals.Contains("private void RefreshFire(", StringComparison.Ordinal)
            && visuals.Contains("private void RefreshWind(", StringComparison.Ordinal),
            "Expected wind to own separate tweens so fire and wind never cancel each other.");
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

        RegressionTestHarness.Require(
            power.Contains("var targets = combatState.HittableEnemies.ToList();", StringComparison.Ordinal)
            && power.Contains("NotifyFireTriggered(Owner, targets);", StringComparison.Ordinal)
            && power.Contains("foreach (var enemy in targets)", StringComparison.Ordinal)
            && cards.Contains("NotifyIconicFireyPlayed(Owner.Creature);", StringComparison.Ordinal)
            && visuals.Contains("if (SakuraModConfig.IsCardVfxEnabled() && States.TryGetValue(owner, out var state))", StringComparison.Ordinal)
            && visuals.Contains("if (targets.Count == 0 || targets.Count > MaxTriggerTargets)", StringComparison.Ordinal)
            && visuals.Contains("QuadraticBezier", StringComparison.Ordinal)
            && visuals.Contains("BezierPoints(control, delta, 14)", StringComparison.Ordinal)
            && visuals.Contains("TrailPoints(path, progress)", StringComparison.Ordinal)
            && visuals.Contains("private const float FireTriggerSparkRadius = 9f;", StringComparison.Ordinal)
            && visuals.Contains("CirclePoints(FireTriggerSparkRadius, 8)", StringComparison.Ordinal)
            && !visuals.Contains("HittableEnemies", StringComparison.Ordinal)
            && !visuals.Contains("CreatureCmd.Damage", StringComparison.Ordinal),
            "Expected gameplay to own target capture and damage while visuals remain bounded, optional, and non-authoritative.");
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
