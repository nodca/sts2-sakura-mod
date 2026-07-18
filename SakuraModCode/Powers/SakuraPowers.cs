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
using SakuraMod.SakuraModCode.Classic.Cards;
using SakuraMod.SakuraModCode.Classic.Powers;
using SakuraMod.SakuraModCode.Extensions;
using STS2RitsuLib.Combat.HandSize;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace SakuraMod.SakuraModCode.Powers;

internal static class SakuraPowerValueProps
{
    public const ValueProp Block = ValueProp.Unpowered;
    public const ValueProp Damage = ValueProp.Unpowered;
    public const ValueProp HpLoss = ValueProp.Unblockable | ValueProp.Unpowered;
}

public class ReflectionPower : SakuraModPower
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

public class LucidGuardPower : SakuraModPower
{
    protected override string IconFileName => "lucid_guard.png";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyDamageCap(Creature? creature, ValueProp props, Creature? source, CardModel? card) =>
        creature == Owner && Amount > 0 && props.IsPoweredAttack()
            ? Math.Max(0, Amount)
            : base.ModifyDamageCap(creature, props, source, card);

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature creature, DamageResult damageResult, ValueProp damageProps, Creature? source, CardModel? card)
    {
        if (creature == Owner && damageResult.UnblockedDamage > 0 && Amount > 0 && damageProps.IsPoweredAttack())
            await PowerCmd.Remove(this);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner.Side == side && participants.Contains(Owner))
            await PowerCmd.Remove(this);
    }
}

public abstract class NextAttackPowerBase : SakuraModPower
{
    private CardModel? _activeAttack;

    protected bool IsActiveAttack(CardModel? card) =>
        Amount > 0 && card is not null && _activeAttack == card;

    public override Task BeforeCardPlayed(CardPlay play)
    {
        if (Amount > 0
            && _activeAttack is null
            && play.Card?.Owner?.Creature == Owner
            && play.Card.Type == CardType.Attack)
        {
            _activeAttack = play.Card;
            AfterActiveAttackSet(play.Card);
        }

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (_activeAttack == play.Card)
            await PowerCmd.Remove(this);
    }

    protected virtual void AfterActiveAttackSet(CardModel card)
    {
    }
}

public abstract class NextAttackThisTurnPowerBase : NextAttackPowerBase
{
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner.Side == side && participants.Contains(Owner))
            await PowerCmd.Remove(this);
    }
}

public class LucidPiercePower : NextAttackThisTurnPowerBase
{
    protected override string IconFileName => "lucid_pierce.png";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public static bool ShouldPierce(Creature? dealer, CardModel? cardSource) =>
        dealer is not null
        && cardSource is { Type: CardType.Attack }
        && cardSource.Owner?.Creature == dealer
        && dealer.GetPower<LucidPiercePower>()?.IsActiveFor(cardSource) == true;

    private bool IsActiveFor(CardModel card) =>
        IsActiveAttack(card);
}

public class MiragePower : SakuraModPower
{
    protected override string IconFileName => "mirage_image.png";

    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource) =>
        Amount > 0 && dealer == Owner && props.IsPoweredAttack()
            ? 0m
            : 1m;

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (Owner.Side == side && participants.Contains(Owner))
            await PowerCmd.Remove(this);
    }
}

public class LabyrinthPower : SakuraModPower
{
    private readonly HashSet<Creature> _enemies = [];
    private readonly List<CardPlay> _activeCardPlays = [];
    private int _playerTurnEndsUntilRelease = 1;
    private Creature? _pendingReleaseEnemy;

    protected override string IconFileName => "sleep_power.png";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    internal static bool AllowsCardInteraction(CardModel? card, bool isTrapped, bool isAlive) =>
        !isTrapped || !isAlive || card?.Type != CardType.Attack;

    public async Task Enter(IEnumerable<Creature> enemies)
    {
        _playerTurnEndsUntilRelease = 1;
        _pendingReleaseEnemy = null;
        foreach (var enemy in enemies.Where(enemy => enemy.IsMonster && enemy.IsAlive))
        {
            _enemies.Add(enemy);
            SakuraLabyrinthMove.Apply(enemy);
        }

        await RemoveIfEmpty();
    }

    public override Task BeforeCardPlayed(CardPlay play)
    {
        _activeCardPlays.Add(play);
        return Task.CompletedTask;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        _activeCardPlays.Remove(play);
        return Task.CompletedTask;
    }

