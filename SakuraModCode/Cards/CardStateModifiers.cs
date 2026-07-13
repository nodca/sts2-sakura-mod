using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Character;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using STS2RitsuLib.Content;
using STS2RitsuLib.Models.Capabilities;

namespace SakuraMod.SakuraModCode.Cards;

public sealed class SynchronizedCardPairModifier : SakuraCardStateCapability
{
    private static readonly HashSet<SynchronizedPairKey> ResolvingPairs = [];
    private readonly List<CardModel> _pairedCards = [];

    public void AddPairedCard(CardModel pairedCard)
    {
        if (Owner is null || pairedCard == Owner || _pairedCards.Contains(pairedCard))
            return;

        _pairedCards.Add(pairedCard);
    }

    protected override void ModifyDescriptionPost(Creature? target, ref string description)
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
        var pairedCardsToConsume = _pairedCards.ToList();
        try
        {
            foreach (var pairedCard in pairedCardsToConsume)
            {
                if (!CanAutoPlay(pairedCard))
                    continue;

                var target = AutoPlayTarget(play, pairedCard);
                if (target is null)
                    continue;

                var pair = SynchronizedPairKey.Create(card, pairedCard);
                if (!ResolvingPairs.Add(pair))
                    continue;

                try
                {
                    await CardCmd.AutoPlay(choiceContext, pairedCard, target, AutoPlayType.Default, false, false);
                }
                finally
                {
                    ResolvingPairs.Remove(pair);
                }
            }
        }
        finally
        {
            ConsumePairs(card, pairedCardsToConsume);
        }
    }

    public override Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner?.Owner?.Creature is { } ownerCreature
            && ownerCreature.Side == side
            && participants.Contains(ownerCreature))
            base.RemoveFromOwner();

        return Task.CompletedTask;
    }

    public IReadOnlyList<CardModel> AutoPlayPairedCards() =>
        _pairedCards
            .Where(CanAutoPlay)
            .ToList();

    private static void ConsumePairs(CardModel card, IReadOnlyList<CardModel> pairedCards)
    {
        foreach (var pairedCard in pairedCards)
        {
            RemovePairedCardReference(card, pairedCard);
            RemovePairedCardReference(pairedCard, card);
        }
    }

    private static void RemovePairedCardReference(CardModel card, CardModel pairedCard)
    {
        foreach (var modifier in SakuraCardStateCapability.Modifiers(card).OfType<SynchronizedCardPairModifier>().ToArray())
            modifier.RemovePairedCard(pairedCard);
    }

    private void RemovePairedCard(CardModel pairedCard)
    {
        _pairedCards.RemoveAll(card => ReferenceEquals(card, pairedCard));
        if (_pairedCards.Count == 0 && Owner is not null)
            RemoveFromOwner();
    }

    private bool CanAutoPlay(CardModel pairedCard)
    {
        var owner = Owner?.Owner;
        return owner is not null
               && pairedCard.Owner == owner
               && IsInSynchronizedAutoPlayPile(pairedCard, owner);
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

public sealed class TemporaryModifier : SakuraCardStateCapability
{
    private const string DelayedRemovalTurnsKey = "DelayedRemovalTurns";
    private static readonly ConditionalWeakTable<ICombatState, HashSet<Player>> CleanupFinishedByCombat = new();
    private static readonly PileType[] CleanupPileOrder =
    [
        PileType.Hand,
        PileType.Play,
        PileType.Discard,
        PileType.Draw,
        PileType.Exhaust
    ];

    public int DelayedRemovalTurns { get; private set; }

    public void DelayRemoval(int turns)
    {
        if (turns <= 0)
            return;

        DelayedRemovalTurns += turns;
    }

    protected override JsonNode? SaveAdditionalState() =>
        new JsonObject { [DelayedRemovalTurnsKey] = DelayedRemovalTurns };

    protected override void LoadAdditionalState(JsonNode? state, int schemaVersion) =>
        DelayedRemovalTurns = state?[DelayedRemovalTurnsKey]?.GetValue<int>() ?? 0;

    protected override void ModifyDescriptionPost(Creature? target, ref string description)
    {
        if (!description.Contains("[red]Temporary[/red]") && !description.Contains("[red]临时[/red]"))
            description += SakuraStateText.TemporaryLine();
    }

    protected override IEnumerable<string> HoverTipKeys(CardModel card) =>
        [SakuraCardHoverTips.TemporaryTipKey];

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        var player = Owner?.Owner;
        if (player?.Creature is not { } ownerCreature
            || ownerCreature.Side != side
            || !participants.Contains(ownerCreature))
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

        foreach (var card in TemporaryCardsInCleanupOrder(player))
        {
            if (card.Pile?.IsCombatPile != true || !card.IsTemporary())
                continue;
            if (ConsumeTemporaryRemovalDelay(card))
                continue;

            await RemoveTemporaryFromCombat(choiceContext, card);
        }
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
            TemporaryDissolveVfx.Play(card);
            await CardPileCmd.RemoveFromCombat(card, true);
        }
    }

    private static bool ConsumeTemporaryRemovalDelay(CardModel card)
    {
        var delayed = false;
        foreach (var modifier in SakuraCardStateCapability.Modifiers(card).OfType<TemporaryModifier>())
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

public sealed class ReplayThisTurnModifier : SakuraCardStateCapability
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

    protected override void OnAttach(CardModel owner)
    {
        base.OnAttach(owner);
        ApplyToOwner();
    }

    protected override JsonNode? SaveAdditionalState() =>
        new JsonObject { [AmountKey] = Amount };

    protected override void LoadAdditionalState(JsonNode? state, int schemaVersion)
    {
        Amount = state?[AmountKey]?.GetValue<int>() ?? 0;
        ApplyToOwner();
    }

    public override Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner?.Owner?.Creature is { } ownerCreature
            && ownerCreature.Side == side
            && participants.Contains(ownerCreature))
            RemoveReplayStateFromOwner();

        return Task.CompletedTask;
    }

    private void RemoveReplayStateFromOwner()
    {
        if (Owner is not { } card)
            return;

        if (_applied)
        {
            card.BaseReplayCount = Math.Max(0, card.BaseReplayCount - Amount);
            _applied = false;
        }

        base.RemoveFromOwner();
    }

    private void ApplyToOwner()
    {
        if (_applied || Owner is null || Amount <= 0)
            return;

        Owner.BaseReplayCount += Amount;
        _applied = true;
    }
}

