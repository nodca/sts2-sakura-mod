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

public class Gravitation() : TransparentCard(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Wind];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar(GravitationHoldPower.ReturnDamageVar, 3)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        await GravitationVfx.PlayOrResolveAsync(this, Owner.Creature, async cues =>
        {
            var power = await PowerCmd.Apply<GravitationHoldPower>(
                choiceContext,
                Owner.Creature,
                1,
                Owner.Creature,
                this,
                false);
            // Every Gravitation stacks its base damage; Gravitation itself never returns.
            power?.AddReturnDamage(DynamicVars[GravitationHoldPower.ReturnDamageVar].IntValue);
            if (power is not null)
                cues.OpenWell();
        });
    }

    protected override void OnUpgrade()
    {
        AddKeywordIfMissing(CardKeyword.Retain);
        DynamicVars[GravitationHoldPower.ReturnDamageVar].UpgradeValueBy(1);
    }
}
