using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using SakuraMod.SakuraModCode.Powers;

namespace SakuraMod.SakuraModCode.Cards;

public class Labyrinth() : TransparentCard(2, CardType.Power, CardRarity.Rare, TargetType.None)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Earth];
    internal override IEnumerable<string> ReferencedStaticHoverTipKeys => [SakuraCardHoverTips.LabyrinthTipKey];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation) =>
        await PowerCmd.Apply<LabyrinthPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this, false);

    protected override void OnUpgrade() => AddKeywordIfMissing(CardKeyword.Innate);
}
