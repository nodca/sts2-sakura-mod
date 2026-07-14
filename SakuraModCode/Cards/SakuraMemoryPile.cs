using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Character;
using STS2RitsuLib.CardPiles;
using STS2RitsuLib.Content;

namespace SakuraMod.SakuraModCode.Cards;

internal static class SakuraMemoryPile
{
    internal const string LocalStem = "Memory";
    internal const string IconPath = MainFile.ResPath + "/images/card_piles/memory.png";

    internal static readonly Vector2 UiOffsetAboveExhaust = new(100f, -100f);

    private static ModCardPileDefinition? _definition;

    internal static string PileId =>
        ModContentRegistry.GetQualifiedCardPileId(MainFile.ModId, LocalStem);

    internal static PileType PileType => Definition.PileType;

    internal static ModCardPileSpec RegistrationSpec { get; } = new()
    {
        Scope = ModCardPileScope.CombatOnly,
        Style = ModCardPileUiStyle.BottomRight,
        Anchor = new ModCardPileAnchor(
            ModCardPileAnchorKind.BottomRightPrimary,
            UiOffsetAboveExhaust),
        IconPath = IconPath,
        VisibleWhen = static context => IsButtonVisible(
            context.Player is { } player
            && SakuraStarterCompatibility.IsKinomotoSakura(player),
            CombatManager.Instance.IsInProgress,
            context.Pile?.Cards.Count ?? 0)
    };

    internal static bool IsButtonVisible(
        bool isKinomotoSakura,
        bool isCombatInProgress,
        int memoryCount) =>
        isKinomotoSakura && isCombatInProgress && memoryCount > 0;

    internal static void Register()
    {
        _definition ??= ModCardPileRegistry.For(MainFile.ModId)
            .RegisterOwned(LocalStem, RegistrationSpec);
    }

    internal static CardPile? Get(Player? player) =>
        player is null ? null : CardPile.Get(PileType, player);

    internal static int Count(Player? player) =>
        Get(player)?.Cards.Count ?? 0;

    internal static async Task MoveTemporaryCardIntoMemory(CardModel card)
    {
        var pile = Get(card.Owner)
            ?? throw new InvalidOperationException("Cannot move a Temporary card into Memory outside combat.");
        var result = await CardPileCmd.Add(
            card,
            pile,
            CardPilePosition.Bottom,
            clonedBy: null,
            skipVisuals: false);
        if (!result.success || !ReferenceEquals(card.Pile, pile))
            throw new InvalidOperationException($"Failed to move {card.Id.Entry} into its owner's Memory pile.");

        card.RemoveTemporaryForExchange();
    }

    internal static async Task<IReadOnlyList<CardModel>> Consume(
        Player player,
        IReadOnlyList<CardModel> selected)
    {
        if (selected.Count == 0)
            return [];

        var pile = Get(player)
            ?? throw new InvalidOperationException("Cannot consume Memory outside combat.");
        ValidateSelection(pile.Cards, selected);

        var copies = selected.Select(static card => card.CreateClone()).ToList();
        try
        {
            await CardPileCmd.RemoveFromCombat(selected, skipVisuals: true);
            return copies;
        }
        catch
        {
            SakuraGeneratedCardLifecycle.RemoveDetachedGeneratedChoices(copies);
            throw;
        }
    }

    internal static void ValidateSelection(
        IReadOnlyList<CardModel> available,
        IReadOnlyList<CardModel> selected)
    {
        var availableRecords = new HashSet<CardModel>(available, ReferenceEqualityComparer.Instance);
        var selectedRecords = new HashSet<CardModel>(ReferenceEqualityComparer.Instance);
        foreach (var record in selected)
        {
            if (!availableRecords.Contains(record) || !selectedRecords.Add(record))
                throw new InvalidOperationException("Memory selection contains a stale or duplicate record.");
        }
    }

    private static ModCardPileDefinition Definition =>
        _definition
        ?? throw new InvalidOperationException("The Sakura Memory pile has not been registered.");
}
