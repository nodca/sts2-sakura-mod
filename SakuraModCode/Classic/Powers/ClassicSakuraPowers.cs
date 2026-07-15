using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
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
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Classic.Cards;
using SakuraMod.SakuraModCode.Classic.Relics;
using SakuraMod.SakuraModCode.Extensions;
using SakuraMod.SakuraModCode.Powers;
using STS2RitsuLib.Combat.HandSize;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace SakuraMod.SakuraModCode.Classic.Powers;

public abstract class ClassicSakuraPower : ModPowerTemplate
{
    protected virtual string IconFileName => "power.png";

    public override string CustomIconPath => IconFileName.PowerImagePath();
    public override string CustomBigIconPath => IconFileName.BigPowerImagePath();
}

public class ClassicMagicChargePower : ClassicSakuraPower
{
    protected override string IconFileName => "magick_charge_power.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
}

public class ClassicCreatePower : ClassicSakuraPower
{
    protected override string IconFileName => "create_power.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
}

public class ClassicReturnPower : ClassicSakuraPower
{
    private readonly Dictionary<PowerModel, int> _recordedPowers = [];
    private int _recordedHp;
    private int _recordedBlock;

    protected override string IconFileName => "classic_sts1_retain.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        CaptureSnapshot();
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Owner.Player != player)
            return;

        if (Amount > 1)
        {
            await PowerCmd.ModifyAmount(choiceContext, this, -1, Owner, null, false);
            return;
        }

        await PowerCmd.Remove(this);
    }

    public override async Task AfterRemoved(Creature oldOwner)
    {
        await RemovePowersAddedAfterSnapshot(oldOwner);
        RestoreRecordedPowerAmounts(oldOwner);

        await SakuraCreatureState.RestoreHp(oldOwner, _recordedHp);
        SakuraCreatureState.RestoreBlock(oldOwner, _recordedBlock);
    }

    private void CaptureSnapshot()
    {
        _recordedPowers.Clear();
        foreach (var power in Owner.Powers.Where(power => power != this))
            _recordedPowers[power] = power.Amount;

        _recordedHp = Owner.CurrentHp;
        _recordedBlock = Owner.Block;
    }

    private async Task RemovePowersAddedAfterSnapshot(Creature creature)
    {
        while (creature.Powers.FirstOrDefault(power => !_recordedPowers.ContainsKey(power)) is { } power)
            await PowerCmd.Remove(power);
    }

    private void RestoreRecordedPowerAmounts(Creature creature)
    {
        var currentPowers = creature.Powers.ToHashSet();
        foreach (var (power, amount) in _recordedPowers)
        {
            if (currentPowers.Contains(power))
                power.SetAmount(amount);
        }
    }

}

public class ClassicDreamPower : ClassicSakuraPower
{
    private readonly List<DreamSwap> _swaps = [];

    protected override string IconFileName => "classic_sts1_establishment.png";
    protected override bool IsVisibleInternal => false;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override Task AfterApplied(Creature? applier, CardModel? cardSource) =>
        ConvertCurrentHand();

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner.Side != side || !participants.Contains(Owner))
            return;

        await ReturnOriginalClowCards();
        await PowerCmd.Remove(this);
    }

    public async Task ConvertCurrentHand()
    {
        if (Owner.Player is not { } player)
            return;

        var hand = CardPile.Get(PileType.Hand, player);
        if (hand is null)
            return;

        foreach (var original in hand.Cards.ToList().OfType<ClassicClowCard>())
        {
            if (original.Identity is not { } identity
                || ClassicSakuraCardCatalog.SakuraTypeFor(identity) is not { } sakuraType)
                continue;

            var template = Owner.CombatState!.CreateCard(
                ModelDb.GetById<CardModel>(ModelDb.GetId(sakuraType)),
                player);
            if (await ReplaceInPile(hand, original, template))
                _swaps.Add(new DreamSwap(identity, original, template));
        }
    }

    private async Task ReturnOriginalClowCards()
    {
        foreach (var swap in _swaps.ToList())
        {
            if (swap.Template.Pile is { Type: PileType.Hand or PileType.Draw or PileType.Discard or PileType.Exhaust } pile)
            {
                await ReplaceInPile(pile, swap.Template, swap.Original);
                swap.Template.CardScope?.RemoveCard(swap.Template);
                continue;
            }

            if (swap.Original.Pile is null)
                swap.Original.CardScope?.RemoveCard(swap.Original);
        }

        _swaps.Clear();
    }

    private async Task<bool> ReplaceInPile(CardPile pile, CardModel oldCard, CardModel newCard)
    {
        if (!pile.Cards.Contains(oldCard))
            return false;

        await CardPileCmd.RemoveFromCombat(oldCard, skipVisuals: false);
        await CardPileCmd.Add(newCard, pile, CardPilePosition.Random, this, skipVisuals: false);
        return true;
    }

    private sealed record DreamSwap(SourceCardIdentity Identity, CardModel Original, CardModel Template);
}

