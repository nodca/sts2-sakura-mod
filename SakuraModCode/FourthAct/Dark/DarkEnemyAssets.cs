using SakuraMod.SakuraModCode.Extensions;

namespace SakuraMod.SakuraModCode.FourthAct.Dark;

public static class DarkEnemyAssets
{
    private const string Root = "monsters/fourth_act/dark";

    public static string Standee => $"{Root}/dark.png".ImagePath();
    public static string Action => $"{Root}/action_frames/dark_action.png".ImagePath();
    public static string ConfinementOverlay =>
        Path.Join(MainFile.ResPath, "scenes", "cards", "overlays", "dark_confinement.tscn");
}
