using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;
using STS2RitsuLib.Content;
using STS2RitsuLib.Models.Capabilities;

namespace SakuraMod.SakuraModCode.Cards;

internal sealed class SakuraSourceCardTextCapability :
    CardCapability,
    ICardHoverTipContributor,
    ICardDescriptionContributor
{
    public static readonly string CapabilityIdValue =
        ModContentRegistry.GetQualifiedModelCapabilityId(MainFile.ModId, "CLASSIC_SAKURA_CARD_TEXT");

    public override string CapabilityId => CapabilityIdValue;
    public override bool ShouldReceiveOwnerHooks => false;

    public IEnumerable<IHoverTip> GetHoverTips(CardModel card) =>
        card is SakuraSourceCard classicCard
            ? SakuraSourceCardText.HoverTips(classicCard)
            : [];

    public IEnumerable<CardDescriptionFragment> GetDescriptionFragments(CardDescriptionContext context)
    {
        if (context.Card is ClowExtraEffectCard card && SakuraSourceCardText.ShouldShowMagicChargeExtraDescription(card))
            yield return new CardDescriptionFragment(SakuraSourceCardText.MagicChargeExtraDescription(card));

        // Fragments sort by ascending Order; the Extra line uses the default 0, so the preview sits above it.
        if (SakuraSourceCardText.BubblesBuffDescription(context.Card, context.Target) is { } buffs)
            yield return new CardDescriptionFragment(buffs, CardDescriptionFragmentPlacement.AfterBase, -100);
    }
}

internal static class SakuraSourceCardTextCapabilities
{
    public static void Register() =>
        ModContentRegistry.For(MainFile.ModId)
            .RegisterModelCapability<SakuraSourceCardTextCapability>(
                ModelPublicEntryOptions.FromStem("CLASSIC_SAKURA_CARD_TEXT"));
}

internal static class SakuraSourceCardText
{
    private const string SourceSpellTipKey = "SAKURAMOD-SPELL_CARD";

    internal static IReadOnlyList<Creature>? BubblesPreviewTargets(CardModel card, Creature? target)
    {
        if (card is not (ClowBubbles or SakuraBubbles) || !card.IsMutable || card.CombatState is not { } combat)
            return null;

        if (card is SakuraBubbles)
            return combat.HittableEnemies.ToList();

        return target is not null && combat.HittableEnemies.Contains(target) ? [target] : null;
    }

    internal static IEnumerable<PowerModel> BubblesPreviewBuffs(IEnumerable<PowerModel> powers) =>
        powers.Where(SakuraPowerRules.IsBubblesRemovableBuff).DistinctBy(power => power.GetType());

    // Always shown on both Bubbles forms; without a combat target or dispellable buff it reads "none".
    internal static LocString? BubblesBuffDescription(CardModel card, Creature? target)
    {
        if (card is not (ClowBubbles or SakuraBubbles))
            return null;

        var names = BubblesPreviewTargets(card, target) is { } targets
            ? BubblesPreviewBuffs(targets.SelectMany(creature => creature.Powers))
                .Select(power => power.Title.GetFormattedText()).ToList()
            : [];
        if (names.Count == 0)
            return new LocString("cards", "SAKURAMOD-BUBBLES.previewNone");

        var text = new LocString("cards", "SAKURAMOD-BUBBLES.preview");
        text.Add("Buffs", string.Join(new LocString("cards", "SAKURAMOD-BUBBLES.buffSeparator").GetFormattedText(), names));
        return text;
    }

