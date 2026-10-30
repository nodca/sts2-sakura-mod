using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace SakuraMod.SakuraModCode.FourthAct.Fire.Intents;

// Flame Breath gains a hit every use, and each unblocked hit can add one Burn.
public sealed class GrowingStatusIntent(Func<int> count) : StatusIntent(count())
{
    public override LocString GetIntentLabel(IEnumerable<Creature> targets, Creature owner)
    {
        var label = base.GetIntentLabel(targets, owner);
        label.Add("CardCount", count());
        return label;
    }

    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        var description = base.GetIntentDescription(targets, owner);
        description.Add("CardCount", count());
        return description;
    }
}
