using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.Relics;
using SakuraMod.SakuraModCode.Extensions;
using STS2RitsuLib.Utils;

namespace SakuraMod.SakuraModCode.Cards;

public class ClowChange() : ClowExtraEffectCard(1, CardType.Skill, CardRarity.Common, TargetType.None)
{
    private const int ExtraDraw = 2;

    public override SakuraElementSet Elements => SakuraElementSet.Earth;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(2)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await ChangeCards(choiceContext, ReleasedValue("Cards"));

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await ChangeCards(choiceContext, ReleasedValue("Cards"));
        await CardPileCmd.Draw(choiceContext, ExtraDraw, Owner, false);
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1);

    private async Task ChangeCards(PlayerChoiceContext choiceContext, int drawPerDiscard)
    {
        var candidates = CardPile.GetCards(Owner, PileType.Hand).Where(card => card != this).ToList();
        if (candidates.Count == 0)
            return;

        var selected = await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(HandPrompt, 0, 1)
            {
                Cancelable = true,
                RequireManualConfirmation = false
            },
            card => candidates.Contains(card),
            this);

        var cards = selected.ToList();
        if (cards.Count == 0)
            return;

        await CardCmd.Discard(choiceContext, cards);
        await CardPileCmd.Draw(choiceContext, drawPerDiscard * cards.Count, Owner, false);
    }
}

public class SakuraChange() : SakuraFormCard(0, CardType.Skill, TargetType.None)
{
    public override SakuraElementSet Elements => SakuraElementSet.Earth;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(5)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var hand = CardPile.GetCards(Owner, PileType.Hand).Where(card => card != this).ToList();
        if (hand.Count > 0)
            await CardCmd.Discard(choiceContext, hand);
        await CardPileCmd.Draw(choiceContext, ReleasedValue("Cards"), Owner, false);
    }
}

