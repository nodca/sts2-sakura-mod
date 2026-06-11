using BaseLib.Abstracts;
using BaseLib.Utils;
using SakuraMod.SakuraModCode.Character;

namespace SakuraMod.SakuraModCode.Potions;

[Pool(typeof(SakuraModPotionPool))]
public abstract class SakuraModPotion : CustomPotionModel;