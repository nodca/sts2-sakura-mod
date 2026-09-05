namespace SakuraMod.SakuraModCode.FourthAct.Fire;

public static class FireEnemyRules
{
    public const int SwordHp = 240, SwordToughHp = 255, SwordSlash = 12, SwordDoubleCut = 7;
    public const int LibraPanHp = 140, LibraToughPanHp = 150, LibraAttack = 15, LibraBlock = 8, LibraAttackBonus = 6;
    public const int FireyHp = 440, FireyToughHp = 465, FlameBreath = 8, Fireball = 18, FireballPerBurn = 4;
    public const int LightHp = 520, LightToughHp = 545, Radiance = 20, Benediction = 16, JudgmentBase = 12, JudgmentPerCard = 3, EmpoweredJudgmentPerCard = 4;
    public static int JudgmentDamage(int handSize, bool empowered) => JudgmentBase + Math.Max(0, handSize) * (empowered ? EmpoweredJudgmentPerCard : JudgmentPerCard);
    public static (int Left, int Right) Swing(int left, int right, int vote) => (Math.Clamp(left - vote, 0, 10), Math.Clamp(right + vote, 0, 10));
    public static (int Left, int Right) Recenter(int left, int right) => (left + Math.Sign(5 - left), right + Math.Sign(5 - right));

    public static ((int Left, int Right) Vote, (int Left, int Right) Final) ResolveLibraTurn(
        int left,
        int right,
        int vote,
        string? imbalanceSide)
    {
        var afterVote = Swing(left, right, vote);
        var final = imbalanceSide switch
        {
            "LEFT" => (Math.Min(10, afterVote.Left + 1), afterVote.Right),
            "RIGHT" => (afterVote.Left, Math.Min(10, afterVote.Right + 1)),
            _ => afterVote
        };
        return (afterVote, final);
    }
}
