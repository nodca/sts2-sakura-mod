using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Classic.Character;
using SakuraMod.SakuraModCode.Classic.Powers;
using SakuraMod.SakuraModCode.Classic.Relics;
using SakuraMod.SakuraModCode.Extensions;
using SakuraMod.SakuraModCode.Powers;
using STS2RitsuLib.Cards.FreePlay;
using STS2RitsuLib.Models.Capabilities;
using STS2RitsuLib.Scaffolding.Content;
using System.Runtime.CompilerServices;

namespace SakuraMod.SakuraModCode.Classic.Cards;

public abstract class ClassicSakuraCard(
    int cost,
    CardType type,
    CardRarity rarity,
    TargetType target) :
    ModCardTemplate(cost, type, rarity, target)
{
    protected static readonly LocString HandPrompt = new("cards", "SAKURAMOD-GENERIC.handPrompt");
    protected static LocString CardLoc<TCard>(string suffix) where TCard : CardModel =>
        new("cards", $"{ModelDb.GetId(typeof(TCard)).Entry}.{suffix}");

    internal SourceEraClass? Era => SakuraCardCatalog.MetadataFor(GetType()).Era;
    public SourceCardIdentity? Identity => SakuraCardCatalog.MetadataFor(GetType()).Identity;
    internal bool IsClowCard => Era == SourceEraClass.Clow;
    internal bool IsSakuraCard => Era == SourceEraClass.Sakura;
    internal bool IsClassicSourceCard => Era is SourceEraClass.Clow or SourceEraClass.Sakura;
    internal bool IsSpellCard => this is ClassicSpellCard;
    internal bool ShowsEnergyCost => this is SpellSeal or SpellRelease || !IsSpellCard;

    public virtual ClassicElement Element => ClassicElement.None;
    internal virtual bool GrantsMagicCharge => IsClassicSourceCard;
    internal virtual bool AddsVoidOnNormalSakuraPlay => IsSakuraCard;
    internal bool ShowsSakuraCardVoidTip => AddsVoidOnNormalSakuraPlay;

    public override string CustomPortraitPath => "card.png".BigCardImagePath();
    public override string PortraitPath => "card.png".CardImagePath();
    public override string BetaPortraitPath => "card.png".CardImagePath();
    protected override IEnumerable<string> ExtraRunAssetPaths => ClassicSakuraVisualAssets.RunAssetPaths(this);

    protected sealed override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play) =>
        SakuraExtraEffectTransaction.Execute(this, choiceContext, play, PlayCard);

    protected abstract Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play);

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await SakuraExtraEffectTransaction.AfterCardPlayed(this, choiceContext, play);
    }

    public override Task AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)
    {
        if (card == this)
            ClassicReleaseState.Reset(this);

        return Task.CompletedTask;
    }

    protected int ReleasedValue(string varName) => DynamicVars[varName].IntValue;

    protected int ReleasedDamage() => ReleasedValue("Damage");
    protected int ReleasedBlock() => ReleasedValue("Block");
    protected int ReleasedMagic() => ReleasedValue("Magic");

    protected static Creature RequiredTarget(CardPlay play) =>
        play.Target ?? throw new InvalidOperationException("Card target is required by this card's TargetType.");

    protected async Task<AttackCommand?> DealDamage(PlayerChoiceContext choiceContext, Creature target, int amount, ValueProp props = ValueProp.Move, int hitCount = 1)
    {
        if (hitCount <= 0)
            return null;

        return await DamageCmd.Attack(amount)
            .WithHitCount(hitCount)
            .FromCard(this)
            .WithValueProp(props)
            .WithNoAttackerAnim()
            .Targeting(target)
            .Execute(choiceContext);
    }

    protected async Task<AttackCommand?> DealDamageToEnemies(PlayerChoiceContext choiceContext, IEnumerable<Creature> targets, int amount, ValueProp props = ValueProp.Move, int hitCount = 1)
    {
        if (hitCount <= 0)
            return null;

        return await DamageCmd.Attack(amount)
            .WithHitCount(hitCount)
            .FromCard(this)
            .WithValueProp(props)
            .WithNoAttackerAnim()
            .TargetingFiltered(targets.Where(static target => target.IsAlive).ToList())
            .Execute(choiceContext);
    }

    protected async Task DealDamageToRandomEnemies(PlayerChoiceContext choiceContext, int amount, int hitCount, ValueProp props = ValueProp.Move)
    {
        if (hitCount <= 0)
            return;

        await DamageCmd.Attack(amount)
            .WithHitCount(hitCount)
            .FromCard(this)
            .WithValueProp(props)
            .WithNoAttackerAnim()
            .TargetingRandomOpponents(CombatState!)
            .Execute(choiceContext);
    }

    protected async Task TriggerCurrentPoison(
        PlayerChoiceContext choiceContext,
        IEnumerable<Creature> targets,
        int triggerCount)
    {
        if (triggerCount <= 0)
            return;

        foreach (var target in targets.Where(static target => target.IsAlive).ToList())
        {
            for (var i = 0; i < triggerCount && target.IsAlive; i++)
            {
                var poison = target.GetPower<PoisonPower>()?.Amount ?? 0;
                if (poison <= 0)
                    break;

                await DealDamage(choiceContext, target, poison, ValueProp.Unblockable);
            }
        }
    }

    protected async Task DealDamageHit(AttackContext attackContext, PlayerChoiceContext choiceContext, Creature target, int amount, ValueProp props = ValueProp.Move)
    {
        if (!target.IsAlive)
            return;

        attackContext.AddHit(await CreatureCmd.Damage(choiceContext, target, amount, props, Owner.Creature, this));
    }

    protected async Task GainBlock(CardPlay play, int amount) =>
        await CreatureCmd.GainBlock(Owner.Creature, amount, ValueProp.Move, play, false);

    protected async Task ApplyPower<T>(PlayerChoiceContext choiceContext, Creature target, int amount) where T : PowerModel =>
        await PowerCmd.Apply<T>(choiceContext, target, amount, Owner.Creature, this, false);

    protected async Task ApplyPowerToEnemies<T>(PlayerChoiceContext choiceContext, int amount) where T : PowerModel =>
        await PowerCmd.Apply<T>(choiceContext, CombatState!.HittableEnemies.ToList(), amount, Owner.Creature, this, false);

    protected async Task AddGeneratedSpells<T>(PlayerChoiceContext choiceContext, int amount) where T : CardModel
    {
        var combatState = Owner.Creature.CombatState
            ?? throw new InvalidOperationException($"Generated {typeof(T).Name} requires an active combat.");
        for (var i = 0; i < amount; i++)
        {
            var card = combatState.CreateCard<T>(Owner);
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner, CardPilePosition.Random);
        }
    }

    protected void AddKeywordIfMissing(CardKeyword keyword)
    {
        if (!Keywords.Contains(keyword))
            AddKeyword(keyword);
    }

}

