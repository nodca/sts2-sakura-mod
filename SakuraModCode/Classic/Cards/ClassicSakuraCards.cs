using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
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
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Classic.Powers;
using SakuraMod.SakuraModCode.Classic.Relics;
using SakuraMod.SakuraModCode.Extensions;
using SakuraMod.SakuraModCode.Powers;
using STS2RitsuLib.Utils;

namespace SakuraMod.SakuraModCode.Classic.Cards;

public class ClowArrow() : ClassicExtraClowCard(0, CardType.Attack, CardRarity.Common, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Firey;
    public override TargetType TargetType =>
        IsMutable && ClassicSakuraMagic.CanSpendMagic(Owner)
            ? TargetType.AnyEnemy
            : base.TargetType;
    protected override bool HasEnergyCostX => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new ClassicDamageVar(5, ValueProp.Move)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (HandWithoutSelf().Count == 0 && play.Resources.EnergySpent <= 0)
            return;

        var discarded = await SelectHandCards(choiceContext, discard: true);
        await FireArrows(choiceContext, null, discarded + ResolveEnergyXValue());
    }

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (HandWithoutSelf().Count == 0 && play.Resources.EnergySpent <= 0)
            return;

        var discarded = await SelectHandCards(choiceContext, discard: true);
        await FireArrows(choiceContext, RequiredTarget(play), discarded + ResolveEnergyXValue());
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(2);

    private async Task<int> SelectHandCards(PlayerChoiceContext choiceContext, bool discard)
    {
        var hand = HandWithoutSelf();
        if (hand.Count == 0)
            return 0;

        var selected = (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, 0, hand.Count)
            {
                Cancelable = true
            },
            card => hand.Contains(card),
            this)).ToList();

        if (selected.Count > 0 && discard)
            await CardCmd.Discard(choiceContext, selected);

        return selected.Count;
    }

    private async Task FireArrows(PlayerChoiceContext choiceContext, Creature? fixedTarget, int count)
    {
        if (fixedTarget is not null)
        {
            await DealDamage(choiceContext, fixedTarget, ReleasedDamage(), hitCount: count);
            return;
        }

        await DealDamageToRandomEnemies(choiceContext, ReleasedDamage(), count);
    }

    private List<CardModel> HandWithoutSelf() =>
        CardPile.GetCards(Owner, PileType.Hand).Where(card => card != this).ToList();
}

public class SakuraArrow() : ClassicSakuraConversionCard(1, CardType.Attack, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Firey;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new ClassicDamageVar(7, ValueProp.Move)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var drawPile = CardPile.GetCards(Owner, PileType.Draw).ToList();
        var count = drawPile
            .OfType<ClassicSakuraCard>()
            .Where(static card => card.Identity is not null)
            .Select(static card => card.Identity!.Value)
            .Distinct()
            .Count();

        if (drawPile.Count > 0)
            await CardCmd.Discard(choiceContext, drawPile);

        await DealDamageToRandomEnemies(choiceContext, ReleasedDamage(), count);
    }
}

public class ClowSword() : ClassicExtraClowCard(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
{
    public override ClassicElement Element => ClassicElement.Firey;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Loner];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new ClassicDamageVar(6, ValueProp.Move, SourceCardIdentity.Sword)];
    protected override HashSet<CardTag> CanonicalTags => [CardTag.Strike];

    private int CurrentDamage() => ClassicCardValues.EffectiveValue(this, DynamicVars.Damage);

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await DealDamage(choiceContext, RequiredTarget(play), CurrentDamage());

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PlayCard(choiceContext, play);
        await DealDamage(choiceContext, RequiredTarget(play), ClassicSakuraMagic.SwordExtraHpLoss, ValueProp.Unblockable);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
}

public class SakuraSword() : ClassicSakuraConversionCard(1, CardType.Attack, TargetType.AnyEnemy)
{
    public override ClassicElement Element => ClassicElement.Firey;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new ClassicDamageVar(16, ValueProp.Move), new DynamicVar("Magic", 25)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = RequiredTarget(play);
        await DealDamage(choiceContext, target, ReleasedDamage());
        await DealDamage(choiceContext, target, target.CurrentHp * ReleasedMagic() / 100, ValueProp.Unblockable);
    }
}

public class ClowShield() : ClassicExtraClowCard(1, CardType.Skill, CardRarity.Basic, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Firey;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Loner];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new ClassicBlockVar(5, ValueProp.Move, SourceCardIdentity.Shield),
        new PowerVar<ClassicShieldWardPower>(ClassicSakuraMagic.ShieldMetallicizeBlock)
    ];
    protected override HashSet<CardTag> CanonicalTags => [CardTag.Defend];

    private int CurrentBlock() => ClassicCardValues.EffectiveValue(this, DynamicVars.Block);

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await GainBlock(play, CurrentBlock());

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PlayCard(choiceContext, play);
        await ApplyPower<ClassicShieldWardPower>(choiceContext, Owner.Creature, DynamicVars["ClassicShieldWardPower"].IntValue);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3);
}

public class SakuraShield() : ClassicSakuraConversionCard(1, CardType.Skill, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Firey;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new ClassicBlockVar(14, ValueProp.Move), new DynamicVar("Magic", 25)];

    internal static int CurrentHpBlock(int currentHp, int rate) =>
        Math.Max(0, currentHp) * Math.Max(0, rate) / 100;

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await GainBlock(play, ReleasedBlock());
        await GainBlock(play, CurrentHpBlock(Owner.Creature.CurrentHp, ReleasedMagic()));
    }
}

public class ClowJump() : ClassicExtraClowCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Windy;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new ClassicBlockVar(7, ValueProp.Move)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await RemovePlayerDebuffs(choiceContext, 1);
        await GainBlock(play, ReleasedBlock());
    }

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await RemovePlayerDebuffs(choiceContext, int.MaxValue);
        await GainBlock(play, ReleasedBlock());
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(4);

    private async Task RemovePlayerDebuffs(PlayerChoiceContext choiceContext, int maxCount)
    {
        var debuffs = Owner.Creature.Powers.Where(static power => power.TypeForCurrentAmount == PowerType.Debuff).ToList();
        for (var removed = 0; removed < maxCount && debuffs.Count > 0; removed++)
        {
            var power = maxCount == int.MaxValue
                ? debuffs[0]
                : Owner.RunState.Rng.CombatCardSelection.NextItem(debuffs);
            if (power is null)
                return;
            await PowerCmd.Remove(power);
            debuffs.Remove(power);
        }
    }
}

public class SakuraJump() : ClassicSakuraConversionCard(1, CardType.Power, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Windy;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Magic", 1)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await ApplyPower<ClassicJumpPower>(choiceContext, Owner.Creature, ReleasedMagic());
}

public class ClowIllusion() : ClassicExtraClowCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Windy;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new ClassicBlockVar(7, ValueProp.Move), new DynamicVar("Magic", 2)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await GainBlock(play, ReleasedBlock());
        await GainBlock(play, ReleasedMagic() * (CardPile.Get(PileType.Exhaust, Owner)?.Cards.Count ?? 0) / 3);
    }

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await GainBlock(play, ReleasedBlock());
        await GainBlock(play, ReleasedMagic() * (CardPile.Get(PileType.Exhaust, Owner)?.Cards.Count ?? 0));
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3);
        DynamicVars["Magic"].UpgradeValueBy(1);
    }
}

public class SakuraIllusion() : ClassicSakuraConversionCard(0, CardType.Skill, TargetType.AnyEnemy)
{
    public override ClassicElement Element => ClassicElement.Windy;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust, CardKeyword.Innate];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<WeakPower>(99), new PowerVar<VulnerablePower>(99)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = RequiredTarget(play);
        await ApplyPower<WeakPower>(choiceContext, target, DynamicVars.Weak.IntValue);
        await ApplyPower<VulnerablePower>(choiceContext, target, DynamicVars.Vulnerable.IntValue);
    }
}

public class ClowMist() : ClassicExtraClowCard(1, CardType.Skill, CardRarity.Common, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Earthy;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<PoisonPower>(4)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await ApplyPowerToEnemies<PoisonPower>(choiceContext, ReleasedValue("PoisonPower"));

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        foreach (var enemy in CombatState!.HittableEnemies)
        {
            await CreatureCmd.LoseBlock(enemy, enemy.Block);
            foreach (var power in enemy.Powers.Where(static power => power is ArtifactPower or ThornsPower or BufferPower or IntangiblePower or BarricadePower).ToList())
                await PowerCmd.Remove(power);
        }

        await PlayCard(choiceContext, play);
    }

    protected override void OnUpgrade() => DynamicVars.Poison.UpgradeValueBy(2);
}

public class SakuraMist() : ClassicSakuraConversionCard(1, CardType.Power, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Earthy;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<NoxiousFumesPower>(6)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        foreach (var enemy in CombatState!.HittableEnemies)
            await CreatureCmd.LoseBlock(enemy, enemy.Block);

        await ApplyPower<NoxiousFumesPower>(choiceContext, Owner.Creature, ReleasedValue("NoxiousFumesPower"));
    }
}

public class ClowRain() : ClassicExtraClowCard(2, CardType.Skill, CardRarity.Uncommon, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Watery;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override PileType GetResultPileTypeForCardPlay()
    {
        return SakuraExtraEffectTransaction.CanActivate(Owner)
            ? PileType.Discard
            : base.GetResultPileTypeForCardPlay();
    }

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ReduceHandCosts(Owner, 1);
        await Task.CompletedTask;
    }

    protected override Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ReduceHandCosts(Owner, 1);
        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);

    private static void ReduceHandCosts(Player owner, int amount)
    {
        foreach (var card in CardPile.GetCards(owner, PileType.Hand).Where(static card => card.EnergyCost.GetWithModifiers(CostModifiers.Local) > 0))
            card.EnergyCost.AddThisTurn(-amount, true);
    }
}

