using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Classic.Cards;
using SakuraMod.SakuraModCode.Extensions;
using SakuraMod.SakuraModCode.Powers;
using STS2RitsuLib.Cards.DynamicVars;

namespace SakuraMod.SakuraModCode.Cards;

public class Action() : SakuraModCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override bool HasExtraEffect => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Wind, SakuraKeywords.Manifest];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1), new CardsVar("ExtraDraw", 1)];

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

        if (IsUsingExtraEffect)
            await CardPileCmd.Draw(choiceContext, DynamicVars["ExtraDraw"].IntValue, Owner, false);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
        DynamicVars["ExtraDraw"].UpgradeValueBy(1);
    }
}

public class Appear() : SakuraModCard(1, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    protected override bool HasExtraEffect => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Wind];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play) =>
        await SakuraManifestLoop.AddTemporaryTransparentCopyToHand(
            this,
            choiceContext,
            freeThisTurn: IsUsingExtraEffect);

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class Aqua() : SakuraModCard(0, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
{
    protected override bool HasExtraEffect => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Water, CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(3, ValueProp.Move),
        new PowerVar<WeakPower>(1)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var targets = CombatState!.HittableEnemies.ToList();
        SakuraCardPlayVfx.PlayAqua(targets);
        foreach (var enemy in targets)
            await SakuraActions.Attack(choiceContext, this, enemy, DynamicVars.Damage.IntValue);

        await PowerCmd.Apply<WeakPower>(choiceContext, targets, DynamicVars.Weak.IntValue, Owner.Creature, this, false);
    }

    protected override PileType GetResultPileTypeForCardPlay() =>
        SakuraModCard.UsesMagicChargeExtraEffect(this)
            ? PileType.Discard
            : base.GetResultPileTypeForCardPlay();

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2);
        DynamicVars.Weak.UpgradeValueBy(1);
    }
}

public class Blade() : SakuraModCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override bool HasExtraEffect => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Fire];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BladeDamageVar(4, ValueProp.Move),
        new BladeHitsVar(2)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = RequiredTarget(play);
        var hits = BladeRules.HitCount(this);
        await SakuraActions.Attack(choiceContext, this, target, BladeRules.Damage(this), hitCount: hits);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(1);
}

internal sealed class BladeDamageVar(decimal damage, ValueProp props) : DamageVar(damage, props)
{
    public override void UpdateCardPreview(CardModel card, CardPreviewMode previewMode, Creature? target, bool runGlobalHooks)
    {
        var baseValue = BladeRules.Damage(card, (int)BaseValue);
        decimal preview = baseValue;
        if (card.Enchantment is not null)
        {
            preview += card.Enchantment.EnchantDamageAdditive(preview, Props);
            preview *= card.Enchantment.EnchantDamageMultiplicative(preview, Props);
            if (!card.IsEnchantmentPreview)
                EnchantedValue = preview;
        }

        if (runGlobalHooks)
            preview = Hook.ModifyDamage(card.Owner.RunState, card.CombatState, target, card.Owner.Creature, baseValue, Props, card, ModifyDamageHookType.All, previewMode, out _);

        PreviewValue = preview;
    }
}

internal sealed class BladeHitsVar(decimal hits) : DynamicVar("Hits", hits)
{
    public override void UpdateCardPreview(CardModel card, CardPreviewMode previewMode, Creature? target, bool runGlobalHooks) =>
        PreviewValue = BladeRules.HitCount(card, (int)BaseValue);
}

internal static class BladeRules
{
    private const int CardsPerDamageBonus = 2;
    private const int DamagePerBonus = 2;

    public static int Damage(CardModel card) =>
        card.DynamicVars.TryGetValue("Damage", out var damage)
            ? Damage(card, damage.IntValue)
            : 0;

    internal static int Damage(CardModel card, int baseDamage) =>
        Damage(baseDamage, PlayedSwordOrBladeCount(card));

    internal static int Damage(int baseDamage, int playedSwordOrBladeCount) =>
        baseDamage + Math.Max(0, playedSwordOrBladeCount / CardsPerDamageBonus) * DamagePerBonus;

    public static int HitCount(CardModel card) =>
        card.DynamicVars.TryGetValue("Hits", out var hits)
            ? HitCount(card, hits.IntValue)
            : 0;

    internal static int HitCount(CardModel card, int baseHits)
    {
        var hits = baseHits;
        if (SakuraModCard.UsesMagicChargeExtraEffect(card))
            hits += 2;
        return Math.Max(0, hits);
    }

    internal static bool IsSwordOrBlade(CardModel card) =>
        SakuraSourceCardCatalog.TryGetMetadata(card, out var metadata)
        && metadata.Identity is SourceCardIdentity.Sword or SourceCardIdentity.Blade;

