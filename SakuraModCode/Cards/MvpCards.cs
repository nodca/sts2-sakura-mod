using BaseLib.Cards.Variables;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.Relics;

namespace SakuraMod.SakuraModCode.Cards;

public class Action() : SakuraModCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Wind, SakuraKeywords.Manifest];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1), new CardsVar("ReleaseDraw", 1)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var card = SakuraActions.Hand(this).Any(card => card != this)
            ? await SakuraActions.SelectHandCard(this, choiceContext, card => card != this, cancelable: false)
            : null;

        if (card is not null)
            await CardCmd.Exhaust(choiceContext, card);

        var manifested = await SakuraManifestLoop.Manifest(this, choiceContext, DynamicVars.Cards.IntValue);
        foreach (var copy in manifested)
            copy.SetToFreeThisTurn();

        if (ShouldRelease)
            await CardPileCmd.Draw(choiceContext, DynamicVars["ReleaseDraw"].IntValue, Owner, false);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
        DynamicVars["ReleaseDraw"].UpgradeValueBy(1);
    }
}

public class Appear() : SakuraModCard(1, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Manifest];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(2), new DynamicVar("ReleaseDiscount", 1)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var manifested = await SakuraManifestLoop.Manifest(this, choiceContext, DynamicVars.Cards.IntValue, excludedType: typeof(Appear));
        if (!ShouldRelease)
            return;

        foreach (var card in manifested)
            await SakuraActions.ReduceCostThisTurn(choiceContext, this, card, DynamicVars["ReleaseDiscount"].IntValue);
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1);
}

public class Aqua() : SakuraModCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Water];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<SakuraFrostbitePower>()];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("AquaDamageBase", 4),
        new CustomExtraDamageVar("AquaDamage", 1),
        new CustomCalculatedDamageVar("AquaDamage", ValueProp.Move).WithMultiplier(AquaRules.FrostbiteBonusCount),
        new PowerVar<WeakPower>(1),
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var targets = CombatState!.HittableEnemies.ToList();
        var aquaDamage = (CalculatedDamageVar)DynamicVars["AquaDamage"];
        SakuraCardPlayVfx.PlayAqua(targets);
        foreach (var enemy in targets)
        {
            var frostbite = enemy.GetPower<SakuraFrostbitePower>();
            var frostbiteAmount = AquaRules.FrostbiteAmount(enemy);

            await SakuraActions.Attack(choiceContext, this, enemy, aquaDamage);
            if (ShouldRelease && enemy.IsAlive && frostbiteAmount > 0 && frostbite is not null)
                await PowerCmd.ModifyAmount(choiceContext, frostbite, frostbiteAmount, Owner.Creature, this, false);
        }

        await PowerCmd.Apply<WeakPower>(choiceContext, targets, DynamicVars.Weak.IntValue, Owner.Creature, this, false);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["AquaDamageBase"].UpgradeValueBy(4);
        DynamicVars.Weak.UpgradeValueBy(1);
        DynamicVars["AquaDamageExtra"].UpgradeValueBy(1);
    }
}

internal static class AquaRules
{
    public static decimal FrostbiteBonusCount(CardModel card, Creature? target)
    {
        if (!card.IsReleased())
            return 0;

        if (target is not null)
            return FrostbiteBonusCount(target);

        return card.CombatState?.HittableEnemies.Select(FrostbiteBonusCount).DefaultIfEmpty(0).Max() ?? 0;
    }

    public static int FrostbiteAmount(Creature target) =>
        Math.Max(0, target.GetPower<SakuraFrostbitePower>()?.Amount ?? 0);

    private static int FrostbiteBonusCount(Creature target) =>
        FrostbiteAmount(target) / 10;
}

public class Blade() : SakuraModCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(4, ValueProp.Move),
        new BladeHitsVar(2)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = RequiredTarget(play);
        var hits = BladeRules.HitCount(this);
        await SakuraActions.Attack(choiceContext, this, target, DynamicVars.Damage.IntValue, hitCount: hits);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(1);
}

internal sealed class BladeHitsVar(decimal hits) : DynamicVar("Hits", hits)
{
    public override void UpdateCardPreview(CardModel card, CardPreviewMode previewMode, Creature? target, bool runGlobalHooks) =>
        PreviewValue = BladeRules.HitCount(card, (int)BaseValue);
}

