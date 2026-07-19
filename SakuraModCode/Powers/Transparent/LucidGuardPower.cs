using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.Extensions;
using STS2RitsuLib.Combat.HandSize;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace SakuraMod.SakuraModCode.Powers;

public class LucidGuardPower : SakuraPowerModel
{
    protected override string IconFileName => "lucid_guard.png";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyDamageCap(Creature? creature, ValueProp props, Creature? source, CardModel? card) =>
        creature == Owner && Amount > 0 && props.IsPoweredAttack()
            ? Math.Max(0, Amount)
            : base.ModifyDamageCap(creature, props, source, card);

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature creature, DamageResult damageResult, ValueProp damageProps, Creature? source, CardModel? card)
    {
        if (creature == Owner && damageResult.UnblockedDamage > 0 && Amount > 0 && damageProps.IsPoweredAttack())
            await PowerCmd.Remove(this);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner.Side == side && participants.Contains(Owner))
            await PowerCmd.Remove(this);
    }
}

