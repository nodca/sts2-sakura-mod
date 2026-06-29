using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Character;

namespace SakuraMod.SakuraModCode.Cards;

public class SakuraSpiral : CustomEnchantmentModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new IntVar("Times", 1)];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.ReplayDynamic, DynamicVars["Times"])
    ];

    public override bool CanEnchant(CardModel card) =>
        base.CanEnchant(card)
        && card.Rarity == CardRarity.Basic
        && (SakuraStarterCompatibility.IsStarterCard<Gale>(card) || SakuraStarterCompatibility.IsStarterCard<Siege>(card));

    public override int EnchantPlayCount(int originalPlayCount) =>
        originalPlayCount + DynamicVars["Times"].IntValue;
}
