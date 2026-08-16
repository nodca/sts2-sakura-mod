using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode;
using System.Reflection;

public sealed class SourceCardCatalogSuite
{
    [Fact]
    public void CatalogOwnsPoolMembershipAndDeterministicOrder()
    {
        var sourceEntries = SakuraCardCatalog.Entries;
        var sourcePoolTypes = SakuraCardCatalog.PoolCardTypes;
        var classicSakuraPoolTypes = ClassicSakuraCardPool.AllCardTypesForPool();
        RegressionTestHarness.Require(
            classicSakuraPoolTypes.SequenceEqual(sourcePoolTypes),
            "Expected Classic Sakura's card pool to delegate to the Source Card catalog without changing registration order.");
        RegressionTestHarness.Require(
            sourcePoolTypes.SequenceEqual(sourceEntries.Select(static entry => entry.CardType)),
            "Expected Source Card pool membership and deterministic order to come from the metadata entries.");
        RegressionTestHarness.Require(
            sourceEntries.Select(static entry => entry.CatalogOrder).SequenceEqual(Enumerable.Range(0, sourceEntries.Count)),
            "Expected Source Card catalog order to be contiguous and deterministic.");
        RegressionTestHarness.Require(
            sourceEntries.Select(static entry => entry.CardType).Distinct().Count() == sourceEntries.Count,
            "Expected one authoritative Source Card metadata entry per retained pool card type.");

    }

    [Fact]
    public void SourceErasExposeExpectedCardFamilies()
    {
        var clowSourceTypes = SakuraCardCatalog.SourceCardTypes(SourceEraClass.Clow);
        var sakuraSourceTypes = SakuraCardCatalog.SourceCardTypes(SourceEraClass.Sakura);
        var clearSourceTypes = SakuraCardCatalog.SourceCardTypes(SourceEraClass.Clear);
        RegressionTestHarness.Require(
            clowSourceTypes.Contains(typeof(ClowSword))
            && clowSourceTypes.Contains(typeof(ClowRain))
            && clowSourceTypes.Contains(typeof(ClowNothing)),
            "Expected starter, rewardable, and special Clow Cards to share the Clow source era.");
        RegressionTestHarness.Require(
            sakuraSourceTypes.Contains(typeof(SakuraSword))
            && sakuraSourceTypes.Contains(typeof(SakuraLove))
            && sakuraSourceTypes.Contains(typeof(SakuraHope))
            && !sakuraSourceTypes.Contains(typeof(AnotherMe))
            && !sakuraSourceTypes.Contains(typeof(GrowingMagic)),
            "Expected conversion and special Sakura Cards to share the Sakura source era while Ancient cards remain era-neutral.");
        RegressionTestHarness.Require(
            clearSourceTypes.SequenceEqual(SakuraTransparentCardCatalog.TransparentCardTypes),
            "Expected SakuraTransparentCardCatalog's Transparent Card view to derive from the Clear source era.");

    }

    [Fact]
    public void CardInstancesResolveAuthoritativeCatalogMetadata()
    {
        var sourceEntries = SakuraCardCatalog.Entries;
        foreach (var entry in sourceEntries)
        {
            var card = (CardModel)(Activator.CreateInstance(entry.CardType)
                ?? throw new InvalidOperationException($"Could not instantiate catalog card {entry.CardType.Name}."));
            RegressionTestHarness.Require(
                SakuraCardCatalog.MetadataFor(card.GetType()) == entry,
                $"Expected {entry.CardType.Name} to resolve its authoritative catalog entry.");
            if (card is SakuraSourceCard classicCard)
            {
                RegressionTestHarness.Require(
                    classicCard.Identity == entry.Identity && classicCard.Era == entry.Era,
                    $"Expected {entry.CardType.Name} instance identity and era to derive from its catalog entry.");
            }
        }

    }

