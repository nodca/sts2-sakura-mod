using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using SakuraMod.SakuraModCode.FourthAct.Wind.Models;

namespace SakuraMod.SakuraModCode.FourthAct.Wind.Intents;

public sealed class WindAttendantSummonIntent(Func<Type?> attendantType) : SummonIntent
{
    protected override string IntentPrefix => attendantType() switch
    {
        Type type when type == typeof(DashMonster) => "SAKURA_MOD_SUMMON_DASH",
        Type type when type == typeof(FloatMonster) => "SAKURA_MOD_SUMMON_FLOAT",
        Type type when type == typeof(SleepMonster) => "SAKURA_MOD_SUMMON_SLEEP",
        _ => "SUMMON"
    };

    public override string GetAnimation(IEnumerable<Creature> targets, Creature owner) => "summon";
}