public abstract class ClassicClowCard(
    int cost,
    CardType type,
    CardRarity rarity,
    TargetType target) :
    ClassicSakuraCard(cost, type, rarity, target);

public abstract class ClassicExtraClowCard :
    ClassicClowCard,
    ISakuraExtraEffectCard
{
    protected ClassicExtraClowCard(
        int cost,
        CardType type,
        CardRarity rarity,
        TargetType target) :
        base(cost, type, rarity, target)
    { }

    protected abstract override Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play);

    protected virtual Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        PlayCard(choiceContext, play);

    Task ISakuraExtraEffectCard.PlayWithExtraEffect(
        PlayerChoiceContext choiceContext,
        CardPlay play,
        SakuraExtraEffectActivation activation) =>
        activation.IsActive
            ? PlayActivatedCard(choiceContext, play)
            : PlayCard(choiceContext, play);
}

public abstract class ClassicSakuraConversionCard(
    int cost,
    CardType type,
    TargetType target) :
    ClassicSakuraCard(cost, type, CardRarity.Token, target)
{
    public override int MaxUpgradeLevel => 0;
    public override bool CanBeGeneratedInCombat => false;
}

public abstract class ClassicSpecialSakuraCard(int cost, CardType type, TargetType target) :
    ClassicSakuraCard(cost, type, CardRarity.Event, target)
{
    public override int MaxUpgradeLevel => 0;
}

public abstract class ClassicSakuraAncientCard(
    int cost,
    CardType type,
    TargetType target) :
    ClassicSakuraCard(cost, type, CardRarity.Ancient, target)
{
    protected abstract string AncientPortraitFileName { get; }
    private string AncientPortraitPath => AncientPortraitFileName.AncientCardImagePath();

    internal override bool GrantsMagicCharge => false;
    internal override bool AddsVoidOnNormalSakuraPlay => false;
    public override bool CanBeGeneratedInCombat => false;
    public sealed override string CustomPortraitPath => AncientPortraitPath;
    public sealed override string PortraitPath => AncientPortraitPath;
    public sealed override string BetaPortraitPath => AncientPortraitPath;
    public sealed override CardAssetProfile AssetProfile =>
        SakuraAncientCardAssets.Create(Type, AncientPortraitPath);
}

