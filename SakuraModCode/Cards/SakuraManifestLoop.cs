using BaseLib.Extensions;
using BaseLib.Utils;
using MegaCrit.Sts2.Core;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rewards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.Relics;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace SakuraMod.SakuraModCode.Cards;

public readonly record struct GeneratedCardOptions
{
    public PileType? Pile { get; init; }
    public CardPilePosition? Position { get; init; }
    public bool RemoveRelease { get; init; }
    public bool RemoveTemporary { get; init; }
    public bool RemoveManifestAtlasOrigin { get; init; }
    public bool AddTemporary { get; init; }
    public bool AddRelease { get; init; }
    public bool AddManifestAtlasOrigin { get; init; }
    public bool FreeThisTurn { get; init; }
}

internal static class SakuraGeneratedCardDiagnostics
{
    public const string EnabledEnvironmentVariable = "SAKURA_GENERATED_CARD_DIAGNOSTICS";

    private static readonly ConditionalWeakTable<CardModel, TimingScope> ActiveTimings = new();

    public static bool Enabled { get; set; } = IsEnabledByEnvironment();

    public static TimingScope? Start(CardModel card, GeneratedCardOptions options)
    {
        if (!Enabled)
            return null;

        var timing = new TimingScope(card, options);
        ActiveTimings.Remove(card);
        ActiveTimings.Add(card, timing);
        return timing;
    }

    public static bool IsTracked(CardModel? card) =>
        Enabled && card is not null && ActiveTimings.TryGetValue(card, out _);

    public static DetailTiming? StartDetail(CardModel? card, string stage)
    {
        if (!Enabled || card is null || !ActiveTimings.TryGetValue(card, out var timing))
            return null;

        return timing.StartDetail(stage);
    }

    private static bool IsEnabledByEnvironment()
    {
        var value = Environment.GetEnvironmentVariable(EnabledEnvironmentVariable);
        return value is not null
               && (value == "1"
                   || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("on", StringComparison.OrdinalIgnoreCase));
    }

    private static void StopTracking(CardModel card, TimingScope timing)
    {
        if (ActiveTimings.TryGetValue(card, out var activeTiming)
            && ReferenceEquals(activeTiming, timing))
            ActiveTimings.Remove(card);
    }

    public sealed class DetailTiming
    {
        private readonly TimingScope _timing;
        private readonly string _stage;
        private readonly long _startTicks;
        private bool _finished;

        internal DetailTiming(TimingScope timing, string stage, long startTicks)
        {
            _timing = timing;
            _stage = stage;
            _startTicks = startTicks;
        }

        public void Finish(string? detail = null)
        {
            if (_finished)
                return;

            _finished = true;
            _timing.FinishDetail(_stage, _startTicks, detail);
        }
    }

    public sealed class TimingScope
    {
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private readonly CardModel _card;
        private readonly string _context;
        private long _lastTicks;
        private long _lastDetailTicks;
        private bool _finished;

        public TimingScope(CardModel card, GeneratedCardOptions options)
        {
            _card = card;
            _context = BuildContext(card, options);
        }

        public void Mark(string stage)
        {
            var now = _stopwatch.ElapsedTicks;
            Log(stage, now - _lastTicks, now);
            _lastTicks = now;
            _lastDetailTicks = now;
        }

        public void Finish(string result)
        {
            if (_finished)
                return;

            _finished = true;
            _stopwatch.Stop();
            Log($"total:{result}", _stopwatch.ElapsedTicks - _lastTicks, _stopwatch.ElapsedTicks);
            StopTracking(_card, this);
        }

        internal DetailTiming StartDetail(string stage)
        {
            var startTicks = _stopwatch.ElapsedTicks;
            LogDetail($"{stage}:begin", startTicks - _lastDetailTicks, startTicks);
            _lastDetailTicks = startTicks;
            return new DetailTiming(this, stage, startTicks);
        }

        internal void FinishDetail(string stage, long startTicks, string? detail)
        {
            if (_finished)
                return;

            var now = _stopwatch.ElapsedTicks;
            LogDetail(
                $"{stage}:end",
                now - _lastDetailTicks,
                now,
                now - startTicks,
                detail);
            _lastDetailTicks = now;
        }

