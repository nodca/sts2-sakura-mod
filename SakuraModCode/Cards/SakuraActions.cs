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
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Ui;
using System.Runtime.CompilerServices;

namespace SakuraMod.SakuraModCode.Cards;

public static class SakuraActions
{
    private static readonly LocString HandPrompt = new("cards", "SAKURAMOD-GENERIC.handPrompt");
    private static readonly LocString CardPrompt = new("cards", "SAKURAMOD-GENERIC.cardPrompt");
    private static readonly ConditionalWeakTable<ICombatState, Dictionary<Player, List<PlayedElementEntry>>> PlayedElementsByCombat = new();
    private static readonly ConditionalWeakTable<ICombatState, Dictionary<Player, CardPlaybackMemory>> PlayedCardsByCombat = new();

    public static int ReleaseGainCountThisTurn(Player owner) =>
        owner.Creature.GetPower<SakuraReleaseCountThisTurnPower>()?.Amount ?? 0;

    public static bool HasPlayedReleasedCardEarlierThisTurn(Player owner) =>
        owner.Creature.CombatState is not null
        && PlayedCardsByCombat.TryGetValue(owner.Creature.CombatState, out var cardsByOwner)
        && cardsByOwner.TryGetValue(owner, out var memory)
        && memory.PlayedReleasedThisTurn;

    public static void BeginPlayerTurn(Player player, ICombatState combatState)
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
        SakuraElementCompass.OnTurnReset(player);

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

    public static IReadOnlyList<CardModel> CardsPlayedLastTurn(ICombatState? combatState, Player? owner)
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

    public static int DistinctCardTypesPlayedThisTurn(Player owner, Func<CardModel, bool> predicate, CardModel? excludedCard = null) =>
        FinishedCardPlayCardsThisTurn(owner, excludedCard)
            .Where(predicate)
            .Select(card => card.GetType())
            .Distinct()
            .Count();

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

    public static async Task RecordReleaseGainThisTurn(PlayerChoiceContext choiceContext, CardModel card)
    {
        if (card.Owner is not { } owner)
            return;

        var power = owner.Creature.GetPower<SakuraReleaseCountThisTurnPower>();
        if (power is null)
        {
            power = await PowerCmd.Apply<SakuraReleaseCountThisTurnPower>(choiceContext, owner.Creature, 1, owner.Creature, null, false);
            power?.TryMarkCounted(card);
            return;
        }

        if (power.TryMarkCounted(card))
            await PowerCmd.Apply<SakuraReleaseCountThisTurnPower>(choiceContext, owner.Creature, 1, owner.Creature, null, false);
    }

    public static async Task ReleaseThisTurnAndRecord(PlayerChoiceContext choiceContext, CardModel card)
    {
        var wasReleased = card.IsReleased();
        card.ReleaseThisTurn();
        if (!wasReleased)
        {
            await RecordReleaseGainThisTurn(choiceContext, card);
        }
    }

    public static async Task ReleaseAndRecord(PlayerChoiceContext choiceContext, CardModel card)
    {
        var wasReleased = card.IsReleased();
        card.Release();
        if (!wasReleased)
        {
            await RecordReleaseGainThisTurn(choiceContext, card);
        }
    }

