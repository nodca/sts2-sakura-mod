using BaseLib.Abstracts;
using BaseLib.Hooks;
using BaseLib.Utils;
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

namespace SakuraMod.SakuraModCode.Powers;

public abstract class ReflectionPowerBase : SakuraModPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected abstract int CalculateReflectionDamage(int attackDamage);

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature creature, DamageResult damageResult, ValueProp damageProps, Creature? source, CardModel? card)
    {
        if (Amount <= 0
            || creature != Owner
            || source is not { IsAlive: true } attacker
            || attacker.Side == Owner.Side
            || !damageProps.IsPoweredAttack()
            || damageResult.TotalDamage <= 0)
            return;

        var reflectionDamage = CalculateReflectionDamage(damageResult.TotalDamage);
        if (reflectionDamage > 0)
            await CreatureCmd.Damage(choiceContext, attacker, reflectionDamage, ValueProp.Unblockable, Owner, null);
        await PowerCmd.Decrement(this);
    }
}

public class ReflectionPower : ReflectionPowerBase
{
    protected override string IconFileName => "reflection.png";

    protected override int CalculateReflectionDamage(int attackDamage) => (attackDamage + 1) / 2;
}

public class StrongReflectionPower : ReflectionPowerBase
{
    protected override string IconFileName => "reflection_strong.png";

    protected override int CalculateReflectionDamage(int attackDamage) => attackDamage;
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

public class MirageImagePower : SakuraModPower
{
    protected override string IconFileName => "mirage_image.png";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private bool IsIncomingAttack(ValueProp props, Creature? dealer) =>
        Amount > 0
        && dealer is not null
        && dealer.Side != Owner.Side
        && props.IsPoweredAttack();

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource) =>
        target == Owner && amount > 0 && IsIncomingAttack(props, dealer)
            ? -Math.Min(amount, Amount)
            : 0;

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature creature, DamageResult damageResult, ValueProp damageProps, Creature? source, CardModel? card)
    {
        if (creature == Owner && IsIncomingAttack(damageProps, source))
            await PowerCmd.Remove(this);
    }
}

public class SakuraFrostbitePower : SakuraModPower
{
    private const int MinimumAmount = 10;

    protected override string IconFileName => "frostbite.png";

    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource) =>
        target == Owner && Amount > 0 && props.IsPoweredAttack()
            ? 1m + Amount / 100m
            : 1m;

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != Owner.Side || !participants.Contains(Owner))
            return;

        var nextAmount = Amount / 2;
        if (nextAmount < MinimumAmount)
        {
            await PowerCmd.Remove(this);
            return;
        }

        await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), this, nextAmount - Amount, Applier, null, false);
    }
}

public class SakuraBurnPower : SakuraModPower
{
    protected override string IconFileName => "burn.png";

    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature creature,
        DamageResult damageResult,
        ValueProp damageProps,
        Creature? source,
        CardModel? card)
    {
        if (creature != Owner
            || Amount <= 0
            || damageResult.TotalDamage <= 0
            || !damageProps.IsPoweredAttack()
            || card is null
            || !SakuraActions.ElementSetOf(card).HasElement(SakuraElement.Fire))
            return;

        await CreatureCmd.Damage(choiceContext, Owner, Amount, ValueProp.Unblockable, source, null);
    }
}

public class SakuraSleepPower : SakuraModPower
{
    private const int MaxAmount = 2;

    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool TryModifyPowerAmountReceived(PowerModel canonicalPower, Creature target, decimal amount, Creature? applier, out decimal modifiedAmount)
    {
        if (target == Owner && canonicalPower is SakuraSleepPower && amount > 0)
        {
            modifiedAmount = Math.Max(0m, Math.Min(amount, MaxAmount - Amount));
            return true;
        }

        modifiedAmount = amount;
        return false;
    }

    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (Amount <= 0 || side != Owner.Side || !participants.Contains(Owner) || !Owner.IsAlive)
            return;

