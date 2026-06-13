using BaseLib.Abstracts;
using SakuraMod.SakuraModCode.Extensions;
using Godot;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Cards;

namespace SakuraMod.SakuraModCode.Character;

public class SakuraModCardPool : CustomCardPoolModel
{
    public override string Title => SakuraMod.CharacterId; //This is not a display name.

    public override string BigEnergyIconPath => "charui/big_energy.png".ImagePath();
    public override string TextEnergyIconPath => "charui/text_energy.png".ImagePath();


    // Fallback frame tint for Sakura cards that do not provide a category-specific material.
    public override Color ShaderColor => new("72D8EF");

    //Color of small card icons
    public override Color DeckEntryCardColor => new("ffffff");

    public override bool IsColorless => false;

    protected override CardModel[] GenerateAllCards() =>
    [
        ModelDb.Card<Gale>(),
        ModelDb.Card<Reflect>(),
        ModelDb.Card<Flight>(),
        ModelDb.Card<global::SakuraMod.SakuraModCode.Cards.Action>(),
        ModelDb.Card<Appear>(),
        ModelDb.Card<Aqua>(),
        ModelDb.Card<Blade>(),
        ModelDb.Card<Hail>(),
        ModelDb.Card<Lucid>(),
        ModelDb.Card<Shade>(),
        ModelDb.Card<Siege>(),
        ModelDb.Card<Swing>(),
        ModelDb.Card<Break>(),
        ModelDb.Card<Choice>(),
        ModelDb.Card<Promise>(),
        ModelDb.Card<Struggle>(),
        ModelDb.Card<Blaze>(),
        ModelDb.Card<Dreaming>(),
        ModelDb.Card<Gravitation>(),
        ModelDb.Card<Mirage>(),
        ModelDb.Card<Record>(),
        ModelDb.Card<Exchange>(),
        ModelDb.Card<Kindness>(),
        ModelDb.Card<Labyrinth>(),
        ModelDb.Card<Repair>(),
        ModelDb.Card<Reversal>(),
        ModelDb.Card<Rewind>(),
        ModelDb.Card<Snooze>(),
        ModelDb.Card<Spiral>(),
        ModelDb.Card<Transfer>(),
        ModelDb.Card<Blank>(),
        ModelDb.Card<Mirror>(),
        ModelDb.Card<Remind>(),
        ModelDb.Card<Synchronize>(),
        ModelDb.Card<global::SakuraMod.SakuraModCode.Cards.Time>(),
        ModelDb.Card<TrueOrFalse>(),

        ModelDb.Card<KeroBond>(),
        ModelDb.Card<KeroRecon>(),
        ModelDb.Card<KeroSnackBreak>(),
        ModelDb.Card<TomoyoCostume>(),
        ModelDb.Card<TomoyoDesign>(),
        ModelDb.Card<AkihoTea>(),
        ModelDb.Card<AkihoDream>(),
        ModelDb.Card<ClockCountryAlice>(),
        ModelDb.Card<DWatch>(),
        ModelDb.Card<FalseDailyLife>(),
        ModelDb.Card<StarlightChant>(),
        ModelDb.Card<Archive>(),
        ModelDb.Card<GrowingMagic>(),
        ModelDb.Card<ReleaseChant>(),
        ModelDb.Card<CardBookSorting>(),
        ModelDb.Card<NewPage>(),
        ModelDb.Card<NamelessMagic>(),
        ModelDb.Card<ChainPhenomenon>(),
        ModelDb.Card<MagicSurge>(),
        ModelDb.Card<MagicTuning>(),
        ModelDb.Card<DreamWandCombo>(),
        ModelDb.Card<CompassTracking>(),
        ModelDb.Card<ThunderEmperorSummon>(),
        ModelDb.Card<MeilingComboKick>(),
        ModelDb.Card<TalismanCombo>(),
        ModelDb.Card<SilverMoonWing>(),
        ModelDb.Card<BigBrotherSense>(),
        ModelDb.Card<YukitoLunchBox>(),
        ModelDb.Card<MomoContract>(),
        ModelDb.Card<YamazakiTallTale>(),
        ModelDb.Card<EriolPhoneCall>(),
        ModelDb.Card<FujitakaNote>(),
        ModelDb.Card<NaokoGhostStory>(),
        ModelDb.Card<SyaoranTalisman>(),
        ModelDb.Card<DaoistSupport>(),
        ModelDb.Card<TomoyoCamera>(),
        ModelDb.Card<TomoyoBond>(),
        ModelDb.Card<SyaoranBond>(),
        ModelDb.Card<CostumePocket>(),
        ModelDb.Card<DreamCostume>(),
        ModelDb.Card<BlessingOfTheNamelessBook>(),

        ModelDb.Card<DreamWand>(),
        ModelDb.Card<DreamCompass>(),
        ModelDb.Card<Stabilize>(),
        ModelDb.Card<MagicAwakening>(),
        ModelDb.Card<ForbiddenMagic>(),
        ModelDb.Card<DreamKeyGlow>(),
        ModelDb.Card<StarWand>(),
        ModelDb.Card<SealedBook>(),
        ModelDb.Card<DreamKeyResonance>(),
        ModelDb.Card<RollerbladeDash>(),
        ModelDb.Card<MagicBarrier>()
    ];
}
