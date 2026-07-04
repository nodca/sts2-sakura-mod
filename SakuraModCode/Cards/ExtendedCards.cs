using BaseLib.Utils;
using BaseLib.Patches.Hooks;
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
using SakuraMod.SakuraModCode.Powers;

namespace SakuraMod.SakuraModCode.Cards;

public class Gravitation() : SakuraModCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.AllEnemies), IReleaseable
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Earth];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("StrengthLoss", 2),
        new DynamicVar("ReleaseReturns", 2)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        SakuraCardPlayVfx.PlayGravitation(CombatState!.HittableEnemies);
        await PowerCmd.Apply<SakuraTemporaryStrengthPower>(choiceContext, CombatState!.HittableEnemies, DynamicVars["StrengthLoss"].IntValue, Owner.Creature, this, false);
        await TriggerReleaseEffect(choiceContext, play);
    }

    public async Task OnReleased(PlayerChoiceContext choiceContext, CardPlay play) =>
        await PowerCmd.Apply<GravitationHoldPower>(choiceContext, Owner.Creature, DynamicVars["ReleaseReturns"].IntValue, Owner.Creature, this, false);

    protected override void OnUpgrade() => DynamicVars["StrengthLoss"].UpgradeValueBy(1);
}

public class Mirage() : SakuraModCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Water, CardKeyword.Exhaust];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => HoverTipFactory.FromCardWithCardHoverTips<MirageImage>(IsUpgraded);
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<BufferPower>(1)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PowerCmd.Apply<BufferPower>(choiceContext, Owner.Creature, DynamicVars["BufferPower"].IntValue, Owner.Creature, this, false);
        var image = CreateMirageImage();
        if (IsUpgraded)
            CardCmd.Upgrade(image, CardPreviewStyle.None);

        var targetPile = ShouldRelease ? PileType.Hand : PileType.Discard;
        var result = await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombatWithResult(
            image,
            new GeneratedCardOptions
            {
                AddTemporary = this.IsTemporary(),
                Pile = targetPile,
                Position = CardPilePosition.Random
            },
            choiceContext);
        if (targetPile != PileType.Hand)
            CardCmd.PreviewCardPileAdd(result);
    }

    private MirageImage CreateMirageImage() =>
        (MirageImage?)CardScope?.CreateCard(ModelDb.Card<MirageImage>(), Owner)
        ?? throw new InvalidOperationException("Cannot create Mirage Image without a card scope.");
}

public class MirageImage() : SakuraModCard(0, CardType.Skill, CardRarity.Basic, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<MirageImagePower>(4)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play) =>
        await PowerCmd.Apply<MirageImagePower>(choiceContext, Owner.Creature, DynamicVars["MirageImagePower"].IntValue, Owner.Creature, this, false);

    protected override void OnUpgrade() => DynamicVars["MirageImagePower"].UpgradeValueBy(2);
}

public class Record() : SakuraModCard(2, CardType.Skill, CardRarity.Rare, TargetType.Self)
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

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (_restoredDuringCurrentPlay)
            return;

        var result = await RecordPower.RecordOrRestore(choiceContext, Owner.Creature, this);
        if (result == RecordResult.Restored)
            _restoredDuringCurrentPlay = true;

        if (!ShouldRelease)
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

public class KeroRecon() : SakuraModCard(0, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    private static readonly LocString SelectionPrompt = new("cards", "SAKURAMOD-KERO_RECON.selectionPrompt");

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar("Look", 3)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var top = CardPile.Get(PileType.Draw, Owner)!.Cards.Take(DynamicVars["Look"].IntValue).ToList();
        var cards = await SakuraActions.SelectUpToFromCardPreviews(this, choiceContext, top, top.Count, prompt: SelectionPrompt, minSelect: 0);
        foreach (var card in cards)
            await CardCmd.Exhaust(choiceContext, card);
    }

    protected override void OnUpgrade() => DynamicVars["Look"].UpgradeValueBy(1);
}

public class KeroSnackBreak() : SakuraModCard(0, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (IsUpgraded)
            await CardPileCmd.Draw(choiceContext, 1, Owner, false);
        await PlayerCmd.GainEnergy(1, Owner);
        if (SakuraActions.HasPlayedReleasedCardEarlierThisTurn(Owner))
            await CardPileCmd.Draw(choiceContext, 1, Owner, false);
    }
}

