using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Extensions;

namespace SakuraMod.SakuraModCode.Powers;

// Amount is how many of your next Attacks spread. Each Attack spends one charge as it starts,
// and stays spread until its play ends; the power leaves once a spent last charge's play ends.
public class LucidPiercePower : SakuraPowerModel
{
    private sealed class Data
    {
        public readonly List<uint> ActivePlays = [];
        public bool LastChargeSpent;
    }

    protected override string IconFileName => "lucid_pierce.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override object InitInternalData() => new Data();

    internal static bool ShouldSpread(Creature? dealer, CardModel? cardSource) =>
        dealer is not null
        && cardSource is { IsMutable: true, Type: CardType.Attack }
        && cardSource.TargetType != TargetType.AllEnemies
        && cardSource.Owner?.Creature == dealer
        && (dealer.GetPower<LucidPierceAllPower>() is not null
            || dealer.GetPower<LucidPiercePower>()?.GetInternalData<Data>().ActivePlays
                .Contains(NetCombatCard.FromModel(cardSource).CombatCardIndex) == true);

    internal static void SpreadAttack(AttackCommand command)
    {
        if (command.DamageProps.IsCardOrMonsterMove()
            && ShouldSpread(command.Attacker, command.ModelSource as CardModel))
            command.RetargetAllOpponents();
    }

    internal static IReadOnlyList<Creature>? ExpandDamageTargets(
        IEnumerable<Creature> targets, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (!props.IsCardOrMonsterMove()
            || !ShouldSpread(dealer, cardSource)
            || dealer?.CombatState is not { } combat)
            return null;

        var original = targets.ToList();
        if (original.Count == 0 || original.Any(target => target.Side == dealer.Side))
            return null;

        // Also covers manual hits grouped by AttackContext, which do not use builder targeting.
        return combat.GetOpponentsOf(dealer).Where(target => target.IsHittable).Distinct().ToList();
    }

    public override async Task BeforeCardPlayed(CardPlay cardPlay)
    {
        var data = GetInternalData<Data>();
        if (data.LastChargeSpent
            || cardPlay.Card.Owner?.Creature != Owner
            || cardPlay.Card.Type != CardType.Attack)
            return;

        data.ActivePlays.Add(NetCombatCard.FromModel(cardPlay.Card).CombatCardIndex);
        if (Amount > 1)
            await PowerCmd.Decrement(this);
        else
            data.LastChargeSpent = true;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var data = GetInternalData<Data>();
        if (!data.ActivePlays.Remove(NetCombatCard.FromModel(cardPlay.Card).CombatCardIndex))
            return;

        if (data.LastChargeSpent && data.ActivePlays.Count == 0)
            await PowerCmd.Remove(this);
    }

    public override Task BeforeAttack(AttackCommand command)
    {
        SpreadAttack(command);
        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner.Side == side && participants.Contains(Owner))
            await PowerCmd.Remove(this);
    }
}

public class LucidPierceAllPower : SakuraPowerModel
{
    protected override string IconFileName => "lucid_pierce.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override Task BeforeAttack(AttackCommand command)
    {
        LucidPiercePower.SpreadAttack(command);
        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner.Side == side && participants.Contains(Owner))
            await PowerCmd.Remove(this);
    }
}
