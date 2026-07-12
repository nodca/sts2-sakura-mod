using MegaCrit.Sts2.Core;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Classic.Powers;
using SakuraMod.SakuraModCode.Extensions;
using SakuraMod.SakuraModCode.Powers;
using STS2RitsuLib.Combat.HandSize;

namespace SakuraMod.SakuraModCode.Cards;

public class Gravitation() : SakuraModCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self), IExtraEffectCard
{
    protected override bool HasExtraEffect => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Earth];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        SakuraCardPlayVfx.PlayGravitation(CombatState!.HittableEnemies);
        var power = await PowerCmd.Apply<GravitationHoldPower>(
            choiceContext,
            Owner.Creature,
            1,
            Owner.Creature,
            this,
            false);
        power?.ExcludeSource(this);
        await TriggerExtraEffect(choiceContext, play);
    }

    public async Task OnExtraEffect(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await ChooseFromPileToHand(choiceContext, PileType.Discard);
        await ChooseFromPileToHand(choiceContext, PileType.Draw);
    }

    protected override void OnUpgrade() => AddKeywordIfMissing(CardKeyword.Retain);

    private async Task ChooseFromPileToHand(PlayerChoiceContext choiceContext, PileType pileType)
    {
        var card = await SakuraActions.SelectFromCards(
            this,
            choiceContext,
            CardPile.Get(pileType, Owner)!.Cards,
            cancelable: false);
        if (card is not null)
            await SakuraActions.MoveExistingCardToHand(this, card);
    }
}

public class Mirage() : SakuraModCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override bool HasExtraEffect => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Water, CardKeyword.Exhaust];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<MiragePower>()];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var targets = IsUsingExtraEffect
            ? CombatState!.HittableEnemies.ToList()
            : [RequiredTarget(play)];
        await PowerCmd.Apply<MiragePower>(choiceContext, targets, 1, Owner.Creature, this, false);
    }

    protected override void OnUpgrade() => AddKeywordIfMissing(CardKeyword.Retain);
}

public class Record() : SakuraModCard(2, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    protected override bool HasExtraEffect => true;
    private bool _restoredDuringCurrentPlay;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Water];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1), new EnergyVar(1)];

    public override (PileType, CardPilePosition) ModifyCardPlayResultPileTypeAndPosition(
        CardModel card,
        bool isAutoPlay,
        ResourceInfo resources,
        PileType pileType,
        CardPilePosition position)
    {
        if (card == this)
            _restoredDuringCurrentPlay = false;

        if (card == this && Owner.Creature.GetPower<RecordPower>() is not null)
            return (PileType.Exhaust, CardPilePosition.Bottom);

        return (pileType, position);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (_restoredDuringCurrentPlay)
            return;

        var result = await RecordPower.RecordOrRestore(choiceContext, Owner.Creature, this);
        if (result == RecordResult.Restored)
            _restoredDuringCurrentPlay = true;

        if (!IsUsingExtraEffect)
            return;

        if (result == RecordResult.Recorded)
        {
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner, false);
            return;
        }

        await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner);
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await base.AfterCardPlayed(choiceContext, play);

        if (play.Card != this || play.PlayIndex < play.PlayCount - 1)
            return;

        var shouldExhaust = _restoredDuringCurrentPlay && play.ResultPile != PileType.Exhaust;
        _restoredDuringCurrentPlay = false;
        if (shouldExhaust && Pile?.Type == PileType.Play)
            await CardCmd.Exhaust(choiceContext, this);
    }

    protected override void OnUpgrade() => AddKeywordIfMissing(CardKeyword.Retain);
}





public class Exchange() : SakuraModCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Fire, CardKeyword.Exhaust];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await CardPileCmd.Draw(choiceContext, 1, Owner, false);

        var cards = await SakuraActions.SelectHandCards(this, choiceContext, card => card != this, 2);
        if (cards.Count == 2)
        {
            var first = cards[0];
            var second = cards[1];
            SakuraActions.TryExchangeEnergyCosts(first, second, restOfCombat: IsUpgraded);
            first.ExchangeTemporaryState(second);
        }
    }
}