public sealed class ManifestAtlasOriginModifier : SakuraCardStateCapability
{
}

public abstract class SakuraCardStateCapability : CardCapability, ICardDescriptionContributor, ICardHoverTipContributor
{
    private const string DescriptionFragmentTable = "cards";
    private const string DescriptionFragmentKey = "SAKURAMOD-GENERIC.capabilityDescriptionFragment";

    public static IEnumerable<SakuraCardStateCapability> Modifiers(CardModel card) =>
        card.Capabilities().All.OfType<SakuraCardStateCapability>();

    public static void AddModifier(CardModel card, SakuraCardStateCapability modifier) =>
        card.AddCapability(modifier, allowMerge: false);

    public static void RemoveModifier(CardModel card, SakuraCardStateCapability modifier) =>
        card.Capabilities().Remove(modifier);

    public IEnumerable<CardDescriptionFragment> GetDescriptionFragments(CardDescriptionContext context)
    {
        var description = "";
        ModifyDescriptionPost(context.Target, ref description);
        if (string.IsNullOrEmpty(description))
            return [];

        var text = new LocString(DescriptionFragmentTable, DescriptionFragmentKey);
        text.Add("Text", description);
        return [new CardDescriptionFragment(text, CardDescriptionFragmentPlacement.AfterBase)];
    }

    protected virtual void ModifyDescriptionPost(Creature? target, ref string description)
    {
    }

    public IEnumerable<IHoverTip> GetHoverTips(CardModel card) =>
        HoverTipKeywords(card).Select(HoverTipFactory.FromKeyword)
            .Concat(HoverTipKeys(card).Select(key => (IHoverTip)SakuraCardHoverTips.StaticTip(key)))
            .Distinct();

    internal IEnumerable<CardKeyword> HoverTipKeywordDecisions(CardModel card) => HoverTipKeywords(card);

    internal IEnumerable<string> HoverTipKeyDecisions(CardModel card) => HoverTipKeys(card);

    protected virtual IEnumerable<CardKeyword> HoverTipKeywords(CardModel card) => [];

