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

public class PromiseManifestPower : SakuraPowerModel
{
    protected override string IconFileName => "promise_manifest.png";

    private bool _lostHp;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature creature, DamageResult damageResult, ValueProp damageProps, Creature? source, CardModel? card)
    {
        if (creature == Owner && damageResult.UnblockedDamage > 0)
            _lostHp = true;

        return Task.CompletedTask;
    }

    public override decimal ModifyHandDraw(Player player, decimal count) =>
        player.Creature == Owner && !_lostHp
            ? count + Amount
            : count;

    public override async Task AfterEnergyReset(Player player)
    {
        if (player.Creature == Owner && !_lostHp)
            await PlayerCmd.GainEnergy(Amount, player);
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner)
            return;

        await PowerCmd.Remove(this);
    }
}

