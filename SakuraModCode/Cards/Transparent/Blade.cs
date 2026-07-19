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

public class Blade() : TransparentExtraEffectCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Fire];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CalculationBaseVar(4),
        new ExtraDamageVar(2),
        new CalculatedDamageVar(ValueProp.Move).WithMultiplier(BladeRules.DamageBonusCount),
        new BladeHitsVar(2)
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        var target = RequiredTarget(play);
        var hits = BladeRules.HitCount(this);
        await SakuraActions.Attack(choiceContext, this, target, DynamicVars.CalculatedDamage, hitCount: hits);
    }

    protected override void OnUpgrade() => DynamicVars.CalculationBase.UpgradeValueBy(1);
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

