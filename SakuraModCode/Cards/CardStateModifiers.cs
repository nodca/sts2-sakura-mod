using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.Relics;
using System.Runtime.CompilerServices;

namespace SakuraMod.SakuraModCode.Cards;

public sealed class ReleaseModifier : CardModifier
{
    public override void OnInitialApplication() =>
        Owner?.NotifyReleaseStateChanged();

    public override void LoadSaveData(ModifierSave save) =>
        Owner?.NotifyReleaseStateChanged();

    public override void ModifyDescriptionPost(MegaCrit.Sts2.Core.Entities.Creatures.Creature? target, ref string description)
    {
        if (!description.Contains("[gold]Released[/gold]") && !description.Contains("[gold]已解封[/gold]"))
            description += SakuraStateText.ReleaseLine();
    }
}

public sealed class ReleaseThisTurnModifier : CardModifier
{
    public override bool ShouldReceiveCombatHooks => true;

    public override void OnInitialApplication() =>
        Owner?.NotifyReleaseStateChanged();

    public override void LoadSaveData(ModifierSave save) =>
        Owner?.NotifyReleaseStateChanged();

    public override void ModifyDescriptionPost(MegaCrit.Sts2.Core.Entities.Creatures.Creature? target, ref string description)
    {
        if (!description.Contains("[gold]Released[/gold]") && !description.Contains("[gold]已解封[/gold]"))
            description += SakuraStateText.ReleaseLine();
    }

    public override Task AfterTurnEnd(PlayerChoiceContext choiceContext, CombatSide side)
    {
        if (Owner?.Owner?.Creature.Side == side)
        {
            var card = Owner;
            CardModifier.RemoveModifier(card, this);
            card.NotifyReleaseStateChanged();
        }

        return Task.CompletedTask;
    }
}

public sealed class SynchronizedCardPairModifier : CardModifier
{
    private static readonly HashSet<SynchronizedPairKey> ResolvingPairs = [];
    private readonly List<CardModel> _partners = [];

    public override bool ShouldReceiveCombatHooks => true;

    public void AddPartner(CardModel partner)
    {
        if (Owner is null || partner == Owner || _partners.Contains(partner))
            return;

        _partners.Add(partner);
    }

    public override void ModifyDescriptionPost(MegaCrit.Sts2.Core.Entities.Creatures.Creature? target, ref string description)
    {
        if (Owner is not { } card)
            return;

        if (SakuraStateText.SynchronizedLine(card) is { } line)
            description += line;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (play.Card != Owner || Owner is null)
            return;

        var card = Owner;
        var partnersToConsume = _partners.ToList();
        try
        {
            foreach (var partner in partnersToConsume)
            {
                if (!CanAutoPlay(partner))
                    continue;

                var target = AutoPlayTarget(play, partner);
                if (target is null)
                    continue;

                var pair = SynchronizedPairKey.Create(card, partner);
                if (!ResolvingPairs.Add(pair))
                    continue;

                try
                {
                    await CardCmd.AutoPlay(choiceContext, partner, target, AutoPlayType.Default, false, false);
                }
                finally
                {
                    ResolvingPairs.Remove(pair);
                }
            }
        }
        finally
        {
            ConsumePairs(card, partnersToConsume);
        }
    }

    public override Task AfterTurnEnd(PlayerChoiceContext choiceContext, CombatSide side)
    {
        if (Owner?.Owner?.Creature.Side == side)
            CardModifier.RemoveModifier(Owner, this);

        return Task.CompletedTask;
    }

    public IReadOnlyList<CardModel> AutoPlayPartners() =>
        _partners
            .Where(CanAutoPlay)
            .ToList();

    private static void ConsumePairs(CardModel card, IReadOnlyList<CardModel> partners)
    {
        foreach (var partner in partners)
        {
            RemovePartnerReference(card, partner);
            RemovePartnerReference(partner, card);
        }
    }

    private static void RemovePartnerReference(CardModel card, CardModel partner)
    {
        foreach (var modifier in CardModifier.Modifiers(card).OfType<SynchronizedCardPairModifier>().ToArray())
            modifier.RemovePartner(partner);
    }