        await CreatureCmd.Stun(Owner);
        await PowerCmd.Decrement(this);
    }

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature creature, DamageResult damageResult, ValueProp damageProps, Creature? source, CardModel? card)
    {
        if (creature == Owner && damageResult.TotalDamage > 0 && damageProps.IsPoweredAttack())
            await PowerCmd.Remove(this);
    }
}

public abstract class ElementPlayedPowerBase : SakuraModPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    // Per-turn element counts are shown on the element compass HUD, so keep these powers off the
    // character status bar to avoid duplicating the display.
    protected override bool IsVisibleInternal => false;

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == Owner.Side && participants.Contains(Owner))
            await PowerCmd.Remove(this);
    }
}

public class WindElementPower : ElementPlayedPowerBase
{
    protected override string IconFileName => "wind_element.png";
}

public class WaterElementPower : ElementPlayedPowerBase
{
    protected override string IconFileName => "water_element.png";
}

public class FireElementPower : ElementPlayedPowerBase
{
    protected override string IconFileName => "fire_element.png";
}

public class EarthElementPower : ElementPlayedPowerBase
{
    protected override string IconFileName => "earth_element.png";
}

public class SakuraTemporaryStrengthPower : TemporaryStrengthPower, ICustomPower
{
    public string? CustomPackedIconPath => ModelDb.Power<StrengthPower>().PackedIconPath;
    public string? CustomBigIconPath => ModelDb.Power<StrengthPower>().ResolvedBigIconPath;
    public string? CustomBigBetaIconPath => ModelDb.Power<StrengthPower>().ResolvedBigIconPath;
    public override AbstractModel OriginModel => ModelDb.Card<Gravitation>();
    protected override bool IsVisibleInternal => false;
    protected override bool IsPositive => false;
}

public class SakuraTemporaryDexterityPower : TemporaryDexterityPower, ICustomPower
{
    public string? CustomPackedIconPath => ModelDb.Power<DexterityPower>().PackedIconPath;
    public string? CustomBigIconPath => ModelDb.Power<DexterityPower>().ResolvedBigIconPath;
    public string? CustomBigBetaIconPath => ModelDb.Power<DexterityPower>().ResolvedBigIconPath;
    public override AbstractModel OriginModel => ModelDb.Card<Flight>();
}

public class DreamCostumePower : SakuraModPower
{
    private bool _usedThisTurn;

    protected override string IconFileName => "dream_costume.png";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal currentCost, out decimal newCost)
    {
        if (!_usedThisTurn && card.IsTemporary() && currentCost > 0)
        {
            newCost = currentCost - 1;
            return true;
        }

        newCost = currentCost;
        return false;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (play.Card?.IsTemporary() == true)
            _usedThisTurn = true;

        return Task.CompletedTask;
    }

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        if (player.Creature == Owner)
            _usedThisTurn = false;

        return Task.CompletedTask;
    }
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

public class SakuraCostReductionUntilPlayedPower : SakuraTrackedCostReductionPower
{
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner.Side != side || !participants.Contains(Owner))
            return;

        PruneDetachedTargets();
        await RemoveIfEmpty();
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

public class SakuraCatalogPower : SakuraModPower
{
    public const int PresenceAmount = 1;

    protected override string IconFileName => "catalog.png";

    private const string CatalogKey = "Catalog";
    private const string TotalKey = "Total";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override int DisplayAmount => CatalogCount;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(CatalogKey, 0),
        new DynamicVar(TotalKey, SakuraCardCatalog.TransparentCardTypes.Count)
    ];

    public void SetCatalogCount(int count)
    {
        var catalogCount = Math.Max(0, count);
        DynamicVars[CatalogKey].BaseValue = catalogCount;
        SetAmount(PresenceAmount + catalogCount, true);
        InvokeDisplayAmountChanged();
    }

    private int CatalogCount => Math.Max(0, Amount - PresenceAmount);
}

public class SakuraManifestedThisTurnPower : SakuraModPower
{
    protected override string IconFileName => "manifested_this_turn.png";
    protected override bool IsVisibleInternal => false;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner.Side == side && participants.Contains(Owner))
            await PowerCmd.Remove(this);
    }
}

public class SakuraReleaseCountThisTurnPower : SakuraModPower
{
    private readonly HashSet<CardModel> _countedCards = [];

