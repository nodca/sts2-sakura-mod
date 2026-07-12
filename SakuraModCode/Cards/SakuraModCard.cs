using Godot;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Classic.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Content;
using STS2RitsuLib.Models.Capabilities;
using STS2RitsuLib.Scaffolding.Content;

namespace SakuraMod.SakuraModCode.Cards;

public interface IExtraEffectCard
{
    Task OnExtraEffect(PlayerChoiceContext choiceContext, CardPlay play);
}

public abstract class SakuraModCard : ModCardTemplate
{
    private bool _usingExtraEffect;

    protected SakuraModCard(int cost, CardType type, CardRarity rarity, TargetType target) :
        base(cost, type, rarity, target)
    {
    }

    protected static LocString CardLoc<TCard>(string suffix) where TCard : CardModel =>
        new("cards", $"{ModelDb.GetId(typeof(TCard)).Entry}.{suffix}");

    //Image size:
    //Normal art: 1000x760 (Using 500x380 should also work, it will simply be scaled.)
    //Full art: 606x852
    public override string CustomPortraitPath =>
        UsesClearCardPortrait
            ? ClearCardPortraitPath
            : SakuraCardFrameVisuals.BigPortraitPath(this);

    //Smaller variants of card images for efficiency:
    //Smaller variant of fullart: 250x350
    //Smaller variant of normalart: 250x190

    //Uses card_portraits/card_name.png as image path. These should be smaller images.
    public override string PortraitPath =>
        UsesClearCardPortrait
            ? ClearCardPortraitPath
            : SakuraCardFrameVisuals.PortraitPath(this);
    public override string BetaPortraitPath => PortraitPath;
    public override Material? CustomFrameMaterial => SakuraCardFrameVisuals.CustomFrameMaterial(this);
    protected override IEnumerable<string> ExtraRunAssetPaths => SakuraCardFrameVisuals.RunAssetPaths(this);

    internal virtual IEnumerable<CardKeyword> ReferencedKeywords => [];
    internal virtual IEnumerable<string> ReferencedStaticHoverTipKeys => [];
    protected virtual bool HasExtraEffect => false;

    protected bool IsUsingExtraEffect => _usingExtraEffect;

    internal static bool UsesMagicChargeExtraEffect(CardModel? card) =>
        card is SakuraModCard sakuraCard
        && SakuraCardCatalog.IsTransparentCard(sakuraCard)
        && sakuraCard.HasExtraEffect
        && (sakuraCard._usingExtraEffect || ShouldShowMagicChargeExtraEffectDescription(sakuraCard));

    internal static bool ShouldShowMagicChargeExtraEffectDescription(CardModel card) =>
        card is SakuraModCard sakuraCard
        && SakuraCardCatalog.IsTransparentCard(sakuraCard)
        && sakuraCard.HasExtraEffect
        && (sakuraCard._usingExtraEffect
            || sakuraCard.IsMutable && ClassicSakuraMagic.CanUseExtraEffect(sakuraCard.Owner));

    internal static bool HasMagicChargeExtraEffect(CardModel? card) =>
        card is SakuraModCard sakuraCard
        && SakuraCardCatalog.IsTransparentCard(sakuraCard)
        && sakuraCard.HasExtraEffect;

    private bool UsesClearCardPortrait => SakuraCardVisualFamilies.UsesClearLayout(this);

    private string ClearCardPortraitPath => ClearCardLayout.CardArtPath(GetType());

    public override Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        if (player == Owner)
            SakuraCardStates.ResetTemporaryCleanupForTurn(player);

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await ApplyClassicElementStateForExtraEffect(choiceContext, play);
        await GainMagicChargeForTransparentCard(choiceContext, play);
        ResetExtraEffect(play);
    }

    protected static Creature RequiredTarget(CardPlay play) =>
        play.Target ?? throw new InvalidOperationException("Card target is required by this card's TargetType.");

    public override async Task BeforeCardPlayed(CardPlay play)
    {
        _usingExtraEffect = play.Card == this
                            && SakuraCardCatalog.IsTransparentCard(this)
                            && HasExtraEffect
                            && ClassicSakuraMagic.CanUseExtraEffect(Owner);

        if (!_usingExtraEffect)
            return;

        await ClassicSakuraMagic.SpendForExtraEffect(new ThrowingPlayerChoiceContext(), Owner);
        await SakuraActions.RecordExtraEffectTriggeredThisTurn(new ThrowingPlayerChoiceContext(), play);
    }

    protected async Task TriggerExtraEffect(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (IsUsingExtraEffect && this is IExtraEffectCard extraEffectCard)
            await extraEffectCard.OnExtraEffect(choiceContext, play);
    }

    protected async Task TriggerExtraEffect(Func<Task> extraEffect)
    {
        if (IsUsingExtraEffect)
            await extraEffect();
    }

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

    private async Task GainMagicChargeForTransparentCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (play.Card == this && SakuraCardCatalog.IsTransparentCard(this))
            await ClassicSakuraMagic.GainMagic(choiceContext, this);
    }

    private async Task ApplyClassicElementStateForExtraEffect(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (play.Card == this && SakuraActions.DidTriggerExtraEffect(play))
            await SakuraActions.ApplyClassicElementStatesForTransparentCard(choiceContext, this);
    }

    private void ResetExtraEffect(CardPlay play)
    {
        if (play.Card == this)
            _usingExtraEffect = false;
    }

}

internal sealed class SakuraCardHoverTipCapability : CardCapability, ICardHoverTipContributor
{
    public static readonly string CapabilityIdValue =
        ModContentRegistry.GetQualifiedModelCapabilityId(MainFile.ModId, "REFERENCED_KEYWORD_HOVER_TIPS");

    public override string CapabilityId => CapabilityIdValue;
    public override bool ShouldReceiveOwnerHooks => false;

    public IEnumerable<IHoverTip> GetHoverTips(CardModel card)
    {
        if (card is not SakuraModCard sakuraCard)
            return [];

        return SakuraCardHoverTips.HoverTips(sakuraCard);
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
    internal const string StrongReflectionTipKey = "SAKURAMOD-STRONG_REFLECTION";
    internal const string LabyrinthTipKey = "SAKURAMOD-ENTER_LABYRINTH";

    internal static IEnumerable<IHoverTip> HoverTips(SakuraModCard card) =>
        KeywordTips(card).Select(HoverTipFactory.FromKeyword)
            .Concat(StaticTipKeys(card).Select(key => (IHoverTip)StaticTip(key)))
            .Distinct();

    internal static IEnumerable<CardKeyword> KeywordTips(SakuraModCard card) =>
        card.ReferencedKeywords
            .Concat(ElementKeywords(SakuraActions.StaticElementSetOf(card)))
            .Distinct();

    internal static IEnumerable<string> StaticTipKeys(SakuraModCard card) =>
        card.ReferencedStaticHoverTipKeys.Distinct();

    internal static IEnumerable<CardKeyword> ElementKeywords(SakuraElementSet elements) =>
        elements.AsElements().Select(SakuraActions.KeywordFor).Distinct();

    internal static HoverTip StaticTip(string key) =>
        new(
            new LocString("static_hover_tips", $"{key}.title"),
            new LocString("static_hover_tips", $"{key}.description"));
}