        private void Log(string stage, long deltaTicks, long totalTicks)
        {
            var deltaMs = TicksToMilliseconds(deltaTicks);
            var totalMs = TicksToMilliseconds(totalTicks);
            MainFile.Logger.Info(
                $"GeneratedCardTiming stage={stage} deltaMs={FormatMilliseconds(deltaMs)} totalMs={FormatMilliseconds(totalMs)} {_context}");
        }

        private void LogDetail(
            string detail,
            long deltaTicks,
            long totalTicks,
            long? durationTicks = null,
            string? extra = null)
        {
            var deltaMs = TicksToMilliseconds(deltaTicks);
            var totalMs = TicksToMilliseconds(totalTicks);
            var duration = durationTicks is null
                ? string.Empty
                : $" durationMs={FormatMilliseconds(TicksToMilliseconds(durationTicks.Value))}";
            var extraText = string.IsNullOrWhiteSpace(extra) ? string.Empty : $" {extra}";
            MainFile.Logger.Info(
                $"GeneratedCardTiming detail={detail} deltaMs={FormatMilliseconds(deltaMs)} totalMs={FormatMilliseconds(totalMs)}{duration}{extraText} {_context}");
        }

        private static string BuildContext(CardModel card, GeneratedCardOptions options)
        {
            var pile = options.Pile ?? PileType.Hand;
            return string.Join(
                ' ',
                $"cardType={card.GetType().Name}",
                $"cardId={card.Id.Entry}",
                $"pile={pile}",
                $"position={options.Position?.ToString() ?? "Random"}",
                $"transparent={SakuraCardCatalog.IsTransparentCard(card)}",
                $"removeRelease={options.RemoveRelease}",
                $"removeTemporary={options.RemoveTemporary}",
                $"removeManifestOrigin={options.RemoveManifestAtlasOrigin}",
                $"addTemporary={options.AddTemporary}",
                $"addRelease={options.AddRelease}",
                $"addManifestOrigin={options.AddManifestAtlasOrigin}",
                $"freeThisTurn={options.FreeThisTurn}");
        }

        private static double TicksToMilliseconds(long ticks) =>
            ticks * 1000.0 / Stopwatch.Frequency;

        private static string FormatMilliseconds(double milliseconds) =>
            milliseconds.ToString("0.###", CultureInfo.InvariantCulture);
    }
}

public static class SakuraManifestLoop
{
    private const int CommonManifestWeight = 6;
    private const int UncommonManifestWeight = 3;
    private const int RareManifestWeight = 1;
    private const int BaseManifestChoiceCount = 3;

    private static readonly LocString ManifestPrompt = new("cards", "SAKURAMOD-GENERIC.manifestPrompt");
    private static readonly SavedSpireField<Player, string> PendingCaptureCandidateCardId =
        new(() => "", "SakuraMod_PendingCaptureCandidateCardId");
    private static readonly ConditionalWeakTable<CombatState, Dictionary<Player, HashSet<Type>>> CatalogedClearCardsByCombat = new();
    private static readonly ConditionalWeakTable<CombatState, Dictionary<Player, Type>> CaptureCandidatesByCombat = new();
    private static readonly ConditionalWeakTable<Player, Type> PendingCaptureCandidatesByPlayer = new();
    private static readonly ConditionalWeakTable<Player, PendingCaptureRewardMarker> PendingCaptureOffersByPlayer = new();
    private static readonly ConditionalWeakTable<CardReward, PendingCaptureRewardMarker> PendingCaptureRewardOffers = new();

    public static void Register() =>
        _ = PendingCaptureCandidateCardId;

    public static Task<IReadOnlyList<CardModel>> Manifest(
        SakuraModCard source,
        PlayerChoiceContext context,
        int amount,
        bool stabilize = false,
        Type? excludedType = null,
        int rareAtlasChoices = 0) =>
        Manifest(source.Owner, context, amount, stabilize, excludedType, rareAtlasChoices);