    private void RemovePartner(CardModel partner)
    {
        _partners.RemoveAll(card => ReferenceEquals(card, partner));
        if (_partners.Count == 0 && Owner is not null)
            CardModifier.RemoveModifier(Owner, this);
    }

    private bool CanAutoPlay(CardModel partner)
    {
        var owner = Owner?.Owner;
        return owner is not null
               && partner.Owner == owner
               && IsInSynchronizedAutoPlayPile(partner, owner);
    }

    private static bool IsInSynchronizedAutoPlayPile(CardModel card, Player owner) =>
        CardPile.Get(PileType.Hand, owner)?.Cards.Contains(card) == true
        || CardPile.Get(PileType.Discard, owner)?.Cards.Contains(card) == true;

    private Creature? AutoPlayTarget(CardPlay play, CardModel card)
    {
        return card.TargetType switch
        {
            TargetType.AnyEnemy or TargetType.AllEnemies => EnemyTarget(play, card),
            TargetType.RandomEnemy => RandomEnemyTarget(card),
            _ => card.Owner?.Creature
        };
    }

    private static Creature? EnemyTarget(CardPlay play, CardModel card) =>
        IsValidEnemyTarget(play.Target, card)
            ? play.Target
            : RandomEnemyTarget(card);

    private static Creature? RandomEnemyTarget(CardModel card)
    {
        var targets = card.CombatState?.HittableEnemies
            .Where(enemy => enemy.IsAlive)
            .ToList();
        return targets is { Count: > 0 }
            ? card.Owner?.RunState.Rng.CombatTargets.NextItem(targets)
            : null;
    }

    private static bool IsValidEnemyTarget(Creature? target, CardModel card) =>
        target is { IsAlive: true }
        && card.CombatState?.HittableEnemies.Contains(target) == true;

    private readonly struct SynchronizedPairKey : IEquatable<SynchronizedPairKey>
    {
        private readonly CardModel _first;
        private readonly CardModel _second;

        private SynchronizedPairKey(CardModel first, CardModel second)
        {
            _first = first;
            _second = second;
        }

        public static SynchronizedPairKey Create(CardModel first, CardModel second)
        {
            var firstHash = RuntimeHelpers.GetHashCode(first);
            var secondHash = RuntimeHelpers.GetHashCode(second);
            return firstHash <= secondHash
                ? new SynchronizedPairKey(first, second)
                : new SynchronizedPairKey(second, first);
        }

        public bool Equals(SynchronizedPairKey other) =>
            ReferenceEquals(_first, other._first) && ReferenceEquals(_second, other._second);

        public override bool Equals(object? obj) =>
            obj is SynchronizedPairKey other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(RuntimeHelpers.GetHashCode(_first), RuntimeHelpers.GetHashCode(_second));
    }
}

public sealed class ElementThisTurnModifier : CardModifier
{
    private const string ElementsKey = "Elements";

    public SakuraElementSet Elements { get; private set; }

    public override bool ShouldReceiveCombatHooks => true;

    public SakuraElementSet AddElements(SakuraElementSet elements)
    {
        var added = elements & ~Elements;
        if (added == SakuraElementSet.None)
            return SakuraElementSet.None;

        Elements |= added;
        ApplyKeywords(added);
        return added;
    }

    public override void StoreSaveData(ModifierSave save) =>
        save.IntProperties[ElementsKey] = (int)Elements;

    public override void LoadSaveData(ModifierSave save)
    {
        Elements = (SakuraElementSet)save.IntProperties.GetValueOrDefault(ElementsKey);
        ApplyKeywords(Elements);
    }

    public override void OnInitialApplication() =>
        ApplyKeywords(Elements);

    public override Task AfterTurnEnd(PlayerChoiceContext choiceContext, CombatSide side)
    {
        if (Owner?.Owner?.Creature.Side == side)
            RemoveFromOwner();

        return Task.CompletedTask;
    }

    private void ApplyKeywords(SakuraElementSet elements)
    {
        if (Owner is null)
            return;

        foreach (var element in elements.AsElements())
        {
            var keyword = SakuraActions.KeywordFor(element);
            if (!Owner.Keywords.Contains(keyword))
                Owner.AddKeyword(keyword);
        }
    }

