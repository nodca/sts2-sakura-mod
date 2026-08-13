using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Extensions;
using SakuraMod.SakuraModCode.Powers;
using STS2RitsuLib.Cards.DynamicVars;

namespace SakuraMod.SakuraModCode.Cards;

public class Blaze() : TransparentExtraEffectCard(3, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
{
    internal const int MaxCardsToExhaust = 3;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Fire];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CalculationBaseVar(27),
        new ExtraDamageVar(2),
        new CalculatedDamageVar(ValueProp.Move).WithMultiplier(BlazeRules.ExhaustedCardMultiplier)
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        await ExhaustSelectedHandCards(choiceContext);

        var target = RequiredTarget(play);
        await BlazeFireColumnVfx.PlayOrResolveAsync(this, Owner.Creature, target, async cues =>
        {
            // Before the attack: the damage number belongs on the beat the fire
            // lands, not after it.
            cues.Impact();
            await SakuraActions.Attack(choiceContext, this, target, DynamicVars.CalculatedDamage);
        });

        var exhaustedCards = CardPile.Get(PileType.Exhaust, Owner)?.Cards.ToList() ?? [];
        await CardPileCmd.RemoveFromCombat(exhaustedCards, skipVisuals: false);
    }

    private async Task ExhaustSelectedHandCards(PlayerChoiceContext choiceContext)
    {
        var hand = CardPile.GetCards(Owner, PileType.Hand)
            .Where(card => card != this)
            .ToList();
        var maxSelect = Math.Min(MaxCardsToExhaust, hand.Count);
        if (maxSelect == 0)
            return;

        var selected = await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 0, maxSelect)
            {
                Cancelable = true
            },
            card => hand.Contains(card),
            this);
        foreach (var card in selected)
            await CardCmd.Exhaust(choiceContext, card);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.CalculationBase.UpgradeValueBy(5);
        DynamicVars.ExtraDamage.UpgradeValueBy(1);
    }
}

internal static class BlazeRules
{
    public static decimal ExhaustedCardMultiplier(CardModel card, Creature? target) =>
        card.Owner is { } owner
            ? (CardPile.Get(PileType.Exhaust, owner)?.Cards.Count ?? 0)
                * (SakuraCardModel.UsesMagicChargeExtraEffect(card) ? 2 : 1)
            : 0;
}
