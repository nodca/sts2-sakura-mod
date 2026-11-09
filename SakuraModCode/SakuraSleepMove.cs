using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace SakuraMod.SakuraModCode;

internal static class SakuraSleepMove
{
    public static void Apply(Creature owner)
    {
        if (!owner.IsAlive || owner.Monster is not { MoveStateMachine: { } machine } monster)
            return;

        var pending = monster.NextMove;
        if (!pending.CanTransitionAway || IsStun(pending) || !machine.States.ContainsKey(pending.Id))
            return;

        // Leave the sleep move replaceable by native mid-turn actions such as a break stun.
        monster.SetMoveImmediate(new SleepMove { FollowUpState = pending });
    }

    public static bool SkippedActionThisTurn(Creature owner) =>
        CombatManager.Instance.History.Entries.OfType<MonsterPerformedMoveEntry>().Any(entry =>
            entry.Actor == owner && (entry.Move is SleepMove || IsStun(entry.Move))
            && entry.HappenedThisTurn(owner.CombatState));

    public static void RestoreIfUnconsumed(Creature owner)
    {
        if (!owner.IsAlive || owner.Monster is not { NextMove: SleepMove sleep } monster)
            return;

        if (sleep.FollowUpState is MoveState covered
            && !CombatManager.Instance.History.Entries.OfType<MonsterPerformedMoveEntry>()
                .Any(entry => entry.Move == sleep))
            monster.SetMoveImmediate(covered);
    }

    private static bool IsStun(MoveState move) => move.Intents is [StunIntent];

    private sealed class SleepMove()
        : MoveState("SAKURAMOD_SLEEP_MOVE", static _ => Task.CompletedTask, new SleepIntent());
}
