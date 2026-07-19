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

public class ClowArrow() : ClowExtraEffectCard(0, CardType.Attack, CardRarity.Common, TargetType.None)
{
    public override SakuraElementSet Elements => SakuraElementSet.Fire;
    public override TargetType TargetType =>
        IsMutable && SakuraMagicCharge.CanSpendMagic(Owner)
            ? TargetType.AnyEnemy
            : base.TargetType;
    protected override bool HasEnergyCostX => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new SakuraSourceDamageVar(5, ValueProp.Move)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (HandWithoutSelf().Count == 0 && play.Resources.EnergySpent <= 0)
            return;

        var discarded = await SelectHandCards(choiceContext, discard: true);
        await FireArrows(choiceContext, null, discarded + ResolveEnergyXValue());
    }

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (HandWithoutSelf().Count == 0 && play.Resources.EnergySpent <= 0)
            return;

        var discarded = await SelectHandCards(choiceContext, discard: true);
        await FireArrows(choiceContext, RequiredTarget(play), discarded + ResolveEnergyXValue());
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(2);

    private async Task<int> SelectHandCards(PlayerChoiceContext choiceContext, bool discard)
    {
        var hand = HandWithoutSelf();
        if (hand.Count == 0)
            return 0;

        var selected = (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, 0, hand.Count)
            {
                Cancelable = true
            },
            card => hand.Contains(card),
            this)).ToList();

        if (selected.Count > 0 && discard)
            await CardCmd.Discard(choiceContext, selected);

        return selected.Count;
    }

    private async Task FireArrows(PlayerChoiceContext choiceContext, Creature? fixedTarget, int count)
    {
        if (fixedTarget is not null)
        {
            await DealDamage(choiceContext, fixedTarget, ReleasedDamage(), hitCount: count);
            return;
        }

        await DealDamageToRandomEnemies(choiceContext, ReleasedDamage(), count);
    }

    private List<CardModel> HandWithoutSelf() =>
        CardPile.GetCards(Owner, PileType.Hand).Where(card => card != this).ToList();
}

public class SakuraArrow() : SakuraFormCard(1, CardType.Attack, TargetType.None)
{
    public override SakuraElementSet Elements => SakuraElementSet.Fire;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new SakuraSourceDamageVar(7, ValueProp.Move)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var drawPile = CardPile.GetCards(Owner, PileType.Draw).ToList();
        var count = CountDistinctCardTypes(drawPile);

        if (drawPile.Count > 0)
            await CardCmd.Discard(choiceContext, drawPile);

        await DealDamageToRandomEnemies(choiceContext, ReleasedDamage(), count);
    }

    internal static int CountDistinctCardTypes(IEnumerable<CardModel> cards) =>
        cards.Select(static card => card.GetType()).Distinct().Count();
}

