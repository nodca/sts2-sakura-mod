using MegaCrit.Sts2.Core.Models.Acts;
using SakuraMod.SakuraModCode.Classic.Events;
using STS2RitsuLib.Content;

namespace SakuraMod.SakuraModCode.Events;

internal static class SakuraEventRegistration
{
    public static void Register()
    {
        var registry = ModContentRegistry.For(MainFile.ModId);

        registry.RegisterActEvent<Hive, ClassicXiaoLangsFeelingsEvent>();
        registry.RegisterActEvent<Glory, ClassicTheSealedCardEvent>();
    }
}
