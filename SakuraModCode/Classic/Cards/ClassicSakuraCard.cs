using BaseLib.Abstracts;
using BaseLib.Extensions;
using BaseLib.Patches.Features;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Classic.Character;
using SakuraMod.SakuraModCode.Classic.Powers;
using SakuraMod.SakuraModCode.Classic.Relics;
using SakuraMod.SakuraModCode.Extensions;
using System.Runtime.CompilerServices;

namespace SakuraMod.SakuraModCode.Classic.Cards;

public enum ClassicSakuraCardFamily
{
    Clow,
    Sakura,
    Spell
}

public enum ClassicCardIdentity
{
    Arrow,
    Big,
    Bubbles,
    Change,
    Cloud,
    Create,
    Dark,
    Dash,
    Dream,
    Earthy,
    Erase,
    Fight,
    Firey,
    Float,
    Flower,
    Fly,
    Freeze,
    Glow,
    Illusion,
    Sword,
    Shield,
    Jump,
    Libra,
    Light,
    Little,
    Lock,
    Loop,
    Maze,
    Mirror,
    Mist,
    Move,
    Nothing,
    Power,
    Rain,
    Return,
    Sand,
    Shadow,
    Shot,
    Silent,
    Sleep,
    Snow,
    Song,
    Storm,
    Sweet,
    Through,
    Thunder,
    Time,
    Twin,
    Voice,
    Watery,
    Wave,
    Windy,
    Wood
}

[Pool(typeof(ClassicSakuraCardPool))]
public abstract class ClassicSakuraCard(
    int cost,
    CardType type,
    CardRarity rarity,
    TargetType target,
    ClassicSakuraCardFamily family,
    ClassicCardIdentity? identity = null) :
    CustomCardModel(cost, type, rarity, target)
{
    protected static readonly LocString HandPrompt = new("cards", "SAKURAMOD-GENERIC.handPrompt");

    public ClassicSakuraCardFamily Family => family;
    public ClassicCardIdentity? Identity => identity;

    public virtual ClassicElement Element => ClassicElement.None;
    protected virtual bool HasMagicChargeExtraEffect => false;
    protected virtual bool GrantsMagicCharge => Family is ClassicSakuraCardFamily.Clow or ClassicSakuraCardFamily.Sakura;
    protected virtual bool AddsVoidOnNormalSakuraPlay => Family == ClassicSakuraCardFamily.Sakura;

    public override string CustomPortraitPath => "card.png".BigCardImagePath();
    public override string PortraitPath => "card.png".CardImagePath();
    public override string BetaPortraitPath => "card.png".CardImagePath();
    protected override IEnumerable<string> ExtraRunAssetPaths => ClassicSakuraVisualAssets.RunAssetPaths(this);

    protected virtual string BigPortraitPath =>
        Family switch
        {
            ClassicSakuraCardFamily.Clow => ClassicSakuraCardCatalog.ArtStem(GetType()).BigClassicClowArtPath(),
            ClassicSakuraCardFamily.Sakura => ClassicSakuraCardCatalog.ArtStem(GetType()).BigClassicSakuraArtPath(),
            ClassicSakuraCardFamily.Spell => ClassicSakuraCardCatalog.ArtStem(GetType()).BigClassicSpellArtPath(),
            _ => "card.png".BigCardImagePath()
        };

    protected sealed override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var usedExtra = HasMagicChargeExtraEffect && ClassicSakuraMagic.CanUseExtraEffect(Owner);
        if (usedExtra)
        {
            if (ClassicSakuraMagic.ShouldSpendMagicForExtraEffect(Owner))
                await ClassicSakuraMagic.SpendMagic(choiceContext, Owner, ClassicSakuraMagic.ExtraEffectCost);
            await PlayExtra(choiceContext, play);
        }
        else
        {
            await PlayNormal(choiceContext, play);
            if (AddsVoidOnNormalSakuraPlay)
                await ClassicSakuraMagic.AddVoidToDrawPile(choiceContext, Owner);
        }
    }

    protected abstract Task PlayNormal(PlayerChoiceContext choiceContext, CardPlay play);

    protected virtual Task PlayExtra(PlayerChoiceContext choiceContext, CardPlay play) =>
        PlayNormal(choiceContext, play);

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (play.Card == this && GrantsMagicCharge && Owner.GetRelic<ClassicSealedBookRelic>() is not null)
            await ClassicSakuraMagic.GainMagic(choiceContext, this);
    }

    public override Task AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)
    {
        if (card == this)
            ClassicReleaseState.Reset(this);

        return Task.CompletedTask;
    }

    protected int ReleasedValue(string varName) =>
        ClassicReleaseState.ReleasedValue(this, varName, DynamicVars[varName].IntValue);

    protected int ReleasedDamage() => ReleasedValue("Damage");
    protected int ReleasedBlock() => ReleasedValue("Block");
    protected int ReleasedMagic() => ReleasedValue("Magic");

    protected static Creature RequiredTarget(CardPlay play) =>
        play.Target ?? throw new InvalidOperationException("Card target is required by this card's TargetType.");

    protected async Task DealDamage(PlayerChoiceContext choiceContext, Creature target, int amount, ValueProp props = ValueProp.Move) =>
        await DamageCmd.Attack(amount)
            .FromCard(this)
            .WithValueProp(props)
            .WithNoAttackerAnim()
            .Targeting(target)
            .Execute(choiceContext);

    protected async Task DealDamageToEnemies(PlayerChoiceContext choiceContext, IEnumerable<Creature> targets, int amount, ValueProp props = ValueProp.Move) =>
        await DamageCmd.Attack(amount)
            .FromCard(this)
            .WithValueProp(props)
            .WithNoAttackerAnim()
            .TargetingFiltered(targets.Where(static target => target.IsAlive).ToList())
            .Execute(choiceContext);

    protected async Task GainBlock(CardPlay play, int amount) =>
        await CreatureCmd.GainBlock(Owner.Creature, amount, ValueProp.Move, play, false);

    protected async Task ApplyPower<T>(PlayerChoiceContext choiceContext, Creature target, int amount) where T : PowerModel =>
        await PowerCmd.Apply<T>(choiceContext, target, amount, Owner.Creature, this, false);

    protected async Task ApplyPowerToEnemies<T>(PlayerChoiceContext choiceContext, int amount) where T : PowerModel =>
        await PowerCmd.Apply<T>(choiceContext, CombatState!.HittableEnemies.ToList(), amount, Owner.Creature, this, false);

    protected async Task AddGeneratedSpells<T>(PlayerChoiceContext choiceContext, int amount) where T : CardModel
    {
        var combatState = Owner.Creature.CombatState
            ?? throw new InvalidOperationException($"Generated {typeof(T).Name} requires an active combat.");
        for (var i = 0; i < amount; i++)
        {
            var card = combatState.CreateCard<T>(Owner);
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner, CardPilePosition.Random);
        }
    }

    protected void AddKeywordIfMissing(CardKeyword keyword)
    {
        if (!Keywords.Contains(keyword))
            AddKeyword(keyword);
    }
}