internal static class BladeRules
{
    public static int HitCount(CardModel card) =>
        card.DynamicVars.TryGetValue("Hits", out var hits)
            ? HitCount(card, hits.IntValue)
            : 0;

    internal static int HitCount(CardModel card, int baseHits)
    {
        var releaseCount = card.Owner is { } owner
            ? SakuraActions.ReleaseGainCountThisTurn(owner)
            : 0;
        var hits = baseHits;
        if (releaseCount > 0)
            hits++;
        if (card.IsReleased())
            hits += releaseCount;
        return Math.Max(0, hits);
    }
}

public class Hail() : SakuraModCard(1, CardType.Attack, CardRarity.Common, TargetType.RandomEnemy)
{
    private const int BaseHits = 3;
    private const int ReleaseHits = 1;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Water];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<SakuraFrostbitePower>()];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(3, ValueProp.Move),
        new PowerVar<SakuraFrostbitePower>(10)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await using var attack = await AttackCommand.CreateContextAsync(CombatState!, choiceContext, this);
        var hits = BaseHits + (ShouldRelease ? ReleaseHits : 0);
        for (var i = 0; i < hits; i++)
            await Hit(choiceContext, attack);
    }

    private async Task Hit(PlayerChoiceContext choiceContext, AttackContext attack)
    {
        var target = Owner.RunState.Rng.CombatTargets.NextItem(CombatState!.HittableEnemies);
        if (target is null)
            return;

        SakuraCardPlayVfx.PlayHail(target);
        attack.AddHit(await CreatureCmd.Damage(
            choiceContext,
            target,
            DynamicVars.Damage.IntValue,
            SakuraActions.AttackProps(this, DynamicVars.Damage.Props),
            Owner.Creature,
            this));
        if (target.IsAlive)
            await PowerCmd.Apply<SakuraFrostbitePower>(choiceContext, target, DynamicVars["SakuraFrostbitePower"].IntValue, Owner.Creature, this, false);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(1);
}

public class Lucid() : SakuraModCard(0, CardType.Skill, CardRarity.Common, TargetType.Self), IReleaseable
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar("Look", 3)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var top = CardPile.Get(PileType.Draw, Owner)!.Cards.Take(DynamicVars["Look"].IntValue).ToList();
        var card = await SakuraActions.SelectFromCardPreviews(this, choiceContext, top, cancelable: false);
        if (card is not null)
            await SakuraActions.MoveExistingCardToHand(this, card);

        await TriggerReleaseEffect(choiceContext, play);
    }

    public async Task OnReleased(PlayerChoiceContext choiceContext, CardPlay play) =>
        await PowerCmd.Apply<LucidPiercePower>(choiceContext, Owner.Creature, 1, Owner.Creature, this, false);

    protected override void OnUpgrade() => DynamicVars["Look"].UpgradeValueBy(1);
}

public class Shade() : SakuraModCard(2, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy), IReleaseable
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(12, ValueProp.Move), new PowerVar<WeakPower>(1)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block.IntValue, ValueProp.Move, play, false);
        var target = RequiredTarget(play);
        await PowerCmd.Apply<WeakPower>(choiceContext, target, DynamicVars.Weak.IntValue, Owner.Creature, this, false);
        await TriggerReleaseEffect(choiceContext, play);
    }

    public async Task OnReleased(PlayerChoiceContext choiceContext, CardPlay play) =>
        await PowerCmd.Apply<BlurPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this, false);

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(4);
}

public class Siege() : SakuraModCard(1, CardType.Skill, CardRarity.Basic, TargetType.Self), IReleaseable
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Earth];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(5, ValueProp.Move),
        new BlockVar("ReleaseBlock", 3, ValueProp.Move),
        new PowerVar<WeakPower>(1)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block.IntValue, ValueProp.Move, play, false);
        await TriggerReleaseEffect(choiceContext, play);
    }

    public async Task OnReleased(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars["ReleaseBlock"].IntValue, ValueProp.Move, play, false);
        await PowerCmd.Apply<WeakPower>(choiceContext, CombatState!.HittableEnemies.ToList(), DynamicVars.Weak.IntValue, Owner.Creature, this, false);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2);
        DynamicVars["ReleaseBlock"].UpgradeValueBy(1);
    }
}

