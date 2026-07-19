using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models.Powers;

namespace SakuraMod.SakuraModCode.Powers;

// Retained as an inert registered model so existing saves keep their public power identity.
public class SiegeGrowthPower : SakuraPowerModel
{
    protected override bool IsVisibleInternal => false;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
}