    private void RemoveFromOwner()
    {
        if (Owner is not { } card)
            return;

        var staticElements = SakuraActions.StaticElementSetOf(card);
        foreach (var element in Elements.AsElements())
        {
            var keyword = SakuraActions.KeywordFor(element);
            if (!staticElements.HasElement(element) && card.Keywords.Contains(keyword))
                card.RemoveKeyword(keyword);
        }

        CardModifier.RemoveModifier(card, this);
    }
}

public sealed class TemporaryModifier : CardModifier
{
    private const string DelayedRemovalTurnsKey = "DelayedRemovalTurns";
    private static readonly LocString ResonancePrompt = new("cards", "SAKURAMOD-GENERIC.temporaryCardPrompt");
    private static readonly ConditionalWeakTable<CombatState, HashSet<Player>> CleanupFinishedByCombat = new();
    private static readonly PileType[] CleanupPileOrder =
    [
        PileType.Hand,
        PileType.Play,
        PileType.Discard,
        PileType.Draw,
        PileType.Exhaust
    ];

    public int DelayedRemovalTurns { get; private set; }

    public override bool ShouldReceiveCombatHooks => true;

    public void DelayRemoval(int turns)
    {
        if (turns <= 0)
            return;

        DelayedRemovalTurns += turns;
    }

    public override void StoreSaveData(ModifierSave save) =>
        save.IntProperties[DelayedRemovalTurnsKey] = DelayedRemovalTurns;

    public override void LoadSaveData(ModifierSave save) =>
        DelayedRemovalTurns = save.IntProperties.GetValueOrDefault(DelayedRemovalTurnsKey);

    public override void ModifyDescriptionPost(MegaCrit.Sts2.Core.Entities.Creatures.Creature? target, ref string description)
    {
        if (!description.Contains("[red]Temporary[/red]") && !description.Contains("[red]临时[/red]"))
            description += SakuraStateText.TemporaryLine();
    }

    public override async Task AfterTurnEnd(PlayerChoiceContext choiceContext, CombatSide side)
    {
        var player = Owner?.Owner;
        if (player?.Creature.Side != side)
            return;

        await CleanupTemporaryCards(choiceContext, player);
    }

    private static async Task CleanupTemporaryCards(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        if (player.Creature.CombatState is { } combatState)
        {
            var cleanedPlayers = CleanupFinishedByCombat.GetValue(combatState, _ => []);
            if (!cleanedPlayers.Add(player))
                return;
        }

        var resonance = player.Creature.GetPower<DreamKeyResonancePower>();
        if (resonance is not null)
            await StabilizeOneTemporaryCard(choiceContext, player);

        foreach (var card in TemporaryCardsInCleanupOrder(player))
        {
            if (card.Pile?.IsCombatPile != true || !card.IsTemporary())
                continue;
            if (ConsumeTemporaryRemovalDelay(card))
                continue;

            await RemoveTemporaryFromCombat(choiceContext, card);
        }
    }

    private static async Task StabilizeOneTemporaryCard(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        var candidates = TemporaryCardsInCleanupOrder(player)
            .Where(card => card.Pile?.IsCombatPile == true && card.CanStabilize())
            .ToList();
        if (candidates.Count == 0)
            return;

        var selected = await CardSelectCmd.FromSimpleGrid(
            choiceContext,
            candidates,
            player,
            new CardSelectorPrefs(ResonancePrompt, 1)
            {
                Cancelable = false,
                RequireManualConfirmation = false
            });

        if (selected.FirstOrDefault() is { } card)
            await card.Stabilize(choiceContext);
    }

    private static IEnumerable<CardModel> TemporaryCardsInCleanupOrder(MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        foreach (var pileType in CleanupPileOrder)
        {
            var pile = CardPile.Get(pileType, player);
            if (pile is null)
                continue;

            foreach (var card in pile.Cards.ToArray())
            {
                if (card.IsTemporary())
                    yield return card;
            }
        }
    }