public class ClassicDarkPower : ClassicSakuraPower
{
    private static readonly LocString Prompt = new("cards", "SAKURAMOD-CLASSIC_DARK.selectionPrompt");

    protected override string IconFileName => "dark_power.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner.Side != side || !participants.Contains(Owner) || Owner.Player is not { } player)
            return;

        var hand = CardPile.GetCards(player, PileType.Hand).ToList();
        if (hand.Count == 0)
            return;

        var selected = (await CardSelectCmd.FromHand(
            choiceContext,
            player,
            new CardSelectorPrefs(Prompt, 0, Math.Min(Amount, hand.Count))
            {
                Cancelable = true
            },
            hand.Contains,
            this)).ToList();

        if (selected.Count == 0)
            return;

        foreach (var card in selected)
            await CardCmd.Exhaust(choiceContext, card, false);
        await PowerCmd.Apply<EnergyNextTurnPower>(choiceContext, Owner, 1, Owner, null, false);
    }
}

public class ClassicDarkSakuraPower : ClassicSakuraPower
{
    protected override string IconFileName => "dark_power_sakuracard.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner.Side != side || !participants.Contains(Owner) || Owner.Player is not { } player)
            return;

        var hand = CardPile.GetCards(player, PileType.Hand).ToList();
        if (hand.Count == 0)
            return;

        foreach (var card in hand)
            await CardCmd.Exhaust(choiceContext, card, false);

        var replacements = new List<CardModel>();
        for (var i = 0; i < hand.Count; i++)
        {
            var card = ClassicSakuraCardCatalog.CreateRandomDarkClowCard(player);
            if (card.IsUpgradable)
                card.UpgradeInternal();
            replacements.Add(card);
        }
        CardCmd.PreviewCardPileAdd(
            await SakuraGeneratedCardLifecycle.AddGeneratedCardsToCombatWithResults(replacements, PileType.Discard, player, CardPilePosition.Bottom),
            style: CardPreviewStyle.GridLayout);
    }
}

public class ClassicShieldWardPower : ClassicSakuraPower
{
    protected override string IconFileName => "classic_sts1_armor.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner.Side != side || !participants.Contains(Owner) || Amount <= 0)
            return;

        await CreatureCmd.GainBlock(Owner, Amount, SakuraPowerValueProps.Block, null, false);
    }
}

public class ClassicWoodPower : ClassicSakuraPower
{
    public const int DefaultStrengthLoss = 2;
    public const int InitialPoison = 3;

    protected override string IconFileName => "earthy_power.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side != CombatSide.Enemy || Owner.Side != CombatSide.Player || Amount <= 0)
            return;

        var poisonTargets = new List<Creature>();
        var strengthLossTargets = new List<Creature>();
        foreach (var enemy in combatState.Enemies.Where(static enemy => enemy.IsAlive))
        {
            if (AppliesBothBranches)
            {
                poisonTargets.Add(enemy);
                strengthLossTargets.Add(enemy);
            }
            else if (enemy.GetPower<PoisonPower>() is { Amount: > 0 })
            {
                strengthLossTargets.Add(enemy);
            }
            else
            {
                poisonTargets.Add(enemy);
            }
        }

        var poisonAmount = PoisonAmount(Amount);
        if (poisonTargets.Count > 0 && poisonAmount > 0)
            // BeforeSideTurnStart runs before the vanilla PoisonPower trigger.
            await PowerCmd.Apply<PoisonPower>(choiceContext, poisonTargets, poisonAmount, Owner, null, false);

