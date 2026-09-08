using MegaCrit.Sts2.Core.Entities.Powers;

namespace SakuraMod.SakuraModCode.Powers;

public class KindnessPower : SakuraPowerModel
{
    protected override string IconFileName => "kindness.png";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
}
