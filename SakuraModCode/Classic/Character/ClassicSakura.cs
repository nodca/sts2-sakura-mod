using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.Combat;
using SakuraMod.SakuraModCode.Classic.Cards;
using SakuraMod.SakuraModCode.Classic.Relics;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Extensions;
using STS2RitsuLib.Scaffolding.Characters;

namespace SakuraMod.SakuraModCode.Classic.Character;

public class ClassicSakura : ModCharacterTemplate<ClassicSakuraCardPool, ClassicSakuraRelicPool, ClassicSakuraPotionPool>
{
    public const string CharacterId = "ClassicSakura";

    public static readonly Color Color = new("f4a7c4");
    private static readonly Color ClearCardSakuraMapDrawingColor = new("F4A7C4");

    public override Color NameColor => Color;
    public override Color MapDrawingColor => ClearCardSakuraMapDrawingColor;
    public override CharacterGender Gender => CharacterGender.Neutral;
    public override int StartingHp => 70;
    public override int StartingGold => 99;
    public override int MaxEnergy => 3;
    public override Color EnergyLabelOutlineColor => new("322a22");
    public override int BaseOrbSlotCount => 0;
    public override float AttackAnimDelay => 0.15f;
    public override float CastAnimDelay => 0.25f;

    protected override IEnumerable<CardModel> LocalStartingDeck =>
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

    protected override IEnumerable<RelicModel> LocalStartingRelics =>
    [
        ModelDb.Relic<ClassicSealedBookRelic>(),
        ModelDb.Relic<ClassicSealedWandRelic>()
    ];

    public override string CustomIconTexturePath => "character_icon_char_name.png".CharacterUiPath();
    public override string CustomIconPath => "character_icon_char_name.png".CharacterUiPath();
    public override string CustomCharacterSelectIconPath => "char_select_char_name.png".CharacterUiPath();
    public override string CustomCharacterSelectLockedIconPath => "char_select_char_name_locked.png".CharacterUiPath();
    public override string CustomMapMarkerPath => "map_marker_char_name.png".CharacterUiPath();
    public override string CustomVisualsPath => "charui/sakura_battle_standee.png".ImagePath();
    public override string CustomEnergyCounterPath =>
        Path.Join(MainFile.ResPath, "scenes", "combat", "energy_counters", "classic_sakura_energy_counter.tscn");

    protected override NCreatureVisuals? TryCreateCreatureVisuals() =>
        SakuraStandeeVisuals.Create(CustomVisualsPath, "Sakura Kinomoto");

    public override string CustomCharacterSelectBgPath =>
        Path.Join(MainFile.ResPath, "scenes", "screens", "char_select", "char_select_bg_classic_sakura.tscn");

    protected override IEnumerable<string> ExtraAssetPaths =>
    [
        ClassicSakuraEnergyIcon.ClowTextPath,
        ClassicSakuraEnergyIcon.SakuraTextPath
    ];

    public override List<string> GetArchitectAttackVfx() =>
    [
        "vfx/vfx_attack_blunt",
        "vfx/vfx_heavy_blunt",
        "vfx/vfx_attack_slash",
        "vfx/vfx_bloody_impact",
        "vfx/vfx_rock_shatter"
    ];
}