        if (strengthLossTargets.Count > 0)
            await PowerCmd.Apply<ClassicTemporaryStrengthLossPower>(
                choiceContext,
                strengthLossTargets,
                Amount,
                Owner,
                null,
                false);
    }

    protected virtual bool AppliesBothBranches => false;

    protected virtual int PoisonAmount(int strengthLoss) =>
        strengthLoss / DefaultStrengthLoss * InitialPoison;
}

public class ClassicSakuraWoodPower : ClassicWoodPower
{
    public const int StrengthLoss = 4;
    public const int PoisonPerTrigger = 2;

    protected override bool AppliesBothBranches => true;

    protected override int PoisonAmount(int strengthLoss) =>
        strengthLoss / StrengthLoss * PoisonPerTrigger;
}

public class ClassicJumpPower : ClassicSakuraPower
{
    protected override string IconFileName => "jump_power_sakuracard.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Owner.Player != player || Amount <= 0)
            return;

        for (var i = 0; i < Amount; i++)
        {
            var debuffs = Owner.Powers.Where(static power => power.TypeForCurrentAmount == PowerType.Debuff).ToList();
            var debuff = Owner.Player.RunState.Rng.CombatCardSelection.NextItem(debuffs);
            if (debuff is null)
                return;

            await PowerCmd.Remove(debuff);
        }
    }
}

public class ClassicSweetPower : ClassicSakuraPower
{
    protected override string IconFileName => "sweet_power_sakuracard.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Owner.Player != player || Amount <= 0)
            return;

        var missingHp = Math.Max(0, Owner.MaxHp - Owner.CurrentHp);
        var heal = missingHp * Amount / 100;
        if (heal > 0)
            await CreatureCmd.Heal(Owner, heal);
    }
}

public class ClassicVoicePower : ClassicSakuraPower
{
    protected override string IconFileName => "voice_power_sakuracard.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Owner.Player != player || Amount <= 0)
            return;

        var combatState = player.Creature.CombatState
            ?? throw new InvalidOperationException("Sakura Voice generated cards require an active combat.");
        for (var i = 0; i < Amount; i++)
        {
            var card = combatState.CreateCard<ClowVoice>(player);
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player, CardPilePosition.Random);
        }
    }
}

public abstract class ClassicElementStatePower : ClassicSakuraPower
{
    private bool _wasActiveForCardPlayed;
    private bool _preserveForNextTurn;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => false;

    protected abstract ClassicElement Element { get; }
    protected abstract Type PermanentPowerType { get; }

    public static void PreserveAllForNextTurn(Creature owner)
    {
        foreach (var power in owner.Powers.OfType<ClassicElementStatePower>())
            power.PreserveForNextTurn();
    }

    public void PreserveForNextTurn() =>
        _preserveForNextTurn = true;

    public override Task BeforeCardPlayed(CardPlay play)
    {
        _wasActiveForCardPlayed = Amount > 0
            && play.Card?.Owner?.Creature == Owner;

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (!_wasActiveForCardPlayed || !SakuraActions.HasClassicElement(play.Card, Element))
        {
            _wasActiveForCardPlayed = false;
            return;
        }

        _wasActiveForCardPlayed = false;
        await TriggerElement(choiceContext, play);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner.Side != side || !participants.Contains(Owner))
            return;

        if (_preserveForNextTurn)
        {
            _preserveForNextTurn = false;
            return;
        }

        if (Owner.Powers.Any(power => power.GetType() == PermanentPowerType))
            return;

        await PowerCmd.Decrement(this);
    }

    protected abstract Task TriggerElement(PlayerChoiceContext choiceContext, CardPlay play);
}

public abstract class ClassicPermanentElementPower : ClassicSakuraPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    protected override bool IsVisibleInternal => false;
}

public class ClassicEarthyPower : ClassicElementStatePower
{
    public const int Block = 4;

    protected override string IconFileName => "earthy_power.png";
    protected override ClassicElement Element => ClassicElement.Earthy;
    protected override Type PermanentPowerType => typeof(ClassicEarthyPermanentPower);

