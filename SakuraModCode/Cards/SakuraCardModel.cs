using Godot;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Extensions;
using SakuraMod.SakuraModCode.Powers;
using STS2RitsuLib.Content;
using STS2RitsuLib.Models.Capabilities;
using STS2RitsuLib.Scaffolding.Content;

namespace SakuraMod.SakuraModCode.Cards;

public abstract class SakuraCardModel : ModCardTemplate
{
    protected SakuraCardModel(int cost, CardType type, CardRarity rarity, TargetType target) :
        base(cost, type, rarity, target)
    {
    }

    protected static LocString CardLoc<TCard>(string suffix) where TCard : CardModel =>
        new("cards", $"{ModelDb.GetId(typeof(TCard)).Entry}.{suffix}");

    public override string CustomPortraitPath => CardModel.MissingPortraitPath;
    public override string PortraitPath => CardModel.MissingPortraitPath;
    public override string BetaPortraitPath => CardModel.MissingPortraitPath;
    protected sealed override IEnumerable<string> ExtraRunAssetPaths =>
        FamilyRunAssetPaths.Concat(SakuraCardVfxAssets.RunAssetPaths(this));

    /// <summary>Frame or face assets owned by the card's visual family.</summary>
    private protected abstract IEnumerable<string> FamilyRunAssetPaths { get; }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (play.Card != this)
            return;

