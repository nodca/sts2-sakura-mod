using BaseLib.Abstracts;
using BaseLib.Extensions;
using BaseLib.Patches.Localization;
using SakuraMod.SakuraModCode.Extensions;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization;

namespace SakuraMod.SakuraModCode.Powers;

/// <summary>
/// This is the base class for your mod's powers, which is set up to load the power's images from your mod's resources.
/// When creating a power, right click the Powers folder and create a new file with the Custom Power template.
/// This will generate a class that extends this one.
/// You can also just create the class manually; just make sure to inherit from this class.
/// </summary>
public abstract class SakuraModPower : CustomPowerModel, IAddDumbVariablesToPowerDescription
{
    //Loads from SakuraMod/images/powers/your_power.png
    protected virtual string IconFileName => "power.png";

    public override string CustomPackedIconPath => IconFileName.PowerImagePath();
    public override string CustomBigIconPath => IconFileName.BigPowerImagePath();

    public void AddDumbVariablesToPowerDescription(LocString description) =>
        DynamicVars.AddTo(description);

    /// <summary>
    /// Whether this power is a buff or debuff.
    /// </summary>
    public abstract override PowerType Type { get; }

    /// <summary>
    /// How this power stacks if reapplied. Counter is the most common type, where applying the power again just
    /// adds to the amount. Single means the power does not stack, like Barricade. None functions identically to
    /// Single, but you're suggested to use Single as it is more explicit about how it will work.
    /// </summary>
    public abstract override PowerStackType StackType { get; }
}