    protected override async Task TriggerElement(PlayerChoiceContext choiceContext, CardPlay play) =>
        await CreatureCmd.GainBlock(Owner, Block, SakuraPowerValueProps.Block, null, false);
}

public class ClassicEarthyPermanentPower : ClassicPermanentElementPower
{
    protected override string IconFileName => "earthy_power_sakuracard.png";
}

public class ClassicFireyPower : ClassicElementStatePower
{
    public const int Damage = 3;

    protected override string IconFileName => "firey_power.png";
    protected override ClassicElement Element => ClassicElement.Firey;
    protected override Type PermanentPowerType => typeof(ClassicFireyPermanentPower);

    protected override async Task TriggerElement(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var combatState = Owner.CombatState
            ?? throw new InvalidOperationException("Classic Firey requires an active combat.");
        foreach (var enemy in combatState.HittableEnemies.ToList())
            await CreatureCmd.Damage(choiceContext, enemy, Damage, SakuraPowerValueProps.Damage, Owner, null);
    }
}

public class ClassicFireyPermanentPower : ClassicPermanentElementPower
{
    protected override string IconFileName => "firey_power_sakuracard.png";
}

public class ClassicWateryPower : ClassicElementStatePower
{
    private const int EnergyTrigger = 2;
    private int _counter;

    protected override string IconFileName => "watery_power.png";
    protected override ClassicElement Element => ClassicElement.Watery;
    protected override Type PermanentPowerType => typeof(ClassicWateryPermanentPower);

    protected override async Task TriggerElement(PlayerChoiceContext choiceContext, CardPlay play)
    {
        _counter++;
        if (_counter < EnergyTrigger)
            return;

        _counter -= EnergyTrigger;
        await PlayerCmd.GainEnergy(1, Owner.Player!);
    }
}

public class ClassicWateryPermanentPower : ClassicPermanentElementPower
{
    protected override string IconFileName => "watery_power_sakuracard.png";
}

public class ClassicWindyPower : ClassicElementStatePower
{
    private const int DrawTrigger = 2;
    private int _counter;

    protected override string IconFileName => "windy_power.png";
    protected override ClassicElement Element => ClassicElement.Windy;
    protected override Type PermanentPowerType => typeof(ClassicWindyPermanentPower);

    protected override async Task TriggerElement(PlayerChoiceContext choiceContext, CardPlay play)
    {
        _counter++;
        if (_counter < DrawTrigger)
            return;

        _counter -= DrawTrigger;
        await CardPileCmd.Draw(choiceContext, 1, Owner.Player!, false);
    }
}

public class ClassicWindyPermanentPower : ClassicPermanentElementPower
{
    protected override string IconFileName => "windy_power_sakuracard.png";
}

public class ClassicWavePower : ClassicSakuraPower
{
    protected override string IconFileName => "wave_power.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (Amount <= 0 || play.Card?.Owner?.Creature != Owner || play.Card.Type != CardType.Attack)
            return;

        await CreatureCmd.GainBlock(Owner, Amount, SakuraPowerValueProps.Block, null, false);
    }
}

public class ClassicBigPower : ClassicSakuraPower
{
    private const decimal DamageMultiplier = 1.33m;

    protected override string IconFileName => "big_power_sakuracard.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource) =>
        Amount > 0 && dealer == Owner && props.IsPoweredAttack()
            ? DamageMultiplier
            : 1m;
}

public class ClassicLittlePower : ClassicSakuraPower
{
    private const decimal DamageMultiplier = 0.67m;

    protected override string IconFileName => "little_power_sakuracard.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource) =>
        Amount > 0 && target == Owner && dealer?.Side != Owner.Side && props.IsPoweredAttack()
            ? DamageMultiplier
            : 1m;
}

public class ClassicFlyPower : ClassicSakuraPower
{
    protected override string IconFileName => "fly_power_sakuracard.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Owner.Player == player && Amount > 0)
            await CardPileCmd.Draw(choiceContext, Amount, player, false);
    }
}

public class ClassicGlowPower : ClassicSakuraPower
{
    protected override string IconFileName => "glow_power_sakuracard.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Owner.Player == player && Amount > 0 && player.GetRelic<ClassicSealedBookRelic>() is not null)
            await PowerCmd.Apply<ClassicMagicChargePower>(choiceContext, Owner, Amount, Owner, null, false);
    }
}

