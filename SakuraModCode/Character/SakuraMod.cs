using BaseLib.Abstracts;
using BaseLib.Utils.NodeFactories;
using SakuraMod.SakuraModCode.Extensions;
using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.Combat;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Relics;

namespace SakuraMod.SakuraModCode.Character;

public class SakuraMod : PlaceholderCharacterModel
{
    public const string CharacterId = "SakuraMod";

    public static readonly Color Color = new("ffffff");
    private static readonly Color SakuraMapDrawingColor = new("F4A7C4");

    public override Color NameColor => Color;
    public override Color MapDrawingColor => SakuraMapDrawingColor;
    public override CharacterGender Gender => CharacterGender.Neutral;
    public override int StartingHp => 70;

    public override IEnumerable<CardModel> StartingDeck => [
        ModelDb.Card<Gale>(),
        ModelDb.Card<Gale>(),
        ModelDb.Card<Gale>(),
        ModelDb.Card<Gale>(),
        ModelDb.Card<Siege>(),
        ModelDb.Card<Siege>(),
        ModelDb.Card<Siege>(),
        ModelDb.Card<Siege>(),
        ModelDb.Card<Stabilize>(),
        ModelDb.Card<DreamWand>()
    ];

    public override IReadOnlyList<RelicModel> StartingRelics =>
    [
        ModelDb.Relic<DreamKey>()
    ];

    public override CardPoolModel CardPool => ModelDb.CardPool<SakuraModCardPool>();
    public override RelicPoolModel RelicPool => ModelDb.RelicPool<SakuraModRelicPool>();
    public override PotionPoolModel PotionPool => ModelDb.PotionPool<SakuraModPotionPool>();

    /*  PlaceholderCharacterModel will utilize placeholder basegame assets for most of your character assets until you
        override all the other methods that define those assets. 
        These are just some of the simplest assets, given some placeholders to differentiate your character with. 
        You don't have to, but you're suggested to rename these images. */
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
    public override string CustomMerchantAnimPath =>
        Path.Join(MainFile.ResPath, "scenes", "merchant", "sakura_merchant_character.tscn");

    public override NCreatureVisuals CreateCustomVisuals() =>
        SakuraStandeeVisuals.Create(CustomVisualPath, "Sakura");

    public override string CustomCharacterSelectBg =>
        Path.Join(MainFile.ResPath, "scenes", "screens", "char_select", "char_select_bg_sakura_mod.tscn");

    public override CustomEnergyCounter? CustomEnergyCounter => new(
        layer => layer == 1
            ? "charui/combat_energy_counter_badge.png".ImagePath()
            : "charui/empty_energy_counter_layer.png".ImagePath(),
        new Color("24343d"),
        new Color("d9edf2"));
}