public class KeroBond() : SakuraModCard(2, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<KeroBondPower>(2)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PowerCmd.Apply<KeroBondPower>(choiceContext, Owner.Creature, DynamicVars["KeroBondPower"].IntValue, Owner.Creature, this, false);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class MagicAwakening() : SakuraModCard(2, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Manifest];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<MagicAwakeningPower>(1)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play) =>
        await PowerCmd.Apply<MagicAwakeningPower>(choiceContext, Owner.Creature, DynamicVars["MagicAwakeningPower"].IntValue, Owner.Creature, this, false);

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
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
            if (ShouldRelease)
                first.ExchangeReleaseState(second);
        }
    }
}

public class Kindness() : SakuraModCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Earth, CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new HealVar(3)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await CreatureCmd.Heal(Owner.Creature, DynamicVars.Heal.IntValue);
        await SakuraManifestLoop.DiscoverGenerated(
            this,
            choiceContext,
            SakuraCardCatalog.PartnerTemplates(),
            freeThisTurn: ShouldRelease,
            upgraded: IsUpgraded);
    }

    protected override void OnUpgrade() => DynamicVars.Heal.UpgradeValueBy(3);
}

public class Labyrinth() : SakuraModCard(2, CardType.Skill, CardRarity.Rare, TargetType.AnyEnemy)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Earth, CardKeyword.Exhaust];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var targets = ShouldRelease
            ? CombatState!.HittableEnemies.ToList()
            : [RequiredTarget(play)];
        SakuraCardPlayVfx.PlayLabyrinth(targets);
        await SakuraActions.SuppressAliveEnemyActions(targets);
    }

    protected override void OnUpgrade() => AddKeywordIfMissing(CardKeyword.Retain);
}

public class Repair() : SakuraModCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self), IReleaseable
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Earth, CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<RegenPower>(3)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PowerCmd.Apply<RegenPower>(choiceContext, Owner.Creature, DynamicVars["RegenPower"].IntValue, Owner.Creature, this, false);
        await TriggerReleaseEffect(choiceContext, play);
    }

    public async Task OnReleased(PlayerChoiceContext choiceContext, CardPlay play) =>
        await PowerCmd.Apply<RepairRegenerationPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this, false);

    protected override void OnUpgrade() => DynamicVars["RegenPower"].UpgradeValueBy(1);
}

public class Reversal() : SakuraModCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy), IReleaseable
{
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
        await TriggerReleaseEffect(choiceContext, play);
    }

    public async Task OnReleased(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var handSize = CardPile.Get(PileType.Hand, Owner)!.Cards.Count;
        var cardsToDraw = MaxHandSizePatch.GetMaxHandSize(Owner, MaxHandSizePatch.DefaultMaxHandSize) - handSize;
        if (cardsToDraw > 0)
            await CardPileCmd.Draw(choiceContext, cardsToDraw, Owner, false);
    }

    protected override void OnUpgrade() => DynamicVars["PileCardsPerDamage"].UpgradeValueBy(-1);
}

public class Rewind() : SakuraModCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Earth, CardKeyword.Exhaust];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var card = await SakuraActions.SelectFromCards(this, choiceContext, CardPile.Get(PileType.Exhaust, Owner)!.Cards, cancelable: false);
        if (card is null)
            return;

        await SakuraActions.MoveExistingCardToHand(this, card);
        await TriggerReleaseEffect(() =>
        {
            card.SetToFreeThisTurn();
            return Task.CompletedTask;
        });
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class Snooze() : SakuraModCard(1, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy), IReleaseable
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Wind];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<WeakPower>(2), new PowerVar<SakuraSleepPower>(2)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = RequiredTarget(play);
        await PowerCmd.Apply<WeakPower>(choiceContext, target, DynamicVars.Weak.IntValue, Owner.Creature, this, false);
        await CardPileCmd.Draw(choiceContext, 1, Owner, false);
        await TriggerReleaseEffect(choiceContext, play);
    }

    public async Task OnReleased(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = play.Target ?? CombatState?.HittableEnemies.FirstOrDefault();
        if (target is not null)
            await PowerCmd.Apply<SakuraSleepPower>(choiceContext, target, DynamicVars["SakuraSleepPower"].IntValue, Owner.Creature, this, false);
    }

    protected override void OnUpgrade() => DynamicVars.Weak.UpgradeValueBy(1);
}

