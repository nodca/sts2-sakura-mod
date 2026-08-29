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

public class ClowStorm() : ClowExtraEffectCard(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    private const int Hits = 5;

    public override SakuraElementSet Elements => SakuraElementSet.Wind;
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CalculationBaseVar(3),
        new ExtraDamageVar(1),
        new CalculatedDamageVar(ValueProp.Move).WithMultiplier(StormRules.WindCardsPlayedThisTurn),
        new DynamicVar("Magic", Hits)
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = RequiredTarget(play);
        await DealDamage(choiceContext, target, CalculatedDamage(target), hitCount: ReleasedMagic());
    }

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await DealDamageToEnemies(choiceContext, CombatState!.HittableEnemies, CalculatedDamage(null), hitCount: ReleasedMagic());
    }

    private int CalculatedDamage(Creature? target) =>
        Math.Max(0, (int)DynamicVars.CalculatedDamage.Calculate(target));

    protected override void OnUpgrade() => DynamicVars.CalculationBase.UpgradeValueBy(1);
}

internal static class StormRules
{
    public static decimal WindCardsPlayedThisTurn(CardModel card, Creature? _) =>
        WindCardsPlayedThisTurnCount(card);

    internal static int WindCardsPlayedThisTurnCount(CardModel card)
    {
        if (card.Owner is not { } owner || card.CombatState is null)
            return 0;

        return CombatManager.Instance.History.CardPlaysFinished
            .Where(entry => entry.CardPlay.Card.Owner == owner && entry.HappenedThisTurn(card.CombatState))
            .Select(entry => entry.CardPlay.Card)
            .Count(CountsAsWindCard);
    }

    internal static bool CountsAsWindCard(CardModel card) =>
        SakuraActions.HasElement(card, SakuraElement.Wind);
}

public class SakuraStorm() : SakuraFormCard(1, CardType.Attack, TargetType.None)
{
    private const int MaxDamageOffset = 5;

    public override SakuraElementSet Elements => SakuraElementSet.Wind;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new SakuraSourceDamageVar(4, ValueProp.Move), new DynamicVar("MaxDamage", 9), new DynamicVar("Magic", 7)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await using var attack = await AttackCommand.CreateContextAsync(CombatState!, choiceContext, this);
        for (var i = 0; i < ReleasedMagic(); i++)
        {
            var target = Owner.RunState.Rng.CombatCardSelection.NextItem(CombatState!.HittableEnemies.ToList());
            if (target is null)
                return;

            var amount = Owner.RunState.Rng.CombatCardSelection.NextInt(ReleasedDamage(), ReleasedDamage() + MaxDamageOffset + 1);
            await DealDamageHit(attack, choiceContext, target, amount);
        }
    }
}

