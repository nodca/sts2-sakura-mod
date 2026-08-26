using MegaCrit.Sts2.Core;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using SakuraMod.SakuraModCode.Character;

namespace SakuraMod.SakuraModCode.Cards;

public class Rewind() : TransparentExtraEffectCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Earth, CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        var choices = CardPile.Get(PileType.Exhaust, Owner)!.Cards
            .Where(SakuraSourceCardRules.CanBeTargetedByClearCardEffects)
            .ToList();
        if (choices.Count == 0)
            return;

        var selectCount = DynamicVars.Cards.IntValue;
        var selected = (choices.Count <= selectCount
            ? choices
            : await SakuraActions.SelectUpToFromCards(
                this,
                choiceContext,
                choices,
                selectCount,
                cancelable: false)).ToList();

        foreach (var card in selected)
        {
            await SakuraActions.MoveExistingCardToHand(this, card);
            if (activation.IsActive)
                card.SetToFreeThisTurn();
        }
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1);
}