public class SakuraRain() : ClassicSakuraConversionCard(0, CardType.Skill, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Watery;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Magic", 3)];

    protected override Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var choices = CardPile.GetCards(Owner, PileType.Hand)
            .Where(static card => card.EnergyCost.GetWithModifiers(CostModifiers.Local) > 0)
            .ToList();

        var amount = Math.Min(ReleasedMagic(), choices.Count);
        List<CardModel> targets = [];
        for (var i = 0; i < amount; i++)
        {
            var card = choices.Count == amount
                ? choices[0]
                : Owner.RunState.Rng.CombatCardSelection.NextItem(choices);
            if (card is null)
                break;

            targets.Add(card);
            choices.Remove(card);
        }

        foreach (var card in targets)
            card.EnergyCost.SetThisCombat(0, true);
        return Task.CompletedTask;
    }
}

public class ClowSweet() : ClassicExtraClowCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Firey;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new HealVar(5)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await CreatureCmd.Heal(Owner.Creature, ReleasedValue("Heal"));

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await ApplyPower<RegenPower>(choiceContext, Owner.Creature, ReleasedValue("Heal"));

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class SakuraSweet() : ClassicSakuraConversionCard(2, CardType.Power, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Firey;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Magic", 10)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await ApplyPower<ClassicSweetPower>(choiceContext, Owner.Creature, ReleasedMagic());
}

public class ClowVoice() : ClassicExtraClowCard(0, CardType.Skill, CardRarity.Common, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Windy;
    public override bool HasTurnEndInHandEffect => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Ethereal, SakuraKeywords.Invisible];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new ClassicBlockVar(4, ValueProp.Move), new DynamicVar("Magic", 1), new CardsVar(1)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await AddVoiceCopies(choiceContext, 1, PileType.Discard);

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await AddVoiceCopies(choiceContext, 3, PileType.Discard);

    protected override async Task OnTurnEndInHand(PlayerChoiceContext choiceContext) =>
        await CreatureCmd.GainBlock(Owner.Creature, ReleasedBlock(), ValueProp.Move, null, false);

    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (card == this)
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner, false);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2);
        DynamicVars["Magic"].UpgradeValueBy(1);
    }

    private async Task AddVoiceCopies(PlayerChoiceContext choiceContext, int count, PileType pile)
    {
        for (var i = 0; i < count; i++)
        {
            var copy = CreateClone();
            await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
                copy,
                pile,
                Owner,
                pile == PileType.Hand ? CardPilePosition.Random : CardPilePosition.Bottom);
        }
    }
}

public class SakuraVoice() : ClassicSakuraConversionCard(1, CardType.Power, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Windy;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Magic", 1)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await ApplyPower<ClassicVoicePower>(choiceContext, Owner.Creature, ReleasedMagic());
}

public class ClowCloud() : ClassicExtraClowCard(1, CardType.Skill, CardRarity.Common, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Watery;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new ClassicBlockVar(5, ValueProp.Move)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await GainBlock(play, ReleasedBlock());

        var wateryCards = CardPile.GetCards(Owner, PileType.Hand).OfType<ClassicSakuraCard>().Count(static card => card.Element.HasElement(ClassicElement.Watery));
        for (var i = 0; i < wateryCards; i++)
            await GainBlock(play, ReleasedBlock());

        if (Owner.Creature.GetPower<ClassicWateryPower>()?.Amount > 0)
            await ClassicCloudEffects.AddRainToHand(Owner, choiceContext, freeForCombat: false);
    }

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PlayCard(choiceContext, play);
        await GainBlock(play, ClassicSakuraMagic.CloudExtraBlock);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(2);
}

public class SakuraCloud() : ClassicSakuraConversionCard(1, CardType.Skill, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Watery;
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new ClassicBlockVar(7, ValueProp.Move),
        new BlockVar("ExtraBlock", 3, ValueProp.Move)
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await GainBlock(play, ReleasedBlock());

        var wateryCards = Owner.Deck.Cards.OfType<ClassicSakuraCard>().Count(static card => card.Element.HasElement(ClassicElement.Watery));
        for (var i = 0; i < wateryCards; i++)
            await GainBlock(play, ReleasedValue("ExtraBlock"));

        await ClassicCloudEffects.AddRainToHand(Owner, choiceContext, freeForCombat: true);
    }
}

internal static class ClassicCloudEffects
{
    public static async Task AddRainToHand(Player owner, PlayerChoiceContext choiceContext, bool freeForCombat)
    {
        var combatState = owner.Creature.CombatState
            ?? throw new InvalidOperationException("Cloud-generated Rain requires an active combat.");
        var rain = combatState.CreateCard<ClowRain>(owner);
        if (freeForCombat)
        {
            rain.EnergyCost.SetThisCombat(0, reduceOnly: true);
            await SakuraGeneratedCardLifecycle.AddGeneratedCardToHand(rain, choiceContext);
            return;
        }

        await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
            rain,
            new GeneratedCardOptions { FreeThisTurn = true },
            choiceContext);
    }
}

public class ClowFlower() : ClassicExtraClowCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Earthy;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new EnergyVar(2)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await PlayerCmd.GainEnergy(ReleasedValue("Energy"), Owner);

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PlayCard(choiceContext, play);
        await PlayerCmd.GainEnergy(ClassicSakuraMagic.FlowerExtraEnergy, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Energy.UpgradeValueBy(1);
        AddKeywordIfMissing(CardKeyword.Retain);
    }
}

public class SakuraFlower() : ClassicSakuraConversionCard(0, CardType.Skill, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Earthy;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain, CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new EnergyVar(5)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await PlayerCmd.GainEnergy(ReleasedValue("Energy"), Owner);
}

public class ClowEarthy() : ClassicClowCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Earthy;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Magic", 1)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await ApplyPower<ClassicEarthyPower>(choiceContext, Owner.Creature, 1);
        await AddGeneratedSpells<SpellLeiDi>(choiceContext, ReleasedMagic());
    }

    protected override void OnUpgrade() => DynamicVars["Magic"].UpgradeValueBy(1);
}

public class SakuraEarthy() : ClassicSakuraConversionCard(1, CardType.Power, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Earthy;

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await ApplyPower<ClassicEarthyPermanentPower>(choiceContext, Owner.Creature, 1);
        await ApplyPower<ClassicEarthyPower>(choiceContext, Owner.Creature, 1);
    }
}

public class ClowFirey() : ClassicClowCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Firey;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Magic", 1)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await ApplyPower<ClassicFireyPower>(choiceContext, Owner.Creature, 1);
        await AddGeneratedSpells<SpellHuoShen>(choiceContext, ReleasedMagic());
    }

    protected override void OnUpgrade() => DynamicVars["Magic"].UpgradeValueBy(1);
}

public class SakuraFirey() : ClassicSakuraConversionCard(1, CardType.Power, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Firey;

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await ApplyPower<ClassicFireyPermanentPower>(choiceContext, Owner.Creature, 1);
        await ApplyPower<ClassicFireyPower>(choiceContext, Owner.Creature, 1);
    }
}

public class ClowWatery() : ClassicClowCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Watery;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Magic", 1)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await ApplyPower<ClassicWateryPower>(choiceContext, Owner.Creature, 1);
        await AddGeneratedSpells<SpellShuiLong>(choiceContext, ReleasedMagic());
    }

    protected override void OnUpgrade() => DynamicVars["Magic"].UpgradeValueBy(1);
}

public class SakuraWatery() : ClassicSakuraConversionCard(1, CardType.Power, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Watery;

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await ApplyPower<ClassicWateryPermanentPower>(choiceContext, Owner.Creature, 1);
        await ApplyPower<ClassicWateryPower>(choiceContext, Owner.Creature, 1);
    }
}

public class ClowWindy() : ClassicClowCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Windy;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Magic", 1)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await ApplyPower<ClassicWindyPower>(choiceContext, Owner.Creature, 1);
        await AddGeneratedSpells<SpellFengHua>(choiceContext, ReleasedMagic());
    }

    protected override void OnUpgrade() => DynamicVars["Magic"].UpgradeValueBy(1);
}

public class SakuraWindy() : ClassicSakuraConversionCard(1, CardType.Power, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Windy;

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await ApplyPower<ClassicWindyPermanentPower>(choiceContext, Owner.Creature, 1);
        await ApplyPower<ClassicWindyPower>(choiceContext, Owner.Creature, 1);
    }
}

public class ClowWave() : ClassicClowCard(1, CardType.Power, CardRarity.Uncommon, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Watery;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Magic", 2)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await ApplyPower<ClassicWavePower>(choiceContext, Owner.Creature, ReleasedMagic());

    protected override void OnUpgrade() => DynamicVars["Magic"].UpgradeValueBy(1);
}

public class SakuraWave() : ClassicSakuraConversionCard(1, CardType.Skill, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Watery;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await ApplyPower<ClassicEarthyPower>(choiceContext, Owner.Creature, 1);
        await ApplyPower<ClassicFireyPower>(choiceContext, Owner.Creature, 1);
        await ApplyPower<ClassicWateryPower>(choiceContext, Owner.Creature, 1);
        await ApplyPower<ClassicWindyPower>(choiceContext, Owner.Creature, 1);
    }
}

public class ClowBig() : ClassicClowCard(1, CardType.Power, CardRarity.Uncommon, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Firey;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<StrengthPower>(3)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await ApplyPower<StrengthPower>(choiceContext, Owner.Creature, ReleasedValue("StrengthPower"));
        if (!IsUpgraded)
            await ApplyPower<DexterityPower>(choiceContext, Owner.Creature, -1);
    }
}

public class SakuraBig() : ClassicSakuraConversionCard(1, CardType.Power, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Firey;

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await ApplyPower<ClassicBigPower>(choiceContext, Owner.Creature, 1);
}