public abstract class ClassicClowCard(
    int cost,
    CardType type,
    CardRarity rarity,
    TargetType target,
    ClassicCardIdentity identity) :
    ClassicSakuraCard(cost, type, rarity, target, ClassicSakuraCardFamily.Clow, identity)
{
    protected override bool HasMagicChargeExtraEffect => true;

    protected override string BigPortraitPath =>
        ClassicSakuraCardCatalog.ArtStem(GetType()).BigClassicClowArtPath();
}

public abstract class ClassicSakuraConversionCard(
    int cost,
    CardType type,
    TargetType target,
    ClassicCardIdentity identity) :
    ClassicSakuraCard(cost, type, CardRarity.Token, target, ClassicSakuraCardFamily.Sakura, identity)
{
    public override int MaxUpgradeLevel => 0;

    protected override string BigPortraitPath =>
        ClassicSakuraCardCatalog.ArtStem(GetType()).BigClassicSakuraArtPath();
}

public abstract class ClassicSpecialSakuraCard(int cost, CardType type, TargetType target) :
    ClassicSakuraCard(cost, type, CardRarity.Event, target, ClassicSakuraCardFamily.Sakura)
{
    public override int MaxUpgradeLevel => 0;

    protected override string BigPortraitPath =>
        ClassicSakuraCardCatalog.ArtStem(GetType()).BigClassicSakuraArtPath();
}

public abstract class ClassicSpellCard(int cost, CardType type, CardRarity rarity, TargetType target) :
    ClassicSakuraCard(cost, type, rarity, target, ClassicSakuraCardFamily.Spell)
{
    protected override bool GrantsMagicCharge => false;
}

[Flags]
public enum ClassicElement
{
    None = 0,
    Firey = 1 << 0,
    Watery = 1 << 1,
    Windy = 1 << 2,
    Earthy = 1 << 3
}

internal static class ClassicElementExtensions
{
    public static bool HasElement(this ClassicElement elements, ClassicElement element) =>
        element != ClassicElement.None && (elements & element) == element;
}

internal static class ClassicSakuraCardCatalog
{
    private static readonly Type[] RewardableClowCardTypes =
    [
        typeof(ClowArrow),
        typeof(ClowBig),
        typeof(ClowBubbles),
        typeof(ClowChange),
        typeof(ClowCreate),
        typeof(ClowDark),
        typeof(ClowDash),
        typeof(ClowDream),
        typeof(ClowEarthy),
        typeof(ClowErase),
        typeof(ClowFight),
        typeof(ClowFirey),
        typeof(ClowFloat),
        typeof(ClowFly),
        typeof(ClowFreeze),
        typeof(ClowGlow),
        typeof(ClowJump),
        typeof(ClowIllusion),
        typeof(ClowLibra),
        typeof(ClowLight),
        typeof(ClowLittle),
        typeof(ClowLock),
        typeof(ClowLoop),
        typeof(ClowMaze),
        typeof(ClowMirror),
        typeof(ClowMist),
        typeof(ClowMove),
        typeof(ClowPower),
        typeof(ClowRain),
        typeof(ClowReturn),
        typeof(ClowSand),
        typeof(ClowShadow),
        typeof(ClowShot),
        typeof(ClowSilent),
        typeof(ClowSleep),
        typeof(ClowSnow),
        typeof(ClowSong),
        typeof(ClowStorm),
        typeof(ClowSweet),
        typeof(ClowThrough),
        typeof(ClowThunder),
        typeof(ClowTime),
        typeof(ClowTwin),
        typeof(ClowVoice),
        typeof(ClowCloud),
        typeof(ClowFlower),
        typeof(ClowWatery),
        typeof(ClowWave),
        typeof(ClowWindy),
        typeof(ClowWood)
    ];

    private static readonly Type[] StarterClowCardTypes =
    [
        typeof(ClowSword),
        typeof(ClowShield)
    ];

    private static readonly Type[] SakuraCardTypes =
    [
        typeof(SakuraArrow),
        typeof(SakuraBig),
        typeof(SakuraBubbles),
        typeof(SakuraChange),
        typeof(SakuraCreate),
        typeof(SakuraDark),
        typeof(SakuraDash),
        typeof(SakuraDream),
        typeof(SakuraEarthy),
        typeof(SakuraErase),
        typeof(SakuraFight),
        typeof(SakuraFirey),
        typeof(SakuraFloat),
        typeof(SakuraFly),
        typeof(SakuraFreeze),
        typeof(SakuraGlow),
        typeof(SakuraSword),
        typeof(SakuraShield),
        typeof(SakuraJump),
        typeof(SakuraIllusion),
        typeof(SakuraLibra),
        typeof(SakuraLight),
        typeof(SakuraLittle),
        typeof(SakuraLock),
        typeof(SakuraLoop),
        typeof(SakuraMaze),
        typeof(SakuraMirror),
        typeof(SakuraMist),
        typeof(SakuraMove),
        typeof(SakuraPower),
        typeof(SakuraRain),
        typeof(SakuraReturn),
        typeof(SakuraSand),
        typeof(SakuraShadow),
        typeof(SakuraShot),
        typeof(SakuraSilent),
        typeof(SakuraSleep),
        typeof(SakuraSnow),
        typeof(SakuraSong),
        typeof(SakuraStorm),
        typeof(SakuraSweet),
        typeof(SakuraThrough),
        typeof(SakuraThunder),
        typeof(SakuraTime),
        typeof(SakuraTwin),
        typeof(SakuraVoice),
        typeof(SakuraCloud),
        typeof(SakuraFlower),
        typeof(SakuraWatery),
        typeof(SakuraWave),
        typeof(SakuraWindy),
        typeof(SakuraWood)
    ];