public abstract class ClassicSpellCard(int cost, CardType type, CardRarity rarity, TargetType target) :
    ClassicSakuraCard(cost, type, rarity, target)
{
    public override bool CanBeGeneratedInCombat => false;

    internal override bool GrantsMagicCharge => false;
}

internal static class SakuraAncientCardAssets
{
    public static CardAssetProfile Create(CardType type, string portraitPath)
    {
        var native = ContentAssetProfiles.AncientCard("ironclad", "break", type);
        return new CardAssetProfile(
            PortraitPath: portraitPath,
            BetaPortraitPath: portraitPath,
            AncientBorderPath: native.AncientBorderPath,
            AncientTextBgPath: native.AncientTextBgPath,
            AncientBannerPath: native.AncientBannerPath,
            VisualStyle: native.VisualStyle);
    }
}

internal static class ClassicSealKillPolicy
{
    public static bool IsSealCard(CardModel? card) =>
        card is SpellSeal or GrowingMagic;
}

[Flags]
public enum ClassicElement
{
    None = 0,
    Firey = 1 << 0,
    Watery = 1 << 1,
    Windy = 1 << 2,
    Earthy = 1 << 3
}

internal static class ClassicElementExtensions
{
    public static bool HasElement(this ClassicElement elements, ClassicElement element) =>
        element != ClassicElement.None && (elements & element) == element;

    public static IEnumerable<ClassicElement> AsElements(this ClassicElement elements)
    {
        if (elements.HasElement(ClassicElement.Earthy))
            yield return ClassicElement.Earthy;
        if (elements.HasElement(ClassicElement.Firey))
            yield return ClassicElement.Firey;
        if (elements.HasElement(ClassicElement.Watery))
            yield return ClassicElement.Watery;
        if (elements.HasElement(ClassicElement.Windy))
            yield return ClassicElement.Windy;
    }
}

internal static class ClassicSakuraCardCatalog
{
    public static IReadOnlyList<Type> AllCardTypes() =>
        SakuraCardCatalog.ClassicCardTypes;

    public static IReadOnlyList<CardModel> RewardableClowTemplates() =>
        SakuraCardCatalog.SourceCardTypes(SourceEraClass.Clow)
            .Where(static type => type != typeof(ClowSword)
                && type != typeof(ClowShield)
                && type != typeof(ClowNothing))
            .Select(TypeToCard)
            .ToList();

    public static IReadOnlyList<CardModel> AllClowTemplates() =>
        SakuraCardCatalog.SourceCardTypes(SourceEraClass.Clow)
            .Where(static type => type != typeof(ClowNothing))
            .Select(TypeToCard)
            .ToList();

    public static CardModel CreateRandomDreamClowCard(Player owner)
    {
        var templates = AllClowTemplates()
            .Where(static card => card is not ClowCreate)
            .ToList();
        return CreateRandomClowCard(owner, templates, "Dream Clow pool");
    }

    public static CardModel CreateRandomDarkClowCard(Player owner)
    {
        var templates = AllClowTemplates();
        var rolledRarity = RollDarkClowRarity(owner);
        var options = templates.Where(card => card.Rarity == rolledRarity).ToList();
        if (options.Count == 0)
            throw new InvalidOperationException($"Dark Clow pool has no {rolledRarity} cards.");

        return CreateRandomClowCard(owner, options, "Dark Clow pool");
    }

    public static Type? SakuraTypeFor(SourceCardIdentity identity) =>
        SakuraCardCatalog.TypeFor(identity, SourceEraClass.Sakura);

    public static CardModel? SakuraTemplateFor(SourceCardIdentity identity) =>
        SakuraTypeFor(identity) is { } type ? TypeToCard(type) : null;

    public static Type? ClowTypeFor(SourceCardIdentity identity) =>
        SakuraCardCatalog.TypeFor(identity, SourceEraClass.Clow);

    public static bool HasSakuraIdentity(Player owner, SourceCardIdentity identity) =>
        CardsInAllKnownPiles(owner)
            .OfType<ClassicSakuraConversionCard>()
            .Any(card => card.Identity == identity);

    public static CardModel CreateMirrorCopySource(CardModel source)
    {
        if (source is SakuraLove)
            return CreateCombatCardFromType<SpellEmptySpell>(source);

        if (source is SakuraHope)
            return CreateCombatCardFromType<ClowNothing>(source);

        if (source is ClassicSakuraConversionCard { Identity: { } identity } && HasSakuraIdentity(source.Owner, identity))
        {
            var clowType = ClowTypeFor(identity)
                ?? throw new InvalidOperationException($"Missing Clow mirror source for {identity}.");
            var canonicalClow = ModelDb.GetById<CardModel>(ModelDb.GetId(clowType));
            var clowCopy = source.CombatState!.CreateCard(canonicalClow, source.Owner);
            MatchStatEquivalentCopy(clowCopy, source);
            return clowCopy;
        }

        return source.CreateClone();
    }

