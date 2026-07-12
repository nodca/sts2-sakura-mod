using Godot;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Extensions;
using STS2RitsuLib.Scaffolding.Content;

namespace SakuraMod.SakuraModCode.Classic.Character;

public class ClassicSakuraCardPool : TypeListCardPoolModel
{
    public override string Title => ClassicSakura.CharacterId;
    public override string BigEnergyIconPath => ClassicSakuraEnergyIcon.SakuraBigPath;
    public override string TextEnergyIconPath => ClassicSakuraEnergyIcon.SakuraTextPath;
    public override string EnergyColorName => "classic_sakura";
    public override Color DeckEntryCardColor => new("f8f0d7");
    public override bool IsColorless => false;

    internal static IReadOnlyList<Type> AllCardTypesForPool() =>
        SakuraSourceCardCatalog.PoolCardTypes;

    internal static CardModel[] AllCardTemplatesForPool() =>
        SakuraSourceCardCatalog.PoolCardTemplates();

}

public class ClassicSakuraRelicPool : TypeListRelicPoolModel
{
    public override Color LabOutlineColor => ClassicSakura.Color;
    public override string BigEnergyIconPath => ClassicSakuraEnergyIcon.SakuraBigPath;
    public override string TextEnergyIconPath => ClassicSakuraEnergyIcon.SakuraTextPath;
    public override string EnergyColorName => "classic_sakura";
}

public class ClassicSakuraPotionPool : TypeListPotionPoolModel
{
    public override Color LabOutlineColor => ClassicSakura.Color;
    public override string BigEnergyIconPath => ClassicSakuraEnergyIcon.SakuraBigPath;
    public override string TextEnergyIconPath => ClassicSakuraEnergyIcon.SakuraTextPath;
    public override string EnergyColorName => "classic_sakura";
}

internal static class ClassicSakuraEnergyIcon
{
    public static string ClowBigPath => "general/orb/classic_clow_energy.png".ClassicCardUiImagePath();
    public static string ClowTextPath => "general/orb/classic_clow_text_energy.png".ClassicCardUiImagePath();
    public static string SakuraBigPath => "general/orb/classic_sakura_energy.png".ClassicCardUiImagePath();
    public static string SakuraTextPath => "general/orb/classic_sakura_text_energy.png".ClassicCardUiImagePath();
}
