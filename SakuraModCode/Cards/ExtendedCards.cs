using BaseLib.Utils;
using MegaCrit.Sts2.Core;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
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
        new DynamicVar("StrengthLoss", 2)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        SakuraCardPlayVfx.PlayGravitation(CombatState!.HittableEnemies);
        await PowerCmd.Apply<SakuraTemporaryStrengthPower>(CombatState!.HittableEnemies, -DynamicVars["StrengthLoss"].IntValue, Owner.Creature, this, false);
        await TriggerReleaseEffect(choiceContext, play);
    }

    public async Task OnReleased(PlayerChoiceContext choiceContext, CardPlay play) =>
        await PowerCmd.Apply<GravitationHoldPower>(Owner.Creature, 1, Owner.Creature, this, false);

    protected override void OnUpgrade() => DynamicVars["StrengthLoss"].UpgradeValueBy(1);
}

public class Mirage() : SakuraModCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self), IReleaseable
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Water, CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<BufferPower>(1)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PowerCmd.Apply<BufferPower>(Owner.Creature, DynamicVars["BufferPower"].IntValue, Owner.Creature, this, false);
        var image = ModelDb.Card<MirageImage>().CreateClone();
        await SakuraActions.AddGeneratedCardToCombat(
            image,
            new SakuraActions.GeneratedCardOptions
            {
                AddTemporary = this.IsTemporary(),
                Pile = IsUpgraded ? PileType.Draw : PileType.Discard,
                Position = IsUpgraded ? CardPilePosition.Top : CardPilePosition.Random
            },
            choiceContext);
        await TriggerReleaseEffect(choiceContext, play);
    }

    public async Task OnReleased(PlayerChoiceContext choiceContext, CardPlay play) =>
        await PowerCmd.Apply<ArtifactPower>(Owner.Creature, 1, Owner.Creature, this, false);
}

public class MirageImage() : SakuraModCard(0, CardType.Skill, CardRarity.Basic, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<MirageImagePower>(4)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play) =>
        await PowerCmd.Apply<MirageImagePower>(Owner.Creature, DynamicVars["MirageImagePower"].IntValue, Owner.Creature, this, false);
}

public class Record() : SakuraModCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self), IReleaseable
{
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var choices = SakuraActions.CardsPlayedLastTurn(CombatState, Owner);
        var card = await SakuraActions.SelectFromCards(this, choiceContext, choices, cancelable: false);
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
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<KeroBondPower>(2),
        new CardsVar(2)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PowerCmd.Apply<KeroBondPower>(Owner.Creature, DynamicVars["KeroBondPower"].IntValue, Owner.Creature, this, false);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner, false);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class TomoyoDesign() : SakuraModCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(4, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block.IntValue, ValueProp.Move, play, false);
        if (!SakuraActions.Hand(this).Any(CanRelease))
            return;

        var card = await SakuraActions.SelectHandCard(this, choiceContext, CanRelease, cancelable: false);
        if (card is not null)
            await SakuraActions.ReleaseAndRecord(card);
    }

    private static bool CanRelease(CardModel card) =>
        SakuraActions.IsClearCard(card) && !card.IsReleased();

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(2);
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

public class Kindness() : SakuraModCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
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

public class Labyrinth() : SakuraModCard(2, CardType.Skill, CardRarity.Rare, TargetType.AllEnemies), IReleaseStateObserver
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Earth, CardKeyword.Exhaust];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        OnReleaseStateChanged();
        SakuraCardPlayVfx.PlayLabyrinth(CombatState!.HittableEnemies);
        await SakuraActions.SuppressAliveEnemyActions(CombatState!.Enemies);
    }

    public void OnReleaseStateChanged()
    {
        if (ShouldRelease)
            RemoveKeywordIfPresent(CardKeyword.Exhaust);
        else
            AddKeywordIfMissing(CardKeyword.Exhaust);
    }

    protected override void OnUpgrade() => AddKeywordIfMissing(CardKeyword.Retain);
}

public class Repair() : SakuraModCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self), IReleaseable
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<SakuraRegenerationPower>(4)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PowerCmd.Apply<SakuraRegenerationPower>(Owner.Creature, DynamicVars["SakuraRegenerationPower"].IntValue, Owner.Creature, this, false);
        await TriggerReleaseEffect(choiceContext, play);
    }

    public async Task OnReleased(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var card = CardPile.Get(PileType.Exhaust, Owner)!.Cards.FirstOrDefault(card => card is SakuraModCard);
        if (card is not null)
            await CardPileCmd.Add(card, PileType.Draw, CardPilePosition.Top, this, true);
    }

    protected override void OnUpgrade() => DynamicVars["SakuraRegenerationPower"].UpgradeValueBy(1);
}

