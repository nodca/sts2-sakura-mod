using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Runs;

namespace SakuraMod.SakuraModCode.Relics;

public class ClassicFrogRaincoatRelic : SakuraRelicModel
{
    protected override string IconFileName => "frog_raincoat.png";

    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override bool IsAllowedInShops => false;

    public override bool IsAllowed(IRunState runState) =>
        false;
}