public class Kindness() : SakuraModCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override bool HasExtraEffect => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Earth, CardKeyword.Exhaust];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var power = await PowerCmd.Apply<KindnessPower>(
            choiceContext,
            Owner.Creature,
            1,
            Owner.Creature,
            this,
            false);
        power?.QueueEffect(IsUsingExtraEffect);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class Labyrinth() : SakuraModCard(2, CardType.Power, CardRarity.Rare, TargetType.None)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Earth];
    internal override IEnumerable<string> ReferencedStaticHoverTipKeys => [SakuraCardHoverTips.LabyrinthTipKey];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var targets = CombatState!.Enemies.Where(enemy => enemy.IsAlive).ToList();
        var power = await PowerCmd.Apply<LabyrinthPower>(
            choiceContext,
            Owner.Creature,
            1,
            Owner.Creature,
            this,
            false);
        if (power is not null)
            await power.Enter(targets);
    }

    protected override void OnUpgrade() => AddKeywordIfMissing(CardKeyword.Retain);
}

public class Repair() : SakuraModCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self), IExtraEffectCard
{
    protected override bool HasExtraEffect => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Earth, CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<RegenPower>(3)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PowerCmd.Apply<RegenPower>(choiceContext, Owner.Creature, DynamicVars["RegenPower"].IntValue, Owner.Creature, this, false);
        await TriggerExtraEffect(choiceContext, play);
    }

    public async Task OnExtraEffect(PlayerChoiceContext choiceContext, CardPlay play) =>
        await PowerCmd.Apply<RepairRegenerationPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this, false);

    protected override void OnUpgrade() => DynamicVars["RegenPower"].UpgradeValueBy(1);
}

public class Reversal() : SakuraModCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy), IExtraEffectCard
{
    protected override bool HasExtraEffect => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Wind];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(4, ValueProp.Move),
        new DynamicVar("PileCardsPerDamage", 3),
        new DamageVar("PileDamage", 1, ValueProp.Move),
        new CardsVar(1)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var drawCards = CardPile.Get(PileType.Draw, Owner)!.Cards.ToList();
        var discardCards = CardPile.Get(PileType.Discard, Owner)!.Cards.ToList();
        var exchangedCards = drawCards.Count + discardCards.Count;
        PileExchangeVfx.Play(drawCards.Count, discardCards.Count);

        foreach (var card in drawCards)
            await SakuraActions.MoveExistingCardToPileWithoutVisuals(this, card, PileType.Discard, CardPilePosition.Bottom);
        foreach (var card in discardCards)
            await SakuraActions.MoveExistingCardToPileWithoutVisuals(this, card, PileType.Draw, CardPilePosition.Bottom);

        var pileDamage = exchangedCards / DynamicVars["PileCardsPerDamage"].IntValue * DynamicVars["PileDamage"].IntValue;
        await SakuraActions.Attack(choiceContext, this, RequiredTarget(play), DynamicVars.Damage.IntValue + pileDamage);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner, false);
        await TriggerExtraEffect(choiceContext, play);
    }

    public async Task OnExtraEffect(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var handSize = CardPile.Get(PileType.Hand, Owner)!.Cards.Count;
        var cardsToDraw = MaxHandSizeCalculator.Calculate(Owner) - handSize;
        if (cardsToDraw > 0)
            await CardPileCmd.Draw(choiceContext, cardsToDraw, Owner, false);
    }

    protected override void OnUpgrade() => DynamicVars["PileCardsPerDamage"].UpgradeValueBy(-1);
}

public class Rewind() : SakuraModCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override bool HasExtraEffect => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Earth, CardKeyword.Exhaust];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var card = await SakuraActions.SelectFromCards(this, choiceContext, CardPile.Get(PileType.Exhaust, Owner)!.Cards, cancelable: false);
        if (card is null)
            return;

        await SakuraActions.MoveExistingCardToHand(this, card);
        await TriggerExtraEffect(() =>
        {
            card.SetToFreeThisTurn();
            return Task.CompletedTask;
        });
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class Snooze() : SakuraModCard(1, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy), IExtraEffectCard
{
    protected override bool HasExtraEffect => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Wind];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<WeakPower>(2), new PowerVar<SakuraSleepPower>(2)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = RequiredTarget(play);
        await PowerCmd.Apply<WeakPower>(choiceContext, target, DynamicVars.Weak.IntValue, Owner.Creature, this, false);
        await CardPileCmd.Draw(choiceContext, 1, Owner, false);
        await TriggerExtraEffect(choiceContext, play);
    }

    public async Task OnExtraEffect(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = play.Target ?? CombatState?.HittableEnemies.FirstOrDefault();
        if (target is not null)
            await PowerCmd.Apply<SakuraSleepPower>(choiceContext, target, DynamicVars["SakuraSleepPower"].IntValue, Owner.Creature, this, false);
    }

    protected override void OnUpgrade() => DynamicVars.Weak.UpgradeValueBy(1);
}