    public static async Task<IReadOnlyList<CardModel>> Manifest(
        Player owner,
        PlayerChoiceContext context,
        int amount,
        bool stabilize = false,
        Type? excludedType = null,
        int rareAtlasChoices = 0)
    {
        List<CardModel> manifested = [];
        amount += owner.Creature.GetPower<MagicSurgePower>()?.ConsumeManifestBonus() ?? 0;

        for (var i = 0; i < amount; i++)
        {
            var source = i < rareAtlasChoices
                ? ManifestSource.RareAtlas
                : ManifestSource.Default;
            var choiceSet = ManifestChoices(owner, excludedType, source);
            var choices = choiceSet.Cards
                .Select(choice => CreateManifestChoice(owner, choice))
                .ToList();
            if (choices.Count == 0)
                break;

            try
            {
                var chosen = await CardSelectCmd.FromSimpleGrid(
                    context,
                    choices,
                    owner,
                    new CardSelectorPrefs(ManifestPrompt, 1)
                    {
                        Cancelable = false,
                        RequireManualConfirmation = false
                    });
                var copy = chosen.FirstOrDefault();
                if (copy is null)
                    break;

                await AddGeneratedCardToCombat(
                    copy,
                    new GeneratedCardOptions
                    {
                        RemoveRelease = true,
                        AddTemporary = !stabilize,
                        AddManifestAtlasOrigin = choiceSet.CaptureEligible
                    },
                    context);
                manifested.Add(copy);
            }
            finally
            {
                RemoveDetachedGeneratedChoices(choices);
            }
        }

        if (manifested.Count > 0)
            await PowerCmd.Apply<SakuraManifestedThisTurnPower>(owner.Creature, 1, owner.Creature, null, false);

        return manifested;
    }

    public static async Task RememberCatalogCard(PlayerChoiceContext context, CardPlay play)
    {
        var card = play.Card;
        if (card?.Owner is not { } owner || card.CombatState is null || !SakuraCardCatalog.IsTransparentCard(card))
            return;

        var cardsByOwner = CatalogedClearCardsByCombat.GetValue(card.CombatState, _ => []);
        if (!cardsByOwner.TryGetValue(owner, out var cards))
        {
            cards = [];
            cardsByOwner[owner] = cards;
        }

        if (cards.Add(card.GetType()))
        {
            await SyncCatalogPower(owner);
            await TriggerCatalogedClearCard(context, play);
        }
    }

    public static int CatalogCount(Player owner)
    {
        var combatState = owner.Creature.CombatState;
        if (combatState is null
            || !CatalogedClearCardsByCombat.TryGetValue(combatState, out var cardsByOwner)
            || !cardsByOwner.TryGetValue(owner, out var cards))
            return 0;

        return cards.Count;
    }

    public static bool HasCatalogedClearCard(Player owner, CardModel card)
    {
        var combatState = owner.Creature.CombatState;
        return combatState is not null
            && SakuraCardCatalog.IsTransparentCard(card)
            && CatalogedClearCardsByCombat.TryGetValue(combatState, out var cardsByOwner)
            && cardsByOwner.TryGetValue(owner, out var cards)
            && cards.Contains(card.GetType());
    }

    public static IReadOnlyList<CardModel> CatalogedClearCards(Player owner, Type? excludedType = null)
    {
        var combatState = owner.Creature.CombatState;
        if (combatState is null
            || !CatalogedClearCardsByCombat.TryGetValue(combatState, out var cardsByOwner)
            || !cardsByOwner.TryGetValue(owner, out var cards))
            return [];

        return SakuraCardCatalog.TransparentCardTypes
            .Where(cards.Contains)
            .Where(type => type != excludedType)
            .Select(SakuraCardCatalog.CardTemplate)
            .ToList();
    }

    public static async Task<CardModel?> SelectCatalogedClearCard(
        SakuraModCard source,
        PlayerChoiceContext context,
        bool cancelable = true,
        Type? excludedType = null)
    {
        var choices = CatalogedClearCards(source.Owner, excludedType)
            .Select(card => CreateGeneratedChoice(source, card, upgraded: false))
            .ToList();
        if (choices.Count == 0)
            return null;

        try
        {
            var selected = await SakuraActions.SelectFromCards(source, context, choices, cancelable);
            return selected?.CreateClone();
        }
        finally
        {
            RemoveDetachedGeneratedChoices(choices);
        }
    }

