using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Cards;

namespace SakuraMod.SakuraModCode.Character;

public enum SourceCardIdentity
{
    Action,
    Appear,
    Arrow,
    Aqua,
    Big,
    Blade,
    Blank,
    Blaze,
    Bubbles,
    Break,
    Change,
    Choice,
    Cloud,
    Create,
    Dark,
    Dash,
    Dream,
    Dreaming,
    Earthy,
    Erase,
    Exchange,
    Fight,
    Firey,
    Float,
    Flight,
    Flower,
    Fly,
    Freeze,
    Gale,
    Glow,
    Gravitation,
    Hail,
    Hope,
    Illusion,
    Kindness,
    Labyrinth,
    Lucid,
    Sword,
    Shield,
    Jump,
    Libra,
    Light,
    Little,
    Lock,
    Loop,
    Love,
    Maze,
    Mirage,
    Mirror,
    Mist,
    Move,
    Nothing,
    Power,
    Promise,
    Rain,
    Record,
    Reflect,
    Remind,
    Repair,
    Return,
    Reversal,
    Rewind,
    Sand,
    Shadow,
    Shade,
    Shot,
    Siege,
    Silent,
    Sleep,
    Snooze,
    Snow,
    Song,
    Spiral,
    Storm,
    Struggle,
    Sweet,
    Swing,
    Synchronize,
    Through,
    Thunder,
    Time,
    Transfer,
    TrueOrFalse,
    Twin,
    Voice,
    Watery,
    Wave,
    Windy,
    Wood
}

internal enum SourceEraClass
{
    Clow,
    Sakura,
    Clear
}

internal enum SakuraSourceCardVisualRoute
{
    Classic,
    Clear,
    Vanilla
}

internal sealed record SakuraCardMetadata(
    Type CardType,
    SourceCardIdentity? Identity,
    SourceEraClass? Era,
    int CatalogOrder,
    SakuraSourceCardVisualRoute VisualRoute);

internal static class SakuraCardCatalog
{
    private static readonly IReadOnlyList<SakuraCardMetadata> EntriesInternal = BuildEntries();
    private static readonly IReadOnlyDictionary<Type, SakuraCardMetadata> EntriesByType =
        EntriesInternal.ToDictionary(static entry => entry.CardType);
    private static readonly IReadOnlyDictionary<(SourceCardIdentity Identity, SourceEraClass Era), Type> TypesByIdentityAndEra =
        EntriesInternal
            .Where(static entry => entry.Identity.HasValue && entry.Era.HasValue)
            .ToDictionary(
                static entry => (entry.Identity!.Value, entry.Era!.Value),
                static entry => entry.CardType);
    private static readonly IReadOnlyDictionary<SourceEraClass, IReadOnlyList<Type>> SourceTypesByEra =
        Enum.GetValues<SourceEraClass>()
            .ToDictionary(
                static era => era,
                static era => (IReadOnlyList<Type>)Array.AsReadOnly(
                    EntriesInternal
                        .Where(entry => entry.Era == era)
                        .Select(static entry => entry.CardType)
                        .ToArray()));

    public static IReadOnlyList<SakuraCardMetadata> Entries => EntriesInternal;
    public static IReadOnlyList<Type> PoolCardTypes { get; } =
        Array.AsReadOnly(EntriesInternal.Select(static entry => entry.CardType).ToArray());
    public static IReadOnlyList<Type> ClassicLayoutCardTypes { get; } =
        Array.AsReadOnly(
            EntriesInternal
                .Where(static entry => entry.Era is SourceEraClass.Clow or SourceEraClass.Sakura
                    || typeof(SpellCard).IsAssignableFrom(entry.CardType))
                .Select(static entry => entry.CardType)
                .ToArray());

    public static IReadOnlyList<Type> SourceCardTypes(SourceEraClass era)
    {
        RequireDefinedEra(era);
        return SourceTypesByEra[era];
    }

    public static CardModel[] PoolCardTemplates() =>
        PoolCardTypes.Select(CardTemplate).ToArray();

    public static bool IsPoolCard(CardModel card) =>
        EntriesByType.ContainsKey(card.GetType());

    public static bool IsPoolCardType(Type type) =>
        EntriesByType.ContainsKey(type);

    public static bool TryGetMetadata(CardModel card, out SakuraCardMetadata metadata) =>
        TryGetMetadata(card.GetType(), out metadata);

