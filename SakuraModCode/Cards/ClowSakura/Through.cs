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

public class ClowThrough() : ClowExtraEffectCard(1, CardType.Attack, CardRarity.Rare, TargetType.None)
{
    private const int ExtraMaxHpRate = 20;

    public override SakuraElementSet Elements => SakuraElementSet.Earth;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new SakuraSourceDamageVar(12, ValueProp.Move), new DynamicVar("Magic", 0), new DynamicVar("Rate", ExtraMaxHpRate)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await DealDamageToEnemies(choiceContext, CombatState!.HittableEnemies, ReleasedDamage());
        await TriggerCurrentPoison(choiceContext, CombatState!.HittableEnemies, triggerCount: 1);
        await SakuraMagicCharge.AddVoidToDiscardPile(choiceContext, Owner);
    }

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await DealDamageToEnemies(choiceContext, CombatState!.HittableEnemies, ReleasedDamage());
        await TriggerPoisonFollowUp(choiceContext, includeAddedPoison: true);
        foreach (var enemy in CombatState!.HittableEnemies.ToList())
        {
            var damage = enemy.MaxHp * ExtraMaxHpRate / 100;
            if (damage > 0)
                await DealDamage(choiceContext, enemy, damage);
        }
        await SakuraMagicCharge.AddVoidToDiscardPile(choiceContext, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3);
        DynamicVars["Magic"].UpgradeValueBy(2);
    }

    private async Task TriggerPoisonFollowUp(PlayerChoiceContext choiceContext, bool includeAddedPoison)
    {
        foreach (var enemy in CombatState!.HittableEnemies.ToList())
        {
            var poison = enemy.GetPower<PoisonPower>()?.Amount ?? 0;
            if (poison <= 0)
                continue;

            if (ReleasedMagic() > 0)
                await ApplyPower<PoisonPower>(choiceContext, enemy, ReleasedMagic());
            var damage = poison + (includeAddedPoison ? ReleasedMagic() : 0);
            await DealDamage(choiceContext, enemy, damage, ValueProp.Unblockable);
        }
    }
}

public class SakuraThrough() : SakuraFormCard(1, CardType.Attack, TargetType.None)
{
    private const int MinDamage = 10;
    private const int MaxHpRate = 10;

    public override SakuraElementSet Elements => SakuraElementSet.Earth;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new SakuraSourceDamageVar(MinDamage, ValueProp.Move), new DynamicVar("Rate", MaxHpRate)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        foreach (var enemy in CombatState!.HittableEnemies.ToList())
        {
            var damage = Math.Max(ReleasedDamage(), enemy.MaxHp * MaxHpRate / 100);
            await DealDamage(choiceContext, enemy, damage);
        }
    }
}

internal static class SakuraEnemyRules
{
    public static bool IsMinion(Creature target) =>
        target.IsSecondaryEnemy || target.HasPower<MinionPower>();

    public static bool IsEliteOrBossCombat(Creature target) =>
        target.CombatState?.Encounter?.RoomType is RoomType.Elite or RoomType.Boss;

    public static bool IsEliteOrBossTarget(Creature target) =>
        IsEliteOrBossCombat(target) && !IsMinion(target);

    public static bool IsBossCombat(Creature target) =>
        target.CombatState?.Encounter?.RoomType is RoomType.Boss;
}