    public static IReadOnlyList<Type> CaptureCandidateTypes(Player owner)
    {
        if (TryGetPendingCaptureCandidate(owner, out var pendingCard))
            return [pendingCard];

        var combatState = owner.Creature.CombatState;
        return combatState is null ? [] : CaptureCandidateTypes(combatState, owner);
    }

    public static void PrepareCaptureCandidatesForReward(Player owner, CombatState combatState)
    {
        PendingCaptureCandidatesByPlayer.Remove(owner);

        var candidates = CaptureCandidateTypes(combatState, owner);
        if (candidates.Count == 0)
            return;

        RememberPendingCaptureCandidate(owner, candidates[0]);
    }

    public static IReadOnlyList<CardModel> CaptureCandidateTemplates(Player owner) =>
        CaptureCandidateTypes(owner)
            .Select(SakuraCardCatalog.CardTemplate)
            .ToList();

    public static CardModel CreateCleanClearCard(Player owner, Type type) =>
        SakuraCardCatalog.CreateCleanTransparentCard(owner, type);

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

    public static async Task<bool> GrantTemporary(PlayerChoiceContext context, CardModel card)
    {
        if (card.IsTemporary())
            return false;

        card.MakeTemporary();
        await TriggerTemporaryGranted(context, card);
        return true;
    }

    public static async Task OnTemporaryStabilized(PlayerChoiceContext context, CardModel card)
    {
        RememberCaptureCandidate(card);

        if (card.Owner?.Creature.GetPower<GrowingMagicPower>() is { } growingMagic)
            await growingMagic.AfterTemporaryStabilized(context);
        if (card.Owner?.GetRelic<StorageRibbon>() is { } storageRibbon)
            await storageRibbon.AfterTemporaryStabilized(context, card);
    }

    public static async Task<CardModel?> AddTemporaryCopyToHand(
        SakuraModCard source,
        PlayerChoiceContext context,
        CardModel card,
        bool release,
        bool freeThisTurn,
        bool preserveRelease = false) =>
        await AddGeneratedCopyToHand(
            source,
            card,
            new GeneratedCardOptions
            {
                RemoveRelease = !preserveRelease,
                AddTemporary = true,
                AddRelease = release,
                FreeThisTurn = freeThisTurn
            },
            context);

    public static async Task<CardModel?> AddRememberedCopyToHand(SakuraModCard source, CardModel card, bool freeThisTurn) =>
        await AddGeneratedCopyToHand(
            source,
            card,
            new GeneratedCardOptions
            {
                RemoveTemporary = true,
                FreeThisTurn = freeThisTurn
            });

    public static async Task<CardModel?> AddGeneratedCopyToHand(
        SakuraModCard source,
        CardModel card,
        GeneratedCardOptions options,
        PlayerChoiceContext? context = null) =>
        await AddGeneratedCopy(
            source,
            card,
            options with
            {
                Pile = PileType.Hand
            },
            context);

    public static async Task<CardModel?> AddGeneratedCopy(
        SakuraModCard source,
        CardModel card,
        GeneratedCardOptions options,
        PlayerChoiceContext? context = null)
    {
        var copy = card.CreateClone();
        copy.RemoveManifestAtlasOrigin();
        await AddGeneratedCardToCombat(copy, options, context);
        return copy;
    }