public class Spiral() : SakuraModCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy), IReleaseable
{
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
        await TriggerReleaseEffect(choiceContext, play);
    }

    public async Task OnReleased(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await SakuraManifestLoop.AddTemporaryCopyToHand(this, choiceContext, this, release: false, freeThisTurn: false);
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

public class Transfer() : SakuraModCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy), IReleaseStateObserver
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Wind, CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("EnemyStrengthLoss", 2),
        new DynamicVar("StrengthGain", 1),
        new DynamicVar("DexterityGain", 1)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        OnReleaseStateChanged();
        var target = RequiredTarget(play);
        await PowerCmd.Apply<StrengthPower>(choiceContext, target, -DynamicVars["EnemyStrengthLoss"].IntValue, Owner.Creature, this, false);
        await PowerCmd.Apply<StrengthPower>(choiceContext, Owner.Creature, DynamicVars["StrengthGain"].IntValue, Owner.Creature, this, false);
        await PowerCmd.Apply<DexterityPower>(choiceContext, Owner.Creature, DynamicVars["DexterityGain"].IntValue, Owner.Creature, this, false);
    }

    public void OnReleaseStateChanged()
    {
        if (ShouldRelease)
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

public class AkihoDream() : SakuraModCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Manifest];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var manifested = await SakuraManifestLoop.Manifest(this, choiceContext, DynamicVars.Cards.IntValue);
        if (IsUpgraded)
            foreach (var card in manifested)
                await SakuraActions.ReduceCostThisTurn(choiceContext, this, card);

        var cards = SakuraActions.Hand(this)
            .Where(card => card != this)
            .ToList();
        var chosen = cards.Count > 0
            ? await SakuraActions.SelectFromCards(this, choiceContext, cards)
            : null;
        if (chosen is not null)
            await CardPileCmd.Add(chosen, PileType.Draw, CardPilePosition.Top, this, skipVisuals: false);

        await PowerCmd.Apply<SakuraDrawNextTurnPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this, false);
    }

    protected override void OnUpgrade() {}
}

public class ClockCountryAlice() : SakuraModCard(2, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips => HoverTipFactory.FromCardWithCardHoverTips<AliceReading>(IsUpgraded);
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<ClockCountryAlicePower>(ClockCountryAlicePower.ReadingMode(upgradedReading: false))
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var mode = DynamicVars["ClockCountryAlicePower"].IntValue;
        var power = await PowerCmd.Apply<ClockCountryAlicePower>(choiceContext, Owner.Creature, mode, Owner.Creature, this, false);
        if (power is not null && power.Amount < mode)
            await PowerCmd.ModifyAmount(choiceContext, power, mode - power.Amount, Owner.Creature, this, false);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
        DynamicVars["ClockCountryAlicePower"].UpgradeValueBy(1);
    }
}

public class AliceReading() : SakuraModCard(1, CardType.Skill, CardRarity.Basic, TargetType.AnyEnemy)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar("StoredDamage", 0, ValueProp.Move),
        new BlockVar("StoredBlock", 0, ValueProp.Move)
    ];

    public void SetStoredValues(int damage, int block)
    {
        DynamicVars["StoredDamage"].BaseValue = Math.Max(0, damage);
        DynamicVars["StoredBlock"].BaseValue = Math.Max(0, block);
        InvokeEnergyCostChanged();
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var power = Owner.Creature.GetPower<ClockCountryAlicePower>();
        if (power is null)
            return;

        await power.ReleaseStoredValues(choiceContext, this, RequiredTarget(play), play);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class ForbiddenMagic() : SakuraModCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    private static readonly LocString SelectionPrompt = new("cards", "SAKURAMOD-FORBIDDEN_MAGIC.selectionPrompt");

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new EnergyVar(1), new CardsVar(1), new DynamicVar("MaxCards", 3)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var eligibleCards = SakuraActions.Hand(this)
            .Where(card => SakuraCardCatalog.IsTransparentCard(card) && !card.IsTemporary())
            .ToList();
        var selectedCards = await SakuraActions.SelectUpToFromCards(
            this,
            choiceContext,
            eligibleCards,
            DynamicVars["MaxCards"].IntValue,
            prompt: SelectionPrompt,
            minSelect: 0);

        var grantedCount = 0;
        foreach (var card in selectedCards)
            if (await SakuraManifestLoop.GrantTemporary(choiceContext, card))
                grantedCount++;

        if (grantedCount == 0)
            return;

        await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue * grantedCount, Owner);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue * grantedCount, Owner, false);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class DWatch() : SakuraModCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var candidates = CardPile.Get(PileType.Discard, Owner)!.Cards
            .Where(card => !card.IsTemporary())
            .ToList();
        var card = await SakuraActions.SelectFromCards(this, choiceContext, candidates, cancelable: false);
        if (card is null)
            return;

        await SakuraActions.MoveExistingCardToHand(this, card);
        card.SetToFreeThisTurn();
        await SakuraManifestLoop.GrantTemporary(choiceContext, card);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class FalseDailyLife() : SakuraModCard(1, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(FalseDailyLifePower.DamageAmount, ValueProp.Move),
        new BlockVar(FalseDailyLifePower.BlockAmount, ValueProp.Move)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var power = Owner.Creature.GetPower<FalseDailyLifePower>();
        if (power is null)
        {
            await PowerCmd.Apply<FalseDailyLifePower>(choiceContext, Owner.Creature, DynamicVars.Damage.IntValue, Owner.Creature, this, false);
            return;
        }

        if (power.Amount < DynamicVars.Damage.IntValue)
            await PowerCmd.ModifyAmount(choiceContext, power, DynamicVars.Damage.IntValue - power.Amount, Owner.Creature, this, false);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2);
        DynamicVars.Block.UpgradeValueBy(1);
    }
}