public class ClassicMovePower : ClassicSakuraPower
{
    protected override string IconFileName => "move_power_sakuracard.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Owner.Player != player || Amount <= 0)
            return;

        var combatState = player.Creature.CombatState
            ?? throw new InvalidOperationException("Sakura Move generated cards require an active combat.");
        for (var i = 0; i < Amount; i++)
        {
            var card = combatState.CreateCard<SpellEmptySpell>(player);
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player, CardPilePosition.Random);
        }
    }
}

public class ClassicLockPower : ClassicSakuraPower
{
    protected override string IconFileName => "lock_power.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner.Side != side || !participants.Contains(Owner))
            return;

        await PowerCmd.Decrement(this);
    }
}

public class ClassicLockSakuraPower : ClassicSakuraPower
{
    protected override string IconFileName => "lock_power_sakuracard.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner.Side != side || !participants.Contains(Owner))
            return;

        await PowerCmd.Decrement(this);
    }
}

public class ClassicLoopPower : ClassicSakuraPower
{
    protected override string IconFileName => "loop_power.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Owner.Player != player || Amount <= 0)
            return;

        await CardPileCmd.Draw(choiceContext, Amount, player, false);
        var hand = CardPile.GetCards(player, PileType.Hand).ToList();
        if (hand.Count == 0)
            return;

        var selected = (await CardSelectCmd.FromHand(
            choiceContext,
            player,
            new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, 0, Math.Min(Amount, hand.Count))
            {
                Cancelable = true
            },
            hand.Contains,
            this)).ToList();
        if (selected.Count > 0)
            await CardCmd.Discard(choiceContext, selected);
    }
}

public class ClassicLoopSakuraPower : ClassicSakuraPower
{
    protected override string IconFileName => "loop_power_sakuracard.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardDiscarded(PlayerChoiceContext choiceContext, CardModel card)
    {
        if (card.Owner == Owner.Player && Amount > 0)
            await CardPileCmd.Draw(choiceContext, Amount, Owner.Player, false);
    }
}

public class ClassicLightPower : ClassicSakuraPower, IMaxHandSizeModifier
{
    private const int ExtraHandSize = 2;
    private bool _upgraded;
    private CardModel? _playedVoid;

    protected override string IconFileName => "light_power.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public bool IsUpgraded => _upgraded;

    public void MarkUpgraded()
    {
        if (_upgraded)
            return;

        _upgraded = true;
        InvokeDisplayAmountChanged();
    }

    public int ModifyMaxHandSize(Player player, int currentMaxHandSize) =>
        player.Creature == Owner ? currentMaxHandSize + ExtraHandSize : currentMaxHandSize;

    public int ModifyMaxHandSizeLate(Player player, int currentMaxHandSize) =>
        currentMaxHandSize;

    public override bool TryModifyKeywordsInCombat(CardModel card, ISet<CardKeyword> keywords)
    {
        if (!_upgraded || !IsOwnedVoid(card))
            return false;

        var changed = keywords.Remove(CardKeyword.Unplayable);
        changed |= keywords.Add(CardKeyword.Exhaust);
        return changed;
    }

    public override (PileType, CardPilePosition) ModifyCardPlayResultPileTypeAndPosition(
        CardModel card,
        bool isAutoPlay,
        ResourceInfo resources,
        PileType pileType,
        CardPilePosition position) =>
        _upgraded && IsOwnedVoid(card)
            ? (PileType.Exhaust, position)
            : (pileType, position);

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (_upgraded && IsOwnedVoid(play.Card))
            _playedVoid = play.Card;

        return Task.CompletedTask;
    }

    public override async Task AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)
    {
        if (card == _playedVoid)
        {
            _playedVoid = null;
            await CardPileCmd.Draw(choiceContext, 1, Owner.Player!, false);
        }
    }

    private bool IsOwnedVoid(CardModel? card) =>
        card is MegaCrit.Sts2.Core.Models.Cards.Void && card.Owner?.Creature == Owner;
}

public class ClassicTwinPower : ClassicSakuraPower
{
    private int _cardsDoubledThisTurn;