public class ClowBubbles() : ClassicExtraClowCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override ClassicElement Element => ClassicElement.Watery;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new ClassicDamageVar(5, ValueProp.Move), new DynamicVar("Magic", 2)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = RequiredTarget(play);
        await DealDamage(choiceContext, target, ReleasedDamage());
        var removed = await RemoveRandomBuff(choiceContext, target);
        await GainUpgradeMagicCharge(choiceContext, removed);
    }

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = RequiredTarget(play);
        await DealDamage(choiceContext, target, ReleasedDamage());
        var removed = await RemoveAllBuffs(target);
        await GainUpgradeMagicCharge(choiceContext, removed);
    }

    protected override void OnUpgrade() { }

    private async Task GainUpgradeMagicCharge(PlayerChoiceContext choiceContext, bool removed)
    {
        if (IsUpgraded && removed && Owner.GetRelic<ClassicSealedBookRelic>() is not null)
            await ClassicSakuraMagic.GainMagic(choiceContext, Owner, ReleasedMagic(), this);
    }

    private async Task<bool> RemoveRandomBuff(PlayerChoiceContext choiceContext, Creature target)
    {
        var buffs = target.Powers.Where(ClassicPowerRules.IsBubblesRemovableBuff).ToList();
        var buff = Owner.RunState.Rng.CombatCardSelection.NextItem(buffs);
        if (buff is null)
            return false;

        await PowerCmd.Remove(buff);
        return true;
    }

    private static async Task<bool> RemoveAllBuffs(Creature target)
    {
        var removed = false;
        foreach (var buff in target.Powers.Where(ClassicPowerRules.IsBubblesRemovableBuff).ToList())
        {
            await PowerCmd.Remove(buff);
            removed = true;
        }

        return removed;
    }
}

public class SakuraBubbles() : ClassicSakuraConversionCard(0, CardType.Attack, TargetType.AllEnemies)
{
    public override ClassicElement Element => ClassicElement.Watery;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust, CardKeyword.Retain];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new ClassicDamageVar(5, ValueProp.Move)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var enemies = CombatState!.HittableEnemies.ToList();
        await DealDamageToEnemies(choiceContext, enemies, ReleasedDamage());
        foreach (var enemy in enemies)
        {
            foreach (var buff in enemy.Powers.Where(ClassicPowerRules.IsBubblesRemovableBuff).ToList())
                await PowerCmd.Remove(buff);
        }
    }
}

public class ClowCreate() : ClassicClowCard(CreateBaseCost, CardType.Power, CardRarity.Rare, TargetType.None)
{
    private const int CreateBaseCost = 5;
    private const int CreateRewardCount = 1;
    private static readonly SavedAttachedState<ClowCreate, int> CostReductions =
        new("SakuraMod_ClowCreateCostReductions", () => 0);

    public override int MaxUpgradeLevel => 0;
    public override ClassicElement Element => ClassicElement.Earthy | ClassicElement.Windy;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Relics", CreateRewardCount)];

    public override void AfterCreated() => ApplyCostReduction();

    protected override void AfterDeserialized() => ApplyCostReduction();

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (!await TryRemoveDeckVersion())
            return;

        await ApplyPower<ClassicCreatePower>(choiceContext, Owner.Creature, DynamicVars["Relics"].IntValue);
        ClassicCreateRewards.AddNormalRelicReward(Owner);
    }

    public static void ReduceCostAtCombatStart(Player owner)
    {
        foreach (var deckCard in owner.Deck.Cards.OfType<ClowCreate>())
            deckCard.ReduceCostOnce();

        foreach (var combatCard in owner.Piles
                     .Where(static pile => pile.Type.IsCombatPile())
                     .SelectMany(static pile => pile.Cards)
                     .OfType<ClowCreate>())
        {
            var deckSource = combatCard.DeckSource();
            if (deckSource is not null)
                combatCard.ApplyCostReduction(CostReductions[deckSource]);
        }
    }

    private async Task<bool> TryRemoveDeckVersion()
    {
        var deckCard = DeckSource();
        if (deckCard?.Pile?.Type != PileType.Deck)
            return false;

        await CardPileCmd.RemoveFromDeck(deckCard, showPreview: false);
        return true;
    }

    private void ReduceCostOnce()
    {
        if (CostReductions[this] >= CreateBaseCost)
            return;

        CostReductions[this]++;
        ApplyCostReduction();
    }

    private void ApplyCostReduction() =>
        ApplyCostReduction(CostReductions[this]);

    private void ApplyCostReduction(int reductions) =>
        EnergyCost.SetCustomBaseCost(Math.Max(0, CreateBaseCost - reductions));

    private ClowCreate? DeckSource()
    {
        HashSet<CardModel> seen = [];
        for (CardModel? current = this; current is not null && seen.Add(current); current = current.CloneOf)
        {
            if (current.DeckVersion is ClowCreate deckVersion)
                return deckVersion;
        }

        return Pile?.Type == PileType.Deck ? this : null;
    }
}

public class SakuraCreate() : ClassicSakuraConversionCard(0, CardType.Power, TargetType.None)
{
    private const int CreateRewardCount = 2;

    public override ClassicElement Element => ClassicElement.Earthy | ClassicElement.Windy;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Relics", CreateRewardCount)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (!await TryRemoveCreateFromDeck())
            return;

        await ApplyPower<ClassicCreatePower>(choiceContext, Owner.Creature, DynamicVars["Relics"].IntValue);
        ClassicCreateRewards.AddNormalRelicReward(Owner);
        ClassicCreateRewards.AddExclusiveOrNormalRelicReward(Owner);
    }

    private async Task<bool> TryRemoveCreateFromDeck()
    {
        var deckCard = Owner.Deck.Cards.OfType<SakuraCreate>().FirstOrDefault();
        if (deckCard is null)
            return false;

        await CardPileCmd.RemoveFromDeck(deckCard, showPreview: false);
        return true;
    }
}

public class ClowReturn() : ClassicClowCard(1, CardType.Power, CardRarity.Rare, TargetType.None)
{
    private const int Duration = 2;
    private const int VoidCount = 2;

    public override ClassicElement Element => ClassicElement.Watery;
    public override IEnumerable<CardKeyword> CanonicalKeywords => IsUpgraded ? [CardKeyword.Retain] : [];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Magic", Duration), new DynamicVar("Voids", VoidCount)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (Owner.Creature.GetPower<ClassicReturnPower>() is { } existing)
        {
            existing.SetAmount(ReleasedMagic());
        }
        else
        {
            await ApplyPower<ClassicReturnPower>(choiceContext, Owner.Creature, ReleasedMagic());
        }

        if (!IsUpgraded)
        {
            for (var i = 0; i < DynamicVars["Voids"].IntValue; i++)
                await ClassicSakuraMagic.AddVoidToDiscardPile(choiceContext, Owner);
        }
    }

    protected override void OnUpgrade() => AddKeywordIfMissing(CardKeyword.Retain);
}

public class SakuraReturn() : ClassicSakuraConversionCard(0, CardType.Power, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Watery;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new ClassicReturnRechargeVar()];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var deckCard = Owner.Deck.Cards.OfType<SakuraReturn>().FirstOrDefault();
        if (deckCard is null)
            return;

        var missingHp = Math.Max(0, Owner.Creature.MaxHp - Owner.Creature.CurrentHp);
        if (missingHp > 0)
            await CreatureCmd.Heal(Owner.Creature, missingHp);

        Owner.GetRelic<ClassicSealedWandRelic>()?.AddReturnRecharge();
        await CardPileCmd.RemoveFromDeck(deckCard, showPreview: false);
    }
}

public class ClowDream() : ClassicExtraClowCard(1, CardType.Skill, CardRarity.Common, TargetType.None)
{
    private const int Cards = 1;
    private const int ExtraCards = 1;

    public override ClassicElement Element => ClassicElement.Windy;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(Cards), new DynamicVar("ExtraCards", ExtraCards)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await AddDreamCards(ReleasedValue("Cards"));

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await AddDreamCards(ReleasedValue("Cards") + DynamicVars["ExtraCards"].IntValue);

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);

    private async Task AddDreamCards(int count)
    {
        for (var i = 0; i < count; i++)
        {
            var card = ClassicSakuraCardCatalog.CreateRandomDreamClowCard(Owner);
            ClassicSakuraMagic.SetFreeForRestOfTurn(card);
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner, CardPilePosition.Random);
        }
    }
}

public class SakuraDream() : ClassicSakuraConversionCard(0, CardType.Skill, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Windy;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (Owner.Creature.GetPower<ClassicDreamPower>() is { } existing)
        {
            await existing.ConvertCurrentHand();
            return;
        }

        await ApplyPower<ClassicDreamPower>(choiceContext, Owner.Creature, 1);
    }
}

public class ClowDark() : ClassicClowCard(1, CardType.Power, CardRarity.Rare, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Windy | ClassicElement.Watery;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Magic", 1)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await ApplyPower<ClassicDarkPower>(choiceContext, Owner.Creature, ReleasedMagic());

    protected override void OnUpgrade() => DynamicVars["Magic"].UpgradeValueBy(2);
}

public class SakuraDark() : ClassicSakuraConversionCard(1, CardType.Power, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Windy | ClassicElement.Watery;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Magic", 2)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await ApplyPower<ClassicDarkSakuraPower>(choiceContext, Owner.Creature, ReleasedMagic());
}

public class ClowMirror() : ClassicExtraClowCard(1, CardType.Skill, CardRarity.Common, TargetType.None)
{
    private static LocString Prompt => CardLoc<ClowMirror>("selectionPrompt");