    public static IEnumerable<IHoverTip> HoverTips(
        SakuraSourceCard card,
        Func<SourceCardIdentity, CardModel?>? sakuraTemplateFor = null,
        Func<Type, CardModel?>? generatedSpellTemplateFor = null)
    {
        sakuraTemplateFor ??= SakuraSourceCardRules.SakuraTemplateFor;
        generatedSpellTemplateFor ??= static type => ModelDb.GetById<CardModel>(ModelDb.GetId(type));

        var tips = new List<IHoverTip>();
        if (CounterpartPreviewIdentity(card) is { } identity)
        {
            var sakuraCard = sakuraTemplateFor(identity);
            if (sakuraCard is not null)
                tips.Add(HoverTipFactory.FromCard(sakuraCard));
        }

        if (GeneratedSpellPreviewType(card) is { } spellType)
        {
            var spellCard = generatedSpellTemplateFor(spellType);
            if (spellCard is not null)
                tips.Add(HoverTipFactory.FromCard(spellCard));
        }

        if (ReferencesThroughTip(card))
            tips.Add(HoverTipFactory.FromPower<ClassicThroughPower>());

        if (ReferencesSleepTip(card))
            tips.Add(HoverTipFactory.FromPower<ClassicSleepPower>());

        if (ReferencesBlurTip(card))
            tips.Add(HoverTipFactory.FromPower<BlurPower>());

        if (ReferencesAirborneTip(card))
            tips.Add(HoverTipFactory.FromPower<AirbornePower>());

        if (ReferencesIntangibleTip(card))
            tips.Add(HoverTipFactory.FromPower<IntangiblePower>());

        if (ReferencesBufferTip(card))
            tips.Add(HoverTipFactory.FromPower<BufferPower>());

        foreach (var key in StaticTipKeys(card))
            tips.Add(StaticTip(key));
        var keywordTips = KeywordTips(card).ToArray();
        foreach (var keyword in keywordTips)
            tips.Add(HoverTipFactory.FromKeyword(keyword));
        tips.AddRange(SakuraCardHoverTips.DependentPowerTips(keywordTips));

        return tips.Distinct();
    }

    internal static SourceCardIdentity? CounterpartPreviewIdentity(SakuraSourceCard card) =>
        card is ClowCard { Identity: { } identity }
            ? identity
            : null;

    internal static Type? GeneratedSpellPreviewType(SakuraSourceCard card) =>
        card switch
        {
            ClowEarthy => typeof(SpellLeiDi),
            ClowFirey => typeof(SpellHuoShen),
            ClowWatery => typeof(SpellShuiLong),
            ClowWindy => typeof(SpellFengHua),
            _ => null
        };

    internal static bool ReferencesThroughTip(SakuraSourceCard card) =>
        card is ClowThrough or SakuraThrough;

    internal static bool ReferencesSleepTip(SakuraSourceCard card) =>
        card is ClowSleep or SakuraSleep;

    internal static bool ReferencesBlurTip(SakuraSourceCard card) =>
        card is ClowShadow;

    internal static bool ReferencesAirborneTip(SakuraSourceCard card) =>
        card is ClowFly;

    internal static bool ReferencesIntangibleTip(SakuraSourceCard card) =>
        card is SakuraShadow;

    internal static bool ReferencesBufferTip(SakuraSourceCard card) =>
        card is ClowSilent or SakuraSilent;

    internal static IEnumerable<string> StaticTipKeys(SakuraSourceCard card)
    {
        if (card.IsSpellCard)
            yield return SourceSpellTipKey;

        switch (card)
        {
            case ClowJump:
            case SakuraJump:
                yield return SakuraCardHoverTips.DebuffTipKey;
                break;
            case ClowBubbles:
            case SakuraBubbles:
                yield return SakuraCardHoverTips.BubblesBuffTipKey;
                break;
            case ClowMist:
                yield return SakuraCardHoverTips.MistDefenseTipKey;
                break;
        }

        foreach (var element in card.Elements.AsElements())
            yield return SourceElementTipKey(card, element);

        foreach (var element in ElementStatesReferencedBy(card))
            yield return ElementStateTipKey(element);

        if (card is { IsClowCard: true, Identity: SourceCardIdentity.Lock })
            yield return "SAKURAMOD-UNREAL";
    }

