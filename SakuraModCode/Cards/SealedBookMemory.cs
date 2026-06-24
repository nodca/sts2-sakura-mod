using BaseLib.Patches.Saves;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace SakuraMod.SakuraModCode.Cards;

internal static class SealedBookMemory
{
    private static readonly SavedSpireField<Player, List<SerializableCard>> SealedCards =
        new(() => new List<SerializableCard>(), "SakuraMod_SealedBookCards")
        {
            Serializer = Serialize,
            Deserializer = Deserialize
        };

    public static void Register()
    {
        ExtendedSaveTypes.RegisterListSaveType<SerializableCard>();
        _ = SealedCards;
    }

    public static int Count(Player? owner) =>
        owner is null ? 0 : Cards(owner).Count;

    public static bool CanSeal(CardModel card) =>
        SakuraActions.IsClearCard(card)
        && card.IsTemporary()
        && card.Pile?.Type == PileType.Hand;

    public static async Task Seal(PlayerChoiceContext context, CardModel card)
    {
        if (card.Owner is not { } owner || !CanSeal(card) || card.Pile?.IsCombatPile != true)
            return;

        Cards(owner).Add(CreateSnapshot(card));
        await TemporaryModifier.RemoveTemporaryFromCombat(context, card);
    }

    public static async Task<CardModel?> Release(SakuraModCard source, PlayerChoiceContext context)
    {
        var sealedCards = Cards(source.Owner);
        if (sealedCards.Count == 0)
            return null;

        var previews = sealedCards
            .Select(snapshot => LoadSnapshot(source, snapshot))
            .ToList();
        try
        {
            var selected = await SakuraActions.SelectFromCards(source, context, previews, cancelable: false);
            if (selected is null)
                return null;

            var index = previews.IndexOf(selected);
            if (index < 0 || index >= sealedCards.Count)
                return null;

            return await ReleaseSnapshot(source, context, sealedCards, index);
        }
        finally
        {
            RemoveDetachedCards(previews);
        }
    }

    private static List<SerializableCard> Cards(Player owner)
    {
        var cards = SealedCards[owner];
        if (cards is not null)
            return cards;

        cards = [];
        SealedCards[owner] = cards;
        return cards;
    }

    private static SerializableCard CreateSnapshot(CardModel card)
    {
        var snapshotCard = card.CreateClone();
        try
        {
            SanitizeSnapshotCard(snapshotCard);
            return snapshotCard.ToSerializable();
        }
        finally
        {
            snapshotCard.CardScope?.RemoveCard(snapshotCard);
        }
    }

    private static CardModel LoadSnapshot(SakuraModCard source, SerializableCard snapshot)
    {
        var card = CardModel.FromSerializable(snapshot);
        var scope = CombatScope(source);
        scope.AddCard(card, source.Owner);
        return card;
    }

    private static async Task<CardModel> ReleaseSnapshot(
        SakuraModCard source,
        PlayerChoiceContext context,
        List<SerializableCard> sealedCards,
        int index)
    {
        CardModel? released = null;
        var snapshot = sealedCards[index];
        try
        {
            released = LoadSnapshot(source, snapshot);
            SanitizeSnapshotCard(released);
            RemoveSnapshot(sealedCards, index, snapshot);
            await SakuraActions.AddGeneratedCardToCombat(
                released,
                new GeneratedCardOptions
                {
                    RemoveTemporary = true,
                    AddRelease = true,
                    FreeThisTurn = true,
                    Pile = PileType.Hand
                },
                context);
            return released;
        }
        catch
        {
            if (released?.Pile is null)
            {
                released?.CardScope?.RemoveCard(released);
                RestoreSnapshot(sealedCards, index, snapshot);
            }
            throw;
        }
    }

    private static void RemoveSnapshot(List<SerializableCard> sealedCards, int index, SerializableCard snapshot)
    {
        if (index < sealedCards.Count && ReferenceEquals(sealedCards[index], snapshot))
        {
            sealedCards.RemoveAt(index);
            return;
        }

        var actualIndex = sealedCards.FindIndex(card => ReferenceEquals(card, snapshot));
        if (actualIndex >= 0)
            sealedCards.RemoveAt(actualIndex);
    }

    private static void RestoreSnapshot(List<SerializableCard> sealedCards, int index, SerializableCard snapshot)
    {
        if (sealedCards.Any(card => ReferenceEquals(card, snapshot)))
            return;

        sealedCards.Insert(Math.Clamp(index, 0, sealedCards.Count), snapshot);
    }

    private static ICardScope CombatScope(SakuraModCard source) =>
        source.Owner.Creature.CombatState as ICardScope
        ?? throw new InvalidOperationException("Sealed Book can only release cards during combat.");

    private static void SanitizeSnapshotCard(CardModel card)
    {
        card.RemovePlaybackStateExceptRelease();
        card.RemoveRelease();
    }

    private static void RemoveDetachedCards(IEnumerable<CardModel> cards)
    {
        foreach (var card in cards)
        {
            if (card.Pile is not null)
                continue;

            card.CardScope?.RemoveCard(card);
        }
    }

    private static void Serialize(List<SerializableCard> cards, PacketWriter writer) =>
        writer.WriteList(cards);

    private static List<SerializableCard> Deserialize(PacketReader reader) =>
        reader.ReadList<SerializableCard>();
}
