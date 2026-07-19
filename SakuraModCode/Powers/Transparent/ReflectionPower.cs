using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.Extensions;
using STS2RitsuLib.Combat.HandSize;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace SakuraMod.SakuraModCode.Powers;

public class ReflectionPower : SakuraPowerModel
{
    protected override string IconFileName => "reflection.png";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    internal static int ReflectedDamage(int attackDamage, int reflectionStacks) =>
        (int)Math.Floor(Math.Max(0, attackDamage) * Math.Max(0, reflectionStacks) / 2m);

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature creature, DamageResult damageResult, ValueProp damageProps, Creature? source, CardModel? card)
    {
        if (Amount <= 0
            || creature != Owner
            || source is not { IsAlive: true } attacker
            || attacker.Side == Owner.Side
            || !damageProps.IsPoweredAttack()
            || damageResult.TotalDamage <= 0)
            return;

        var reflectionDamage = ReflectedDamage(damageResult.TotalDamage, (int)Amount);
        await CreatureCmd.Damage(choiceContext, attacker, reflectionDamage, SakuraPowerValueProps.Damage, Owner, null);
        await PowerCmd.Decrement(this);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Enemy && Owner.Side == CombatSide.Player && Amount > 0)
            await PowerCmd.Decrement(this);
    }
}