    public override bool ShouldAllowHitting(Creature creature) =>
        AllowsCardInteraction(
            _activeCardPlays.LastOrDefault()?.Card,
            _enemies.Contains(creature),
            creature.IsAlive);

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource) =>
        AllowsCardInteraction(
            cardSource,
            target is not null && _enemies.Contains(target),
            target?.IsAlive == true)
            ? 1m
            : 0m;

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side != CombatSide.Enemy || Amount <= 0)
            return;

        CleanupDeadEnemies();
        foreach (var enemy in _enemies.ToList())
            SakuraLabyrinthMove.EnsureSuppressed(enemy);

        await RemoveIfEmpty();
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Amount <= 0 || player != Owner.Player)
            return;

        CleanupDeadEnemies();
        if (_playerTurnEndsUntilRelease == 0 && !IsTrapped(_pendingReleaseEnemy))
            _pendingReleaseEnemy = PickRandomEnemy();

        foreach (var enemy in _enemies.ToList())
            SakuraLabyrinthMove.EnsureSuppressed(enemy, revealCoveredIntent: enemy == _pendingReleaseEnemy);

        await RemoveIfEmpty();
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        CleanupDeadEnemies();
        if (side != Owner.Side || !participants.Contains(Owner))
        {
            await RemoveIfEmpty();
            return;
        }

        if (_playerTurnEndsUntilRelease > 0)
        {
            _playerTurnEndsUntilRelease--;
            await RemoveIfEmpty();
            return;
        }

        var enemy = IsTrapped(_pendingReleaseEnemy) ? _pendingReleaseEnemy : PickRandomEnemy();
        if (enemy is not null)
        {
            _enemies.Remove(enemy);
            SakuraLabyrinthMove.Restore(enemy);
        }
        _pendingReleaseEnemy = null;

        await RemoveIfEmpty();
    }

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (!wasRemovalPrevented && _enemies.Remove(creature))
        {
            if (_pendingReleaseEnemy == creature)
                _pendingReleaseEnemy = null;
            SakuraLabyrinthMove.Clear(creature);
        }

        await RemoveIfEmpty();
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        foreach (var enemy in _enemies.Where(enemy => enemy.CombatState?.ContainsCreature(enemy) == true))
            SakuraLabyrinthMove.Restore(enemy);
        _enemies.Clear();
        _activeCardPlays.Clear();
        _pendingReleaseEnemy = null;
        return Task.CompletedTask;
    }

    private Creature? PickRandomEnemy()
    {
        var combatState = Owner.CombatState;
        var candidates = _enemies
            .Where(enemy => enemy.IsAlive && combatState?.ContainsCreature(enemy) == true)
            .OrderBy(enemy => enemy.CombatId ?? uint.MaxValue)
            .ThenBy(enemy => enemy.SlotName, StringComparer.Ordinal)
            .ToList();
        return candidates.Count == 0
            ? null
            : Owner.Player?.RunState.Rng.CombatTargets.NextItem(candidates);
    }

    private void CleanupDeadEnemies()
    {
        var combatState = Owner.CombatState;
        foreach (var enemy in _enemies
                     .Where(enemy => !enemy.IsAlive || combatState?.ContainsCreature(enemy) != true)
                     .ToList())
        {
            _enemies.Remove(enemy);
            if (_pendingReleaseEnemy == enemy)
                _pendingReleaseEnemy = null;
            SakuraLabyrinthMove.Clear(enemy);
        }
    }

    private bool IsTrapped(Creature? enemy) =>
        enemy is not null
        && enemy.IsAlive
        && _enemies.Contains(enemy)
        && Owner.CombatState?.ContainsCreature(enemy) == true;

    private async Task RemoveIfEmpty()
    {
        if (_enemies.Count == 0 && Owner.GetPower<LabyrinthPower>() == this)
            await PowerCmd.Remove(this);
    }
}

public class SakuraFrostbitePower : SakuraModPower
{
    private const int FreezeThreshold = 6;