    private static readonly Type[] SpecialCardTypes =
    [
        typeof(ClowNothing),
        typeof(SakuraLove),
        typeof(SakuraHope)
    ];

    private static readonly Type[] AncientCardTypes =
    [
        typeof(SakuraLegacy)
    ];

    private static readonly Type[] SpellCardTypes =
    [
        typeof(SpellSeal),
        typeof(SpellRelease),
        typeof(SpellTurn),
        typeof(SpellEmptySpell),
        typeof(SpellHuoShen),
        typeof(SpellLeiDi),
        typeof(SpellFengHua),
        typeof(SpellShuiLong)
    ];

    private static readonly IReadOnlyDictionary<ClassicCardIdentity, Type> SakuraByIdentity =
        new Dictionary<ClassicCardIdentity, Type>
        {
            [ClassicCardIdentity.Sword] = typeof(SakuraSword),
            [ClassicCardIdentity.Shield] = typeof(SakuraShield),
            [ClassicCardIdentity.Arrow] = typeof(SakuraArrow),
            [ClassicCardIdentity.Big] = typeof(SakuraBig),
            [ClassicCardIdentity.Bubbles] = typeof(SakuraBubbles),
            [ClassicCardIdentity.Change] = typeof(SakuraChange),
            [ClassicCardIdentity.Create] = typeof(SakuraCreate),
            [ClassicCardIdentity.Dark] = typeof(SakuraDark),
            [ClassicCardIdentity.Dash] = typeof(SakuraDash),
            [ClassicCardIdentity.Dream] = typeof(SakuraDream),
            [ClassicCardIdentity.Earthy] = typeof(SakuraEarthy),
            [ClassicCardIdentity.Erase] = typeof(SakuraErase),
            [ClassicCardIdentity.Fight] = typeof(SakuraFight),
            [ClassicCardIdentity.Firey] = typeof(SakuraFirey),
            [ClassicCardIdentity.Float] = typeof(SakuraFloat),
            [ClassicCardIdentity.Fly] = typeof(SakuraFly),
            [ClassicCardIdentity.Freeze] = typeof(SakuraFreeze),
            [ClassicCardIdentity.Glow] = typeof(SakuraGlow),
            [ClassicCardIdentity.Jump] = typeof(SakuraJump),
            [ClassicCardIdentity.Illusion] = typeof(SakuraIllusion),
            [ClassicCardIdentity.Libra] = typeof(SakuraLibra),
            [ClassicCardIdentity.Light] = typeof(SakuraLight),
            [ClassicCardIdentity.Little] = typeof(SakuraLittle),
            [ClassicCardIdentity.Lock] = typeof(SakuraLock),
            [ClassicCardIdentity.Loop] = typeof(SakuraLoop),
            [ClassicCardIdentity.Maze] = typeof(SakuraMaze),
            [ClassicCardIdentity.Mirror] = typeof(SakuraMirror),
            [ClassicCardIdentity.Mist] = typeof(SakuraMist),
            [ClassicCardIdentity.Move] = typeof(SakuraMove),
            [ClassicCardIdentity.Power] = typeof(SakuraPower),
            [ClassicCardIdentity.Rain] = typeof(SakuraRain),
            [ClassicCardIdentity.Return] = typeof(SakuraReturn),
            [ClassicCardIdentity.Sand] = typeof(SakuraSand),
            [ClassicCardIdentity.Shadow] = typeof(SakuraShadow),
            [ClassicCardIdentity.Shot] = typeof(SakuraShot),
            [ClassicCardIdentity.Silent] = typeof(SakuraSilent),
            [ClassicCardIdentity.Sleep] = typeof(SakuraSleep),
            [ClassicCardIdentity.Snow] = typeof(SakuraSnow),
            [ClassicCardIdentity.Song] = typeof(SakuraSong),
            [ClassicCardIdentity.Storm] = typeof(SakuraStorm),
            [ClassicCardIdentity.Sweet] = typeof(SakuraSweet),
            [ClassicCardIdentity.Through] = typeof(SakuraThrough),
            [ClassicCardIdentity.Thunder] = typeof(SakuraThunder),
            [ClassicCardIdentity.Time] = typeof(SakuraTime),
            [ClassicCardIdentity.Twin] = typeof(SakuraTwin),
            [ClassicCardIdentity.Voice] = typeof(SakuraVoice),
            [ClassicCardIdentity.Cloud] = typeof(SakuraCloud),
            [ClassicCardIdentity.Flower] = typeof(SakuraFlower),
            [ClassicCardIdentity.Watery] = typeof(SakuraWatery),
            [ClassicCardIdentity.Wave] = typeof(SakuraWave),
            [ClassicCardIdentity.Windy] = typeof(SakuraWindy),
            [ClassicCardIdentity.Wood] = typeof(SakuraWood)
        };

