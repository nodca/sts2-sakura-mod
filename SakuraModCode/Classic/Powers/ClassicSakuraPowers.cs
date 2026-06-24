using BaseLib.Abstracts;
using BaseLib.Hooks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Classic.Cards;
using SakuraMod.SakuraModCode.Classic.Relics;
using SakuraMod.SakuraModCode.Extensions;

namespace SakuraMod.SakuraModCode.Classic.Powers;

public abstract class ClassicSakuraPower : CustomPowerModel
{
    protected virtual string IconFileName => "power.png";

    public override string CustomPackedIconPath => IconFileName.PowerImagePath();
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

    protected override string IconFileName => "power.png";
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

        foreach (var power in Owner.Powers.Where(power => power != this).ToList())
            await PowerCmd.Remove(power);
        await PowerCmd.Remove(this);
    }

    public override async Task AfterRemoved(Creature oldOwner)
    {
        foreach (var (power, amount) in _recordedPowers)
        {
            await PowerCmd.Apply(new ThrowingPlayerChoiceContext(), power, oldOwner, amount, oldOwner, null, false);
            power.SetAmount(amount);
        }

        await CreatureCmd.SetCurrentHp(oldOwner, Math.Clamp(_recordedHp, 0, oldOwner.MaxHp));
        await SetBlock(oldOwner, _recordedBlock);
    }

    private void CaptureSnapshot()
    {
        _recordedPowers.Clear();
        foreach (var power in Owner.Powers.Where(power => power != this))
            _recordedPowers[power] = power.Amount;

        _recordedHp = Owner.CurrentHp;
        _recordedBlock = Owner.Block;
    }

    private static async Task SetBlock(Creature creature, int block)
    {
        if (creature.Block > 0)
            await CreatureCmd.LoseBlock(creature, creature.Block);
        if (block > 0)
            await CreatureCmd.GainBlock(creature, block, ValueProp.Move, null, false);
    }
}

public class ClassicDreamPower : ClassicSakuraPower
{
    private readonly List<DreamSwap> _swaps = [];

    protected override string IconFileName => "power.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        ConvertCurrentHand();
        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner.Side != side || !participants.Contains(Owner))
            return;

        ReturnOriginalClowCards();
        await PowerCmd.Remove(this);
    }

    public void ConvertCurrentHand()
    {
        if (Owner.Player is not { } player)
            return;

        var existingSakura = ExistingSakuraIdentities(player);
        foreach (var swap in _swaps)
            existingSakura.Add(swap.Identity);

        var hand = CardPile.Get(PileType.Hand, player);
        if (hand is null)
            return;

        foreach (var original in hand.Cards.ToList().OfType<ClassicClowCard>())
        {
            if (original.Identity is not { } identity
                || !existingSakura.Add(identity)
                || ClassicSakuraCardCatalog.SakuraTypeFor(identity) is not { } sakuraType)
                continue;

            var template = Owner.CombatState!.CreateCard(
                ModelDb.GetById<CardModel>(ModelDb.GetId(sakuraType)),
                player);
            ReplaceInPile(hand, original, template);
            _swaps.Add(new DreamSwap(identity, original, template));
        }
    }

    private void ReturnOriginalClowCards()
    {
        foreach (var swap in _swaps.ToList())
        {
            if (swap.Template.Pile is { Type: PileType.Hand or PileType.Draw or PileType.Discard or PileType.Exhaust } pile)
            {
                ReplaceInPile(pile, swap.Template, swap.Original);
                swap.Template.CardScope?.RemoveCard(swap.Template);
                continue;
            }

            if (swap.Original.Pile is null)
                swap.Original.CardScope?.RemoveCard(swap.Original);
        }

        _swaps.Clear();
    }

    private static HashSet<ClassicCardIdentity> ExistingSakuraIdentities(Player player) =>
        CardPile.GetCards(player, PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust)
            .OfType<ClassicSakuraConversionCard>()
            .Where(static card => card.Identity is not null)
            .Select(static card => card.Identity!.Value)
            .ToHashSet();

    private static void ReplaceInPile(CardPile pile, CardModel oldCard, CardModel newCard)
    {
        var index = pile.Cards.ToList().IndexOf(oldCard);
        if (index < 0)
            return;

        oldCard.RemoveFromCurrentPile();
        pile.AddInternal(newCard, index);
    }

    private sealed record DreamSwap(ClassicCardIdentity Identity, CardModel Original, CardModel Template);
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

        for (var i = 0; i < hand.Count; i++)
        {
            var card = ClassicSakuraCardCatalog.CreateRandomDarkClowCard(player);
            if (card.IsUpgradable)
                card.UpgradeInternal();
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Discard, player, CardPilePosition.Random);
        }
    }
}

