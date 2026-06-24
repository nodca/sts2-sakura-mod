using BaseLib.Abstracts;
using BaseLib.Extensions;
using BaseLib.Utils;
using Godot;
using SakuraMod.SakuraModCode.Character;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace SakuraMod.SakuraModCode.Cards;

public interface IReleaseable
{
    Task OnReleased(PlayerChoiceContext choiceContext, CardPlay play);
}

public interface IReleaseStateObserver
{
    void OnReleaseStateChanged();
}

[Pool(typeof(SakuraModCardPool))]
public abstract class SakuraModCard(int cost, CardType type, CardRarity rarity, TargetType target) :
    CustomCardModel(cost, type, rarity, target)
{
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
    public override Texture2D? CustomFrame => SakuraCardFrameVisuals.CustomFrameTexture(this);
    public override Material? CreateCustomFrameMaterial => SakuraCardFrameVisuals.CustomFrameMaterial(this);
    protected override IEnumerable<string> ExtraRunAssetPaths => SakuraCardFrameVisuals.RunAssetPaths(this);

    protected bool ShouldRelease => this.IsReleased();

    private bool UsesClearCardPortrait => SakuraCardVisualFamilies.IsClear(this);

    private string ClearCardPortraitPath => ClearCardLayout.CardArtPath(GetType());

    public override Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        if (player == Owner)
            SakuraActions.BeginPlayerTurn(player, combatState);

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await SakuraManifestLoop.RememberCatalogCard(choiceContext, play);
        await SakuraActions.RememberPlayedElements(choiceContext, play);
    }

    public override Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner.Creature.Side == side && participants.Contains(Owner.Creature))
            SakuraActions.EndPlayerTurn(Owner);

        return Task.CompletedTask;
    }

    protected static Creature RequiredTarget(CardPlay play) =>
        play.Target ?? throw new InvalidOperationException("Card target is required by this card's TargetType.");

    protected async Task TriggerReleaseEffect(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (ShouldRelease && this is IReleaseable releaseable)
            await releaseable.OnReleased(choiceContext, play);
    }

    protected async Task TriggerReleaseEffect(Func<Task> releaseEffect)
    {
        if (ShouldRelease)
            await releaseEffect();
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
}