    private static readonly IReadOnlyDictionary<ClassicCardIdentity, Type> ClowByIdentity =
        new Dictionary<ClassicCardIdentity, Type>
        {
            [ClassicCardIdentity.Sword] = typeof(ClowSword),
            [ClassicCardIdentity.Shield] = typeof(ClowShield),
            [ClassicCardIdentity.Arrow] = typeof(ClowArrow),
            [ClassicCardIdentity.Big] = typeof(ClowBig),
            [ClassicCardIdentity.Bubbles] = typeof(ClowBubbles),
            [ClassicCardIdentity.Change] = typeof(ClowChange),
            [ClassicCardIdentity.Create] = typeof(ClowCreate),
            [ClassicCardIdentity.Dark] = typeof(ClowDark),
            [ClassicCardIdentity.Dash] = typeof(ClowDash),
            [ClassicCardIdentity.Dream] = typeof(ClowDream),
            [ClassicCardIdentity.Earthy] = typeof(ClowEarthy),
            [ClassicCardIdentity.Erase] = typeof(ClowErase),
            [ClassicCardIdentity.Fight] = typeof(ClowFight),
            [ClassicCardIdentity.Firey] = typeof(ClowFirey),
            [ClassicCardIdentity.Float] = typeof(ClowFloat),
            [ClassicCardIdentity.Fly] = typeof(ClowFly),
            [ClassicCardIdentity.Freeze] = typeof(ClowFreeze),
            [ClassicCardIdentity.Glow] = typeof(ClowGlow),
            [ClassicCardIdentity.Jump] = typeof(ClowJump),
            [ClassicCardIdentity.Illusion] = typeof(ClowIllusion),
            [ClassicCardIdentity.Libra] = typeof(ClowLibra),
            [ClassicCardIdentity.Light] = typeof(ClowLight),
            [ClassicCardIdentity.Little] = typeof(ClowLittle),
            [ClassicCardIdentity.Lock] = typeof(ClowLock),
            [ClassicCardIdentity.Loop] = typeof(ClowLoop),
            [ClassicCardIdentity.Maze] = typeof(ClowMaze),
            [ClassicCardIdentity.Mirror] = typeof(ClowMirror),
            [ClassicCardIdentity.Mist] = typeof(ClowMist),
            [ClassicCardIdentity.Move] = typeof(ClowMove),
            [ClassicCardIdentity.Power] = typeof(ClowPower),
            [ClassicCardIdentity.Rain] = typeof(ClowRain),
            [ClassicCardIdentity.Return] = typeof(ClowReturn),
            [ClassicCardIdentity.Sand] = typeof(ClowSand),
            [ClassicCardIdentity.Shadow] = typeof(ClowShadow),
            [ClassicCardIdentity.Shot] = typeof(ClowShot),
            [ClassicCardIdentity.Silent] = typeof(ClowSilent),
            [ClassicCardIdentity.Sleep] = typeof(ClowSleep),
            [ClassicCardIdentity.Snow] = typeof(ClowSnow),
            [ClassicCardIdentity.Song] = typeof(ClowSong),
            [ClassicCardIdentity.Storm] = typeof(ClowStorm),
            [ClassicCardIdentity.Sweet] = typeof(ClowSweet),
            [ClassicCardIdentity.Through] = typeof(ClowThrough),
            [ClassicCardIdentity.Thunder] = typeof(ClowThunder),
            [ClassicCardIdentity.Time] = typeof(ClowTime),
            [ClassicCardIdentity.Twin] = typeof(ClowTwin),
            [ClassicCardIdentity.Voice] = typeof(ClowVoice),
            [ClassicCardIdentity.Cloud] = typeof(ClowCloud),
            [ClassicCardIdentity.Flower] = typeof(ClowFlower),
            [ClassicCardIdentity.Watery] = typeof(ClowWatery),
            [ClassicCardIdentity.Wave] = typeof(ClowWave),
            [ClassicCardIdentity.Windy] = typeof(ClowWindy),
            [ClassicCardIdentity.Wood] = typeof(ClowWood),
            [ClassicCardIdentity.Nothing] = typeof(ClowNothing)
        };