    public static async Task RemoveTemporaryFromCombat(PlayerChoiceContext choiceContext, CardModel card)
    {
        if (card.Pile?.IsCombatPile == true)
        {
            TemporaryCardMemory.Remember(card);
            if (card.Owner?.Creature.GetPower<FalseDailyLifePower>() is { } falseDailyLife)
                await falseDailyLife.AfterTemporaryRemoved(choiceContext);
            if (card.Owner?.Creature.GetPower<DreamsEndPower>() is { } dreamsEnd)
                await dreamsEnd.AfterTemporaryRemoved(choiceContext);
            TemporaryDissolveVfx.Play(card);
            await CardPileCmd.RemoveFromCombat(card, true);
        }
    }

    private static bool ConsumeTemporaryRemovalDelay(CardModel card)
    {
        var delayed = false;
        foreach (var modifier in CardModifier.Modifiers(card).OfType<TemporaryModifier>())
            delayed |= modifier.ConsumeRemovalDelay();

        return delayed;
    }

    private bool ConsumeRemovalDelay()
    {
        if (DelayedRemovalTurns <= 0)
            return false;

        DelayedRemovalTurns--;
        return true;
    }

    internal static void ResetCleanupForTurn(Player player)
    {
        if (player.Creature.CombatState is null
            || !CleanupFinishedByCombat.TryGetValue(player.Creature.CombatState, out var cleanedPlayers))
            return;

        cleanedPlayers.Remove(player);
    }
}

public sealed class ReplayThisTurnModifier : CardModifier
{
    private const string AmountKey = "Amount";
    private bool _applied;

    public int Amount { get; private set; }

    public void SetAmount(int amount)
    {
        if (amount <= 0)
            return;

        Amount = amount;
    }

    public override void OnInitialApplication() =>
        ApplyToOwner();

    public override void StoreSaveData(ModifierSave save) =>
        save.IntProperties[AmountKey] = Amount;

    public override void LoadSaveData(ModifierSave save)
    {
        Amount = save.IntProperties.GetValueOrDefault(AmountKey);
        ApplyToOwner();
    }

    public override Task AfterTurnEnd(PlayerChoiceContext choiceContext, CombatSide side)
    {
        if (Owner?.Owner?.Creature.Side == side)
            RemoveFromOwner();

        return Task.CompletedTask;
    }

    private void RemoveFromOwner()
    {
        if (Owner is not { } card)
            return;

        if (_applied)
        {
            card.BaseReplayCount = Math.Max(0, card.BaseReplayCount - Amount);
            _applied = false;
        }

        CardModifier.RemoveModifier(card, this);
    }

    private void ApplyToOwner()
    {
        if (_applied || Owner is null || Amount <= 0)
            return;

        Owner.BaseReplayCount += Amount;
        _applied = true;
    }
}

public sealed class ManifestAtlasOriginModifier : CardModifier;

public static class TemporaryCardMemory
{
    private static readonly ConditionalWeakTable<CombatState, Dictionary<Player, List<CardModel>>> CardsByCombat = new();

    public static void Remember(CardModel card)
    {
        if (card.CombatState is null || card.Owner is null)
            return;

        var copy = card.CreateClone();
        copy.RemoveTemporaryForExchange();

        var cardsByOwner = CardsByCombat.GetValue(card.CombatState, _ => []);
        if (!cardsByOwner.TryGetValue(card.Owner, out var cards))
        {
            cards = [];
            cardsByOwner[card.Owner] = cards;
        }

        cards.Add(copy);
    }

    public static IReadOnlyList<CardModel> CardsRemovedByTemporary(CombatState? combatState, Player? player)
    {
        if (combatState is null || player is null)
            return [];

        return CardsByCombat.TryGetValue(combatState, out var cardsByOwner)
               && cardsByOwner.TryGetValue(player, out var cards)
            ? cards
            : [];
    }
}

internal static class SakuraStateText
{
    public static string ReleaseLine() =>
        $"\n{ReleasedLabel()}{SentenceEnding()}";

    public static string TemporaryLine() =>
        $"\n{TemporaryLabel()}{SentenceEnding()}";

    public static string ReleasedLabel() =>
        IsSimplifiedChinese() ? "[gold]已解封[/gold]" : "[gold]Released[/gold]";

    public static string TemporaryLabel() =>
        IsSimplifiedChinese() ? "[red]临时[/red]" : "[red]Temporary[/red]";

    public static string ClearCardLabel() =>
        IsSimplifiedChinese() ? "透明牌" : "Clear Card";

