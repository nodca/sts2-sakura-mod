using SakuraMod.SakuraModCode.Extensions;

namespace SakuraMod.SakuraModCode.FourthAct.Fire;

public static class FireEnemyAssets
{
    private const string Root = "monsters/fourth_act/firey";
    private const string SwordRoot = "monsters/fourth_act/sword";

    public static string Firey => $"{Root}/firey.png".ImagePath();
    public static string Sword => $"{SwordRoot}/sword.png".ImagePath();
}
