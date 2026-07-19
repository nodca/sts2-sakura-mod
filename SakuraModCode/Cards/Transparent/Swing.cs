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

public class Swing() : TransparentExtraEffectCard(2, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Earth];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CalculationBaseVar(12),
        new PowerVar<WeakPower>(1),
        new ExtraDamageVar(3),
        new CalculatedDamageVar(ValueProp.Move).WithMultiplier(SwingRules.WeakMultiplier)
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        var targets = CombatState!.HittableEnemies.ToList();
        foreach (var enemy in targets.Where(enemy => enemy.IsAlive))
            await SakuraActions.Attack(choiceContext, this, enemy, DynamicVars.CalculatedDamage);

        var survivingTargets = targets.Where(enemy => enemy.IsAlive).ToList();
        if (survivingTargets.Count > 0)
            await PowerCmd.Apply<WeakPower>(choiceContext, survivingTargets, DynamicVars.Weak.IntValue, Owner.Creature, this, false);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.CalculationBase.UpgradeValueBy(4);
        DynamicVars.ExtraDamage.UpgradeValueBy(1);
    }
}

internal static class SwingRules
{
    public static decimal WeakMultiplier(CardModel card, Creature? target) =>
        WeakMultiplier(
            target?.GetPower<WeakPower>()?.Amount ?? 0,
            SakuraCardModel.UsesMagicChargeExtraEffect(card));

    internal static int WeakMultiplier(int weak, bool doubleWeakBonus) =>
        Math.Max(0, weak) * (doubleWeakBonus ? 2 : 1);
}