    private static int PlayedSwordOrBladeCount(CardModel card)
    {
        if (card.Owner is not { } owner || card.CombatState is null)
            return 0;

        return CombatManager.Instance.History.CardPlaysFinished
            .Where(entry => entry is CardPlayFinishedEntry { CardPlay.Card.Owner: var cardOwner } && cardOwner == owner)
            .Select(entry => ((CardPlayFinishedEntry)entry).CardPlay.Card)
            .Where(playedCard => playedCard != card)
            .Count(IsSwordOrBlade);
    }
}

public class Hail() : SakuraModCard(1, CardType.Attack, CardRarity.Common, TargetType.AllEnemies)
{
    protected override bool HasExtraEffect => true;
    private const int BaseHits = 2;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Water];
    internal override IEnumerable<CardKeyword> ReferencedKeywords => [SakuraKeywords.Frostbite];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(3, ValueProp.Move),
        new PowerVar<SakuraFrostbitePower>(1)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await using var attack = await AttackCommand.CreateContextAsync(CombatState!, choiceContext, this);
        var frostbite = DynamicVars["SakuraFrostbitePower"].IntValue + (IsUsingExtraEffect ? 1 : 0);
        foreach (var target in CombatState!.HittableEnemies.ToList())
        {
            for (var i = 0; i < BaseHits && target.IsAlive; i++)
                await Hit(choiceContext, attack, target);

            if (target.IsAlive)
                await PowerCmd.Apply<SakuraFrostbitePower>(choiceContext, target, frostbite, Owner.Creature, this, false);
        }
    }

    private async Task Hit(PlayerChoiceContext choiceContext, AttackContext attack, Creature target)
    {
        SakuraCardPlayVfx.PlayHail(target);
        attack.AddHit(await CreatureCmd.Damage(
            choiceContext,
            target,
            DynamicVars.Damage.IntValue,
            SakuraActions.AttackProps(this, DynamicVars.Damage.Props),
            Owner.Creature,
            this));
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(1);
}

public class Lucid() : SakuraModCard(0, CardType.Skill, CardRarity.Common, TargetType.Self), IExtraEffectCard
{
    protected override bool HasExtraEffect => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Water];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar("Look", 3)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var top = CardPile.Get(PileType.Draw, Owner)!.Cards.Take(DynamicVars["Look"].IntValue).ToList();
        var card = await SakuraActions.SelectFromCardPreviews(this, choiceContext, top, cancelable: false);
        if (card is not null)
            await SakuraActions.MoveExistingCardToHand(this, card);

        await TriggerExtraEffect(choiceContext, play);
    }

    public async Task OnExtraEffect(PlayerChoiceContext choiceContext, CardPlay play) =>
        await PowerCmd.Apply<LucidPiercePower>(choiceContext, Owner.Creature, 1, Owner.Creature, this, false);

    protected override void OnUpgrade() => DynamicVars["Look"].UpgradeValueBy(1);
}

public class Shade() : SakuraModCard(2, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy), IExtraEffectCard
{
    protected override bool HasExtraEffect => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Water];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(12, ValueProp.Move), new PowerVar<WeakPower>(1)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block.IntValue, ValueProp.Move, play, false);
        var target = RequiredTarget(play);
        await PowerCmd.Apply<WeakPower>(choiceContext, target, DynamicVars.Weak.IntValue, Owner.Creature, this, false);
        await TriggerExtraEffect(choiceContext, play);
    }

    public async Task OnExtraEffect(PlayerChoiceContext choiceContext, CardPlay play) =>
        await PowerCmd.Apply<BlurPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this, false);

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(4);
}

public class Siege() : SakuraModCard(0, CardType.Skill, CardRarity.Common, TargetType.Self), IExtraEffectCard
{
    protected override bool HasExtraEffect => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Earth];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new SiegeBlockVar(SiegeRules.BaseBlock, ValueProp.Move),
        new BlockVar("ExtraBlock", 2, ValueProp.Move),
        new PowerVar<WeakPower>(2)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var enemyCount = CombatState!.HittableEnemies.Count();
        var block = SiegeRules.BlockAmount(
            DynamicVars.Block.IntValue,
            DynamicVars["ExtraBlock"].IntValue,
            enemyCount);
        await CreatureCmd.GainBlock(Owner.Creature, block, ValueProp.Move, play, false);
        await TriggerExtraEffect(choiceContext, play);
    }

    public async Task OnExtraEffect(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PowerCmd.Apply<WeakPower>(choiceContext, CombatState!.HittableEnemies.ToList(), DynamicVars.Weak.IntValue, Owner.Creature, this, false);
    }

    protected override void OnUpgrade() => DynamicVars["ExtraBlock"].UpgradeValueBy(1);
}

internal static class SiegeRules
{
    internal const int BaseBlock = 1;

    internal static int BlockAmount(int baseBlock, int blockPerEnemy, int enemyCount) =>
        baseBlock + blockPerEnemy * Math.Max(0, enemyCount);
}