    public static async Task<CardModel> AddGeneratedCardToCombat(
        CardModel card,
        GeneratedCardOptions options = default,
        PlayerChoiceContext? context = null)
    {
        var timing = SakuraGeneratedCardDiagnostics.Start(card, options);

        try
        {
            if (options.RemoveRelease)
                card.RemoveRelease();
            if (options.RemoveTemporary)
                card.RemoveTemporaryForExchange();
            if (options.RemoveManifestAtlasOrigin)
                card.RemoveManifestAtlasOrigin();
            var temporaryGranted = false;
            if (options.AddTemporary)
            {
                var hadTemporary = card.IsTemporary();
                card.MakeTemporary();
                temporaryGranted = !hadTemporary;
            }
            if (options.FreeThisTurn)
                card.SetToFreeThisTurn();
            if (options.AddManifestAtlasOrigin)
                card.MarkManifestAtlasOrigin();
            timing?.Mark("state-normalization");

            await CardPileCmd.AddGeneratedCardToCombat(
                card,
                options.Pile ?? PileType.Hand,
                true,
                options.Position ?? CardPilePosition.Random);
            timing?.Mark("card-pile-add-generated");

            if (temporaryGranted && context is not null)
                await TriggerTemporaryGranted(context, card);
            timing?.Mark("temporary-granted-observers");

            if (options.AddRelease)
                await SakuraActions.ReleaseAndRecord(card);
            timing?.Mark("release-recording");

            timing?.Finish("completed");
            return card;
        }
        catch
        {
            timing?.Finish("failed");
            throw;
        }
    }

    public static async Task<CardModel?> DiscoverGenerated(
        SakuraModCard source,
        PlayerChoiceContext context,
        IReadOnlyList<CardModel> cards,
        bool freeThisTurn = false,
        bool upgraded = false)
    {
        var choices = cards
            .Select(card => CreateGeneratedChoice(source, card, upgraded))
            .ToList();
        try
        {
            var chosen = await SakuraActions.SelectFromCards(source, context, choices, cancelable: false);
            if (chosen is null)
                return null;

            await AddGeneratedCardToCombat(
                chosen,
                new GeneratedCardOptions
                {
                    FreeThisTurn = freeThisTurn
                });
            return chosen;
        }
        finally
        {
            RemoveDetachedGeneratedChoices(choices);
        }
    }

    private static IReadOnlyList<Type> CaptureCandidateTypes(CombatState combatState, Player owner)
    {
        if (!CaptureCandidatesByCombat.TryGetValue(combatState, out var cardsByOwner)
            || !cardsByOwner.TryGetValue(owner, out var type))
            return [];

        return [type];
    }

    private static CardModel CreateManifestChoice(Player owner, CardModel source)
    {
        var copy = source.IsCanonical
            ? CreateCombatCardFromTemplate(owner, source)
            : CloneToUpgradeLevel(source, source.CurrentUpgradeLevel);
        UpgradeToLevel(copy, source.CurrentUpgradeLevel);
        return copy;
    }

    private static CardModel CreateCombatCardFromTemplate(Player owner, CardModel source)
    {
        var scope = owner.Creature.CombatState
            ?? throw new InvalidOperationException($"Cannot create manifest choice {source.Id.Entry} outside combat.");
        return scope.CreateCard(source, owner);
    }

    private static ManifestChoiceSet ManifestChoices(Player owner, Type? excludedType, ManifestSource source)
    {
        var hasNamelessBookTruth = owner.GetRelic<NamelessBookTruth>() is not null;
        var captureEligible = source == ManifestSource.RareAtlas || !hasNamelessBookTruth;
        var weightedSources = source switch
        {
            ManifestSource.RareAtlas => WeightedManifestAtlasSources(card => card.Rarity == CardRarity.Rare),
            _ when hasNamelessBookTruth => CardPile.GetCards(owner, PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust)
                .Where(SakuraCardCatalog.IsTransparentCard)
                .ToList(),
            _ => WeightedManifestAtlasSources(SakuraCardCatalog.IsDefaultManifestAtlasCard)
        };
        if (excludedType is not null)
            weightedSources.RemoveAll(card => card.GetType() == excludedType);

        List<CardModel> choices = [];
        var rng = owner.RunState.Rng.CombatCardSelection;

        while (choices.Count < ManifestChoiceCount(owner) && weightedSources.Count > 0)
        {
            var picked = rng.NextItem(weightedSources);
            if (picked is null)
                break;

            choices.Add(picked);
            var pickedType = picked.GetType();
            weightedSources.RemoveAll(card => card.GetType() == pickedType);
        }

        return new ManifestChoiceSet(choices, captureEligible);
    }