    public static bool TryGetMetadata(Type type, out SakuraCardMetadata metadata) =>
        EntriesByType.TryGetValue(type, out metadata!);

    public static SakuraCardMetadata MetadataFor(Type type) =>
        EntriesByType.TryGetValue(type, out var metadata)
            ? metadata
            : throw new KeyNotFoundException($"No Source Card metadata is registered for {type.Name}.");

    public static Type? TypeFor(SourceCardIdentity identity, SourceEraClass era)
    {
        RequireDefinedIdentity(identity);
        RequireDefinedEra(era);
        return TypesByIdentityAndEra.TryGetValue((identity, era), out var type) ? type : null;
    }

    internal static void ValidateEntries(IReadOnlyList<SakuraCardMetadata> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var seenTypes = new HashSet<Type>();
        var seenOrders = new HashSet<int>();
        var seenIdentityEras = new HashSet<(SourceCardIdentity Identity, SourceEraClass Era)>();

        foreach (var entry in entries)
        {
            if (entry.CardType is null)
                throw new InvalidOperationException("Source Card metadata contains a null card type.");
            if (!typeof(CardModel).IsAssignableFrom(entry.CardType))
                throw new InvalidOperationException($"{entry.CardType.Name} is not a card model type.");
            if (!seenTypes.Add(entry.CardType))
                throw new InvalidOperationException($"Duplicate Source Card type registration: {entry.CardType.Name}.");
            if (entry.CatalogOrder < 0 || !seenOrders.Add(entry.CatalogOrder))
                throw new InvalidOperationException($"Duplicate or invalid Source Card catalog order: {entry.CatalogOrder}.");
            if (!Enum.IsDefined(entry.VisualRoute))
                throw new InvalidOperationException($"Invalid Source Card visual route for {entry.CardType.Name}.");

            var hasIdentity = entry.Identity.HasValue;
            var hasEra = entry.Era.HasValue;
            if (hasIdentity != hasEra)
                throw new InvalidOperationException($"{entry.CardType.Name} must define both Source Card identity and era, or neither.");

            if (hasIdentity)
            {
                RequireDefinedIdentity(entry.Identity!.Value);
                RequireDefinedEra(entry.Era!.Value);
                if (!seenIdentityEras.Add((entry.Identity.Value, entry.Era.Value)))
                    throw new InvalidOperationException($"Duplicate Source Card identity {entry.Identity} in the {entry.Era} era.");
                if (entry.Era == SourceEraClass.Clear && entry.VisualRoute != SakuraSourceCardVisualRoute.Clear)
                    throw new InvalidOperationException($"Clear Card {entry.CardType.Name} must use the Clear visual route.");
                if (entry.Era != SourceEraClass.Clear && entry.VisualRoute == SakuraSourceCardVisualRoute.Clear)
                    throw new InvalidOperationException($"Non-Clear card {entry.CardType.Name} cannot use the Clear visual route.");
            }
            else if (!typeof(SpellCard).IsAssignableFrom(entry.CardType)
                     && !typeof(SakuraAncientCard).IsAssignableFrom(entry.CardType))
            {
                throw new InvalidOperationException($"Era-neutral catalog metadata is not allowed for {entry.CardType.Name}.");
            }
        }

        if (seenOrders.Count != entries.Count || seenOrders.Any(order => order >= entries.Count))
            throw new InvalidOperationException("Source Card catalog order must be contiguous from zero.");
    }