    public override ClassicElement Element => ClassicElement.Watery;
    public override IEnumerable<CardKeyword> CanonicalKeywords => IsUpgraded
        ? [CardKeyword.Exhaust]
        : [CardKeyword.Ethereal, CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Copies", 1), new DynamicVar("ExtraCopies", 1)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await CopyMirrorTarget(choiceContext, DynamicVars["Copies"].IntValue);

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await CopyMirrorTarget(choiceContext, DynamicVars["Copies"].IntValue + DynamicVars["ExtraCopies"].IntValue);

    protected override void OnUpgrade()
    {
        if (Keywords.Contains(CardKeyword.Ethereal))
            RemoveKeyword(CardKeyword.Ethereal);
    }

    private async Task CopyMirrorTarget(PlayerChoiceContext choiceContext, int copies)
    {
        var choices = CardPile.GetCards(Owner, PileType.Hand)
            .Where(IsMirrorCopyCandidate)
            .ToList();
        if (choices.Count == 0)
            return;

        var selected = (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(Prompt, 1)
            {
                Cancelable = false,
                RequireManualConfirmation = false
            },
            card => choices.Contains(card),
            this)).FirstOrDefault();

        if (selected is null)
            return;

        for (var i = 0; i < copies; i++)
        {
            var copy = ClassicSakuraCardCatalog.CreateMirrorCopySource(selected);
            await CardPileCmd.AddGeneratedCardToCombat(copy, PileType.Hand, Owner, CardPilePosition.Random);
        }
    }

    private bool IsMirrorCopyCandidate(CardModel card) =>
        card != this
        && card is ClassicSakuraCard { IsClassicSourceCard: true, Identity: { } identity }
        && identity is not SourceCardIdentity.Mirror and not SourceCardIdentity.Create;
}

public class SakuraMirror() : ClassicSakuraConversionCard(0, CardType.Skill, TargetType.None)
{
    private static LocString Prompt => CardLoc<SakuraMirror>("selectionPrompt");

    public override ClassicElement Element => ClassicElement.Watery;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var combatState = Owner.Creature.CombatState
            ?? throw new InvalidOperationException("Sakura Mirror card choices require an active combat.");
        var choices = ClassicSakuraCardCatalog.AllClowTemplates()
            .Select(template => combatState.CreateCard(template, Owner))
            .ToList();
        if (choices.Count == 0)
            return;

        CardModel? selected = null;
        try
        {
            selected = choices.Count == 1
                ? choices[0]
                : (await CardSelectCmd.FromSimpleGrid(
                    choiceContext,
                    choices,
                    Owner,
                    new CardSelectorPrefs(Prompt, 1)
                    {
                        Cancelable = false,
                        RequireManualConfirmation = false
                    })).FirstOrDefault();

            if (selected is null)
                return;

            var copy = ClassicSakuraCardCatalog.CreateMirrorCopySource(selected);
            await CardPileCmd.AddGeneratedCardToCombat(copy, PileType.Hand, Owner, CardPilePosition.Random);
        }
        finally
        {
            foreach (var choice in choices)
            {
                if (choice.Pile is null)
                    choice.CardScope?.RemoveCard(choice);
            }
        }
    }
}

public class ClowLittle() : ClassicClowCard(1, CardType.Power, CardRarity.Uncommon, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Watery;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<DexterityPower>(3)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await ApplyPower<DexterityPower>(choiceContext, Owner.Creature, ReleasedValue("DexterityPower"));
        if (!IsUpgraded)
            await ApplyPower<StrengthPower>(choiceContext, Owner.Creature, -1);
    }
}

public class SakuraLittle() : ClassicSakuraConversionCard(1, CardType.Power, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Watery;

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await ApplyPower<ClassicLittlePower>(choiceContext, Owner.Creature, 1);
}

public class ClowLight() : ClassicClowCard(1, CardType.Power, CardRarity.Rare, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Earthy | ClassicElement.Firey;

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var power = await PowerCmd.Apply<ClassicLightPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this, false);
        if (IsUpgraded)
            power?.MarkUpgraded();
    }
}

public class SakuraLight() : ClassicSakuraConversionCard(1, CardType.Power, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Earthy | ClassicElement.Firey;

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await ApplyPower<ClassicLightSakuraPower>(choiceContext, Owner.Creature, 1);
}

public class ClowLock() : ClassicExtraClowCard(1, CardType.Skill, CardRarity.Common, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Firey;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Magic", 2), new EnergyVar(2)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var hand = CardPile.GetCards(Owner, PileType.Hand).Where(card => card != this).ToList();
        if (hand.Count == 0)
            return;

        var selected = (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 0, Math.Min(ReleasedMagic(), hand.Count))
            {
                Cancelable = true
            },
            card => hand.Contains(card),
            this)).ToList();
        if (selected.Count == 0)
            return;

        foreach (var card in selected)
            await CardCmd.Exhaust(choiceContext, card, false);

        var copy = CreateClone();
        await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
            copy,
            PileType.Discard,
            Owner,
            CardPilePosition.Bottom);

        await ClassicSakuraMagic.GainMagic(choiceContext, Owner, selected.Count, this);
    }

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PlayCard(choiceContext, play);
        await PlayerCmd.GainEnergy(ReleasedValue("Energy"), Owner);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class SakuraLock() : ClassicSakuraConversionCard(1, CardType.Skill, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Firey;

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await ApplyPower<ClassicLockSakuraPower>(choiceContext, Owner.Creature, 1);
}

public class ClowLoop() : ClassicClowCard(1, CardType.Power, CardRarity.Uncommon, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Earthy;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await ApplyPower<ClassicLoopPower>(choiceContext, Owner.Creature, ReleasedValue("Cards"));

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1);
}

public class SakuraLoop() : ClassicSakuraConversionCard(1, CardType.Power, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Earthy;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await ApplyPower<ClassicLoopSakuraPower>(choiceContext, Owner.Creature, ReleasedValue("Cards"));
}

public class ClowPower() : ClassicExtraClowCard(2, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    private const int ExtraHpLoss = 15;

    public override ClassicElement Element => ClassicElement.Firey;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new ClassicDamageVar(26, ValueProp.Move)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await DealDamage(choiceContext, RequiredTarget(play), ReleasedDamage());
        await ApplyPower<StrengthPower>(choiceContext, Owner.Creature, -1);
    }

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PlayCard(choiceContext, play);
        await DealDamage(choiceContext, RequiredTarget(play), ExtraHpLoss, ValueProp.Unblockable);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(8);
}

public class SakuraPower() : ClassicSakuraConversionCard(2, CardType.Power, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Firey;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<StrengthPower>(5)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await ApplyPower<StrengthPower>(choiceContext, Owner.Creature, ReleasedValue("StrengthPower"));
}

public class ClowMaze() : ClassicExtraClowCard(2, CardType.Skill, CardRarity.Common, TargetType.None)
{
    private const int ExtraBlock = 14;

    public override ClassicElement Element => ClassicElement.Earthy;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new ClassicBlockVar(17, ValueProp.Move)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await GainBlock(play, ReleasedBlock());

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PlayCard(choiceContext, play);
        await GainBlock(play, ExtraBlock);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(6);
}

public class SakuraMaze() : ClassicSakuraConversionCard(1, CardType.Skill, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Earthy;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new ClassicBlockVar(8, ValueProp.Move), new DynamicVar("Magic", 3)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        for (var i = 0; i < ReleasedMagic(); i++)
            await GainBlock(play, ReleasedBlock());
    }
}

public class ClowSand() : ClassicExtraClowCard(0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    private const int ExtraPoison = 7;

    public override ClassicElement Element => ClassicElement.Earthy;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new ClassicDamageVar(3, ValueProp.Move), new PowerVar<PoisonPower>(2)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = RequiredTarget(play);
        await DealDamage(choiceContext, target, ReleasedDamage());
        await ApplyPower<PoisonPower>(choiceContext, target, ReleasedValue("PoisonPower"));
    }

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PlayCard(choiceContext, play);
        await ApplyPower<PoisonPower>(choiceContext, RequiredTarget(play), ExtraPoison);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2);
        DynamicVars["PoisonPower"].UpgradeValueBy(1);
    }
}

public class SakuraSand() : ClassicSakuraConversionCard(0, CardType.Attack, TargetType.AnyEnemy)
{
    public override ClassicElement Element => ClassicElement.Earthy;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new ClassicDamageVar(6, ValueProp.Move), new DynamicVar("Magic", 6)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = RequiredTarget(play);
        await DealDamage(choiceContext, target, ReleasedDamage());
        for (var i = 0; i < ReleasedMagic(); i++)
            await ApplyPower<PoisonPower>(choiceContext, target, 1);
    }
}

public class ClowShot() : ClassicExtraClowCard(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    private const int Hits = 2;
    private const int ExtraVulnerable = 3;

    public override ClassicElement Element => ClassicElement.Firey;
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new ClassicDamageVar(4, ValueProp.Move),
        new DynamicVar("Hits", Hits),
        new PowerVar<VigorPower>(2)
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await DealDamage(choiceContext, RequiredTarget(play), ReleasedDamage(), hitCount: Hits);
        await ApplyPower<VigorPower>(choiceContext, Owner.Creature, ReleasedValue("VigorPower"));
    }

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PlayCard(choiceContext, play);
        await ApplyPower<VulnerablePower>(choiceContext, RequiredTarget(play), ExtraVulnerable);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(1);
    }
}

public class SakuraShot() : ClassicSakuraConversionCard(1, CardType.Attack, TargetType.AnyEnemy)
{
    public override ClassicElement Element => ClassicElement.Firey;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new ClassicDamageVar(1, ValueProp.Move), new DynamicVar("Magic", 12)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = RequiredTarget(play);
        await DealDamage(choiceContext, target, ReleasedDamage(), hitCount: ReleasedMagic());
    }
}

public class ClowShadow() : ClassicExtraClowCard(1, CardType.Skill, CardRarity.Common, TargetType.None)
{
    private const int ExtraBlur = 2;