    internal static IEnumerable<CardKeyword> KeywordTips(SakuraSourceCard card)
    {
        if (card is ClowFreeze or ClowSnow or SakuraSnow)
            yield return SakuraKeywords.Frostbite;

        if (card is SpellTurn)
            yield return SakuraKeywords.Purge;

        if (card.ShowsSakuraCardVoidTip)
            yield return SakuraKeywords.SakuraCard;

        if (card is { IsClowCard: true, Identity: SourceCardIdentity.Voice })
        {
            yield return SakuraKeywords.Invisible;
            yield return SakuraKeywords.Echo;
        }

        if (card.Identity is SourceCardIdentity.Return or SourceCardIdentity.Create)
            yield return SakuraKeywords.Removable;

        if (card is not { Identity: SourceCardIdentity.Create })
            yield break;

        if (!card.IsClowCard)
            yield break;

        yield return SakuraKeywords.CostDecreasing;
        yield return SakuraKeywords.EntityLimited;
    }

    internal static bool ShouldShowMagicChargeExtraDescription(ClowExtraEffectCard card) =>
        SakuraExtraEffectTransaction.ShouldShowDescription(card);

    internal static LocString MagicChargeExtraDescription(ClowExtraEffectCard card) =>
        new("cards", $"{ModelDb.GetId(card.GetType()).Entry}.extraDescription");

    internal static bool ReferencesMagicChargeTip(SakuraSourceCard card) =>
        card is GrowingMagic or AnotherMe
        || card.Identity is SourceCardIdentity.Bubbles
            or SourceCardIdentity.Change
            or SourceCardIdentity.Fight
            or SourceCardIdentity.Glow
            or SourceCardIdentity.Libra
            or SourceCardIdentity.Lock
            or SourceCardIdentity.Thunder;

    internal static IEnumerable<SakuraElement> ElementStatesReferencedBy(SakuraSourceCard card)
    {
        if (card.IsSakuraCard && card.Identity == SourceCardIdentity.Wave)
            return (SakuraElementSet.Earth | SakuraElementSet.Fire | SakuraElementSet.Water | SakuraElementSet.Wind).AsElements();

        if (!card.IsClassicSourceCard)
            return [];

        return card.Identity is SourceCardIdentity.Cloud
            or SourceCardIdentity.Flower
            or SourceCardIdentity.Earthy
            or SourceCardIdentity.Firey
            or SourceCardIdentity.Watery
            or SourceCardIdentity.Windy
            ? card.Elements.AsElements()
            : [];
    }

    private static string SourceElementTipKey(SakuraSourceCard card, SakuraElement element) =>
        card.IsSpellCard
            ? SourceElementSpellTipKey(element)
            : SourceElementCardTipKey(element);

    private static string SourceElementCardTipKey(SakuraElement element) =>
        element switch
        {
            SakuraElement.Earth => "SAKURAMOD-EARTHY_CARD",
            SakuraElement.Fire => "SAKURAMOD-FIREY_CARD",
            SakuraElement.Water => "SAKURAMOD-WATERY_CARD",
            SakuraElement.Wind => "SAKURAMOD-WINDY_CARD",
            _ => throw new ArgumentOutOfRangeException(nameof(element), element, null)
        };

    private static string SourceElementSpellTipKey(SakuraElement element) =>
        element switch
        {
            SakuraElement.Earth => "SAKURAMOD-EARTHY_SPELL_CARD",
            SakuraElement.Fire => "SAKURAMOD-FIREY_SPELL_CARD",
            SakuraElement.Water => "SAKURAMOD-WATERY_SPELL_CARD",
            SakuraElement.Wind => "SAKURAMOD-WINDY_SPELL_CARD",
            _ => throw new ArgumentOutOfRangeException(nameof(element), element, null)
        };

    internal static string ElementStateTipKey(SakuraElement element) =>
        element switch
        {
            SakuraElement.Earth => "SAKURAMOD-EARTHY_STATE",
            SakuraElement.Fire => "SAKURAMOD-FIREY_STATE",
            SakuraElement.Water => "SAKURAMOD-WATERY_STATE",
            SakuraElement.Wind => "SAKURAMOD-WINDY_STATE",
            _ => throw new ArgumentOutOfRangeException(nameof(element), element, null)
        };

    private static HoverTip StaticTip(string key) =>
        new(
            new LocString("static_hover_tips", $"{key}.title"),
            new LocString("static_hover_tips", $"{key}.description"));
}
