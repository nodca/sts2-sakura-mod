using Godot;
using SakuraMod.SakuraModCode.Cards;

namespace SakuraMod.Tests;

public sealed class CreatureTintSuite
{
    private static readonly Vector3 Freeze = new(0.80f, 1.02f, 1.30f);
    private static readonly Vector3 Amber = new(1.08f, 0.94f, 0.72f);
    private static readonly Color Dimmed = new(0.5f, 0.5f, 0.5f, 0.8f);

    [Fact]
    public void FirstWriteKeepsTheExistingRgbAsBaseAndNeverTouchesAlpha()
    {
        var tint = new CreatureTint.Composition();
        var written = tint.Set("freeze", Freeze, Dimmed);
        AssertRgb(new Color(0.40f, 0.51f, 0.65f), written);
        Assert.Equal(0.8f, written.A);

        var restored = tint.Clear("freeze", written with { A = 0.3f });
        Assert.NotNull(restored);
        AssertRgb(Dimmed, restored.Value);
        Assert.Equal(0.3f, restored.Value.A);
        Assert.True(tint.IsEmpty);
    }

    [Fact]
    public void TwoSourcesComposeAsAStableProductInsteadOfCompounding()
    {
        var tint = new CreatureTint.Composition();
        var current = Colors.White;
        // Both sources refresh every frame, as the visuals do.
        for (var frame = 0; frame < 30; frame++)
        {
            current = tint.Set("freeze", Freeze, current);
            current = tint.Set("time", Amber, current);
        }
        AssertRgb(new Color(Freeze.X * Amber.X, Freeze.Y * Amber.Y, Freeze.Z * Amber.Z), current);

        current = tint.Clear("freeze", current)!.Value;
        AssertRgb(new Color(Amber.X, Amber.Y, Amber.Z), current);
        current = tint.Clear("time", current)!.Value;
        AssertRgb(Colors.White, current);
    }

    [Fact]
    public void AnotherWritersColourBecomesTheNewBase()
    {
        var tint = new CreatureTint.Composition();
        var current = tint.Set("time", Amber, Colors.White);
        // Native background dimming rewrites the colour while the tint is held.
        var foreign = new Color(0.6f, 0.6f, 0.6f, 1f);
        current = tint.Set("time", Amber, foreign);
        AssertRgb(new Color(0.6f * Amber.X, 0.6f * Amber.Y, 0.6f * Amber.Z), current);
        AssertRgb(foreign, tint.Clear("time", current)!.Value);
    }

    [Fact]
    public void ClearingLeavesAForeignColourAlone()
    {
        var tint = new CreatureTint.Composition();
        tint.Set("freeze", Freeze, Colors.White);
        Assert.Null(tint.Clear("freeze", new Color(0.2f, 0.3f, 0.4f)));
        Assert.True(tint.IsEmpty);
        Assert.Null(tint.Clear("freeze", Colors.White));
    }

    private static void AssertRgb(Color expected, Color actual) =>
        RegressionTestHarness.Require(
            Math.Abs(expected.R - actual.R) < 0.0001f
            && Math.Abs(expected.G - actual.G) < 0.0001f
            && Math.Abs(expected.B - actual.B) < 0.0001f,
            $"Expected RGB {expected}, got {actual}.");
}
