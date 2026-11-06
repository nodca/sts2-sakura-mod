using MegaCrit.Sts2.Core;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Extensions;

namespace SakuraMod.SakuraModCode.Cards;

public class Blank() : TransparentExtraEffectCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    internal const int ExtraEnergy = 2;

    private static readonly PileType[] ForgottenTargetPileTypes =
    [
        PileType.Hand,
        PileType.Draw,
        PileType.Discard
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Earth, CardKeyword.Exhaust];
    internal override IEnumerable<string> ReferencedStaticHoverTipKeys =>
        [SakuraCardHoverTips.TemporaryTipKey];
    internal static IReadOnlyList<PileType> TargetPileTypes => ForgottenTargetPileTypes;

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        var cards = CardPile.GetCards(Owner, ForgottenTargetPileTypes)
            .Where(card => CanGainForgotten(card.Type, card.IsTemporary()))
            .ToList();

        var forgottenCards = 0;
        foreach (var card in cards)
        {
            if (await SakuraForgotten.GrantTemporary(choiceContext, card))
                forgottenCards++;
        }

        if (forgottenCards > 0)
        {
            await PowerCmd.Apply<DrawCardsNextTurnPower>(
                choiceContext,
                Owner.Creature,
                forgottenCards,
                Owner.Creature,
                this,
                false);
        }

        if (activation.IsActive)
            await ApplyExtraEffect(choiceContext);
    }

    internal static bool CanGainForgotten(CardType type, bool isForgotten) =>
        !isForgotten && type is CardType.Status or CardType.Curse;

    private Task ApplyExtraEffect(PlayerChoiceContext choiceContext) =>
        PowerCmd.Apply<EnergyNextTurnPower>(
            choiceContext,
            Owner.Creature,
            ExtraEnergy,
            Owner.Creature,
            this,
            false);

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
