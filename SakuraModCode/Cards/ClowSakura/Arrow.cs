using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.Relics;
using SakuraMod.SakuraModCode.Extensions;
using STS2RitsuLib.Utils;

namespace SakuraMod.SakuraModCode.Cards;

public class ClowArrow() : ClowExtraEffectCard(0, CardType.Attack, CardRarity.Common, TargetType.None)
{
    public override SakuraElementSet Elements => SakuraElementSet.Fire;
    public override TargetType TargetType =>
        IsMutable && SakuraMagicCharge.CanSpendMagic(Owner)
            ? TargetType.AnyEnemy
            : base.TargetType;
    protected override bool HasEnergyCostX => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new SakuraSourceDamageVar(5, ValueProp.Move)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (HandWithoutSelf().Count == 0 && play.Resources.EnergySpent <= 0)
            return;

        var discarded = await SelectHandCards(choiceContext, discard: true);
        var count = discarded + ResolveEnergyXValue();

        // The session opens around the volley alone. Selecting which cards to
        // discard is an unbounded player interaction, and a cel session is
        // bounded by a wall clock: wrapping the selection would let the session
        // dispose itself while the player is still choosing.
        await ArrowBowProjectileVfx.PlayOrResolveAsync(
            this,
            Owner.Creature,
            ArrowWeight.Light,
            cues => FireArrows(choiceContext, null, count, cues));
    }

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (HandWithoutSelf().Count == 0 && play.Resources.EnergySpent <= 0)
            return;

        var discarded = await SelectHandCards(choiceContext, discard: true);
        var count = discarded + ResolveEnergyXValue();
        var target = RequiredTarget(play);

        // Activated is the same volley one tier heavier, on the frequency axis
        // rather than through a second orchestration.
        await ArrowBowProjectileVfx.PlayOrResolveAsync(
            this,
            Owner.Creature,
            ArrowWeight.Medium,
            cues => FireArrows(choiceContext, target, count, cues));
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(2);

    private async Task<int> SelectHandCards(PlayerChoiceContext choiceContext, bool discard)
    {
        var hand = HandWithoutSelf();
        if (hand.Count == 0)
            return 0;

        var selected = (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, 0, hand.Count)
            {
                Cancelable = true
            },
            card => hand.Contains(card),
            this)).ToList();

        if (selected.Count > 0 && discard)
            await CardCmd.Discard(choiceContext, selected);

        return selected.Count;
    }

    private async Task FireArrows(
        PlayerChoiceContext choiceContext,
        Creature? fixedTarget,
        int count,
        ArrowBowProjectileVfx.Cues cues)
    {
        var volley = new ArrowVolley(cues);

        if (fixedTarget is not null)
        {
            await DealDamage(choiceContext, fixedTarget, ReleasedDamage(), hitCount: count, onHit: volley.Loose);
            return;
        }

        await DealDamageToRandomEnemies(choiceContext, ReleasedDamage(), count, onHit: volley.Loose);
    }

    private List<CardModel> HandWithoutSelf() =>
        CardPile.GetCards(Owner, PileType.Hand).Where(card => card != this).ToList();
}

/// <summary>
/// Counts the arrows one volley has loosed, so each hit can be scheduled
/// against the volley rather than against its own target.
/// </summary>
/// <remarks>
/// The count is presentation ordering, so it lives here and not on the card:
/// gameplay only ever reports which creature a hit struck, and only for hits the
/// engine actually landed.
/// </remarks>
internal sealed class ArrowVolley(ArrowBowProjectileVfx.Cues cues)
{
    private int _loosed;

    internal Task Loose(Creature target) => cues.Loose(target, _loosed++);
}

public class SakuraArrow() : SakuraFormCard(1, CardType.Attack, TargetType.None)
{
    public override SakuraElementSet Elements => SakuraElementSet.Fire;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new SakuraSourceDamageVar(7, ValueProp.Move)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var drawPile = CardPile.GetCards(Owner, PileType.Draw).ToList();
        var count = CountDistinctCardTypes(drawPile);

        if (drawPile.Count > 0)
            await CardCmd.Discard(choiceContext, drawPile);

        // The release form is the richest tier by the frequency axis, not by a
        // longer orchestration.
        await ArrowBowProjectileVfx.PlayOrResolveAsync(
            this,
            Owner.Creature,
            ArrowWeight.Heavy,
            async cues =>
            {
                var volley = new ArrowVolley(cues);
                await DealDamageToRandomEnemies(choiceContext, ReleasedDamage(), count, onHit: volley.Loose);
            });
    }

    internal static int CountDistinctCardTypes(IEnumerable<CardModel> cards) =>
        cards.Select(static card => card.GetType()).Distinct().Count();
}

