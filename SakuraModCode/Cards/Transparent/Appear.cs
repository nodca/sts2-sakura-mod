using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.HoverTips;
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

public class Appear() : TransparentCard(2, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Wind];
    internal override IEnumerable<CardKeyword> ReferencedKeywords => [SakuraKeywords.Manifest];
    internal override IEnumerable<string> ReferencedStaticHoverTipKeys =>
        [SakuraCardHoverTips.TemporaryTipKey];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new CardsVar(1)];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromCard<SpellHuoShen>(),
        HoverTipFactory.FromCard<SpellLeiDi>(),
        HoverTipFactory.FromCard<SpellFengHua>(),
        HoverTipFactory.FromCard<SpellShuiLong>()
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        var power = await PowerCmd.Apply<AppearPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars.Cards.IntValue,
            Owner.Creature,
            this,
            false);
        if (IsUpgraded)
            power?.AddUpgradedStacks(DynamicVars.Cards.IntValue);
    }

    // The upgrade keeps the cost and upgrades this copy's random card each turn instead.
    protected override void OnUpgrade() { }
}
