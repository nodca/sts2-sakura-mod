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
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Extensions;
using SakuraMod.SakuraModCode.Powers;
using STS2RitsuLib.Cards.DynamicVars;

namespace SakuraMod.SakuraModCode.Cards;

public class Blade() : TransparentExtraEffectCard(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Fire];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CalculationBaseVar(7),
        new ExtraDamageVar(2),
        new CalculatedDamageVar(ValueProp.Move).WithMultiplier(BladeRules.DamageBonusCount),
        new BladeHitsVar(2)
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        var target = RequiredTarget(play);
        // Read once, before the attack, and handed to the visuals as a resolved number.
        // The hit count is what makes the twin blades cross two or four times; they are
        // packed into a fixed envelope, so four crosses faster rather than for longer.
        var hits = BladeRules.HitCount(this);
        await SakuraSwordBladeVfx.PlayOrResolveAsync(
            this,
            Owner.Creature,
            [target],
            SwordMode.Dual,
            // Middle tier: an Uncommon costing 2 sits between the Basic attack and the
            // release token in how often it is played. Its hit count buys crossing
            // strokes, not extra weight per stroke.
            SlashWeight.Medium,
            async cues =>
            {
                // Before the attack: the freeze and the contact flash belong on the frame
                // the game's own damage numbers land.
                cues.Impact(target);
                await SakuraActions.Attack(
                    choiceContext,
                    this,
                    target,
                    DynamicVars.CalculatedDamage,
                    hitCount: hits,
                    // Same family sound as the single-blade cards, per hit.
                    hitSfx: ClowSword.StrikeSfx);
            },
            crossings: hits);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.CalculationBase.UpgradeValueBy(1);
        DynamicVars.ExtraDamage.UpgradeValueBy(2);
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

    public static decimal DamageBonusCount(CardModel card, Creature? _) =>
        DamageBonusCount(PlayedSwordOrBladeCount(card));

    internal static int DamageBonusCount(int playedSwordCount) =>
        Math.Max(0, playedSwordCount / CardsPerDamageBonus);

    public static int HitCount(CardModel card) =>
        card.DynamicVars.TryGetValue("Hits", out var hits)
            ? HitCount(card, hits.IntValue)
            : 0;

    internal static int HitCount(CardModel card, int baseHits)
    {
        var hits = baseHits;
        if (SakuraCardModel.UsesMagicChargeExtraEffect(card))
            hits += 2;
        return Math.Max(0, hits);
    }

    internal static bool CountsForDamageBonus(CardModel card) =>
        card is Blade
        || (SakuraCardCatalog.TryGetMetadata(card, out var metadata)
            && metadata.Identity == SourceCardIdentity.Sword);

    private static int PlayedSwordOrBladeCount(CardModel card)
    {
        if (card.Owner is not { } owner || card.CombatState is null)
            return 0;

        return CombatManager.Instance.History.CardPlaysFinished
            .Where(entry => entry is CardPlayFinishedEntry { CardPlay.Card.Owner: var cardOwner } && cardOwner == owner)
            .Select(entry => ((CardPlayFinishedEntry)entry).CardPlay.Card)
            .Count(CountsForDamageBonus);
    }
}
