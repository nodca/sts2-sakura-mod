using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace SakuraMod.SakuraModCode;

internal static class SakuraLabyrinthMove
{
    private const string LabyrinthMoveId = "SAKURAMOD_LABYRINTH_MOVE";
    private static readonly ConditionalWeakTable<Creature, LabyrinthMoveState> States = new();

    public static void Apply(Creature owner)
    {
        if (!CanControl(owner))
        {
            Clear(owner);
            return;
        }

        States.GetOrCreateValue(owner).Apply(owner);
    }

    public static void EnsureSuppressed(Creature owner, bool revealCoveredIntent = false)
    {
        if (!CanControl(owner))
        {
            Clear(owner);
            return;
        }

        States.GetOrCreateValue(owner).EnsureSuppressed(owner, revealCoveredIntent);
    }

    public static void Restore(Creature owner)
    {
        if (States.TryGetValue(owner, out var state))
            state.Restore(owner);

        Clear(owner);
    }

    public static void Clear(Creature owner) => States.Remove(owner);

    private static bool CanControl(Creature owner) =>
        owner.IsMonster && owner.IsAlive && owner.Monster is not null && owner.CombatState is not null;

    private sealed class LabyrinthMoveState
    {
        private MoveState? _coveredMove;
        private MoveState? _labyrinthMove;
        private bool _phaseChanged;

        public void Apply(Creature owner)
        {
            if (IsCurrentLabyrinthMove(owner))
                return;

            var currentMove = owner.Monster!.NextMove;
            if (_coveredMove is null)
            {
                _coveredMove = currentMove;
            }
            else if (currentMove != _coveredMove)
            {
                _phaseChanged = true;
                _coveredMove = currentMove;
            }

            SetLabyrinthMove(owner);
        }

        public void EnsureSuppressed(Creature owner, bool revealCoveredIntent)
        {
            if (_coveredMove is null)
            {
                Apply(owner);
                return;
            }

            var currentMove = owner.Monster!.NextMove;
            if (currentMove != _coveredMove && !IsCurrentLabyrinthMove(owner))
            {
                _phaseChanged = true;
                _coveredMove = currentMove;
            }

            SetLabyrinthMove(owner, revealCoveredIntent);
        }

        public void Restore(Creature owner)
        {
            if (!CanControl(owner) || _coveredMove is null)
                return;

            var currentMove = owner.Monster!.NextMove;
            if (!_phaseChanged && currentMove != _coveredMove && !IsCurrentLabyrinthMove(owner))
            {
                _phaseChanged = true;
                _coveredMove = currentMove;
            }

            owner.Monster!.SetMoveImmediate(_coveredMove, forceTransition: true);
            if (_phaseChanged)
                owner.PrepareForNextTurn(owner.CombatState!.GetOpponentsOf(owner));
        }

        private void SetLabyrinthMove(Creature owner, bool revealCoveredIntent = false)
        {
            if (_coveredMove is null)
                return;

            var followUpStateId = FollowUpStateId(owner, _coveredMove);
            if (followUpStateId is null)
                return;

            var labyrinthIntent = revealCoveredIntent
                ? new LabyrinthReleaseWarningIntent()
                : new LabyrinthIntent();
            var intents = revealCoveredIntent
                ? new AbstractIntent[] { labyrinthIntent }.Concat(_coveredMove.Intents).ToArray()
                : [labyrinthIntent];

            _labyrinthMove = new MoveState(LabyrinthMoveId, SkipMove, intents)
            {
                FollowUpStateId = followUpStateId,
                MustPerformOnceBeforeTransitioning = true
            };
            owner.Monster!.SetMoveImmediate(_labyrinthMove, forceTransition: true);
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

        private static Task SkipMove(IReadOnlyList<Creature> targets) => Task.CompletedTask;

        private bool IsCurrentLabyrinthMove(Creature owner) =>
            owner.Monster?.NextMove == _labyrinthMove;
    }
}
