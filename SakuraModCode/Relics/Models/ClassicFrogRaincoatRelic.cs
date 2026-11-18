using STS2RitsuLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Runs;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;

namespace SakuraMod.SakuraModCode.Relics;

public class ClassicFrogRaincoatRelic : SakuraRelicModel
{
    // Retired key, kept registered so older saves still serialize: a multiplayer
    // loaded-run join sends the save over the network, and SavedProperties maps
    // every stored name to a net id, throwing on names it no longer knows.
    private static readonly SavedAttachedState<ClassicFrogRaincoatRelic, bool> LegacyManifestedThisTurn =
        new("SakuraMod_FrogRaincoatManifestedThisTurn", () => false);

    protected override string IconFileName => "frog_raincoat.png";

    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override bool IsAllowedInShops => false;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new CardsVar(1), new DynamicVar("Turns", 3)];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromKeyword(SakuraKeywords.Manifest),
        SakuraCardHoverTips.StaticTip(SakuraCardHoverTips.TemporaryTipKey)
    ];

    public override bool IsAllowed(IRunState runState) => false;

    // Like Ring of the Drake, the first few turns are read from TurnNumber; the
    // Manifest choices arrive upgraded, before the hand is drawn.
    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        if (player != Owner
            || Owner.PlayerCombatState is not { } playerCombat
            || playerCombat.TurnNumber > DynamicVars["Turns"].IntValue)
            return;

        Flash();
        await SakuraManifestLoop.Manifest(Owner, choiceContext, DynamicVars.Cards.IntValue, upgraded: true);
    }
}