    protected override string IconFileName => "frostbite.png";

    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    internal static (int FreezeStacks, int RemainingFrostbite) ConvertToFreeze(int frostbite)
    {
        var amount = Math.Max(0, frostbite);
        return (amount / FreezeThreshold, amount % FreezeThreshold);
    }

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource) =>
        target == Owner && Amount > 0 && props.IsPoweredAttack()
            ? 1m + Amount / 10m
            : 1m;

    public override async Task AfterApplied(Creature? applier, CardModel? cardSource) =>
        await ResolveFreeze(new ThrowingPlayerChoiceContext(), applier, cardSource);

    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (power == this)
            await ResolveFreeze(choiceContext, applier, cardSource);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (!Owner.IsMonster || side != Owner.Side || !participants.Contains(Owner))
            return;

        if (Amount <= 1)
        {
            await PowerCmd.Remove(this);
            return;
        }

        await PowerCmd.Decrement(this);
    }

    private async Task ResolveFreeze(PlayerChoiceContext choiceContext, Creature? applier, CardModel? cardSource)
    {
        var (freezeStacks, remainingFrostbite) = ConvertToFreeze(Amount);
        if (freezeStacks <= 0)
            return;

        var freezeApplier = applier ?? Applier ?? Owner;
        if (remainingFrostbite == 0)
            await PowerCmd.Remove(this);
        else
            await PowerCmd.ModifyAmount(choiceContext, this, remainingFrostbite - Amount, freezeApplier, cardSource, false);

        await PowerCmd.Apply<ClassicFreezePower>(
            choiceContext,
            Owner,
            freezeStacks,
            freezeApplier,
            cardSource,
            false);
    }
}

public class SakuraTemporaryDexterityPower : TemporaryDexterityPower, IModPowerAssetOverrides
{
    public PowerAssetProfile AssetProfile => PowerAssetProfile.Empty;
    public string? CustomIconPath => ModelDb.Power<DexterityPower>().PackedIconPath;
    public string? CustomBigIconPath => ModelDb.Power<DexterityPower>().ResolvedBigIconPath;
    public override AbstractModel OriginModel => ModelDb.Card<Flight>();
}

public abstract class SakuraTrackedCostReductionPower : SakuraModPower
{
    private readonly HashSet<CardModel> _targets = [];

    protected override bool IsVisibleInternal => false;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public void AddTarget(CardModel card)
    {
        _targets.Add(card);
    }

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal currentCost, out decimal newCost)
    {
        if (_targets.Contains(card) && currentCost > 0)
        {
            newCost = Math.Max(0, currentCost - Math.Max(1, Amount));
            return true;
        }

        newCost = currentCost;
        return false;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (play.Card is not null)
            _targets.Remove(play.Card);

        await RemoveIfEmpty();
    }

    protected void PruneDetachedTargets()
    {
        _targets.RemoveWhere(card => card.Pile?.IsCombatPile != true);
    }

    protected async Task RemoveIfEmpty()
    {
        if (_targets.Count == 0)
            await PowerCmd.Remove(this);
    }
}

public class SakuraCostReductionPower : SakuraTrackedCostReductionPower
{
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner.Side == side && participants.Contains(Owner))
            await PowerCmd.Remove(this);
    }
}

public enum RecordResult
{
    Recorded,
    Restored
}

public class RecordPower : SakuraModPower
{
    private const string RecordedHpKey = "RecordedHp";
    private const string RecordedBlockKey = "RecordedBlock";

    protected override string IconFileName => "record.png";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override int DisplayAmount => RecordedHp;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(RecordedHpKey, 0),
        new DynamicVar(RecordedBlockKey, 0)
    ];

    private int RecordedHp => DynamicVars[RecordedHpKey].IntValue;
    private int RecordedBlock => DynamicVars[RecordedBlockKey].IntValue;

    public static async Task<RecordResult> RecordOrRestore(PlayerChoiceContext choiceContext, Creature owner, CardModel source)
    {
        if (owner.GetPower<RecordPower>() is { } record)
        {
            await record.RestoreRecordedValues();
            return RecordResult.Restored;
        }

        var power = await PowerCmd.Apply<RecordPower>(choiceContext, owner, 1, owner, source, false);
        power?.StoreCurrentValues(owner);
        return RecordResult.Recorded;
    }

    private void StoreCurrentValues(Creature creature)
    {
        DynamicVars[RecordedHpKey].BaseValue = Math.Max(0, creature.CurrentHp);
        DynamicVars[RecordedBlockKey].BaseValue = Math.Max(0, creature.Block);
        InvokeDisplayAmountChanged();
    }

    private async Task RestoreRecordedValues()
    {
        await SakuraCreatureState.RestoreHp(Owner, RecordedHp);
        SakuraCreatureState.RestoreBlock(Owner, RecordedBlock);
        await PowerCmd.Remove(this);
    }
}

