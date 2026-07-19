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

public class ClowBubbles() : ClowExtraEffectCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override SakuraElementSet Elements => SakuraElementSet.Water;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new SakuraSourceDamageVar(5, ValueProp.Move), new DynamicVar("Magic", 2)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await SakuraThroughResolution.WithPropagationSuppressed(async () =>
        {
            foreach (var target in SakuraThroughResolution.TargetsFor(play))
            {
                await DealDamage(choiceContext, target, ReleasedDamage());
                var removed = await RemoveRandomBuff(choiceContext, target);
                await GainUpgradeMagicCharge(choiceContext, removed);
            }
        });
    }

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await SakuraThroughResolution.WithPropagationSuppressed(async () =>
        {
            foreach (var target in SakuraThroughResolution.TargetsFor(play))
            {
                await DealDamage(choiceContext, target, ReleasedDamage());
                var removed = await RemoveAllBuffs(target);
                await GainUpgradeMagicCharge(choiceContext, removed);
            }
        });
    }

    protected override void OnUpgrade() { }

    private async Task GainUpgradeMagicCharge(PlayerChoiceContext choiceContext, bool removed)
    {
        if (IsUpgraded && removed && Owner.GetRelic<ClassicSealedBookRelic>() is not null)
            await SakuraMagicCharge.GainMagic(choiceContext, Owner, ReleasedMagic(), this);
    }

    private async Task<bool> RemoveRandomBuff(PlayerChoiceContext choiceContext, Creature target)
    {
        var buffs = target.Powers.Where(SakuraPowerRules.IsBubblesRemovableBuff).ToList();
        var buff = Owner.RunState.Rng.CombatCardSelection.NextItem(buffs);
        if (buff is null)
            return false;

        await PowerCmd.Remove(buff);
        return true;
    }

    private static async Task<bool> RemoveAllBuffs(Creature target)
    {
        var removed = false;
        foreach (var buff in target.Powers.Where(SakuraPowerRules.IsBubblesRemovableBuff).ToList())
        {
            await PowerCmd.Remove(buff);
            removed = true;
        }

        return removed;
    }
}

public class SakuraBubbles() : SakuraFormCard(0, CardType.Attack, TargetType.AllEnemies)
{
    public override SakuraElementSet Elements => SakuraElementSet.Water;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust, CardKeyword.Retain];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new SakuraSourceDamageVar(5, ValueProp.Move)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var enemies = CombatState!.HittableEnemies.ToList();
        await DealDamageToEnemies(choiceContext, enemies, ReleasedDamage());
        foreach (var enemy in enemies)
        {
            foreach (var buff in enemy.Powers.Where(SakuraPowerRules.IsBubblesRemovableBuff).ToList())
                await PowerCmd.Remove(buff);
        }
    }
}