    private static readonly IReadOnlyDictionary<Type, string> ArtStems = new Dictionary<Type, string>
    {
        [typeof(ClowSword)] = "the_sword_p.png",
        [typeof(ClowShield)] = "the_shield_p.png",
        [typeof(ClowArrow)] = "the_arrow_p.png",
        [typeof(ClowBig)] = "the_big_p.png",
        [typeof(ClowBubbles)] = "the_bubbles_p.png",
        [typeof(ClowChange)] = "the_change_p.png",
        [typeof(ClowCreate)] = "the_create_p.png",
        [typeof(ClowDark)] = "the_dark_p.png",
        [typeof(ClowDash)] = "the_dash_p.png",
        [typeof(ClowDream)] = "the_dream_p.png",
        [typeof(ClowJump)] = "the_jump_p.png",
        [typeof(ClowEarthy)] = "the_earthy_p.png",
        [typeof(ClowErase)] = "the_erase_p.png",
        [typeof(ClowFight)] = "the_fight_p.png",
        [typeof(ClowFirey)] = "the_firey_p.png",
        [typeof(ClowFloat)] = "the_float_p.png",
        [typeof(ClowFly)] = "the_fly_p.png",
        [typeof(ClowFreeze)] = "the_freeze_p.png",
        [typeof(ClowGlow)] = "the_glow_p.png",
        [typeof(ClowIllusion)] = "the_illusion_p.png",
        [typeof(ClowLibra)] = "the_libra_p.png",
        [typeof(ClowLight)] = "the_light_p.png",
        [typeof(ClowLittle)] = "the_little_p.png",
        [typeof(ClowLock)] = "the_lock_p.png",
        [typeof(ClowLoop)] = "the_loop_p.png",
        [typeof(ClowMaze)] = "the_maze_p.png",
        [typeof(ClowMirror)] = "the_mirror_p.png",
        [typeof(ClowMist)] = "the_mist_p.png",
        [typeof(ClowMove)] = "the_move_p.png",
        [typeof(ClowPower)] = "the_power_p.png",
        [typeof(ClowRain)] = "the_rain_p.png",
        [typeof(ClowReturn)] = "the_return_p.png",
        [typeof(ClowSand)] = "the_sand_p.png",
        [typeof(ClowShadow)] = "the_shadow_p.png",
        [typeof(ClowShot)] = "the_shot_p.png",
        [typeof(ClowSilent)] = "the_silent_p.png",
        [typeof(ClowSleep)] = "the_sleep_p.png",
        [typeof(ClowSnow)] = "the_snow_p.png",
        [typeof(ClowSong)] = "the_song_p.png",
        [typeof(ClowStorm)] = "the_storm_p.png",
        [typeof(ClowSweet)] = "the_sweet_p.png",
        [typeof(ClowThrough)] = "the_through_p.png",
        [typeof(ClowThunder)] = "the_thunder_p.png",
        [typeof(ClowTime)] = "the_time_p.png",
        [typeof(ClowTwin)] = "the_twin_p.png",
        [typeof(ClowVoice)] = "the_voice_p.png",
        [typeof(ClowCloud)] = "the_cloud_p.png",
        [typeof(ClowFlower)] = "the_flower_p.png",
        [typeof(ClowWatery)] = "the_watery_p.png",
        [typeof(ClowWave)] = "the_wave_p.png",
        [typeof(ClowWindy)] = "the_windy_p.png",
        [typeof(ClowWood)] = "the_wood_p.png",
        [typeof(ClowNothing)] = "the_nothing_p.png",
        [typeof(SakuraArrow)] = "the_arrow_p.png",
        [typeof(SakuraEarthy)] = "the_earthy_p.png",
        [typeof(SakuraErase)] = "the_erase_p.png",
        [typeof(SakuraFight)] = "the_fight_p.png",
        [typeof(SakuraFirey)] = "the_firey_p.png",
        [typeof(SakuraFloat)] = "the_float_p.png",
        [typeof(SakuraBig)] = "the_big_p.png",
        [typeof(SakuraBubbles)] = "the_bubbles_p.png",
        [typeof(SakuraChange)] = "the_change_p.png",
        [typeof(SakuraCreate)] = "the_create_p.png",
        [typeof(SakuraDark)] = "the_dark_p.png",
        [typeof(SakuraDash)] = "the_dash_p.png",
        [typeof(SakuraSword)] = "the_sword_p.png",
        [typeof(SakuraShield)] = "the_shield_p.png",
        [typeof(SakuraDream)] = "the_dream_p.png",
        [typeof(SakuraFly)] = "the_fly_p.png",
        [typeof(SakuraFreeze)] = "the_freeze_p.png",
        [typeof(SakuraGlow)] = "the_glow_p.png",
        [typeof(SakuraJump)] = "the_jump_p.png",
        [typeof(SakuraIllusion)] = "the_illusion_p.png",
        [typeof(SakuraLibra)] = "the_libra_p.png",
        [typeof(SakuraLight)] = "the_light_p.png",
        [typeof(SakuraLittle)] = "the_little_p.png",
        [typeof(SakuraLock)] = "the_lock_p.png",
        [typeof(SakuraLoop)] = "the_loop_p.png",
        [typeof(SakuraMaze)] = "the_maze_p.png",
        [typeof(SakuraMirror)] = "the_mirror_p.png",
        [typeof(SakuraMist)] = "the_mist_p.png",
        [typeof(SakuraMove)] = "the_move_p.png",
        [typeof(SakuraPower)] = "the_power_p.png",
        [typeof(SakuraRain)] = "the_rain_p.png",
        [typeof(SakuraReturn)] = "the_return_p.png",
        [typeof(SakuraSand)] = "the_sand_p.png",
        [typeof(SakuraShadow)] = "the_shadow_p.png",
        [typeof(SakuraShot)] = "the_shot_p.png",
        [typeof(SakuraSilent)] = "the_silent_p.png",
        [typeof(SakuraSleep)] = "the_sleep_p.png",
        [typeof(SakuraSnow)] = "the_snow_p.png",
        [typeof(SakuraSong)] = "the_song_p.png",
        [typeof(SakuraStorm)] = "the_storm_p.png",
        [typeof(SakuraSweet)] = "the_sweet_p.png",
        [typeof(SakuraThrough)] = "the_through_p.png",
        [typeof(SakuraThunder)] = "the_thunder_p.png",
        [typeof(SakuraTime)] = "the_time_p.png",
        [typeof(SakuraTwin)] = "the_twin_p.png",
        [typeof(SakuraVoice)] = "the_voice_p.png",
        [typeof(SakuraCloud)] = "the_cloud_p.png",
        [typeof(SakuraFlower)] = "the_flower_p.png",
        [typeof(SakuraWatery)] = "the_watery_p.png",
        [typeof(SakuraWave)] = "the_wave_p.png",
        [typeof(SakuraWindy)] = "the_windy_p.png",
        [typeof(SakuraWood)] = "the_wood_p.png",
        [typeof(SakuraLove)] = "the_love_p.png",
        [typeof(SakuraHope)] = "the_hope_p.png",
        [typeof(SpellSeal)] = "default_card_p.png",
        [typeof(SpellRelease)] = "default_card_p.png",
        [typeof(SpellTurn)] = "default_card_p.png",
        [typeof(SpellEmptySpell)] = "empty_spell_p.png",
        [typeof(SpellHuoShen)] = "huoshen_p.png",
        [typeof(SpellLeiDi)] = "leidi_p.png",
        [typeof(SpellFengHua)] = "fenghua_p.png",
        [typeof(SpellShuiLong)] = "shuilong_p.png"
    };

    public static CardModel[] AllCardTemplates() =>
    [
        ..StarterClowCardTypes.Select(TypeToCard),
        ..RewardableClowCardTypes.Select(TypeToCard),
        ..SakuraCardTypes.Select(TypeToCard),
        ..SpecialCardTypes.Select(TypeToCard),
        ..AncientCardTypes.Select(TypeToCard),
        ..SpellCardTypes.Select(TypeToCard)
    ];

    public static IReadOnlyList<CardModel> RewardableClowTemplates() =>
        RewardableClowCardTypes.Select(TypeToCard).ToList();

    public static IReadOnlyList<CardModel> AllClowTemplates() =>
    [
        ..StarterClowCardTypes.Select(TypeToCard),
        ..RewardableClowCardTypes.Select(TypeToCard)
    ];

    public static CardModel CreateRandomDreamClowCard(Player owner)
    {
        var templates = AllClowTemplates()
            .Where(static card => card is not ClowCreate)
            .ToList();
        return CreateRandomClowCard(owner, templates, "Dream Clow pool");
    }

    public static CardModel CreateRandomDarkClowCard(Player owner)
    {
        var templates = AllClowTemplates();
        var rolledRarity = RollDarkClowRarity(owner);
        var options = templates.Where(card => card.Rarity == rolledRarity).ToList();
        if (options.Count == 0)
            throw new InvalidOperationException($"Dark Clow pool has no {rolledRarity} cards.");

        return CreateRandomClowCard(owner, options, "Dark Clow pool");
    }

