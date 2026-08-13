using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Runs;

namespace SakuraMod.SakuraModCode.Relics;

public class ClassicPinkTransformationCostumeRelic : SakuraRelicModel
{
    protected override string IconFileName => "pink_transformation_costume.png";

    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override bool IsAllowedInShops => false;

    public override bool IsAllowed(IRunState runState) => false;
}