        await SakuraMagicCharge.AfterCardPlayed(choiceContext, this);
        await SakuraFormVoid.AfterCardPlayed(choiceContext, this);
    }

    protected static IReadOnlyList<Creature> PlayTargets(CardPlay play) =>
        play.Target is { } target ? [target] : [];

    protected static Creature RequiredTarget(CardPlay play) =>
        play.Target ?? throw new InvalidOperationException("Card target is required by this card's TargetType.");

    protected sealed override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (this is ISakuraExtraEffectCard)
        {
            await SakuraExtraEffectTransaction.Execute(this, choiceContext, play);
            return;
        }

        var opportunity = SakuraMagicCharge.CaptureOpportunity(Owner);
        SakuraExtraEffectTransaction.TryShowMagicCircle(this, default);
        await PlayCard(choiceContext, play, default);
        await SakuraMagicCharge.TryApplyCapturedOpportunity(choiceContext, this, opportunity);
    }

    protected virtual Task PlayCard(
        PlayerChoiceContext choiceContext,
        CardPlay play,
        SakuraExtraEffectActivation activation) =>
        Task.CompletedTask;

    protected async Task<AttackCommand?> DealDamage(
        PlayerChoiceContext choiceContext,
        Creature target,
        int amount,
        ValueProp props = ValueProp.Move,
        int hitCount = 1,
        string? hitSfx = null,
        Func<Creature, Task>? onHit = null,
        string? hitVfx = null,
        string? hitTmpSfx = null,
        Func<Creature, Godot.Node2D?>? hitVfxNode = null,
        bool spawnHitVfxAtBase = false,
        bool keepAttackerAnim = false)
    {
        if (hitCount <= 0)
            return null;

        var attack = DamageCmd.Attack(amount)
            .WithHitCount(hitCount)
            .FromCard(this)
            .WithValueProp(props)
            .Targeting(target);
        if (!keepAttackerAnim)
            attack.WithNoAttackerAnim();
        WithHitFx(attack, hitVfx, hitSfx, hitTmpSfx, hitVfxNode, spawnHitVfxAtBase);
        return await WithHitObserver(attack, onHit).Execute(choiceContext);
    }

    /// <summary>
    /// Attacks with a <see cref="CalculatedDamageVar"/> so each hit uses the same
    /// calculation as the card preview.
    /// </summary>
    protected async Task<AttackCommand?> DealDamage(
        PlayerChoiceContext choiceContext,
        Creature target,
        CalculatedDamageVar damage,
        int hitCount = 1,
        string? hitSfx = null,
        string? hitVfx = null)
    {
        if (hitCount <= 0)
            return null;

        var attack = DamageCmd.Attack(damage)
            .WithHitCount(hitCount)
            .FromCard(this)
            .WithValueProp(damage.Props)
            .WithNoAttackerAnim()
            .Targeting(target);
        WithHitFx(attack, hitVfx, hitSfx, null, null, spawnHitVfxAtBase: false);
        return await attack.Execute(choiceContext);
    }

    protected async Task<AttackCommand?> DealDamageToEnemies(
        PlayerChoiceContext choiceContext,
        IEnumerable<Creature> targets,
        int amount,
        ValueProp props = ValueProp.Move,
        int hitCount = 1,
        string? hitVfx = null,
        string? hitTmpSfx = null,
        Func<Creature, Godot.Node2D?>? hitVfxNode = null)
    {
        if (hitCount <= 0)
            return null;

        var attack = DamageCmd.Attack(amount)
            .WithHitCount(hitCount)
            .FromCard(this)
            .WithValueProp(props)
            .WithNoAttackerAnim()
            .TargetingFiltered(targets.Where(static target => target.IsAlive).ToList());
        WithHitFx(attack, hitVfx, null, hitTmpSfx, hitVfxNode, spawnHitVfxAtBase: false);
        return await attack.Execute(choiceContext);
    }

    /// <summary>
    /// Applies an optional vanilla hit effect: a scene path through <c>WithHitFx</c>, or a
    /// per-target node factory through <c>WithHitVfxNode</c> for effects that take a tint or scale.
    /// </summary>
    private static void WithHitFx(
        AttackCommand attack,
        string? vfx,
        string? sfx,
        string? tmpSfx,
        Func<Creature, Godot.Node2D?>? vfxNode,
        bool spawnHitVfxAtBase)
    {
        if (vfx != null || sfx != null || tmpSfx != null)
            attack.WithHitFx(vfx, sfx, tmpSfx);
        if (vfx != null && spawnHitVfxAtBase)
            attack.WithHitVfxSpawnedAtBase();
        if (vfxNode != null)
            attack.WithHitVfxNode(vfxNode);
    }

    protected async Task<AttackCommand?> DealDamageToRandomEnemies(
        PlayerChoiceContext choiceContext,
        int amount,
        int hitCount,
        ValueProp props = ValueProp.Move,
        string? hitVfx = null,
        string? hitSfx = null,
        bool spawnHitVfxAtBase = false,
        Func<Creature, Task>? onHit = null)
    {
        if (hitCount <= 0)
            return null;

        var attack = DamageCmd.Attack(amount)
            .WithHitCount(hitCount)
            .FromCard(this)
            .WithValueProp(props)
            .WithNoAttackerAnim()
            .TargetingRandomOpponents(CombatState!)
            .WithHitFx(hitVfx, hitSfx);

        if (spawnHitVfxAtBase)
            attack.WithHitVfxSpawnedAtBase();

        return await WithHitObserver(attack, onHit).Execute(choiceContext);
    }

    /// <summary>
    /// Attaches a per-hit target observer to an attack, or returns it untouched
    /// when no caller asked for one.
    /// </summary>
    /// <remarks>
    /// The engine resolves one hit as: call every <c>WithHitVfxNode</c> factory
    /// with the creature this hit is about to strike, then await
    /// <c>BeforeDamage</c>, then run <c>CreatureCmd.Damage</c> against that same
    /// creature. Returning <see langword="null"/> from the factory makes
    /// <c>AddChildSafely</c> a no-op, so the factory is a pure observer here: the
    /// caller learns the target without contributing a node.
    /// <para>
    /// <c>pending</c> is copied out and cleared before the await because a hit
    /// whose factory never ran would otherwise replay the previous hit's target.
    /// That is not hypothetical: the engine skips the factory entirely when
    /// <c>Creature.GetVfxContainer()</c> is null, which is exactly what happens
    /// under <c>TestMode</c>, and it also skips it when the attack has no valid
    /// target left. In both cases no damage follows either, so skipping the
    /// observer keeps the two in step.
    /// </para>
    /// <para>
    /// There is deliberately no <c>IsAlive</c> check on the way out. A creature
    /// can only reach <c>pending</c> by the factory having just run for it, and
    /// nothing applies damage between the factory and <c>BeforeDamage</c>, so a
    /// target cannot die inside that window. One that died earlier simply never
    /// reaches the factory again, and gating on <c>IsAlive</c> here would do
    /// nothing except drop the flash on the hit that killed it — which is the
    /// one hit that most needs to be seen landing.
    /// </para>
    /// <para>
    /// Both current callers resolve exactly one creature per hit — random
    /// targeting, or a single fixed target — which is what the observer assumes.
    /// An attack that damaged several creatures in one hit would fire the
    /// factory once per creature but run <c>BeforeDamage</c> once, so the
    /// observer is not built for that shape.
    /// </para>
    /// <para>
    /// The engine's own remark on <c>BeforeDamage</c> asks for a first-class
    /// builder method once several attacks share the logic. This is that method:
    /// the alternative is every caller restating the observer pair.
    /// </para>
    /// </remarks>
    private static AttackCommand WithHitObserver(AttackCommand attack, Func<Creature, Task>? onHit)
    {
        if (onHit is null)
            return attack;

        Creature? pending = null;
        return attack
            .WithHitVfxNode(target =>
            {
                pending = target;
                return null;
            })
            .BeforeDamage(async () =>
            {
                var target = pending;
                pending = null;
                if (target is not null)
                    await onHit(target);
            });
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
                // One native Poison tick: damage equal to its Amount, then lose 1.
                if (target.GetPower<PoisonPower>() is not { Amount: > 0 } poison)
                    break;

                await DealDamage(choiceContext, target, poison.Amount, ValueProp.Unblockable);
                if (target.IsAlive)
                    await PowerCmd.Decrement(poison);
            }
        }
    }

    protected async Task DealDamageHit(AttackContext attackContext, PlayerChoiceContext choiceContext, Creature target, int amount, ValueProp props = ValueProp.Move, string? hitVfx = null)
    {
        if (!target.IsAlive)
            return;

        // This hit bypasses AttackCommand, so it plays the hit VFX the way AttackCommand does.
        if (hitVfx != null)
            VfxCmd.PlayOnCreatureCenter(target, hitVfx);
        attackContext.AddHit(await CreatureCmd.Damage(choiceContext, target, amount, props, Owner.Creature, this));
    }

    protected async Task GainBlock(CardPlay play, int amount) =>
        await CreatureCmd.GainBlock(Owner.Creature, amount, ValueProp.Move, play, false);

    protected void AddKeywordIfMissing(CardKeyword keyword)
    {
        if (!Keywords.Contains(keyword))
            AddKeyword(keyword);
    }

    protected void RemoveKeywordIfPresent(CardKeyword keyword)
    {
        if (Keywords.Contains(keyword))
            RemoveKeyword(keyword);
    }
}

