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

public class ClowRain() : ClowExtraEffectCard(2, CardType.Skill, CardRarity.Uncommon, TargetType.None)
{
    public override SakuraElementSet Elements => SakuraElementSet.Water;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override PileType GetResultPileTypeForCardPlay()
    {
        return SakuraExtraEffectTransaction.CanActivate(Owner)
            ? PileType.Discard
            : base.GetResultPileTypeForCardPlay();
    }

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ReduceHandCosts(Owner, 1);
        await Task.CompletedTask;
    }

    protected override Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ReduceHandCosts(Owner, 1);
        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);

    private static void ReduceHandCosts(Player owner, int amount)
    {
        foreach (var card in CardPile.GetCards(owner, PileType.Hand).Where(static card => card.EnergyCost.GetWithModifiers(CostModifiers.Local) > 0))
            card.EnergyCost.AddThisTurn(-amount, true);
    }
}

public class SakuraRain() : SakuraFormCard(0, CardType.Skill, TargetType.None)
{
    public override SakuraElementSet Elements => SakuraElementSet.Water;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Magic", 3)];

    protected override Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var choices = CardPile.GetCards(Owner, PileType.Hand)
            .Where(static card => card.EnergyCost.GetWithModifiers(CostModifiers.Local) > 0)
            .ToList();

        var amount = Math.Min(ReleasedMagic(), choices.Count);
        List<CardModel> targets = [];
        for (var i = 0; i < amount; i++)
        {
            var card = choices.Count == amount
                ? choices[0]
                : Owner.RunState.Rng.CombatCardSelection.NextItem(choices);
            if (card is null)
                break;

            targets.Add(card);
            choices.Remove(card);
        }

        foreach (var card in targets)
            card.EnergyCost.SetThisCombat(0, true);
        return Task.CompletedTask;
    }
}

