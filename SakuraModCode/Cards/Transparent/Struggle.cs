using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Character;

namespace SakuraMod.SakuraModCode.Cards;

public class Struggle() : TransparentExtraEffectCard(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Fire];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(14, ValueProp.Move),
        new DamageVar("ExtraDamage", 10, ValueProp.Move)
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        var target = RequiredTarget(play);
        var damage = DynamicVars.Damage.IntValue;
        if (!activation.IsActive)
        {
            await DealDamage(choiceContext, play, target, damage, hitVfx: SakuraNativeHitFx.HeavyBlunt, hitTmpSfx: SakuraNativeHitFx.HeavyTmpSfx);
            return;
        }

        // Resolve both hits before consuming powers such as Vigor.
        await using var attack = await AttackCommand.CreateContextAsync(CombatState!, choiceContext, play);
        await DealDamageHit(attack, choiceContext, play, target, damage,
            hitVfx: SakuraNativeHitFx.HeavyBlunt, hitTmpSfx: SakuraNativeHitFx.HeavyTmpSfx);
        if (Owner.Creature.IsAlive)
            await DealDamageHit(attack, choiceContext, play, target, DynamicVars["ExtraDamage"].IntValue,
                hitVfx: SakuraNativeHitFx.HeavyBlunt, hitTmpSfx: SakuraNativeHitFx.HeavyTmpSfx);
    }

    public override decimal ModifyDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay) =>
        cardSource == this && dealer == Owner.Creature && props.IsPoweredAttack()
            ? Owner.Creature.GetPower<StrengthPower>()?.Amount ?? 0
            : 0;

    public override Task AfterCardEnteredCombat(CardModel card)
    {
        if (card != this || IsClone)
            return Task.CompletedTask;

        var attacksPlayedThisTurn = CombatManager.Instance.History.CardPlaysFinished.Count(entry =>
            StruggleRules.IsOtherAttack(this, entry.CardPlay.Card)
            && entry.CardPlay.Card.Owner == Owner
            && entry.HappenedThisTurn(CombatState));
        ReduceCostBy(attacksPlayedThisTurn);
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await base.AfterCardPlayed(choiceContext, play);

        if (play.Card.Owner == Owner && StruggleRules.IsOtherAttack(this, play.Card))
            ReduceCostBy(1);
    }

    private void ReduceCostBy(int amount)
    {
        // A negative local cost skips native combat cost hooks, including Gravitation's surcharge.
        var localCost = EnergyCost.GetWithModifiers(CostModifiers.Local);
        if (localCost > 0 && amount > 0)
            EnergyCost.AddThisTurn(-Math.Min(amount, localCost));
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(4);
}

internal static class StruggleRules
{
    public static bool IsOtherAttack(Struggle source, CardModel playedCard) =>
        playedCard != source && playedCard.Type == CardType.Attack;
}
