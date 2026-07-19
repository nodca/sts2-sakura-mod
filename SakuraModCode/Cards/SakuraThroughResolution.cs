using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Powers;

namespace SakuraMod.SakuraModCode.Cards;

internal sealed record ThroughPlayScope(
    CardModel Card,
    CardPlay ActivePlay,
    Creature PrimaryTarget,
    IReadOnlyList<Creature> Targets,
    int MagicCharge,
    int BonusDamage)
{
    internal IEnumerable<Creature> SecondaryTargets => Targets.Skip(1);

    internal bool Contains(Creature target) => Targets.Contains(target);

    internal ThroughPlayScope WithActivePlay(CardPlay play) => this with { ActivePlay = play };

    internal ThroughPlayScope WithoutActivePlay() => this with
    {
        ActivePlay = new CardPlay
        {
            Card = Card,
            Target = null,
            ResultPile = ActivePlay.ResultPile,
            Resources = ActivePlay.Resources,
            IsAutoPlay = ActivePlay.IsAutoPlay,
            PlayIndex = -1,
            PlayCount = ActivePlay.PlayCount
        }
    };

    internal bool IsActive => ActivePlay.PlayIndex >= 0;
}

internal static class SakuraThroughResolution
{
    private static readonly AsyncLocal<int> SuppressionDepth = new();

    internal static bool IsPropagationSuppressed => SuppressionDepth.Value > 0;

    internal static bool IsEligibleCard(CardModel card) =>
        card.TargetType == TargetType.AnyEnemy
        && (card is TransparentCard
            || card is SpellCard
            || card is SakuraAncientCard
            || card is SakuraSourceCard source && (source.IsClowCard || source.IsSakuraCard));

    internal static ThroughPlayScope? TryCreate(CardPlay play, int magicCharge, int bonusDamage)
    {
        var target = play.Target;
        var combat = play.Card.CombatState;
        if (target is null || combat is null || target.Side == play.Card.Owner.Creature.Side)
            return null;

        var enemies = combat.Enemies;
        var targetIndex = -1;
        for (var i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] == target)
            {
                targetIndex = i;
                break;
            }
        }
        if (targetIndex < 0)
            return null;

        var targets = enemies
            .Skip(targetIndex)
            .Where(IsStillValid)
            .Distinct()
            .ToList();
        if (targets.Count == 0 || targets[0] != target)
            return null;

        return new ThroughPlayScope(play.Card, play, target, targets, magicCharge, bonusDamage);
    }

    internal static bool IsStillValid(Creature creature) => creature.IsAlive;

    internal static IReadOnlyList<Creature> TargetsFor(CardPlay play)
    {
        var target = play.Target;
        if (target is null)
            return [];

        var scope = play.Card.Owner?.Creature.GetPower<ClassicThroughPower>()?.ActiveScope;
        return scope is { IsActive: true }
               && scope.Card == play.Card
               && scope.PrimaryTarget == target
            ? scope.Targets.Where(IsStillValid).ToList()
            : [target];
    }

    internal static IReadOnlyList<Creature>? ExpandDamageTargets(
        IEnumerable<Creature> originalTargets,
        CardModel? cardSource)
    {
        if (IsPropagationSuppressed || cardSource?.Owner?.Creature.GetPower<ClassicThroughPower>()?.ActiveScope is not { IsActive: true } scope)
            return null;

        var targets = originalTargets as IReadOnlyList<Creature> ?? originalTargets.ToList();
        if (targets.Count != 1 || targets[0] != scope.PrimaryTarget)
            return null;

        return scope.Targets.Where(IsStillValid).ToList();
    }

    internal static async Task WithPropagationSuppressed(Func<Task> action)
    {
        SuppressionDepth.Value++;
        try
        {
            await action();
        }
        finally
        {
            SuppressionDepth.Value--;
        }
    }

    internal static async Task<T> WithPropagationSuppressed<T>(Func<Task<T>> action)
    {
        SuppressionDepth.Value++;
        try
        {
            return await action();
        }
        finally
        {
            SuppressionDepth.Value--;
        }
    }

    internal static Task? TryExpandNewPower(
        MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext choiceContext,
        PowerModel power,
        Creature target,
        decimal amount,
        Creature? applier,
        CardModel? cardSource,
        bool silent)
    {
        if (!TryGetPowerScope(target, cardSource, power, out var scope))
            return null;

        return WithPropagationSuppressed(async () =>
        {
            await PowerCmd.Apply(choiceContext, power, target, amount, applier, cardSource, silent);
            foreach (var secondary in scope.SecondaryTargets.Where(IsStillValid))
                await ApplyPowerToSecondary(choiceContext, power, secondary, amount, applier, cardSource!, silent);
        });
    }

    internal static Task<int>? TryExpandExistingPower(
        MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource,
        bool silent)
    {
        if (!TryGetPowerScope(power.Owner, cardSource, power, out var scope))
            return null;

        return WithPropagationSuppressed(async () =>
        {
            var primaryAmount = await PowerCmd.ModifyAmount(choiceContext, power, amount, applier, cardSource, silent);
            foreach (var secondary in scope.SecondaryTargets.Where(IsStillValid))
                await ApplyPowerToSecondary(choiceContext, power, secondary, amount, applier, cardSource!, silent);
            return primaryAmount;
        });
    }

    private static bool TryGetPowerScope(
        Creature target,
        CardModel? cardSource,
        PowerModel power,
        out ThroughPlayScope scope)
    {
        scope = null!;
        if (IsPropagationSuppressed
            || cardSource?.Owner?.Creature.GetPower<ClassicThroughPower>()?.ActiveScope is not { IsActive: true } active
            || active.Card != cardSource
            || active.PrimaryTarget != target)
            return false;

        scope = active;
        return true;
    }

    private static async Task ApplyPowerToSecondary(
        MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext choiceContext,
        PowerModel sourcePower,
        Creature target,
        decimal originalAmount,
        Creature? applier,
        CardModel cardSource,
        bool silent)
    {
        var canonical = ModelDb.DebugPower(sourcePower.GetType());
        var existing = PowerCmd.FindExistingInstanceForStacking(canonical, target, applier);
        if (existing is null)
            await PowerCmd.Apply(choiceContext, canonical.ToMutable(), target, originalAmount, applier, cardSource, silent);
        else
            await PowerCmd.ModifyAmount(choiceContext, existing, originalAmount, applier, cardSource, silent);
    }
}

