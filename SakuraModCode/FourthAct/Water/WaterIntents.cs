using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace SakuraMod.SakuraModCode.FourthAct.Water.Intents;

public sealed class TidalDrawIntent(Func<decimal> damage, Func<(int Minimum, int Maximum)> range) : SingleAttackIntent(damage)
{
    public override LocString GetIntentLabel(IEnumerable<Creature> targets, Creature owner)
    {
        var (minimum, maximum) = range();
        var label = new LocString("intents", "SAKURA_MOD_FORMAT_DAMAGE_RANGE");
        label.Add("Min", minimum);
        label.Add("Max", maximum);
        return label;
    }
}

public sealed class WaterBlockStealIntent : DebuffIntent
{
    protected override string IntentPrefix => "SAKURA_MOD_STEAL_BLOCK";
    public override string GetAnimation(IEnumerable<Creature> targets, Creature owner) => "debuff";
}
