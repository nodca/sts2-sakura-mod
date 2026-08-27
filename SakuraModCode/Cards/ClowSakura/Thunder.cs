using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Character;

namespace SakuraMod.SakuraModCode.Cards;

file static class ThunderHitFx
{
    // Vanilla Defect Lightning Orb strike (same path as LightningOrb.ApplyLightningDamage).
    public const string Vfx = VfxCmd.lightningPath;
    public const string Sfx = "event:/sfx/characters/defect/defect_lightning_evoke";
}

public class ClowThunder() : ClowExtraEffectCard(3, CardType.Attack, CardRarity.Rare, TargetType.None)
{
    public override SakuraElementSet Elements => SakuraElementSet.Earth;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain];
    protected override bool IsPlayable => SakuraMagicCharge.CanSpendMagic(Owner);
    protected override IEnumerable<DynamicVar> CanonicalVars => [new SakuraSourceDamageVar(15, ValueProp.Move), new DynamicVar("Magic", 4)];

    protected override Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        Task.CompletedTask;

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await DealDamageToRandomEnemies(
            choiceContext,
            ReleasedDamage(),
            ReleasedMagic(),
            hitVfx: ThunderHitFx.Vfx,
            hitSfx: ThunderHitFx.Sfx,
            spawnHitVfxAtBase: true);
        await SakuraMagicCharge.AddVoidToDiscardPile(choiceContext, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3);
        DynamicVars["Magic"].UpgradeValueBy(1);
    }
}

public class SakuraThunder() : SakuraFormCard(0, CardType.Attack, TargetType.None)
{
    private const int ResourceDivisor = 2;

    public override SakuraElementSet Elements => SakuraElementSet.Earth;
    protected override bool HasEnergyCostX => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new SakuraSourceDamageVar(15, ValueProp.Move), new DynamicVar("Magic", ResourceDivisor)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var charge = await SakuraMagicCharge.SpendAllMagic(choiceContext, Owner);
        var count = ((int)play.Resources.EnergySpent * ResourceDivisor + charge) / ResourceDivisor;
        await DealDamageToRandomEnemies(
            choiceContext,
            ReleasedDamage(),
            count,
            hitVfx: ThunderHitFx.Vfx,
            hitSfx: ThunderHitFx.Sfx,
            spawnHitVfxAtBase: true);
    }
}
