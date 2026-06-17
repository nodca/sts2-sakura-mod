using BaseLib.Utils;
using BaseLib.Patches.Hooks;
using MegaCrit.Sts2.Core;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
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
        await PowerCmd.Apply<SakuraTemporaryStrengthPower>(CombatState!.HittableEnemies, DynamicVars["StrengthLoss"].IntValue, Owner.Creature, this, false);
        await TriggerReleaseEffect(choiceContext, play);
    }

    public async Task OnReleased(PlayerChoiceContext choiceContext, CardPlay play) =>
        await PowerCmd.Apply<GravitationHoldPower>(Owner.Creature, DynamicVars["ReleaseReturns"].IntValue, Owner.Creature, this, false);

    protected override void OnUpgrade() => DynamicVars["StrengthLoss"].UpgradeValueBy(1);
}

public class Mirage() : SakuraModCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Water, CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<BufferPower>(1)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PowerCmd.Apply<BufferPower>(Owner.Creature, DynamicVars["BufferPower"].IntValue, Owner.Creature, this, false);
        var image = CreateMirageImage();
        if (ShouldRelease)
            image.AddKeyword(CardKeyword.Retain);

        await SakuraActions.AddGeneratedCardToCombat(
            image,
            new GeneratedCardOptions
            {
                AddTemporary = this.IsTemporary(),
                Pile = ShouldRelease
                    ? PileType.Hand
                    : IsUpgraded
                        ? PileType.Draw
                        : PileType.Discard,
                Position = !ShouldRelease && IsUpgraded
                    ? CardPilePosition.Top
                    : CardPilePosition.Random
            },
            choiceContext);
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
        await PowerCmd.Apply<MirageImagePower>(Owner.Creature, DynamicVars["MirageImagePower"].IntValue, Owner.Creature, this, false);
}

public class Record() : SakuraModCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self), IReleaseable
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Catalog];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var card = await SakuraActions.SelectCatalogedClearCard(this, choiceContext, cancelable: false, excludedType: typeof(Record));
        if (card is not null)
        {
            var copy = await SakuraActions.AddTemporaryCopyToHand(
                this,
                choiceContext,
                card,
                release: false,
                freeThisTurn: false,
                preserveRelease: true);
            if (copy is not null)
                await SakuraActions.ReduceCostThisTurn(this, copy);
        }

        await TriggerReleaseEffect(choiceContext, play);
    }

    public async Task OnReleased(PlayerChoiceContext choiceContext, CardPlay play) =>
        await RecordPower.RecordOrRestore(Owner.Creature, this);

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
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
        await PowerCmd.Apply<KeroBondPower>(Owner.Creature, DynamicVars["KeroBondPower"].IntValue, Owner.Creature, this, false);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class MagicAwakening() : SakuraModCard(2, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<MagicAwakeningPower>(1)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play) =>
        await PowerCmd.Apply<MagicAwakeningPower>(Owner.Creature, DynamicVars["MagicAwakeningPower"].IntValue, Owner.Creature, this, false);

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class Exchange() : SakuraModCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
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
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new HealVar(3)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await CreatureCmd.Heal(Owner.Creature, DynamicVars.Heal.IntValue);
        await SakuraActions.DiscoverGenerated(
            this,
            choiceContext,
            SakuraActions.PartnerTemplates(),
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
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<RegenPower>(4)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PowerCmd.Apply<RegenPower>(Owner.Creature, DynamicVars["RegenPower"].IntValue, Owner.Creature, this, false);
        await TriggerReleaseEffect(choiceContext, play);
    }

    public async Task OnReleased(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var card = CardPile.Get(PileType.Exhaust, Owner)!.Cards.FirstOrDefault(card => card is SakuraModCard);
        if (card is not null)
            await SakuraActions.MoveExistingCardToPileWithoutVisuals(this, card, PileType.Draw, CardPilePosition.Top);
    }

    protected override void OnUpgrade() => DynamicVars["RegenPower"].UpgradeValueBy(1);
}

