using SakuraMod.SakuraModCode;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;

public sealed class GravitationWellVisualSuite
{
    private const string ShaderPath = "SakuraMod/shaders/card_vfx/gravitation_well.gdshader";

    [Fact]
    public void GravitationRouteDeclaresTheWellShaderBesideTheMagicCircle()
    {
        var declared = SakuraCardVfxAssets.RunAssetPaths(new Gravitation()).ToArray();
        RegressionTestHarness.Require(
            GravitationHoldVisual.AssetPaths.All(declared.Contains)
            && declared.Except(GravitationHoldVisual.AssetPaths).All(SakuraMagicCirclePresenter.AssetPaths.Contains)
            && declared.Length == declared.Distinct(StringComparer.Ordinal).Count(),
            "Expected Gravitation to declare the well shader plus only the shared magic circle.");
    }

    [Fact]
    public void WellShaderUsesTheSharedIncludeAndOwnsNoClock()
    {
        var shader = File.ReadAllText(RegressionTestHarness.FindRepoFile(ShaderPath));
        RegressionTestHarness.Require(
            shader.Contains("#include \"res://SakuraMod/shaders/card_vfx/cel_vfx.gdshaderinc\"", StringComparison.Ordinal)
            && !shader.Contains("TIME", StringComparison.Ordinal)
            && !shader.Contains("SCREEN_UV", StringComparison.Ordinal),
            "Expected the well to use the shared cel include, take time only from the session, and leave the scene untouched.");
        // The well's own operator is the solved ring family; it must not borrow
        // water's metaball union or fire's domain warp.
        RegressionTestHarness.Require(
            !shader.Contains("cel_smin(", StringComparison.Ordinal)
            && !shader.Contains("cel_fbm(", StringComparison.Ordinal),
            "Expected the Gravitation well not to borrow another card's field operator.");
    }

    [Fact]
    public void WellOpensOnTheVisibleGroundWithRoomForItsHalo()
    {
        foreach (var body in new[] { new Godot.Vector2(120f, 260f), new Godot.Vector2(240f, 360f), new Godot.Vector2(330f, 420f) })
        {
            var floor = new Godot.Vector2(400f, 700f);
            var anchor = new CelVfxGeometry.CasterAnchor(floor - new Godot.Vector2(0f, body.Y * 0.5f), floor, body, 1f, false);
            var layout = GravitationHoldVisual.Layout.From(anchor);

            RegressionTestHarness.Require(
                Godot.Mathf.IsEqualApprox(layout.Center.Y, floor.Y - SakuraElementSlotLayout.GroundVisualLift),
                "Expected the well centred on the standee's visible ground, not the HP strip.");
            RegressionTestHarness.Require(
                layout.RimRadius >= GravitationHoldVisual.MinRimRadius
                && layout.RimRadius <= GravitationHoldVisual.MaxRimRadius,
                "Expected the rim radius clamped for every outfit width.");
            RegressionTestHarness.Require(
                layout.RegionSize.X > layout.RimRadius * 2f
                && layout.RegionSize.Y > layout.RimRadius * GravitationHoldVisual.WellAspect * 2f,
                "Expected padding outside the rim for its halo and floor-drag band.");
        }
    }

    [Fact]
    public void GravitationPowerKeepsTheWellNonAuthoritative()
    {
        var power = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Powers/Transparent/GravitationHoldPower.cs"));
        RegressionTestHarness.Require(
            power.Contains("GravitationHoldVisual.Mount(Owner);", StringComparison.Ordinal)
            && power.Contains("GravitationHoldVisual.NotifyReturned(Owner, card);", StringComparison.Ordinal)
            && power.Contains("GravitationHoldVisual.NotifyRemoved(oldOwner);", StringComparison.Ordinal)
            && !power.Contains("await GravitationHoldVisual", StringComparison.Ordinal),
            "Expected the Hold Power to notify the well synchronously and never wait on it.");
    }
}
