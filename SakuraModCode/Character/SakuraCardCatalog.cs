using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Cards;

namespace SakuraMod.SakuraModCode.Character;

public static class SakuraCardCatalog
{
    private static readonly IReadOnlyList<Type> TransparentCardTypesInternal =
    [
        typeof(Gale),
        typeof(Reflect),
        typeof(Flight),
        typeof(global::SakuraMod.SakuraModCode.Cards.Action),
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
        typeof(global::SakuraMod.SakuraModCode.Cards.Time),
        typeof(TrueOrFalse)
    ];

    private static readonly IReadOnlyList<Type> DefaultManifestExcludedTransparentCardTypes =
    [
        typeof(Gale),
        typeof(Siege)
    ];

    private static readonly IReadOnlyList<Type> SupportCardTypesInternal =
    [
        typeof(KeroBond),
        typeof(KeroRecon),
        typeof(KeroSnackBreak),
        typeof(TomoyoCostume),
        typeof(AkihoDream),
        typeof(ClockCountryAlice),
        typeof(DWatch),
        typeof(FalseDailyLife),
        typeof(StarlightChant),
        typeof(Archive),
        typeof(GrowingMagic),
        typeof(ReleaseChant),
        typeof(CardBookSorting),
        typeof(NamelessMagic),
        typeof(ChainPhenomenon),
        typeof(MagicSurge),
        typeof(MagicTuning),
        typeof(DreamWandCombo),
        typeof(CompassTracking),
        typeof(ThunderEmperorSummon),
        typeof(MeilingComboKick),
        typeof(TalismanCombo),
        typeof(SilverMoonWing),
        typeof(BigBrotherSense),
        typeof(YukitoLunchBox),
        typeof(MomoContract),
        typeof(YamazakiTallTale),
        typeof(FujitakaNote),
        typeof(NaokoGhostStory),
        typeof(SyaoranTalisman),
        typeof(DaoistSupport),
        typeof(TomoyoCamera),
        typeof(TomoyoBond),
        typeof(SyaoranBond),
        typeof(DreamCostume),
        typeof(BlessingOfTheNamelessBook),
        typeof(DreamWand),
        typeof(DreamCompass),
        typeof(Stabilize),
        typeof(MagicAwakening),
        typeof(ForbiddenMagic),
        typeof(DreamKeyGlow),
        typeof(DreamKeyRevelation),
        typeof(StarWand),
        typeof(SealedBook),
        typeof(DreamKeyResonance),
        typeof(RollerbladeDash),
        typeof(MagicBarrier),
        typeof(CerberusTrueForm),
        typeof(YueTrueForm),
        typeof(FourSymbols),
        typeof(DreamsEnd),
        typeof(Echo)
    ];

    private static readonly IReadOnlyList<Type> PartnerCardTypesInternal =
    [
        typeof(KeroBond),
        typeof(KeroRecon),
        typeof(KeroSnackBreak),
        typeof(TomoyoCostume),
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
        typeof(FujitakaNote),
        typeof(NaokoGhostStory),
        typeof(CerberusTrueForm),
        typeof(YueTrueForm)
    ];

    private static readonly IReadOnlyList<Type> StarterCardTypesInternal =
    [
        typeof(Gale),
        typeof(Siege),
        typeof(Stabilize),
        typeof(DreamWand)
    ];

    private static readonly IReadOnlyList<Type> TsubasaCardTypesInternal =
    [
        typeof(VoidBond),
        typeof(TsubasaAnotherMe),
        typeof(EquivalentExchange),
        typeof(CopiedSoul),
        typeof(SleepingWings),
        typeof(MemoryFeather),
        typeof(DimensionalDrift)
    ];

    private static readonly IReadOnlyList<Type> EventOnlyCardTypesInternal =
    [
        typeof(MemoryFracture),
        ..TsubasaCardTypesInternal
    ];

