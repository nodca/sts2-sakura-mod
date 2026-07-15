using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.Cards;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using STS2RitsuLib.Content;
using STS2RitsuLib.Models.Capabilities;

namespace SakuraMod.SakuraModCode.Classic.Cards;

internal sealed class ClassicSakuraCardTextCapability :
    CardCapability,
    ICardHoverTipContributor,
    ICardDescriptionContributor
{
    public static readonly string CapabilityIdValue =
        ModContentRegistry.GetQualifiedModelCapabilityId(MainFile.ModId, "CLASSIC_SAKURA_CARD_TEXT");

    public override string CapabilityId => CapabilityIdValue;
    public override bool ShouldReceiveOwnerHooks => false;

    public IEnumerable<IHoverTip> GetHoverTips(CardModel card) =>
        card is ClassicSakuraCard classicCard
            ? ClassicSakuraCardText.HoverTips(classicCard)
            : [];

    public IEnumerable<CardDescriptionFragment> GetDescriptionFragments(CardDescriptionContext context) =>
        context.Card is ClassicExtraClowCard card && ClassicSakuraCardText.ShouldShowMagicChargeExtraDescription(card)
            ? [new CardDescriptionFragment(ClassicSakuraCardText.MagicChargeExtraDescription(card))]
            : [];
}

internal static class ClassicSakuraCardTextCapabilities
{
    public static void Register() =>
        ModContentRegistry.For(MainFile.ModId)
            .RegisterModelCapability<ClassicSakuraCardTextCapability>(
                ModelPublicEntryOptions.FromStem("CLASSIC_SAKURA_CARD_TEXT"));
}

internal static class ClassicSakuraCardText
{
    private const string ClassicSpellTipKey = "SAKURAMOD-CLASSIC_SPELL_CARD";

    public static IEnumerable<IHoverTip> HoverTips(
        ClassicSakuraCard card,
        Func<SourceCardIdentity, CardModel?>? sakuraTemplateFor = null)
    {
        sakuraTemplateFor ??= ClassicSakuraCardCatalog.SakuraTemplateFor;

        var tips = new List<IHoverTip>();
        if (CounterpartPreviewIdentity(card) is { } identity)
        {
            var sakuraCard = sakuraTemplateFor(identity);
            if (sakuraCard is not null)
                tips.Add(HoverTipFactory.FromCard(sakuraCard));
        }

        foreach (var key in StaticTipKeys(card))
            tips.Add(StaticTip(key));
        foreach (var keyword in KeywordTips(card))
            tips.Add(HoverTipFactory.FromKeyword(keyword));

        return tips.Distinct();
    }

    internal static SourceCardIdentity? CounterpartPreviewIdentity(ClassicSakuraCard card) =>
        card is ClassicClowCard { Identity: { } identity }
            ? identity
            : null;

    internal static IEnumerable<string> StaticTipKeys(ClassicSakuraCard card)
    {
        if (card.IsSpellCard)
            yield return ClassicSpellTipKey;

        foreach (var element in card.Element.AsElements())
            yield return ClassicElementTipKey(card, element);

        foreach (var element in ElementStatesReferencedBy(card))
            yield return ClassicElementStateTipKey(element);

        if (card is { IsClowCard: true, Identity: SourceCardIdentity.Lock })
            yield return "SAKURAMOD-CLASSIC_UNREAL";
    }

    internal static IEnumerable<CardKeyword> KeywordTips(ClassicSakuraCard card)
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

    internal static bool ShouldShowMagicChargeExtraDescription(ClassicExtraClowCard card) =>
        card.IsMutable && SakuraExtraEffectTransaction.CanActivate(card.Owner);

    internal static LocString MagicChargeExtraDescription(ClassicExtraClowCard card) =>
        new("cards", $"{ModelDb.GetId(card.GetType()).Entry}.extraDescription");

    internal static bool ReferencesMagicChargeTip(ClassicSakuraCard card) =>
        card is SakuraLegacy
        || card.Identity is SourceCardIdentity.Bubbles
            or SourceCardIdentity.Fight
            or SourceCardIdentity.Glow
            or SourceCardIdentity.Libra
            or SourceCardIdentity.Lock
            or SourceCardIdentity.Thunder;

    internal static IEnumerable<ClassicElement> ElementStatesReferencedBy(ClassicSakuraCard card)
    {
        if (card.IsSakuraCard && card.Identity == SourceCardIdentity.Wave)
            return (ClassicElement.Earthy | ClassicElement.Firey | ClassicElement.Watery | ClassicElement.Windy).AsElements();

        if (!card.IsClassicSourceCard)
            return [];

        return card.Identity is SourceCardIdentity.Cloud
            or SourceCardIdentity.Earthy
            or SourceCardIdentity.Firey
            or SourceCardIdentity.Watery
            or SourceCardIdentity.Windy
            ? card.Element.AsElements()
            : [];
    }

    private static string ClassicElementTipKey(ClassicSakuraCard card, ClassicElement element) =>
        card.IsSpellCard
            ? ClassicElementSpellTipKey(element)
            : ClassicElementCardTipKey(element);

    private static string ClassicElementCardTipKey(ClassicElement element) =>
        element switch
        {
            ClassicElement.Earthy => "SAKURAMOD-CLASSIC_EARTHY_CARD",
            ClassicElement.Firey => "SAKURAMOD-CLASSIC_FIREY_CARD",
            ClassicElement.Watery => "SAKURAMOD-CLASSIC_WATERY_CARD",
            ClassicElement.Windy => "SAKURAMOD-CLASSIC_WINDY_CARD",
            _ => throw new ArgumentOutOfRangeException(nameof(element), element, null)
        };

    private static string ClassicElementSpellTipKey(ClassicElement element) =>
        element switch
        {
            ClassicElement.Earthy => "SAKURAMOD-CLASSIC_EARTHY_SPELL_CARD",
            ClassicElement.Firey => "SAKURAMOD-CLASSIC_FIREY_SPELL_CARD",
            ClassicElement.Watery => "SAKURAMOD-CLASSIC_WATERY_SPELL_CARD",
            ClassicElement.Windy => "SAKURAMOD-CLASSIC_WINDY_SPELL_CARD",
            _ => throw new ArgumentOutOfRangeException(nameof(element), element, null)
        };

    internal static string ClassicElementStateTipKey(ClassicElement element) =>
        element switch
        {
            ClassicElement.Earthy => "SAKURAMOD-CLASSIC_EARTHY_STATE",
            ClassicElement.Firey => "SAKURAMOD-CLASSIC_FIREY_STATE",
            ClassicElement.Watery => "SAKURAMOD-CLASSIC_WATERY_STATE",
            ClassicElement.Windy => "SAKURAMOD-CLASSIC_WINDY_STATE",
            _ => throw new ArgumentOutOfRangeException(nameof(element), element, null)
        };

    private static HoverTip StaticTip(string key) =>
        new(
            new LocString("static_hover_tips", $"{key}.title"),
            new LocString("static_hover_tips", $"{key}.description"));
}
