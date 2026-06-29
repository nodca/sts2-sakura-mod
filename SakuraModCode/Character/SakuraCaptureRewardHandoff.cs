using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Rewards;
using BaseLib.Utils;
using SakuraMod.SakuraModCode.Cards;
using System.Runtime.CompilerServices;

namespace SakuraMod.SakuraModCode.Character;

internal static class SakuraCaptureRewardHandoff
{
    private static readonly SavedSpireField<Player, string> PendingCaptureCandidateCardId =
        new(() => "", "SakuraMod_PendingCaptureCandidateCardId");
    private static readonly ConditionalWeakTable<Player, Type> PendingCaptureCandidatesByPlayer = new();
    private static readonly ConditionalWeakTable<Player, PendingCaptureRewardMarker> PendingCaptureOffersByPlayer = new();
    private static readonly ConditionalWeakTable<CardReward, PendingCaptureRewardMarker> PendingCaptureRewardOffers = new();

    public static void Register() =>
        _ = PendingCaptureCandidateCardId;

    public static IEnumerable<CardModel> ModifyMerchantCardPool(Player player, IEnumerable<CardModel> options)
    {
        var pool = options.ToList();
        if (!ShouldUseCaptureRules(player) || !pool.Any(card => card is SakuraModCard))
            return pool;

        return pool.Where(card => !SakuraCardCatalog.IsTransparentCard(card)).ToList();
    }

    public static bool TryModifyCardRewardOptions(
        Player player,
        List<CardCreationResult> options,
        CardCreationOptions creationOptions)
    {
        if (!ShouldUseCaptureRules(player) || creationOptions.Source != CardCreationSource.Encounter)
            return false;

        return RewriteRewardOptions(
            options,
            SakuraCardCatalog.IsTransparentCard,
            SakuraCardCatalog.RewardableSupportCardTemplates(player),
            type => SakuraCardCatalog.CreateCleanTransparentCard(player, type),
            (original, supportTemplates, usedTypes) => CreateSupportReplacement(player, original, supportTemplates, usedTypes),
            CaptureCandidateTypes(player));
    }

    public static void PrepareCaptureCandidatesForReward(Player owner, ICombatState combatState)
    {
        PendingCaptureCandidatesByPlayer.Remove(owner);

        var candidates = SakuraManifestLoop.CaptureCandidateTypes(combatState, owner);
        if (candidates.Count == 0)
            return;

        RememberPendingCaptureCandidate(owner, candidates[0]);
    }

    public static IReadOnlyList<Type> CaptureCandidateTypes(Player owner)
    {
        if (TryGetPendingCaptureCandidate(owner, out var pendingCard))
            return [pendingCard];

        return SakuraManifestLoop.CaptureCandidateTypes(owner);
    }

    public static void ClearPendingCaptureCandidates(Player owner)
    {
        PendingCaptureCandidatesByPlayer.Remove(owner);
        PendingCaptureOffersByPlayer.Remove(owner);
        PendingCaptureCandidateCardId[owner] = "";
    }

    public static void RememberPendingCaptureRewardOffers(Player owner, IReadOnlyList<Reward> rewards)
    {
        if (!TryGetPendingCaptureCandidate(owner, out var pendingCard))
            return;

        foreach (var reward in RewardsAndChildren(rewards).OfType<CardReward>())
        {
            if (!reward.Cards.Any(card => card.GetType() == pendingCard))
                continue;

            MarkPendingCaptureOffer(owner, reward);
        }
    }

    public static void ClearPendingCaptureCandidatesForReward(Player owner, Reward reward)
    {
        if (reward is CardReward cardReward && PendingCaptureRewardOffers.TryGetValue(cardReward, out _))
            ClearPendingCaptureCandidates(owner);
    }

    public static void ClearOfferedPendingCaptureCandidates(Player owner)
    {
        if (PendingCaptureOffersByPlayer.TryGetValue(owner, out _))
            ClearPendingCaptureCandidates(owner);
    }

    internal static bool RewriteRewardOptions(
        List<CardCreationResult> options,
        Func<CardModel, bool> isTransparentCard,
        IReadOnlyList<CardModel> supportTemplates,
        Func<Type, CardModel> createCaptureCandidate,
        Func<CardModel, IReadOnlyList<CardModel>, HashSet<Type>, CardModel> createSupportReplacement,
        IReadOnlyList<Type> captureCandidateTypes)
    {
        var changed = ReplaceClearCardRewardOptions(options, isTransparentCard, supportTemplates, createSupportReplacement);
        changed |= AddCaptureCandidates(options, createCaptureCandidate, captureCandidateTypes);
        return changed;
    }

    private static bool ReplaceClearCardRewardOptions(
        List<CardCreationResult> options,
        Func<CardModel, bool> isTransparentCard,
        IReadOnlyList<CardModel> supportTemplates,
        Func<CardModel, IReadOnlyList<CardModel>, HashSet<Type>, CardModel> createSupportReplacement)
    {
        if (supportTemplates.Count == 0)
            return false;

        var changed = false;
        HashSet<Type> usedTypes = options
            .Select(option => option.Card.GetType())
            .Where(type => !SakuraCardCatalog.TransparentCardTypes.Contains(type))
            .ToHashSet();

        for (var i = 0; i < options.Count; i++)
        {
            var card = options[i].Card;
            if (!isTransparentCard(card))
                continue;

            var replacement = createSupportReplacement(card, supportTemplates, usedTypes);
            usedTypes.Add(replacement.GetType());
            options[i] = new CardCreationResult(replacement);
            changed = true;
        }

        return changed;
    }

