using SakuraMod.SakuraModCode.Ui;

public sealed class HoverTipViewportGeometrySuite
{
    [Fact]
    public void ContentAlreadyInsideSafeViewportDoesNotMove()
    {
        var offset = Calculate(20f, 20f, 800f, 600f);

        Assert.Equal(new HoverTipViewportOffset(0f, 0f), offset);
    }

    [Theory]
    [InlineData(-40f, 20f, 400f, 600f, 48f, 0f)]
    [InlineData(200f, 20f, 1040f, 600f, -48f, 0f)]
    [InlineData(20f, -30f, 800f, 400f, 0f, 38f)]
    [InlineData(20f, 100f, 800f, 750f, 0f, -38f)]
    public void OverflowMovesToNearestSafeBoundary(
        float left,
        float top,
        float right,
        float bottom,
        float expectedX,
        float expectedY)
    {
        var offset = Calculate(left, top, right, bottom);

        Assert.Equal(expectedX, offset.X);
        Assert.Equal(expectedY, offset.Y);
    }

    [Fact]
    public void OversizedContentCentersOnEachAxis()
    {
        var offset = Calculate(-100f, -50f, 1200f, 900f);

        Assert.Equal(-50f, offset.X);
        Assert.Equal(-65f, offset.Y);
    }

    [Fact]
    public void DualAxisOverflowIsCorrectedTogether()
    {
        var offset = Calculate(-40f, -30f, 800f, 650f);

        Assert.Equal(48f, offset.X);
        Assert.Equal(38f, offset.Y);
    }

    [Fact]
    public void WrappedRelicColumnsMoveIntoA1920Viewport()
    {
        var offset = HoverTipViewportGeometry.CalculateTranslation(
            viewportLeft: 0f,
            viewportTop: 0f,
            viewportRight: 1920f,
            viewportBottom: 1080f,
            contentLeft: -620f,
            contentTop: 450f,
            contentRight: 460f,
            contentBottom: 1030f,
            margin: 8f);

        Assert.Equal(628f, offset.X);
        Assert.Equal(0f, offset.Y);
    }

    private static HoverTipViewportOffset Calculate(
        float contentLeft,
        float contentTop,
        float contentRight,
        float contentBottom) =>
        HoverTipViewportGeometry.CalculateTranslation(
            viewportLeft: 0f,
            viewportTop: 0f,
            viewportRight: 1000f,
            viewportBottom: 720f,
            contentLeft,
            contentTop,
            contentRight,
            contentBottom,
            margin: 8f);
}