    protected override string IconFileName => "twin_power.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount)
    {
        if (!CanDouble(card))
            return playCount;

        _cardsDoubledThisTurn++;
        return playCount + 1;
    }

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Owner.Player == player)
            _cardsDoubledThisTurn = 0;

        return Task.CompletedTask;
    }

    private bool CanDouble(CardModel card) =>
        Amount > 0
        && _cardsDoubledThisTurn < Amount
        && card.Owner?.Creature == Owner
        && card is ClassicSakuraCard { IsClowCard: true }
        && !card.IsClone
        && !card.IsDupe;
}

public class ClassicTwinSakuraPower : ClassicSakuraPower
{
    protected override string IconFileName => "twin_power_sakuracard.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount) =>
        card.Owner?.Creature == Owner && IsClow(card) && !card.IsClone && !card.IsDupe
            ? playCount + Math.Max(0, Amount)
            : playCount;

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        RefreshKnownClowCardCosts();
        return Task.CompletedTask;
    }

    public override Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (power == this)
            RefreshKnownClowCardCosts();

        return Task.CompletedTask;
    }

    public override Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        if (creator == Owner.Player)
            RefreshCost(card);

        return Task.CompletedTask;
    }

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal currentCost, out decimal newCost)
    {
        if (card.Owner?.Creature != Owner || Owner.GetPower<ClassicNothingPower>() is not null)
        {
            newCost = currentCost;
            return false;
        }

        return TryIncreaseClowCardCost(card, Amount, currentCost, out newCost);
    }

    internal static bool TryIncreaseClowCardCost(CardModel card, int amount, decimal currentCost, out decimal newCost)
    {
        var costIncrease = Math.Max(0, amount);
        if (!IsCostedClow(card, currentCost) || costIncrease <= 0)
        {
            newCost = currentCost;
            return false;
        }

        newCost = currentCost + costIncrease;
        return true;
    }

    private void RefreshKnownClowCardCosts()
    {
        if (Owner.Player is not { } player)
            return;

        foreach (var card in CardPile.GetCards(player, PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust))
            RefreshCost(card);
    }

    private static void RefreshCost(CardModel card)
    {
        if (IsCostedClow(card, card.EnergyCost.GetWithModifiers(CostModifiers.Local)))
            card.InvokeEnergyCostChanged();
    }

    private static bool IsCostedClow(CardModel card, decimal currentCost) =>
        IsClow(card)
        && !card.EnergyCost.CostsX
        && currentCost >= 0;

    private static bool IsClow(CardModel card) =>
        card is ClassicSakuraCard { IsClowCard: true };
}

public class ClassicFloatPower : ClassicSakuraPower
{
    protected override string IconFileName => "float_power.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (Owner.Player == card.Owner && Amount > 0)
            await CreatureCmd.GainBlock(Owner, Amount, SakuraPowerValueProps.Block, null, false);
    }
}

public class ClassicFloatSakuraPower : ClassicSakuraPower
{
    protected override string IconFileName => "float_power_sakuracard.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Owner.Player == player && Amount > 0)
            await PowerCmd.Apply<DexterityPower>(choiceContext, Owner, Amount, Owner, null, false);
    }
}

public class ClassicFreezePower : ClassicSakuraPower
{
    internal const int BlockGain = 5;

    protected override string IconFileName => "freeze_power.png";
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        if (!Owner.IsMonster || Owner.Monster?.IntendsToAttack != true)
            return;

        await CreatureCmd.Stun(Owner);
        await CreatureCmd.GainBlock(Owner, BlockGain, SakuraPowerValueProps.Block, null, false);
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (Owner.Side == side && participants.Contains(Owner))
            await PowerCmd.Remove(this);
    }
}

public class ClassicTimePower : ClassicSakuraPower
{
    protected override string IconFileName => "classic_sts1_time.png";
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterApplied(Creature? applier, CardModel? cardSource) =>
        await StunOwner();

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner.Side != side || !participants.Contains(Owner))
            return;

        if (Amount > 1)
        {
            await StunOwner();
            await PowerCmd.Decrement(this);
            return;
        }

        await PowerCmd.Remove(this);
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        if (oldOwner.IsMonster && oldOwner.IsAlive && oldOwner.CombatState is not null)
            oldOwner.PrepareForNextTurn(oldOwner.CombatState.GetOpponentsOf(oldOwner));

        return Task.CompletedTask;
    }

    private async Task StunOwner()
    {
        if (Owner.IsMonster)
            await CreatureCmd.Stun(Owner);
    }
}

