using BaseLib.Abstracts;
using BaseLib.Extensions;
using BaseLib.Patches.Features;
using BaseLib.Utils;
using MegaCrit.Sts2.Core;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.Relics;
using SakuraMod.SakuraModCode.Character;
using System.Runtime.CompilerServices;

namespace SakuraMod.SakuraModCode.Cards;

public static class SakuraActions
{
    private const int CommonManifestWeight = 6;
    private const int UncommonManifestWeight = 3;
    private const int RareManifestWeight = 1;
    private const int BaseManifestChoiceCount = 3;

    private static readonly LocString ManifestPrompt = new("cards", "SAKURAMOD-GENERIC.manifestPrompt");
    private static readonly LocString HandPrompt = new("cards", "SAKURAMOD-GENERIC.handPrompt");
    private static readonly LocString CardPrompt = new("cards", "SAKURAMOD-GENERIC.cardPrompt");
    private static readonly IReadOnlyList<Type> ManifestableClearCardTypes =
    [
        typeof(Gale),
        typeof(Reflect),
        typeof(Flight),
        typeof(Action),
        typeof(Appear),
        typeof(Aqua),
        typeof(Blade),
        typeof(Hail),
        typeof(Lucid),
        typeof(Shade),
        typeof(Siege),
        typeof(Swing),
        typeof(Break),
        typeof(Choice),
        typeof(Promise),
        typeof(Struggle),
        typeof(Blaze),
        typeof(Dreaming),
        typeof(Gravitation),
        typeof(Mirage),
        typeof(Record),
        typeof(Exchange),
        typeof(Kindness),
        typeof(Labyrinth),
        typeof(Repair),
        typeof(Reversal),
        typeof(Rewind),
        typeof(Snooze),
        typeof(Spiral),
        typeof(Transfer),
        typeof(Blank),
        typeof(Mirror),
        typeof(Remind),
        typeof(Synchronize),
        typeof(Time),
        typeof(TrueOrFalse)
    ];
    private static readonly HashSet<Type> DefaultManifestExcludedClearCardTypes =
    [
        typeof(Gale),
        typeof(Siege)
    ];
    private static readonly HashSet<Type> ClearCardTypes = ManifestableClearCardTypes.ToHashSet();
    private static readonly IReadOnlyList<Type> PartnerCardTypes =
    [
        typeof(KeroBond),
        typeof(KeroRecon),
        typeof(KeroSnackBreak),
        typeof(TomoyoCostume),
        typeof(TomoyoDesign),
        typeof(AkihoTea),
        typeof(AkihoDream),
        typeof(ClockCountryAlice),
        typeof(DWatch),
        typeof(FalseDailyLife),
        typeof(CompassTracking),
        typeof(SyaoranTalisman),
        typeof(DaoistSupport),
        typeof(TomoyoCamera),
        typeof(TomoyoBond),
        typeof(SyaoranBond),
        typeof(CostumePocket),
        typeof(DreamCostume),
        typeof(BlessingOfTheNamelessBook),
        typeof(ThunderEmperorSummon),
        typeof(MeilingComboKick),
        typeof(TalismanCombo),
        typeof(SilverMoonWing),
        typeof(BigBrotherSense),
        typeof(YukitoLunchBox),
        typeof(MomoContract),
        typeof(YamazakiTallTale),
        typeof(EriolPhoneCall),
        typeof(FujitakaNote),
        typeof(NaokoGhostStory)
    ];
    private static readonly HashSet<Type> PartnerCardTypeSet = PartnerCardTypes.ToHashSet();
    private static readonly ConditionalWeakTable<CombatState, Dictionary<Player, List<PlayedElementEntry>>> PlayedElementsByCombat = new();
    private static readonly ConditionalWeakTable<CombatState, Dictionary<Player, CardPlaybackMemory>> PlayedCardsByCombat = new();
    private static readonly ConditionalWeakTable<CombatState, Dictionary<Player, HashSet<Type>>> CatalogedClearCardsByCombat = new();
    private static readonly ConditionalWeakTable<CombatState, Dictionary<Player, Type>> CaptureCandidatesByCombat = new();
    private static readonly ConditionalWeakTable<Player, Type> PendingCaptureCandidatesByPlayer = new();

    public static IReadOnlyList<Type> ClearCardModelTypes => ManifestableClearCardTypes;

    public static Task<IReadOnlyList<CardModel>> Manifest(
        SakuraModCard source,
        PlayerChoiceContext context,
        int amount,
        bool stabilize = false,
        Type? excludedType = null) =>
        Manifest(source.Owner, context, amount, stabilize, excludedType);

