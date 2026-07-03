using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace SakuraMod.SakuraModCode;

internal static class SakuraSleepMove
{
    private const string SleepMoveId = "SAKURAMOD_SLEEP_MOVE";
    private static readonly ConditionalWeakTable<Creature, SleepMoveState> States = new();

    public static void Apply(Creature owner)
    {
        if (!CanControl(owner))
        {
            Clear(owner);
            return;
        }

        States.GetOrCreateValue(owner).Apply(owner);
    }

    public static bool WasConsumedBy(Creature owner) =>
        States.TryGetValue(owner, out var state) && state.WasConsumedBy(owner);

    public static void Renew(Creature owner)
    {
        if (!CanControl(owner))
        {
            Clear(owner);
            return;
        }

        States.GetOrCreateValue(owner).Renew(owner);
    }

    public static void RestoreIfUnconsumed(Creature owner)
    {
        if (States.TryGetValue(owner, out var state))
            state.RestoreIfUnconsumed(owner);

        Clear(owner);
    }

    private static bool CanControl(Creature owner) =>
        owner.IsMonster && owner.IsAlive && owner.Monster is not null;

    private static void Clear(Creature owner) =>
        States.Remove(owner);

    private sealed class SleepMoveState
    {
        private MoveState? _coveredMove;
        private MoveState? _sleepMove;
        private bool _sleepMovePerformed;

        public void Apply(Creature owner)
        {
            if (IsCurrentSleepMove(owner))
                return;

            var currentMove = owner.Monster!.NextMove;
            if (currentMove.Id == SleepMoveId)
                return;

            _coveredMove = currentMove;
            SetSleepMove(owner);
        }

        public bool WasConsumedBy(Creature owner) =>
            IsCurrentSleepMove(owner) && _sleepMovePerformed;

        public void Renew(Creature owner)
        {
            if (_coveredMove is null)
            {
                Apply(owner);
                return;
            }

            SetSleepMove(owner);
        }

        public void RestoreIfUnconsumed(Creature owner)
        {
            if (!CanControl(owner)
                || _coveredMove is null
                || !IsCurrentSleepMove(owner)
                || _sleepMovePerformed)
                return;

            owner.Monster!.SetMoveImmediate(_coveredMove, forceTransition: true);
        }

        private void SetSleepMove(Creature owner)
        {
            if (_coveredMove is null)
                return;

            _sleepMovePerformed = false;
            var followUpStateId = FollowUpStateId(owner, _coveredMove);
            if (followUpStateId is null)
                return;

            _sleepMove = new MoveState(SleepMoveId, SleepMove, new SleepIntent())
            {
                FollowUpStateId = followUpStateId,
                MustPerformOnceBeforeTransitioning = true
            };
            owner.Monster!.SetMoveImmediate(_sleepMove, forceTransition: true);
        }

        private static string? FollowUpStateId(Creature owner, MoveState coveredMove)
        {
            var stateMachine = owner.Monster!.MoveStateMachine;
            if (stateMachine is null)
                return null;

            if (stateMachine.States.ContainsKey(coveredMove.Id))
                return coveredMove.Id;

            return stateMachine.StateLog
                .LastOrDefault(state => stateMachine.States.ContainsKey(state.Id))
                ?.Id;
        }

        private Task SleepMove(IReadOnlyList<Creature> targets)
        {
            _sleepMovePerformed = true;
            return Task.CompletedTask;
        }

        private bool IsCurrentSleepMove(Creature owner) =>
            owner.Monster?.NextMove == _sleepMove;
    }
}
