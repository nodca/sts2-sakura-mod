using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using SakuraMod.SakuraModCode.Cards;

namespace SakuraMod.SakuraModCode.Character;

public static class SakuraCaptureRules
{
    public static bool CaptureModeEnabled { get; set; } = true;
}

internal static class SakuraCaptureRunHooks
{
    private const string RunHookId = "SakuraMod.Capture.Run";
    private const string CombatHookId = "SakuraMod.Capture.Combat";
    private static readonly SakuraCaptureHook Hook = new();
    private static bool _registered;

    public static void Register()
    {
        if (_registered)
            return;

        ModHelper.SubscribeForRunStateHooks(RunHookId, _ => [Hook]);
        ModHelper.SubscribeForCombatStateHooks(CombatHookId, _ => [Hook]);
        _registered = true;
    }
}

internal sealed class SakuraCaptureHook : AbstractModel
{
    public override bool ShouldReceiveCombatHooks => true;

    public override IEnumerable<CardModel> ModifyMerchantCardPool(Player player, IEnumerable<CardModel> options)
    {
        var pool = options.ToList();
        if (!ShouldUseCaptureRules(player) || !pool.Any(card => card is SakuraModCard))
            return pool;

        return pool.Where(card => !SakuraActions.IsClearCard(card)).ToList();
    }

    public override bool TryModifyCardRewardOptions(
        Player player,
        List<CardCreationResult> cardRewardOptions,
        CardCreationOptions creationOptions)
    {
        if (!ShouldUseCaptureRules(player) || creationOptions.Source != CardCreationSource.Encounter)
            return false;

        var changed = ReplaceClearCardRewardOptions(player, cardRewardOptions);
        changed |= AddCaptureCandidates(player, cardRewardOptions);
        return changed;
    }

    public override Task AfterCombatVictory(CombatRoom room)
    {
        if (!SakuraCaptureRules.CaptureModeEnabled)
            return Task.CompletedTask;

        foreach (var player in room.CombatState.Players.Where(SakuraStarterCards.IsSakura))
        {
            SakuraManifestLoop.ClearOfferedPendingCaptureCandidates(player);
            SakuraManifestLoop.PrepareCaptureCandidatesForReward(player, room.CombatState);
        }

        return Task.CompletedTask;
    }

    public override bool TryModifyRewards(Player player, List<Reward> rewards, AbstractRoom? room)
    {
        if (ShouldUseCaptureRules(player))
            SakuraManifestLoop.RememberPendingCaptureRewardOffers(player, rewards);

        return false;
    }

    public override Task AfterRewardTaken(Player player, Reward reward)
    {
        if (ShouldUseCaptureRules(player))
            SakuraManifestLoop.ClearPendingCaptureCandidatesForReward(player, reward);

        return Task.CompletedTask;
    }

    private static bool ShouldUseCaptureRules(Player player) =>
        SakuraCaptureRules.CaptureModeEnabled && SakuraStarterCards.IsSakura(player);

    private static bool ReplaceClearCardRewardOptions(Player player, List<CardCreationResult> options)
    {
        var supportTemplates = SakuraActions.RewardableSupportCardTemplates(player);
        if (supportTemplates.Count == 0)
            return false;

        var changed = false;
        HashSet<Type> usedTypes = options
            .Select(option => option.Card.GetType())
            .Where(type => !SakuraActions.ClearCardModelTypes.Contains(type))
            .ToHashSet();

        for (var i = 0; i < options.Count; i++)
        {
            var card = options[i].Card;
            if (!SakuraActions.IsClearCard(card))
                continue;

            var replacement = CreateSupportReplacement(player, card, supportTemplates, usedTypes);
            usedTypes.Add(replacement.GetType());
            options[i] = new CardCreationResult(replacement);
            changed = true;
        }

        return changed;
    }

    private static bool AddCaptureCandidates(Player player, List<CardCreationResult> options)
    {
        var existingTypes = options.Select(option => option.Card.GetType()).ToHashSet();
        var changed = false;
        foreach (var type in SakuraManifestLoop.CaptureCandidateTypes(player))
        {
            if (!existingTypes.Add(type))
                continue;

            options.Add(new CardCreationResult(SakuraManifestLoop.CreateCleanClearCard(player, type)));
            changed = true;
        }

        return changed;
    }

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
}