    public static Type? SakuraTypeFor(ClassicCardIdentity identity) =>
        SakuraByIdentity.TryGetValue(identity, out var type) ? type : null;

    public static Type? ClowTypeFor(ClassicCardIdentity identity) =>
        ClowByIdentity.TryGetValue(identity, out var type) ? type : null;

    public static bool HasSakuraIdentity(Player owner, ClassicCardIdentity identity) =>
        CardsInAllKnownPiles(owner)
            .OfType<ClassicSakuraConversionCard>()
            .Any(card => card.Identity == identity);

    public static CardModel CreateMirrorCopySource(CardModel source)
    {
        if (source is SakuraLove)
            return CreateCombatCardFromType<SpellEmptySpell>(source);

        if (source is SakuraHope)
            return CreateCombatCardFromType<ClowNothing>(source);

        if (source is ClassicSakuraConversionCard { Identity: { } identity } && HasSakuraIdentity(source.Owner, identity))
        {
            var clowType = ClowTypeFor(identity)
                ?? throw new InvalidOperationException($"Missing Clow mirror source for {identity}.");
            var canonicalClow = ModelDb.GetById<CardModel>(ModelDb.GetId(clowType));
            var clowCopy = source.CombatState!.CreateCard(canonicalClow, source.Owner);
            MatchStatEquivalentCopy(clowCopy, source);
            return clowCopy;
        }

        return source.CreateClone();
    }

    public static bool HasSpecialCard<T>(Player owner) where T : CardModel =>
        CardsInAllKnownPiles(owner).OfType<T>().Any();

    public static int ConvertedSakuraCount(Player owner) =>
        owner.Deck.Cards.OfType<ClassicSakuraConversionCard>().Select(card => card.Identity).Distinct().Count()
        + owner.Deck.Cards.Count(static card => card is SpellTurn);

    public static int StarterClowCount(Player owner, ClassicCardIdentity identity) =>
        Math.Clamp(owner.Deck.Cards.OfType<ClassicClowCard>().Count(card => card.Identity == identity), 1, 4);

    public static bool IsEligibleClowForTurn(CardModel card) =>
        card is ClassicClowCard { Identity: { } identity }
        && card.Pile?.Type == PileType.Hand
        && SakuraTypeFor(identity) is not null
        && !HasSakuraIdentity(card.Owner, identity);

    public static string ArtStem(Type type) =>
        ArtStems.TryGetValue(type, out var stem)
            ? stem
            : throw new InvalidOperationException($"Missing Classic Sakura art mapping for {type.Name}.");

    private static CardModel TypeToCard(Type type) =>
        ModelDb.GetById<CardModel>(ModelDb.GetId(type));

    private static CardModel CreateCombatCardFromType<T>(CardModel source) where T : CardModel
    {
        var canonical = ModelDb.GetById<CardModel>(ModelDb.GetId(typeof(T)));
        return source.CombatState!.CreateCard(canonical, source.Owner);
    }

    private static CardRarity RollDarkClowRarity(Player owner)
    {
        var roll = owner.RunState.Rng.CombatCardSelection.NextInt(10);
        if (roll == 0)
            return CardRarity.Rare;
        return roll <= 3 ? CardRarity.Uncommon : CardRarity.Common;
    }

    private static CardModel CreateRandomClowCard(Player owner, IReadOnlyList<CardModel> templates, string poolName)
    {
        if (templates.Count == 0)
            throw new InvalidOperationException($"{poolName} is empty.");

        var template = owner.RunState.Rng.CombatCardSelection.NextItem(templates)
            ?? throw new InvalidOperationException($"{poolName} random selection failed.");
        var combatState = owner.Creature.CombatState
            ?? throw new InvalidOperationException($"{poolName} generated cards require an active combat.");
        return combatState.CreateCard(template, owner);
    }

    private static void MatchUpgradeLevel(CardModel target, CardModel source)
    {
        while (target.CurrentUpgradeLevel < source.CurrentUpgradeLevel && target.IsUpgradable)
            target.UpgradeInternal();
    }

    private static void MatchStatEquivalentCopy(CardModel target, CardModel source)
    {
        MatchUpgradeLevel(target, source);

        if (!source.EnergyCost.CostsX && !target.EnergyCost.CostsX)
            target.EnergyCost.SetCustomBaseCost(Math.Max(0, source.EnergyCost.GetWithModifiers(CostModifiers.Local)));

        foreach (var (name, sourceVar) in source.DynamicVars)
        {
            if (target.DynamicVars.TryGetValue(name, out var targetVar))
                targetVar.BaseValue = sourceVar.BaseValue;
        }
    }

    private static IEnumerable<CardModel> CardsInAllKnownPiles(Player owner) =>
        owner.Deck.Cards.Concat(owner.Piles.SelectMany(static pile => pile.Cards));
}

internal static class ClassicStarterScaling
{
    private const int BaseStarterCount = 4;
    private const decimal LoneRate = 0.2m;
    private const decimal MaxLoneRate = 0.6m;

    public static int ScaledValue(Player owner, ClassicCardIdentity identity, int baseValue)
    {
        var count = ClassicSakuraCardCatalog.StarterClowCount(owner, identity);
        var rate = Math.Min(MaxLoneRate, Math.Max(0m, (BaseStarterCount - count) * LoneRate));
        return (int)Math.Floor(baseValue * (1m + rate));
    }
}