    public static string PartnerCardLabel() =>
        IsSimplifiedChinese() ? "伙伴牌" : "Partner Card";

    public static string TechniqueCardLabel() =>
        IsSimplifiedChinese() ? "技法牌" : "Technique Card";

    public static string SynchronizedLine(IReadOnlyList<string> partnerNames)
    {
        var names = IsSimplifiedChinese()
            ? string.Join("、", partnerNames)
            : string.Join(", ", partnerNames);
        return IsSimplifiedChinese()
            ? $"\n[gold]同步：[/gold]{names}。"
            : $"\n[gold]Synced:[/gold] {names}.";
    }

    public static string? SynchronizedLine(CardModel card)
    {
        var partnerNames = card.SynchronizedAutoPlayPartners()
            .Select(CardNameWithUpgrade)
            .ToList();

        return partnerNames.Count > 0
            ? SynchronizedLine(partnerNames)
            : null;
    }

    public static string CardNameWithUpgrade(CardModel card)
    {
        var title = LocalizedCardTitle(card);
        return card.IsUpgraded ? $"{title}+" : title;
    }

    public static IReadOnlyList<string> KnownStatusLabels { get; } =
    [
        "[gold]已解封[/gold]",
        "[gold]Released[/gold]",
        "[red]临时[/red]",
        "[red]Temporary[/red]",
        "透明牌",
        "Clear Card"
    ];

    public static IReadOnlyList<string> KnownNonClearHeaderKeywordLabels { get; } =
    [
        "保留",
        "消耗",
        "固有",
        "Retain",
        "Exhaust",
        "Innate"
    ];

    private static string SentenceEnding() =>
        IsSimplifiedChinese() ? "。" : ".";

    private static string LocalizedCardTitle(CardModel card)
    {
        var type = card.GetType();
        return IsSimplifiedChinese()
            ? ChineseCardTitle(type)
            : EnglishCardTitle(type);
    }

    private static string ChineseCardTitle(Type type) =>
        type.Name switch
        {
            nameof(Gale) => "疾风",
            nameof(Reflect) => "反射",
            nameof(Flight) => "飞翔",
            nameof(Action) => "行动",
            nameof(Appear) => "显现",
            nameof(Aqua) => "水源",
            nameof(Blade) => "双剑",
            nameof(Hail) => "冰雹",
            nameof(Lucid) => "透过",
            nameof(Shade) => "影像",
            nameof(Siege) => "包围",
            nameof(Swing) => "摇动",
            nameof(Break) => "破坏",
            nameof(Choice) => "选择",
            nameof(Promise) => "约定",
            nameof(Struggle) => "争斗",
            nameof(Blaze) => "火焰",
            nameof(Dreaming) => "梦见",
            nameof(Gravitation) => "引力",
            nameof(Mirage) => "幻影",
            nameof(Record) => "记录",
            nameof(Exchange) => "交换",
            nameof(Kindness) => "慈爱",
            nameof(Labyrinth) => "迷宫",
            nameof(Repair) => "修复",
            nameof(Reversal) => "逆转",
            nameof(Rewind) => "倒流",
            nameof(Snooze) => "打盹",
            nameof(Spiral) => "螺旋",
            nameof(Transfer) => "转移",
            nameof(Blank) => "白纸",
            nameof(Mirror) => "镜像",
            nameof(Remind) => "想起",
            nameof(Synchronize) => "呼应",
            nameof(Time) => "时间",
            nameof(TrueOrFalse) => "虚实",
            _ => type.Name
        };

    private static string EnglishCardTitle(Type type) =>
        type.Name switch
        {
            nameof(TrueOrFalse) => "True or False",
            _ => type.Name
        };

    private static bool IsSimplifiedChinese() =>
        LocManager.Instance?.Language == "zhs";
}

public static class SakuraCardStates
{
    public static bool IsReleased(this CardModel card) =>
        CardModifier.Modifiers(card).Any(IsReleaseStateModifier);

    public static bool IsTemporary(this CardModel card) =>
        CardModifier.Modifiers(card).Any(modifier => modifier is TemporaryModifier);