public class Reversal() : SakuraModCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy), IReleaseable
{
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
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

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
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<WeakPower>(2), new PowerVar<SakuraSleepPower>(2)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = RequiredTarget(play);
        await PowerCmd.Apply<WeakPower>(target, DynamicVars.Weak.IntValue, Owner.Creature, this, false);
        await CardPileCmd.Draw(choiceContext, 1, Owner, false);
        await TriggerReleaseEffect(choiceContext, play);
    }

    public async Task OnReleased(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = play.Target ?? CombatState?.HittableEnemies.FirstOrDefault();
        if (target is not null)
            await PowerCmd.Apply<SakuraSleepPower>(target, DynamicVars["SakuraSleepPower"].IntValue, Owner.Creature, this, false);
    }

    protected override void OnUpgrade() => DynamicVars.Weak.UpgradeValueBy(1);
}

public class Spiral() : SakuraModCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy), IReleaseable
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Wind];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(4, ValueProp.Move), new DamageVar("SpiralDamage", 3, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = RequiredTarget(play);
        var previousSpirals = SakuraActions.CardPlayCountThisCombat(Owner, card => card is Spiral, play);
        var damage = DynamicVars.Damage.IntValue + previousSpirals * DynamicVars["SpiralDamage"].IntValue;
        await SakuraActions.Attack(choiceContext, this, target, damage);
        await TriggerReleaseEffect(choiceContext, play);
    }

    public async Task OnReleased(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await SakuraActions.AddGeneratedCopyToHand(
            this,
            this,
            new GeneratedCardOptions
            {
                RemoveRelease = true,
                RemoveManifestAtlasOrigin = true,
                AddTemporary = true
            },
            choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars["SpiralDamage"].UpgradeValueBy(1);
}

public class Transfer() : SakuraModCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy), IReleaseStateObserver
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
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
        await PowerCmd.Apply<StrengthPower>(target, -DynamicVars["EnemyStrengthLoss"].IntValue, Owner.Creature, this, false);
        await PowerCmd.Apply<StrengthPower>(Owner.Creature, DynamicVars["StrengthGain"].IntValue, Owner.Creature, this, false);
        await PowerCmd.Apply<DexterityPower>(Owner.Creature, DynamicVars["DexterityGain"].IntValue, Owner.Creature, this, false);
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
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var manifested = await SakuraActions.Manifest(this, choiceContext, DynamicVars.Cards.IntValue);
        if (IsUpgraded)
            foreach (var card in manifested)
                await SakuraActions.ReduceCostThisTurn(this, card);

        var cards = SakuraActions.Hand(this)
            .Where(card => card != this)
            .ToList();
        var chosen = cards.Count > 0
            ? await SakuraActions.SelectFromCards(this, choiceContext, cards)
            : null;
        if (chosen is not null)
            await CardPileCmd.Add(chosen, PileType.Draw, CardPilePosition.Top, this, skipVisuals: false);

        await PowerCmd.Apply<SakuraDrawNextTurnPower>(Owner.Creature, 1, Owner.Creature, this, false);
    }

    protected override void OnUpgrade() {}
}

public class ClockCountryAlice() : SakuraModCard(2, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<ClockCountryAlicePower>(ClockCountryAlicePower.ReadingMode(upgradedReading: false))
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var mode = DynamicVars["ClockCountryAlicePower"].IntValue;
        var power = await PowerCmd.Apply<ClockCountryAlicePower>(Owner.Creature, mode, Owner.Creature, this, false);
        if (power is not null && power.Amount < mode)
            await PowerCmd.ModifyAmount(power, mode - power.Amount, Owner.Creature, this, false);
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
            .Where(card => SakuraActions.IsClearCard(card) && !card.IsTemporary())
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
            if (await SakuraActions.GrantTemporary(choiceContext, card))
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
        await SakuraActions.GrantTemporary(choiceContext, card);
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
            await PowerCmd.Apply<FalseDailyLifePower>(Owner.Creature, DynamicVars.Damage.IntValue, Owner.Creature, this, false);
            return;
        }

        if (power.Amount < DynamicVars.Damage.IntValue)
            await PowerCmd.ModifyAmount(power, DynamicVars.Damage.IntValue - power.Amount, Owner.Creature, this, false);
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
        new DamageVar(5, ValueProp.Move),
        new DamageVar("CatalogDamage", 2, ValueProp.Move)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = RequiredTarget(play);
        var damage = DynamicVars.Damage.IntValue + SakuraActions.CatalogCount(Owner) * DynamicVars["CatalogDamage"].IntValue;
        await SakuraActions.Attack(choiceContext, this, target, damage);
    }

    protected override void OnUpgrade() => DynamicVars["CatalogDamage"].UpgradeValueBy(1);
}