public class Swing() : SakuraModCard(2, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Earth];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(9, ValueProp.Move),
        new PowerVar<WeakPower>(1),
        new DamageVar("WeakDamage", 3, ValueProp.Move)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var targets = CombatState!.HittableEnemies.ToList();
        await PowerCmd.Apply<WeakPower>(choiceContext, targets, DynamicVars.Weak.IntValue, Owner.Creature, this, false);

        foreach (var enemy in targets.Where(enemy => enemy.IsAlive))
        {
            var weak = Math.Max(0, enemy.GetPower<WeakPower>()?.Amount ?? 0);
            var bonus = weak * DynamicVars["WeakDamage"].IntValue;
            if (ShouldRelease)
                bonus *= 2;

            await SakuraActions.Attack(choiceContext, this, enemy, DynamicVars.Damage.IntValue + bonus);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3);
        DynamicVars["WeakDamage"].UpgradeValueBy(1);
    }
}

public class DreamCompass() : SakuraModCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var choices = CardPile.Get(PileType.Draw, Owner)!.Cards
            .Where(SakuraCardCatalog.IsTransparentCard)
            .ToList();
        var card = await SakuraActions.SelectFromCardPreviews(this, choiceContext, choices, cancelable: false);

        if (card is null)
            return;

        if (IsUpgraded)
            await SakuraActions.ReleaseThisTurnAndRecord(choiceContext, card);
        await SakuraActions.MoveExistingCardToHand(this, card);
    }
}

public class Stabilize() : SakuraModCard(0, CardType.Skill, CardRarity.Basic, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Stabilize, CardKeyword.Innate];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await CardPileCmd.Draw(choiceContext, 1, Owner, false);

        if (Owner.GetRelic<KaitoPocketWatch>() is null)
        {
            var card = await SakuraActions.SelectStabilizeCandidate(this, choiceContext);
            if (card is not null)
            {
                await card.Stabilize(choiceContext);
                if (IsUpgraded)
                    card.EnergyCost.SetThisTurnOrUntilPlayed(0, true);
            }
        }
    }
}

public class KeroAdvice() : SakuraModCard(0, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1), new BlockVar(4, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var drawn = await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner, false);
        var freed = drawn.FirstOrDefault(card => card.Type == CardType.Skill);
        if (freed is not null)
            freed.SetToFreeThisTurn();
        else
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block.IntValue, ValueProp.Move, play, false);
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1);
}

public class TomoyoCostume() : SakuraModCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(5, ValueProp.Move), new DynamicVar("Delay", 1)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block.IntValue, ValueProp.Move, play, false);
        if (!SakuraActions.Hand(this).Any(IsTarget))
            return;

        var card = await SakuraActions.SelectHandCard(
            this,
            choiceContext,
            IsTarget,
            cancelable: false);
        if (card is null)
            return;

        if (!card.Keywords.Contains(CardKeyword.Retain))
            card.AddKeyword(CardKeyword.Retain);
        card.DelayTemporaryRemoval(DynamicVars["Delay"].IntValue);
    }

    private bool IsTarget(CardModel card) =>
        card != this && SakuraCardCatalog.IsTransparentCard(card) && card.IsTemporary();

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(2);
}

public class Break() : SakuraModCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy), IReleaseable
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Fire];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(9, ValueProp.Move), new PowerVar<VulnerablePower>(1)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = RequiredTarget(play);
        var hadBlock = target.Block > 0;
        if (IsUpgraded && hadBlock)
            await CreatureCmd.LoseBlock(target, target.Block);

        var damage = DynamicVars.Damage.IntValue * (hadBlock ? 2 : 1);
        await SakuraActions.Attack(choiceContext, this, target, damage);
        await TriggerReleaseEffect(choiceContext, play);
    }

    public async Task OnReleased(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = play.Target ?? CombatState?.HittableEnemies.FirstOrDefault();
        if (target is null)
            return;

        if (target.HasPower<ArtifactPower>())
            await PowerCmd.Apply<ArtifactPower>(choiceContext, target, -1, Owner.Creature, this, false);
        await PowerCmd.Apply<VulnerablePower>(choiceContext, target, DynamicVars.Vulnerable.IntValue, Owner.Creature, this, false);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3);
        DynamicVars.Vulnerable.UpgradeValueBy(1);
    }
}

