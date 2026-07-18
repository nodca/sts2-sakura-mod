using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Extensions;
using SakuraMod.SakuraModCode.Powers;

namespace SakuraMod.SakuraModCode.Cards;

public class Gale() : SakuraExtraEffectCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Wind];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(3, ValueProp.Move),
        new GaleHitsVar(),
        new CardsVar("ExtraDraw", 3)
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        var target = RequiredTarget(play);
        var hits = GaleRules.HitCount(this);
        await SakuraActions.AttackCommand(this, target, DynamicVars.Damage.IntValue, DynamicVars.Damage.Props)
            .WithHitCount(hits)
            .WithHitVfxNode(target => SakuraCardPlayVfx.CreateGaleWindBlade(Owner.Creature, target))
            .Execute(choiceContext);

        if (activation.IsActive)
            await ApplyExtraEffect(choiceContext, play);
    }

    private async Task ApplyExtraEffect(PlayerChoiceContext choiceContext, CardPlay play) =>
        await CardPileCmd.Draw(choiceContext, DynamicVars["ExtraDraw"].IntValue, Owner, false);

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(1);
}

internal sealed class GaleHitsVar() : DynamicVar("Hits", 1)
{
    public override void UpdateCardPreview(CardModel card, CardPreviewMode previewMode, Creature? target, bool runGlobalHooks) =>
        PreviewValue = GaleRules.HitCount(card);
}

internal static class GaleRules
{
    public static int HitCount(CardModel card) =>
        HitCount(SakuraDrawCountHook.DrawCountThisTurn(card));

    internal static int HitCount(int drawCount) =>
        1 + Math.Max(0, drawCount) / 2;
}

public class Reflect() : SakuraExtraEffectCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Water];
    internal override IEnumerable<string> ReferencedStaticHoverTipKeys =>
        [SakuraCardHoverTips.ReflectionTipKey];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(5, ValueProp.Move),
        new PowerVar<ReflectionPower>(2),
        new DynamicVar("ExtraReflection", 2)
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play, false);
        var reflection = DynamicVars["ReflectionPower"].IntValue;
        if (activation.IsActive)
            reflection += DynamicVars["ExtraReflection"].IntValue;
        await PowerCmd.Apply<ReflectionPower>(choiceContext, Owner.Creature, reflection, Owner.Creature, this, false);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3);
        DynamicVars["ReflectionPower"].UpgradeValueBy(1);
    }
}

public class Flight() : SakuraExtraEffectCard(1, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Wind];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(5, ValueProp.Move), new PowerVar<SakuraTemporaryDexterityPower>(2), new EnergyVar(1)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play, false);
        await PowerCmd.Apply<SakuraTemporaryDexterityPower>(choiceContext, Owner.Creature, DynamicVars["SakuraTemporaryDexterityPower"].IntValue, Owner.Creature, this, false);
        if (activation.IsActive)
            await ApplyExtraEffect(choiceContext, play);
    }

    private async Task ApplyExtraEffect(PlayerChoiceContext choiceContext, CardPlay play) =>
        await PlayerCmd.GainEnergy(1, Owner);

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2);
    }
}
