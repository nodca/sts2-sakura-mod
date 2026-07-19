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

public class Aqua() : TransparentExtraEffectCard(0, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Water, CardKeyword.Exhaust];
    internal override IEnumerable<CardKeyword> ReferencedKeywords => [SakuraKeywords.Frostbite];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(3, ValueProp.Move),
        new PowerVar<SakuraFrostbitePower>(1),
        new PowerVar<WeakPower>(1)
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        var targets = CombatState!.HittableEnemies.ToList();
        SakuraCardPlayVfx.PlayAqua(targets);
        foreach (var enemy in targets)
            await SakuraActions.Attack(choiceContext, this, enemy, DynamicVars.Damage.IntValue);

        var frostbittenTargets = targets
            .Where(static enemy => enemy.IsAlive && enemy.GetPower<SakuraFrostbitePower>() is { Amount: > 0 })
            .ToList();
        await PowerCmd.Apply<SakuraFrostbitePower>(
            choiceContext,
            frostbittenTargets,
            DynamicVars["SakuraFrostbitePower"].IntValue,
            Owner.Creature,
            this,
            false);
        await PowerCmd.Apply<WeakPower>(choiceContext, targets, DynamicVars.Weak.IntValue, Owner.Creature, this, false);
    }

    protected override PileType GetResultPileTypeForCardPlay() =>
        SakuraCardModel.UsesMagicChargeExtraEffect(this)
            ? PileType.Discard
            : base.GetResultPileTypeForCardPlay();

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2);
        DynamicVars.Weak.UpgradeValueBy(1);
    }
}