public class StarlightChant() : SakuraModCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Catalog];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CalculationBaseVar(5),
        new ExtraDamageVar(2),
        new CalculatedDamageVar(ValueProp.Move).WithMultiplier(TechniqueDamageRules.CatalogCount)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = RequiredTarget(play);
        await SakuraActions.Attack(choiceContext, this, target, DynamicVars.CalculatedDamage);
    }

    protected override void OnUpgrade() => DynamicVars.ExtraDamage.UpgradeValueBy(1);
}

public class Archive() : SakuraModCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Catalog];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play) =>
        await SakuraManifestLoop.AddRandomUncatalogedTemporaryClearCardToHand(this, choiceContext);

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class GrowingMagic() : SakuraModCard(1, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Stabilize];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<GrowingMagicPower>(1)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play) =>
        await PowerCmd.Apply<GrowingMagicPower>(choiceContext, Owner.Creature, DynamicVars["GrowingMagicPower"].IntValue, Owner.Creature, this, false);

    protected override void OnUpgrade() => AddKeywordIfMissing(CardKeyword.Innate);
}

public class ReleaseChant() : SakuraModCard(1, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (!SakuraActions.Hand(this).Any(CanRelease))
        {
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner, false);
            return;
        }

        var card = await SakuraActions.SelectHandCard(this, choiceContext, CanRelease, cancelable: false);
        if (card is not null)
            await SakuraActions.ReleaseThisTurnAndRecord(choiceContext, card);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner, false);
    }

    private static bool CanRelease(CardModel card) =>
        SakuraCardCatalog.IsTransparentCard(card) && !card.IsReleased();

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class CardBookSorting() : SakuraModCard(1, CardType.Skill, CardRarity.Rare, TargetType.AnyEnemy)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Burn];
    internal override IEnumerable<CardKeyword> ReferencedKeywords => [SakuraKeywords.Fire];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<SakuraBurnPower>(1),
        new DynamicVar("FireElements", 1)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PowerCmd.Apply<SakuraBurnPower>(
            choiceContext,
            RequiredTarget(play),
            DynamicVars["SakuraBurnPower"].IntValue,
            Owner.Creature,
            this,
            false);
        await SakuraActions.GainElementCountThisTurn(choiceContext, Owner, SakuraElement.Fire, DynamicVars["FireElements"].IntValue);

        var fireCount = SakuraActions.PlayedElementCount(Owner, SakuraElement.Fire);
        for (var i = 0; i < fireCount; i++)
            await SakuraActions.TriggerTalismanEffect(choiceContext, Owner, SakuraElement.Fire, play, this);
    }

    protected override void OnUpgrade() => DynamicVars["FireElements"].UpgradeValueBy(1);
}

public class NamelessMagic() : SakuraModCard(2, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Catalog];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CalculationBaseVar(0),
        new ExtraDamageVar(3),
        new CalculatedDamageVar(ValueProp.Move).WithMultiplier(TechniqueDamageRules.CatalogCount)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await SakuraActions.Attack(choiceContext, this, CombatState!.HittableEnemies.ToList(), DynamicVars.CalculatedDamage);
    }

    protected override void OnUpgrade() => DynamicVars.ExtraDamage.UpgradeValueBy(1);
}

public class ChainPhenomenon() : SakuraModCard(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CalculationBaseVar(4),
        new ExtraDamageVar(2),
        new CalculatedDamageVar(ValueProp.Move).WithMultiplier(TechniqueDamageRules.ClearCardTypesPlayedThisTurn)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = RequiredTarget(play);
        await SakuraActions.Attack(choiceContext, this, target, DynamicVars.CalculatedDamage);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.CalculationBase.UpgradeValueBy(1);
        DynamicVars.ExtraDamage.UpgradeValueBy(1);
    }
}

