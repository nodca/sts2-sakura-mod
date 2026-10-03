using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.ValueProps;

namespace SakuraMod.SakuraModCode.FourthAct.Earth.Intents;

public sealed class ShadowAttackIntent(Creature target, int damage, int repeats) : MultiAttackIntent(damage, repeats)
{
    internal Creature Target => target;

    // Vanilla AttackIntent always previews damage against the local player. This intent has an explicit target.
    private int GetTargetDamage(Creature owner)
    {
        var amount = DamageCalc!();
        if (target.Player is { } player)
            amount = Hook.ModifyDamage(player.RunState, target.CombatState, target, owner, amount, ValueProp.Move,
                null, ModifyDamageHookType.All, CardPreviewMode.None, out _);
        return Math.Max(0, (int)amount);
    }

    public override int GetTotalDamage(IEnumerable<Creature> targets, Creature owner) => GetTargetDamage(owner) * Repeats;

    public override LocString GetIntentLabel(IEnumerable<Creature> targets, Creature owner)
    {
        var label = new LocString("intents", Repeats == 1 ? "FORMAT_DAMAGE_SINGLE" : "FORMAT_DAMAGE_MULTI");
        label.Add("Damage", GetTargetDamage(owner));
        label.Add("Repeat", Repeats);
        return label;
    }

    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        var description = new LocString("intents", Repeats == 1
            ? "SAKURA_MOD_SHADOW_ATTACK_SINGLE.description" : "SAKURA_MOD_SHADOW_ATTACK_MULTI.description");
        description.Add("Target", target.Name);
        description.Add("Damage", GetTargetDamage(owner));
        description.Add("Repeat", Repeats);
        return description;
    }
}

public sealed class ShadowDefendIntent(int veilBlock, int skills, int surgeBlock, int powers) : DefendIntent
{
    internal int GetBlock(Creature owner)
    {
        int Preview(int amount) => owner.CombatState is { } combat
            ? Math.Max(0, (int)Hook.ModifyBlock(combat, owner, amount, ValueProp.Move, null, null, out _)) : amount;
        return (skills > 0 ? Preview(veilBlock) * skills : 0) + (powers > 0 ? Preview(surgeBlock) * powers : 0);
    }

    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        var description = new LocString("intents", "SAKURA_MOD_SHADOW_DEFEND.description");
        description.Add("Amount", GetBlock(owner));
        return description;
    }
}

public sealed class ShadowHealIntent(int amount) : HealIntent
{
    internal int Amount => amount;

    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        var description = new LocString("intents", "SAKURA_MOD_SHADOW_HEAL.description");
        description.Add("Amount", amount);
        return description;
    }
}

public sealed class ShadowStrengthIntent(int amount) : BuffIntent
{
    internal int Amount => amount;

    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        var description = new LocString("intents", "SAKURA_MOD_SHADOW_STRENGTH.description");
        description.Add("Amount", amount);
        return description;
    }
}
