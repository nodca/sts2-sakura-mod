using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using SakuraMod.SakuraModCode.Cards;
using STS2RitsuLib.Utils;

namespace SakuraMod.SakuraModCode.Relics;

public class ClassicRedCapeRelic : SakuraRelicModel
{
    private static readonly SavedAttachedState<ClassicRedCapeRelic, bool> ActivatedThisCombat =
        new("SakuraMod_RedCapeActivatedThisCombat", () => false);

    protected override string IconFileName => "red_cape.png";

    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override bool IsAllowedInShops => false;

    public override bool IsAllowed(IRunState runState) => false;

    public override Task BeforeCombatStart()
    {
        ActivatedThisCombat[this] = false;
        return Task.CompletedTask;
    }

    internal bool TryActivateFreeExtraEffect(CardModel card)
    {
        if (!CanActivateFreeExtraEffect(card))
            return false;

        ActivatedThisCombat[this] = true;
        Flash();
        return true;
    }

    internal bool CanActivateFreeExtraEffect(CardModel card) =>
        CanActivateFreeExtraEffect(
            ActivatedThisCombat[this],
            card.Owner == Owner,
            IsEligible(card));

    internal static bool CanActivateFreeExtraEffect(
        bool activatedThisCombat,
        bool ownerMatches,
        bool isEligible) =>
        !activatedThisCombat && ownerMatches && isEligible;

    internal static bool IsEligible(CardModel? card) =>
        card is SakuraSourceCard { IsClowCard: true }
        && SakuraExtraEffectTransaction.Supports(card);
}