internal static class TechniqueDamageRules
{
    public static decimal CatalogCount(CardModel card, Creature? target) =>
        card.Owner is { } owner
            ? Math.Max(0, SakuraManifestLoop.CatalogCount(owner))
            : 0;

    public static decimal ClearCardTypesPlayedThisTurn(CardModel card, Creature? target) =>
        card.Owner is { } owner
            ? Math.Max(0, SakuraActions.DistinctCardTypesPlayedThisTurn(owner, SakuraCardCatalog.IsTransparentCard, card))
            : 0;
}

public class MagicSurge() : SakuraModCard(1, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Manifest];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<MagicSurgePower>(1)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play) =>
        await PowerCmd.Apply<MagicSurgePower>(choiceContext, Owner.Creature, DynamicVars["MagicSurgePower"].IntValue, Owner.Creature, this, false);

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class MagicTuning() : SakuraModCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Stabilize];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (!SakuraActions.Hand(this).Any(CanTune))
            return;

        var card = await SakuraActions.SelectHandCard(this, choiceContext, CanTune, cancelable: false);
        if (card is null)
            return;

        if (card.IsTemporary())
            await card.Stabilize(choiceContext);
        else
            await SakuraActions.ReleaseThisTurnAndRecord(choiceContext, card);
    }

    private static bool CanTune(CardModel card) =>
        SakuraCardCatalog.IsTransparentCard(card)
        && (card.IsTemporary()
            ? card.CanStabilize()
            : !card.IsReleased());

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class DreamWandCombo() : SakuraModCard(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Fire, SakuraKeywords.Burn];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(14, ValueProp.Move),
        new PowerVar<SakuraBurnPower>(2)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = RequiredTarget(play);
        await SakuraActions.Attack(choiceContext, this, target, DynamicVars.Damage.IntValue);
        if (target.IsAlive)
            await PowerCmd.Apply<SakuraBurnPower>(choiceContext, target, DynamicVars["SakuraBurnPower"].IntValue, Owner.Creature, this, false);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4);
        DynamicVars["SakuraBurnPower"].UpgradeValueBy(1);
    }
}

public class CompassTracking() : SakuraModCard(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(6, ValueProp.Move),
        new DamageVar("ElementDamage", 3, ValueProp.Move)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = RequiredTarget(play);
        var elementTypesPlayed = SakuraActions.PlayedElementsThisTurn(Owner).Distinct().Count();
        var damage = DynamicVars.Damage.IntValue + elementTypesPlayed * DynamicVars["ElementDamage"].IntValue;
        await SakuraActions.Attack(choiceContext, this, target, damage);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
}

public class ThunderEmperorSummon() : SakuraModCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Wind, SakuraKeywords.Fire];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(8, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play) =>
        await SakuraActions.Attack(choiceContext, this, RequiredTarget(play), DynamicVars.Damage.IntValue);

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
}

public class MeilingComboKick() : SakuraModCard(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips => HoverTipFactory.FromCardWithCardHoverTips<Struggle>();
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(3, ValueProp.Move),
        new DynamicVar("Hits", 2),
        new CardsVar(1)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await SakuraActions.Attack(choiceContext, this, RequiredTarget(play), DynamicVars.Damage.IntValue, hitCount: DynamicVars["Hits"].IntValue);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner, false);
        await SakuraManifestLoop.AddTemporaryReleasedCardToCombat(CreateReleasedStruggle(), choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(1);

    private Struggle CreateReleasedStruggle() =>
        (Struggle?)CardScope?.CreateCard(ModelDb.Card<Struggle>(), Owner)
        ?? throw new InvalidOperationException("Cannot create Struggle without a card scope.");
}

public class TalismanCombo() : SakuraModCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var power = await PowerCmd.Apply<TalismanComboPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this, false);
        power?.StartAfter(play);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class SilverMoonWing() : SakuraModCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(6, ValueProp.Move),
        new BlockVar("ReleaseBlock", 6, ValueProp.Move),
        new PowerVar<WeakPower>(1)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block.IntValue, ValueProp.Move, play, false);
        if (!SakuraActions.HasPlayedReleasedCardEarlierThisTurn(Owner))
            return;

        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars["ReleaseBlock"].IntValue, ValueProp.Move, play, false);
        await PowerCmd.Apply<WeakPower>(choiceContext, CombatState!.HittableEnemies.ToList(), DynamicVars.Weak.IntValue, Owner.Creature, this, false);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2);
        DynamicVars["ReleaseBlock"].UpgradeValueBy(2);
    }
}