public class Archive() : SakuraModCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Stabilize];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await SakuraActions.Manifest(this, choiceContext, DynamicVars.Cards.IntValue);

        if (!SakuraActions.StabilizeCandidates(this).Any())
            return;

        var card = await SakuraActions.SelectStabilizeCandidate(this, choiceContext);
        if (card is not null)
            await card.Stabilize(choiceContext);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class GrowingMagic() : SakuraModCard(1, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Stabilize];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<GrowingMagicPower>(1)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play) =>
        await PowerCmd.Apply<GrowingMagicPower>(Owner.Creature, DynamicVars["GrowingMagicPower"].IntValue, Owner.Creature, this, false);

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
            await SakuraActions.ReleaseThisTurnAndRecord(card);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner, false);
    }

    private static bool CanRelease(CardModel card) =>
        SakuraActions.IsClearCard(card) && !card.IsReleased();

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class CardBookSorting() : SakuraModCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    private static readonly LocString SelectionPrompt = new("cards", "SAKURAMOD-CARD_BOOK_SORTING.selectionPrompt");

    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Stabilize];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(2),
        new BlockVar(3, ValueProp.Move)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var cards = await SakuraActions.SelectUpToFromCards(
            this,
            choiceContext,
            SakuraActions.StabilizeCandidates(this),
            DynamicVars.Cards.IntValue,
            prompt: SelectionPrompt,
            minSelect: 0);

        var stabilized = 0;
        foreach (var card in cards)
        {
            if (!SakuraActions.StabilizeCandidates(this).Contains(card))
                continue;

            await card.Stabilize(choiceContext);
            stabilized++;
        }

        if (stabilized > 0)
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block.IntValue * stabilized, ValueProp.Move, play, false);
    }
    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(1);
}

public class NamelessMagic() : SakuraModCard(2, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Catalog];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(3, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var damage = DynamicVars.Damage.IntValue * SakuraActions.CatalogCount(Owner);
        await SakuraActions.Attack(choiceContext, this, CombatState!.HittableEnemies.ToList(), damage);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(1);
}

public class ChainPhenomenon() : SakuraModCard(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(4, ValueProp.Move),
        new DamageVar("ClearDamage", 2, ValueProp.Move)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = RequiredTarget(play);
        var clearCardsPlayed = SakuraActions.DistinctCardTypesPlayedThisTurn(Owner, SakuraActions.IsClearCard, this);
        var damage = DynamicVars.Damage.IntValue + clearCardsPlayed * DynamicVars["ClearDamage"].IntValue;
        await SakuraActions.Attack(choiceContext, this, target, damage);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(1);
        DynamicVars["ClearDamage"].UpgradeValueBy(1);
    }
}

public class MagicSurge() : SakuraModCard(2, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<MagicSurgePower>(1)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play) =>
        await PowerCmd.Apply<MagicSurgePower>(Owner.Creature, DynamicVars["MagicSurgePower"].IntValue, Owner.Creature, this, false);

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
            await SakuraActions.ReleaseThisTurnAndRecord(card);
    }

    private static bool CanTune(CardModel card) =>
        SakuraActions.IsClearCard(card)
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
            await PowerCmd.Apply<SakuraBurnPower>(target, DynamicVars["SakuraBurnPower"].IntValue, Owner.Creature, this, false);
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
        await SakuraActions.AddGeneratedCardToCombat(
            CreateReleasedStruggle(),
            new GeneratedCardOptions
            {
                AddTemporary = true,
                AddRelease = true
            },
            choiceContext);
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
        var power = await PowerCmd.Apply<TalismanComboPower>(Owner.Creature, 1, Owner.Creature, this, false);
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
        await PowerCmd.Apply<WeakPower>(CombatState!.HittableEnemies.ToList(), DynamicVars.Weak.IntValue, Owner.Creature, this, false);
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
        await SakuraActions.ReleaseThisTurnAndRecord(card);
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
            await SakuraActions.AddTemporaryCopyToHand(this, choiceContext, card, release: false, freeThisTurn: IsUpgraded, preserveRelease: true);
    }

    protected override void OnUpgrade() {}
}

