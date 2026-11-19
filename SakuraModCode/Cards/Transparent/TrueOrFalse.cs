using MegaCrit.Sts2.Core;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.Extensions;
using STS2RitsuLib.Combat.HandSize;

namespace SakuraMod.SakuraModCode.Cards;

public class TrueOrFalse() : TransparentCard(1, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    internal const string DamageVar = "ForgottenDamage";
    internal const string BlockVar = "RecallBlock";

    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Fire];
    internal override IEnumerable<string> ReferencedStaticHoverTipKeys =>
        [SakuraCardHoverTips.TemporaryTipKey, SakuraMemoryPile.PileId];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(DamageVar, 8),
        new DynamicVar(BlockVar, 5)
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        var power = await PowerCmd.Apply<TrueOrFalsePower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[DamageVar].IntValue,
            Owner.Creature,
            this,
            false);
        power?.AddBlock(DynamicVars[BlockVar].IntValue);
    }

    protected override void OnUpgrade()
    {
        DynamicVars[DamageVar].UpgradeValueBy(2);
        DynamicVars[BlockVar].UpgradeValueBy(2);
    }
}