    public static bool DelayTemporaryRemoval(this CardModel card, int turns)
    {
        var temporary = CardModifier.Modifiers(card).OfType<TemporaryModifier>().FirstOrDefault();
        if (temporary is null)
            return false;

        temporary.DelayRemoval(turns);
        return true;
    }

    public static void ResetTemporaryCleanupForTurn(Player player)
    {
        TemporaryModifier.ResetCleanupForTurn(player);
    }

    public static bool IsManifestAtlasOrigin(this CardModel card) =>
        CardModifier.Modifiers(card).Any(modifier => modifier is ManifestAtlasOriginModifier);

    public static void Release(this CardModel card)
    {
        if (HasReleaseState(card, ReleaseStateKind.Permanent))
        {
            card.NotifyReleaseStateChanged();
            return;
        }

        AddReleaseState(card, ReleaseStateKind.Permanent);
    }

    public static void MakeReleasePermanent(this CardModel card)
    {
        var deckVersion = PermanentReleaseSourceOf(card, allowDeckFallback: !card.IsTemporary());
        foreach (var target in PermanentReleaseTargets(card, deckVersion))
        {
            target.StabilizeWithoutTrigger();
            target.Release();
        }
    }

    public static async Task MakeReleasePermanent(this CardModel card, PlayerChoiceContext choiceContext)
    {
        var deckVersion = PermanentReleaseSourceOf(card, allowDeckFallback: !card.IsTemporary());
        var stabilized = card.RemoveTemporaryForStabilize();
        if (stabilized)
            await SakuraManifestLoop.OnTemporaryStabilized(choiceContext, card);

        foreach (var target in PermanentReleaseTargets(card, deckVersion))
        {
            if (!ReferenceEquals(target, card))
                target.StabilizeWithoutTrigger();
            target.Release();
        }
    }

    public static void ReleaseThisTurn(this CardModel card)
    {
        if (card.IsReleased())
        {
            card.NotifyReleaseStateChanged();
            return;
        }

        AddReleaseState(card, ReleaseStateKind.ThisTurn);
    }

    public static void MakeTemporary(this CardModel card)
    {
        if (!card.IsTemporary())
            CardModifier.AddModifier(card, NewModifier<TemporaryModifier>());
    }

    public static void MarkManifestAtlasOrigin(this CardModel card)
    {
        if (!card.IsManifestAtlasOrigin())
            CardModifier.AddModifier(card, NewModifier<ManifestAtlasOriginModifier>());
    }

    public static void RemoveManifestAtlasOrigin(this CardModel card)
    {
        foreach (var modifier in CardModifier.Modifiers(card).OfType<ManifestAtlasOriginModifier>().ToArray())
            CardModifier.RemoveModifier(card, modifier);
    }

    public static SakuraElementSet TemporaryElementSet(this CardModel card) =>
        CardModifier.Modifiers(card)
            .OfType<ElementThisTurnModifier>()
            .Aggregate(SakuraElementSet.None, (set, modifier) => set | modifier.Elements);

    public static IReadOnlyList<CardModel> SynchronizedAutoPlayPartners(this CardModel card)
    {
        List<CardModel> partners = [];
        HashSet<CardModel> seen = new(ReferenceEqualityComparer.Instance);
        foreach (var modifier in CardModifier.Modifiers(card).OfType<SynchronizedCardPairModifier>())
        {
            foreach (var partner in modifier.AutoPlayPartners())
            {
                if (ReferenceEquals(partner, card))
                    continue;

                if (seen.Add(partner))
                    partners.Add(partner);
            }
        }

        return partners;
    }

    public static SakuraElementSet GrantElementsThisTurn(this CardModel card, SakuraElementSet elements)
    {
        var missing = elements & ~SakuraActions.ElementSetOf(card);
        if (missing == SakuraElementSet.None)
            return SakuraElementSet.None;

        var modifier = CardModifier.Modifiers(card).OfType<ElementThisTurnModifier>().FirstOrDefault();
        if (modifier is null)
        {
            modifier = NewModifier<ElementThisTurnModifier>();
            CardModifier.AddModifier(card, modifier);
        }

        return modifier.AddElements(missing);
    }