    private static bool AddCaptureCandidates(
        List<CardCreationResult> options,
        Func<Type, CardModel> createCaptureCandidate,
        IReadOnlyList<Type> captureCandidateTypes)
    {
        var existingTypes = options.Select(option => option.Card.GetType()).ToHashSet();
        var changed = false;
        foreach (var type in captureCandidateTypes)
        {
            if (!existingTypes.Add(type))
                continue;

            options.Add(new CardCreationResult(createCaptureCandidate(type)));
            changed = true;
        }

        return changed;
    }

    private static bool ShouldUseCaptureRules(Player player) =>
        SakuraCaptureRules.CaptureModeEnabled && SakuraStarterCompatibility.IsSakura(player);

    private static CardModel CreateSupportReplacement(
        Player player,
        CardModel original,
        IReadOnlyList<CardModel> supportTemplates,
        HashSet<Type> usedTypes)
    {
        var template = PickSupportTemplate(original, supportTemplates, usedTypes);
        var replacement = player.RunState.CreateCard(template, player);
        UpgradeToLevel(replacement, original.CurrentUpgradeLevel);
        return replacement;
    }

    private static CardModel PickSupportTemplate(
        CardModel original,
        IReadOnlyList<CardModel> supportTemplates,
        HashSet<Type> usedTypes)
    {
        CardModel? picked = PickSupportTemplate(original, supportTemplates, usedTypes, SameRarityAndType)
            ?? PickSupportTemplate(original, supportTemplates, usedTypes, SameRarity)
            ?? PickSupportTemplate(original, supportTemplates, usedTypes, SameType)
            ?? PickSupportTemplate(original, supportTemplates, usedTypes, _ => true)
            ?? PickSupportTemplate(original, supportTemplates, [], SameRarityAndType)
            ?? PickSupportTemplate(original, supportTemplates, [], SameRarity)
            ?? PickSupportTemplate(original, supportTemplates, [], SameType)
            ?? PickSupportTemplate(original, supportTemplates, [], _ => true);

        return picked ?? throw new InvalidOperationException("Sakura support reward pool is empty.");

        bool SameRarityAndType(CardModel card) => card.Rarity == original.Rarity && card.Type == original.Type;
        bool SameRarity(CardModel card) => card.Rarity == original.Rarity;
        bool SameType(CardModel card) => card.Type == original.Type;
    }

    private static CardModel? PickSupportTemplate(
        CardModel original,
        IReadOnlyList<CardModel> supportTemplates,
        HashSet<Type> usedTypes,
        Func<CardModel, bool> predicate)
    {
        var candidates = supportTemplates
            .Where(predicate)
            .Where(card => !usedTypes.Contains(card.GetType()))
            .ToList();
        return candidates.Count == 0
            ? null
            : original.Owner.PlayerRng.Rewards.NextItem(candidates);
    }

    private static void UpgradeToLevel(CardModel card, int upgradeLevel)
    {
        while (card.CurrentUpgradeLevel < upgradeLevel && card.IsUpgradable)
            CardCmd.Upgrade(card);
    }

    private static void RememberPendingCaptureCandidate(Player owner, Type type)
    {
        PendingCaptureCandidatesByPlayer.Remove(owner);
        PendingCaptureCandidatesByPlayer.Add(owner, type);
        PendingCaptureCandidateCardId[owner] = SakuraCardCatalog.CardTemplate(type).Id.Entry;
    }

    private static bool TryGetPendingCaptureCandidate(Player owner, out Type type)
    {
        if (PendingCaptureCandidatesByPlayer.TryGetValue(owner, out type!))
            return true;

        if (TryLoadPendingCaptureCandidate(owner, out type))
        {
            RememberPendingCaptureCandidate(owner, type);
            return true;
        }

        type = default!;
        return false;
    }

    private static bool TryLoadPendingCaptureCandidate(Player owner, out Type type)
    {
        var cardId = PendingCaptureCandidateCardId[owner];
        if (!string.IsNullOrWhiteSpace(cardId))
            return SakuraCardCatalog.TryGetTransparentCardTypeById(cardId, out type);

        type = default!;
        return false;
    }

    private static void MarkPendingCaptureOffer(Player owner, CardReward reward)
    {
        PendingCaptureOffersByPlayer.Remove(owner);
        PendingCaptureOffersByPlayer.Add(owner, new PendingCaptureRewardMarker());
        PendingCaptureRewardOffers.Remove(reward);
        PendingCaptureRewardOffers.Add(reward, new PendingCaptureRewardMarker());
    }

    private static IEnumerable<Reward> RewardsAndChildren(IEnumerable<Reward> rewards)
    {
        foreach (var reward in rewards)
        {
            yield return reward;

            if (reward is not LinkedRewardSet linkedRewardSet)
                continue;

            foreach (var child in linkedRewardSet.Rewards)
                yield return child;
        }
    }

    private sealed class PendingCaptureRewardMarker;
}
