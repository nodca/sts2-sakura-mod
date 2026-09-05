using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using STS2RitsuLib.Scaffolding.Content;

namespace SakuraMod.SakuraModCode.FourthAct.Water.Powers;

public sealed class WaterFrozenPower : SakuraPowerModel
{
    protected override string IconFileName => "fourth_act/water_frozen.png";
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType) =>
        card.Owner?.Creature != Owner || autoPlayType != AutoPlayType.None;
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != Owner.Side || !participants.Contains(Owner)) return;
        if (Amount <= 1) await PowerCmd.Remove(this);
        else await PowerCmd.Decrement(this);
    }
}

public sealed class DrenchedPower : SakuraPowerModel
{
    protected override string IconFileName => "fourth_act/drenched.png";
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal currentCost, out decimal newCost)
    {
        if (card.Owner?.Creature == Owner && currentCost > 0 && !card.EnergyCost.CostsX)
        { newCost = currentCost + 1; return true; }
        newCost = currentCost; return false;
    }
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    { if (side == Owner.Side && participants.Contains(Owner)) await PowerCmd.Remove(this); }
}

public sealed class WaterSovereigntyPower : SakuraPowerModel
{
    protected override string IconFileName => "fourth_act/water_sovereignty.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override int DisplayAmount => -1;
    public override bool TryModifyPowerAmountReceived(PowerModel canonicalPower, Creature target, decimal amount, Creature? applier, out decimal modifiedAmount)
    {
        if (target.IsPlayer && canonicalPower is ClassicWateryPower or ClassicWateryPermanentPower && amount > 0)
        {
            modifiedAmount = 0;
            Flash();
            SakuraElementStateHud.NotifyPrevented(target.Player, SakuraElementSet.Water);
            return true;
        }
        modifiedAmount = amount; return false;
    }
}

public sealed class WaterReservoirPower : SakuraPowerModel
{
    private readonly Dictionary<Creature, int> _values = [];

    protected override string IconFileName => "fourth_act/water_reservoir.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override int DisplayAmount => _values.Values.Sum();

    public int For(Creature player) => _values.GetValueOrDefault(player);

    public void Add(Creature player, int amount)
    {
        if (amount <= 0)
            return;
        _values[player] = For(player) + amount;
        InvokeDisplayAmountChanged();
    }

    public void Consume(Creature player, int amount)
    {
        if (amount <= 0)
            return;
        _values[player] = WaterEnemyRules.RemainingReservoir(For(player), amount);
        InvokeDisplayAmountChanged();
    }
}
