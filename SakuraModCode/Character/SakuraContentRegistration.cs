using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.Relics;
using STS2RitsuLib.Content;
using STS2RitsuLib;

namespace SakuraMod.SakuraModCode.Character;

internal static class SakuraContentRegistration
{
    public static void Register()
    {
        var registry = ModContentRegistry.For(MainFile.ModId);

        SakuraMemoryPile.Register();
        SakuraCardTextCapabilities.Register();
        SakuraSourceCardTextCapabilities.Register();
        ConfigureDefaultCardTextCapabilities(registry);
        registry.RegisterSingleton<SakuraDrawCountHook>();
        registry.RegisterSingleton<GrowingMagicDamageHook>();
        registry.RegisterCharacter<ClassicSakura>();
        RegisterCards(registry, typeof(ClassicSakuraCardPool), AllCardTypesForRegistration());
        RegisterPowers(registry, AllPowerTypesForRegistration());
        RegisterRelics(registry, typeof(ClassicSakuraRelicPool), SakuraRelicCatalog.AllRelicTypes());
        RitsuLibFramework.RegisterTouchOfOrobasRefinementMapping<ClassicSealedWandRelic, ClassicStarWandRelic>(MainFile.ModId);
        RitsuLibFramework.RegisterArchaicToothTranscendenceMapping<SpellSeal, GrowingMagic>(MainFile.ModId);
        RitsuLibFramework.RegisterDustyTomeCard<ClassicSakura, AnotherMe>(MainFile.ModId);
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
        registry.ConfigureDefaultModelCapabilities<SakuraCardModel>(
            "SakuraMod:DefaultSakuraCardText",
            static (_, capabilities) => capabilities.Add<SakuraCardHoverTipCapability>(),
            0);
        registry.ConfigureDefaultModelCapabilities<SakuraOptionCard>(
            "SakuraMod:DefaultSakuraOptionCardText",
            static (_, capabilities) => capabilities.Add<SakuraCardHoverTipCapability>(),
            0);
        registry.ConfigureDefaultModelCapabilities<SakuraSourceCard>(
            "SakuraMod:DefaultClassicSakuraCardText",
            static (_, capabilities) => capabilities.Add<SakuraSourceCardTextCapability>(),
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