internal sealed class ClassicDamageVar(decimal damage, ValueProp props, ClassicCardIdentity? starterIdentity = null) :
    DamageVar(damage, props)
{
    public override void UpdateCardPreview(CardModel card, CardPreviewMode previewMode, Creature? target, bool runGlobalHooks)
    {
        var baseValue = AdjustedBase(card);
        var preview = baseValue;
        if (card.Enchantment is not null)
        {
            preview += card.Enchantment.EnchantDamageAdditive(preview, Props);
            preview *= card.Enchantment.EnchantDamageMultiplicative(preview, Props);
            if (!card.IsEnchantmentPreview)
                EnchantedValue = preview;
        }

        if (runGlobalHooks)
            preview = Hook.ModifyDamage(card.Owner.RunState, card.CombatState, target, card.Owner.Creature, baseValue, Props, card, ModifyDamageHookType.All, previewMode, out _);

        PreviewValue = preview;
    }

    private decimal AdjustedBase(CardModel card)
    {
        var baseValue = (int)BaseValue;
        if (card.Owner is not null && starterIdentity is { } identity)
            baseValue = ClassicStarterScaling.ScaledValue(card.Owner, identity, baseValue);
        return ClassicReleaseState.ReleasedValue(card, Name, baseValue);
    }
}

internal sealed class ClassicBlockVar(decimal block, ValueProp props, ClassicCardIdentity? starterIdentity = null) :
    BlockVar(block, props)
{
    public override void UpdateCardPreview(CardModel card, CardPreviewMode previewMode, Creature? target, bool runGlobalHooks)
    {
        var baseValue = AdjustedBase(card);
        var preview = baseValue;
        if (card.Enchantment is not null)
        {
            preview += card.Enchantment.EnchantBlockAdditive(preview);
            preview *= card.Enchantment.EnchantBlockMultiplicative(preview);
            if (!card.IsEnchantmentPreview)
                EnchantedValue = preview;
        }

        if (runGlobalHooks && card.CombatState is not null)
            preview = Hook.ModifyBlock(card.CombatState, card.Owner.Creature, baseValue, Props, card, null, out _);

        PreviewValue = preview;
    }

    private decimal AdjustedBase(CardModel card)
    {
        var baseValue = (int)BaseValue;
        if (card.Owner is not null && starterIdentity is { } identity)
            baseValue = ClassicStarterScaling.ScaledValue(card.Owner, identity, baseValue);
        return ClassicReleaseState.ReleasedValue(card, Name, baseValue);
    }
}

internal sealed class ClassicReturnRechargeVar() : DynamicVar("Magic", 15)
{
    public override void UpdateCardPreview(CardModel card, CardPreviewMode previewMode, Creature? target, bool runGlobalHooks) =>
        PreviewValue = card.Owner is { } owner
            ? ClassicSealedWandRelic.ReturnRechargeAmountFor(owner)
            : BaseValue;
}

internal static class ClassicSakuraMagic
{
    public const int ExtraEffectCost = 10;
    public const int SwordExtraHpLoss = 15;
    public const int ShieldMetallicizeBlock = 4;
    public const int CloudExtraBlock = 12;
    public const int FlowerExtraEnergy = 2;

    public static bool CanSpendMagic(Player? owner) =>
        owner?.Creature.GetPower<ClassicMagicChargePower>()?.Amount >= ExtraEffectCost;

    public static bool CanUseExtraEffect(Player? owner) =>
        CanSpendMagic(owner) && owner?.Creature.GetPower<ClassicLockPower>() is null;

    public static bool ShouldSpendMagicForExtraEffect(Player owner) =>
        owner.Creature.GetPower<ClassicLockSakuraPower>() is null;

    public static async Task SpendMagic(PlayerChoiceContext choiceContext, Player owner, int amount)
    {
        var power = owner.Creature.GetPower<ClassicMagicChargePower>();
        if (power is not null)
            await PowerCmd.ModifyAmount(choiceContext, power, -amount, owner.Creature, null, false);
    }

    public static async Task SpendUpToMagic(PlayerChoiceContext choiceContext, Player owner, int amount)
    {
        var power = owner.Creature.GetPower<ClassicMagicChargePower>();
        if (power is null || amount <= 0)
            return;

        var spend = Math.Min(power.Amount, amount);
        await PowerCmd.ModifyAmount(choiceContext, power, -spend, owner.Creature, null, false);
    }

    public static async Task<int> SpendAllMagic(PlayerChoiceContext choiceContext, Player owner)
    {
        var power = owner.Creature.GetPower<ClassicMagicChargePower>();
        if (power is null || power.Amount <= 0)
            return 0;

        var amount = power.Amount;
        await PowerCmd.ModifyAmount(choiceContext, power, -amount, owner.Creature, null, false);
        return amount;
    }

    public static async Task GainMagic(PlayerChoiceContext choiceContext, ClassicSakuraCard card)
    {
        var amount = card.Type == CardType.Power ? 2 : 1;
        await PowerCmd.Apply<ClassicMagicChargePower>(choiceContext, card.Owner.Creature, amount, card.Owner.Creature, card, false);
    }

    public static async Task AddVoidToDrawPile(PlayerChoiceContext choiceContext, Player owner)
    {
        var combatState = owner.Creature.CombatState
            ?? throw new InvalidOperationException("Classic Sakura generated Void requires an active combat.");
        var card = combatState.CreateCard<MegaCrit.Sts2.Core.Models.Cards.Void>(owner);
        await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Draw, owner, CardPilePosition.Random);
    }

    public static async Task AddVoidToDiscardPile(PlayerChoiceContext choiceContext, Player owner)
    {
        var combatState = owner.Creature.CombatState
            ?? throw new InvalidOperationException("Classic Sakura generated Void requires an active combat.");
        var card = combatState.CreateCard<MegaCrit.Sts2.Core.Models.Cards.Void>(owner);
        await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Discard, owner, CardPilePosition.Random);
    }
}

internal static class ClassicCreateRewards
{
    private const int RareRoll = 0;
    private const int UncommonRolls = 3;
    private const int TotalRolls = 8;

    public static void AddNormalRelicReward(Player owner) =>
        CurrentCombatRoom(owner).AddExtraReward(owner, new RelicReward(RollSourceRelicRarity(owner), owner));

    public static void AddExclusiveOrNormalRelicReward(Player owner)
    {
        var relic = ClassicSakuraExclusiveRelics.TryPullRandomAvailableForCreate(owner);
        if (relic is null)
        {
            AddNormalRelicReward(owner);
            return;
        }

        CurrentCombatRoom(owner).AddExtraReward(owner, new RelicReward(relic.ToMutable(), owner));
    }

