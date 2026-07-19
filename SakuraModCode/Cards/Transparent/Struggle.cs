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

public class Struggle() : TransparentExtraEffectCard(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Fire];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(16, ValueProp.Move),
        new ExtraDamageVar(8)
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        var damage = DynamicVars.Damage.IntValue;
        if (activation.IsActive)
            damage += DynamicVars.ExtraDamage.IntValue;

        await SakuraActions.Attack(choiceContext, this, RequiredTarget(play), damage);
    }

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

    private void ReduceCostBy(int amount) => EnergyCost.AddThisTurn(-amount);

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(4);
}

internal static class StruggleRules
{
    public static bool IsOtherAttack(Struggle source, CardModel playedCard) =>
        playedCard != source && playedCard.Type == CardType.Attack;
}


