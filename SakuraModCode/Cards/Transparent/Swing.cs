using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
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
        new SwingDamageVar(12, ValueProp.Move)
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        var multiplier = SwingDamageWindowPower.RequestedMultiplier(activation.IsActive);
        var window = Owner.Creature.GetPower<SwingDamageWindowPower>();
        if (window is null)
        {
            window = await PowerCmd.Apply<SwingDamageWindowPower>(
                choiceContext,
                Owner.Creature,
                multiplier,
                Owner.Creature,
                this,
                false);
        }
        else
        {
            window.KeepHighestMultiplier(multiplier);
        }

        var targets = CombatState!.HittableEnemies.ToList();
        foreach (var enemy in targets.Where(enemy => enemy.IsAlive))
            await SakuraActions.Attack(choiceContext, this, enemy, DynamicVars.Damage.IntValue);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(4);
}

internal sealed class SwingDamageVar(decimal damage, ValueProp props) : DamageVar(damage, props)
{
    public override void UpdateCardPreview(
        CardModel card,
        CardPreviewMode previewMode,
        Creature? target,
        bool runGlobalHooks)
    {
        var hasWindow = card.IsMutable && card.Owner?.Creature.GetPower<SwingDamageWindowPower>() is not null;
        var localMultiplier = !hasWindow
            ? (target?.GetPower<WeakPower>()?.Amount > 0
                ? SwingDamageWindowPower.RequestedMultiplier(SakuraCardModel.UsesMagicChargeExtraEffect(card))
                : 1)
            : 1;
        var baseValue = BaseValue * localMultiplier;
        var preview = baseValue;
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