    public static async Task Stabilize(this CardModel card, PlayerChoiceContext choiceContext)
    {
        if (!card.CanStabilize())
            return;

        if (card.RemoveTemporaryForStabilize())
            await SakuraManifestLoop.OnTemporaryStabilized(choiceContext, card);
    }

    public static void StabilizeWithoutTrigger(this CardModel card)
    {
        if (!card.CanStabilize())
            return;

        card.RemoveTemporaryForStabilize();
    }

    public static void RemoveRelease(this CardModel card)
    {
        foreach (var modifier in CardModifier.Modifiers(card).Where(IsReleaseStateModifier).ToArray())
            CardModifier.RemoveModifier(card, modifier);

        card.NotifyReleaseStateChanged();
    }

    public static void NotifyReleaseStateChanged(this CardModel card)
    {
        if (card is IReleaseStateObserver observer)
            observer.OnReleaseStateChanged();
    }

    public static void ExchangeReleaseState(this CardModel first, CardModel second)
    {
        var firstStates = ReleaseStateKinds(first);
        var secondStates = ReleaseStateKinds(second);
        if (firstStates.Count == 0 && secondStates.Count == 0)
            return;

        first.RemoveRelease();
        second.RemoveRelease();
        foreach (var state in firstStates)
            AddReleaseState(second, state, playVfx: !secondStates.Contains(state));
        foreach (var state in secondStates)
            AddReleaseState(first, state, playVfx: !firstStates.Contains(state));
    }

    public static void SynchronizeWith(this CardModel first, CardModel second)
    {
        if (first == second)
            return;

        GetOrAddSynchronizedCardPairModifier(first).AddPartner(second);
        GetOrAddSynchronizedCardPairModifier(second).AddPartner(first);
    }

    public static void RemoveTemporaryForExchange(this CardModel card)
    {
        foreach (var modifier in CardModifier.Modifiers(card).OfType<TemporaryModifier>().ToArray())
            CardModifier.RemoveModifier(card, modifier);
    }

    private static bool RemoveTemporaryForStabilize(this CardModel card)
    {
        var removed = false;
        foreach (var modifier in CardModifier.Modifiers(card).OfType<TemporaryModifier>().ToArray())
        {
            CardModifier.RemoveModifier(card, modifier);
            removed = true;
        }

        return removed;
    }

    public static void RemovePlaybackStateExceptRelease(this CardModel card)
    {
        card.RemoveTemporaryForExchange();
        card.RemoveManifestAtlasOrigin();
        RemoveElementThisTurn(card);

        foreach (var modifier in CardModifier.Modifiers(card)
                     .Where(modifier => modifier is SynchronizedCardPairModifier)
                     .ToArray())
            CardModifier.RemoveModifier(card, modifier);

        foreach (var modifier in CardModifier.Modifiers(card).OfType<ReplayThisTurnModifier>().ToArray())
        {
            card.BaseReplayCount = Math.Max(0, card.BaseReplayCount - modifier.Amount);
            CardModifier.RemoveModifier(card, modifier);
        }
    }

    public static void ExchangeTemporaryState(this CardModel first, CardModel second)
    {
        var firstTemporary = first.IsTemporary();
        var secondTemporary = second.IsTemporary();
        if (firstTemporary == secondTemporary)
            return;

        if (firstTemporary)
        {
            first.RemoveTemporaryForExchange();
            second.MakeTemporary();
        }
        else
        {
            second.RemoveTemporaryForExchange();
            first.MakeTemporary();
        }
    }

    public static void AddReplayThisTurn(this CardModel card, int amount)
    {
        if (amount <= 0)
            return;

        var modifier = NewModifier<ReplayThisTurnModifier>();
        modifier.SetAmount(amount);
        CardModifier.AddModifier(card, modifier);
    }

    public static bool CanStabilize(this CardModel card) =>
        card.Owner?.GetRelic<KaitoPocketWatch>() is null;

    private static T NewModifier<T>() where T : CardModifier =>
        (T)CardModifier.Get<T>().MutableClone();

    private static SynchronizedCardPairModifier GetOrAddSynchronizedCardPairModifier(CardModel card)
    {
        var modifier = CardModifier.Modifiers(card).OfType<SynchronizedCardPairModifier>().FirstOrDefault();
        if (modifier is not null)
            return modifier;

        modifier = NewModifier<SynchronizedCardPairModifier>();
        CardModifier.AddModifier(card, modifier);
        return modifier;
    }