public class SakuraExtraEffectCountThisTurnPower : SakuraModPower
{
    protected override string IconFileName => "extra_effect_count_this_turn.png";
    protected override bool IsVisibleInternal => false;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner.Side == side && participants.Contains(Owner))
            await PowerCmd.Remove(this);
    }
}

public class RepairRegenerationPower : SakuraModPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override bool TryModifyPowerAmountReceived(PowerModel canonicalPower, Creature target, decimal amount, Creature? applier, out decimal modifiedAmount)
    {
        if (target == Owner && canonicalPower is RegenPower && amount == -1 && applier is null)
        {
            modifiedAmount = 0;
            return true;
        }

        modifiedAmount = amount;
        return false;
    }
}


















public class PromiseManifestPower : SakuraModPower
{
    protected override string IconFileName => "promise_manifest.png";

    private bool _lostHp;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature creature, DamageResult damageResult, ValueProp damageProps, Creature? source, CardModel? card)
    {
        if (creature == Owner && damageResult.UnblockedDamage > 0)
            _lostHp = true;

        return Task.CompletedTask;
    }

    public override decimal ModifyHandDraw(Player player, decimal count) =>
        player.Creature == Owner && !_lostHp
            ? count + Amount
            : count;

    public override async Task AfterEnergyReset(Player player)
    {
        if (player.Creature == Owner && !_lostHp)
            await PlayerCmd.GainEnergy(Amount, player);
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner)
            return;

        await PowerCmd.Remove(this);
    }
}

public class SpiralNextTurnPower : SakuraModPower
{
    private readonly Queue<CardModel> _sources = [];

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public void QueueCopy(CardModel source) =>
        _sources.Enqueue(source);

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner)
            return;

        while (_sources.TryDequeue(out var source))
            await SakuraGeneratedCardLifecycle.AddTemporaryCopyToHand(source, false, choiceContext);

        await PowerCmd.Remove(this);
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        _sources.Clear();
        return Task.CompletedTask;
    }
}

public class DreamingPower : SakuraModPower
{
    protected override string IconFileName => "dreaming.png";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task BeforeHandDraw(
        Player player,
        PlayerChoiceContext choiceContext,
        ICombatState combatState)
    {
        if (player.Creature != Owner || Amount <= 0)
            return;

        for (var i = 0; i < Amount; i++)
        {
            var card = await SakuraActions.ChooseAndMoveTopDrawPileCard(player, choiceContext, lookCount: 5);
            if (card is null)
                return;

            await SakuraGeneratedCardLifecycle.GrantTemporary(choiceContext, card);
            card.SetToFreeThisTurn();
        }
    }
}

public class GravitationHoldPower : SakuraModPower
{
    private readonly HashSet<CardModel> _excludedSources = [];
    private readonly HashSet<CardModel> _pendingReturns = [];
    private readonly Dictionary<CardModel, int> _returnCounts = [];

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public void ExcludeSource(CardModel card) => _excludedSources.Add(card);

    public override (PileType, CardPilePosition) ModifyCardPlayResultPileTypeAndPosition(
        CardModel card,
        bool isAutoPlay,
        ResourceInfo resources,
        PileType pileType,
        CardPilePosition position)
    {
        if (Amount <= 0
            || card.Owner?.Creature != Owner
            || _excludedSources.Contains(card)
            || pileType != PileType.Discard)
            return (pileType, position);

        _pendingReturns.Add(card);
        return (PileType.Hand, CardPilePosition.Bottom);
    }

    public override Task AfterModifyingCardPlayResultPileOrPosition(
        CardModel card,
        PileType pileType,
        CardPilePosition position)
    {
        if (!_pendingReturns.Remove(card) || pileType != PileType.Hand)
            return Task.CompletedTask;

        _returnCounts[card] = _returnCounts.GetValueOrDefault(card) + 1;
        card.InvokeEnergyCostChanged();
        return Task.CompletedTask;
    }

