using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Classic.Cards;
using SakuraMod.SakuraModCode.Classic.Character;
using SakuraMod.SakuraModCode.Classic.Powers;
using SakuraMod.SakuraModCode.Classic.Relics;
using SakuraMod.SakuraModCode.Powers;
using STS2RitsuLib.Content;

namespace SakuraMod.SakuraModCode.Character;

internal static class SakuraContentRegistration
{
    public static void Register()
    {
        var registry = ModContentRegistry.For(MainFile.ModId);

        SakuraMemoryPile.Register();
        SakuraCardTextCapabilities.Register();
        ClassicSakuraCardTextCapabilities.Register();
        ConfigureDefaultCardTextCapabilities(registry);
        registry.RegisterSingleton<SakuraDrawCountHook>();
        registry.RegisterCharacter<ClassicSakura>();
        RegisterCards(registry, typeof(ClassicSakuraCardPool), AllCardTypesForRegistration());
        RegisterPowers(registry, AllPowerTypesForRegistration());
        RegisterRelics(registry, typeof(ClassicSakuraRelicPool), ClassicSakuraExclusiveRelics.AllClassicRelicTypes());
    }

    private static void RegisterCards(ModContentRegistry registry, Type poolType, IEnumerable<Type> cardTypes)
    {
        foreach (var cardType in cardTypes.Distinct())
        {
            registry.RegisterCard(poolType, cardType);
        }
    }

    private static void RegisterPowers(ModContentRegistry registry, IEnumerable<Type> powerTypes)
    {
        foreach (var powerType in powerTypes.Distinct())
        {
            registry.RegisterPower(powerType);
        }
    }

    private static void RegisterRelics(ModContentRegistry registry, Type poolType, IEnumerable<Type> relicTypes)
    {
        foreach (var relicType in relicTypes.Distinct())
        {
            registry.RegisterRelic(poolType, relicType);
        }
    }

    private static void ConfigureDefaultCardTextCapabilities(ModContentRegistry registry)
    {
        registry.ConfigureDefaultModelCapabilities<SakuraModCard>(
            "SakuraMod:DefaultSakuraCardText",
            static (_, capabilities) => capabilities.Add<SakuraCardHoverTipCapability>(),
            0);
        registry.ConfigureDefaultModelCapabilities<ClassicSakuraCard>(
            "SakuraMod:DefaultClassicSakuraCardText",
            static (_, capabilities) => capabilities.Add<ClassicSakuraCardTextCapability>(),
            0);
    }

    internal static IEnumerable<Type> AllPowerTypesForRegistration() =>
        typeof(SakuraContentRegistration).Assembly.GetTypes()
            .Where(static type =>
                !type.IsAbstract
                && typeof(PowerModel).IsAssignableFrom(type)
                && type.Namespace?.StartsWith("SakuraMod.SakuraModCode", StringComparison.Ordinal) == true)
            .OrderBy(static type => type.FullName, StringComparer.Ordinal);

    internal static IEnumerable<Type> AllCardTypesForRegistration() =>
        ClassicSakuraCardPool.AllCardTypesForPool()
            .Concat(SakuraOptionCardCatalog.CardTypes)
            .Distinct();
}