    public static async Task<CardModel?> SelectOrRandomUnreleasedClearCardInHand(
        PlayerChoiceContext context,
        Player owner,
        bool choose)
    {
        var choices = CardPile.GetCards(owner, PileType.Hand)
            .Where(card => SakuraCardCatalog.IsTransparentCard(card) && !card.IsReleased())
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

    public static async Task ReduceCostThisTurn(PlayerChoiceContext choiceContext, SakuraModCard source, CardModel card, int amount = 1)
    {
        if (amount <= 0)
            return;

        var power = source.Owner.Creature.GetPower<SakuraCostReductionPower>()
                    ?? await PowerCmd.Apply<SakuraCostReductionPower>(choiceContext, source.Owner.Creature, amount, source.Owner.Creature, source, false);
        power?.AddTarget(card);
    }

    public static async Task ReduceCostUntilPlayed(PlayerChoiceContext choiceContext, SakuraModCard source, CardModel card, int amount = 1)
    {
        if (amount <= 0)
            return;

        var power = source.Owner.Creature.GetPower<SakuraCostReductionUntilPlayedPower>()
                    ?? await PowerCmd.Apply<SakuraCostReductionUntilPlayedPower>(choiceContext, source.Owner.Creature, amount, source.Owner.Creature, source, false);
        power?.AddTarget(card);
    }

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
        Creature target,
        CalculatedDamageVar damage,
        int hitCount = 1)
    {
        await DamageCmd.Attack(damage)
            .WithHitCount(hitCount)
            .FromCard(source)
            .WithValueProp(AttackProps(source, damage.Props))
            .WithNoAttackerAnim()
            .Targeting(target)
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

    public static async Task Attack(
        PlayerChoiceContext context,
        SakuraModCard source,
        IEnumerable<Creature> targets,
        CalculatedDamageVar damage,
        int hitCount = 1)
    {
        var targetList = targets.ToList();
        if (targetList.Count == 0)
            return;

        await DamageCmd.Attack(damage)
            .WithHitCount(hitCount)
            .FromCard(source)
            .WithValueProp(AttackProps(source, damage.Props))
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

    internal static ValueProp AttackProps(SakuraModCard source, ValueProp props) =>
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

    public static bool TryGetLastPlayedElement(Player owner, out SakuraElement element)
    {
        element = default;
        if (owner.Creature.CombatState is null
            || !PlayedElementsByCombat.TryGetValue(owner.Creature.CombatState, out var entriesByOwner)
            || !entriesByOwner.TryGetValue(owner, out var entries))
            return false;

        if (entries.Count == 0)
            return false;

        element = entries[^1].LastElement;
        return true;
    }

    public static bool HasPlayedElementThisTurn(Player owner) =>
        PlayedElementTypesThisTurn(owner).Count > 0;

    public static async Task TriggerTalismanEffect(
        PlayerChoiceContext choiceContext,
        Player owner,
        SakuraElement element,
        CardPlay play,
        CardModel? source)
    {
        switch (element)
        {
            case SakuraElement.Wind:
                await CardPileCmd.Draw(choiceContext, SyaoranBondPower.WindDraw, owner, false);
                break;
            case SakuraElement.Water:
                foreach (var enemy in TalismanTargets(owner))
                    await CreatureCmd.Damage(choiceContext, enemy, SyaoranBondPower.WaterDamage, ValueProp.Move, owner.Creature, source);
                break;
            case SakuraElement.Fire:
                if (RandomTalismanTarget(owner) is { } fireTarget)
                    await CreatureCmd.Damage(choiceContext, fireTarget, SyaoranBondPower.FireDamage, ValueProp.Move, owner.Creature, source);
                break;
            case SakuraElement.Earth:
                await CreatureCmd.GainBlock(owner.Creature, SyaoranBondPower.EarthBlock, ValueProp.Move, play, false);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(element), element, null);
        }
    }

    private static IReadOnlyList<Creature> TalismanTargets(Player owner) =>
        owner.Creature.CombatState?.HittableEnemies
            .Where(enemy => enemy.IsAlive)
            .ToList()
        ?? [];

    private static Creature? RandomTalismanTarget(Player owner)
    {
        var targets = TalismanTargets(owner);
        return targets.Count > 0
            ? owner.RunState.Rng.CombatTargets.NextItem(targets)
            : null;
    }

    public static async Task RememberPlayedElements(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var card = play.Card;
        if (card?.Owner is not { } owner || card.CombatState is null)
            return;

        var elements = ElementSetOf(card);
        if (elements == SakuraElementSet.None)
            return;

        var entries = PlayedElementEntries(card.CombatState, owner);

        var existing = entries.LastOrDefault(entry => ReferenceEquals(entry.Play, play));
        if (existing is not null)
        {
            var addedElements = elements & ~existing.Elements;
            if (addedElements == SakuraElementSet.None)
                return;

            existing.Elements |= addedElements;
            existing.LastElement = LastElementOf(addedElements);
            entries.Remove(existing);
            entries.Add(existing);
            await ApplyPlayedElementPowers(choiceContext, owner, addedElements);
            return;
        }

        entries.Add(new PlayedElementEntry(play, elements, LastElementOf(elements)));
        await ApplyPlayedElementPowers(choiceContext, owner, elements);
    }

    public static void ClearPlayedElements(Player owner)
    {
        if (owner.Creature.CombatState is not null && PlayedElementsByCombat.TryGetValue(owner.Creature.CombatState, out var entriesByOwner))
            entriesByOwner.Remove(owner);
    }

    public static SakuraElement RandomElement(Player owner) =>
        owner.RunState.Rng.CombatCardSelection.NextItem(SakuraElementSets.AllElements);

    public static bool TryRandomMissingElement(Player owner, CardModel card, out SakuraElement element)
    {
        var missing = SakuraElementSets.AllElements
            .Where(candidate => !ElementSetOf(card).HasElement(candidate))
            .ToList();
        element = missing.Count > 0
            ? owner.RunState.Rng.CombatCardSelection.NextItem(missing)
            : default;
        return missing.Count > 0;
    }

    public static SakuraElementSet GrantElementsThisTurn(CardModel card, SakuraElementSet elements) =>
        card.GrantElementsThisTurn(elements);

    public static async Task PutSyaoranTalismanInHand(Player owner, bool upgraded, AbstractModel? source = null)
    {
        var hand = CardPile.Get(PileType.Hand, owner)?.Cards;
        if (hand?.Any(IsSyaoranTalisman) == true)
            return;

        var card = FindSyaoranTalisman(owner, PileType.Draw)
            ?? FindSyaoranTalisman(owner, PileType.Discard);
        if (card is null)
        {
            var combatState = owner.Creature.CombatState
                ?? throw new InvalidOperationException("Cannot create SyaoranTalisman without an active combat state.");
            card = combatState.CreateCard<SyaoranTalisman>(owner);
            UpgradeForSyaoranBond(card, upgraded);
            await SakuraManifestLoop.AddGeneratedCardToHand(card);
            return;
        }

        UpgradeForSyaoranBond(card, upgraded);
        await MoveExistingCardToHand(source, card);
    }

    private static bool IsSyaoranTalisman(CardModel card) =>
        card is SyaoranTalisman;

    private static CardModel? FindSyaoranTalisman(Player owner, PileType pileType) =>
        CardPile.Get(pileType, owner)?.Cards.FirstOrDefault(IsSyaoranTalisman);

    private static void UpgradeForSyaoranBond(CardModel card, bool upgraded)
    {
        if (!upgraded)
            return;

        while (card.CurrentUpgradeLevel < 1 && card.IsUpgradable)
            card.UpgradeInternal();
    }

    private static int PlayedElementAmount(Player owner, SakuraElement element) =>
        element switch
        {
            SakuraElement.Wind => owner.Creature.GetPower<WindElementPower>()?.Amount ?? 0,
            SakuraElement.Water => owner.Creature.GetPower<WaterElementPower>()?.Amount ?? 0,
            SakuraElement.Fire => owner.Creature.GetPower<FireElementPower>()?.Amount ?? 0,
            SakuraElement.Earth => owner.Creature.GetPower<EarthElementPower>()?.Amount ?? 0,
            _ => 0
        };

    public static int PlayedElementCount(Player owner, SakuraElement element) =>
        PlayedElementAmount(owner, element);

    public static async Task GainElementCountThisTurn(PlayerChoiceContext choiceContext, Player owner, SakuraElement element, int amount)
    {
        if (amount <= 0)
            return;

        for (var i = 0; i < amount; i++)
            await ApplyPlayedElementPower(choiceContext, owner, element);

        RecordPlayedElement(owner, element);
        SakuraElementCompass.OnElementsPlayed(owner);
    }

    private static void RecordPlayedElement(Player owner, SakuraElement element)
    {
        if (owner.Creature.CombatState is null)
            return;

        PlayedElementEntries(owner.Creature.CombatState, owner)
            .Add(new PlayedElementEntry(null, element.ToSet(), element));
    }

    private static List<PlayedElementEntry> PlayedElementEntries(ICombatState combatState, Player owner)
    {
        var entriesByOwner = PlayedElementsByCombat.GetValue(combatState, _ => []);
        if (entriesByOwner.TryGetValue(owner, out var entries))
            return entries;

        entries = [];
        entriesByOwner[owner] = entries;
        return entries;
    }

    private static async Task ApplyPlayedElementPowers(PlayerChoiceContext choiceContext, Player owner, SakuraElementSet elements)
    {
        foreach (var element in elements.AsElements())
            await ApplyPlayedElementPower(choiceContext, owner, element);

        SakuraElementCompass.OnElementsPlayed(owner);
    }

    private static Task ApplyPlayedElementPower(PlayerChoiceContext choiceContext, Player owner, SakuraElement element) =>
        element switch
        {
            SakuraElement.Wind => PowerCmd.Apply<WindElementPower>(choiceContext, owner.Creature, 1, owner.Creature, null, false),
            SakuraElement.Water => PowerCmd.Apply<WaterElementPower>(choiceContext, owner.Creature, 1, owner.Creature, null, false),
            SakuraElement.Fire => PowerCmd.Apply<FireElementPower>(choiceContext, owner.Creature, 1, owner.Creature, null, false),
            SakuraElement.Earth => PowerCmd.Apply<EarthElementPower>(choiceContext, owner.Creature, 1, owner.Creature, null, false),
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

    private static SakuraElement LastElementOf(SakuraElementSet elements)
    {
        SakuraElement? last = null;
        foreach (var element in elements.AsElements())
            last = element;

        return last ?? throw new ArgumentException("Element set must contain at least one element.", nameof(elements));
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

    public static async Task MoveExistingCardToHand(AbstractModel? source, CardModel card) =>
        await CardPileCmd.Add(card, PileType.Hand, CardPilePosition.Random, source, skipVisuals: false);

    public static async Task MoveExistingCardToPileWithoutVisuals(
        AbstractModel? source,
        CardModel card,
        PileType pileType,
        CardPilePosition position)
    {
        var result = await CardPileCmd.Add(card, pileType, position, source, skipVisuals: true);
        if (result.success && result.cardAdded.Pile is { IsCombatPile: true } pile)
            pile.InvokeCardAddFinished();
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

    private sealed class PlayedElementEntry(CardPlay? play, SakuraElementSet elements, SakuraElement lastElement)
    {
        public CardPlay? Play { get; } = play;
        public SakuraElementSet Elements { get; set; } = elements;
        public SakuraElement LastElement { get; set; } = lastElement;
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

    private static CardPlaybackMemory GetCardPlaybackMemory(ICombatState combatState, Player owner)
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
