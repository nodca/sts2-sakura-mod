using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.Cards;
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

    public IEnumerable<CardDescriptionFragment> GetDescriptionFragments(CardDescriptionContext context) =>
        context.Card is ClowExtraEffectCard card && SakuraSourceCardText.ShouldShowMagicChargeExtraDescription(card)
            ? [new CardDescriptionFragment(SakuraSourceCardText.MagicChargeExtraDescription(card))]
            : [];
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

    public static IEnumerable<IHoverTip> HoverTips(
        SakuraSourceCard card,
        Func<SourceCardIdentity, CardModel?>? sakuraTemplateFor = null)
    {
        sakuraTemplateFor ??= SakuraSourceCardRules.SakuraTemplateFor;

        var tips = new List<IHoverTip>();
        if (CounterpartPreviewIdentity(card) is { } identity)
        {
            var sakuraCard = sakuraTemplateFor(identity);
            if (sakuraCard is not null)
                tips.Add(HoverTipFactory.FromCard(sakuraCard));
        }

        if (ReferencesThroughTip(card))
            tips.Add(HoverTipFactory.FromPower<ClassicThroughPower>());

        foreach (var key in StaticTipKeys(card))
            tips.Add(StaticTip(key));
        foreach (var keyword in KeywordTips(card))
            tips.Add(HoverTipFactory.FromKeyword(keyword));

        return tips.Distinct();
    }

    internal static SourceCardIdentity? CounterpartPreviewIdentity(SakuraSourceCard card) =>
        card is ClowCard { Identity: { } identity }
            ? identity
            : null;

    internal static bool ReferencesThroughTip(SakuraSourceCard card) =>
        card is ClowThrough or SakuraThrough;

    internal static IEnumerable<string> StaticTipKeys(SakuraSourceCard card)
    {
        if (card.IsSpellCard)
            yield return SourceSpellTipKey;

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

        if (card.ShowsSakuraCardVoidTip)
            yield return SakuraKeywords.SakuraCard;

        if (card is { IsClowCard: true, Identity: SourceCardIdentity.Voice })
        {
            yield return SakuraKeywords.Invisible;
            yield return SakuraKeywords.Echo;
        }

        if (card is not { Identity: SourceCardIdentity.Create })
            yield break;

        yield return SakuraKeywords.Removable;
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