public abstract class TransparentCard(
    int cost,
    CardType type,
    CardRarity rarity,
    TargetType target) :
    SakuraCardModel(cost, type, rarity, target)
{
    public override Material? CustomFrameMaterial => SakuraCardFrameVisuals.CustomFrameMaterial(this);
    private protected override IEnumerable<string> FamilyRunAssetPaths => SakuraCardFrameVisuals.RunAssetPaths(this);

    internal virtual IEnumerable<CardKeyword> ReferencedKeywords => [];
    internal virtual IEnumerable<string> ReferencedStaticHoverTipKeys => [];

    internal static bool UsesMagicChargeExtraEffect(CardModel? card) =>
        card is TransparentCard transparent
        && SakuraTransparentCardCatalog.IsTransparentCard(transparent)
        && SakuraExtraEffectTransaction.Supports(transparent)
        && SakuraExtraEffectTransaction.ShouldShowAsActive(transparent);

    internal static bool ShouldShowMagicChargeExtraEffectDescription(CardModel card) =>
        card is TransparentCard transparent
        && SakuraTransparentCardCatalog.IsTransparentCard(transparent)
        && SakuraExtraEffectTransaction.ShouldShowDescription(transparent);

    internal static bool HasMagicChargeExtraEffect(CardModel? card) =>
        card is TransparentCard transparent
        && SakuraTransparentCardCatalog.IsTransparentCard(transparent)
        && SakuraExtraEffectTransaction.Supports(transparent);
}

public abstract class TransparentExtraEffectCard(
    int cost,
    CardType type,
    CardRarity rarity,
    TargetType target) :
    TransparentCard(cost, type, rarity, target), ISakuraExtraEffectCard
{
    protected abstract override Task PlayCard(
        PlayerChoiceContext choiceContext,
        CardPlay play,
        SakuraExtraEffectActivation activation);

    Task ISakuraExtraEffectCard.PlayWithExtraEffect(
        PlayerChoiceContext choiceContext,
        CardPlay play,
        SakuraExtraEffectActivation activation) =>
        PlayCard(choiceContext, play, activation);
}

