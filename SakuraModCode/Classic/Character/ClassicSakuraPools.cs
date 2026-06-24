using BaseLib.Abstracts;
using Godot;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Classic.Cards;
using SakuraMod.SakuraModCode.Classic.Relics;
using SakuraMod.SakuraModCode.Extensions;

namespace SakuraMod.SakuraModCode.Classic.Character;

public class ClassicSakuraCardPool : CustomCardPoolModel
{
    public override string Title => ClassicSakura.CharacterId;
    public override string BigEnergyIconPath => ClassicSakuraEnergyIcon.SakuraBigPath;
    public override string TextEnergyIconPath => ClassicSakuraEnergyIcon.SakuraTextPath;
    public override Color ShaderColor => new("D8A447");
    public override Color DeckEntryCardColor => new("f8f0d7");
    public override bool IsColorless => false;

    protected override CardModel[] GenerateAllCards() =>
        ClassicSakuraCardCatalog.AllCardTemplates();
}

public class ClassicClowCardVisualPool : CustomCardPoolModel
{
    public override string Title => $"{ClassicSakura.CharacterId}ClowVisual";
    public override string BigEnergyIconPath => ClassicSakuraEnergyIcon.ClowBigPath;
    public override string TextEnergyIconPath => ClassicSakuraEnergyIcon.ClowTextPath;
    public override Color ShaderColor => new("D8A447");
    public override Color DeckEntryCardColor => new("f8f0d7");
    public override bool IsColorless => false;
}

public class ClassicSakuraCardVisualPool : CustomCardPoolModel
{
    public override string Title => $"{ClassicSakura.CharacterId}SakuraVisual";
    public override string BigEnergyIconPath => ClassicSakuraEnergyIcon.SakuraBigPath;
    public override string TextEnergyIconPath => ClassicSakuraEnergyIcon.SakuraTextPath;
    public override Color ShaderColor => new("D8A447");
    public override Color DeckEntryCardColor => new("f8f0d7");
    public override bool IsColorless => false;
}

public class ClassicSakuraRelicPool : CustomRelicPoolModel
{
    public override Color LabOutlineColor => ClassicSakura.Color;
    public override string BigEnergyIconPath => ClassicSakuraEnergyIcon.SakuraBigPath;
    public override string TextEnergyIconPath => ClassicSakuraEnergyIcon.SakuraTextPath;

    protected override IEnumerable<RelicModel> GenerateAllRelics() =>
        ClassicSakuraExclusiveRelics.AllClassicRelics();
}

public class ClassicSakuraPotionPool : CustomPotionPoolModel
{
    public override Color LabOutlineColor => ClassicSakura.Color;
    public override string BigEnergyIconPath => ClassicSakuraEnergyIcon.SakuraBigPath;
    public override string TextEnergyIconPath => ClassicSakuraEnergyIcon.SakuraTextPath;
}

internal static class ClassicSakuraEnergyIcon
{
    public static string ClowBigPath => "general/orb/classic_clow_energy.png".ClassicCardUiImagePath();
    public static string ClowTextPath => "general/orb/classic_clow_text_energy.png".ClassicCardUiImagePath();
    public static string SakuraBigPath => "general/orb/classic_sakura_energy.png".ClassicCardUiImagePath();
    public static string SakuraTextPath => "general/orb/classic_sakura_text_energy.png".ClassicCardUiImagePath();
}
