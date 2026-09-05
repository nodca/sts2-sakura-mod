using SakuraMod.SakuraModCode.Extensions;

namespace SakuraMod.SakuraModCode.FourthAct.Water;

public static class WaterEnemyAssets
{
    private const string Root = "monsters/fourth_act/water";

    public static string Freeze => $"{Root}/freeze.png".ImagePath();
    public static string Rain => $"{Root}/rain.png".ImagePath();
    public static string Watery => $"{Root}/watery.png".ImagePath();
}
