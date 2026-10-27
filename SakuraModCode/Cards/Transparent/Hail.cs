using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Extensions;
using SakuraMod.SakuraModCode.Powers;

namespace SakuraMod.SakuraModCode.Cards;

public class Hail() : TransparentCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Water];
    internal override IEnumerable<CardKeyword> ReferencedKeywords => [SakuraKeywords.Frostbite];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(8, ValueProp.Move),
        new PowerVar<SakuraFrostbitePower>(1),
        new DynamicVar("Magic", 3)
    ];

    protected override async Task PlayCard(
        PlayerChoiceContext choiceContext,
        CardPlay play,
        SakuraExtraEffectActivation activation)
    {
        var opportunity = SakuraMagicCharge.CaptureOpportunity(Owner);
        await SakuraMagicCharge.TryApplyCapturedOpportunity(choiceContext, this, opportunity);

        var maxSpend = DynamicVars["Magic"].IntValue;
        var spent = HailRules.SpendableMagic(this);
        if (spent > 0)
            await SakuraMagicCharge.SpendUpToMagic(choiceContext, Owner, maxSpend);

        var damage = DynamicVars.Damage.IntValue;
        var frostbite = HailRules.Frostbite(this, spent);
        var targets = CombatState!.HittableEnemies.ToList();
        await HailStormVfx.PlayOrResolveAsync(this, Owner.Creature, targets, spent, maxSpend, async cues =>
        {
            foreach (var target in targets)
            {
                if (!target.IsAlive)
                    continue;

                cues.Impact(target);
                await DealDamage(choiceContext, target, damage);

                if (target.IsAlive && frostbite > 0)
                    await PowerCmd.Apply<SakuraFrostbitePower>(
                        choiceContext,
                        target,
                        frostbite,
                        Owner.Creature,
                        this,
                        false);
            }
        });
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
}

internal static class HailRules
{
    internal static int Frostbite(CardModel card, int spentMagic) =>
        Math.Max(0, spentMagic) * card.DynamicVars["SakuraFrostbitePower"].IntValue;

    internal static int SpendableMagic(CardModel card)
    {
        if (!card.IsMutable || card.Owner is not { } owner)
            return 0;

        var maxSpend = card.DynamicVars["Magic"].IntValue;
        var current = owner.Creature.GetPower<ClassicMagicChargePower>()?.Amount ?? 0;
        return Math.Min(current, maxSpend);
    }
}
