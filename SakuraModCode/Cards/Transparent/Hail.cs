using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
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
        new HailDamageVar(6, ValueProp.Move),
        new PowerVar<SakuraFrostbitePower>(1),
        new DynamicVar("Magic", 10),
        new DynamicVar("BonusDamage", 2)
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

        var damage = HailRules.TotalDamage(this, spent);
        var frostbite = DynamicVars["SakuraFrostbitePower"].IntValue;
        var targets = CombatState!.HittableEnemies.ToList();
        await HailIceShardVfx.PlayOrResolveAsync(this, Owner.Creature, targets, async cues =>
        {
            foreach (var target in targets)
            {
                if (!target.IsAlive)
                    continue;

                cues.Impact(target);
                await SakuraActions.Attack(choiceContext, this, target, damage);

                if (target.IsAlive)
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

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2);
        DynamicVars["SakuraFrostbitePower"].UpgradeValueBy(1);
    }
}

internal static class HailRules
{
    internal static int TotalDamage(CardModel card, int? spentMagic = null)
    {
        var spent = spentMagic ?? SpendableMagic(card);
        return card.DynamicVars.Damage.IntValue + spent * card.DynamicVars["BonusDamage"].IntValue;
    }

    internal static int SpendableMagic(CardModel card)
    {
        if (!card.IsMutable || card.Owner is not { } owner)
            return 0;

        var maxSpend = card.DynamicVars["Magic"].IntValue;
        var current = owner.Creature.GetPower<ClassicMagicChargePower>()?.Amount ?? 0;
        return Math.Min(current, maxSpend);
    }
}

internal sealed class HailDamageVar(decimal damage, ValueProp props) : DamageVar(damage, props)
{
    public override void UpdateCardPreview(
        CardModel card,
        CardPreviewMode previewMode,
        Creature? target,
        bool runGlobalHooks)
    {
        var baseValue = HailRules.TotalDamage(card);
        decimal preview = baseValue;
        if (card.Enchantment is not null)
        {
            preview += card.Enchantment.EnchantDamageAdditive(preview, Props);
            preview *= card.Enchantment.EnchantDamageMultiplicative(preview, Props);
            if (!card.IsEnchantmentPreview)
                EnchantedValue = preview;
        }

        if (runGlobalHooks && card.IsMutable && card.Owner is { } owner)
            preview = Hook.ModifyDamage(
                owner.RunState,
                card.CombatState,
                target,
                owner.Creature,
                baseValue,
                Props,
                card,
                ModifyDamageHookType.All,
                previewMode,
                out _);

        PreviewValue = preview;
    }
}