    protected virtual IEnumerable<string> HoverTipKeys(CardModel card) => [];
}

public static class TemporaryCardMemory
{
    private static readonly ConditionalWeakTable<ICombatState, Dictionary<Player, List<CardModel>>> CardsByCombat = new();

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

    public static IReadOnlyList<CardModel> CardsRemovedByTemporary(ICombatState? combatState, Player? player)
    {
        if (combatState is null || player is null)
            return [];

        return CardsByCombat.TryGetValue(combatState, out var cardsByOwner)
               && cardsByOwner.TryGetValue(player, out var cards)
            ? cards
            : [];
    }

    public static void Consume(ICombatState? combatState, Player? player, IReadOnlyList<CardModel> selected)
    {
        if (selected.Count == 0)
            return;
        if (combatState is null
            || player is null
            || !CardsByCombat.TryGetValue(combatState, out var cardsByOwner)
            || !cardsByOwner.TryGetValue(player, out var cards))
        {
            throw new InvalidOperationException("Cannot consume Temporary memory outside its owning combat and player.");
        }

        ConsumeRecords(cards, selected);
    }

    internal static void ConsumeRecords(List<CardModel> records, IReadOnlyList<CardModel> selected)
    {
        var selectedIndices = new List<int>(selected.Count);
        foreach (var selectedRecord in selected)
        {
            var index = records.FindIndex(record => ReferenceEquals(record, selectedRecord));
            if (index < 0 || selectedIndices.Contains(index))
                throw new InvalidOperationException("Temporary memory selection contains a stale or duplicate record.");

            selectedIndices.Add(index);
        }

        selectedIndices.Sort();
        for (var i = selectedIndices.Count - 1; i >= 0; i--)
            records.RemoveAt(selectedIndices[i]);
    }
}

internal static class SakuraStateText
{
    public static string TemporaryLine() =>
        $"\n{TemporaryLabel()}{SentenceEnding()}";

    public static string TemporaryLabel() =>
        IsSimplifiedChinese() ? "[red]临时[/red]" : "[red]Temporary[/red]";

    public static string ClearCardLabel() =>
        IsSimplifiedChinese() ? "透明牌" : "Clear Card";

    public static string SynchronizedLine(IReadOnlyList<string> pairedNames)
    {
        var names = IsSimplifiedChinese()
            ? string.Join("、", pairedNames)
            : string.Join(", ", pairedNames);
        return IsSimplifiedChinese()
            ? $"\n[gold]同步：[/gold]{names}。"
            : $"\n[gold]Synced:[/gold] {names}.";
    }

    public static string? SynchronizedLine(CardModel card)
    {
        var pairedNames = card.SynchronizedAutoPlayPairedCards()
            .Select(CardNameWithUpgrade)
            .ToList();

        return pairedNames.Count > 0
            ? SynchronizedLine(pairedNames)
            : null;
    }

    public static string CardNameWithUpgrade(CardModel card)
    {
        var title = LocalizedCardTitle(card);
        return card.IsUpgraded ? $"{title}+" : title;
    }

