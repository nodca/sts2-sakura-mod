using Godot;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
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
    public override Material? CustomFrameMaterial => SakuraCardFrameVisuals.CustomFrameMaterial(this);
    protected override IEnumerable<string> ExtraRunAssetPaths => SakuraCardFrameVisuals.RunAssetPaths(this);

    internal virtual IEnumerable<CardKeyword> ReferencedKeywords => [];
    internal virtual IEnumerable<string> ReferencedStaticHoverTipKeys => [];
    internal static bool UsesMagicChargeExtraEffect(CardModel? card) =>
        card is SakuraCardModel sakuraCard
        && SakuraTransparentCardCatalog.IsTransparentCard(sakuraCard)
        && SakuraExtraEffectTransaction.Supports(sakuraCard)
        && SakuraExtraEffectTransaction.ShouldShowAsActive(sakuraCard);

    internal static bool ShouldShowMagicChargeExtraEffectDescription(CardModel card) =>
        card is SakuraCardModel sakuraCard
        && SakuraTransparentCardCatalog.IsTransparentCard(sakuraCard)
        && SakuraExtraEffectTransaction.ShouldShowDescription(sakuraCard);

    internal static bool HasMagicChargeExtraEffect(CardModel? card) =>
        card is SakuraCardModel sakuraCard
        && SakuraTransparentCardCatalog.IsTransparentCard(sakuraCard)
        && SakuraExtraEffectTransaction.Supports(sakuraCard);

    public override Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        if (player == Owner)
            SakuraCardStates.ResetTemporaryCleanupForTurn(player);

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await SakuraExtraEffectTransaction.AfterCardPlayed(this, choiceContext, play);

        if (play.Card == this && play.IsLastInSeries)
            SakuraReleaseState.Reset(this);
    }

    public override Task AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)
    {
        if (card == this)
            SakuraReleaseState.Reset(this);

        return Task.CompletedTask;
    }

    protected static Creature RequiredTarget(CardPlay play) =>
        play.Target ?? throw new InvalidOperationException("Card target is required by this card's TargetType.");

    protected sealed override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play) =>
        SakuraExtraEffectTransaction.Execute(
            this,
            choiceContext,
            play,
            (context, currentPlay) => PlayCard(context, currentPlay, default));

    protected virtual Task PlayCard(
        PlayerChoiceContext choiceContext,
        CardPlay play,
        SakuraExtraEffectActivation activation) =>
        Task.CompletedTask;

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
    SakuraCardModel(cost, type, rarity, target);

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
        if (card is not SakuraCardModel && card is not SakuraOptionCard)
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
            SakuraCardModel sakuraCard => sakuraCard.ReferencedKeywords,
            SakuraOptionCard optionCard => optionCard.ReferencedKeywords,
            _ => []
        };

    private static IEnumerable<string> ReferencedStaticHoverTipKeys(CardModel card) =>
        card switch
        {
            SakuraCardModel sakuraCard => sakuraCard.ReferencedStaticHoverTipKeys,
            SakuraOptionCard optionCard => optionCard.ReferencedStaticHoverTipKeys,
            _ => []
        };
}
