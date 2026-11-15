using Godot;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Extensions;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;

namespace SakuraMod.SakuraModCode.Character;

public class ClassicSakuraCardPool : TypeListCardPoolModel
{
    private ShaderMaterial? _poolFrameMaterial;

    public override string Title => ClassicSakura.CharacterId;
    public override string BigEnergyIconPath => ClassicCardEnergyIcon.SakuraBigPath;
    public override string TextEnergyIconPath => ClassicCardEnergyIcon.SakuraTextPath;
    public override string EnergyColorName => "classic_sakura";
    public override Color DeckEntryCardColor => new("f8f0d7");
    public override bool IsColorless => false;
    public override Material? PoolFrameMaterial =>
        _poolFrameMaterial is not null && GodotObject.IsInstanceValid(_poolFrameMaterial)
            ? _poolFrameMaterial
            : _poolFrameMaterial = MaterialUtils.CreateHsvShaderMaterial(0.95f, 0.58f, 1.35f);

    internal static IReadOnlyList<Type> AllCardTypesForPool() =>
        SakuraCardCatalog.PoolCardTypes;

    internal static CardModel[] AllCardTemplatesForPool() =>
        SakuraCardCatalog.PoolCardTemplates();

}

public class ClassicSakuraRelicPool : TypeListRelicPoolModel
{
    public override Color LabOutlineColor => ClassicSakura.Color;
    public override string BigEnergyIconPath => ClassicCardEnergyIcon.SakuraBigPath;
    public override string TextEnergyIconPath => ClassicCardEnergyIcon.SakuraTextPath;
    public override string EnergyColorName => "classic_sakura";
}

public class ClassicSakuraPotionPool : TypeListPotionPoolModel
{
    public override Color LabOutlineColor => ClassicSakura.Color;
    public override string BigEnergyIconPath => ClassicCardEnergyIcon.SakuraBigPath;
    public override string TextEnergyIconPath => ClassicCardEnergyIcon.SakuraTextPath;
    public override string EnergyColorName => "classic_sakura";
}

internal static class ClassicCardEnergyIcon
{
    public static string ClowBigPath => "general/orb/classic_clow_energy.png".ClassicCardUiImagePath();
    public static string ClowTextPath => "general/orb/classic_clow_text_energy.png".ClassicCardUiImagePath();
    public static string SakuraBigPath => "general/orb/classic_sakura_energy.png".ClassicCardUiImagePath();
    public static string SakuraTextPath => "general/orb/classic_sakura_text_energy.png".ClassicCardUiImagePath();
}