    private static List<ReleaseStateKind> ReleaseStateKinds(CardModel card) =>
        CardModifier.Modifiers(card)
            .Select(ReleaseStateKindOf)
            .Where(kind => kind.HasValue)
            .Select(kind => kind!.Value)
            .ToList();

    private static bool HasReleaseState(CardModel card, ReleaseStateKind state) =>
        CardModifier.Modifiers(card).Any(modifier => ReleaseStateKindOf(modifier) == state);

    private static bool IsReleaseStateModifier(CardModifier modifier) =>
        ReleaseStateKindOf(modifier) is not null;

    private static IReadOnlyList<CardModel> PermanentReleaseTargets(CardModel card, CardModel? deckVersion)
    {
        List<CardModel> targets = [];
        HashSet<CardModel> seen = new(ReferenceEqualityComparer.Instance);
        AddTarget(card);
        if (deckVersion is not null)
        {
            AddTarget(deckVersion);
            foreach (var combatCard in card.Owner.PlayerCombatState?.AllCards ?? [])
            {
                if (DeckVersionOf(combatCard) == deckVersion)
                    AddTarget(combatCard);
            }
        }

        return targets;

        void AddTarget(CardModel target)
        {
            if (seen.Add(target))
                targets.Add(target);
        }
    }

    private static CardModel? DeckVersionOf(CardModel card)
    {
        HashSet<CardModel> seen = new(ReferenceEqualityComparer.Instance);
        for (CardModel? current = card; current is not null && seen.Add(current); current = current.CloneOf)
        {
            if (current.DeckVersion is { } deckVersion)
                return deckVersion;
        }

        return null;
    }

    private static CardModel? PermanentReleaseSourceOf(CardModel card, bool allowDeckFallback)
    {
        if (card.Pile?.Type == PileType.Deck)
            return card;

        if (DeckVersionOf(card) is { } deckVersion)
            return deckVersion;

        return allowDeckFallback ? SingleMatchingDeckCard(card) : null;
    }

    private static CardModel? SingleMatchingDeckCard(CardModel card)
    {
        if (card.Owner is not { } owner)
            return null;

        var matches = owner.Deck.Cards
            .Where(deckCard =>
                !ReferenceEquals(deckCard, card)
                && deckCard.Id == card.Id
                && deckCard.CurrentUpgradeLevel == card.CurrentUpgradeLevel)
            .ToList();

        return matches.Count == 1 ? matches[0] : null;
    }

    private static void RemoveElementThisTurn(CardModel card)
    {
        var staticElements = SakuraActions.StaticElementSetOf(card);
        foreach (var modifier in CardModifier.Modifiers(card).OfType<ElementThisTurnModifier>().ToArray())
        {
            foreach (var element in modifier.Elements.AsElements())
            {
                var keyword = SakuraActions.KeywordFor(element);
                if (!staticElements.HasElement(element) && card.Keywords.Contains(keyword))
                    card.RemoveKeyword(keyword);
            }

            CardModifier.RemoveModifier(card, modifier);
        }
    }

    private static ReleaseStateKind? ReleaseStateKindOf(CardModifier modifier) =>
        modifier switch
        {
            ReleaseModifier => ReleaseStateKind.Permanent,
            ReleaseThisTurnModifier => ReleaseStateKind.ThisTurn,
            _ => null
        };

    private static void AddReleaseState(CardModel card, ReleaseStateKind state, bool playVfx = true)
    {
        if (HasReleaseState(card, state))
        {
            card.NotifyReleaseStateChanged();
            return;
        }

        switch (state)
        {
            case ReleaseStateKind.Permanent:
                CardModifier.AddModifier(card, NewModifier<ReleaseModifier>());
                break;
            case ReleaseStateKind.ThisTurn:
                CardModifier.AddModifier(card, NewModifier<ReleaseThisTurnModifier>());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(state), state, null);
        }

        card.NotifyReleaseStateChanged();

        if (playVfx)
            ReleaseVfx.Play(card);
    }

    private enum ReleaseStateKind
    {
        Permanent,
        ThisTurn
    }
}