public class ClassicSilentPendingPower : ClassicSakuraPower
{
    protected override string IconFileName => "silent_power.png";
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Owner.Player != player)
            return;

        await PowerCmd.Apply<ClassicSilentNoAttackPower>(choiceContext, Owner, 1, Owner, null, false);
        await PowerCmd.Remove(this);
    }
}

public class ClassicSilentNoAttackPower : ClassicSakuraPower
{
    protected override string IconFileName => "silent_power.png";
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType) =>
        card.Owner.Creature != Owner || card.Type != CardType.Attack;

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (Owner.Side == side && participants.Contains(Owner))
            await PowerCmd.Remove(this);
    }
}

public class ClassicSleepPower : ClassicSakuraPower
{
    protected override string IconFileName => "sleep_power.png";
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        SakuraSleepMove.Apply(Owner);
        return Task.CompletedTask;
    }

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == Owner && result.UnblockedDamage > 0)
            await PowerCmd.Remove(this);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner.Side != side
            || !participants.Contains(Owner)
            || !SakuraSleepMove.WasConsumedBy(Owner))
            return;

        if (Amount > 1)
        {
            await PowerCmd.Decrement(this);
            SakuraSleepMove.Renew(Owner);
            return;
        }

        await PowerCmd.Remove(this);
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        SakuraSleepMove.RestoreIfUnconsumed(oldOwner);

        return Task.CompletedTask;
    }
}

public class ClassicCerberusMarkPower : ClassicSakuraPower
{
    private const decimal DamageMultiplierPerStack = 0.25m;

    protected override string IconFileName => "classic_sts1_accuracy.png";
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource) =>
        target == Owner && Amount > 0 && props.IsPoweredAttack()
            ? 1m + DamageMultiplierPerStack * Amount
            : 1m;

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner.Side == side && participants.Contains(Owner))
            await PowerCmd.Remove(this);
    }
}

public class ClassicNothingPower : ClassicSakuraPower
{
    private int _damage = 4;
    private int _block = 3;
    private bool _upgraded;

    protected override string IconFileName => "classic_sts1_retain.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Damage", _damage),
        new DynamicVar("Block", _block)
    ];

    public void SetValues(int damage, int block, bool upgraded)
    {
        if (_upgraded)
            return;

        _damage = damage;
        _block = block;
        _upgraded = upgraded;
        DynamicVars["Damage"].BaseValue = _damage;
        DynamicVars["Block"].BaseValue = _block;
        InvokeDisplayAmountChanged();
        RefreshKnownMagicCardCosts();
    }

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        RefreshKnownMagicCardCosts();
        return Task.CompletedTask;
    }

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal currentCost, out decimal newCost)
    {
        if (IsCostedOwnedMagic(card) && currentCost > 0)
        {
            newCost = 0;
            return true;
        }

        newCost = currentCost;
        return false;
    }

    public override bool TryModifyKeywordsInCombat(CardModel card, ISet<CardKeyword> keywords)
    {
        if (!IsOwnedClowOrSakura(card))
            return false;

        return keywords.Add(CardKeyword.Exhaust);
    }

    public override (PileType, CardPilePosition) ModifyCardPlayResultPileTypeAndPosition(
        CardModel card,
        bool isAutoPlay,
        ResourceInfo resources,
        PileType pileType,
        CardPilePosition position) =>
        IsOwnedClowOrSakura(card)
            ? (PileType.Exhaust, position)
            : (pileType, position);

    public override Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        RefreshCost(card);
        return Task.CompletedTask;
    }

    public override Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        if (creator?.Creature == Owner)
            RefreshCost(card);

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (!IsOwnedClowOrSakura(play.Card))
            return;

        await CreatureCmd.GainBlock(Owner, _block, SakuraPowerValueProps.Block, null, false);

        var combatState = Owner.CombatState
            ?? throw new InvalidOperationException("Classic Nothing requires an active combat.");
        foreach (var enemy in combatState.HittableEnemies.ToList())
            await CreatureCmd.Damage(choiceContext, enemy, _damage, SakuraPowerValueProps.HpLoss, Owner, null);
    }

    public override async Task AfterCombatEnd(CombatRoom room)
    {
        if (_upgraded || Owner.Player is not { } player)
            return;

        var candidates = player.Deck.Cards
            .Where(static card => card is not ClowNothing)
            .ToList();
        var swallowed = player.RunState.Rng.CombatCardSelection.NextItem(candidates);
        if (swallowed is not null)
            await CardPileCmd.RemoveFromDeck(swallowed, showPreview: true);
    }

    private void RefreshKnownMagicCardCosts()
    {
        if (Owner.Player is not { } player)
            return;

        foreach (var card in CardPile.GetCards(player, PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust))
            RefreshCost(card);
    }

    private static void RefreshCost(CardModel card)
    {
        if (IsCostedMagic(card))
            card.InvokeEnergyCostChanged();
    }

    private bool IsCostedOwnedMagic(CardModel card) =>
        card.Owner?.Creature == Owner && IsCostedMagic(card);

    private static bool IsCostedMagic(CardModel card) =>
        card is ClassicSakuraCard { IsClassicSourceCard: true }
        && !card.EnergyCost.CostsX
        && card.EnergyCost.GetWithModifiers(CostModifiers.Local) > 0;

    private bool IsOwnedClowOrSakura(CardModel? card) =>
        card?.Owner?.Creature == Owner
        && card is ClassicSakuraCard { IsClassicSourceCard: true };
}

