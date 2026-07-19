using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Extensions;
using SakuraMod.SakuraModCode.Powers;
using STS2RitsuLib.Cards.DynamicVars;

namespace SakuraMod.SakuraModCode.Cards;

public class Blaze() : TransparentExtraEffectCard(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Fire];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CalculationBaseVar(18),
        new ExtraDamageVar(2),
        new CalculatedDamageVar(ValueProp.Move).WithMultiplier(BlazeRules.ExhaustedCardMultiplier)
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        var target = RequiredTarget(play);
        SakuraCardPlayVfx.PlayBlaze(target);
        await SakuraActions.Attack(choiceContext, this, target, DynamicVars.CalculatedDamage);
    }

    protected override void OnUpgrade() => DynamicVars.CalculationBase.UpgradeValueBy(6);
}

internal static class BlazeRules
{
    public static decimal ExhaustedCardMultiplier(CardModel card, Creature? target) =>
        card.Owner is { } owner
            ? (CardPile.Get(PileType.Exhaust, owner)?.Cards.Count ?? 0)
                * (SakuraCardModel.UsesMagicChargeExtraEffect(card) ? 2 : 1)
            : 0;
}

