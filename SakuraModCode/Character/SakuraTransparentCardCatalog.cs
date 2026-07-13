using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Cards;

namespace SakuraMod.SakuraModCode.Character;

public static class SakuraTransparentCardCatalog
{
    private static readonly IReadOnlyList<Type> TransparentCardTypesInternal =
        SakuraCardCatalog.SourceCardTypes(SourceEraClass.Clear);

    private static readonly IReadOnlyList<Type> DefaultManifestExcludedTransparentCardTypes =
    [
        typeof(Gale),
        typeof(Siege)
    ];

    private static readonly HashSet<Type> TransparentCardTypeSet = TransparentCardTypesInternal.ToHashSet();
    private static readonly HashSet<Type> DefaultManifestExcludedTransparentCardTypeSet =
        DefaultManifestExcludedTransparentCardTypes.ToHashSet();
    private static readonly IReadOnlyList<Type> DefaultManifestAtlasTypesInternal =
        TransparentCardTypesInternal
            .Where(type => !DefaultManifestExcludedTransparentCardTypeSet.Contains(type))
            .ToList();
    private static readonly HashSet<Type> DefaultManifestAtlasTypeSet = DefaultManifestAtlasTypesInternal.ToHashSet();
    public static IReadOnlyList<Type> AllCardTypes => TransparentCardTypesInternal;
    public static IReadOnlyList<Type> TransparentCardTypes => TransparentCardTypesInternal;
    public static IReadOnlyList<Type> DefaultManifestAtlasTypes => DefaultManifestAtlasTypesInternal;

    public static CardModel[] AllCardTemplates() =>
        AllCardTypes
            .Select(CardTemplate)
            .ToArray();

    public static CardModel[] TransparentCardTemplates() =>
        TransparentCardTypesInternal
            .Select(CardTemplate)
            .ToArray();

    public static CardModel CardTemplate(Type type) =>
        ModelDb.GetById<CardModel>(ModelDb.GetId(type));

    public static bool IsTransparentCard(CardModel card) =>
        TransparentCardTypeSet.Contains(card.GetType());

    public static bool TryGetTransparentCardTypeById(string cardId, out Type type)
    {
        type = TransparentCardTypesInternal.FirstOrDefault(type => ModelDb.GetId(type).Entry == cardId)!;
        return type is not null;
    }

    public static bool IsDefaultManifestAtlasCard(CardModel card) =>
        DefaultManifestAtlasTypeSet.Contains(card.GetType());

    public static CardModel CreateCleanTransparentCard(Player owner, Type type)
    {
        if (!TransparentCardTypeSet.Contains(type))
            throw new ArgumentException($"{type.Name} is not a Transparent Card.", nameof(type));

        var card = owner.RunState.CreateCard(CardTemplate(type), owner);
        card.RemovePlaybackState();
        return card;
    }
}