public class Spiral() : SakuraModCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy), IExtraEffectCard
{
    protected override bool HasExtraEffect => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Wind];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CalculationBaseVar(4),
        new ExtraDamageVar(3),
        new CalculatedDamageVar(ValueProp.Move).WithMultiplier(SpiralRules.PlayedPreviouslyThisCombat)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = RequiredTarget(play);
        await SakuraActions.Attack(choiceContext, this, target, DynamicVars.CalculatedDamage);
        await TriggerExtraEffect(choiceContext, play);
    }

    public async Task OnExtraEffect(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await SakuraGeneratedCardLifecycle.AddTemporaryCopyToHand(
            this,
            freeThisTurn: false,
            context: choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.ExtraDamage.UpgradeValueBy(1);
}

internal static class SpiralRules
{
    public static decimal PlayedPreviouslyThisCombat(CardModel card, Creature? target) =>
        card.Owner is { } owner
            ? CombatManager.Instance.History.CardPlaysFinished
                .Where(entry => entry is CardPlayFinishedEntry { CardPlay.Card.Owner: var cardOwner } && cardOwner == owner)
                .Select(entry => ((CardPlayFinishedEntry)entry).CardPlay.Card)
                .Count(static playedCard => playedCard is Spiral)
            : 0;
}

public class Transfer() : SakuraModCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override bool HasExtraEffect => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Wind, CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("EnemyStrengthLoss", 2),
        new DynamicVar("StrengthGain", 1),
        new DynamicVar("DexterityGain", 1)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ApplyExtraEffectExhaustChange();
        var target = RequiredTarget(play);
        await PowerCmd.Apply<StrengthPower>(choiceContext, target, -DynamicVars["EnemyStrengthLoss"].IntValue, Owner.Creature, this, false);
        await PowerCmd.Apply<StrengthPower>(choiceContext, Owner.Creature, DynamicVars["StrengthGain"].IntValue, Owner.Creature, this, false);
        await PowerCmd.Apply<DexterityPower>(choiceContext, Owner.Creature, DynamicVars["DexterityGain"].IntValue, Owner.Creature, this, false);
    }

    protected override PileType GetResultPileTypeForCardPlay() =>
        SakuraModCard.UsesMagicChargeExtraEffect(this)
            ? PileType.Discard
            : base.GetResultPileTypeForCardPlay();

    private void ApplyExtraEffectExhaustChange()
    {
        if (IsUsingExtraEffect)
            RemoveKeywordIfPresent(CardKeyword.Exhaust);
        else
            AddKeywordIfMissing(CardKeyword.Exhaust);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["EnemyStrengthLoss"].UpgradeValueBy(1);
        DynamicVars["StrengthGain"].UpgradeValueBy(1);
    }
}




































public class Blank() : SakuraModCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self), IExtraEffectCard
{
    protected override bool HasExtraEffect => true;
    private int _removedCardsThisPlay;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Earth, CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(4, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block.IntValue, ValueProp.Move, play, false);

        var cards = CardPile.GetCards(Owner, PileType.Hand, PileType.Draw, PileType.Discard)
            .Where(card => card.Type is CardType.Status or CardType.Curse)
            .ToList();
        _removedCardsThisPlay = cards.Count;

        foreach (var card in cards)
            await CardCmd.Exhaust(choiceContext, card);

        await TriggerExtraEffect(choiceContext, play);
    }

    public async Task OnExtraEffect(PlayerChoiceContext choiceContext, CardPlay play)
    {
        foreach (var power in Owner.Creature.Powers.Where(IsOwnNegativePower).ToList())
            await PowerCmd.Remove(power);

        foreach (var enemy in CombatState!.Enemies)
        {
            foreach (var power in enemy.Powers
                         .Where(IsEnemyPositivePower)
                         .ToList())
                await PowerCmd.Remove(power);
        }

        if (_removedCardsThisPlay > 0)
            await CardPileCmd.Draw(choiceContext, _removedCardsThisPlay, Owner, false);
    }

    private static bool IsOwnNegativePower(PowerModel power) =>
        power.Type == PowerType.Debuff
        || IsNegativeStatPower(power);

    private static bool IsEnemyPositivePower(PowerModel power) =>
        IsPositiveStatPower(power)
        || power is ArtifactPower { Amount: > 0 };

    private static bool IsNegativeStatPower(PowerModel power) =>
        power.Amount < 0 && power is StrengthPower or DexterityPower;

    private static bool IsPositiveStatPower(PowerModel power) =>
        power.Amount > 0 && power is StrengthPower or DexterityPower;

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3);
}

