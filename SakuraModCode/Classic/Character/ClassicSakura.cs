using BaseLib.Abstracts;
using BaseLib.Utils.NodeFactories;
using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.Combat;
using SakuraMod.SakuraModCode.Classic.Cards;
using SakuraMod.SakuraModCode.Classic.Relics;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Extensions;

namespace SakuraMod.SakuraModCode.Classic.Character;

public class ClassicSakura : PlaceholderCharacterModel
{
    public const string CharacterId = "ClassicSakura";

    public static readonly Color Color = new("f4a7c4");
    private static readonly Color ClassicMapDrawingColor = new("D8A447");

    public override Color NameColor => Color;
    public override Color MapDrawingColor => ClassicMapDrawingColor;
    public override CharacterGender Gender => CharacterGender.Neutral;
    public override int StartingHp => 70;
    public override int StartingGold => 99;
    public override int MaxEnergy => 3;
    public override int BaseOrbSlotCount => 0;

    public override IEnumerable<CardModel> StartingDeck =>
    [
        ModelDb.Card<ClowSword>(),
        ModelDb.Card<ClowSword>(),
        ModelDb.Card<ClowSword>(),
        ModelDb.Card<ClowSword>(),
        ModelDb.Card<ClowShield>(),
        ModelDb.Card<ClowShield>(),
        ModelDb.Card<ClowShield>(),
        ModelDb.Card<ClowShield>(),
        ModelDb.Card<SpellSeal>(),
        ModelDb.Card<SpellRelease>()
    ];

    public override IReadOnlyList<RelicModel> StartingRelics =>
    [
        ModelDb.Relic<ClassicSealedBookRelic>(),
        ModelDb.Relic<ClassicSealedWandRelic>()
    ];

    public override CardPoolModel CardPool => ModelDb.CardPool<ClassicSakuraCardPool>();
    public override RelicPoolModel RelicPool => ModelDb.RelicPool<ClassicSakuraRelicPool>();
    public override PotionPoolModel PotionPool => ModelDb.PotionPool<ClassicSakuraPotionPool>();

    public override Control CustomIcon
    {
        get
        {
            var icon = NodeFactory<Control>.CreateFromResource(CustomIconTexturePath);
            icon.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            return icon;
        }
    }

    public override string CustomIconTexturePath => "character_icon_char_name.png".CharacterUiPath();
    public override string CustomCharacterSelectIconPath => "char_select_char_name.png".CharacterUiPath();
    public override string CustomCharacterSelectLockedIconPath => "char_select_char_name_locked.png".CharacterUiPath();
    public override string CustomMapMarkerPath => "map_marker_char_name.png".CharacterUiPath();
    public override string CustomVisualPath => "charui/sakura_battle_standee.png".ImagePath();

    public override NCreatureVisuals CreateCustomVisuals() =>
        SakuraStandeeVisuals.Create(CustomVisualPath, "Classic Sakura");

    public override string CustomCharacterSelectBg =>
        Path.Join(MainFile.ResPath, "scenes", "screens", "char_select", "char_select_bg_classic_sakura.tscn");

    public override CustomEnergyCounter? CustomEnergyCounter => new(
        layer => layer == 1
            ? "charui/combat_energy_counter_badge.png".ImagePath()
            : "charui/empty_energy_counter_layer.png".ImagePath(),
        new Color("322a22"),
        new Color("f4e1a3"));
}