    private static readonly HashSet<Type> TransparentCardTypeSet = TransparentCardTypesInternal.ToHashSet();
    private static readonly HashSet<Type> DefaultManifestExcludedTransparentCardTypeSet =
        DefaultManifestExcludedTransparentCardTypes.ToHashSet();
    private static readonly IReadOnlyList<Type> DefaultManifestAtlasTypesInternal =
        TransparentCardTypesInternal
            .Where(type => !DefaultManifestExcludedTransparentCardTypeSet.Contains(type))
            .ToList();
    private static readonly HashSet<Type> DefaultManifestAtlasTypeSet = DefaultManifestAtlasTypesInternal.ToHashSet();
    private static readonly HashSet<Type> PartnerCardTypeSet = PartnerCardTypesInternal.ToHashSet();
    private static readonly HashSet<Type> StarterCardTypeSet = StarterCardTypesInternal.ToHashSet();
    private static readonly HashSet<Type> TsubasaCardTypeSet = TsubasaCardTypesInternal.ToHashSet();
    private static readonly HashSet<Type> EventOnlyCardTypeSet = EventOnlyCardTypesInternal.ToHashSet();
    private static readonly IReadOnlyList<Type> AllCardTypesInternal =
        TransparentCardTypesInternal
            .Concat(SupportCardTypesInternal)
            .Concat(EventOnlyCardTypesInternal)
            .ToList();
    private static readonly IReadOnlyList<Type> TechniqueCardTypesInternal =
        SupportCardTypesInternal
            .Where(type => !PartnerCardTypeSet.Contains(type))
            .ToList();
    private static readonly HashSet<Type> TechniqueCardTypeSet = TechniqueCardTypesInternal.ToHashSet();

    public static IReadOnlyList<Type> AllCardTypes => AllCardTypesInternal;
    public static IReadOnlyList<Type> TransparentCardTypes => TransparentCardTypesInternal;
    public static IReadOnlyList<Type> DefaultManifestAtlasTypes => DefaultManifestAtlasTypesInternal;
    public static IReadOnlyList<Type> PartnerCardTypes => PartnerCardTypesInternal;
    public static IReadOnlyList<Type> TechniqueCardTypes => TechniqueCardTypesInternal;
    public static IReadOnlyList<Type> StarterCardTypes => StarterCardTypesInternal;
    public static IReadOnlyList<Type> TsubasaCardTypes => TsubasaCardTypesInternal;

    public static CardModel[] AllCardTemplates() =>
        AllCardTypesInternal
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

    public static bool IsSupportCard(CardModel card) =>
        card is SakuraModCard && !IsTransparentCard(card) && !IsEventOnlyCard(card);

    public static bool IsPartnerCard(CardModel card) =>
        PartnerCardTypeSet.Contains(card.GetType());

    public static bool IsTechniqueCard(CardModel card) =>
        card is SakuraModCard && TechniqueCardTypeSet.Contains(card.GetType());

    public static bool IsTsubasaCard(CardModel card) =>
        TsubasaCardTypeSet.Contains(card.GetType());

    public static bool IsEventOnlyCard(CardModel card) =>
        EventOnlyCardTypeSet.Contains(card.GetType());

    public static bool IsStarterCard(CardModel card) =>
        StarterCardTypeSet.Contains(card.GetType());

    public static bool IsStarterCard<T>(CardModel card) where T : CardModel =>
        card.GetType() == typeof(T);

    public static bool IsStrikeEquivalentStarterCard(CardModel card) =>
        IsStarterCard<Gale>(card);

    public static bool IsDefendEquivalentStarterCard(CardModel card) =>
        IsStarterCard<Siege>(card);

    public static bool IsBasicStrikeOrDefendEquivalent(CardModel card) =>
        card.Rarity == CardRarity.Basic
        && (IsStrikeEquivalentStarterCard(card) || IsDefendEquivalentStarterCard(card));

    public static bool IsRemovableStarterCard(CardModel card) =>
        IsStarterCard(card) && card.IsRemovable;

    public static bool IsTransformableStarterCard(CardModel card) =>
        IsStarterCard(card) && card.IsTransformable;

    public static bool CanReplaceStrikeOrDefendPair(Player player) =>
        CountRemovable<Gale>(player) >= 2 || CountRemovable<Siege>(player) >= 2;

    public static int CountRemovable<T>(Player player) where T : CardModel =>
        player.Deck.Cards.Count(card => IsStarterCard<T>(card) && card.IsRemovable);

    public static IReadOnlyList<CardModel> PartnerTemplates() =>
        PartnerCardTypesInternal.Select(CardTemplate).ToList();

    public static IReadOnlyList<CardModel> RewardableSupportCardTemplates(Player owner) =>
        ModelDb.CardPool<SakuraModCardPool>()
            .GetUnlockedCards(owner.UnlockState, owner.RunState.CardMultiplayerConstraint)
            .Where(IsSupportCard)
            .Where(card => card.Rarity is not (CardRarity.Basic or CardRarity.Ancient or CardRarity.Event or CardRarity.Token))
            .ToList();

    public static CardModel CreateCleanTransparentCard(Player owner, Type type)
    {
        if (!TransparentCardTypeSet.Contains(type))
            throw new ArgumentException($"{type.Name} is not a Transparent Card.", nameof(type));

        var card = owner.RunState.CreateCard(CardTemplate(type), owner);
        card.RemovePlaybackStateExceptRelease();
        card.RemoveRelease();
        return card;
    }
}