    protected override string IconFileName => "release_count_this_turn.png";
    protected override bool IsVisibleInternal => false;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public bool TryMarkCounted(CardModel card) =>
        _countedCards.Add(card);

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

public class BlessingOfTheNamelessBookPower : SakuraModPower
{
    private const int RandomReleaseMode = 1;
    private const int ChooseReleaseMode = 2;
    private bool _usedChainThisTurn;

    protected override string IconFileName => "nameless_book_blessing.png";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public static int Mode(bool chooseRelease) =>
        chooseRelease ? ChooseReleaseMode : RandomReleaseMode;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        if (player.Creature != Owner)
            return;

        _usedChainThisTurn = false;
        await GrantReleaseThisTurn(choiceContext);
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (_usedChainThisTurn
            || play.Card?.Owner?.Creature != Owner
            || play.Card.IsReleased() != true)
            return;

        _usedChainThisTurn = true;
        await GrantReleaseThisTurn(choiceContext);
    }

    private async Task GrantReleaseThisTurn(PlayerChoiceContext choiceContext)
    {
        if (Owner.Player is null)
            return;

        var card = await SakuraActions.SelectOrRandomUnreleasedClearCardInHand(
            choiceContext,
            Owner.Player,
            choose: Amount >= ChooseReleaseMode);
        if (card is not null)
            await SakuraActions.ReleaseThisTurnAndRecord(choiceContext, card);
    }
}

public class ClockCountryAlicePower : SakuraModPower
{
    private const int BasicReadingMode = 1;
    private const int UpgradedReadingMode = 2;

    private int _storedDamage;
    private int _storedBlock;
    private bool _readingQueued;
    private bool _readingGenerated;

    protected override string IconFileName => "clock_country_alice.png";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public bool CreatesUpgradedReading => Amount >= UpgradedReadingMode;
    public int StoredDamage => _storedDamage;
    public int StoredBlock => _storedBlock;

    public static int ReadingMode(bool upgradedReading) =>
        upgradedReading ? UpgradedReadingMode : BasicReadingMode;

    public override Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (dealer == Owner && IsRecordedTemporarySource(cardSource) && result.TotalDamage > 0)
            AddStoredValues(result.TotalDamage, 0);

        return Task.CompletedTask;
    }

    public override Task AfterBlockGained(Creature creature, decimal amount, ValueProp props, CardModel? cardSource)
    {
        if (creature == Owner && IsRecordedTemporarySource(cardSource) && amount > 0)
            AddStoredValues(0, (int)amount);

        return Task.CompletedTask;
    }

    public async Task ReleaseStoredValues(PlayerChoiceContext choiceContext, SakuraModCard source, Creature target, CardPlay play)
    {
        if (_storedDamage > 0)
            await SakuraActions.Attack(choiceContext, source, target, _storedDamage);
        if (_storedBlock > 0)
            await CreatureCmd.GainBlock(Owner, _storedBlock, ValueProp.Move, play, false);
        _readingQueued = false;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        if (player.Creature != Owner || !_readingQueued || (_storedDamage <= 0 && _storedBlock <= 0))
            return;

        _readingQueued = false;
        _readingGenerated = true;
        var reading = Owner.CombatState?.CreateCard(ModelDb.Card<AliceReading>(), player) as AliceReading
            ?? throw new InvalidOperationException("Cannot create Alice Reading outside combat.");
        if (CreatesUpgradedReading && reading.IsUpgradable)
            reading.UpgradeInternal();
        SyncReadingValues(reading);

        await SakuraManifestLoop.AddGeneratedCardToHand(reading, choiceContext);
    }

    private void AddStoredValues(int damage, int block)
    {
        _storedDamage += Math.Max(0, damage);
        _storedBlock += Math.Max(0, block);
        if (!_readingGenerated)
            _readingQueued = _storedDamage > 0 || _storedBlock > 0;
        SyncReadingValuesInCombat();
    }

    private void SyncReadingValuesInCombat()
    {
        if (Owner.Player is not { } player)
            return;

        foreach (var pileType in CombatPileTypes)
        {
            var pile = CardPile.Get(pileType, player);
            if (pile is null)
                continue;

            foreach (var reading in pile.Cards.OfType<AliceReading>())
                SyncReadingValues(reading);
        }
    }

    private void SyncReadingValues(AliceReading reading)
    {
        reading.SetStoredValues(_storedDamage, _storedBlock);
    }

    private static bool IsRecordedTemporarySource(CardModel? card) =>
        card is not null && card is not AliceReading && card.IsTemporary();

    private static readonly PileType[] CombatPileTypes =
    [
        PileType.Hand,
        PileType.Play,
        PileType.Discard,
        PileType.Draw,
        PileType.Exhaust
    ];
}

