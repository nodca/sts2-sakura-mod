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

public class Lucid() : TransparentExtraEffectCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Water];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar("Look", 3)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        var top = CardPile.Get(PileType.Draw, Owner)!.Cards.Take(DynamicVars["Look"].IntValue).ToList();
        var card = await SakuraActions.SelectFromCardPreviews(this, choiceContext, top, cancelable: false);
        if (card is not null)
            await SakuraActions.MoveExistingCardToHand(this, card);

        if (activation.IsActive)
            await ApplyExtraEffect(choiceContext, play);
    }

    private async Task ApplyExtraEffect(PlayerChoiceContext choiceContext, CardPlay play) =>
        await PowerCmd.Apply<LucidPiercePower>(choiceContext, Owner.Creature, 1, Owner.Creature, this, false);

    protected override void OnUpgrade() => DynamicVars["Look"].UpgradeValueBy(1);
}

