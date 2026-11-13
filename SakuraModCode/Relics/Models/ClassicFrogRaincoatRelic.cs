using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Runs;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using STS2RitsuLib.Utils;

namespace SakuraMod.SakuraModCode.Relics;

public class ClassicFrogRaincoatRelic : SakuraRelicModel
{
    // Like Music Box: the first matching play each turn pays off once, evaluated
    // from the After hook's play and stored where the checksum can see it.
    private static readonly SavedAttachedState<ClassicFrogRaincoatRelic, bool> ManifestedThisTurn =
        new("SakuraMod_FrogRaincoatManifestedThisTurn", () => false);

    protected override string IconFileName => "frog_raincoat.png";

    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override bool IsAllowedInShops => false;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromKeyword(SakuraKeywords.Manifest),
        SakuraCardHoverTips.StaticTip(SakuraCardHoverTips.TemporaryTipKey)
    ];

    protected override bool IsAllowedInternal(IRunState runState) => false;

    public override Task BeforeCombatStart()
    {
        ManifestedThisTurn[this] = false;
        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (participants.Contains(Owner.Creature))
            ManifestedThisTurn[this] = false;
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (ManifestedThisTurn[this]
            || play.Card?.Owner != Owner
            || !SakuraTransparentCardCatalog.IsTransparentCard(play.Card))
            return;

        ManifestedThisTurn[this] = true;
        Flash();
        await SakuraManifestLoop.Manifest(Owner, choiceContext, DynamicVars.Cards.IntValue);
    }
}