    public override ClassicElement Element => ClassicElement.Earthy;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new ClassicBlockVar(5, ValueProp.Move), new PowerVar<BlurPower>(1)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await GainBlock(play, ReleasedBlock());
        await ApplyPower<BlurPower>(choiceContext, Owner.Creature, ReleasedValue("BlurPower"));
    }

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PlayCard(choiceContext, play);
        await ApplyPower<BlurPower>(choiceContext, Owner.Creature, ExtraBlur);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3);
}

public class SakuraShadow() : ClassicSakuraConversionCard(1, CardType.Skill, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Earthy;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust, CardKeyword.Retain];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new ClassicBlockVar(8, ValueProp.Move), new PowerVar<IntangiblePower>(2)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await GainBlock(play, ReleasedBlock());
        await ApplyPower<IntangiblePower>(choiceContext, Owner.Creature, ReleasedValue("IntangiblePower"));
    }
}

public class ClowDash() : ClassicExtraClowCard(0, CardType.Skill, CardRarity.Rare, TargetType.None)
{
    private const int ExtraDraw = 2;

    public override ClassicElement Element => ClassicElement.Windy;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(3)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await CardPileCmd.Draw(choiceContext, ReleasedValue("Cards"), Owner, false);

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await CardPileCmd.Draw(choiceContext, ReleasedValue("Cards") + ExtraDraw, Owner, false);

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1);
}

public class SakuraDash() : ClassicSakuraConversionCard(0, CardType.Skill, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Windy;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await CardPileCmd.Draw(choiceContext, 99, Owner, false);
}

public class ClowFly() : ClassicExtraClowCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.None)
{
    private const int MagicChargeCost = 3;
    private const int ExtraDraw = 2;

    public override ClassicElement Element => ClassicElement.Windy;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(2)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await CardPileCmd.Draw(choiceContext, ReleasedValue("Cards"), Owner, false);
        if (!IsUpgraded)
            await ClassicSakuraMagic.SpendUpToMagic(choiceContext, Owner, MagicChargeCost);
    }

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await CardPileCmd.Draw(choiceContext, ReleasedValue("Cards") + ExtraDraw, Owner, false);
}

public class SakuraFly() : ClassicSakuraConversionCard(1, CardType.Power, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Windy;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(2)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await ApplyPower<ClassicFlyPower>(choiceContext, Owner.Creature, ReleasedValue("Cards"));
}

public class ClowGlow() : ClassicClowCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.None)
{
    private const int MagicChargeGain = 3;

    public override ClassicElement Element => ClassicElement.Firey;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1), new DynamicVar("Magic", MagicChargeGain)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await CardPileCmd.Draw(choiceContext, ReleasedValue("Cards"), Owner, false);
        if (Owner.GetRelic<ClassicSealedBookRelic>() is not null)
            await ClassicSakuraMagic.GainMagic(choiceContext, Owner, ReleasedMagic(), this);
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1);
}

public class SakuraGlow() : ClassicSakuraConversionCard(1, CardType.Power, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Firey;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Magic", 4)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await ApplyPower<ClassicGlowPower>(choiceContext, Owner.Creature, ReleasedMagic());
}

public class ClowLibra() : ClassicExtraClowCard(0, CardType.Skill, CardRarity.Common, TargetType.None)
{
    private const int ExtraBlock = 12;

    public override ClassicElement Element => ClassicElement.Firey;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new ClassicBlockVar(4, ValueProp.Move), new DynamicVar("Magic", 3)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (Owner.Creature.Block == 0)
        {
            await ClassicSakuraMagic.SpendUpToMagic(choiceContext, Owner, ReleasedMagic());
            await GainBlock(play, ReleasedBlock());
        }

        await GainBlock(play, ReleasedBlock());
    }

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PlayCard(choiceContext, play);
        await GainBlock(play, ExtraBlock);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2);
        DynamicVars["Magic"].UpgradeValueBy(-1);
    }
}

public class SakuraLibra() : ClassicSakuraConversionCard(0, CardType.Skill, TargetType.None)
{
    private const int ChargePerEnergy = 3;

    public override ClassicElement Element => ClassicElement.Firey;
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new ClassicBlockVar(3, ValueProp.Move),
        new DynamicVar("Magic", ChargePerEnergy),
        new EnergyVar(1)
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var charge = await ClassicSakuraMagic.SpendAllMagic(choiceContext, Owner);
        for (var i = 0; i < charge; i++)
            await GainBlock(play, ReleasedBlock());

        var energy = EnergyFromCharge(charge) * ReleasedValue("Energy");
        if (energy > 0)
            await PlayerCmd.GainEnergy(energy, Owner);
    }

    internal static int EnergyFromCharge(int charge) =>
        Math.Max(0, charge) / ChargePerEnergy;
}

public class ClowChange() : ClassicExtraClowCard(1, CardType.Skill, CardRarity.Common, TargetType.None)
{
    private const int ExtraDraw = 2;

    public override ClassicElement Element => ClassicElement.Earthy;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(2)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await ChangeCards(choiceContext, ReleasedValue("Cards"));

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await ChangeCards(choiceContext, ReleasedValue("Cards"));
        await CardPileCmd.Draw(choiceContext, ExtraDraw, Owner, false);
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1);

    private async Task ChangeCards(PlayerChoiceContext choiceContext, int drawPerDiscard)
    {
        var candidates = CardPile.GetCards(Owner, PileType.Hand).Where(card => card != this).ToList();
        if (candidates.Count == 0)
            return;

        var selected = await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(HandPrompt, 0, 1)
            {
                Cancelable = true,
                RequireManualConfirmation = false
            },
            card => candidates.Contains(card),
            this);

        var cards = selected.ToList();
        if (cards.Count == 0)
            return;

        await CardCmd.Discard(choiceContext, cards);
        await CardPileCmd.Draw(choiceContext, drawPerDiscard * cards.Count, Owner, false);
    }
}

public class SakuraChange() : ClassicSakuraConversionCard(0, CardType.Skill, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Earthy;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(5)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var hand = CardPile.GetCards(Owner, PileType.Hand).Where(card => card != this).ToList();
        if (hand.Count > 0)
            await CardCmd.Discard(choiceContext, hand);
        await CardPileCmd.Draw(choiceContext, ReleasedValue("Cards"), Owner, false);
    }
}

public class ClowMove() : ClassicExtraClowCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.None)
{
    private const int ExtraEmptySpells = 2;

    public override ClassicElement Element => ClassicElement.Firey;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Magic", 2)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await AddGeneratedSpells<SpellEmptySpell>(choiceContext, ReleasedMagic());

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await AddGeneratedSpells<SpellEmptySpell>(choiceContext, ReleasedMagic() + ExtraEmptySpells);

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class SakuraMove() : ClassicSakuraConversionCard(0, CardType.Skill, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Firey;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Magic", 2)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await ApplyPower<ClassicMovePower>(choiceContext, Owner.Creature, ReleasedMagic());
}

public class ClowFight() : ClassicExtraClowCard(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
{
    private const int ExtraStrength = 2;

    public override ClassicElement Element => ClassicElement.Earthy;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new ClassicDamageVar(9, ValueProp.Move), new DynamicVar("Magic", 1)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await DealDamage(choiceContext, RequiredTarget(play), ReleasedDamage());
        await ClassicSakuraMagic.GainMagic(choiceContext, Owner, 1, this);
        await ApplyPower<ClassicTemporaryStrengthPower>(choiceContext, Owner.Creature, ReleasedMagic());
        await AddFightCopy(choiceContext);
    }

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PlayCard(choiceContext, play);
        await ApplyPower<StrengthPower>(choiceContext, Owner.Creature, ExtraStrength);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2);
        DynamicVars["Magic"].UpgradeValueBy(1);
    }

    private async Task AddFightCopy(PlayerChoiceContext choiceContext)
    {
        var copy = SakuraActions.CloneWithCurrentUpgrade<ClowFight>(this);
        await CardPileCmd.AddGeneratedCardToCombat(copy, PileType.Hand, Owner, CardPilePosition.Random);
    }
}

public class SakuraFight() : ClassicSakuraConversionCard(1, CardType.Attack, TargetType.AnyEnemy)
{
    public override ClassicElement Element => ClassicElement.Earthy;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new ClassicDamageVar(13, ValueProp.Move), new PowerVar<StrengthPower>(2)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await DealDamage(choiceContext, RequiredTarget(play), ReleasedDamage());
        await ApplyPower<StrengthPower>(choiceContext, Owner.Creature, ReleasedValue("StrengthPower"));
    }
}

public class ClowFloat() : ClassicClowCard(1, CardType.Power, CardRarity.Uncommon, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Windy;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Magic", 1)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await ApplyPower<ClassicFloatPower>(choiceContext, Owner.Creature, ReleasedMagic());

    protected override void OnUpgrade() => AddKeywordIfMissing(CardKeyword.Innate);
}

public class SakuraFloat() : ClassicSakuraConversionCard(1, CardType.Power, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Windy;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Innate];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Magic", 2)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await ApplyPower<ClassicFloatSakuraPower>(choiceContext, Owner.Creature, ReleasedMagic());
}

internal static class ClassicFreezeRules
{
    public static int FrostbiteAmount(int baseFrostbite, int normalTargetBonus, bool isEliteOrBossTarget) =>
        baseFrostbite + (isEliteOrBossTarget ? 0 : normalTargetBonus);
}