    public static bool HasSpecialCard<T>(Player owner) where T : CardModel =>
        CardsInAllKnownPiles(owner).OfType<T>().Any();

    public static int ConvertedSakuraCount(Player owner) =>
        owner.Deck.Cards.OfType<ClassicSakuraConversionCard>().Select(card => card.Identity).Distinct().Count()
        + owner.Deck.Cards.Count(static card => card is SpellTurn);

    public static int StarterClowCount(Player owner, SourceCardIdentity identity) =>
        Math.Clamp(owner.Deck.Cards.OfType<ClassicClowCard>().Count(card => card.Identity == identity), 1, 4);

    public static bool IsEligibleClowForTurn(CardModel card) =>
        card is ClassicClowCard { Identity: { } identity }
        && card.Pile?.Type == PileType.Hand
        && SakuraTypeFor(identity) is not null
        && !HasSakuraIdentity(card.Owner, identity);

    // Clear effects must not replay or recover Turn, otherwise one generated
    // Turn can repeatedly convert Clow Cards into Sakura Cards.
    internal static bool CanBeTargetedByClearCardEffects(CardModel card) =>
        card is not SpellTurn;

    private static CardModel TypeToCard(Type type) =>
        ModelDb.GetById<CardModel>(ModelDb.GetId(type));

    private static CardModel CreateCombatCardFromType<T>(CardModel source) where T : CardModel
    {
        var canonical = ModelDb.GetById<CardModel>(ModelDb.GetId(typeof(T)));
        return source.CombatState!.CreateCard(canonical, source.Owner);
    }

    private static CardRarity RollDarkClowRarity(Player owner)
    {
        var roll = owner.RunState.Rng.CombatCardSelection.NextInt(10);
        if (roll == 0)
            return CardRarity.Rare;
        return roll <= 3 ? CardRarity.Uncommon : CardRarity.Common;
    }

    private static CardModel CreateRandomClowCard(Player owner, IReadOnlyList<CardModel> templates, string poolName)
    {
        if (templates.Count == 0)
            throw new InvalidOperationException($"{poolName} is empty.");

        var template = owner.RunState.Rng.CombatCardSelection.NextItem(templates)
            ?? throw new InvalidOperationException($"{poolName} random selection failed.");
        var combatState = owner.Creature.CombatState
            ?? throw new InvalidOperationException($"{poolName} generated cards require an active combat.");
        return combatState.CreateCard(template, owner);
    }

    private static void MatchUpgradeLevel(CardModel target, CardModel source)
    {
        while (target.CurrentUpgradeLevel < source.CurrentUpgradeLevel && target.IsUpgradable)
            target.UpgradeInternal();
    }

    private static void MatchStatEquivalentCopy(CardModel target, CardModel source)
    {
        MatchUpgradeLevel(target, source);

        if (!source.EnergyCost.CostsX && !target.EnergyCost.CostsX)
            target.EnergyCost.SetCustomBaseCost(Math.Max(0, source.EnergyCost.GetWithModifiers(CostModifiers.Local)));

        foreach (var (name, sourceVar) in source.DynamicVars)
        {
            if (target.DynamicVars.TryGetValue(name, out var targetVar))
                targetVar.BaseValue = sourceVar.BaseValue;
        }
    }

    private static IEnumerable<CardModel> CardsInAllKnownPiles(Player owner) =>
        owner.Deck.Cards.Concat(owner.Piles.SelectMany(static pile => pile.Cards));
}

internal static class ClassicStarterScaling
{
    private const int BaseStarterCount = 4;
    private const decimal LoneRate = 0.2m;
    private const decimal MaxLoneRate = 0.6m;

    public static int ScaledValue(Player owner, SourceCardIdentity identity, int baseValue)
    {
        var count = ClassicSakuraCardCatalog.StarterClowCount(owner, identity);
        var rate = Math.Min(MaxLoneRate, Math.Max(0m, (BaseStarterCount - count) * LoneRate));
        return (int)Math.Floor(baseValue * (1m + rate));
    }
}

internal interface IClassicEffectiveValueVar
{
    int EffectiveValue(CardModel card);
}