    private static IReadOnlyList<SakuraCardMetadata> BuildEntries()
    {
        var entries = new List<SakuraCardMetadata>();

        void Add<TCard>(SourceCardIdentity identity, SourceEraClass era, SakuraSourceCardVisualRoute route)
            where TCard : CardModel =>
            entries.Add(new(typeof(TCard), identity, era, entries.Count, route));

        void AddClassic<TCard>(SourceCardIdentity identity, SourceEraClass era)
            where TCard : CardModel =>
            Add<TCard>(identity, era, SakuraSourceCardVisualRoute.Classic);

        void AddSpell<TCard>() where TCard : SpellCard =>
            entries.Add(new(typeof(TCard), null, null, entries.Count, SakuraSourceCardVisualRoute.Classic));

        void AddAncient<TCard>() where TCard : SakuraAncientCard =>
            entries.Add(new(typeof(TCard), null, null, entries.Count, SakuraSourceCardVisualRoute.Vanilla));

        AddClassic<ClowSword>(SourceCardIdentity.Sword, SourceEraClass.Clow);
        AddClassic<ClowShield>(SourceCardIdentity.Shield, SourceEraClass.Clow);
        AddClassic<ClowArrow>(SourceCardIdentity.Arrow, SourceEraClass.Clow);
        AddClassic<ClowBig>(SourceCardIdentity.Big, SourceEraClass.Clow);
        AddClassic<ClowBubbles>(SourceCardIdentity.Bubbles, SourceEraClass.Clow);
        AddClassic<ClowChange>(SourceCardIdentity.Change, SourceEraClass.Clow);
        AddClassic<ClowCreate>(SourceCardIdentity.Create, SourceEraClass.Clow);
        AddClassic<ClowDark>(SourceCardIdentity.Dark, SourceEraClass.Clow);
        AddClassic<ClowDash>(SourceCardIdentity.Dash, SourceEraClass.Clow);
        AddClassic<ClowDream>(SourceCardIdentity.Dream, SourceEraClass.Clow);
        AddClassic<ClowEarthy>(SourceCardIdentity.Earthy, SourceEraClass.Clow);
        AddClassic<ClowErase>(SourceCardIdentity.Erase, SourceEraClass.Clow);
        AddClassic<ClowFight>(SourceCardIdentity.Fight, SourceEraClass.Clow);
        AddClassic<ClowFirey>(SourceCardIdentity.Firey, SourceEraClass.Clow);
        AddClassic<ClowFloat>(SourceCardIdentity.Float, SourceEraClass.Clow);
        AddClassic<ClowFly>(SourceCardIdentity.Fly, SourceEraClass.Clow);
        AddClassic<ClowFreeze>(SourceCardIdentity.Freeze, SourceEraClass.Clow);
        AddClassic<ClowGlow>(SourceCardIdentity.Glow, SourceEraClass.Clow);
        AddClassic<ClowJump>(SourceCardIdentity.Jump, SourceEraClass.Clow);
        AddClassic<ClowIllusion>(SourceCardIdentity.Illusion, SourceEraClass.Clow);
        AddClassic<ClowLibra>(SourceCardIdentity.Libra, SourceEraClass.Clow);
        AddClassic<ClowLight>(SourceCardIdentity.Light, SourceEraClass.Clow);
        AddClassic<ClowLittle>(SourceCardIdentity.Little, SourceEraClass.Clow);
        AddClassic<ClowLock>(SourceCardIdentity.Lock, SourceEraClass.Clow);
        AddClassic<ClowLoop>(SourceCardIdentity.Loop, SourceEraClass.Clow);
        AddClassic<ClowMaze>(SourceCardIdentity.Maze, SourceEraClass.Clow);
        AddClassic<ClowMirror>(SourceCardIdentity.Mirror, SourceEraClass.Clow);
        AddClassic<ClowMist>(SourceCardIdentity.Mist, SourceEraClass.Clow);
        AddClassic<ClowMove>(SourceCardIdentity.Move, SourceEraClass.Clow);
        AddClassic<ClowPower>(SourceCardIdentity.Power, SourceEraClass.Clow);
        AddClassic<ClowRain>(SourceCardIdentity.Rain, SourceEraClass.Clow);
        AddClassic<ClowReturn>(SourceCardIdentity.Return, SourceEraClass.Clow);
        AddClassic<ClowSand>(SourceCardIdentity.Sand, SourceEraClass.Clow);
        AddClassic<ClowShadow>(SourceCardIdentity.Shadow, SourceEraClass.Clow);
        AddClassic<ClowShot>(SourceCardIdentity.Shot, SourceEraClass.Clow);
        AddClassic<ClowSilent>(SourceCardIdentity.Silent, SourceEraClass.Clow);
        AddClassic<ClowSleep>(SourceCardIdentity.Sleep, SourceEraClass.Clow);
        AddClassic<ClowSnow>(SourceCardIdentity.Snow, SourceEraClass.Clow);
        AddClassic<ClowSong>(SourceCardIdentity.Song, SourceEraClass.Clow);
        AddClassic<ClowStorm>(SourceCardIdentity.Storm, SourceEraClass.Clow);
        AddClassic<ClowSweet>(SourceCardIdentity.Sweet, SourceEraClass.Clow);
        AddClassic<ClowThrough>(SourceCardIdentity.Through, SourceEraClass.Clow);
        AddClassic<ClowThunder>(SourceCardIdentity.Thunder, SourceEraClass.Clow);
        AddClassic<ClowTime>(SourceCardIdentity.Time, SourceEraClass.Clow);
        AddClassic<ClowTwin>(SourceCardIdentity.Twin, SourceEraClass.Clow);
        AddClassic<ClowVoice>(SourceCardIdentity.Voice, SourceEraClass.Clow);
        AddClassic<ClowCloud>(SourceCardIdentity.Cloud, SourceEraClass.Clow);
        AddClassic<ClowFlower>(SourceCardIdentity.Flower, SourceEraClass.Clow);
        AddClassic<ClowWatery>(SourceCardIdentity.Watery, SourceEraClass.Clow);
        AddClassic<ClowWave>(SourceCardIdentity.Wave, SourceEraClass.Clow);
        AddClassic<ClowWindy>(SourceCardIdentity.Windy, SourceEraClass.Clow);
        AddClassic<ClowWood>(SourceCardIdentity.Wood, SourceEraClass.Clow);

        AddClassic<SakuraArrow>(SourceCardIdentity.Arrow, SourceEraClass.Sakura);
        AddClassic<SakuraBig>(SourceCardIdentity.Big, SourceEraClass.Sakura);
        AddClassic<SakuraBubbles>(SourceCardIdentity.Bubbles, SourceEraClass.Sakura);
        AddClassic<SakuraChange>(SourceCardIdentity.Change, SourceEraClass.Sakura);
        AddClassic<SakuraCreate>(SourceCardIdentity.Create, SourceEraClass.Sakura);
        AddClassic<SakuraDark>(SourceCardIdentity.Dark, SourceEraClass.Sakura);
        AddClassic<SakuraDash>(SourceCardIdentity.Dash, SourceEraClass.Sakura);
        AddClassic<SakuraDream>(SourceCardIdentity.Dream, SourceEraClass.Sakura);
        AddClassic<SakuraEarthy>(SourceCardIdentity.Earthy, SourceEraClass.Sakura);
        AddClassic<SakuraErase>(SourceCardIdentity.Erase, SourceEraClass.Sakura);
        AddClassic<SakuraFight>(SourceCardIdentity.Fight, SourceEraClass.Sakura);
        AddClassic<SakuraFirey>(SourceCardIdentity.Firey, SourceEraClass.Sakura);
        AddClassic<SakuraFloat>(SourceCardIdentity.Float, SourceEraClass.Sakura);
        AddClassic<SakuraFly>(SourceCardIdentity.Fly, SourceEraClass.Sakura);
        AddClassic<SakuraFreeze>(SourceCardIdentity.Freeze, SourceEraClass.Sakura);
        AddClassic<SakuraGlow>(SourceCardIdentity.Glow, SourceEraClass.Sakura);
        AddClassic<SakuraSword>(SourceCardIdentity.Sword, SourceEraClass.Sakura);
        AddClassic<SakuraShield>(SourceCardIdentity.Shield, SourceEraClass.Sakura);
        AddClassic<SakuraJump>(SourceCardIdentity.Jump, SourceEraClass.Sakura);
        AddClassic<SakuraIllusion>(SourceCardIdentity.Illusion, SourceEraClass.Sakura);
        AddClassic<SakuraLibra>(SourceCardIdentity.Libra, SourceEraClass.Sakura);
        AddClassic<SakuraLight>(SourceCardIdentity.Light, SourceEraClass.Sakura);
        AddClassic<SakuraLittle>(SourceCardIdentity.Little, SourceEraClass.Sakura);
        AddClassic<SakuraLock>(SourceCardIdentity.Lock, SourceEraClass.Sakura);
        AddClassic<SakuraLoop>(SourceCardIdentity.Loop, SourceEraClass.Sakura);
        AddClassic<SakuraMaze>(SourceCardIdentity.Maze, SourceEraClass.Sakura);
        AddClassic<SakuraMirror>(SourceCardIdentity.Mirror, SourceEraClass.Sakura);
        AddClassic<SakuraMist>(SourceCardIdentity.Mist, SourceEraClass.Sakura);
        AddClassic<SakuraMove>(SourceCardIdentity.Move, SourceEraClass.Sakura);
        AddClassic<SakuraPower>(SourceCardIdentity.Power, SourceEraClass.Sakura);
        AddClassic<SakuraRain>(SourceCardIdentity.Rain, SourceEraClass.Sakura);
        AddClassic<SakuraReturn>(SourceCardIdentity.Return, SourceEraClass.Sakura);
        AddClassic<SakuraSand>(SourceCardIdentity.Sand, SourceEraClass.Sakura);
        AddClassic<SakuraShadow>(SourceCardIdentity.Shadow, SourceEraClass.Sakura);
        AddClassic<SakuraShot>(SourceCardIdentity.Shot, SourceEraClass.Sakura);
        AddClassic<SakuraSilent>(SourceCardIdentity.Silent, SourceEraClass.Sakura);
        AddClassic<SakuraSleep>(SourceCardIdentity.Sleep, SourceEraClass.Sakura);
        AddClassic<SakuraSnow>(SourceCardIdentity.Snow, SourceEraClass.Sakura);
        AddClassic<SakuraSong>(SourceCardIdentity.Song, SourceEraClass.Sakura);
        AddClassic<SakuraStorm>(SourceCardIdentity.Storm, SourceEraClass.Sakura);
        AddClassic<SakuraSweet>(SourceCardIdentity.Sweet, SourceEraClass.Sakura);
        AddClassic<SakuraThrough>(SourceCardIdentity.Through, SourceEraClass.Sakura);
        AddClassic<SakuraThunder>(SourceCardIdentity.Thunder, SourceEraClass.Sakura);
        AddClassic<SakuraTime>(SourceCardIdentity.Time, SourceEraClass.Sakura);
        AddClassic<SakuraTwin>(SourceCardIdentity.Twin, SourceEraClass.Sakura);
        AddClassic<SakuraVoice>(SourceCardIdentity.Voice, SourceEraClass.Sakura);
        AddClassic<SakuraCloud>(SourceCardIdentity.Cloud, SourceEraClass.Sakura);
        AddClassic<SakuraFlower>(SourceCardIdentity.Flower, SourceEraClass.Sakura);
        AddClassic<SakuraWatery>(SourceCardIdentity.Watery, SourceEraClass.Sakura);
        AddClassic<SakuraWave>(SourceCardIdentity.Wave, SourceEraClass.Sakura);
        AddClassic<SakuraWindy>(SourceCardIdentity.Windy, SourceEraClass.Sakura);
        AddClassic<SakuraWood>(SourceCardIdentity.Wood, SourceEraClass.Sakura);

        AddClassic<ClowNothing>(SourceCardIdentity.Nothing, SourceEraClass.Clow);
        AddClassic<SakuraLove>(SourceCardIdentity.Love, SourceEraClass.Sakura);
        AddClassic<SakuraHope>(SourceCardIdentity.Hope, SourceEraClass.Sakura);
        AddAncient<GrowingMagic>();
        AddAncient<AnotherMe>();

        AddSpell<SpellSeal>();
        AddSpell<SpellRelease>();
        AddSpell<SpellTurn>();
        AddSpell<SpellEmptySpell>();
        AddSpell<SpellHuoShen>();
        AddSpell<SpellLeiDi>();
        AddSpell<SpellFengHua>();
        AddSpell<SpellShuiLong>();

        Add<Gale>(SourceCardIdentity.Gale, SourceEraClass.Clear, SakuraSourceCardVisualRoute.Clear);
        Add<Reflect>(SourceCardIdentity.Reflect, SourceEraClass.Clear, SakuraSourceCardVisualRoute.Clear);
        Add<Flight>(SourceCardIdentity.Flight, SourceEraClass.Clear, SakuraSourceCardVisualRoute.Clear);
        Add<global::SakuraMod.SakuraModCode.Cards.Action>(SourceCardIdentity.Action, SourceEraClass.Clear, SakuraSourceCardVisualRoute.Clear);
        Add<Appear>(SourceCardIdentity.Appear, SourceEraClass.Clear, SakuraSourceCardVisualRoute.Clear);
        Add<Aqua>(SourceCardIdentity.Aqua, SourceEraClass.Clear, SakuraSourceCardVisualRoute.Clear);
        Add<Blade>(SourceCardIdentity.Blade, SourceEraClass.Clear, SakuraSourceCardVisualRoute.Clear);
        Add<Hail>(SourceCardIdentity.Hail, SourceEraClass.Clear, SakuraSourceCardVisualRoute.Clear);
        Add<Lucid>(SourceCardIdentity.Lucid, SourceEraClass.Clear, SakuraSourceCardVisualRoute.Clear);
        Add<Shade>(SourceCardIdentity.Shade, SourceEraClass.Clear, SakuraSourceCardVisualRoute.Clear);
        Add<Siege>(SourceCardIdentity.Siege, SourceEraClass.Clear, SakuraSourceCardVisualRoute.Clear);
        Add<Swing>(SourceCardIdentity.Swing, SourceEraClass.Clear, SakuraSourceCardVisualRoute.Clear);
        Add<Break>(SourceCardIdentity.Break, SourceEraClass.Clear, SakuraSourceCardVisualRoute.Clear);
        Add<Choice>(SourceCardIdentity.Choice, SourceEraClass.Clear, SakuraSourceCardVisualRoute.Clear);
        Add<Promise>(SourceCardIdentity.Promise, SourceEraClass.Clear, SakuraSourceCardVisualRoute.Clear);
        Add<Struggle>(SourceCardIdentity.Struggle, SourceEraClass.Clear, SakuraSourceCardVisualRoute.Clear);
        Add<Blaze>(SourceCardIdentity.Blaze, SourceEraClass.Clear, SakuraSourceCardVisualRoute.Clear);
        Add<Dreaming>(SourceCardIdentity.Dreaming, SourceEraClass.Clear, SakuraSourceCardVisualRoute.Clear);
        Add<Gravitation>(SourceCardIdentity.Gravitation, SourceEraClass.Clear, SakuraSourceCardVisualRoute.Clear);
        Add<Mirage>(SourceCardIdentity.Mirage, SourceEraClass.Clear, SakuraSourceCardVisualRoute.Clear);
        Add<Record>(SourceCardIdentity.Record, SourceEraClass.Clear, SakuraSourceCardVisualRoute.Clear);
        Add<Exchange>(SourceCardIdentity.Exchange, SourceEraClass.Clear, SakuraSourceCardVisualRoute.Clear);
        Add<Kindness>(SourceCardIdentity.Kindness, SourceEraClass.Clear, SakuraSourceCardVisualRoute.Clear);
        Add<Labyrinth>(SourceCardIdentity.Labyrinth, SourceEraClass.Clear, SakuraSourceCardVisualRoute.Clear);
        Add<Repair>(SourceCardIdentity.Repair, SourceEraClass.Clear, SakuraSourceCardVisualRoute.Clear);
        Add<Reversal>(SourceCardIdentity.Reversal, SourceEraClass.Clear, SakuraSourceCardVisualRoute.Clear);
        Add<Rewind>(SourceCardIdentity.Rewind, SourceEraClass.Clear, SakuraSourceCardVisualRoute.Clear);
        Add<Snooze>(SourceCardIdentity.Snooze, SourceEraClass.Clear, SakuraSourceCardVisualRoute.Clear);
        Add<Spiral>(SourceCardIdentity.Spiral, SourceEraClass.Clear, SakuraSourceCardVisualRoute.Clear);
        Add<Transfer>(SourceCardIdentity.Transfer, SourceEraClass.Clear, SakuraSourceCardVisualRoute.Clear);
        Add<Blank>(SourceCardIdentity.Blank, SourceEraClass.Clear, SakuraSourceCardVisualRoute.Clear);
        Add<Mirror>(SourceCardIdentity.Mirror, SourceEraClass.Clear, SakuraSourceCardVisualRoute.Clear);
        Add<Remind>(SourceCardIdentity.Remind, SourceEraClass.Clear, SakuraSourceCardVisualRoute.Clear);
        Add<Synchronize>(SourceCardIdentity.Synchronize, SourceEraClass.Clear, SakuraSourceCardVisualRoute.Clear);
        Add<global::SakuraMod.SakuraModCode.Cards.Time>(SourceCardIdentity.Time, SourceEraClass.Clear, SakuraSourceCardVisualRoute.Clear);
        Add<TrueOrFalse>(SourceCardIdentity.TrueOrFalse, SourceEraClass.Clear, SakuraSourceCardVisualRoute.Clear);

        ValidateEntries(entries);
        return entries.AsReadOnly();
    }

    private static void RequireDefinedIdentity(SourceCardIdentity identity)
    {
        if (!Enum.IsDefined(identity))
            throw new ArgumentOutOfRangeException(nameof(identity), identity, "Unknown Source Card identity.");
    }

    private static void RequireDefinedEra(SourceEraClass era)
    {
        if (!Enum.IsDefined(era))
            throw new ArgumentOutOfRangeException(nameof(era), era, "Unknown Source Card era.");
    }

    private static CardModel CardTemplate(Type type) =>
        ModelDb.GetById<CardModel>(ModelDb.GetId(type));
}