public class Mirror() : SakuraModCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    protected override bool HasExtraEffect => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Water, CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new RepeatVar(1), new RepeatVar("ExtraRepeat", 1)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var card = await SakuraActions.SelectHandCard(this, choiceContext, card => card != this);
        if (card is not null)
        {
            var amount = DynamicVars["Repeat"].IntValue;
            if (IsUsingExtraEffect)
                amount += DynamicVars["ExtraRepeat"].IntValue;
            card.AddReplayThisTurn(amount);
        }
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class Remind() : SakuraModCard(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    private static LocString SelectionPrompt => CardLoc<Remind>("selectionPrompt");

    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Fire, CardKeyword.Exhaust];
    protected override bool HasEnergyCostX => true;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var choices = TemporaryCardMemory.CardsRemovedByTemporary(CombatState, Owner);
        var recallCount = RecallCount();
        var cards = choices.Count <= recallCount
            ? choices
            : await SakuraActions.SelectUpToFromCards(
                this,
                choiceContext,
                choices,
                recallCount,
                cancelable: false,
                prompt: SelectionPrompt,
                minSelect: recallCount);

        foreach (var card in cards)
        {
            await SakuraGeneratedCardLifecycle.AddTemporaryRememberedCopyToHand(
                card,
                freeThisTurn: true,
                context: choiceContext);
        }
    }

    private int RecallCount()
    {
        var energy = Math.Max(0, ResolveEnergyXValue());
        return IsUpgraded ? energy * 2 + 1 : energy + 1;
    }

    protected override void OnUpgrade() { }
}

public class Synchronize() : SakuraModCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Wind];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var cards = await SakuraActions.SelectHandCards(
            this,
            choiceContext,
            card => SakuraCardCatalog.IsTransparentCard(card) && card != this,
            2);
        if (cards.Count != 2)
            return;

        cards[0].SynchronizeWith(cards[1]);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class Time() : SakuraModCard(3, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    protected override bool HasExtraEffect => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Fire, CardKeyword.Exhaust];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        SakuraCardPlayVfx.PlayTime(Owner.Creature);
        var power = await PowerCmd.Apply<TimeStopPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this, true);
        if (IsUsingExtraEffect)
            power?.PreserveCurrentTurnState();

        PlayerCmd.EndTurn(Owner, canBackOut: false);
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await base.AfterCardPlayed(choiceContext, play);

        if (play.Card == this
            && Owner.Creature.GetPower<TimeStopPower>() is { PreservesCurrentTurnState: true } power)
            power.PreserveElementStates();
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class TrueOrFalse() : SakuraModCard(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    protected override bool HasExtraEffect => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Fire, CardKeyword.Exhaust, SakuraKeywords.Stabilize];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(2),
        new EnergyVar(1)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (IsUsingExtraEffect)
        {
            await Virtual(choiceContext);
            await Real(choiceContext);
            return;
        }

        var virtualChoice = SakuraActions.CloneWithCurrentUpgrade<TrueOrFalseDrawChoice>(this);
        var realChoice = SakuraActions.CloneWithCurrentUpgrade<TrueOrFalseEnergyChoice>(this);
        virtualChoice.DynamicVars.Cards.BaseValue = DynamicVars.Cards.IntValue;
        realChoice.DynamicVars.Energy.BaseValue = DynamicVars.Energy.IntValue;

        var choice = await SakuraActions.SelectFromCards(this, choiceContext, [virtualChoice, realChoice], cancelable: false);
        if (choice is TrueOrFalseEnergyChoice)
            await Real(choiceContext);
        else
            await Virtual(choiceContext);
    }

    private async Task Virtual(PlayerChoiceContext choiceContext)
    {
        var card = SakuraActions.Hand(this).Any(CanBecomeTemporary)
            ? await SakuraActions.SelectHandCard(this, choiceContext, CanBecomeTemporary, cancelable: false)
            : null;
        if (card is not null)
            await SakuraGeneratedCardLifecycle.GrantTemporary(choiceContext, card);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner, false);
    }

    private bool CanBecomeTemporary(CardModel card) =>
        card != this && !card.IsTemporary();

    private async Task Real(PlayerChoiceContext choiceContext)
    {
        var card = SakuraActions.StabilizeCandidates(this).Count > 0
            ? await SakuraActions.SelectStabilizeCandidate(this, choiceContext, cancelable: false)
            : null;
        if (card is not null)
            await card.Stabilize(choiceContext);

        await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1);
        DynamicVars.Energy.UpgradeValueBy(1);
    }
}