internal static class ClassicCardValues
{
    public static int EffectiveValue(CardModel card, DynamicVar variable) =>
        variable is IClassicEffectiveValueVar effectiveValueVar
            ? effectiveValueVar.EffectiveValue(card)
            : variable.IntValue;
}

internal sealed class ClassicDamageVar : DamageVar, IClassicEffectiveValueVar
{
    private readonly SourceCardIdentity? _starterIdentity;

    public ClassicDamageVar(decimal damage, ValueProp props, SourceCardIdentity? starterIdentity = null) :
        base(damage, props)
    {
        _starterIdentity = starterIdentity;
    }

    public ClassicDamageVar(string name, decimal damage, ValueProp props, SourceCardIdentity? starterIdentity = null) :
        base(name, damage, props)
    {
        _starterIdentity = starterIdentity;
    }

    public override void UpdateCardPreview(CardModel card, CardPreviewMode previewMode, Creature? target, bool runGlobalHooks)
    {
        decimal baseValue = EffectiveValue(card);
        var preview = baseValue;
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

    public int EffectiveValue(CardModel card)
    {
        var baseValue = (int)BaseValue;
        if (card.Owner is not null && _starterIdentity is { } identity)
        {
            return ClassicReleaseState.AdjustedReleasedValue(
                card,
                Name,
                baseValue,
                value => ClassicStarterScaling.ScaledValue(card.Owner, identity, value));
        }

        return baseValue;
    }
}

internal sealed class ClassicCombatHistoryDamageVar(
    decimal damage,
    ValueProp props,
    Func<CardModel, int> hitCount,
    string perHitVarName = ClassicSnowRules.PerCardDamageVar) : DamageVar(damage, props)
{
    public override void UpdateCardPreview(CardModel card, CardPreviewMode previewMode, Creature? target, bool runGlobalHooks)
    {
        var count = Math.Max(0, hitCount(card));
        var baseValue = card.DynamicVars.TryGetValue(perHitVarName, out var perHitVar)
            ? (int)perHitVar.BaseValue
            : (int)BaseValue;
        decimal preview = baseValue;
        if (card.Enchantment is not null)
        {
            preview += card.Enchantment.EnchantDamageAdditive(preview, Props);
            preview *= card.Enchantment.EnchantDamageMultiplicative(preview, Props);
            if (!card.IsEnchantmentPreview)
                EnchantedValue = preview * count;
        }

        if (runGlobalHooks)
            preview = Hook.ModifyDamage(card.Owner.RunState, card.CombatState, target, card.Owner.Creature, baseValue, Props, card, ModifyDamageHookType.All, previewMode, out _);

        PreviewValue = preview * count;
    }
}

internal sealed class ClassicCombatHistoryCountVar(Func<CardModel, int> hitCount) : DynamicVar("Magic", 0)
{
    public override void UpdateCardPreview(CardModel card, CardPreviewMode previewMode, Creature? target, bool runGlobalHooks) =>
        PreviewValue = Math.Max(0, hitCount(card));
}

internal sealed class ClassicBlockVar(decimal block, ValueProp props, SourceCardIdentity? starterIdentity = null) :
    BlockVar(block, props), IClassicEffectiveValueVar
{
    public override void UpdateCardPreview(CardModel card, CardPreviewMode previewMode, Creature? target, bool runGlobalHooks)
    {
        decimal baseValue = EffectiveValue(card);
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

    public int EffectiveValue(CardModel card)
    {
        var baseValue = (int)BaseValue;
        if (card.Owner is not null && starterIdentity is { } identity)
        {
            return ClassicReleaseState.AdjustedReleasedValue(
                card,
                Name,
                baseValue,
                value => ClassicStarterScaling.ScaledValue(card.Owner, identity, value));
        }

        return baseValue;
    }
}

internal sealed class ClassicReturnRechargeVar() : DynamicVar("Magic", 15)
{
    public override void UpdateCardPreview(CardModel card, CardPreviewMode previewMode, Creature? target, bool runGlobalHooks) =>
        PreviewValue = card.Owner is { } owner
            ? ClassicSealedWandRelic.ReturnRechargeAmountFor(owner)
            : BaseValue;
}

internal static class ClassicSakuraMagic
{
    public const int NormalMagicChargeGain = 1;
    public const int PowerMagicChargeGain = 2;
    public const int ElementOpportunityThreshold = 5;
    public const int ExtraEffectCost = 10;
    public const int SwordExtraHpLoss = 15;
    public const int ShieldMetallicizeBlock = 3;
    public const int CloudExtraBlock = 12;
    public const int FlowerExtraEnergy = 2;

    public static bool CanSpendMagic(Player? owner) =>
        owner?.Creature.GetPower<ClassicMagicChargePower>()?.Amount >= ExtraEffectCost;

    internal static ClassicMagicChargeBand BandFor(int amount) =>
        amount >= ExtraEffectCost
            ? ClassicMagicChargeBand.Full
            : amount >= ElementOpportunityThreshold
                ? ClassicMagicChargeBand.Resonant
                : ClassicMagicChargeBand.Low;

    internal static ClassicMagicChargeOpportunityTransition OpportunityTransition(int previousAmount, int currentAmount)
    {
        var previousBand = BandFor(previousAmount);
        var currentBand = BandFor(currentAmount);
        if (currentBand == ClassicMagicChargeBand.Resonant && previousBand != ClassicMagicChargeBand.Resonant)
            return ClassicMagicChargeOpportunityTransition.Arm;
        if (currentBand != ClassicMagicChargeBand.Resonant)
            return ClassicMagicChargeOpportunityTransition.Expire;
        return ClassicMagicChargeOpportunityTransition.Preserve;
    }

    internal static ClassicMagicChargeOpportunity? CaptureOpportunity(Player owner)
    {
        var power = owner.Creature.GetPower<ClassicMagicChargePower>();
        if (power is null || BandFor(power.Amount) != ClassicMagicChargeBand.Resonant)
            return null;

        var generation = power.ArmedOpportunityGeneration;
        return generation > 0
            ? new ClassicMagicChargeOpportunity(power, generation)
            : null;
    }

    internal static bool TryConsumeOpportunity(Player owner, ClassicMagicChargeOpportunity opportunity) =>
        ReferenceEquals(owner.Creature.GetPower<ClassicMagicChargePower>(), opportunity.Power)
        && opportunity.Power.TryConsumeOpportunity(opportunity.Generation);

    public static async Task SpendMagic(PlayerChoiceContext choiceContext, Player owner, int amount)
    {
        if (amount > 0)
            await ModifyMagic(choiceContext, owner, -amount, null, false);
    }

    public static async Task SpendUpToMagic(PlayerChoiceContext choiceContext, Player owner, int amount)
    {
        var current = owner.Creature.GetPower<ClassicMagicChargePower>()?.Amount ?? 0;
        if (current <= 0 || amount <= 0)
            return;

        await ModifyMagic(choiceContext, owner, -Math.Min(current, amount), null, false);
    }

    public static async Task<int> SpendAllMagic(PlayerChoiceContext choiceContext, Player owner)
    {
        var amount = owner.Creature.GetPower<ClassicMagicChargePower>()?.Amount ?? 0;
        if (amount <= 0)
            return 0;

        await ModifyMagic(choiceContext, owner, -amount, null, false);
        return amount;
    }

    public static async Task GainMagic(PlayerChoiceContext choiceContext, CardModel card)
    {
        var amount = card.Type == CardType.Power ? PowerMagicChargeGain : NormalMagicChargeGain;
        await GainMagic(choiceContext, card.Owner, amount, card);
    }

    public static async Task GainMagic(
        PlayerChoiceContext choiceContext,
        Player owner,
        int amount,
        CardModel? cardSource = null,
        bool fast = false)
    {
        if (amount > 0)
            await ModifyMagic(choiceContext, owner, amount, cardSource, fast);
    }

    private static async Task ModifyMagic(
        PlayerChoiceContext choiceContext,
        Player owner,
        int delta,
        CardModel? cardSource,
        bool fast)
    {
        if (delta == 0)
            return;

        var previousAmount = owner.Creature.GetPower<ClassicMagicChargePower>()?.Amount ?? 0;
        if (delta > 0)
        {
            await PowerCmd.Apply<ClassicMagicChargePower>(
                choiceContext,
                owner.Creature,
                delta,
                owner.Creature,
                cardSource,
                fast);
        }
        else if (owner.Creature.GetPower<ClassicMagicChargePower>() is { } power)
        {
            await PowerCmd.ModifyAmount(choiceContext, power, delta, owner.Creature, cardSource, fast);
        }

        var currentPower = owner.Creature.GetPower<ClassicMagicChargePower>();
        var currentAmount = currentPower?.Amount ?? 0;
        if (currentPower is not null)
        {
            switch (OpportunityTransition(previousAmount, currentAmount))
            {
                case ClassicMagicChargeOpportunityTransition.Arm:
                    currentPower.ArmNextOpportunity();
                    break;
                case ClassicMagicChargeOpportunityTransition.Expire:
                    currentPower.ExpireOpportunity();
                    break;
            }

            currentPower.NotifyProjectionChanged();
        }
    }

    public static void SetFreeForRestOfTurn(CardModel card)
    {
        card.SetToFreeForRestOfTurn();
    }

    public static async Task AddVoidToDrawPile(PlayerChoiceContext choiceContext, Player owner)
    {
        var combatState = owner.Creature.CombatState
            ?? throw new InvalidOperationException("Classic Sakura generated Void requires an active combat.");
        var card = combatState.CreateCard<MegaCrit.Sts2.Core.Models.Cards.Void>(owner);
        CardCmd.PreviewCardPileAdd(await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombatWithResult(
            card,
            PileType.Draw,
            owner,
            CardPilePosition.Random));
    }

    public static async Task AddVoidToDiscardPile(PlayerChoiceContext choiceContext, Player owner)
    {
        var combatState = owner.Creature.CombatState
            ?? throw new InvalidOperationException("Classic Sakura generated Void requires an active combat.");
        var card = combatState.CreateCard<MegaCrit.Sts2.Core.Models.Cards.Void>(owner);
        CardCmd.PreviewCardPileAdd(await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombatWithResult(
            card,
            PileType.Discard,
            owner,
            CardPilePosition.Bottom));
    }

}

internal enum ClassicMagicChargeBand
{
    Low,
    Resonant,
    Full
}

internal enum ClassicMagicChargeOpportunityTransition
{
    Preserve,
    Arm,
    Expire
}

internal readonly record struct ClassicMagicChargeOpportunity(
    ClassicMagicChargePower Power,
    int Generation);

internal static class ClassicCreateRewards
{
    private const int RareRoll = 0;
    private const int UncommonRolls = 3;
    private const int TotalRolls = 8;

    public static void AddNormalRelicReward(Player owner) =>
        CurrentCombatRoom(owner).AddExtraReward(owner, new RelicReward(RollSourceRelicRarity(owner), owner));

    public static void AddExclusiveOrNormalRelicReward(Player owner)
    {
        var relic = ClassicSakuraExclusiveRelics.TryPullRandomAvailableForCreate(owner);
        if (relic is null)
        {
            AddNormalRelicReward(owner);
            return;
        }

        CurrentCombatRoom(owner).AddExtraReward(owner, new RelicReward(relic.ToMutable(), owner));
    }

    private static RelicRarity RollSourceRelicRarity(Player owner)
    {
        var roll = owner.PlayerRng.Rewards.NextInt(TotalRolls);
        if (roll == RareRoll)
            return RelicRarity.Rare;
        return roll <= UncommonRolls ? RelicRarity.Uncommon : RelicRarity.Common;
    }

    private static CombatRoom CurrentCombatRoom(Player owner) =>
        owner.RunState.CurrentRoom as CombatRoom
        ?? throw new InvalidOperationException("Create requires the current room to be a combat room.");
}

internal static class ClassicReleaseState
{
    private static readonly ConditionalWeakTable<CardModel, ReleaseMarker> ReleasedCards = new();

    public static bool IsReleased(CardModel card) =>
        ReleasedCards.TryGetValue(card, out _);

    public static void Apply(CardModel card, float releaseRate)
    {
        if (ReleasedCards.TryGetValue(card, out _))
            return;

        var marker = new ReleaseMarker(releaseRate);

        if (card.EnergyCost.GetWithModifiers(CostModifiers.Local) > 0)
            card.EnergyCost.SetThisTurnOrUntilPlayed(0, true);

        if (card.Type == CardType.Power)
        {
            AddReleaseKeyword(card, CardKeyword.Ethereal, marker);
        }
        else
        {
            AddReleaseKeyword(card, CardKeyword.Exhaust, marker);
            if (!card.Keywords.Contains(CardKeyword.Retain))
                AddReleaseKeyword(card, CardKeyword.Ethereal, marker);
        }

        foreach (var variable in card.DynamicVars.Values)
        {
            var delta = ReleaseDelta(variable.IntValue, releaseRate);
            if (delta == 0)
                continue;

            variable.BaseValue += delta;
            marker.DynamicVarDeltas[variable.Name] = delta;
        }

        ReleasedCards.Add(card, marker);
    }

    public static int AdjustedReleasedValue(
        CardModel card,
        string varName,
        int currentValue,
        Func<int, int> adjust)
    {
        if (!ReleasedCards.TryGetValue(card, out var marker))
            return adjust(currentValue);

        var releaseDelta = marker.DynamicVarDeltas.GetValueOrDefault(varName);
        var adjustedValue = adjust(currentValue - releaseDelta);
        return adjustedValue + ReleaseDelta(adjustedValue, marker.Rate);
    }

    public static void Reset(CardModel card)
    {
        if (!ReleasedCards.TryGetValue(card, out var marker))
            return;

        foreach (var (name, delta) in marker.DynamicVarDeltas)
        {
            if (card.DynamicVars.TryGetValue(name, out var variable))
                variable.BaseValue -= delta;
        }

        foreach (var keyword in marker.AddedKeywords)
        {
            if (card.Keywords.Contains(keyword))
                card.RemoveKeyword(keyword);
        }

        ReleasedCards.Remove(card);
    }

    private static void AddReleaseKeyword(CardModel card, CardKeyword keyword, ReleaseMarker marker)
    {
        if (card.Keywords.Contains(keyword))
            return;

        card.AddKeyword(keyword);
        marker.AddedKeywords.Add(keyword);
    }

    private static int ReleaseDelta(int baseValue, float rate) =>
        (int)Math.Floor(baseValue * rate);

    private sealed class ReleaseMarker(float rate)
    {
        public float Rate { get; } = rate;
        public Dictionary<string, int> DynamicVarDeltas { get; } = [];
        public List<CardKeyword> AddedKeywords { get; } = [];
    }
}

internal static class ClassicSakuraAssetPaths
{
    public static string NormalClassicArtStem(this string path) =>
        path.EndsWith("_p.png", StringComparison.Ordinal)
            ? string.Concat(path.AsSpan(0, path.Length - "_p.png".Length), ".png")
            : throw new InvalidOperationException($"Classic art stem must point at a _p.png large art file: {path}");
}

internal static class ClassicCombatHistory
{
    public static int PlayedCardsThisCombat(Player owner, Func<CardModel, bool> predicate) =>
        CombatManager.Instance.History.CardPlaysFinished
            .Where(entry => entry is CardPlayFinishedEntry { CardPlay.Card.Owner: var cardOwner } && cardOwner == owner)
            .Select(entry => ((CardPlayFinishedEntry)entry).CardPlay.Card)
            .Count(predicate);
}

internal static class ClassicSnowRules
{
    public const string PerCardDamageVar = "SnowDamage";

    public static int PlayedWateryCards(CardModel card) =>
        PlayedCards(card, CountsAsWateryCard);

    internal static bool CountsAsWateryCard(CardModel card) =>
        SakuraActions.HasClassicElement(card, ClassicElement.Watery);

    public static IEnumerable<Creature> FrostbiteReceivers(AttackCommand? attack) =>
        attack?.Results
            .SelectMany(static hit => hit)
            .Where(ShouldApplyFrostbite)
            .Select(static result => result.Receiver)
        ?? [];

    internal static bool ShouldApplyFrostbite(DamageResult result) =>
        result.UnblockedDamage > 0;

    public static async Task ApplyFrostbite(
        PlayerChoiceContext choiceContext,
        ClassicSakuraCard source,
        AttackCommand? attack)
    {
        foreach (var target in FrostbiteReceivers(attack))
        {
            if (!target.IsAlive || target.Side == source.Owner.Creature.Side)
                continue;

            await PowerCmd.Apply<SakuraFrostbitePower>(
                choiceContext,
                target,
                1,
                source.Owner.Creature,
                source,
                false);
        }
    }

    private static int PlayedCards(CardModel card, Func<CardModel, bool> predicate) =>
        card.Owner is null || card.CombatState is null
            ? 0
            : ClassicCombatHistory.PlayedCardsThisCombat(card.Owner, predicate);
}

internal static class ClassicPowerRules
{
    public static bool IsBubblesRemovableBuff(PowerModel power) =>
        power is StrengthPower { Amount: > 0 }
        or ArtifactPower { Amount: > 0 }
        or RitualPower { Amount: > 0 }
        or ThornsPower { Amount: > 0 }
        or PlatingPower { Amount: > 0 };

    public static async Task ApplyBypassingArtifact<T>(
        PlayerChoiceContext choiceContext,
        Creature target,
        int amount,
        Creature source,
        CardModel cardSource) where T : PowerModel
    {
        var artifact = target.GetPower<ArtifactPower>();
        if (artifact is null)
        {
            await PowerCmd.Apply<T>(choiceContext, target, amount, source, cardSource, false);
            return;
        }

        var artifactAmount = artifact.Amount;
        await PowerCmd.Remove(artifact);
        await PowerCmd.Apply<T>(choiceContext, target, amount, source, cardSource, false);
        if (target.IsAlive)
            await PowerCmd.Apply<ArtifactPower>(choiceContext, target, artifactAmount, source, cardSource, false);
    }
}