public class ClowFreeze() : ClassicExtraClowCard(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override ClassicElement Element => ClassicElement.Watery;
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new ClassicDamageVar(14, ValueProp.Move),
        new ClassicBlockVar(6, ValueProp.Move),
        new PowerVar<SakuraFrostbitePower>(1)
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = RequiredTarget(play);
        await DealDamage(choiceContext, target, ReleasedDamage());
        await GainBlock(play, ReleasedBlock());
        await ApplyPower<SakuraFrostbitePower>(choiceContext, target, 1);
    }

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = RequiredTarget(play);
        await DealDamage(choiceContext, target, ReleasedDamage());
        await GainBlock(play, ReleasedBlock());
        await ApplyPower<SakuraFrostbitePower>(choiceContext, target, 1);

        if (target.GetPower<SakuraFrostbitePower>() is { Amount: > 0 } frostbite)
            await PowerCmd.ModifyAmount(choiceContext, frostbite, frostbite.Amount, Owner.Creature, this, false);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4);
        DynamicVars.Block.UpgradeValueBy(2);
    }
}

public class SakuraFreeze() : ClassicSakuraConversionCard(2, CardType.Attack, TargetType.AnyEnemy)
{
    public override ClassicElement Element => ClassicElement.Watery;
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new ClassicDamageVar(22, ValueProp.Move),
        new ClassicBlockVar(10, ValueProp.Move),
        new PowerVar<SakuraFrostbitePower>(3),
        new DynamicVar("ExtraFrostbite", 3)
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = RequiredTarget(play);
        await DealDamage(choiceContext, target, ReleasedDamage());
        await GainBlock(play, ReleasedBlock());
        var isEliteOrBossTarget = ClassicEnemyRules.IsEliteOrBossTarget(target);
        var frostbite = ClassicFreezeRules.FrostbiteAmount(
            ReleasedValue("SakuraFrostbitePower"),
            ReleasedValue("ExtraFrostbite"),
            isEliteOrBossTarget);
        await ApplyPower<SakuraFrostbitePower>(choiceContext, target, frostbite);
    }
}

public class ClowStorm() : ClassicExtraClowCard(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    private const int Hits = 5;

    public override ClassicElement Element => ClassicElement.Windy;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new ClassicDamageVar(4, ValueProp.Move), new DynamicVar("Magic", Hits)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await ClassicSakuraMagic.AddVoidToDiscardPile(choiceContext, Owner);
        var target = RequiredTarget(play);
        await DealDamage(choiceContext, target, ReleasedDamage(), hitCount: ReleasedMagic());
    }

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await ClassicSakuraMagic.AddVoidToDiscardPile(choiceContext, Owner);
        await DealDamageToEnemies(choiceContext, CombatState!.HittableEnemies, ReleasedDamage(), hitCount: ReleasedMagic());
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(2);
}

public class SakuraStorm() : ClassicSakuraConversionCard(1, CardType.Attack, TargetType.None)
{
    private const int MaxDamageOffset = 8;

    public override ClassicElement Element => ClassicElement.Windy;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new ClassicDamageVar(1, ValueProp.Move), new DynamicVar("MaxDamage", 9), new DynamicVar("Magic", 7)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await using var attack = await AttackCommand.CreateContextAsync(CombatState!, choiceContext, this);
        for (var i = 0; i < ReleasedMagic(); i++)
        {
            var target = Owner.RunState.Rng.CombatCardSelection.NextItem(CombatState!.HittableEnemies.ToList());
            if (target is null)
                return;

            var amount = Owner.RunState.Rng.CombatCardSelection.NextInt(ReleasedDamage(), ReleasedDamage() + MaxDamageOffset + 1);
            await DealDamageHit(attack, choiceContext, target, amount);
        }
    }
}

public class ClowThrough() : ClassicExtraClowCard(1, CardType.Attack, CardRarity.Rare, TargetType.None)
{
    private const int ExtraMaxHpRate = 20;

    public override ClassicElement Element => ClassicElement.Earthy;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new ClassicDamageVar(12, ValueProp.Move), new DynamicVar("Magic", 0), new DynamicVar("Rate", ExtraMaxHpRate)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await DealDamageToEnemies(choiceContext, CombatState!.HittableEnemies, ReleasedDamage());
        await TriggerPoisonFollowUp(choiceContext, includeAddedPoison: false);
        await ClassicSakuraMagic.AddVoidToDiscardPile(choiceContext, Owner);
    }

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await DealDamageToEnemies(choiceContext, CombatState!.HittableEnemies, ReleasedDamage());
        await TriggerPoisonFollowUp(choiceContext, includeAddedPoison: true);
        foreach (var enemy in CombatState!.HittableEnemies.ToList())
        {
            var damage = enemy.MaxHp * ExtraMaxHpRate / 100;
            if (damage > 0)
                await DealDamage(choiceContext, enemy, damage);
        }
        await ClassicSakuraMagic.AddVoidToDiscardPile(choiceContext, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3);
        DynamicVars["Magic"].UpgradeValueBy(2);
    }

    private async Task TriggerPoisonFollowUp(PlayerChoiceContext choiceContext, bool includeAddedPoison)
    {
        foreach (var enemy in CombatState!.HittableEnemies.ToList())
        {
            var poison = enemy.GetPower<PoisonPower>()?.Amount ?? 0;
            if (poison <= 0)
                continue;

            if (ReleasedMagic() > 0)
                await ApplyPower<PoisonPower>(choiceContext, enemy, ReleasedMagic());
            var damage = poison + (includeAddedPoison ? ReleasedMagic() : 0);
            await DealDamage(choiceContext, enemy, damage, ValueProp.Unblockable);
        }
    }
}

public class SakuraThrough() : ClassicSakuraConversionCard(1, CardType.Attack, TargetType.None)
{
    private const int MinDamage = 10;
    private const int MaxHpRate = 10;

    public override ClassicElement Element => ClassicElement.Earthy;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new ClassicDamageVar(MinDamage, ValueProp.Move), new DynamicVar("Rate", MaxHpRate)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        foreach (var enemy in CombatState!.HittableEnemies.ToList())
        {
            var damage = Math.Max(ReleasedDamage(), enemy.MaxHp * MaxHpRate / 100);
            await DealDamage(choiceContext, enemy, damage);
        }
    }
}

internal static class ClassicEnemyRules
{
    public static bool IsMinion(Creature target) =>
        target.IsSecondaryEnemy || target.HasPower<MinionPower>();

    public static bool IsEliteOrBossCombat(Creature target) =>
        target.CombatState?.Encounter?.RoomType is RoomType.Elite or RoomType.Boss;

    public static bool IsEliteOrBossTarget(Creature target) =>
        IsEliteOrBossCombat(target) && !IsMinion(target);

    public static bool IsBossCombat(Creature target) =>
        target.CombatState?.Encounter?.RoomType is RoomType.Boss;
}

public class ClowErase() : ClassicExtraClowCard(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    private const int NormalKillHpPercent = 33;
    private const int ExtraKillHpPercent = 66;

    public override ClassicElement Element => ClassicElement.Windy;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new ClassicDamageVar(9, ValueProp.Move), new DynamicVar("KillPercent", NormalKillHpPercent), new DynamicVar("ExtraKillPercent", ExtraKillHpPercent)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await KillOrDamage(choiceContext, RequiredTarget(play), NormalKillHpPercent);

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await KillOrDamage(choiceContext, RequiredTarget(play), ExtraKillHpPercent);

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(4);

    private async Task KillOrDamage(PlayerChoiceContext choiceContext, Creature target, int killHpPercent)
    {
        if (ClassicEnemyRules.IsMinion(target) || (!ClassicEnemyRules.IsEliteOrBossCombat(target) && target.CurrentHp * 100 <= target.MaxHp * killHpPercent))
        {
            await CreatureCmd.Kill(target, force: true);
            return;
        }

        await DealDamage(choiceContext, target, ReleasedDamage());
    }
}

public class SakuraErase() : ClassicSakuraConversionCard(3, CardType.Attack, TargetType.AnyEnemy)
{
    private const int EliteBossEnergyRefund = 3;

    public override ClassicElement Element => ClassicElement.Windy;

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = RequiredTarget(play);
        if (ClassicEnemyRules.IsEliteOrBossCombat(target))
        {
            await PlayerCmd.GainEnergy(EliteBossEnergyRefund, Owner);
            return;
        }

        foreach (var power in target.Powers.ToList())
            await PowerCmd.Remove(power);

        var hpLoss = Math.Max(0, target.CurrentHp - 1);
        if (hpLoss > 0)
            await CreatureCmd.Damage(choiceContext, target, hpLoss, ValueProp.Unblockable | ValueProp.Unpowered, Owner.Creature, this);
    }
}

public class ClowSilent() : ClassicExtraClowCard(1, CardType.Skill, CardRarity.Common, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Watery;

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await ApplyPower<BufferPower>(choiceContext, Owner.Creature, 1);
        await ApplyPower<ClassicSilentPendingPower>(choiceContext, Owner.Creature, 1);
    }

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await ApplyPower<BufferPower>(choiceContext, Owner.Creature, 1);

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class SakuraSilent() : ClassicSakuraConversionCard(1, CardType.Skill, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Watery;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<BufferPower>(2)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await ApplyPower<BufferPower>(choiceContext, Owner.Creature, ReleasedValue("BufferPower"));
}

public class ClowSleep() : ClassicExtraClowCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override ClassicElement Element => ClassicElement.Windy;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Magic", 2)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await ApplySleep(choiceContext, RequiredTarget(play));

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        foreach (var enemy in CombatState!.HittableEnemies.ToList())
            await ApplySleep(choiceContext, enemy);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);

    private async Task ApplySleep(PlayerChoiceContext choiceContext, Creature target)
    {
        await ApplyPower<ClassicSleepPower>(choiceContext, target, ReleasedMagic());
    }
}

public class SakuraSleep() : ClassicSakuraConversionCard(1, CardType.Skill, TargetType.AnyEnemy)
{
    public override ClassicElement Element => ClassicElement.Windy;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Magic", 4)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = RequiredTarget(play);
        await ApplyPower<ClassicSleepPower>(choiceContext, target, ReleasedMagic());
    }
}