public class Reversal() : SakuraModCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self), IReleaseable
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1), new CardsVar("ReleaseDraw", 1)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var drawCards = CardPile.Get(PileType.Draw, Owner)!.Cards.ToList();
        var discardCards = CardPile.Get(PileType.Discard, Owner)!.Cards.ToList();
        PileExchangeVfx.Play(drawCards.Count, discardCards.Count);

        foreach (var card in drawCards)
            await CardPileCmd.Add(card, PileType.Discard, CardPilePosition.Bottom, this, true);
        foreach (var card in discardCards)
            await CardPileCmd.Add(card, PileType.Draw, CardPilePosition.Bottom, this, true);

        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner, false);
        await TriggerReleaseEffect(choiceContext, play);
    }

    public async Task OnReleased(PlayerChoiceContext choiceContext, CardPlay play) =>
        await CardPileCmd.Draw(choiceContext, DynamicVars["ReleaseDraw"].IntValue, Owner, false);

    protected override void OnUpgrade() => DynamicVars["ReleaseDraw"].UpgradeValueBy(1);
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
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<WeakPower>(2), new DynamicVar("StrengthLoss", 2)];

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
            await PowerCmd.Apply<SakuraTemporaryStrengthPower>(target, -DynamicVars["StrengthLoss"].IntValue, Owner.Creature, this, false);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Weak.UpgradeValueBy(1);
        DynamicVars["StrengthLoss"].UpgradeValueBy(1);
    }
}

public class Spiral() : SakuraModCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy), IReleaseable
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Wind];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(7, ValueProp.Move), new CardsVar(1), new CardsVar("ReleaseCards", 1)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = RequiredTarget(play);
        await SakuraActions.Attack(choiceContext, this, target, DynamicVars.Damage.IntValue);

        var count = DynamicVars.Cards.IntValue + (ShouldRelease ? DynamicVars["ReleaseCards"].IntValue : 0);
        var cards = await SakuraActions.SelectUpToFromCards(this, choiceContext, CardPile.Get(PileType.Discard, Owner)!.Cards, count, cancelable: false);
        foreach (var card in cards)
            await CardPileCmd.Add(card, PileType.Draw, CardPilePosition.Top, this, true);
    }

    public async Task OnReleased(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var cards = await SakuraActions.SelectUpToFromCards(this, choiceContext, CardPile.Get(PileType.Discard, Owner)!.Cards, DynamicVars["ReleaseCards"].IntValue, cancelable: false);
        foreach (var card in cards)
            await CardPileCmd.Add(card, PileType.Draw, CardPilePosition.Top, this, true);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
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

public class AkihoTea() : SakuraModCard(1, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(2)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner, false);
        var discard = await SakuraActions.SelectHandCard(this, choiceContext, card => card != this);
        if (discard is not null)
        {
            var wasClear = discard is SakuraModCard;
            await CardCmd.Discard(choiceContext, [discard]);
            if (wasClear)
                await SakuraActions.Manifest(this, choiceContext, 1);
        }
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1);
}

public class AkihoDream() : SakuraModCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var clearCards = CardPile.Get(PileType.Draw, Owner)!.Cards
            .Where(SakuraActions.IsManifestableClearCard)
            .ToList();
        var tucked = await SakuraActions.SelectFromCards(this, choiceContext, clearCards, cancelable: false);
        if (tucked is not null)
            await CardPileCmd.Add(tucked, PileType.Discard, CardPilePosition.Bottom, this, true);

        await SakuraActions.Manifest(this, choiceContext, DynamicVars.Cards.IntValue, excludedType: tucked?.GetType());
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1);
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

public class AliceReading() : SakuraModCard(0, CardType.Skill, CardRarity.Basic, TargetType.AnyEnemy)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var power = Owner.Creature.GetPower<ClockCountryAlicePower>();
        if (power is null)
            return;

        await power.ReleaseStoredValues(choiceContext, this, RequiredTarget(play), play);
        if (IsUpgraded)
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner, false);
    }

    protected override void OnUpgrade() {}
}

public class ForbiddenMagic() : SakuraModCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new EnergyVar(1), new CardsVar(1)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var grantedCount = 0;
        foreach (var card in SakuraActions.Hand(this)
                     .Where(card => SakuraActions.IsClearCard(card) && !card.IsTemporary())
                     .ToList())
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

public class CompassTracking() : SakuraModCard(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(8, ValueProp.Move), new PowerVar<WeakPower>(1), new PowerVar<VulnerablePower>(1)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = RequiredTarget(play);
        await SakuraActions.Attack(choiceContext, this, target, DynamicVars.Damage.IntValue);
        if (!SakuraActions.IntendsToAttack(target))
            return;

        await PowerCmd.Apply<WeakPower>(target, DynamicVars.Weak.IntValue, Owner.Creature, this, false);
        await PowerCmd.Apply<VulnerablePower>(target, DynamicVars.Vulnerable.IntValue, Owner.Creature, this, false);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
}