[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.Damage),
    [typeof(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext), typeof(IEnumerable<Creature>), typeof(decimal), typeof(ValueProp), typeof(Creature), typeof(CardModel)])]
internal static class SakuraThroughDamageTargetsPatch
{
    private static void Prefix(ref IEnumerable<Creature> targets, CardModel? cardSource)
    {
        if (SakuraThroughResolution.ExpandDamageTargets(targets, cardSource) is { } expanded)
            targets = expanded;
    }
}

[HarmonyPatch(typeof(PowerCmd), nameof(PowerCmd.Apply),
    [typeof(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext), typeof(PowerModel), typeof(Creature), typeof(decimal), typeof(Creature), typeof(CardModel), typeof(bool)])]
internal static class SakuraThroughNewPowerTargetsPatch
{
    private static bool Prefix(
        MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext choiceContext,
        PowerModel power,
        Creature target,
        decimal amount,
        Creature? applier,
        CardModel? cardSource,
        bool silent,
        ref Task __result)
    {
        var expanded = SakuraThroughResolution.TryExpandNewPower(choiceContext, power, target, amount, applier, cardSource, silent);
        if (expanded is null)
            return true;

        __result = expanded;
        return false;
    }
}

[HarmonyPatch(typeof(PowerCmd), nameof(PowerCmd.ModifyAmount),
    [typeof(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext), typeof(PowerModel), typeof(decimal), typeof(Creature), typeof(CardModel), typeof(bool)])]
internal static class SakuraThroughExistingPowerTargetsPatch
{
    private static bool Prefix(
        MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal offset,
        Creature? applier,
        CardModel? cardSource,
        bool silent,
        ref Task<int> __result)
    {
        var expanded = SakuraThroughResolution.TryExpandExistingPower(choiceContext, power, offset, applier, cardSource, silent);
        if (expanded is null)
            return true;

        __result = expanded;
        return false;
    }
}
