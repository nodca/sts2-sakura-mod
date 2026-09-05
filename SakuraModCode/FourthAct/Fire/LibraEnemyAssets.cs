using SakuraMod.SakuraModCode.Extensions;

namespace SakuraMod.SakuraModCode.FourthAct.Fire;

public static class LibraEnemyAssets
{
    private const string Root = "monsters/fourth_act/libra";

    public const float CentralScale = 0.76f;
    public const float MoonScale = 0.85f;
    public const float SunScale = 0.62f;
    public const float MoonWidth = 300f;
    public const float MoonHeight = 300f;
    public const float SunWidth = 544f;
    public const float SunHeight = 706f;

    public static string Central => $"{Root}/central.png".ImagePath();
    public static string Moon => $"{Root}/moon.png".ImagePath();
    public static string Sun => $"{Root}/sun.png".ImagePath();

    public static IReadOnlyList<string> All { get; } = [Central, Moon, Sun];
}

internal static class LibraVisualLayout
{
    internal const float NeutralPanCenterY = 680f;
    internal const float IdealStep = 24f;
    internal const float SafeTop = 330f;
    internal const float SafeBottom = 980f;
    internal const float PanVisualCenterOffsetY = -40f;

    internal static float PanCenterY(int points, float scaledVisibleHeight)
    {
        var halfHeight = scaledVisibleHeight * 0.5f;
        var minimum = SafeTop + halfHeight;
        var maximum = SafeBottom - halfHeight;
        return Math.Clamp(
            NeutralPanCenterY + (Math.Clamp(points, 0, 10) - 5) * IdealStep,
            minimum,
            maximum);
    }

    internal static float CreatureY(int points, float scaledVisibleHeight) =>
        PanCenterY(points, scaledVisibleHeight) - PanVisualCenterOffsetY;
}
