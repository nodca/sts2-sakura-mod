using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using SakuraMod.SakuraModCode.Cards;
using STS2RitsuLib.Utils;

namespace SakuraMod.SakuraModCode.Relics;

public class ClassicRedCapeRelic : SakuraRelicModel
{
    // Retired key, kept registered so older saves still serialize: a multiplayer
    // loaded-run join sends the save over the network, and SavedProperties maps
    // every stored name to a net id, throwing on names it no longer knows.
    private static readonly SavedAttachedState<ClassicRedCapeRelic, bool> LegacyActivatedThisCombat =
        new("SakuraMod_RedCapeActivatedThisCombat", () => false);

    // The turn the free Extra Effect was last used; 0 means not yet this combat.
    private static readonly SavedAttachedState<ClassicRedCapeRelic, int> ActivatedTurn =
        new("SakuraMod_RedCapeActivatedTurn", () => 0);

    protected override string IconFileName => "red_cape.png";

    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override bool IsAllowedInShops => false;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Turns", 3)];

    public override bool IsAllowed(IRunState runState) => false;

    public override Task BeforeCombatStart()
    {
        ActivatedTurn[this] = 0;
        return Task.CompletedTask;
    }

    internal bool TryActivateFreeExtraEffect(CardModel card)
    {
        if (!CanActivateFreeExtraEffect(card))
            return false;

        ActivatedTurn[this] = CurrentTurn;
        Flash();
        return true;
    }

    // Like Ring of the Drake, the first few turns are read from TurnNumber.
    internal bool CanActivateFreeExtraEffect(CardModel card) =>
        CanActivateFreeExtraEffect(
            CurrentTurn,
            ActivatedTurn[this],
            DynamicVars["Turns"].IntValue,
            card.Owner == Owner,
            IsEligible(card));

    internal static bool CanActivateFreeExtraEffect(
        int turnNumber,
        int activatedTurn,
        int turns,
        bool ownerMatches,
        bool isEligible) =>
        turnNumber >= 1
        && turnNumber <= turns
        && activatedTurn != turnNumber
        && ownerMatches
        && isEligible;

    internal static bool IsEligible(CardModel? card) =>
        card is SakuraSourceCard { IsClowCard: true }
        && SakuraExtraEffectTransaction.Supports(card);

    private int CurrentTurn => Owner.PlayerCombatState?.TurnNumber ?? 0;
}
