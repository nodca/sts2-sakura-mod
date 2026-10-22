using Godot;
using SakuraMod.SakuraModCode.Cards;

namespace SakuraMod.Tests;

public sealed class CelRibbonSuite
{
    private const float CoilRadius = 34f;
    private const string ShaderPath = "SakuraMod/shaders/card_vfx/cel_ribbon.gdshader";

    [Fact]
    public void RibbonShaderUsesTheSharedIncludeAndOwnsNoClock()
    {
        var shader = File.ReadAllText(RegressionTestHarness.FindRepoFile(ShaderPath));
        RegressionTestHarness.Require(
            shader.Contains("#include \"res://SakuraMod/shaders/card_vfx/cel_vfx.gdshaderinc\"", StringComparison.Ordinal)
            && !shader.Contains("TIME", StringComparison.Ordinal)
            && !shader.Contains("SCREEN_", StringComparison.Ordinal),
            "Expected the ribbon to use the shared cel include and take time only from its owner.");
        RegressionTestHarness.Require(
            File.Exists(RegressionTestHarness.FindRepoFile(ShaderPath + ".uid")),
            "Expected the ribbon shader's .uid in the repository.");
    }

    [Fact]
    public void StripInitialisesEveryShaderUniform()
    {
        // Tweening a never-set uniform starts from Nil and crashes.
        var strip = File.ReadAllText(RegressionTestHarness.FindRepoFile("SakuraModCode/Cards/Visuals/CelRibbonStrip.cs"));
        var shader = File.ReadAllText(RegressionTestHarness.FindRepoFile(ShaderPath));
        foreach (var uniform in System.Text.RegularExpressions.Regex.Matches(shader, @"^uniform \w+ (\w+)", System.Text.RegularExpressions.RegexOptions.Multiline).Select(match => match.Groups[1].Value))
        {
            RegressionTestHarness.Require(
                strip.Contains($"SetShaderParameter(\"{uniform}\"", StringComparison.Ordinal),
                $"Expected CelRibbonStrip to initialise the {uniform} uniform.");
        }
    }

    [Fact]
    public void FigureEightIsWrittenLikeAnEightWithTheFrontStrandOnTop()
    {
        var centre = new Vector2(300f, 200f);
        var path = CelRibbonPaths.FigureEight(centre, 160f, 72f);
        var top = centre + new Vector2(0f, -80f);
        var bottom = centre + new Vector2(0f, 80f);

        RegressionTestHarness.Require(
            path.Closed
            && path.Points[0].DistanceTo(top) < 0.5f
            && path.Points[^1].DistanceTo(top) < 0.5f
            && path.PointAt(0.5f).DistanceTo(bottom) < 1f,
            "Expected the eight to start and end at its top and turn at its bottom.");
        // The first leg runs down the upper lobe's left side, as a hand writes an 8.
        Assert.True(path.PointAt(0.1f).X < centre.X && path.PointAt(0.1f).Y < centre.Y);
        Assert.True(path.PointAt(0.25f).DistanceTo(centre) < 1f && path.PointAt(0.75f).DistanceTo(centre) < 1f);

        // Pinched at the top and bottom; back face first, front face second,
        // so the front strand is drawn last and crosses on top.
        Assert.Equal(0f, path.FacingAt(0f), 3);
        Assert.Equal(0f, path.FacingAt(0.5f), 3);
        Assert.Equal(0f, path.FacingAt(1f), 3);
        Assert.Equal(-1f, path.FacingAt(0.25f), 3);
        Assert.Equal(1f, path.FacingAt(0.75f), 3);
    }

    [Fact]
    public void PileExchangeTurnsOverOnceBetweenTwoCoils()
    {
        var draw = new Vector2(80f, 980f);
        var discard = new Vector2(1840f, 980f);
        var ribbon = CelRibbonPaths.PileExchange(draw, discard, CoilRadius, Vector2.Up);
        var path = ribbon.Path;

        Assert.Single(path.Twists);
        Assert.InRange(path.Twists[0], 0.3f, 0.7f);
        Assert.Equal(1f, path.FacingAt(0f), 3);
        Assert.Equal(-1f, path.FacingAt(1f), 3);
        Assert.Equal(CoilRadius, path.Points[0].DistanceTo(draw), 1);
        Assert.Equal(CoilRadius, path.Points[^1].DistanceTo(discard), 1);
        Assert.True(path.PointAt(path.Twists[0]).Y < draw.Y - 40f, "Expected the arch to rise above the piles.");
        // Cards step off the first coil before the twist and onto the second after it.
        Assert.True(ribbon.Departure < path.Twists[0] && path.Twists[0] < ribbon.Arrival);
        Assert.InRange(path.PointAt(ribbon.Departure).DistanceTo(draw), CoilRadius - 1f, CoilRadius + 0.5f);
        Assert.InRange(path.PointAt(ribbon.Arrival).DistanceTo(discard), CoilRadius - 1f, CoilRadius + 0.5f);
    }

    [Fact]
    public void StackedPilesArchTowardTheScreenNotAlongTheirLine()
    {
        var exhaust = new Vector2(1840f, 860f);
        var discard = new Vector2(1840f, 980f);
        var path = CelRibbonPaths.PileExchange(exhaust, discard, CoilRadius, Vector2.Left).Path;

        Assert.True(path.PointAt(path.Twists[0]).X < exhaust.X - 40f, "Expected the arch to bulge toward the screen centre.");
    }

    [Fact]
    public void SamplesAreEvenlySpacedAlongTheRibbon()
    {
        var path = CelRibbonPaths.FigureEight(Vector2.Zero, 200f, 90f);
        var steps = path.Points.Zip(path.Points.Skip(1), (a, b) => a.DistanceTo(b)).ToArray();
        var mean = steps.Average();

        Assert.All(steps, step => Assert.InRange(step, mean * 0.95f, mean * 1.05f));
    }

    [Fact]
    public void StripPinchesAtATwistWithoutCollapsing()
    {
        var path = CelRibbonPaths.FigureEight(Vector2.Zero, 200f, 90f, segments: 100);
        var strip = CelRibbonPaths.BuildStrip(path, 7f);

        Assert.Equal(path.Points.Length * 2, strip.Vertices.Length);
        Assert.Equal(strip.Vertices.Length, strip.Uvs.Length);
        Assert.Equal(strip.Vertices.Length, strip.Colors.Length);

        float WidthAt(int sample) => strip.Vertices[sample * 2].DistanceTo(strip.Vertices[sample * 2 + 1]);
        Assert.Equal(14f, WidthAt(75), 2);
        Assert.InRange(WidthAt(50), 14f * CelRibbonPaths.MinTwistWidth - 0.01f, 14f * 0.5f);
        // The facing travels to the shader in the vertex colour.
        Assert.Equal(0f, strip.Colors[50].R, 3);
        Assert.Equal(1f, strip.Colors[150].R, 3);
    }
}
