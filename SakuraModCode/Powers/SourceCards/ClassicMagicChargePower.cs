using MegaCrit.Sts2.Core.Entities.Powers;

namespace SakuraMod.SakuraModCode.Powers;

public class ClassicMagicChargePower : SakuraPowerModel
{
    protected override string IconFileName => "magick_charge_power.png";
    protected override bool IsVisibleInternal => false;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
}
