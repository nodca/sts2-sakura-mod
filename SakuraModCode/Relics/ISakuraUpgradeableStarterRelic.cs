using MegaCrit.Sts2.Core.Models;

namespace SakuraMod.SakuraModCode.Relics;

public interface ISakuraUpgradeableStarterRelic
{
    RelicModel? GetUpgradeReplacement();
}