public class ClassicShieldWardPower : ClassicSakuraPower
{
    protected override string IconFileName => "power.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Owner.Player == player && Amount > 0)
            await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Move, null, false);
    }
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
    private bool _wasActiveBeforeCardPlayed;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected abstract ClassicElement Element { get; }
    protected abstract Type PermanentPowerType { get; }

    public override Task BeforeCardPlayed(CardPlay play)
    {
        _wasActiveBeforeCardPlayed = Amount > 0
            && play.Card?.Owner?.Creature == Owner
            && play.Card is ClassicSakuraCard card
            && card.Element.HasElement(Element);

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (!_wasActiveBeforeCardPlayed)
            return;

        _wasActiveBeforeCardPlayed = false;
        await TriggerElement(choiceContext, play);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner.Side != side || !participants.Contains(Owner) || Owner.Powers.Any(power => power.GetType() == PermanentPowerType))
            return;

        await PowerCmd.Decrement(this);
    }

    protected abstract Task TriggerElement(PlayerChoiceContext choiceContext, CardPlay play);
}

public abstract class ClassicPermanentElementPower : ClassicSakuraPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
}

public class ClassicEarthyPower : ClassicElementStatePower
{
    public const int Block = 4;

    protected override string IconFileName => "earthy_power.png";
    protected override ClassicElement Element => ClassicElement.Earthy;
    protected override Type PermanentPowerType => typeof(ClassicEarthyPermanentPower);

    protected override async Task TriggerElement(PlayerChoiceContext choiceContext, CardPlay play) =>
        await CreatureCmd.GainBlock(Owner, Block, ValueProp.Move, play, false);
}

public class ClassicEarthyPermanentPower : ClassicPermanentElementPower
{
    protected override string IconFileName => "earthy_power_sakuracard.png";
}

public class ClassicFireyPower : ClassicElementStatePower
{
    public const int HpLoss = 3;

    protected override string IconFileName => "firey_power.png";
    protected override ClassicElement Element => ClassicElement.Firey;
    protected override Type PermanentPowerType => typeof(ClassicFireyPermanentPower);

    protected override async Task TriggerElement(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var combatState = Owner.CombatState
            ?? throw new InvalidOperationException("Classic Firey requires an active combat.");
        foreach (var enemy in combatState.HittableEnemies.ToList())
            await CreatureCmd.Damage(choiceContext, enemy, HpLoss, ValueProp.Unblockable, Owner, play.Card);
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

        await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Move, play, false);
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

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (_upgraded && IsOwnedVoid(play.Card))
            await CardPileCmd.Draw(choiceContext, 1, Owner.Player!, false);
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
        && card is ClassicSakuraCard { Family: ClassicSakuraCardFamily.Clow }
        && !card.IsClone
        && !card.IsDupe;
}