    [Fact]
    public void CardTypesDoNotDuplicateCatalogIdentityState()
    {
        var duplicatedIdentityFields = typeof(SakuraSourceCard).Assembly.GetTypes()
            .Where(static type => typeof(SakuraSourceCard).IsAssignableFrom(type))
            .SelectMany(static type => type.GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            .Where(static field => field.FieldType == typeof(SourceCardIdentity)
                || field.FieldType == typeof(SourceEraClass))
            .ToList();
        RegressionTestHarness.Require(
            duplicatedIdentityFields.Count == 0,
            $"Expected Classic card instances not to store catalog identity/era fields; found: {string.Join(", ", duplicatedIdentityFields.Select(static field => $"{field.DeclaringType?.Name}.{field.Name}"))}.");
        var duplicatedIdentityParameters = typeof(SakuraSourceCard).Assembly.GetTypes()
            .Where(static type => typeof(SakuraSourceCard).IsAssignableFrom(type))
            .SelectMany(static type => type.GetConstructors(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            .SelectMany(static constructor => constructor.GetParameters()
                .Select(parameter => (Constructor: constructor, Parameter: parameter)))
            .Where(static value => value.Parameter.ParameterType == typeof(SourceCardIdentity)
                || value.Parameter.ParameterType == typeof(SourceEraClass))
            .ToList();
        RegressionTestHarness.Require(
            duplicatedIdentityParameters.Count == 0,
            $"Expected Classic card constructors not to accept catalog identity/era; found: {string.Join(", ", duplicatedIdentityParameters.Select(static value => $"{value.Constructor.DeclaringType?.Name}.{value.Parameter.Name}"))}.");
    }

    [Fact]
    public void RepresentativeMetadataAndEraVocabularyRemainStable()
    {
        RegressionTestHarness.Require(
            SakuraCardCatalog.MetadataFor(typeof(ClowSword)) is
            { Identity: SourceCardIdentity.Sword, Era: SourceEraClass.Clow },
            "Expected Clow Sword to expose its Source Card identity and era.");
        RegressionTestHarness.Require(
            SakuraCardCatalog.MetadataFor(typeof(SakuraSword)) is
            { Identity: SourceCardIdentity.Sword, Era: SourceEraClass.Sakura },
            "Expected Sakura Sword to expose the same identity in the Sakura era.");
        RegressionTestHarness.Require(
            SakuraCardCatalog.MetadataFor(typeof(Gale)) is
            { Identity: SourceCardIdentity.Gale, Era: SourceEraClass.Clear },
            "Expected Gale to expose its Clear Card identity and era.");
        RegressionTestHarness.Require(
            SakuraCardCatalog.TypeFor(SourceCardIdentity.Sword, SourceEraClass.Clow) == typeof(ClowSword)
            && SakuraCardCatalog.TypeFor(SourceCardIdentity.Sword, SourceEraClass.Sakura) == typeof(SakuraSword),
            "Expected Clow and Sakura form lookup to derive from shared identity metadata.");
        var spellMetadata = SakuraCardCatalog.MetadataFor(typeof(SpellSeal));
        var anotherMeMetadata = SakuraCardCatalog.MetadataFor(typeof(AnotherMe));
        var growingMagicMetadata = SakuraCardCatalog.MetadataFor(typeof(GrowingMagic));
        RegressionTestHarness.Require(
            spellMetadata.Identity is null
            && spellMetadata.Era is null
            && spellMetadata.VisualRoute == SakuraSourceCardVisualRoute.Classic,
            "Expected Spell cards to remain in the pool and Classic visual route without becoming a Source Era Class.");
        RegressionTestHarness.Require(
            anotherMeMetadata is { Identity: null, Era: null, VisualRoute: SakuraSourceCardVisualRoute.Vanilla }
            && growingMagicMetadata is { Identity: null, Era: null, VisualRoute: SakuraSourceCardVisualRoute.Vanilla },
            "Expected Sakura Ancient cards to remain era-neutral and use the native Ancient visual route.");
        RegressionTestHarness.Require(
            Enum.GetValues<SourceEraClass>().SequenceEqual([
                SourceEraClass.Clow,
                SourceEraClass.Sakura,
                SourceEraClass.Clear
            ]),
            "Expected Source Era Class to contain exactly Clow, Sakura, and Clear.");

    }

    [Fact]
    public void CatalogViewsExcludeRemovedClearSupportCards()
    {
        var sourcePoolTypes = SakuraCardCatalog.PoolCardTypes;
        var clowSourceTypes = SakuraCardCatalog.SourceCardTypes(SourceEraClass.Clow);
        var sakuraSourceTypes = SakuraCardCatalog.SourceCardTypes(SourceEraClass.Sakura);
        var clearSourceTypes = SakuraCardCatalog.SourceCardTypes(SourceEraClass.Clear);
        RegressionTestHarness.RequireNoRemovedCardTypes("Source Card catalog", sourcePoolTypes, RegressionTestData.RemovedClearSupportCardTypeNames);
        RegressionTestHarness.RequireNoRemovedCardTypes("Clow source era", clowSourceTypes, RegressionTestData.RemovedClearSupportCardTypeNames);
        RegressionTestHarness.RequireNoRemovedCardTypes("Sakura source era", sakuraSourceTypes, RegressionTestData.RemovedClearSupportCardTypeNames);
        RegressionTestHarness.RequireNoRemovedCardTypes("Clear source era", clearSourceTypes, RegressionTestData.RemovedClearSupportCardTypeNames);

    }

    [Fact]
    public void CatalogValidationRejectsAmbiguousOrIncompleteEntries()
    {
        RegressionTestHarness.RequireThrows<InvalidOperationException>(
            () => SakuraCardCatalog.ValidateEntries([
                new(typeof(Gale), SourceCardIdentity.Gale, SourceEraClass.Clear, 0, SakuraSourceCardVisualRoute.Clear),
                new(typeof(Gale), SourceCardIdentity.Reflect, SourceEraClass.Clear, 1, SakuraSourceCardVisualRoute.Clear)
            ]),
            "Expected Source Card catalog validation to reject duplicate card types.");
        RegressionTestHarness.RequireThrows<InvalidOperationException>(
            () => SakuraCardCatalog.ValidateEntries([
                new(typeof(Gale), SourceCardIdentity.Gale, SourceEraClass.Clear, 0, SakuraSourceCardVisualRoute.Clear),
                new(typeof(Reflect), SourceCardIdentity.Gale, SourceEraClass.Clear, 1, SakuraSourceCardVisualRoute.Clear)
            ]),
            "Expected Source Card catalog validation to reject duplicate identities in the same era.");
        RegressionTestHarness.RequireThrows<InvalidOperationException>(
            () => SakuraCardCatalog.ValidateEntries([
                new(typeof(Gale), SourceCardIdentity.Gale, SourceEraClass.Clear, 0, SakuraSourceCardVisualRoute.Clear),
                new(typeof(Reflect), SourceCardIdentity.Reflect, SourceEraClass.Clear, 0, SakuraSourceCardVisualRoute.Clear)
            ]),
            "Expected Source Card catalog validation to reject duplicate catalog order values.");
        RegressionTestHarness.RequireThrows<ArgumentOutOfRangeException>(
            () => SakuraCardCatalog.ValidateEntries([
                new(typeof(Gale), SourceCardIdentity.Gale, (SourceEraClass)999, 0, SakuraSourceCardVisualRoute.Clear)
            ]),
            "Expected Source Card catalog validation to reject invalid source eras.");
        RegressionTestHarness.RequireThrows<InvalidOperationException>(
            () => SakuraCardCatalog.ValidateEntries([
                new(typeof(Gale), SourceCardIdentity.Gale, null, 0, SakuraSourceCardVisualRoute.Clear)
            ]),
            "Expected Source Card catalog validation to reject incomplete source metadata.");
        SakuraCardCatalog.ValidateEntries([
            new(typeof(SpellSeal), null, null, 0, SakuraSourceCardVisualRoute.Classic)
        ]);
        SakuraCardCatalog.ValidateEntries([
            new(typeof(GrowingMagic), null, null, 0, SakuraSourceCardVisualRoute.Vanilla)
        ]);
        SakuraCardCatalog.ValidateEntries([
            new(typeof(Gale), SourceCardIdentity.Gale, SourceEraClass.Clear, 0, SakuraSourceCardVisualRoute.Clear)
        ]);
        RegressionTestHarness.RequireThrows<KeyNotFoundException>(
            () => SakuraCardCatalog.MetadataFor(typeof(CardModel)),
            "Expected missing Source Card metadata lookups to fail explicitly.");
        var sourceMetadataFieldNames = typeof(SakuraCardMetadata)
            .GetProperties()
            .Select(static property => property.Name)
            .ToHashSet(StringComparer.Ordinal);
        RegressionTestHarness.Require(
            !sourceMetadataFieldNames.Overlaps(["Catalog", "Release", "Manifest", "Temporary"]),
            "Expected Source Card metadata not to encode removed or unresolved gameplay mechanics.");
    }
}