    public static async Task<IReadOnlyList<CardModel>> Manifest(
        Player owner,
        PlayerChoiceContext context,
        int amount,
        bool stabilize = false,
        Type? excludedType = null)
    {
        List<CardModel> manifested = [];
        amount += owner.Creature.GetPower<MagicSurgePower>()?.ConsumeManifestBonus() ?? 0;

        for (var i = 0; i < amount; i++)
        {
            var choiceSet = ManifestChoices(owner, excludedType, ManifestSource.Default);
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

    public static async Task<IReadOnlyList<CardModel>> ManifestRareAtlas(
        Player owner,
        PlayerChoiceContext context,
        int amount = 1)
    {
        List<CardModel> manifested = [];
        for (var i = 0; i < amount; i++)
        {
            var choiceSet = ManifestChoices(owner, excludedType: null, ManifestSource.RareAtlas);
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
                        AddTemporary = true,
                        AddManifestAtlasOrigin = true
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

    public static int ReleaseGainCountThisTurn(Player owner) =>
        owner.Creature.GetPower<SakuraReleaseCountThisTurnPower>()?.Amount ?? 0;

    public static bool HasPlayedReleasedCardEarlierThisTurn(Player owner) =>
        owner.Creature.CombatState is not null
        && PlayedCardsByCombat.TryGetValue(owner.Creature.CombatState, out var cardsByOwner)
        && cardsByOwner.TryGetValue(owner, out var memory)
        && memory.PlayedReleasedThisTurn;

    public static void BeginPlayerTurn(Player player, CombatState combatState)
    {
        ClearPlayedElements(player);

        var memory = GetCardPlaybackMemory(combatState, player);
        if (memory.TurnOpen)
            return;

        SakuraCardStates.ResetTemporaryCleanupForTurn(player);
        memory.LastTurn = memory.CurrentTurn;
        memory.CurrentTurn = [];
        memory.RecordedPlays.Clear();
        memory.PlayedReleasedThisTurn = false;
        memory.TurnOpen = true;
    }

    public static void EndPlayerTurn(Player player)
    {
        if (player.Creature.CombatState is null
            || !PlayedCardsByCombat.TryGetValue(player.Creature.CombatState, out var cardsByOwner)
            || !cardsByOwner.TryGetValue(player, out var memory))
            return;

        memory.TurnOpen = false;
        memory.PlayedReleasedThisTurn = false;
    }

    public static void RememberPlayedReleasedCard(CardPlay play)
    {
        var card = play.Card;
        if (card?.Owner is not { } owner || card.CombatState is null || card.IsReleased() != true)
            return;

        GetCardPlaybackMemory(card.CombatState, owner).PlayedReleasedThisTurn = true;
    }

    public static void RememberPlayedCard(CardPlay play)
    {
        var card = play.Card;
        if (card?.Owner is not { } owner || card.CombatState is null)
            return;

        var memory = GetCardPlaybackMemory(card.CombatState, owner);
        if (!memory.RecordedPlays.Add(play))
            return;

        var snapshot = CreatePlaybackSnapshot(card);
        var entry = new PlayedCardSnapshot(play, snapshot, CreatePlaybackKey(snapshot));
        memory.CurrentTurn.Add(entry);
        memory.AllCombat.Add(entry);
    }

    public static async Task RememberCatalogCard(PlayerChoiceContext context, CardPlay play)
    {
        var card = play.Card;
        if (card?.Owner is not { } owner || card.CombatState is null || !IsClearCard(card))
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
            && IsClearCard(card)
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

        return ManifestableClearCardTypes
            .Where(cards.Contains)
            .Where(type => type != excludedType)
            .Select(CardTemplate)
            .ToList();
    }

    public static IReadOnlyList<Type> CaptureCandidateTypes(Player owner)
    {
        if (PendingCaptureCandidatesByPlayer.TryGetValue(owner, out var pendingCard))
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

        PendingCaptureCandidatesByPlayer.Add(owner, candidates[0]);
    }

    private static IReadOnlyList<Type> CaptureCandidateTypes(CombatState combatState, Player owner)
    {
        if (!CaptureCandidatesByCombat.TryGetValue(combatState, out var cardsByOwner)
            || !cardsByOwner.TryGetValue(owner, out var type))
            return [];

        return [type];
    }

    public static IReadOnlyList<CardModel> CaptureCandidateTemplates(Player owner) =>
        CaptureCandidateTypes(owner)
            .Select(CardTemplate)
            .ToList();

    public static CardModel CreateCleanClearCard(Player owner, Type type)
    {
        if (!ClearCardTypes.Contains(type))
            throw new ArgumentException($"{type.Name} is not a Clear Card.", nameof(type));

        var card = owner.RunState.CreateCard(CardTemplate(type), owner);
        card.RemovePlaybackStateExceptRelease();
        card.RemoveRelease();
        return card;
    }

    public static void ClearPendingCaptureCandidates(Player owner) =>
        PendingCaptureCandidatesByPlayer.Remove(owner);

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
            var selected = await SelectFromCards(source, context, choices, cancelable);
            return selected?.CreateClone();
        }
        finally
        {
            RemoveDetachedGeneratedChoices(choices);
        }
    }

    public static IReadOnlyList<CardModel> CardsPlayedLastTurn(CombatState? combatState, Player? owner)
    {
        if (combatState is null || owner is null)
            return [];

        if (!PlayedCardsByCombat.TryGetValue(combatState, out var cardsByOwner)
            || !cardsByOwner.TryGetValue(owner, out var memory))
            return [];

        List<CardModel> cards = [];
        HashSet<string> seenKeys = [];
        foreach (var entry in memory.LastTurn)
        {
            if (seenKeys.Add(entry.Key))
                cards.Add(entry.Snapshot.CreateClone());
        }

        return cards;
    }

    private static CardModel CreatePlaybackSnapshot(CardModel card)
    {
        return card.CreateClone();
    }

    private static string CreatePlaybackKey(CardModel card)
    {
        var modifiers = string.Join(
            ',',
            CardModifier.Modifiers(card)
                .Select(modifier => modifier.GetType().FullName)
                .OrderBy(name => name));
        var keywords = string.Join(',', card.Keywords.OrderBy(keyword => keyword.ToString()));
        var tags = string.Join(',', card.Tags.OrderBy(tag => tag.ToString()));
        var dynamicVars = string.Join(
            ',',
            card.DynamicVars.OrderBy(pair => pair.Key)
                .Select(pair => $"{pair.Key}:{pair.Value.IntValue}"));
        var enchantment = card.Enchantment is null ? "" : $"{card.Enchantment.Id}:{card.Enchantment.Amount}";
        var affliction = card.Affliction is null ? "" : $"{card.Affliction.Id}:{card.Affliction.Amount}";

        return string.Join(
            '\u001F',
            card.Id.Entry,
            card.CurrentUpgradeLevel.ToString(),
            card.EnergyCost.CostsX ? "X" : card.EnergyCost.GetResolved().ToString(),
            card.HasStarCostX ? "X" : card.CurrentStarCost.ToString(),
            card.GetEnchantedReplayCount().ToString(),
            modifiers,
            keywords,
            tags,
            dynamicVars,
            enchantment,
            affliction,
            card.IsTemporary().ToString());
    }

    public static IReadOnlyList<CardModel> CardsPlayedThisTurn(Player owner, CardModel? excludedCard = null)
    {
        return FinishedCardPlayCardsThisTurn(owner, excludedCard)
            .Distinct()
            .ToList();
    }

    public static int CardPlayCountThisTurn(Player owner, Func<CardModel, bool> predicate, CardModel? excludedCard = null) =>
        FinishedCardPlayCardsThisTurn(owner, excludedCard).Count(predicate);

    public static int CardPlayCountThisCombat(Player owner, Func<CardModel, bool> predicate, CardPlay? excludedPlay = null)
    {
        var combatState = owner.Creature.CombatState;
        if (combatState is null
            || !PlayedCardsByCombat.TryGetValue(combatState, out var cardsByOwner)
            || !cardsByOwner.TryGetValue(owner, out var memory))
            return 0;

        return memory.AllCombat
            .Where(entry => entry.Play != excludedPlay)
            .Select(entry => entry.Snapshot)
            .Count(predicate);
    }

    private static IEnumerable<CardModel> FinishedCardPlayCardsThisTurn(Player owner, CardModel? excludedCard)
    {
        var combatState = owner.Creature.CombatState;
        if (combatState is null)
            return [];

        return CombatManager.Instance.History.CardPlaysFinished
            .Where(entry => entry.HappenedThisTurn(combatState) && entry.CardPlay.Card.Owner == owner)
            .Select(entry => entry.CardPlay.Card)
            .Where(card => card != excludedCard);
    }

    public static async Task RecordReleaseGainThisTurn(CardModel card)
    {
        if (card.Owner is not { } owner)
            return;

        var power = owner.Creature.GetPower<SakuraReleaseCountThisTurnPower>();
        if (power is null)
        {
            power = await PowerCmd.Apply<SakuraReleaseCountThisTurnPower>(owner.Creature, 1, owner.Creature, null, false);
            power?.TryMarkCounted(card);
            return;
        }

        if (power.TryMarkCounted(card))
            await PowerCmd.Apply<SakuraReleaseCountThisTurnPower>(owner.Creature, 1, owner.Creature, null, false);
    }

    public static async Task ReleaseThisTurnAndRecord(CardModel card)
    {
        var wasReleased = card.IsReleased();
        card.ReleaseThisTurn();
        if (!wasReleased)
            await RecordReleaseGainThisTurn(card);
    }

    public static async Task ReleaseAndRecord(CardModel card)
    {
        var wasReleased = card.IsReleased();
        card.Release();
        if (!wasReleased)
            await RecordReleaseGainThisTurn(card);
    }

    public static async Task<CardModel?> SelectOrRandomUnreleasedClearCardInHand(
        PlayerChoiceContext context,
        Player owner,
        bool choose)
    {
        var choices = CardPile.GetCards(owner, PileType.Hand)
            .Where(card => IsClearCard(card) && !card.IsReleased())
            .ToList();
        if (choices.Count == 0)
            return null;

        if (!choose)
            return owner.RunState.Rng.CombatCardSelection.NextItem(choices);

        var selected = await CardSelectCmd.FromSimpleGrid(
            context,
            choices,
            owner,
            new CardSelectorPrefs(CardPrompt, 1)
            {
                Cancelable = false,
                RequireManualConfirmation = false
            });

        return selected.FirstOrDefault();
    }

    public static async Task ReduceCostThisTurn(SakuraModCard source, CardModel card, int amount = 1)
    {
        if (amount <= 0)
            return;

        var power = source.Owner.Creature.GetPower<SakuraCostReductionPower>()
                    ?? await PowerCmd.Apply<SakuraCostReductionPower>(source.Owner.Creature, amount, source.Owner.Creature, source, false);
        power?.AddTarget(card);
    }

    public static async Task ReduceCostUntilPlayed(SakuraModCard source, CardModel card, int amount = 1)
    {
        if (amount <= 0)
            return;

        var power = source.Owner.Creature.GetPower<SakuraCostReductionUntilPlayedPower>()
                    ?? await PowerCmd.Apply<SakuraCostReductionUntilPlayedPower>(source.Owner.Creature, amount, source.Owner.Creature, source, false);
        power?.AddTarget(card);
    }

    private static ManifestChoiceSet ManifestChoices(Player owner, Type? excludedType, ManifestSource source)
    {
        var captureEligible = source == ManifestSource.RareAtlas || owner.GetRelic<NamelessBookTruth>() is null;
        var weightedSources = source switch
        {
            ManifestSource.RareAtlas => WeightedManifestAtlasSources(card => card.Rarity == CardRarity.Rare),
            _ when owner.GetRelic<NamelessBookTruth>() is not null => CardPile.GetCards(owner, PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust)
                .Where(IsManifestableClearCard)
                .ToList(),
            _ => WeightedManifestAtlasSources(IsDefaultManifestAtlasSource)
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
        ManifestableClearCardTypes
            .Select(CardTemplate)
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

    private static bool IsDefaultManifestAtlasSource(CardModel card) =>
        !DefaultManifestExcludedClearCardTypes.Contains(card.GetType());

    public static bool IsClearCard(CardModel card) =>
        ClearCardTypes.Contains(card.GetType());

    public static bool IsManifestableClearCard(CardModel card) =>
        IsClearCard(card);

    public static bool IsSupportCard(CardModel card) =>
        card is SakuraModCard && !IsClearCard(card);

    public static IReadOnlyList<CardModel> RewardableSupportCardTemplates(Player owner) =>
        ModelDb.CardPool<SakuraModCardPool>()
            .GetUnlockedCards(owner.UnlockState, owner.RunState.CardMultiplayerConstraint)
            .Where(IsSupportCard)
            .Where(card => card.Rarity is not (CardRarity.Basic or CardRarity.Ancient or CardRarity.Event or CardRarity.Token))
            .ToList();

    public static bool HasManifestedThisTurn(Player owner) =>
        owner.Creature.GetPower<SakuraManifestedThisTurnPower>() is not null;

    public static bool IntendsToAttack(Creature creature) =>
        creature.Monster?.NextMove.Intents.Any(intent => intent is AttackIntent) == true;

    public static async Task SuppressAliveEnemyActions(IEnumerable<Creature> enemies)
    {
        foreach (var enemy in enemies.Where(enemy => enemy.IsAlive).ToList())
            await CreatureCmd.Stun(enemy);
    }

    public static async Task Attack(
        PlayerChoiceContext context,
        SakuraModCard source,
        Creature target,
        decimal damage,
        ValueProp props = ValueProp.Move,
        int hitCount = 1)
    {
        await AttackCommand(source, target, damage, props, hitCount)
            .WithNoAttackerAnim()
            .Execute(context);
    }

    public static async Task Attack(
        PlayerChoiceContext context,
        SakuraModCard source,
        IEnumerable<Creature> targets,
        decimal damage,
        ValueProp props = ValueProp.Move,
        int hitCount = 1)
    {
        var targetList = targets.ToList();
        if (targetList.Count == 0)
            return;

        await DamageCmd.Attack(damage)
            .WithHitCount(hitCount)
            .FromCard(source)
            .WithValueProp(AttackProps(source, props))
            .WithNoAttackerAnim()
            .TargetingFiltered(targetList)
            .Execute(context);
    }

    public static AttackCommand AttackCommand(
        SakuraModCard source,
        Creature target,
        decimal damage,
        ValueProp props = ValueProp.Move,
        int hitCount = 1,
        string? vfx = null,
        string? sfx = null,
        string? tmpSfx = null) =>
        DamageCmd.Attack(damage)
            .WithHitCount(hitCount)
            .FromCard(source)
            .WithValueProp(AttackProps(source, props))
            .Targeting(target)
            .WithHitFx(vfx, sfx, tmpSfx);

    public static AttackCommand AttackCommand(
        SakuraModCard source,
        Creature target,
        int hitCount = 1,
        string? vfx = null,
        string? sfx = null,
        string? tmpSfx = null) =>
        AttackCommand(source, target, source.DynamicVars.Damage.IntValue, source.DynamicVars.Damage.Props, hitCount, vfx, sfx, tmpSfx);

    private static ValueProp AttackProps(SakuraModCard source, ValueProp props) =>
        LucidPiercePower.ShouldPierce(source.Owner.Creature, source)
            ? props | ValueProp.Unblockable
            : props;

    public static bool TryGetElement(CardModel card, out SakuraElement element)
    {
        var elements = ElementsOf(card).ToList();
        element = elements.FirstOrDefault();
        return elements.Count > 0;
    }

    public static SakuraElementSet ElementSetOf(CardModel card) =>
        ElementSetFromKeywords(card.Keywords) | card.TemporaryElementSet();

    public static SakuraElementSet StaticElementSetOf(CardModel card) =>
        ElementSetFromKeywords(card.CanonicalKeywords);

    public static IReadOnlyList<SakuraElement> ElementsOf(CardModel card) =>
        ElementSetOf(card).AsElements().ToList();

    public static IReadOnlyList<SakuraElement> PlayedElementsThisTurn(Player owner)
    {
        return SakuraElementSets.AllElements
            .SelectMany(element => Enumerable.Repeat(element, PlayedElementAmount(owner, element)))
            .ToList();
    }

    public static IReadOnlyList<SakuraElement> PlayedElementTypesThisTurn(Player owner) =>
        SakuraElementSets.AllElements
            .Where(element => PlayedElementAmount(owner, element) > 0)
            .ToList();

    public static bool HasPlayedElementThisTurn(Player owner) =>
        PlayedElementTypesThisTurn(owner).Count > 0;

    public static async Task TriggerTalismanEffect(
        PlayerChoiceContext choiceContext,
        Player owner,
        SakuraElement element,
        CardPlay play,
        CardModel? source)
    {
        var target = TalismanTarget(owner, play);
        switch (element)
        {
            case SakuraElement.Wind:
                await CardPileCmd.Draw(choiceContext, SyaoranBondPower.WindDraw, owner, false);
                break;
            case SakuraElement.Water:
                if (target is not null)
                    await PowerCmd.Apply<WeakPower>(target, SyaoranBondPower.WaterWeak, owner.Creature, source, false);
                break;
            case SakuraElement.Fire:
                if (target is not null)
                    await CreatureCmd.Damage(choiceContext, target, SyaoranBondPower.FireDamage, ValueProp.Move, owner.Creature, source);
                break;
            case SakuraElement.Earth:
                await CreatureCmd.GainBlock(owner.Creature, SyaoranBondPower.EarthBlock, ValueProp.Move, play, false);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(element), element, null);
        }
    }

    private static Creature? TalismanTarget(Player owner, CardPlay play)
    {
        if (IsValidTalismanTarget(owner, play.Target))
            return play.Target;

        var targets = owner.Creature.CombatState?.HittableEnemies
            .Where(enemy => enemy.IsAlive)
            .ToList();
        return targets is { Count: > 0 }
            ? owner.RunState.Rng.CombatTargets.NextItem(targets)
            : null;
    }

    private static bool IsValidTalismanTarget(Player owner, Creature? target) =>
        target is { IsAlive: true }
        && owner.Creature.CombatState?.HittableEnemies.Contains(target) == true;

    public static async Task RememberPlayedElements(CardPlay play)
    {
        var card = play.Card;
        if (card?.Owner is not { } owner || card.CombatState is null)
            return;

        var elements = ElementSetOf(card);
        if (elements == SakuraElementSet.None)
            return;

        var entriesByOwner = PlayedElementsByCombat.GetValue(card.CombatState, _ => []);
        if (!entriesByOwner.TryGetValue(owner, out var entries))
        {
            entries = [];
            entriesByOwner[owner] = entries;
        }

        var existing = entries.LastOrDefault(entry => ReferenceEquals(entry.Play, play));
        if (existing is not null)
        {
            var addedElements = elements & ~existing.Elements;
            if (addedElements == SakuraElementSet.None)
                return;

            existing.Elements |= addedElements;
            await ApplyPlayedElementPowers(owner, addedElements);
            return;
        }

        entries.Add(new PlayedElementEntry(play, card, elements));
        await ApplyPlayedElementPowers(owner, elements);
    }

    public static void ClearPlayedElements(Player owner)
    {
        if (owner.Creature.CombatState is not null && PlayedElementsByCombat.TryGetValue(owner.Creature.CombatState, out var entriesByOwner))
            entriesByOwner.Remove(owner);
    }

    public static SakuraElement RandomElement(Player owner) =>
        owner.RunState.Rng.CombatCardSelection.NextItem(SakuraElementSets.AllElements);

    public static SakuraElementSet GrantElementsThisTurn(CardModel card, SakuraElementSet elements) =>
        card.GrantElementsThisTurn(elements);

    private static int PlayedElementAmount(Player owner, SakuraElement element) =>
        element switch
        {
            SakuraElement.Wind => owner.Creature.GetPower<WindElementPower>()?.Amount ?? 0,
            SakuraElement.Water => owner.Creature.GetPower<WaterElementPower>()?.Amount ?? 0,
            SakuraElement.Fire => owner.Creature.GetPower<FireElementPower>()?.Amount ?? 0,
            SakuraElement.Earth => owner.Creature.GetPower<EarthElementPower>()?.Amount ?? 0,
            _ => 0
        };

    private static async Task ApplyPlayedElementPowers(Player owner, SakuraElementSet elements)
    {
        foreach (var element in elements.AsElements())
            await ApplyPlayedElementPower(owner, element);
    }

    private static Task ApplyPlayedElementPower(Player owner, SakuraElement element) =>
        element switch
        {
            SakuraElement.Wind => PowerCmd.Apply<WindElementPower>(owner.Creature, 1, owner.Creature, null, false),
            SakuraElement.Water => PowerCmd.Apply<WaterElementPower>(owner.Creature, 1, owner.Creature, null, false),
            SakuraElement.Fire => PowerCmd.Apply<FireElementPower>(owner.Creature, 1, owner.Creature, null, false),
            SakuraElement.Earth => PowerCmd.Apply<EarthElementPower>(owner.Creature, 1, owner.Creature, null, false),
            _ => Task.CompletedTask
        };

    public static CardKeyword KeywordFor(SakuraElement element) =>
        element switch
        {
            SakuraElement.Wind => SakuraKeywords.Wind,
            SakuraElement.Water => SakuraKeywords.Water,
            SakuraElement.Fire => SakuraKeywords.Fire,
            SakuraElement.Earth => SakuraKeywords.Earth,
            _ => throw new ArgumentOutOfRangeException(nameof(element), element, null)
        };

    private static SakuraElementSet ElementSetFromKeywords(IEnumerable<CardKeyword> keywords)
    {
        var elements = SakuraElementSet.None;
        foreach (var keyword in keywords)
            elements |= ElementSetFromKeyword(keyword);

        return elements;
    }

    private static SakuraElementSet ElementSetFromKeyword(CardKeyword keyword)
    {
        if (keyword == SakuraKeywords.Wind)
            return SakuraElementSet.Wind;
        if (keyword == SakuraKeywords.Water)
            return SakuraElementSet.Water;
        if (keyword == SakuraKeywords.Fire)
            return SakuraElementSet.Fire;
        if (keyword == SakuraKeywords.Earth)
            return SakuraElementSet.Earth;

        return SakuraElementSet.None;
    }

    private static CardModel CardTemplate(Type type) =>
        ModelDb.GetById<CardModel>(ModelDb.GetId(type));

    private static CardModel CloneForManifest(CardModel source) =>
        CloneToUpgradeLevel(source, source.CurrentUpgradeLevel);

    private static CardModel CloneToUpgradeLevel(CardModel source, int upgradeLevel)
    {
        var copy = source.CreateClone();
        UpgradeToLevel(copy, upgradeLevel);
        return copy;
    }

    public static T CloneWithCurrentUpgrade<T>(CardModel source) where T : CardModel
    {
        var scope = source.CardScope
            ?? throw new InvalidOperationException($"Cannot create {typeof(T).Name} without a card scope.");
        var copy = scope.CreateCard<T>(source.Owner);
        while (copy.CurrentUpgradeLevel < source.CurrentUpgradeLevel && copy.IsUpgradable)
            copy.UpgradeInternal();
        return copy;
    }

    public static async Task<CardModel?> SelectHandCard(
        SakuraModCard source,
        PlayerChoiceContext context,
        Func<CardModel, bool> predicate,
        bool cancelable = true)
    {
        var selected = await CardSelectCmd.FromHand(
            context,
            source.Owner,
            new CardSelectorPrefs(HandPrompt, 1)
            {
                Cancelable = cancelable,
                RequireManualConfirmation = false
            },
            predicate,
            source);

        return selected.FirstOrDefault();
    }

    public static IReadOnlyList<CardModel> StabilizeCandidates(Player owner) =>
        CardPile.GetCards(owner, PileType.Hand, PileType.Discard)
            .Where(CanGenericStabilize)
            .ToList();

    public static IReadOnlyList<CardModel> StabilizeCandidates(SakuraModCard source) =>
        StabilizeCandidates(source.Owner);

    public static async Task<CardModel?> SelectStabilizeCandidate(
        SakuraModCard source,
        PlayerChoiceContext context,
        bool cancelable = true) =>
        await SelectFromCards(source, context, StabilizeCandidates(source), cancelable);

    private static bool CanGenericStabilize(CardModel card) =>
        card.IsTemporary() && card.CanStabilize();


    public static async Task<IReadOnlyList<CardModel>> SelectHandCards(
        SakuraModCard source,
        PlayerChoiceContext context,
        Func<CardModel, bool> predicate,
        int count,
        bool cancelable = true)
    {
        if (count <= 0)
            return [];

        if (Hand(source).Count(predicate) < count)
            return [];

        var selected = await CardSelectCmd.FromHand(
            context,
            source.Owner,
            new CardSelectorPrefs(HandPrompt, count)
            {
                Cancelable = cancelable,
                RequireManualConfirmation = false
            },
            predicate,
            source);

        return selected.ToList();
    }

    public static async Task<IReadOnlyList<CardModel>> SelectUpToHandCards(
        SakuraModCard source,
        PlayerChoiceContext context,
        Func<CardModel, bool> predicate,
        int count,
        bool cancelable = true)
    {
        List<CardModel> selected = [];

        while (selected.Count < count
               && Hand(source).Any(card => !selected.Contains(card) && predicate(card)))
        {
            var card = await SelectHandCard(
                source,
                context,
                card => !selected.Contains(card) && predicate(card),
                cancelable);
            if (card is null)
                break;

            selected.Add(card);
        }

        return selected;
    }

    public static CardModel? FirstHandCard(SakuraModCard source, Func<CardModel, bool> predicate) =>
        CardPile.Get(PileType.Hand, source.Owner)!.Cards.FirstOrDefault(predicate);

    public static IEnumerable<CardModel> Hand(SakuraModCard source) =>
        CardPile.Get(PileType.Hand, source.Owner)!.Cards;

    public static async Task<CardModel?> SelectFromCards(
        SakuraModCard source,
        PlayerChoiceContext context,
        IEnumerable<CardModel> cards,
        bool cancelable = true)
    {
        var choices = cards.ToList();
        if (choices.Count == 0)
            return null;

        try
        {
            if (choices.All(choice => choice is SakuraOptionCard) && choices.Count <= 3)
                return await CardSelectCmd.FromChooseACardScreen(context, choices, source.Owner, canSkip: cancelable);

            var selected = await CardSelectCmd.FromSimpleGrid(
                context,
                choices,
                source.Owner,
                new CardSelectorPrefs(CardPrompt, 1)
                {
                    Cancelable = cancelable,
                    RequireManualConfirmation = false
                });

            return selected.FirstOrDefault();
        }
        finally
        {
            RemoveDetachedOptionCards(choices);
        }
    }

    private static void RemoveDetachedOptionCards(IEnumerable<CardModel> choices)
    {
        foreach (var choice in choices)
        {
            if (choice is not SakuraOptionCard || choice.Pile is not null)
                continue;

            choice.CardScope?.RemoveCard(choice);
        }
    }

    public static async Task<CardModel?> SelectFromCardPreviews(
        SakuraModCard source,
        PlayerChoiceContext context,
        IReadOnlyList<CardModel> cards,
        bool cancelable = true)
    {
        if (cards.Count == 0)
            return null;

        var previews = cards.Select(card => card.CreateClone()).ToList();
        var selected = await SelectFromCards(source, context, previews, cancelable);
        if (selected is null)
            return null;

        var index = previews.IndexOf(selected);
        return index >= 0 ? cards[index] : null;
    }

    public static async Task<IReadOnlyList<CardModel>> SelectUpToFromCardPreviews(
        SakuraModCard source,
        PlayerChoiceContext context,
        IReadOnlyList<CardModel> cards,
        int count,
        bool cancelable = true,
        LocString? prompt = null,
        int? minSelect = null)
    {
        if (cards.Count == 0)
            return [];

        var previews = cards.Select(card => card.CreateClone()).ToList();
        var selected = await SelectUpToFromCards(source, context, previews, count, cancelable, prompt, minSelect);

        return selected
            .Select(preview => previews.IndexOf(preview))
            .Where(index => index >= 0)
            .Select(index => cards[index])
            .ToList();
    }

    public static async Task<IReadOnlyList<CardModel>> SelectUpToFromCards(
        SakuraModCard source,
        PlayerChoiceContext context,
        IEnumerable<CardModel> cards,
        int count,
        bool cancelable = true,
        LocString? prompt = null,
        int? minSelect = null)
    {
        var choices = cards.ToList();
        if (prompt is null && minSelect is null)
        {
            List<CardModel> orderedSelection = [];
            while (orderedSelection.Count < count && choices.Count > 0)
            {
                var card = await SelectFromCards(source, context, choices, cancelable);
                if (card is null)
                    break;

                orderedSelection.Add(card);
                choices.Remove(card);
            }

            return orderedSelection;
        }

        var maxSelect = Math.Min(count, choices.Count);
        if (maxSelect <= 0)
            return [];

        var requiredCount = minSelect ?? (cancelable ? 0 : maxSelect);
        var prefs = new CardSelectorPrefs(prompt ?? CardPrompt, Math.Clamp(requiredCount, 0, maxSelect), maxSelect)
        {
            Cancelable = cancelable
        };

        try
        {
            var selected = await CardSelectCmd.FromSimpleGrid(context, choices, source.Owner, prefs);
            return selected.ToList();
        }
        finally
        {
            RemoveDetachedOptionCards(choices);
        }
    }

    public static async Task MoveExistingCardToHand(SakuraModCard source, CardModel card) =>
        await CardPileCmd.Add(card, PileType.Hand, CardPilePosition.Random, source, skipVisuals: false);

    public static async Task<bool> GrantTemporary(PlayerChoiceContext context, CardModel card)
    {
        if (card.IsTemporary())
            return false;

        card.MakeTemporary();
        await TriggerTemporaryGranted(context, card);
        return true;
    }

    public static async Task TriggerTemporaryStabilized(PlayerChoiceContext context, CardModel card)
    {
        RememberCaptureCandidate(card);

        if (card.Owner?.Creature.GetPower<GrowingMagicPower>() is { } growingMagic)
            await growingMagic.AfterTemporaryStabilized(context);
        if (card.Owner?.Creature.GetPower<NewPagePower>() is { } newPage)
            await newPage.AfterTemporaryStabilized(context);
        if (card.Owner?.Creature.GetPower<NewPageBlockPower>() is { } newPageBlock)
            await newPageBlock.AfterTemporaryStabilized(context);
        if (card.Owner?.GetRelic<StorageRibbon>() is { } storageRibbon)
            await storageRibbon.AfterTemporaryStabilized(context, card);
    }

    private static void RememberCaptureCandidate(CardModel card)
    {
        if (card.Owner is not { } owner
            || card.CombatState is null
            || !IsClearCard(card)
            || !card.IsManifestAtlasOrigin())
            return;

        var cardsByOwner = CaptureCandidatesByCombat.GetValue(card.CombatState, _ => []);
        cardsByOwner.TryAdd(owner, card.GetType());
    }

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

    public static bool TryExchangeEnergyCosts(CardModel first, CardModel second, bool restOfCombat)
    {
        if (!TryGetExchangeableEnergyCost(first, out var firstCost)
            || !TryGetExchangeableEnergyCost(second, out var secondCost))
            return false;

        if (restOfCombat)
        {
            first.EnergyCost.SetThisCombat(secondCost);
            second.EnergyCost.SetThisCombat(firstCost);
        }
        else
        {
            first.EnergyCost.SetThisTurn(secondCost);
            second.EnergyCost.SetThisTurn(firstCost);
        }

        return true;
    }

    private static bool TryGetExchangeableEnergyCost(CardModel card, out int cost)
    {
        if (card.EnergyCost.CostsX)
        {
            cost = 0;
            return false;
        }

        cost = card.EnergyCost.GetResolved();
        return cost >= 0;
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

        await CardPileCmd.AddGeneratedCardToCombat(
            card,
            options.Pile ?? PileType.Hand,
            true,
            options.Position ?? CardPilePosition.Random);
        if (temporaryGranted && context is not null)
            await TriggerTemporaryGranted(context, card);
        if (options.AddRelease)
            await ReleaseAndRecord(card);
        return card;
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
            var chosen = await SelectFromCards(source, context, choices, cancelable: false);
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

    public static IReadOnlyList<CardModel> PartnerTemplates() =>
        PartnerCardTypes.Select(CardTemplate).ToList();

    public static bool IsPartner(CardModel card) =>
        PartnerCardTypeSet.Contains(card.GetType());

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

    private enum ManifestSource
    {
        Default,
        RareAtlas
    }

    private sealed record ManifestChoiceSet(IReadOnlyList<CardModel> Cards, bool CaptureEligible);

    private sealed class PlayedElementEntry(CardPlay play, CardModel card, SakuraElementSet elements)
    {
        public CardPlay Play { get; } = play;
        public CardModel Card { get; } = card;
        public SakuraElementSet Elements { get; set; } = elements;
    }

    private sealed class PlayedCardSnapshot(CardPlay play, CardModel snapshot, string key)
    {
        public CardPlay Play { get; } = play;
        public CardModel Snapshot { get; } = snapshot;
        public string Key { get; } = key;
    }

    private sealed class CardPlaybackMemory
    {
        public List<PlayedCardSnapshot> LastTurn { get; set; } = [];
        public List<PlayedCardSnapshot> CurrentTurn { get; set; } = [];
        public List<PlayedCardSnapshot> AllCombat { get; } = [];
        public HashSet<CardPlay> RecordedPlays { get; } = [];
        public bool PlayedReleasedThisTurn { get; set; }
        public bool TurnOpen { get; set; }
    }

    private static CardPlaybackMemory GetCardPlaybackMemory(CombatState combatState, Player owner)
    {
        var cardsByOwner = PlayedCardsByCombat.GetValue(combatState, _ => []);
        if (!cardsByOwner.TryGetValue(owner, out var memory))
        {
            memory = new CardPlaybackMemory();
            cardsByOwner[owner] = memory;
        }

        return memory;
    }

}