    private static int ManifestChoiceCount(Player owner) =>
        BaseManifestChoiceCount + (owner.GetRelic<SakuraIntuition>()?.AdditionalManifestChoices ?? 0);

    private static List<CardModel> WeightedManifestAtlasSources(Func<CardModel, bool>? predicate = null) =>
        SakuraCardCatalog.TransparentCardTypes
            .Select(SakuraCardCatalog.CardTemplate)
            .Where(card => predicate?.Invoke(card) != false)
            .SelectMany(card => Enumerable.Repeat(card, ManifestWeight(card.Rarity)))
            .ToList();

    private static int ManifestWeight(CardRarity rarity) =>
        rarity switch
        {
            CardRarity.Basic or CardRarity.Common => CommonManifestWeight,
            CardRarity.Uncommon => UncommonManifestWeight,
            CardRarity.Rare => RareManifestWeight,
            _ => RareManifestWeight
        };

    private static void RememberCaptureCandidate(CardModel card)
    {
        if (card.Owner is not { } owner
            || card.CombatState is null
            || !SakuraCardCatalog.IsTransparentCard(card)
            || !card.IsManifestAtlasOrigin())
            return;

        var cardsByOwner = CaptureCandidatesByCombat.GetValue(card.CombatState, _ => []);
        cardsByOwner.TryAdd(owner, card.GetType());
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

    private static async Task TriggerCatalogedClearCard(PlayerChoiceContext context, CardPlay play)
    {
        if (play.Card?.Owner?.GetRelic<CatalogNewPage>() is { } catalogNewPage)
            await catalogNewPage.AfterCatalogedClearCard(context, play);
    }

    private static async Task SyncCatalogPower(Player owner)
    {
        var count = CatalogCount(owner);
        if (count <= 0)
            return;

        var power = owner.Creature.GetPower<SakuraCatalogPower>();
        if (power is null)
        {
            await PowerCmd.Apply<SakuraCatalogPower>(owner.Creature, count, owner.Creature, null, false);
            return;
        }

        await PowerCmd.ModifyAmount(power, count - power.Amount, owner.Creature, null, false);
    }

    private static async Task TriggerTemporaryGranted(PlayerChoiceContext context, CardModel card)
    {
        if (card.Owner?.Creature.GetPower<FalseDailyLifePower>() is { } falseDailyLife)
            await falseDailyLife.AfterTemporaryGranted(context);
    }

    private static CardModel CreateGeneratedChoice(SakuraModCard source, CardModel card, bool upgraded)
    {
        var targetUpgradeLevel = upgraded
            ? Math.Max(1, card.CurrentUpgradeLevel)
            : card.CurrentUpgradeLevel;
        var choice = card.IsCanonical
            ? CreateCardFromTemplate(source, card)
            : CloneToUpgradeLevel(card, targetUpgradeLevel);
        UpgradeToLevel(choice, targetUpgradeLevel);
        return choice;
    }

    private static CardModel CreateCardFromTemplate(SakuraModCard source, CardModel card)
    {
        var scope = source.CardScope
            ?? throw new InvalidOperationException($"Cannot create generated {card.Id.Entry} without a card scope.");
        return scope.CreateCard(card, source.Owner);
    }

    private static CardModel CloneToUpgradeLevel(CardModel source, int upgradeLevel)
    {
        var copy = source.CreateClone();
        UpgradeToLevel(copy, upgradeLevel);
        return copy;
    }

    private static void UpgradeToLevel(CardModel card, int upgradeLevel)
    {
        while (card.CurrentUpgradeLevel < upgradeLevel && card.IsUpgradable)
            card.UpgradeInternal();
    }

    private static void RemoveDetachedGeneratedChoices(IEnumerable<CardModel> choices)
    {
        foreach (var choice in choices)
        {
            if (choice.Pile is not null)
                continue;

            choice.CardScope?.RemoveCard(choice);
        }
    }

    private enum ManifestSource
    {
        Default,
        RareAtlas
    }

    private sealed record ManifestChoiceSet(IReadOnlyList<CardModel> Cards, bool CaptureEligible);
}
