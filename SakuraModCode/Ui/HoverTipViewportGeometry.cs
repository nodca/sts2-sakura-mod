namespace SakuraMod.SakuraModCode.Ui;

internal readonly record struct HoverTipViewportOffset(float X, float Y)
{
    public bool IsZero => MathF.Abs(X) < 0.01f && MathF.Abs(Y) < 0.01f;
}

internal static class HoverTipViewportGeometry
{
    public static HoverTipViewportOffset CalculateTranslation(
        float viewportLeft,
        float viewportTop,
        float viewportRight,
        float viewportBottom,
        float contentLeft,
        float contentTop,
        float contentRight,
        float contentBottom,
        float margin)
    {
        return new HoverTipViewportOffset(
            CalculateAxisTranslation(
                viewportLeft,
                viewportRight,
                contentLeft,
                contentRight,
                margin),
            CalculateAxisTranslation(
                viewportTop,
                viewportBottom,
                contentTop,
                contentBottom,
                margin));
    }

    private static float CalculateAxisTranslation(
        float viewportMin,
        float viewportMax,
        float contentMin,
        float contentMax,
        float margin)
    {
        var safeMin = viewportMin + margin;
        var safeMax = viewportMax - margin;
        var safeSize = safeMax - safeMin;
        var contentSize = contentMax - contentMin;

        if (safeSize <= 0f || contentSize <= 0f)
            return 0f;

        if (contentSize > safeSize)
            return safeMin + (safeSize - contentSize) * 0.5f - contentMin;

        if (contentMin < safeMin)
            return safeMin - contentMin;

        return contentMax > safeMax
            ? safeMax - contentMax
            : 0f;
    }
}