public class BigBrotherSense() : SakuraModCard(1, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(5, ValueProp.Move),
        new BlockVar("AttackBlock", 8, ValueProp.Move)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var block = CombatState!.Enemies.Any(SakuraActions.IntendsToAttack)
            ? DynamicVars["AttackBlock"].IntValue
            : DynamicVars.Block.IntValue;
        await CreatureCmd.GainBlock(Owner.Creature, block, ValueProp.Move, play, false);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2);
        DynamicVars["AttackBlock"].UpgradeValueBy(2);
    }
}

public class YukitoLunchBox() : SakuraModCard(1, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(6, ValueProp.Move),
        new HealVar(3)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block.IntValue, ValueProp.Move, play, false);
        await CreatureCmd.Heal(Owner.Creature, DynamicVars.Heal.IntValue);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3);
}

public class MomoContract() : SakuraModCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Stabilize];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (!SakuraActions.Hand(this).Any(CanContract))
            return;

        var card = await SakuraActions.SelectHandCard(this, choiceContext, CanContract, cancelable: false);
        if (card is null)
            return;

        await card.Stabilize(choiceContext);
        await SakuraActions.ReleaseThisTurnAndRecord(choiceContext, card);
    }

    private static bool CanContract(CardModel card) =>
        card.IsTemporary() && card.CanStabilize();

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class YamazakiTallTale() : SakuraModCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (!SakuraActions.Hand(this).Any(card => card.IsTemporary()))
            return;

        var card = await SakuraActions.SelectHandCard(this, choiceContext, card => card.IsTemporary(), cancelable: false);
        if (card is not null)
            await SakuraManifestLoop.AddTemporaryCopyToHand(this, choiceContext, card, release: false, freeThisTurn: IsUpgraded, preserveRelease: true);
    }

    protected override void OnUpgrade() {}
}

public class FujitakaNote() : SakuraModCard(1, CardType.Skill, CardRarity.Common, TargetType.AllEnemies)
{
    internal override IEnumerable<CardKeyword> ReferencedKeywords => [SakuraKeywords.Water];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<SakuraFrostbitePower>()];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<SakuraFrostbitePower>(10),
        new DynamicVar("WaterElements", 1)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var targets = CombatState!.HittableEnemies.ToList();
        await PowerCmd.Apply<SakuraFrostbitePower>(choiceContext, targets, DynamicVars["SakuraFrostbitePower"].IntValue, Owner.Creature, this, false);
        await SakuraActions.GainElementCountThisTurn(choiceContext, Owner, SakuraElement.Water, DynamicVars["WaterElements"].IntValue);

        var waterCount = SakuraActions.PlayedElementCount(Owner, SakuraElement.Water);
        for (var i = 0; i < waterCount; i++)
            await SakuraActions.TriggerTalismanEffect(choiceContext, Owner, SakuraElement.Water, play, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["SakuraFrostbitePower"].UpgradeValueBy(10);
        DynamicVars["WaterElements"].UpgradeValueBy(1);
    }
}

public class NaokoGhostStory() : SakuraModCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.AllEnemies)
{
    private const int VulnerableCatalogThreshold = 4;
    private const int ExtraDebuffCatalogThreshold = 8;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Catalog];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(4, ValueProp.Move),
        new PowerVar<WeakPower>(1),
        new PowerVar<VulnerablePower>(1)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var targets = CombatState!.HittableEnemies.ToList();
        var catalogCount = SakuraManifestLoop.CatalogCount(Owner);

        await PowerCmd.Apply<WeakPower>(choiceContext, targets, DynamicVars.Weak.IntValue, Owner.Creature, this, false);
        await SakuraActions.Attack(choiceContext, this, targets, DynamicVars.Damage.IntValue);

        var survivingTargets = targets.Where(enemy => enemy.IsAlive).ToList();
        if (catalogCount >= VulnerableCatalogThreshold)
            await PowerCmd.Apply<VulnerablePower>(choiceContext, survivingTargets, DynamicVars.Vulnerable.IntValue, Owner.Creature, this, false);

        if (catalogCount < ExtraDebuffCatalogThreshold)
            return;

        survivingTargets = survivingTargets.Where(enemy => enemy.IsAlive).ToList();
        await PowerCmd.Apply<WeakPower>(choiceContext, survivingTargets, DynamicVars.Weak.IntValue, Owner.Creature, this, false);
        await PowerCmd.Apply<VulnerablePower>(choiceContext, survivingTargets, DynamicVars.Vulnerable.IntValue, Owner.Creature, this, false);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class SyaoranTalisman() : SakuraModCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await SakuraActions.Attack(choiceContext, this, RequiredTarget(play), DynamicVars.Damage.IntValue);
        var power = await PowerCmd.Apply<SyaoranTalismanPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this, false);
        power?.Ignore(play);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class DaoistSupport() : SakuraModCard(0, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var card = await SakuraActions.SelectHandCard(this, choiceContext, SakuraCardCatalog.IsTransparentCard, cancelable: false);
        if (card is null)
            return;

        SakuraActions.GrantElementsThisTurn(card, SakuraElementSet.All);
        if (IsUpgraded)
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner, false);
    }

    protected override void OnUpgrade() {}
}