public class Choice() : SakuraModCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Manifest];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar("ManifestCards", 1), new CardsVar("DrawCards", 2)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var manifestChoice = SakuraActions.CloneWithCurrentUpgrade<ChoiceManifestChoice>(this);
        var drawChoice = SakuraActions.CloneWithCurrentUpgrade<ChoiceDrawChoice>(this);
        var choice = await SakuraActions.SelectFromCards(this, choiceContext, [manifestChoice, drawChoice], cancelable: false);
        if (choice is ChoiceDrawChoice)
            await Draw(choiceContext, ChoiceRepeatCount);
        else
            await Manifest(choiceContext, ChoiceRepeatCount);
    }

    private int ChoiceRepeatCount => ShouldRelease ? 2 : 1;

    private async Task Manifest(PlayerChoiceContext choiceContext, int repeats)
    {
        for (var i = 0; i < repeats; i++)
            await SakuraManifestLoop.Manifest(this, choiceContext, DynamicVars["ManifestCards"].IntValue);
    }

    private async Task Draw(PlayerChoiceContext choiceContext, int repeats) =>
        await CardPileCmd.Draw(choiceContext, DynamicVars["DrawCards"].IntValue * repeats, Owner, false);

    protected override void OnUpgrade() => DynamicVars["ManifestCards"].UpgradeValueBy(1);
}

public class Promise() : SakuraModCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self), IReleaseable
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Manifest];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(6, ValueProp.Move), new CardsVar(1), new CardsVar("ReleaseCards", 1)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block.IntValue, ValueProp.Move, play, false);
        var amount = DynamicVars.Cards.IntValue + (ShouldRelease ? DynamicVars["ReleaseCards"].IntValue : 0);
        await PowerCmd.Apply<PromiseManifestPower>(choiceContext, Owner.Creature, amount, Owner.Creature, this, false);
    }

    public async Task OnReleased(PlayerChoiceContext choiceContext, CardPlay play) =>
        await PowerCmd.Apply<PromiseManifestPower>(choiceContext, Owner.Creature, DynamicVars["ReleaseCards"].IntValue, Owner.Creature, this, false);

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3);
}

public class Struggle() : SakuraModCard(0, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Fire];
    protected override bool HasEnergyCostX => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(4, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = RequiredTarget(play);
        var discarded = SakuraActions.Hand(this).Where(card => card != this).ToList();
        if (discarded.Count > 0)
            await CardCmd.Discard(choiceContext, discarded);

        var hits = ResolveEnergyXValue() + (ShouldRelease ? discarded.Count : 0);
        await SakuraActions.Attack(choiceContext, this, target, DynamicVars.Damage.IntValue, hitCount: hits);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(1);
}

public class DreamCostume() : SakuraModCard(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play) =>
        await PowerCmd.Apply<DreamCostumePower>(choiceContext, Owner.Creature, 1, Owner.Creature, this, false);

    protected override void OnUpgrade() => AddKeywordIfMissing(CardKeyword.Innate);
}

public class Blaze() : SakuraModCard(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Fire, SakuraKeywords.Burn];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CalculationBaseVar(14),
        new ExtraDamageVar(3),
        new CalculatedDamageVar(ValueProp.Move).WithMultiplier(BlazeRules.TemporaryRemovedMultiplier),
        new PowerVar<SakuraBurnPower>(2)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = RequiredTarget(play);
        SakuraCardPlayVfx.PlayBlaze(target);
        await SakuraActions.Attack(choiceContext, this, target, DynamicVars.CalculatedDamage);
        if (target.IsAlive)
            await PowerCmd.Apply<SakuraBurnPower>(choiceContext, target, DynamicVars["SakuraBurnPower"].IntValue, Owner.Creature, this, false);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.CalculationBase.UpgradeValueBy(4);
        DynamicVars.ExtraDamage.UpgradeValueBy(1);
    }
}

internal static class BlazeRules
{
    public static decimal TemporaryRemovedMultiplier(CardModel card, Creature? target) =>
        card.Owner is { } owner
            ? TemporaryCardMemory.CardsRemovedByTemporary(card.CombatState, owner).Count * (card.IsReleased() ? 2 : 1)
            : 0;
}

public class Dreaming() : SakuraModCard(2, CardType.Power, CardRarity.Rare, TargetType.Self), IReleaseable
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Manifest];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PowerCmd.Apply<DreamingPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this, false);
        await TriggerReleaseEffect(choiceContext, play);
    }

    public async Task OnReleased(PlayerChoiceContext choiceContext, CardPlay play) =>
        await SakuraManifestLoop.Manifest(this, choiceContext, 1);

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
