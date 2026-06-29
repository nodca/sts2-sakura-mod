using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;

namespace SakuraMod.SakuraModCode.Cards;

public class Gale() : SakuraModCard(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy), IReleaseable
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Wind];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(6, ValueProp.Move),
        new DamageVar("ReleaseDamage", 4, ValueProp.Move),
        new CardsVar("ReleaseDraw", 1)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = RequiredTarget(play);
        await SakuraActions.AttackCommand(this, target, DynamicVars.Damage.IntValue, DynamicVars.Damage.Props)
            .WithHitVfxNode(target => SakuraCardPlayVfx.CreateGaleWindBlade(Owner.Creature, target))
            .Execute(choiceContext);
        await TriggerReleaseEffect(choiceContext, play);
    }

    public async Task OnReleased(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = play.Target ?? CombatState?.HittableEnemies.FirstOrDefault();
        if (target is not null)
        {
            await SakuraActions.AttackCommand(this, target, damage: DynamicVars["ReleaseDamage"].IntValue)
                .WithHitVfxNode(target => SakuraCardPlayVfx.CreateGaleWindBlade(Owner.Creature, target, releaseFollowUp: true))
                .WithNoAttackerAnim()
                .Execute(choiceContext);
        }

        await CardPileCmd.Draw(choiceContext, DynamicVars["ReleaseDraw"].IntValue, Owner, false);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
}

public class Reflect() : SakuraModCard(1, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Water];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar("UpgradeBlock", 3, ValueProp.Move)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (IsUpgraded)
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars["UpgradeBlock"].IntValue, ValueProp.Move, play, false);

        if (ShouldRelease)
            await PowerCmd.Apply<StrongReflectionPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this, false);
        else
            await PowerCmd.Apply<ReflectionPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this, false);
    }

    protected override void OnUpgrade() {}
}

public class Flight() : SakuraModCard(1, CardType.Skill, CardRarity.Common, TargetType.Self), IReleaseable
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Wind];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(4, ValueProp.Move), new PowerVar<SakuraTemporaryDexterityPower>(1), new EnergyVar(1)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await CommonActions.CardBlock(this, play);
        await PowerCmd.Apply<SakuraTemporaryDexterityPower>(choiceContext, Owner.Creature, DynamicVars["SakuraTemporaryDexterityPower"].IntValue, Owner.Creature, this, false);
        await TriggerReleaseEffect(choiceContext, play);
    }

    public async Task OnReleased(PlayerChoiceContext choiceContext, CardPlay play) =>
        await PlayerCmd.GainEnergy(1, Owner);

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2);
    }
}

public class DreamWand() : SakuraModCard(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        SakuraVoicePlayback.TryPlay(SakuraVoiceTrigger.DreamWand, CombatState);

        var selected = await SakuraActions.SelectUpToHandCards(
            this,
            choiceContext,
            card => SakuraCardCatalog.IsTransparentCard(card) && !card.IsReleased(),
            DynamicVars.Cards.IntValue,
            cancelable: false);
        foreach (var card in selected)
        {
            await SakuraActions.ReleaseThisTurnAndRecord(choiceContext, card);
            await SakuraActions.ReduceCostThisTurn(choiceContext, this, card);
        }
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