    private static RelicRarity RollSourceRelicRarity(Player owner)
    {
        var roll = owner.PlayerRng.Rewards.NextInt(TotalRolls);
        if (roll == RareRoll)
            return RelicRarity.Rare;
        return roll <= UncommonRolls ? RelicRarity.Uncommon : RelicRarity.Common;
    }

    private static CombatRoom CurrentCombatRoom(Player owner) =>
        owner.RunState.CurrentRoom as CombatRoom
        ?? throw new InvalidOperationException("Create requires the current room to be a combat room.");
}

internal static class ClassicReleaseState
{
    private static readonly ConditionalWeakTable<CardModel, ReleaseMarker> ReleasedCards = new();
    private static readonly string[] NonMagicVarNames = ["Damage", "Block", "Cards"];

    public static bool IsReleased(CardModel card) =>
        ReleasedCards.TryGetValue(card, out _);

    public static void Apply(CardModel card, float releaseRate)
    {
        if (ReleasedCards.TryGetValue(card, out _))
            return;

        var marker = new ReleaseMarker(releaseRate);

        if (card.EnergyCost.GetWithModifiers(CostModifiers.Local) > 0)
            card.EnergyCost.SetThisTurnOrUntilPlayed(0, true);

        if (card.Type == CardType.Power)
        {
            AddReleaseKeyword(card, CardKeyword.Ethereal, marker);
        }
        else
        {
            AddReleaseKeyword(card, CardKeyword.Exhaust, marker);
            if (!card.Keywords.Contains(CardKeyword.Retain))
                AddReleaseKeyword(card, CardKeyword.Ethereal, marker);
        }

        foreach (var variable in card.DynamicVars.Values.Where(variable => !NonMagicVarNames.Contains(variable.Name)))
        {
            var delta = ReleaseDelta(variable.IntValue, releaseRate);
            if (delta == 0)
                continue;

            variable.BaseValue += delta;
            marker.DynamicVarDeltas[variable.Name] = delta;
        }

        ReleasedCards.Add(card, marker);
    }

    public static int ReleasedValue(CardModel card, string varName, int baseValue)
    {
        if (!ReleasedCards.TryGetValue(card, out var marker))
            return baseValue;

        return marker.DynamicVarDeltas.ContainsKey(varName)
            ? baseValue
            : baseValue + ReleaseDelta(baseValue, marker.Rate);
    }

    public static int ReleasedValue(CardModel card, int baseValue)
    {
        if (!ReleasedCards.TryGetValue(card, out var marker))
            return baseValue;

        return baseValue + ReleaseDelta(baseValue, marker.Rate);
    }

    public static void Reset(CardModel card)
    {
        if (!ReleasedCards.TryGetValue(card, out var marker))
            return;

        foreach (var (name, delta) in marker.DynamicVarDeltas)
        {
            if (card.DynamicVars.TryGetValue(name, out var variable))
                variable.BaseValue -= delta;
        }

        foreach (var keyword in marker.AddedKeywords)
        {
            if (card.Keywords.Contains(keyword))
                card.RemoveKeyword(keyword);
        }

        ReleasedCards.Remove(card);
    }

    private static void AddReleaseKeyword(CardModel card, CardKeyword keyword, ReleaseMarker marker)
    {
        if (card.Keywords.Contains(keyword))
            return;

        card.AddKeyword(keyword);
        marker.AddedKeywords.Add(keyword);
    }

    private static int ReleaseDelta(int baseValue, float rate) =>
        (int)Math.Floor(baseValue * rate);

    private sealed class ReleaseMarker(float rate)
    {
        public float Rate { get; } = rate;
        public Dictionary<string, int> DynamicVarDeltas { get; } = [];
        public List<CardKeyword> AddedKeywords { get; } = [];
    }
}

internal static class ClassicSakuraAssetPaths
{
    public static string BigClassicClowArtPath(this string path) =>
        Path.Join("big", "clow", path).ClassicFullFaceImagePath();

    public static string BigClassicSakuraArtPath(this string path) =>
        Path.Join("big", "sakura", path).ClassicFullFaceImagePath();

    public static string BigClassicSpellArtPath(this string path) =>
        Path.Join("big", "spell", path).ClassicFullFaceImagePath();

    public static string NormalClassicArtStem(this string path) =>
        path.EndsWith("_p.png", StringComparison.Ordinal)
            ? string.Concat(path.AsSpan(0, path.Length - "_p.png".Length), ".png")
            : throw new InvalidOperationException($"Classic art stem must point at a _p.png large art file: {path}");
}

internal static class ClassicCombatHistory
{
    public static int PlayedClassicCardsThisCombat(Player owner, CardModel excludedCard, Func<ClassicSakuraCard, bool> predicate) =>
        CombatManager.Instance.History.CardPlaysFinished
            .Where(entry => entry is CardPlayFinishedEntry { CardPlay.Card.Owner: var cardOwner } && cardOwner == owner)
            .Select(entry => ((CardPlayFinishedEntry)entry).CardPlay.Card)
            .Where(card => card != excludedCard)
            .OfType<ClassicSakuraCard>()
            .Count(predicate);
}

internal static class ClassicPowerRules
{
    public static bool IsBubblesRemovableBuff(PowerModel power) =>
        power is StrengthPower { Amount: > 0 }
        or ArtifactPower { Amount: > 0 }
        or RitualPower { Amount: > 0 }
        or ThornsPower { Amount: > 0 }
        or PlatingPower { Amount: > 0 };

    public static async Task ApplyBypassingArtifact<T>(
        PlayerChoiceContext choiceContext,
        Creature target,
        int amount,
        Creature source,
        CardModel cardSource) where T : PowerModel
    {
        var artifact = target.GetPower<ArtifactPower>();
        if (artifact is null)
        {
            await PowerCmd.Apply<T>(choiceContext, target, amount, source, cardSource, false);
            return;
        }

        var artifactAmount = artifact.Amount;
        await PowerCmd.Remove(artifact);
        await PowerCmd.Apply<T>(choiceContext, target, amount, source, cardSource, false);
        if (target.IsAlive)
            await PowerCmd.Apply<ArtifactPower>(choiceContext, target, artifactAmount, source, cardSource, false);
    }
}
