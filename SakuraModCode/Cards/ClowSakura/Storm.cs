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

public class ClowStorm() : ClowExtraEffectCard(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    private const int Hits = 5;

    public override SakuraElementSet Elements => SakuraElementSet.Wind;
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CalculationBaseVar(4),
        new ExtraDamageVar(1),
        new CalculatedDamageVar(ValueProp.Move).WithMultiplier(StormRules.WindCardsPlayedThisTurn),
        new DynamicVar("Magic", Hits)
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = RequiredTarget(play);
        await StormCycloneVfx.PlayFunnelAsync(
            this,
            Owner.Creature,
            [target],
            StormRules.WindCardsPlayedThisTurnCount(this),
            wholeLine: false,
            async cues => await DealDamage(
                choiceContext,
                play,
                target,
                CalculatedDamage(target),
                hitCount: ReleasedMagic(),
                onHit: hit =>
                {
                    cues.Hit(hit);
                    return Task.CompletedTask;
                },
                hitVfx: cues.FallbackHitVfx));
    }

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var targets = CombatState!.HittableEnemies.ToList();
        await StormCycloneVfx.PlayFunnelAsync(
            this,
            Owner.Creature,
            targets,
            StormRules.WindCardsPlayedThisTurnCount(this),
            wholeLine: true,
            async cues => await DealDamageToEnemies(
                choiceContext,
                play,
                targets,
                CalculatedDamage(null),
                hitCount: ReleasedMagic(),
                hitVfx: cues.FallbackHitVfx,
                // A pure observer: the factory reports each creature a hit is
                // about to strike and contributes no node.
                hitVfxNode: hit =>
                {
                    cues.Hit(hit);
                    return null;
                }));
    }

    private int CalculatedDamage(Creature? target) =>
        Math.Max(0, (int)DynamicVars.CalculatedDamage.Calculate(target));

    protected override void OnUpgrade() => DynamicVars.CalculationBase.UpgradeValueBy(1);
}

internal static class StormRules
{
    public static decimal WindCardsPlayedThisTurn(CardModel card, Creature? _) =>
        WindCardsPlayedThisTurnCount(card);

    internal static int WindCardsPlayedThisTurnCount(CardModel card)
    {
        if (card.Owner is not { } owner || card.CombatState is null)
            return 0;

        return CombatManager.Instance.History.CardPlaysFinished
            .Where(entry => entry.CardPlay.Card.Owner == owner && entry.HappenedThisTurn(card.CombatState))
            .Select(entry => entry.CardPlay.Card)
            .Count(CountsAsWindCard);
    }

    internal static bool CountsAsWindCard(CardModel card) =>
        SakuraActions.HasElement(card, SakuraElement.Wind);
}

public class SakuraStorm() : SakuraFormCard(1, CardType.Attack, TargetType.None)
{
    private const int MinDamage = 1;
    private const int MaxDamageOffset = 8;

    internal const string MaxDamageVar = "MaxDamage";

    public override SakuraElementSet Elements => SakuraElementSet.Wind;

    // Both ends are damage vars so Strength, Weak, Vulnerable, and enchantments move
    // the displayed range together; the roll below stays on the unmodified base values
    // because the damage pipeline applies those hooks to every hit.
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new SakuraSourceDamageVar(MinDamage, ValueProp.Move),
        new SakuraSourceDamageVar(MaxDamageVar, MinDamage + MaxDamageOffset, ValueProp.Move),
        new DynamicVar("Magic", 7)
    ];

    protected override Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        StormCycloneVfx.PlayVortexAsync(
            this,
            Owner.Creature,
            CombatState!.HittableEnemies.ToList(),
            async cues =>
            {
                await using var attack = await AttackCommand.CreateContextAsync(CombatState!, choiceContext, play);
                for (var i = 0; i < ReleasedMagic(); i++)
                {
                    var target = Owner.RunState.Rng.CombatCardSelection.NextItem(CombatState!.HittableEnemies.ToList());
                    if (target is null)
                        return;

                    var min = ReleasedDamage();
                    var max = ReleasedValue(MaxDamageVar);
                    var amount = Owner.RunState.Rng.CombatCardSelection.NextInt(min, max + 1);
                    cues.Strike(target, StormCycloneVfx.Strength(amount, min, max));
                    await DealDamageHit(attack, choiceContext, play, target, amount, hitVfx: cues.FallbackHitVfx);
                }
            });
}