public class FujitakaNote() : SakuraModCard(0, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    private static readonly LocString SelectionPrompt = new("cards", "SAKURAMOD-FUJITAKA_NOTE.selectionPrompt");

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar("Look", 2),
        new CardsVar(1)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var top = CardPile.Get(PileType.Draw, Owner)!.Cards.Take(DynamicVars["Look"].IntValue).ToList();
        var cards = await SakuraActions.SelectUpToFromCardPreviews(this, choiceContext, top, 1, prompt: SelectionPrompt, minSelect: 0);
        if (cards.FirstOrDefault() is { } discarded)
            await SakuraActions.MoveExistingCardToPileWithoutVisuals(this, discarded, PileType.Discard, CardPilePosition.Random);

        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner, false);
    }

    protected override void OnUpgrade() => DynamicVars["Look"].UpgradeValueBy(1);
}

public class NaokoGhostStory() : SakuraModCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Catalog];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(1),
        new CardsVar("Draw", 1)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var manifested = await SakuraActions.Manifest(this, choiceContext, DynamicVars.Cards.IntValue);
        if (manifested.Any(card => !SakuraActions.HasCatalogedClearCard(Owner, card)))
            await CardPileCmd.Draw(choiceContext, DynamicVars["Draw"].IntValue, Owner, false);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class SyaoranTalisman() : SakuraModCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await SakuraActions.Attack(choiceContext, this, RequiredTarget(play), DynamicVars.Damage.IntValue);
        var power = await PowerCmd.Apply<SyaoranTalismanPower>(Owner.Creature, 1, Owner.Creature, this, false);
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
        var card = await SakuraActions.SelectHandCard(this, choiceContext, SakuraActions.IsClearCard, cancelable: false);
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
        await PowerCmd.Apply<SyaoranBondPower>(Owner.Creature, 1, Owner.Creature, this, false);
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
            await SakuraActions.AddTemporaryCopyToHand(this, choiceContext, card, release: false, freeThisTurn: SakuraActions.IsPartner(card));
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1);
}

public class TomoyoBond() : SakuraModCard(2, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<TomoyoBondPower>(1)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play) =>
        await PowerCmd.Apply<TomoyoBondPower>(Owner.Creature, DynamicVars["TomoyoBondPower"].IntValue, Owner.Creature, this, false);

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class DreamKeyGlow() : SakuraModCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(5, ValueProp.Move), new CardsVar(1)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block.IntValue, ValueProp.Move, play, false);
        var cards = await SakuraActions.Manifest(this, choiceContext, DynamicVars.Cards.IntValue);
        foreach (var card in cards)
            await SakuraActions.ReleaseThisTurnAndRecord(card);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3);
}

public class DreamKeyRevelation() : SakuraModCard(1, CardType.Skill, CardRarity.Ancient, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var cards = await SakuraActions.Manifest(this, choiceContext, DynamicVars.Cards.IntValue);
        foreach (var card in cards)
            await SakuraActions.ReleaseThisTurnAndRecord(card);
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1);
}

public class Blank() : SakuraModCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self), IReleaseable
{
    private int _removedCardsThisPlay;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
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
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
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

public class Remind() : SakuraModCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var choices = TemporaryCardMemory.CardsRemovedByTemporary(CombatState, Owner);
        if (choices.Count == 0)
        {
            await SakuraActions.Manifest(this, choiceContext, 1);
            return;
        }

        var cards = await SakuraActions.SelectUpToFromCards(this, choiceContext, choices, DynamicVars.Cards.IntValue);

        foreach (var card in cards)
            await SakuraActions.AddRememberedCopyToHand(this, card, freeThisTurn: ShouldRelease);
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1);
}