public class SyaoranTalisman() : SakuraModCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(6, ValueProp.Move),
        new CardsVar("WindDraw", 1),
        new PowerVar<WeakPower>(1),
        new DamageVar("FireDamage", 5, ValueProp.Move),
        new BlockVar("EarthBlock", 5, ValueProp.Move)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = RequiredTarget(play);
        await SakuraActions.Attack(choiceContext, this, target, DynamicVars.Damage.IntValue);
        foreach (var element in SakuraActions.PlayedElementsThisTurn(Owner))
        {
            switch (element)
            {
                case SakuraElement.Wind:
                    await CardPileCmd.Draw(choiceContext, DynamicVars["WindDraw"].IntValue, Owner, false);
                    break;
                case SakuraElement.Water:
                    await PowerCmd.Apply<WeakPower>(target, DynamicVars.Weak.IntValue, Owner.Creature, this, false);
                    break;
                case SakuraElement.Fire:
                    await SakuraActions.Attack(choiceContext, this, target, DynamicVars["FireDamage"].IntValue);
                    break;
                case SakuraElement.Earth:
                    await CreatureCmd.GainBlock(Owner.Creature, DynamicVars["EarthBlock"].IntValue, ValueProp.Move, play, false);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(element), element, null);
            }
        }
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(2);
}

public class DaoistSupport() : SakuraModCard(0, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var card = await SakuraActions.SelectHandCard(this, choiceContext, SakuraActions.IsClearCard, cancelable: false);
        if (card is null)
            return;

        SakuraActions.GrantElementsThisTurn(card, SakuraElementSet.All);
    }

    protected override void OnUpgrade()
    {
        RemoveKeywordIfPresent(CardKeyword.Exhaust);
    }
}

public class SyaoranBond() : SakuraModCard(2, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<PlatingPower>(6),
        new DamageVar(6, ValueProp.Move)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PowerCmd.Apply<PlatingPower>(Owner.Creature, DynamicVars["PlatingPower"].IntValue, Owner.Creature, this, false);
        await PowerCmd.Apply<SyaoranBondPower>(Owner.Creature, DynamicVars.Damage.IntValue, Owner.Creature, this, false);
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
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(2),
        new PowerVar<TomoyoBondPower>(1)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner);
        await PowerCmd.Apply<TomoyoBondPower>(Owner.Creature, DynamicVars["TomoyoBondPower"].IntValue, Owner.Creature, this, false);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class DreamKeyGlow() : SakuraModCard(1, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(5, ValueProp.Move), new CardsVar(1)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block.IntValue, ValueProp.Move, play, false);
        await SakuraActions.Manifest(this, choiceContext, DynamicVars.Cards.IntValue);
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
            await CardPileCmd.RemoveFromCombat(card, true);

        await TriggerReleaseEffect(choiceContext, play);
    }

    public async Task OnReleased(PlayerChoiceContext choiceContext, CardPlay play)
    {
        foreach (var power in Owner.Creature.Powers.Where(power => power.Type == PowerType.Debuff).ToList())
            await PowerCmd.Remove(power);

        foreach (var enemy in CombatState!.Enemies)
        {
            foreach (var power in enemy.Powers
                         .Where(power => power.Amount > 0 && power is StrengthPower or DexterityPower or ArtifactPower)
                         .ToList())
                await PowerCmd.Remove(power);
        }

        if (_removedCardsThisPlay > 0)
            await CardPileCmd.Draw(choiceContext, _removedCardsThisPlay, Owner, false);
    }

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
        var cards = await SakuraActions.SelectUpToFromCards(this, choiceContext, choices, DynamicVars.Cards.IntValue);

        foreach (var card in cards)
            await SakuraActions.AddRememberedCopyToHand(this, card, freeThisTurn: ShouldRelease);
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1);
}

public class Synchronize() : SakuraModCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var cards = await SakuraActions.SelectUpToHandCards(
            this,
            choiceContext,
            card => SakuraActions.IsClearCard(card) && card != this,
            2,
            cancelable: false);
        if (cards.Count < 2)
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
            power?.PreserveResources();

        PlayerCmd.EndTurn(Owner, canBackOut: false);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class TrueOrFalse() : SakuraModCard(0, CardType.Skill, CardRarity.Rare, TargetType.Self), IReleaseable
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(2), new EnergyVar(2)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (ShouldRelease)
        {
            await TriggerReleaseEffect(choiceContext, play);
            return;
        }

        var drawChoice = SakuraActions.CloneWithCurrentUpgrade<TrueOrFalseDrawChoice>(this);
        var energyChoice = SakuraActions.CloneWithCurrentUpgrade<TrueOrFalseEnergyChoice>(this);
        var choice = await SakuraActions.SelectFromCards(this, choiceContext, [drawChoice, energyChoice], cancelable: false);
        if (choice is TrueOrFalseEnergyChoice)
            await GainEnergy();
        else
            await Draw();

        return;

        async Task Draw() =>
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner, false);

        async Task GainEnergy() =>
            await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner);
    }

    public async Task OnReleased(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner, false);
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
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (!SakuraActions.Hand(this).Any(CanSeal))
            return;

        var card = await SakuraActions.SelectHandCard(this, choiceContext, CanSeal, cancelable: false);
        if (card is null)
            return;

        card.Stabilize();
        card.Release();
    }

    private static bool CanSeal(CardModel card) =>
        card.IsReleased();

    protected override void OnUpgrade() => AddKeywordIfMissing(CardKeyword.Retain);
}

public class DreamKeyResonance() : SakuraModCard(2, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play) =>
        await PowerCmd.Apply<DreamKeyResonancePower>(Owner.Creature, 1, Owner.Creature, this, false);

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