    public override bool TryModifyEnergyCostInCombat(
        CardModel card,
        decimal currentCost,
        out decimal newCost) =>
        TryIncreaseReturnedCardCost(card, _returnCounts.GetValueOrDefault(card), currentCost, out newCost);

    internal static bool TryIncreaseReturnedCardCost(
        CardModel card,
        int returnCount,
        decimal currentCost,
        out decimal newCost)
    {
        if (card.EnergyCost.CostsX || currentCost < 0 || returnCount <= 0)
        {
            newCost = currentCost;
            return false;
        }

        newCost = currentCost + returnCount;
        return true;
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == Owner.Side && participants.Contains(Owner))
            await PowerCmd.Remove(this);
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        foreach (var card in _returnCounts.Keys)
            card.InvokeEnergyCostChanged();

        _excludedSources.Clear();
        _pendingReturns.Clear();
        _returnCounts.Clear();
        return Task.CompletedTask;
    }
}

public class KindnessPower : SakuraModPower
{
    private readonly Queue<bool> _pendingEffects = [];
    private readonly HashSet<CardModel> _zeroCostCards = [];
    private CardModel? _targetCard;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public void QueueEffect(bool extraEffect) =>
        _pendingEffects.Enqueue(extraEffect);

    public override (PileType, CardPilePosition) ModifyCardPlayResultPileTypeAndPosition(
        CardModel card,
        bool isAutoPlay,
        ResourceInfo resources,
        PileType pileType,
        CardPilePosition position)
    {
        if (Amount <= 0
            || _targetCard is not null
            || _pendingEffects.Count == 0
            || card.Owner?.Creature != Owner
            || !ClassicSakuraCardCatalog.CanBeTargetedByClearCardEffects(card)
            || pileType != PileType.Exhaust)
            return (pileType, position);

        _targetCard = card;
        if (_pendingEffects.Dequeue())
            _zeroCostCards.Add(card);

        return (PileType.Hand, CardPilePosition.Bottom);
    }

    public override Task AfterModifyingCardPlayResultPileOrPosition(
        CardModel card,
        PileType pileType,
        CardPilePosition position)
    {
        if (card == _targetCard && pileType != PileType.Hand)
            _zeroCostCards.Remove(card);

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (play.Card != _targetCard || play.PlayIndex < play.PlayCount - 1)
            return;

        var card = play.Card;
        if (_zeroCostCards.Remove(card))
        {
            card.EnergyCost.SetThisTurn(0, true);
            card.InvokeEnergyCostChanged();
        }

        _targetCard = null;
        await PowerCmd.Decrement(this);
    }
}

public class TimeStopPower : SakuraModPower
{
    private bool _preserveCurrentTurnState;
    private bool _extraTurnStarted;

    protected override string IconFileName => "time_stop.png";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public bool PreservesCurrentTurnState => _preserveCurrentTurnState;

    public void PreserveCurrentTurnState()
    {
        _preserveCurrentTurnState = true;
        PreserveElementStates();
    }

    public void PreserveElementStates() =>
        ClassicElementStatePower.PreserveAllForNextTurn(Owner);

    public override bool ShouldTakeExtraTurn(MegaCrit.Sts2.Core.Entities.Players.Player player) =>
        Amount > 0 && player.Creature == Owner;

    public override async Task AfterTakingExtraTurn(MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        if (player.Creature != Owner)
            return;

        if (_preserveCurrentTurnState)
            _extraTurnStarted = true;
        else
            await PowerCmd.Remove(this);
    }

    public override bool ShouldFlush(MegaCrit.Sts2.Core.Entities.Players.Player player) =>
        player != Owner.Player || !_preserveCurrentTurnState;

    public override bool ShouldClearBlock(Creature creature) =>
        !_preserveCurrentTurnState || creature != Owner;

    public override bool ShouldPlayerResetEnergy(MegaCrit.Sts2.Core.Entities.Players.Player player) =>
        player != Owner.Player || !_preserveCurrentTurnState;

    public override async Task AfterPlayerTurnStartLate(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        if (_extraTurnStarted && player.Creature == Owner)
            await PowerCmd.Remove(this);
    }
}


internal static class SakuraPowerTriggers
{
    public static bool IsOwnerClearCard(CardPlay play, Creature owner) =>
        play.Card is { } card
        && card.Owner?.Creature == owner
        && SakuraTransparentCardCatalog.IsTransparentCard(card);
}