public class Synchronize() : SakuraModCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var cards = await SakuraActions.SelectHandCards(
            this,
            choiceContext,
            card => SakuraActions.IsClearCard(card) && card != this,
            2);
        if (cards.Count != 2)
            return;

        cards[0].SynchronizeWith(cards[1]);

        if (ShouldRelease && cards.Any(card => card.IsReleased()))
            foreach (var card in cards)
                await SakuraActions.ReleaseThisTurnAndRecord(card);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class Time() : SakuraModCard(3, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        SakuraCardPlayVfx.PlayTime(Owner.Creature);
        var power = await PowerCmd.Apply<TimeStopPower>(Owner.Creature, 1, Owner.Creature, this, true);
        if (ShouldRelease)
            power?.PreserveBlock();

        PlayerCmd.EndTurn(Owner, canBackOut: false);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class TrueOrFalse() : SakuraModCard(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust, SakuraKeywords.Stabilize];
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
            await SakuraActions.GrantTemporary(choiceContext, card);
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
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play) =>
        await PowerCmd.Apply<StarWandPower>(Owner.Creature, 0, Owner.Creature, this, false);

    protected override void OnUpgrade() => AddKeywordIfMissing(CardKeyword.Innate);
}

public class SealedBook() : SakuraModCard(2, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    private const string SealedCountVar = "SealedCount";

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust, CardKeyword.Retain];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var canSeal = SakuraActions.Hand(this).Any(SakuraActions.CanSealIntoSealedBook);
        var canRelease = SakuraActions.SealedBookCount(Owner) > 0;
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
        description.Add(SealedCountVar, SakuraActions.SealedBookCount(DescriptionOwner()));

    private Player? DescriptionOwner() =>
        IsMutable ? Owner : null;

    private async Task Seal(PlayerChoiceContext choiceContext)
    {
        var card = await SakuraActions.SelectHandCard(this, choiceContext, SakuraActions.CanSealIntoSealedBook, cancelable: false);
        if (card is null)
            return;

        await SakuraActions.SealIntoSealedBook(choiceContext, card);
    }

    private async Task Release(PlayerChoiceContext choiceContext) =>
        await SakuraActions.ReleaseFromSealedBook(this, choiceContext);

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class DreamKeyResonance() : SakuraModCard(2, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Stabilize];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play) =>
        await PowerCmd.Apply<DreamKeyResonancePower>(Owner.Creature, 1, Owner.Creature, this, false);

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
        await PowerCmd.Apply<StrongReflectionPower>(Owner.Creature, 1, Owner.Creature, this, false);
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

public class DreamsEnd() : SakuraModCard(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play) =>
        await PowerCmd.Apply<DreamsEndPower>(Owner.Creature, 1, Owner.Creature, this, false);

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class Echo() : SakuraModCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var owned = Owner.Deck.Cards.Where(SakuraActions.IsClearCard).ToList();
        var chosen = await SelectDeckCard(choiceContext, owned);
        if (chosen is null)
            return;

        var source = CreateCombatScopedCopySource(chosen);
        try
        {
            await SakuraActions.AddTemporaryCopyToHand(this, choiceContext, source, release: true, freeThisTurn: true);
        }
        finally
        {
            if (source.Pile is null)
                source.CardScope?.RemoveCard(source);
        }
    }

    private async Task<CardModel?> SelectDeckCard(PlayerChoiceContext choiceContext, IReadOnlyList<CardModel> cards)
    {
        var previews = cards.Select(CreateCombatScopedCopySource).ToList();
        try
        {
            var selected = await SakuraActions.SelectFromCards(this, choiceContext, previews, cancelable: false);
            if (selected is null)
                return null;

            var index = previews.IndexOf(selected);
            return index >= 0 ? cards[index] : null;
        }
        finally
        {
            foreach (var preview in previews)
            {
                if (preview.Pile is null)
                    preview.CardScope?.RemoveCard(preview);
            }
        }
    }

    private CardModel CreateCombatScopedCopySource(CardModel card) =>
        Owner.Creature.CombatState?.CloneCard(card)
        ?? throw new InvalidOperationException($"Cannot copy {card.Id.Entry} without a card scope.");

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
