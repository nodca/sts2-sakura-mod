using BaseLib.Abstracts;
using SakuraMod.SakuraModCode.Extensions;
using Godot;

namespace SakuraMod.SakuraModCode.Character;

public class SakuraModRelicPool : CustomRelicPoolModel
{
    public override Color LabOutlineColor => SakuraMod.Color;

    public override string BigEnergyIconPath => "charui/big_energy.png".ImagePath();
    public override string TextEnergyIconPath => "charui/text_energy.png".ImagePath();
}