    public static IReadOnlyList<string> KnownStatusLabels { get; } =
    [
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
        "无形",
        "Retain",
        "Exhaust",
        "Innate",
        "Invisible"
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
    public static void Register()
    {
        var registry = ModContentRegistry.For(MainFile.ModId);
        registry.RegisterModelCapability<SynchronizedCardPairModifier>();
        registry.RegisterModelCapability<TemporaryModifier>();
        registry.RegisterModelCapability<ReplayThisTurnModifier>();
        registry.RegisterModelCapability<ManifestAtlasOriginModifier>();
    }

    public static bool IsTemporary(this CardModel card) =>
        SakuraCardStateCapability.Modifiers(card).Any(modifier => modifier is TemporaryModifier);

    public static bool DelayTemporaryRemoval(this CardModel card, int turns)
    {
        var temporary = SakuraCardStateCapability.Modifiers(card).OfType<TemporaryModifier>().FirstOrDefault();
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
        SakuraCardStateCapability.Modifiers(card).Any(modifier => modifier is ManifestAtlasOriginModifier);

    public static void MakeTemporary(this CardModel card)
    {
        if (!card.IsTemporary())
            SakuraCardStateCapability.AddModifier(card, NewModifier<TemporaryModifier>());
    }

    public static void MarkManifestAtlasOrigin(this CardModel card)
    {
        if (!card.IsManifestAtlasOrigin())
            SakuraCardStateCapability.AddModifier(card, NewModifier<ManifestAtlasOriginModifier>());
    }

    public static void RemoveManifestAtlasOrigin(this CardModel card)
    {
        foreach (var modifier in SakuraCardStateCapability.Modifiers(card).OfType<ManifestAtlasOriginModifier>().ToArray())
            SakuraCardStateCapability.RemoveModifier(card, modifier);
    }

    public static IReadOnlyList<CardModel> SynchronizedAutoPlayPairedCards(this CardModel card)
    {
        List<CardModel> pairedCards = [];
        HashSet<CardModel> seen = new(ReferenceEqualityComparer.Instance);
        foreach (var modifier in SakuraCardStateCapability.Modifiers(card).OfType<SynchronizedCardPairModifier>())
        {
            foreach (var pairedCard in modifier.AutoPlayPairedCards())
            {
                if (ReferenceEquals(pairedCard, card))
                    continue;

                if (seen.Add(pairedCard))
                    pairedCards.Add(pairedCard);
            }
        }

        return pairedCards;
    }

    public static async Task Stabilize(this CardModel card, PlayerChoiceContext choiceContext)
    {
        if (!card.CanStabilize())
            return;

        if (!card.RemoveTemporaryForStabilize())
            return;

        SakuraVoicePlayback.TryPlay(SakuraVoiceTrigger.Stabilize, card.CombatState);
        await SakuraManifestLoop.OnTemporaryStabilized(choiceContext, card);
    }

    public static void StabilizeWithoutTrigger(this CardModel card)
    {
        if (!card.CanStabilize())
            return;

        card.RemoveTemporaryForStabilize();
    }

    public static void SynchronizeWith(this CardModel first, CardModel second)
    {
        if (first == second)
            return;

        GetOrAddSynchronizedCardPairModifier(first).AddPairedCard(second);
        GetOrAddSynchronizedCardPairModifier(second).AddPairedCard(first);
    }

    public static void RemoveTemporaryForExchange(this CardModel card)
    {
        foreach (var modifier in SakuraCardStateCapability.Modifiers(card).OfType<TemporaryModifier>().ToArray())
            SakuraCardStateCapability.RemoveModifier(card, modifier);
    }

    private static bool RemoveTemporaryForStabilize(this CardModel card)
    {
        var removed = false;
        foreach (var modifier in SakuraCardStateCapability.Modifiers(card).OfType<TemporaryModifier>().ToArray())
        {
            SakuraCardStateCapability.RemoveModifier(card, modifier);
            removed = true;
        }

        return removed;
    }

    public static void RemovePlaybackState(this CardModel card)
    {
        card.RemoveTemporaryForExchange();
        card.RemoveManifestAtlasOrigin();

        foreach (var modifier in SakuraCardStateCapability.Modifiers(card)
                     .Where(modifier => modifier is SynchronizedCardPairModifier)
                     .ToArray())
            SakuraCardStateCapability.RemoveModifier(card, modifier);

        foreach (var modifier in SakuraCardStateCapability.Modifiers(card).OfType<ReplayThisTurnModifier>().ToArray())
        {
            card.BaseReplayCount = Math.Max(0, card.BaseReplayCount - modifier.Amount);
            SakuraCardStateCapability.RemoveModifier(card, modifier);
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
        SakuraCardStateCapability.AddModifier(card, modifier);
    }

    public static bool CanStabilize(this CardModel card) =>
        SakuraTransparentCardCatalog.IsTransparentCard(card);

    private static T NewModifier<T>() where T : SakuraCardStateCapability =>
        ModelCapabilityRegistry.Create<T>();

    private static SynchronizedCardPairModifier GetOrAddSynchronizedCardPairModifier(CardModel card)
    {
        var modifier = SakuraCardStateCapability.Modifiers(card).OfType<SynchronizedCardPairModifier>().FirstOrDefault();
        if (modifier is not null)
            return modifier;

        modifier = NewModifier<SynchronizedCardPairModifier>();
        SakuraCardStateCapability.AddModifier(card, modifier);
        return modifier;
    }

}