internal sealed class SiegeBlockVar(decimal block, ValueProp props) : BlockVar(block, props)
{
    public override void UpdateCardPreview(CardModel card, CardPreviewMode previewMode, Creature? target, bool runGlobalHooks)
    {
        var blockPerEnemy = card.DynamicVars.TryGetValue("ExtraBlock", out var extraBlock)
            ? extraBlock.IntValue
            : 0;
        var enemyCount = card.CombatState?.HittableEnemies.Count() ?? 0;
        decimal baseValue = SiegeRules.BlockAmount((int)BaseValue, blockPerEnemy, enemyCount);
        var preview = baseValue;
        if (card.Enchantment is not null)
        {
            preview += card.Enchantment.EnchantBlockAdditive(preview);
            preview *= card.Enchantment.EnchantBlockMultiplicative(preview);
            if (!card.IsEnchantmentPreview)
                EnchantedValue = preview;
        }

        if (runGlobalHooks && card.CombatState is not null)
            preview = Hook.ModifyBlock(card.CombatState, card.Owner.Creature, baseValue, Props, card, null, out _);

        PreviewValue = preview;
    }
}

public class Swing() : SakuraModCard(2, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
{
    protected override bool HasExtraEffect => true;
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
            if (IsUsingExtraEffect)
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





public class Break() : SakuraModCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy), IExtraEffectCard
{
    protected override bool HasExtraEffect => true;
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
        await TriggerExtraEffect(choiceContext, play);
    }

    public async Task OnExtraEffect(PlayerChoiceContext choiceContext, CardPlay play)
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
    protected override bool HasExtraEffect => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Fire, SakuraKeywords.Manifest];
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

    private int ChoiceRepeatCount => IsUsingExtraEffect ? 2 : 1;

    private async Task Manifest(PlayerChoiceContext choiceContext, int repeats)
    {
        for (var i = 0; i < repeats; i++)
            await SakuraManifestLoop.Manifest(this, choiceContext, DynamicVars["ManifestCards"].IntValue);
    }

    private async Task Draw(PlayerChoiceContext choiceContext, int repeats) =>
        await CardPileCmd.Draw(choiceContext, DynamicVars["DrawCards"].IntValue * repeats, Owner, false);

    protected override void OnUpgrade() => DynamicVars["ManifestCards"].UpgradeValueBy(1);
}

public class Promise() : SakuraModCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self), IExtraEffectCard
{
    protected override bool HasExtraEffect => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Earth];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(6, ValueProp.Move), new PowerVar<PlatingPower>(4)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block.IntValue, ValueProp.Move, play, false);
        await PowerCmd.Apply<PromiseManifestPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this, false);
        await TriggerExtraEffect(choiceContext, play);
    }

    public async Task OnExtraEffect(PlayerChoiceContext choiceContext, CardPlay play) =>
        await PowerCmd.Apply<PlatingPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["PlatingPower"].IntValue,
            Owner.Creature,
            this,
            false);

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(4);
}

public class Struggle() : SakuraModCard(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override bool HasExtraEffect => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Fire];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(20, ValueProp.Move),
        new ExtraDamageVar(8)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var damage = DynamicVars.Damage.IntValue;
        if (IsUsingExtraEffect)
            damage += DynamicVars.ExtraDamage.IntValue;

        await SakuraActions.Attack(choiceContext, this, RequiredTarget(play), damage);
    }

    public override Task AfterCardEnteredCombat(CardModel card)
    {
        if (card != this || IsClone)
            return Task.CompletedTask;

        var attacksPlayedThisTurn = CombatManager.Instance.History.CardPlaysFinished.Count(entry =>
            entry.CardPlay.Card.Type == CardType.Attack
            && entry.CardPlay.Card.Owner == Owner
            && entry.HappenedThisTurn(CombatState));
        ReduceCostBy(attacksPlayedThisTurn);
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await base.AfterCardPlayed(choiceContext, play);

        if (play.Card.Owner == Owner && play.Card.Type == CardType.Attack)
            ReduceCostBy(1);
    }

    private void ReduceCostBy(int amount) => EnergyCost.AddThisTurn(-amount);

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(4);
}


public class Blaze() : SakuraModCard(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
{
    protected override bool HasExtraEffect => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Fire];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CalculationBaseVar(18),
        new ExtraDamageVar(2),
        new CalculatedDamageVar(ValueProp.Move).WithMultiplier(BlazeRules.ExhaustedCardMultiplier)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = RequiredTarget(play);
        SakuraCardPlayVfx.PlayBlaze(target);
        await SakuraActions.Attack(choiceContext, this, target, DynamicVars.CalculatedDamage);
    }

    protected override void OnUpgrade() => DynamicVars.CalculationBase.UpgradeValueBy(6);
}

internal static class BlazeRules
{
    public static decimal ExhaustedCardMultiplier(CardModel card, Creature? target) =>
        card.Owner is { } owner
            ? (CardPile.Get(PileType.Exhaust, owner)?.Cards.Count ?? 0)
                * (SakuraModCard.UsesMagicChargeExtraEffect(card) ? 2 : 1)
            : 0;
}

public class Dreaming() : SakuraModCard(2, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Water];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
        => await PowerCmd.Apply<DreamingPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this, false);

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