public class SyaoranBond() : SakuraModCard(2, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PowerCmd.Apply<SyaoranBondPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this, false);
        await SakuraActions.PutSyaoranTalismanInHand(Owner, IsUpgraded, this);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class TomoyoCamera() : SakuraModCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var playedCards = SakuraActions.CardsPlayedThisTurn(Owner, this);
        var cards = await SakuraActions.SelectUpToFromCards(this, choiceContext, playedCards, DynamicVars.Cards.IntValue);

        foreach (var card in cards)
            await SakuraManifestLoop.AddTemporaryCopyToHand(this, choiceContext, card, release: false, freeThisTurn: SakuraCardCatalog.IsPartnerCard(card));
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1);
}

public class TomoyoBond() : SakuraModCard(2, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<TomoyoBondPower>(1)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play) =>
        await PowerCmd.Apply<TomoyoBondPower>(choiceContext, Owner.Creature, DynamicVars["TomoyoBondPower"].IntValue, Owner.Creature, this, false);

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class DreamKeyGlow() : SakuraModCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Manifest];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(5, ValueProp.Move), new CardsVar(1)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block.IntValue, ValueProp.Move, play, false);
        var cards = await SakuraManifestLoop.Manifest(this, choiceContext, DynamicVars.Cards.IntValue);
        foreach (var card in cards)
            await SakuraActions.ReleaseThisTurnAndRecord(choiceContext, card);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3);
}

public class DreamKeyRevelation() : SakuraModCard(1, CardType.Skill, CardRarity.Ancient, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Manifest, CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var cards = await SakuraManifestLoop.Manifest(this, choiceContext, DynamicVars.Cards.IntValue);
        foreach (var card in cards)
            await SakuraActions.ReleaseThisTurnAndRecord(choiceContext, card);
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1);
}

public class Blank() : SakuraModCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self), IReleaseable
{
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

        await TriggerReleaseEffect(choiceContext, play);
    }

    public async Task OnReleased(PlayerChoiceContext choiceContext, CardPlay play)
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
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Water, CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new RepeatVar(1), new RepeatVar("ReleaseRepeat", 1)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var card = await SakuraActions.SelectHandCard(this, choiceContext, card => card != this);
        if (card is not null)
        {
            var amount = DynamicVars["Repeat"].IntValue;
            if (ShouldRelease)
                amount += DynamicVars["ReleaseRepeat"].IntValue;
            card.AddReplayThisTurn(amount);
        }
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class Remind() : SakuraModCard(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    private static readonly LocString SelectionPrompt = new("cards", "SAKURAMOD-REMIND.selectionPrompt");

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
            var copy = await SakuraManifestLoop.AddTemporaryRememberedCopyToHand(this, choiceContext, card, freeThisTurn: true);
            if (copy is not null && ShouldRelease)
                await SakuraActions.ReleaseThisTurnAndRecord(choiceContext, copy);
        }
    }

    private int RecallCount()
    {
        var energy = Math.Max(0, ResolveEnergyXValue());
        return IsUpgraded ? energy * 2 + 1 : energy + 1;
    }

    protected override void OnUpgrade() {}
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

        if (ShouldRelease && cards.Any(card => card.IsReleased()))
            foreach (var card in cards)
                await SakuraActions.ReleaseThisTurnAndRecord(choiceContext, card);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class Time() : SakuraModCard(3, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Fire, CardKeyword.Exhaust];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        SakuraCardPlayVfx.PlayTime(Owner.Creature);
        var power = await PowerCmd.Apply<TimeStopPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this, true);
        if (ShouldRelease)
            power?.PreserveBlock();

        PlayerCmd.EndTurn(Owner, canBackOut: false);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class TrueOrFalse() : SakuraModCard(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Fire, CardKeyword.Exhaust, SakuraKeywords.Stabilize];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(2),
        new EnergyVar(1)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (ShouldRelease)
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
            await SakuraManifestLoop.GrantTemporary(choiceContext, card);
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

