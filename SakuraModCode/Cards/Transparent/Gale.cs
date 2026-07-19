using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Extensions;

namespace SakuraMod.SakuraModCode.Cards;

public class Gale() : TransparentExtraEffectCard(0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Wind];
    internal override IEnumerable<string> ReferencedStaticHoverTipKeys =>
        [SakuraCardHoverTips.TemporaryTipKey];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(6, ValueProp.Move),
        new CardsVar("Cards", 2),
        new CardsVar("ExtraCopies", 2)
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        var target = RequiredTarget(play);
        await SakuraActions.AttackCommand(this, target, DynamicVars.Damage.IntValue, DynamicVars.Damage.Props)
            .WithHitVfxNode(target => SakuraCardPlayVfx.CreateGaleWindBlade(Owner.Creature, target))
            .Execute(choiceContext);

        if (activation.IsActive)
            await ApplyExtraEffect(choiceContext);
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await base.AfterCardPlayed(choiceContext, play);

        if (play.Card != this || !GaleRules.ShouldDrawAfterPlay(GaleRules.PlayedCount(Owner)))
            return;

        await CardPileCmd.Draw(choiceContext, DynamicVars["Cards"].IntValue, Owner, false);
    }

    private async Task ApplyExtraEffect(PlayerChoiceContext choiceContext)
    {
        for (var i = 0; i < DynamicVars["ExtraCopies"].IntValue; i++)
        {
            await SakuraGeneratedCardLifecycle.AddTemporaryCopyToHand(
                this,
                freeThisTurn: false,
                context: choiceContext);
        }
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
}

internal static class GaleRules
{
    private const int PlaysPerDraw = 3;

    internal static int PlayedCount(Player owner) =>
        SakuraCombatHistory.PlayedCardsThisCombat(owner, CountsAsGale);

    internal static bool CountsAsGale(CardModel card) => card is Gale;

    internal static bool ShouldDrawAfterPlay(int playedCount) =>
        playedCount > 0 && playedCount % PlaysPerDraw == 0;
}