public class ClassicTwinSakuraPower : ClassicSakuraPower
{
    protected override string IconFileName => "twin_power_sakuracard.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount) =>
        IsClow(card) && !card.IsClone && !card.IsDupe
            ? playCount + Math.Max(0, Amount)
            : playCount;

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        ApplyCostIncreaseToKnownClowCards(Amount);
        return Task.CompletedTask;
    }

    public override Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (power == this && amount > 0)
            ApplyCostIncreaseToKnownClowCards((int)amount);

        return Task.CompletedTask;
    }

    public override Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        if (creator == Owner.Player)
            ApplyCostIncrease(card, Amount);

        return Task.CompletedTask;
    }

    private void ApplyCostIncreaseToKnownClowCards(int amount)
    {
        if (amount <= 0 || Owner.Player is not { } player)
            return;

        foreach (var card in CardPile.GetCards(player, PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust))
            ApplyCostIncrease(card, amount);
    }

    private static void ApplyCostIncrease(CardModel card, int amount)
    {
        if (!IsCostedClow(card) || amount <= 0)
            return;

        card.EnergyCost.AddThisCombat(amount);
        card.InvokeEnergyCostChanged();
    }

    private static bool IsCostedClow(CardModel card) =>
        IsClow(card)
        && !card.EnergyCost.CostsX
        && card.EnergyCost.GetWithModifiers(CostModifiers.Local) >= 0;

    private static bool IsClow(CardModel card) =>
        card is ClassicSakuraCard { Family: ClassicSakuraCardFamily.Clow };
}

public class ClassicFloatPower : ClassicSakuraPower
{
    protected override string IconFileName => "float_power.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (Owner.Player == card.Owner && Amount > 0)
            await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Move, null, false);
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
    protected override string IconFileName => "freeze_power.png";
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

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
    protected override string IconFileName => "power.png";
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
    private const int ExpirationWeak = 2;

    protected override string IconFileName => "sleep_power.png";
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterApplied(Creature? applier, CardModel? cardSource) =>
        await StunIfAllowed();

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == Owner && result.UnblockedDamage > 0)
            await PowerCmd.Remove(this);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner.Side != side || !participants.Contains(Owner))
            return;

        if (Amount > 1)
        {
            await StunIfAllowed();
            await PowerCmd.Decrement(this);
            return;
        }

        var missingHp = Math.Max(0, Owner.MaxHp - Owner.CurrentHp);
        if (missingHp > 0)
            await CreatureCmd.Heal(Owner, missingHp);

        await PowerCmd.Apply<WeakPower>(choiceContext, Owner, ExpirationWeak, Owner, null, false);
        await PowerCmd.Remove(this);
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        if (oldOwner.IsMonster && oldOwner.IsAlive && oldOwner.CombatState is not null)
            oldOwner.PrepareForNextTurn(oldOwner.CombatState.GetOpponentsOf(oldOwner));

        return Task.CompletedTask;
    }

    private async Task StunIfAllowed()
    {
        if (Owner.IsMonster)
            await CreatureCmd.Stun(Owner);
    }
}

public class ClassicCerberusMarkPower : ClassicSakuraPower
{
    private const decimal DamageMultiplier = 1.25m;

    protected override string IconFileName => "power.png";
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource) =>
        target == Owner && Amount > 0 && props.IsPoweredAttack()
            ? DamageMultiplier
            : 1m;

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner.Side == side && participants.Contains(Owner))
            await PowerCmd.Remove(this);
    }
}

public class ClassicTemporaryStrengthPower : TemporaryStrengthPower, ICustomPower
{
    public string? CustomPackedIconPath => ModelDb.Power<StrengthPower>().PackedIconPath;
    public string? CustomBigIconPath => ModelDb.Power<StrengthPower>().ResolvedBigIconPath;
    public string? CustomBigBetaIconPath => ModelDb.Power<StrengthPower>().ResolvedBigIconPath;
    public override AbstractModel OriginModel => ModelDb.Card<ClowFight>();
}

public class ClassicTemporaryStrengthLossPower : TemporaryStrengthPower, ICustomPower
{
    public string? CustomPackedIconPath => ModelDb.Power<StrengthPower>().PackedIconPath;
    public string? CustomBigIconPath => ModelDb.Power<StrengthPower>().ResolvedBigIconPath;
    public string? CustomBigBetaIconPath => ModelDb.Power<StrengthPower>().ResolvedBigIconPath;
    public override AbstractModel OriginModel => ModelDb.Card<ClowWood>();
    protected override bool IsVisibleInternal => false;
    protected override bool IsPositive => false;
}
