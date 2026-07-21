using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.Combat;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Relics;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Extensions;
using STS2RitsuLib.Scaffolding.Characters;
using STS2RitsuLib.Scaffolding.Visuals.StateMachine;

namespace SakuraMod.SakuraModCode.Character;

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
    public override string CustomMerchantAnimPath =>
        Path.Join(MainFile.ResPath, "scenes", "merchant", "sakura_merchant_character.tscn");
    public override string CustomEnergyCounterPath =>
        Path.Join(MainFile.ResPath, "scenes", "combat", "energy_counters", "sakura_energy_counter.tscn");
    public override string CustomRestSiteAnimPath =>
        Path.Join(MainFile.ResPath, "scenes", "rest_site", "sakura_rest_site_character.tscn");

    protected override NCreatureVisuals? TryCreateCreatureVisuals() =>
        SakuraCombatVisuals.CreateSelected(CustomVisualsPath);

    protected override ModAnimStateMachine SetupCustomRestSiteAnimationStateMachine(
        Node restSiteRoot,
        CharacterModel character) =>
        ModAnimStateMachines.StandardRestSiteCue(restSiteRoot, character, "rest_idle");

    public override string CustomCharacterSelectBgPath =>
        Path.Join(MainFile.ResPath, "scenes", "screens", "char_select", "sakura_character_select_background.tscn");

    protected override IEnumerable<string> ExtraAssetPaths =>
    [
        ClassicCardEnergyIcon.ClowTextPath,
        ClassicCardEnergyIcon.SakuraTextPath
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