public class StarWand() : SakuraModCard(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (Owner.Creature.GetPower<StarWandPower>() is null)
        {
            var power = await PowerCmd.Apply<StarWandPower>(
                choiceContext,
                Owner.Creature,
                StarWandPower.BootstrapAmount,
                Owner.Creature,
                this,
                false);
            power?.ResetStars();
        }
    }

    protected override void OnUpgrade() => AddKeywordIfMissing(CardKeyword.Innate);
}

public class SealedBook() : SakuraModCard(2, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    private const string SealedCountVar = "SealedCount";

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust, CardKeyword.Retain];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var canSeal = SakuraActions.Hand(this).Any(SealedBookMemory.CanSeal);
        var canRelease = SealedBookMemory.Count(Owner) > 0;
        if (!canSeal && !canRelease)
            return;

        if (canSeal && canRelease)
        {
            var sealChoice = SakuraActions.CloneWithCurrentUpgrade<SealedBookSealChoice>(this);
            var releaseChoice = SakuraActions.CloneWithCurrentUpgrade<SealedBookReleaseChoice>(this);
            var choice = await SakuraActions.SelectFromCards(this, choiceContext, [sealChoice, releaseChoice], cancelable: false);
            if (choice is SealedBookReleaseChoice)
                await Release(choiceContext);
            else if (choice is SealedBookSealChoice)
                await Seal(choiceContext);
            return;
        }

        if (canSeal)
            await Seal(choiceContext);
        else
            await Release(choiceContext);
    }

    protected override void AddExtraArgsToDescription(LocString description) =>
        description.Add(SealedCountVar, SealedBookMemory.Count(DescriptionOwner()));

    private Player? DescriptionOwner() =>
        IsMutable ? Owner : null;

    private async Task Seal(PlayerChoiceContext choiceContext)
    {
        var card = await SakuraActions.SelectHandCard(this, choiceContext, SealedBookMemory.CanSeal, cancelable: false);
        if (card is null)
            return;

        await SealedBookMemory.Seal(choiceContext, card);
    }

    private async Task Release(PlayerChoiceContext choiceContext) =>
        await SealedBookMemory.Release(this, choiceContext);

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class DreamKeyResonance() : SakuraModCard(2, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Stabilize];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play) =>
        await PowerCmd.Apply<DreamKeyResonancePower>(choiceContext, Owner.Creature, 1, Owner.Creature, this, false);

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class CerberusTrueForm() : SakuraModCard(2, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Fire];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(9, ValueProp.Move),
        new PowerVar<SakuraBurnPower>(3)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var targets = CombatState!.HittableEnemies.ToList();
        foreach (var enemy in targets)
        {
            var damage = DynamicVars.Damage.IntValue;
            if (enemy.GetPower<SakuraBurnPower>()?.Amount > 0)
                damage *= 2;
            await SakuraActions.Attack(choiceContext, this, enemy, damage);
        }

        await PowerCmd.Apply<SakuraBurnPower>(
            choiceContext,
            targets.Where(enemy => enemy.IsAlive).ToList(),
            DynamicVars["SakuraBurnPower"].IntValue,
            Owner.Creature,
            this,
            false);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
}

public class YueTrueForm() : SakuraModCard(2, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(12, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block.IntValue, ValueProp.Move, play, false);
        await PowerCmd.Apply<StrongReflectionPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this, false);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(6);
}

public class FourSymbols() : SakuraModCard(2, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(8, ValueProp.Move),
        new DamageVar("FullDamage", 16, ValueProp.Move),
        new PowerVar<WeakPower>(2)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var allElements = SakuraActions.PlayedElementTypesThisTurn(Owner).Count >= SakuraElementSets.AllElements.Count;
        var targets = CombatState!.HittableEnemies.ToList();
        var damage = allElements ? DynamicVars["FullDamage"].IntValue : DynamicVars.Damage.IntValue;
        await SakuraActions.Attack(choiceContext, this, targets, damage);
        if (allElements)
            await PowerCmd.Apply<WeakPower>(
                choiceContext,
                targets.Where(enemy => enemy.IsAlive).ToList(),
                DynamicVars.Weak.IntValue,
                Owner.Creature,
                this,
                false);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3);
        DynamicVars["FullDamage"].UpgradeValueBy(4);
    }
}

public class DreamsEnd() : SakuraModCard(2, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play) =>
        await PowerCmd.Apply<DreamsEndPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this, false);

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class Echo() : SakuraModCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner, false);
        await PowerCmd.Apply<EchoPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this, false);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
