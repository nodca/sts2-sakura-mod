using SakuraMod.SakuraModCode.Extensions;

namespace SakuraMod.SakuraModCode.FourthAct.Earth;

public static class EarthEnemyAssets
{
    private const string ShadowRoot = "monsters/fourth_act/shadow";
    private const string WoodRoot = "monsters/fourth_act/wood";
    private const string EarthyRoot = "monsters/fourth_act/earthy";

    public static string Shadow => $"{ShadowRoot}/shadow.png".ImagePath();
    public static string Wood => $"{WoodRoot}/wood.png".ImagePath();
    public static string Earthy => $"{EarthyRoot}/earthy.png".ImagePath();
}