public class ClowWood() : ClassicClowCard(1, CardType.Power, CardRarity.Uncommon, TargetType.None)
{
    private const int BaseThorns = 2;

    public override ClassicElement Element => ClassicElement.Earthy;
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<ThornsPower>(BaseThorns),
        new PowerVar<PoisonPower>(ClassicWoodPower.InitialPoison),
        new PowerVar<StrengthPower>("StrengthLoss", ClassicWoodPower.DefaultStrengthLoss)
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await ApplyPower<ThornsPower>(choiceContext, Owner.Creature, DynamicVars["ThornsPower"].IntValue);
        await ApplyPower<ClassicWoodPower>(choiceContext, Owner.Creature, DynamicVars["StrengthLoss"].IntValue);
    }

    protected override void OnUpgrade() => DynamicVars["ThornsPower"].UpgradeValueBy(2);
}

public class SakuraWood() : ClassicSakuraConversionCard(1, CardType.Power, TargetType.None)
{
    private const int BaseThorns = 4;

    public override ClassicElement Element => ClassicElement.Earthy;
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<ThornsPower>(BaseThorns),
        new PowerVar<PoisonPower>(ClassicSakuraWoodPower.PoisonPerTrigger),
        new PowerVar<StrengthPower>("StrengthLoss", ClassicSakuraWoodPower.StrengthLoss)
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await ApplyPower<ThornsPower>(choiceContext, Owner.Creature, DynamicVars["ThornsPower"].IntValue);
        await ApplyPower<ClassicSakuraWoodPower>(choiceContext, Owner.Creature, DynamicVars["StrengthLoss"].IntValue);
    }
}

public class ClowSong() : ClassicExtraClowCard(0, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
{
    private const int ExtraHits = 2;

    public override ClassicElement Element => ClassicElement.Windy;
    protected override bool HasEnergyCostX => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new ClassicDamageVar(4, ValueProp.Move), new DynamicVar("ExtraHits", ExtraHits)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var count = await ExhaustSongCards(choiceContext) ?? 0;
        await Sing(choiceContext, count);
    }

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var exhausted = await ExhaustSongCards(choiceContext);
        if (exhausted is null)
            return;

        var count = exhausted.Value + ExtraHits;
        await Sing(choiceContext, count);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(2);

    private async Task<int?> ExhaustSongCards(PlayerChoiceContext choiceContext)
    {
        var hand = CardPile.GetCards(Owner, PileType.Hand).Where(card => card != this).ToList();
        if (hand.Count == 0)
            return null;

        var maxSelect = Math.Min(hand.Count, ResolveEnergyXValue() + 1);
        if (maxSelect <= 0)
            return null;

        var selected = (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 0, maxSelect)
            {
                Cancelable = true
            },
            card => hand.Contains(card),
            this)).ToList();

        var count = selected.Sum(SongCount);
        foreach (var card in selected)
            await CardCmd.Exhaust(choiceContext, card);

        return count;
    }

    private int SongCount(CardModel card) =>
        card is ClassicSakuraCard { Identity: SourceCardIdentity.Voice } voice
        && voice.DynamicVars.TryGetValue("Magic", out var magic)
            ? 1 + magic.IntValue
            : 1;

    private async Task Sing(PlayerChoiceContext choiceContext, int count)
    {
        for (var i = 0; i < count; i++)
            await DealDamageToEnemies(choiceContext, CombatState!.HittableEnemies.ToList(), ReleasedDamage());
    }
}

public class SakuraSong() : ClassicSakuraConversionCard(1, CardType.Attack, TargetType.AllEnemies)
{
    public override ClassicElement Element => ClassicElement.Windy;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new ClassicDamageVar(6, ValueProp.Move), new ClassicBlockVar(3, ValueProp.Move)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var count = await ExhaustSongCards(choiceContext);
        for (var i = 0; i < count; i++)
        {
            await DealDamageToEnemies(choiceContext, CombatState!.HittableEnemies.ToList(), ReleasedDamage());
            await GainBlock(play, ReleasedBlock());
        }
    }

    private async Task<int> ExhaustSongCards(PlayerChoiceContext choiceContext)
    {
        var hand = CardPile.GetCards(Owner, PileType.Hand).Where(card => card != this).ToList();
        if (hand.Count == 0)
            return 0;

        var selected = (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 0, hand.Count)
            {
                Cancelable = true
            },
            card => hand.Contains(card),
            this)).ToList();

        var count = selected.Sum(SongCount);
        foreach (var card in selected)
            await CardCmd.Exhaust(choiceContext, card);

        return count;
    }

    private int SongCount(CardModel card) =>
        card is ClassicSakuraCard { Identity: SourceCardIdentity.Voice } voice
        && voice.DynamicVars.TryGetValue("Magic", out var magic)
            ? 1 + magic.IntValue
            : 1;
}

public class ClowSnow() : ClassicExtraClowCard(2, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
{
    private const int ExtraDamage = 9;

    public override ClassicElement Element => ClassicElement.Watery;
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new ClassicCombatHistoryDamageVar(4, ValueProp.Move, ClassicSnowRules.PlayedWateryClowCards),
        new ClassicDamageVar(ClassicSnowRules.PerCardDamageVar, 4, ValueProp.Move),
        new ClassicCombatHistoryCountVar(ClassicSnowRules.PlayedWateryClowCards),
        new DynamicVar("ExtraDamage", ExtraDamage)
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var count = ClassicSnowRules.PlayedWateryClowCards(this);
        for (var i = 0; i < count; i++)
        {
            var target = Owner.RunState.Rng.CombatCardSelection.NextItem(CombatState!.HittableEnemies.ToList());
            if (target is null)
                return;

            await ClassicSnowRules.ApplyFrostbite(
                choiceContext,
                this,
                await DealDamage(choiceContext, target, SnowDamage()));
        }
    }

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PlayCard(choiceContext, play);
        await ClassicSnowRules.ApplyFrostbite(
            choiceContext,
            this,
            await DealDamageToEnemies(choiceContext, CombatState!.HittableEnemies.ToList(), ExtraDamage));
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2);
        DynamicVars[ClassicSnowRules.PerCardDamageVar].UpgradeValueBy(2);
    }

    private int SnowDamage() => ReleasedValue(ClassicSnowRules.PerCardDamageVar);
}

public class SakuraSnow() : ClassicSakuraConversionCard(1, CardType.Attack, TargetType.AllEnemies)
{
    public override ClassicElement Element => ClassicElement.Watery;
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new ClassicCombatHistoryDamageVar(5, ValueProp.Move, ClassicSnowRules.PlayedWateryCards),
        new ClassicDamageVar(ClassicSnowRules.PerCardDamageVar, 5, ValueProp.Move),
        new ClassicCombatHistoryCountVar(ClassicSnowRules.PlayedWateryCards)
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var count = ClassicSnowRules.PlayedWateryCards(this);
        for (var i = 0; i < count; i++)
        {
            var attack = await DealDamageToEnemies(
                choiceContext,
                CombatState!.HittableEnemies.ToList(),
                SnowDamage());
            await ClassicSnowRules.ApplyFrostbite(choiceContext, this, attack);
        }
    }

    private int SnowDamage() => ReleasedValue(ClassicSnowRules.PerCardDamageVar);
}

public class ClowThunder() : ClassicExtraClowCard(3, CardType.Attack, CardRarity.Rare, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Earthy;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain];
    protected override bool IsPlayable => ClassicSakuraMagic.CanSpendMagic(Owner);
    protected override IEnumerable<DynamicVar> CanonicalVars => [new ClassicDamageVar(15, ValueProp.Move), new DynamicVar("Magic", 4)];

    protected override Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        Task.CompletedTask;

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await DealDamageToRandomEnemies(choiceContext, ReleasedDamage(), ReleasedMagic());
        await ClassicSakuraMagic.AddVoidToDiscardPile(choiceContext, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3);
        DynamicVars["Magic"].UpgradeValueBy(1);
    }
}

public class SakuraThunder() : ClassicSakuraConversionCard(0, CardType.Attack, TargetType.None)
{
    private const int ResourceDivisor = 2;

    public override ClassicElement Element => ClassicElement.Earthy;
    protected override bool HasEnergyCostX => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new ClassicDamageVar(15, ValueProp.Move), new DynamicVar("Magic", ResourceDivisor)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var charge = await ClassicSakuraMagic.SpendAllMagic(choiceContext, Owner);
        var count = ((int)play.Resources.EnergySpent * ResourceDivisor + charge) / ResourceDivisor;
        await DealDamageToRandomEnemies(choiceContext, ReleasedDamage(), count);
    }
}

public class ClowTime() : ClassicExtraClowCard(2, CardType.Skill, CardRarity.Rare, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Watery;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust, CardKeyword.Retain];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PowerCmd.Apply<RetainHandPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this, false);

        if (Owner.Creature.Block > 0)
            await PowerCmd.Apply<BlockNextTurnPower>(choiceContext, Owner.Creature, Owner.Creature.Block, Owner.Creature, this, false);

        var energy = Owner.PlayerCombatState?.Energy ?? 0;
        if (energy > 0)
            await PowerCmd.Apply<EnergyNextTurnPower>(choiceContext, Owner.Creature, energy, Owner.Creature, this, false);

        ClassicElementStatePower.PreserveAllForNextTurn(Owner.Creature);
        await ClassicSakuraMagic.AddVoidToDrawPile(choiceContext, Owner);
        PlayerCmd.EndTurn(Owner, canBackOut: false);
    }

    protected override Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        PlayCard(choiceContext, play);

    protected override PileType GetResultPileTypeForCardPlay()
    {
        var usesExtra = IsMutable && SakuraExtraEffectTransaction.CanActivate(Owner);
        return usesExtra ? PileType.Discard : base.GetResultPileTypeForCardPlay();
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await base.AfterCardPlayed(choiceContext, play);

        if (play.Card == this)
            ClassicElementStatePower.PreserveAllForNextTurn(Owner.Creature);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class SakuraTime() : ClassicSakuraConversionCard(1, CardType.Skill, TargetType.AllEnemies)
{
    public override ClassicElement Element => ClassicElement.Watery;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust, CardKeyword.Innate];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<ClassicTimePower>(1)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        foreach (var enemy in CombatState!.HittableEnemies.ToList())
            await ClassicPowerRules.ApplyBypassingArtifact<ClassicTimePower>(choiceContext, enemy, ReleasedValue("ClassicTimePower"), Owner.Creature, this);
        await ClassicSakuraMagic.AddVoidToDiscardPile(choiceContext, Owner);
    }
}

