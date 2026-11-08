using System.Reflection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Singleton;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace SakuraMod.SakuraModCode.Extensions;

public static class AttackCommandExtensions
{
    private static readonly MethodInfo DamagePropsSetter =
        typeof(AttackCommand).GetProperty(nameof(AttackCommand.DamageProps))
            ?.GetSetMethod(nonPublic: true)
        ?? throw new MissingMemberException(nameof(AttackCommand), nameof(AttackCommand.DamageProps));

    private static readonly FieldInfo CombatStateField =
        typeof(AttackCommand).GetField("_combatState", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingMemberException(nameof(AttackCommand), "_combatState");

    public static AttackCommand WithValueProp(this AttackCommand command, ValueProp props)
    {
        DamagePropsSetter.Invoke(command, [props]);
        return command;
    }

    public static AttackCommand TargetingFiltered(this AttackCommand command, IEnumerable<Creature> targets)
    {
        var targetList = targets.Where(static target => target.IsAlive).ToList();
        var attacker = command.Attacker
            ?? throw new InvalidOperationException("AttackCommand.TargetingFiltered requires an attacker in combat.");
        var combatState = attacker.CombatState
            ?? throw new InvalidOperationException("AttackCommand.TargetingFiltered requires an attacker in combat.");

        // FromMonster claims TargetingAllOpponents for itself and the builder
        // rejects any second Targeting* call, so the filter can only swap the
        // resolved combat state in place.
        CombatStateField.SetValue(command, new FilteredCombatState(combatState, attacker, targetList));
        return command;
    }

    private sealed class FilteredCombatState(
        ICombatState inner,
        Creature attacker,
        IReadOnlyList<Creature> targets) : ICombatState
    {
        public IRunState RunState => inner.RunState;
        public IReadOnlyList<Creature> Allies => inner.Allies;
        public IReadOnlyList<Creature> Enemies => inner.Enemies;
        public IReadOnlyList<Creature> Creatures => inner.Creatures;
        // Monster-source attacks resolve their possible targets through
        // PlayerCreatures, so this is where the filter must bite.
        public IReadOnlyList<Creature> PlayerCreatures => targets;
        public IReadOnlyList<Player> Players => inner.Players;
        public IReadOnlyList<ModifierModel> Modifiers => inner.Modifiers;
        public MultiplayerScalingModel? MultiplayerScalingModel => inner.MultiplayerScalingModel;
        public int RoundNumber { get => inner.RoundNumber; set => inner.RoundNumber = value; }
        public CombatSide CurrentSide { get => inner.CurrentSide; set => inner.CurrentSide = value; }
        public EncounterModel? Encounter => inner.Encounter;
        public IReadOnlyList<Creature> EscapedCreatures => inner.EscapedCreatures;
        public IReadOnlyList<Creature> CreaturesOnCurrentSide => inner.CreaturesOnCurrentSide;
        public IReadOnlyList<Creature> HittableEnemies => targets;

        public event Action<ICombatState>? CreaturesChanged
        {
            add => inner.CreaturesChanged += value;
            remove => inner.CreaturesChanged -= value;
        }

        public T CreateCard<T>(Player owner) where T : CardModel => inner.CreateCard<T>(owner);
        public CardModel CreateCard(CardModel canonicalCard, Player owner) => inner.CreateCard(canonicalCard, owner);
        public CardModel CloneCard(CardModel mutableCard) => inner.CloneCard(mutableCard);
        public void AddCard(CardModel card, Player owner) => inner.AddCard(card, owner);
        public void RemoveCard(CardModel card) => inner.RemoveCard(card);
        public bool ContainsCard(CardModel card) => inner.ContainsCard(card);
        public void AddPlayer(Player player) => inner.AddPlayer(player);
        public Creature CreateCreature(MonsterModel monster, CombatSide side, string? slot) => inner.CreateCreature(monster, side, slot);
        public void CreatureEscaped(Creature creature) => inner.CreatureEscaped(creature);
        public void RemoveCreature(Creature creature, bool unattach = true) => inner.RemoveCreature(creature, unattach);
        public bool ContainsCreature(Creature creature) => inner.ContainsCreature(creature);
        public bool ContainsMonster<T>() where T : MonsterModel => inner.ContainsMonster<T>();
        public Creature? GetCreature(uint? combatId) => inner.GetCreature(combatId);
        public Task<Creature?> GetCreatureAsync(uint? combatId, double timeoutSec) => inner.GetCreatureAsync(combatId, timeoutSec);
        public IReadOnlyList<Creature> GetCreaturesOnSide(CombatSide side) => inner.GetCreaturesOnSide(side);
        public IReadOnlyList<Creature> GetOpponentsOf(Creature creature) => ReferenceEquals(creature, attacker) ? targets : inner.GetOpponentsOf(creature);
        public IReadOnlyList<Creature> GetTeammatesOf(Creature creature) => inner.GetTeammatesOf(creature);
        public Player? GetPlayer(ulong playerId) => inner.GetPlayer(playerId);
        public IEnumerable<AbstractModel> IterateHookListeners() => inner.IterateHookListeners();
        public void SortEnemiesBySlotName() => inner.SortEnemiesBySlotName();
        public void SetEnemyIndex(Creature creature, int index) => inner.SetEnemyIndex(creature, index);
        public void AddCreature(Creature creature) => inner.AddCreature(creature);
        public bool IsLiveCombat() => inner.IsLiveCombat();
    }
}