internal sealed class SakuraCardHoverTipCapability : CardCapability, ICardHoverTipContributor
{
    public static readonly string CapabilityIdValue =
        ModContentRegistry.GetQualifiedModelCapabilityId(MainFile.ModId, "REFERENCED_KEYWORD_HOVER_TIPS");

    public override string CapabilityId => CapabilityIdValue;
    public override bool ShouldReceiveOwnerHooks => false;

    public IEnumerable<IHoverTip> GetHoverTips(CardModel card)
    {
        if (card is not TransparentCard && card is not SakuraOptionCard)
            return [];

        return SakuraCardHoverTips.HoverTips(card);
    }
}

internal static class SakuraCardTextCapabilities
{
    public static void Register() =>
        ModContentRegistry.For(MainFile.ModId)
            .RegisterModelCapability<SakuraCardHoverTipCapability>(
                ModelPublicEntryOptions.FromStem("REFERENCED_KEYWORD_HOVER_TIPS"));
}

internal static class SakuraCardHoverTips
{
    internal const string TemporaryTipKey = "SAKURAMOD-TEMPORARY";
    internal const string ReflectionTipKey = "SAKURAMOD-REFLECTION";
    internal const string LabyrinthTipKey = "SAKURAMOD-ENTER_LABYRINTH";
    internal const string RemindTipKey = "SAKURAMOD-REMIND";
    internal const string DebuffTipKey = "SAKURAMOD-DEBUFF";
    internal const string BubblesBuffTipKey = "SAKURAMOD-BUBBLES_BUFF";
    internal const string BubblesBasicBuffTipKey = "SAKURAMOD-BUBBLES_BASIC_BUFF";
    internal const string MistDefenseTipKey = "SAKURAMOD-MIST_DEFENSE";

    internal static IEnumerable<IHoverTip> HoverTips(CardModel card)
    {
        var keywordTips = KeywordTips(card).ToArray();
        return keywordTips.Select(HoverTipFactory.FromKeyword)
            .Concat(DependentPowerTips(keywordTips))
            .Concat(StaticTipKeys(card).Select(key => (IHoverTip)StaticTip(key)))
            .Distinct();
    }

    internal static IEnumerable<CardKeyword> KeywordTips(CardModel card) =>
        ReferencedKeywords(card)
            .Concat(ElementKeywords(SakuraActions.StaticElementSetOf(card)))
            .Distinct();

    internal static IEnumerable<string> StaticTipKeys(CardModel card) =>
        ReferencedStaticHoverTipKeys(card).Distinct();

    internal static IEnumerable<IHoverTip> DependentPowerTips(IEnumerable<CardKeyword> keywordTips)
    {
        if (ShouldIncludeFreezePowerTip(keywordTips))
            yield return HoverTipFactory.FromPower<ClassicFreezePower>();
    }

    internal static bool ShouldIncludeFreezePowerTip(IEnumerable<CardKeyword> keywordTips) =>
        keywordTips.Contains(SakuraKeywords.Frostbite);

    internal static IEnumerable<CardKeyword> ElementKeywords(SakuraElementSet elements) =>
        elements.AsElements().Select(SakuraActions.KeywordFor).Distinct();

    internal static HoverTip StaticTip(string key) =>
        new(
            new LocString("static_hover_tips", $"{key}.title"),
            new LocString("static_hover_tips", $"{key}.description"));

    private static IEnumerable<CardKeyword> ReferencedKeywords(CardModel card) =>
        card switch
        {
            TransparentCard transparent => transparent.ReferencedKeywords,
            SakuraOptionCard optionCard => optionCard.ReferencedKeywords,
            _ => []
        };

    private static IEnumerable<string> ReferencedStaticHoverTipKeys(CardModel card) =>
        card switch
        {
            TransparentCard transparent => transparent.ReferencedStaticHoverTipKeys,
            SakuraOptionCard optionCard => optionCard.ReferencedStaticHoverTipKeys,
            _ => []
        };
}