public class ClowTwin() : ClassicClowCard(3, CardType.Power, CardRarity.Rare, TargetType.None)
{
    public override ClassicElement Element => ClassicElement.Firey | ClassicElement.Watery;

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await ApplyPower<ClassicTwinPower>(choiceContext, Owner.Creature, 1);

    protected override void OnUpgrade() => AddKeywordIfMissing(CardKeyword.Retain);
}

public class SakuraTwin() : ClassicSakuraConversionCard(1, CardType.Power, TargetType.None)
{
    private const int TwinAmount = 1;

    public override ClassicElement Element => ClassicElement.Firey | ClassicElement.Watery;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Amount", TwinAmount)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await ApplyPower<ClassicTwinSakuraPower>(choiceContext, Owner.Creature, DynamicVars["Amount"].IntValue);
}

public class SpellSeal() : ClassicSpellCard(2, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new ClassicDamageVar(14, ValueProp.Move), new DynamicVar("Magic", 2)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await DamageCmd.Attack(DynamicVars.Damage.IntValue)
            .FromCard(this)
            .Targeting(RequiredTarget(play))
            .Execute(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(4);
}

public class SpellRelease() : ClassicSpellCard(1, CardType.Skill, CardRarity.Basic, TargetType.None)
{
    private static LocString Prompt => CardLoc<SpellRelease>("selectionPrompt");
    private const float ReleaseRate = 0.5f;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<VulnerablePower>(1)];
    public override int MaxUpgradeLevel => 0;

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var choices = CardPile.GetCards(Owner, PileType.Hand)
            .Where(CanRelease)
            .ToList();
        if (choices.Count > 0)
        {
            var selected = (await CardSelectCmd.FromHand(
                choiceContext,
                Owner,
                new CardSelectorPrefs(Prompt, 1)
                {
                    Cancelable = false,
                    RequireManualConfirmation = false
                },
                card => choices.Contains(card),
                this)).FirstOrDefault();

            if (selected is not null)
                ApplyRelease(selected);
        }

        await PowerCmd.Apply<VulnerablePower>(
            choiceContext,
            CombatState!.HittableEnemies.ToList(),
            DynamicVars.Vulnerable.IntValue,
            Owner.Creature,
            this,
            false);
    }

    private void ApplyRelease(CardModel selected)
    {
        var swordJade = Owner.GetRelic<ClassicSwordJadeRelic>();
        if (swordJade is not null)
        {
            swordJade.Flash();
            ClassicReleaseState.Apply(selected, ClassicSwordJadeRelic.ReleaseRate);
            return;
        }

        ClassicReleaseState.Apply(selected, ReleaseRate);
    }

    internal static bool CanRelease(CardModel card) =>
        SakuraCardCatalog.TryGetMetadata(card, out var metadata)
        && metadata.Era is SourceEraClass.Clow or SourceEraClass.Sakura or SourceEraClass.Clear;
}

public class SpellTurn() : ClassicSpellCard(-2, CardType.Skill, CardRarity.Token, TargetType.None)
{
    private static LocString Prompt => CardLoc<SpellTurn>("selectionPrompt");

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain, CardKeyword.Exhaust];
    public override int MaxUpgradeLevel => 0;
    protected override bool IsPlayable => CardPile.GetCards(Owner, PileType.Hand).Any(ClassicSakuraCardCatalog.IsEligibleClowForTurn);

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var choices = CardPile.GetCards(Owner, PileType.Hand)
            .Where(ClassicSakuraCardCatalog.IsEligibleClowForTurn)
            .ToList();
        if (choices.Count == 0)
            return;

        var selected = (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(Prompt, 1)
            {
                Cancelable = false,
                RequireManualConfirmation = false
            },
            card => choices.Contains(card),
            this)).FirstOrDefault();

        if (selected is not ClassicClowCard { Identity: { } identity } selectedClow)
            return;

        var canonicalSakura = ClassicSakuraCardCatalog.SakuraTemplateFor(identity);
        if (canonicalSakura is null || ClassicSakuraCardCatalog.HasSakuraIdentity(Owner, identity))
            return;

        var deckCard = selectedClow.DeckVersion;
        if (deckCard is null || deckCard.Pile?.Type != PileType.Deck)
            return;

        var vfx = ClassicTurnTransformationVfx.TryCreate(selectedClow);
        try
        {
            if (vfx is not null && !await vfx.PlayPrelude())
                return;

            var handCard = await TransformSelected(selectedClow, deckCard, canonicalSakura);
            if (handCard is null)
                return;

            if (vfx is not null)
                await vfx.PlayReveal(handCard);
        }
        finally
        {
            vfx?.Dispose();
        }

        if (DeckVersion?.Pile?.Type == PileType.Deck)
            await CardPileCmd.RemoveFromDeck(DeckVersion, showPreview: false);
    }

    private async Task<CardModel?> TransformSelected(
        ClassicClowCard selected,
        CardModel deckCard,
        CardModel canonicalSakura)
    {
        var deckReplacement = Owner.RunState.CreateCard(canonicalSakura, Owner);
        var results = (await CardCmd.Transform(
            [new CardTransformation(deckCard, deckReplacement)],
            null,
            CardPreviewStyle.None)).ToList();
        if (results.Count == 0)
            return null;

        var combatState = Owner.Creature.CombatState
            ?? throw new InvalidOperationException("Spell Turn requires an active combat.");
        var handCard = combatState.CreateCard(canonicalSakura, Owner);
        await CardPileCmd.AddGeneratedCardToCombat(handCard, PileType.Hand, Owner, CardPilePosition.Random);
        ClassicReleaseState.Reset(selected);
        await CardPileCmd.RemoveFromCombat(selected, skipVisuals: false);
        return handCard;
    }
}

public class SpellEmptySpell() : ClassicSpellCard(0, CardType.Skill, CardRarity.Token, TargetType.None)
{
    private static LocString Prompt => CardLoc<SpellEmptySpell>("selectionPrompt");
    private static readonly Type[] ElementSpellTypes =
    [
        typeof(SpellHuoShen),
        typeof(SpellLeiDi),
        typeof(SpellFengHua),
        typeof(SpellShuiLong)
    ];

    public override int MaxUpgradeLevel => 0;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Ethereal, CardKeyword.Exhaust, SakuraKeywords.Purge];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var combatState = Owner.Creature.CombatState
            ?? throw new InvalidOperationException("Empty Spell requires an active combat.");
        var choices = ElementSpellTypes
            .Select(type => combatState.CreateCard(ModelDb.GetById<CardModel>(ModelDb.GetId(type)), Owner))
            .ToList();

        try
        {
            var selected = choices.Count == 1
                ? choices[0]
                : (await CardSelectCmd.FromSimpleGrid(
                    choiceContext,
                    choices,
                    Owner,
                    new CardSelectorPrefs(Prompt, 1)
                    {
                        Cancelable = false,
                        RequireManualConfirmation = false
                    })).FirstOrDefault();

            if (selected is null)
                return;

            var card = combatState.CreateCard(ModelDb.GetById<CardModel>(ModelDb.GetId(selected.GetType())), Owner);
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner, CardPilePosition.Random);
        }
        finally
        {
            foreach (var choice in choices)
            {
                if (choice.Pile is null)
                    choice.CardScope?.RemoveCard(choice);
            }
        }
    }
}

public abstract class ClassicElementSpellCard(int cost, CardType type, TargetType target, ClassicElement element) :
    ClassicSpellCard(cost, type, CardRarity.Token, target)
{
    public override int MaxUpgradeLevel => 0;
    public override ClassicElement Element => element;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Ethereal, CardKeyword.Exhaust];
}

public class SpellHuoShen() : ClassicElementSpellCard(0, CardType.Attack, TargetType.AnyEnemy, ClassicElement.Firey)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new ClassicDamageVar(5, ValueProp.Move)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await DealDamage(choiceContext, RequiredTarget(play), ReleasedDamage());
}

public class SpellLeiDi() : ClassicElementSpellCard(0, CardType.Attack, TargetType.None, ClassicElement.Earthy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new ClassicDamageVar(6, ValueProp.Move)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = Owner.RunState.Rng.CombatCardSelection.NextItem(CombatState!.HittableEnemies.ToList());
        if (target is not null)
            await DealDamage(choiceContext, target, ReleasedDamage());
    }
}

public class SpellFengHua() : ClassicElementSpellCard(0, CardType.Attack, TargetType.AnyEnemy, ClassicElement.Windy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new ClassicDamageVar(1, ValueProp.Move), new DynamicVar("Magic", 3)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = RequiredTarget(play);
        await DealDamage(choiceContext, target, ReleasedDamage(), hitCount: ReleasedMagic());
    }
}

public class SpellShuiLong() : ClassicElementSpellCard(0, CardType.Attack, TargetType.None, ClassicElement.Watery)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new ClassicDamageVar(4, ValueProp.Move)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await DealDamageToEnemies(choiceContext, CombatState!.HittableEnemies, ReleasedDamage());
}