public class FalseDailyLifePower : SakuraModPower
{
    public const int DamageAmount = 6;
    public const int BlockAmount = 2;

    protected override string IconFileName => "false_daily_life.png";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public int RemovedTemporaryBlock => Amount >= DamageAmount + 2 ? BlockAmount + 1 : BlockAmount;

    public async Task AfterTemporaryGranted(PlayerChoiceContext choiceContext)
    {
        if (Owner.Player is null || Owner.CombatState is null)
            return;

        var targets = Owner.CombatState.HittableEnemies
            .Where(enemy => enemy.IsAlive)
            .ToList();
        var target = Owner.Player.RunState.Rng.CombatCardSelection.NextItem(targets);
        if (target is not null)
            await CreatureCmd.Damage(choiceContext, target, Amount, ValueProp.Move, Owner, null);
    }

    public async Task AfterTemporaryRemoved(PlayerChoiceContext choiceContext)
    {
        await CreatureCmd.GainBlock(Owner, RemovedTemporaryBlock, ValueProp.Move, null, false);
    }
}

public class GrowingMagicPower : SakuraModPower
{
    protected override string IconFileName => "growing_magic.png";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public async Task AfterTemporaryStabilized(PlayerChoiceContext choiceContext)
    {
        if (Amount <= 0)
            return;

        await PowerCmd.Apply<StrengthPower>(choiceContext, Owner, Amount, Owner, null, false);
        if (Owner.Player is not null)
            await CardPileCmd.Draw(choiceContext, 1, Owner.Player, false);
    }
}

public class DreamsEndPower : SakuraModPower
{
    private int _nextTurnDraws;

    protected override string IconFileName => "dreams_end.png";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];

    public Task AfterTemporaryRemoved(PlayerChoiceContext choiceContext)
    {
        if (Owner.Player is not null)
            _nextTurnDraws += DynamicVars.Cards.IntValue;
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner || _nextTurnDraws <= 0)
            return;

        var draws = _nextTurnDraws;
        _nextTurnDraws = 0;
        await CardPileCmd.Draw(choiceContext, draws, player, false);
    }
}

public class MagicSurgePower : SakuraModPower
{
    private bool _usedThisTurn;

    protected override string IconFileName => "magic_surge.png";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public int ConsumeManifestBonus()
    {
        if (_usedThisTurn || Amount <= 0)
            return 0;

        _usedThisTurn = true;
        return Amount;
    }

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature == Owner)
            _usedThisTurn = false;

        return Task.CompletedTask;
    }

    public override Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == Owner.Side && participants.Contains(Owner))
            _usedThisTurn = false;

        return Task.CompletedTask;
    }
}

public class KeroBondPower : SakuraModPower, IMaxHandSizeModifier
{
    protected override string IconFileName => "kero_bond.png";

    private bool _usedThisTurn;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public int ModifyMaxHandSize(Player player, int currentMaxHandSize) =>
        player.Creature == Owner ? currentMaxHandSize + Math.Max(0, Amount) : currentMaxHandSize;

    public int ModifyMaxHandSizeLate(Player player, int currentMaxHandSize) =>
        currentMaxHandSize;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (_usedThisTurn || Amount <= 0 || !SakuraPowerTriggers.IsOwnerClearCard(play, Owner))
            return;

        _usedThisTurn = true;
        await CardPileCmd.Draw(choiceContext, Amount, Owner.Player!, false);
    }

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature == Owner)
            _usedThisTurn = false;

        return Task.CompletedTask;
    }
}

