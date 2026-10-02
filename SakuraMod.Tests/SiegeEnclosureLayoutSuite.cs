using Godot;
using SakuraMod.SakuraModCode.Cards;

public sealed class SiegeEnclosureLayoutSuite
{
    [Theory]
    [InlineData(1300, 500, 140, 260)] // One enemy.
    [InlineData(1040, 430, 780, 350)] // A wide group with mixed heights.
    [InlineData(1690, 730, 180, 260)] // Near the right and bottom screen edges.
    [InlineData(1300, 300, 240, 600)] // A tall boss.
    public void WholeGroupFitsInsideTheCubeAndViewport(float x, float y, float width, float height)
    {
        var group = new Rect2(x, y, width, height);
        var viewport = new Rect2(0, 0, 1920, 1080);
        var layout = SiegeEnclosureVfx.Layout.ForGroup(group, viewport);

        // The inner rectangle is shared by both projected walls. Covering it
        // ensures even the group's outermost head and feet stay inside the cube.
        var inner = new Rect2(
            layout.Center + new Vector2(-0.5f, -0.27f) * layout.Width,
            new Vector2(1f, 0.62f) * layout.Width);
        Assert.True(inner.Encloses(group));

        // Include the diamond's far and near corners, not just the enemy bounds.
        var silhouette = new Rect2(
            layout.Center - new Vector2(0.5f, 0.43f) * layout.Width,
            new Vector2(1f, 0.86f) * layout.Width);
        Assert.True(viewport.Encloses(silhouette));
    }

    [Fact]
    public void DelayedEnclosureIsPreloadedWithSiege()
    {
        Assert.Contains(SiegeEnclosureVfx.ScenePath, SakuraCardVfxAssets.RunAssetPaths(new Siege()));
    }
}
