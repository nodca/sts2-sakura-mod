using SakuraMod.SakuraModCode.Extensions;

namespace SakuraMod.SakuraModCode.FourthAct.Wind;

public static class WindEnemyAssets
{
    private const string Root = "monsters/fourth_act/wind";

    public static string Windy => $"{Root}/windy.png".ImagePath();
    public static string FlyAirborne => $"{Root}/fly_airborne.png".ImagePath();
    public static string FlyGrounded => $"{Root}/fly_grounded.png".ImagePath();
    public static string Illusion => $"{Root}/illusion.png".ImagePath();
    public static string Dash => $"{Root}/dash.png".ImagePath();
    public static string Float => $"{Root}/float.png".ImagePath();
    public static string Sleep => $"{Root}/sleep.png".ImagePath();
    public static string IllusionCast => $"{Root}/action_frames/illusion_cast.png".ImagePath();
    public static string WindyAction => $"{Root}/action_frames/windy_action.png".ImagePath();
    public static string DashAttack => $"{Root}/action_frames/dash_attack.png".ImagePath();
    public static string SleepCast => $"{Root}/action_frames/sleep_cast.png".ImagePath();

    public static IReadOnlyList<string> FlyTransitionFrames { get; } =
        Enumerable.Range(0, 7)
            .Select(index => $"{Root}/fly_transition/frame_{index:00}.png".ImagePath())
            .ToArray();

    public static IReadOnlyList<string> All { get; } =
    [
        Windy,
        FlyAirborne,
        FlyGrounded,
        Illusion,
        Dash,
        Float,
        Sleep,
        IllusionCast,
        WindyAction,
        DashAttack,
        SleepCast,
        .. FlyTransitionFrames
    ];

    public static IEnumerable<string> ActionFramesFor(string standeePath)
    {
        if (standeePath == Illusion)
            yield return IllusionCast;
        else if (standeePath == Windy)
            yield return WindyAction;
        else if (standeePath == Dash)
            yield return DashAttack;
        else if (standeePath == Sleep)
            yield return SleepCast;
    }
}