public class ClassicHopePower : ClassicSakuraPower
{
    private const decimal Multiplier = 2m;

    protected override string IconFileName => "glow_power_sakuracard.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource) =>
        Amount > 0 && dealer == Owner && props.IsPoweredAttack()
            ? Multiplier
            : 1m;

    public override decimal ModifyBlockMultiplicative(Creature target, decimal block, ValueProp props, CardModel? cardSource, CardPlay? cardPlay) =>
        Amount > 0 && target == Owner && props.HasFlag(ValueProp.Move) && !props.HasFlag(ValueProp.Unpowered)
            ? Multiplier
            : 1m;
}

public class ClassicNothingMonsterPower : ClassicSakuraPower
{
    private const int ExhaustChance = 10;

    protected override string IconFileName => "classic_sts1_blur.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Chance", ExhaustChance)];

    public override (PileType, CardPilePosition) ModifyCardPlayResultPileTypeAndPosition(
        CardModel card,
        bool isAutoPlay,
        ResourceInfo resources,
        PileType pileType,
        CardPilePosition position)
    {
        if (card.Owner?.Creature != Owner || card is not ClassicSakuraCard { IsClassicSourceCard: true })
            return (pileType, position);

        var roll = Owner.Player?.RunState.Rng.CombatCardSelection.NextInt(100) ?? 100;
        return roll < ExhaustChance
            ? (PileType.Exhaust, position)
            : (pileType, position);
    }
}

public class ClassicTemporaryStrengthPower : TemporaryStrengthPower, IModPowerAssetOverrides
{
    public PowerAssetProfile AssetProfile => PowerAssetProfile.Empty;
    public string? CustomIconPath => ModelDb.Power<StrengthPower>().PackedIconPath;
    public string? CustomBigIconPath => ModelDb.Power<StrengthPower>().ResolvedBigIconPath;
    public override AbstractModel OriginModel => ModelDb.Card<ClowFight>();
    protected override bool IsVisibleInternal => false;
}

public class ClassicTemporaryStrengthLossPower : TemporaryStrengthPower, IModPowerAssetOverrides
{
    public PowerAssetProfile AssetProfile => PowerAssetProfile.Empty;
    public string? CustomIconPath => ModelDb.Power<StrengthPower>().PackedIconPath;
    public string? CustomBigIconPath => ModelDb.Power<StrengthPower>().ResolvedBigIconPath;
    public override AbstractModel OriginModel => ModelDb.Card<ClowWood>();
    protected override bool IsVisibleInternal => false;
    protected override bool IsPositive => false;
}