public class TomoyoDesignPower : SakuraModPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task BeforeCardPlayed(CardPlay play)
    {
        var card = play.Card;
        if (Amount <= 0
            || card?.Owner?.Creature != Owner
            || !SakuraCardCatalog.IsTransparentCard(card)
            || card.IsReleased())
            return;

        await SakuraActions.ReleaseAndRecord(new ThrowingPlayerChoiceContext(), card);
        await PowerCmd.Decrement(this);
    }
}

public class TomoyoBondPower : SakuraModPower
{
    protected override string IconFileName => "tomoyo_bond.png";

    private const int ClearCardsPerEnergy = 2;
    private int _clearCardsPlayed;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (Amount <= 0 || !SakuraPowerTriggers.IsOwnerClearCard(play, Owner))
            return;

        _clearCardsPlayed++;
        while (_clearCardsPlayed >= ClearCardsPerEnergy)
        {
            _clearCardsPlayed -= ClearCardsPerEnergy;
            await PlayerCmd.GainEnergy(Amount, Owner.Player!);
        }
    }
}

public class SyaoranBondPower : SakuraModPower
{
    protected override string IconFileName => "syaoran_bond.png";

    public const int WindDraw = 1;
    public const int WaterDamage = 3;
    public const int FireDamage = 6;
    public const int EarthBlock = 5;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override Task BeforeCardPlayed(CardPlay play)
    {
        var card = play.Card;
        if (Amount <= 0
            || card?.Owner?.Creature != Owner
            || !SakuraCardCatalog.IsTransparentCard(card)
            || !SakuraActions.TryRandomMissingElement(card.Owner, card, out var element))
            return Task.CompletedTask;

        SakuraActions.GrantElementsThisTurn(card, element.ToSet());
        return Task.CompletedTask;
    }

}

public class TalismanComboPower : SakuraModPower
{
    private SakuraElement? _previousElement;
    private CardPlay? _ignoredPlay;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public void StartAfter(CardPlay play) =>
        _ignoredPlay = play;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (ReferenceEquals(play, _ignoredPlay))
        {
            _ignoredPlay = null;
            return;
        }

        var card = play.Card;
        if (Amount <= 0 || card?.Owner?.Creature != Owner)
            return;

        foreach (var element in SakuraActions.ElementSetOf(card).AsElements())
            await ResolveElement(choiceContext, card.Owner, play, element);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == Owner.Side && participants.Contains(Owner))
            await PowerCmd.Remove(this);
    }

    private async Task ResolveElement(PlayerChoiceContext choiceContext, Player owner, CardPlay play, SakuraElement element)
    {
        if (_previousElement is not { } previous)
        {
            _previousElement = element;
            return;
        }

        if (previous == element)
        {
            await CardPileCmd.Draw(choiceContext, 1, owner, false);
            return;
        }

        await SakuraActions.TriggerTalismanEffect(choiceContext, owner, previous, play, null);
        _previousElement = element;
    }
}

public class SyaoranTalismanPower : SakuraModPower
{
    private CardPlay? _ignoredPlay;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public void Ignore(CardPlay play) =>
        _ignoredPlay = play;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (ReferenceEquals(play, _ignoredPlay))
        {
            _ignoredPlay = null;
            return;
        }

        var card = play.Card;
        if (Amount <= 0 || card?.Owner?.Creature != Owner)
            return;

        foreach (var element in SakuraActions.ElementSetOf(card).AsElements())
            await SakuraActions.TriggerTalismanEffect(choiceContext, card.Owner, element, play, null);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == Owner.Side && participants.Contains(Owner))
            await PowerCmd.Remove(this);
    }
}

public class SakuraBlockNextTurnPower : SakuraModPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        if (player.Creature != Owner)
            return;

        await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Move, null, false);
        await PowerCmd.Remove(this);
    }
}

