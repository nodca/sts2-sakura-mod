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

public class Gravitation() : SakuraExtraEffectCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Earth];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
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
        if (activation.IsActive)
            await ApplyExtraEffect(choiceContext, play);
    }

    private async Task ApplyExtraEffect(PlayerChoiceContext choiceContext, CardPlay play)
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

public class Mirage() : SakuraExtraEffectCard(1, CardType.Skill, CardRarity.Rare, TargetType.AnyEnemy)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Water, CardKeyword.Exhaust];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<MiragePower>()];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        var targets = activation.IsActive
            ? CombatState!.HittableEnemies.ToList()
            : [RequiredTarget(play)];
        await PowerCmd.Apply<MiragePower>(choiceContext, targets, 1, Owner.Creature, this, false);
    }

    protected override void OnUpgrade() => AddKeywordIfMissing(CardKeyword.Retain);
}

public class Record() : SakuraExtraEffectCard(2, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
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

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        if (_restoredDuringCurrentPlay)
            return;

        var result = await RecordPower.RecordOrRestore(choiceContext, Owner.Creature, this);
        if (result == RecordResult.Restored)
            _restoredDuringCurrentPlay = true;

        if (!activation.IsActive)
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

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        await CardPileCmd.Draw(choiceContext, 1, Owner, false);

        var cards = await SakuraActions.SelectHandCards(
            this,
            choiceContext,
            card => card != this && SakuraActions.HasExchangeableEnergyCost(card),
            2);
        if (cards.Count == 2)
        {
            var first = cards[0];
            var second = cards[1];
            SakuraActions.TryExchangeEnergyCosts(first, second, restOfCombat: IsUpgraded);
            first.ExchangeTemporaryState(second);
        }
    }
}

public class Kindness() : SakuraExtraEffectCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Earth, CardKeyword.Exhaust];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        var power = await PowerCmd.Apply<KindnessPower>(
            choiceContext,
            Owner.Creature,
            1,
            Owner.Creature,
            this,
            false);
        power?.QueueEffect(activation.IsActive);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class Labyrinth() : SakuraModCard(2, CardType.Power, CardRarity.Rare, TargetType.None)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Earth];
    internal override IEnumerable<string> ReferencedStaticHoverTipKeys => [SakuraCardHoverTips.LabyrinthTipKey];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
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

public class Repair() : SakuraExtraEffectCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Earth, CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<RegenPower>(3)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        await PowerCmd.Apply<RegenPower>(choiceContext, Owner.Creature, DynamicVars["RegenPower"].IntValue, Owner.Creature, this, false);
        if (activation.IsActive)
            await ApplyExtraEffect(choiceContext, play);
    }

    private async Task ApplyExtraEffect(PlayerChoiceContext choiceContext, CardPlay play) =>
        await PowerCmd.Apply<RepairRegenerationPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this, false);

    protected override void OnUpgrade() => DynamicVars["RegenPower"].UpgradeValueBy(1);
}

public class Reversal() : SakuraExtraEffectCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Wind];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(4, ValueProp.Move),
        new DynamicVar("PileCardsPerDamage", 3),
        new DamageVar("PileDamage", 1, ValueProp.Move),
        new CardsVar(1)
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
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
        if (activation.IsActive)
            await ApplyExtraEffect(choiceContext, play);
    }

    private async Task ApplyExtraEffect(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var handSize = CardPile.Get(PileType.Hand, Owner)!.Cards.Count;
        var cardsToDraw = MaxHandSizeCalculator.Calculate(Owner) - handSize;
        if (cardsToDraw > 0)
            await CardPileCmd.Draw(choiceContext, cardsToDraw, Owner, false);
    }

    protected override void OnUpgrade() => DynamicVars["PileCardsPerDamage"].UpgradeValueBy(-1);
}

public class Rewind() : SakuraExtraEffectCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Earth, CardKeyword.Exhaust];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        var card = await SakuraActions.SelectFromCards(this, choiceContext, CardPile.Get(PileType.Exhaust, Owner)!.Cards, cancelable: false);
        if (card is null)
            return;

        await SakuraActions.MoveExistingCardToHand(this, card);
        if (activation.IsActive)
        {
            card.SetToFreeThisTurn();
        }
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class Snooze() : SakuraExtraEffectCard(1, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Wind];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<WeakPower>(2), new PowerVar<SakuraSleepPower>(2)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        var target = RequiredTarget(play);
        await PowerCmd.Apply<WeakPower>(choiceContext, target, DynamicVars.Weak.IntValue, Owner.Creature, this, false);
        await CardPileCmd.Draw(choiceContext, 1, Owner, false);
        if (activation.IsActive)
            await ApplyExtraEffect(choiceContext, play);
    }

    private async Task ApplyExtraEffect(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = play.Target ?? CombatState?.HittableEnemies.FirstOrDefault();
        if (target is not null)
            await PowerCmd.Apply<SakuraSleepPower>(choiceContext, target, DynamicVars["SakuraSleepPower"].IntValue, Owner.Creature, this, false);
    }

    protected override void OnUpgrade() => DynamicVars.Weak.UpgradeValueBy(1);
}

public class Spiral() : SakuraExtraEffectCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Wind];
    internal override IEnumerable<string> ReferencedStaticHoverTipKeys => [SakuraMemoryPile.PileId];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(4, ValueProp.Move),
        new BlockVar(4, ValueProp.Move),
        new DynamicVar("MemoryScale", 1),
        new CardsVar("ExtraCopies", 2)
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        var target = RequiredTarget(play);
        var memoryCount = SakuraMemoryPile.Count(Owner);
        var memoryScale = DynamicVars["MemoryScale"].IntValue;
        var damage = OutputWithMemory(DynamicVars.Damage.IntValue, memoryCount, memoryScale);
        var block = OutputWithMemory(DynamicVars.Block.IntValue, memoryCount, memoryScale);
        await SakuraActions.Attack(choiceContext, this, target, damage);
        await CreatureCmd.GainBlock(Owner.Creature, block, ValueProp.Move, play, false);
        if (activation.IsActive)
            await ApplyExtraEffect(choiceContext, play);
    }

    private async Task ApplyExtraEffect(PlayerChoiceContext choiceContext, CardPlay play)
    {
        for (var i = 0; i < DynamicVars["ExtraCopies"].IntValue; i++)
        {
            await SakuraGeneratedCardLifecycle.AddTemporaryCopyToHand(
                this,
                freeThisTurn: true,
                context: choiceContext);
        }
    }

    internal static int OutputWithMemory(int baseValue, int memoryCount, int memoryScale = 1) =>
        baseValue + Math.Max(0, memoryCount) * Math.Max(0, memoryScale);

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2);
        DynamicVars.Block.UpgradeValueBy(2);
    }
}

public class Transfer() : SakuraExtraEffectCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Wind, CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("EnemyStrengthLoss", 2),
        new DynamicVar("StrengthGain", 1),
        new DynamicVar("DexterityGain", 1)
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        ApplyExtraEffectExhaustChange(activation);
        var target = RequiredTarget(play);
        await PowerCmd.Apply<StrengthPower>(choiceContext, target, -DynamicVars["EnemyStrengthLoss"].IntValue, Owner.Creature, this, false);
        await PowerCmd.Apply<StrengthPower>(choiceContext, Owner.Creature, DynamicVars["StrengthGain"].IntValue, Owner.Creature, this, false);
        await PowerCmd.Apply<DexterityPower>(choiceContext, Owner.Creature, DynamicVars["DexterityGain"].IntValue, Owner.Creature, this, false);
    }

    protected override PileType GetResultPileTypeForCardPlay() =>
        SakuraModCard.UsesMagicChargeExtraEffect(this)
            ? PileType.Discard
            : base.GetResultPileTypeForCardPlay();

    private void ApplyExtraEffectExhaustChange(SakuraExtraEffectActivation activation)
    {
        if (activation.IsActive)
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




































public class Blank() : SakuraExtraEffectCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    private static readonly PileType[] ForgottenTargetPileTypes =
    [
        PileType.Hand,
        PileType.Draw,
        PileType.Discard
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Earth, CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(4, ValueProp.Move)];
    internal static IReadOnlyList<PileType> TargetPileTypes => ForgottenTargetPileTypes;

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block.IntValue, ValueProp.Move, play, false);

        var cards = CardPile.GetCards(Owner, ForgottenTargetPileTypes)
            .Where(card => CanGainForgotten(card.Type, card.IsTemporary()))
            .ToList();

        var forgottenCards = 0;
        foreach (var card in cards)
        {
            if (await SakuraGeneratedCardLifecycle.GrantTemporary(choiceContext, card))
                forgottenCards++;
        }

        if (forgottenCards > 0)
        {
            await PowerCmd.Apply<DrawCardsNextTurnPower>(
                choiceContext,
                Owner.Creature,
                forgottenCards,
                Owner.Creature,
                this,
                false);
        }

        if (activation.IsActive)
            await ApplyExtraEffect();
    }

    internal static bool CanGainForgotten(CardType type, bool isForgotten) =>
        !isForgotten && type is CardType.Status or CardType.Curse;

    private async Task ApplyExtraEffect()
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

public class Mirror() : SakuraExtraEffectCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Water, CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new RepeatVar(1), new RepeatVar("ExtraRepeat", 1)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        var card = await SakuraActions.SelectHandCard(this, choiceContext, card => card != this);
        if (card is not null)
        {
            var amount = DynamicVars["Repeat"].IntValue;
            if (activation.IsActive)
                amount += DynamicVars["ExtraRepeat"].IntValue;
            card.BaseReplayCount += amount;
        }
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class Remind() : SakuraModCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    private static LocString SelectionPrompt => CardLoc<Remind>("selectionPrompt");

    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Fire];
    internal override IEnumerable<string> ReferencedStaticHoverTipKeys =>
        [SakuraMemoryPile.PileId, SakuraCardHoverTips.RemindTipKey];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        var choices = SakuraMemoryPile.Get(Owner)?.Cards.ToList() ?? [];
        var recallCount = DynamicVars.Cards.IntValue;
        var cards = (choices.Count <= recallCount
            ? choices.ToList()
            : await SakuraActions.SelectUpToFromCards(
                this,
                choiceContext,
                choices,
                recallCount,
                cancelable: false,
                prompt: SelectionPrompt,
                minSelect: recallCount)).ToList();

        var copies = await SakuraMemoryPile.Consume(Owner, cards);

        try
        {
            foreach (var copy in copies)
            {
                await SakuraGeneratedCardLifecycle.AddTemporaryRememberedCardToHand(
                    copy,
                    freeThisTurn: true,
                    context: choiceContext);
            }
        }
        finally
        {
            SakuraGeneratedCardLifecycle.RemoveDetachedGeneratedChoices(copies);
        }
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1);
}

public class Synchronize() : SakuraModCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Wind];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        var cards = await SakuraActions.SelectHandCards(
            this,
            choiceContext,
            card => CanSynchronize(this, card),
            2,
            pretendCardsCanBePlayed: true);
        if (cards.Count != 2)
            return;

        cards[0].SynchronizeWith(cards[1]);
    }

    internal static bool CanSynchronize(CardModel source, CardModel candidate) =>
        candidate != source && !candidate.Keywords.Contains(CardKeyword.Unplayable);

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class Time() : SakuraExtraEffectCard(3, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Fire, CardKeyword.Exhaust];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        SakuraCardPlayVfx.PlayTime(Owner.Creature);
        var power = await PowerCmd.Apply<TimeStopPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this, true);
        if (activation.IsActive)
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

public class TrueOrFalse() : SakuraExtraEffectCard(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => IsUpgraded
        ? [SakuraKeywords.Fire, SakuraKeywords.Stabilize]
        : [SakuraKeywords.Fire, CardKeyword.Exhaust, SakuraKeywords.Stabilize];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(2),
        new EnergyVar(2)
    ];
    protected override bool IsPlayable => SakuraModCard.UsesMagicChargeExtraEffect(this)
        ? CanCompleteExtraEffect()
        : CanUseVirtual() || CanUseReal();

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        if (activation.IsActive)
        {
            if (await Virtual(choiceContext))
                await Real(choiceContext);
            return;
        }

        List<CardModel> choices = [];
        if (CanUseVirtual())
        {
            var virtualChoice = SakuraActions.CloneWithCurrentUpgrade<TrueOrFalseDrawChoice>(this);
            virtualChoice.DynamicVars.Cards.BaseValue = DynamicVars.Cards.IntValue;
            choices.Add(virtualChoice);
        }
        if (CanUseReal())
        {
            var realChoice = SakuraActions.CloneWithCurrentUpgrade<TrueOrFalseEnergyChoice>(this);
            realChoice.DynamicVars.Energy.BaseValue = DynamicVars.Energy.IntValue;
            choices.Add(realChoice);
        }

        var choice = choices.Count == 1
            ? choices[0]
            : await SakuraActions.SelectFromCards(this, choiceContext, choices, cancelable: false);
        if (choice is TrueOrFalseDrawChoice)
            await Virtual(choiceContext);
        else if (choice is TrueOrFalseEnergyChoice)
            await Real(choiceContext);
    }

    private bool CanUseVirtual() => SakuraActions.Hand(this).Any(CanBecomeTemporary);

    private bool CanUseReal() => SakuraActions.StabilizeCandidates(this).Count > 0;

    private bool CanCompleteExtraEffect() => CanUseVirtual();

    private async Task<bool> Virtual(PlayerChoiceContext choiceContext)
    {
        var card = await SakuraActions.SelectHandCard(this, choiceContext, CanBecomeTemporary, cancelable: false);
        if (card is null || !await SakuraGeneratedCardLifecycle.GrantTemporary(choiceContext, card))
            return false;

        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner, false);
        return true;
    }

    private bool CanBecomeTemporary(CardModel card) =>
        card != this && !card.IsTemporary();

    private async Task<bool> Real(PlayerChoiceContext choiceContext)
    {
        var card = await SakuraActions.SelectStabilizeCandidate(this, choiceContext, cancelable: false);
        if (card is null)
            return false;

        await card.Stabilize(choiceContext);
        if (card.IsTemporary())
            return false;

        await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner);
        return true;
    }

    protected override void OnUpgrade() => RemoveKeywordIfPresent(CardKeyword.Exhaust);
}