public class SakuraDrawNextTurnPower : SakuraModPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        if (player.Creature != Owner)
            return;

        await CardPileCmd.Draw(choiceContext, Amount, player, false);
        await PowerCmd.Remove(this);
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

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        if (player.Creature != Owner)
            return;

        if (!_lostHp)
            await SakuraManifestLoop.Manifest(player, choiceContext, Amount);

        await PowerCmd.Remove(this);
    }
}

public class DreamingPower : SakuraModPower
{
    protected override string IconFileName => "dreaming.png";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        if (player.Creature == Owner)
        {
            var source = CardPile.GetCards(player, PileType.Draw, PileType.Discard, PileType.Exhaust, PileType.Hand)
                .OfType<SakuraModCard>()
                .FirstOrDefault();
            if (source is not null)
                await SakuraManifestLoop.Manifest(source, choiceContext, Math.Max(1, Amount));
        }
    }
}

public class GravitationHoldPower : SakuraModPower
{
    private readonly HashSet<CardModel> _returnedCards = [];

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override (PileType, CardPilePosition) ModifyCardPlayResultPileTypeAndPosition(
        CardModel card,
        bool isAutoPlay,
        ResourceInfo resources,
        PileType pileType,
        CardPilePosition position)
    {
        if (Amount <= 0 || card.Owner?.Creature != Owner || pileType != PileType.Discard)
            return (pileType, position);

        _returnedCards.Add(card);
        return (PileType.Hand, CardPilePosition.Bottom);
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (Amount > 0 && play.Card is { } card && _returnedCards.Remove(card))
            await PowerCmd.Decrement(this);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == Owner.Side && participants.Contains(Owner))
            await PowerCmd.Remove(this);
    }
}

public class MagicAwakeningPower : SakuraModPower
{
    protected override string IconFileName => "magic_awakening.png";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        if (Amount <= 0 || player.Creature != Owner)
            return;

        await SakuraManifestLoop.Manifest(player, choiceContext, Amount);
    }
}

public class StarWandPower : SakuraModPower
{
    public const int BootstrapAmount = 1;

    private const int StarThreshold = 2;

    protected override string IconFileName => "star_wand.png";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override int DisplayAmount => Amount;

    public void ResetStars() => SetStars(0);

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (play.Card?.Owner?.Creature != Owner || play.Card.IsReleased() != true)
            return;

        SetStars(Amount + 1);
        if (Amount < StarThreshold)
            return;

        await CardPileCmd.Draw(choiceContext, 1, Owner.Player!, false);
        await PlayerCmd.GainEnergy(1, Owner.Player!);
        SetStars(Amount - StarThreshold);
    }

    private void SetStars(int amount)
    {
        SetAmount(Math.Max(0, amount), true);
    }
}

public class TimeStopPower : SakuraModPower
{
    private bool _preserveBlock;
    private bool _extraTurnStarted;

    protected override string IconFileName => "time_stop.png";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public void PreserveBlock() =>
        _preserveBlock = true;

    public override bool ShouldTakeExtraTurn(MegaCrit.Sts2.Core.Entities.Players.Player player) =>
        Amount > 0 && player.Creature == Owner;

    public override async Task AfterTakingExtraTurn(MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        if (player.Creature != Owner)
            return;

        if (_preserveBlock)
            _extraTurnStarted = true;
        else
            await PowerCmd.Remove(this);
    }

    public override bool ShouldFlush(MegaCrit.Sts2.Core.Entities.Players.Player player) =>
        true;

    public override bool ShouldClearBlock(Creature creature) =>
        !_preserveBlock || creature != Owner;

    public override bool ShouldPlayerResetEnergy(MegaCrit.Sts2.Core.Entities.Players.Player player) =>
        true;

    public override async Task AfterPlayerTurnStartLate(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        if (_extraTurnStarted && player.Creature == Owner)
            await PowerCmd.Remove(this);
    }
}

public class DreamKeyResonancePower : SakuraModPower
{
    protected override string IconFileName => "dream_key_resonance.png";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
}

internal static class SakuraPowerTriggers
{
    public static bool IsOwnerClearCard(CardPlay play, Creature owner) =>
        play.Card is { } card
        && card.Owner?.Creature == owner
        && SakuraCardCatalog.IsTransparentCard(card);
}
