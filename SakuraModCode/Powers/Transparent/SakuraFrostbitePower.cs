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
using SakuraMod.SakuraModCode.FourthAct.Water.Models;
using SakuraMod.SakuraModCode.FourthAct.Water.Powers;
using STS2RitsuLib.Combat.HandSize;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace SakuraMod.SakuraModCode.Powers;

public class SakuraFrostbitePower : SakuraPowerModel
{
    internal const int InitialFreezeThreshold = 5;
    private const int PlayerFrozenBlock = 5;

    protected override string IconFileName => "frostbite.png";

    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("FreezeThreshold", InitialFreezeThreshold)];

    internal int CurrentFreezeThreshold =>
        InitialFreezeThreshold + (Owner.GetPower<SakuraFreezeResistancePower>()?.Amount ?? 0);

    internal static (int FreezeStacks, int RemainingFrostbite) ConvertToFreeze(int frostbite, int threshold)
    {
        var remaining = Math.Max(0, frostbite);
        var stacks = 0;
        while (remaining >= threshold)
        {
            remaining -= threshold;
            threshold++;
            stacks++;
        }
        return (stacks, remaining);
    }

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource) =>
        target == Owner && Amount > 0 && props.IsPoweredAttack()
            ? 1m + Amount / 10m
            : 1m;

    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        await ResolveFreeze(new ThrowingPlayerChoiceContext(), applier, cardSource);
        FreezeShellVisual.NotifyFrostbite(Owner, pulse: true);
    }

    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (power == this)
        {
            await ResolveFreeze(choiceContext, applier, cardSource);
            FreezeShellVisual.NotifyFrostbite(Owner, pulse: amount > 0);
        }
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != Owner.Side || !participants.Contains(Owner))
            return;

        // Decrement, not Remove, so Clow Time's stasis can hold the last stack.
        await PowerCmd.Decrement(this);
    }

    private async Task ResolveFreeze(PlayerChoiceContext choiceContext, Creature? applier, CardModel? cardSource)
    {
        DynamicVars["FreezeThreshold"].BaseValue = CurrentFreezeThreshold;
        var (freezeStacks, remainingFrostbite) = ConvertToFreeze(Amount, CurrentFreezeThreshold);
        if (freezeStacks <= 0)
            return;

        var freezeApplier = applier ?? Applier ?? Owner;
        // Commit before modifying Frostbite: its amount-change hook re-enters this method.
        await PowerCmd.Apply<SakuraFreezeResistancePower>(choiceContext, Owner, freezeStacks, freezeApplier, cardSource, false);
        DynamicVars["FreezeThreshold"].BaseValue = CurrentFreezeThreshold;
        if (remainingFrostbite == 0)
            await PowerCmd.Remove(this);
        else
            await PowerCmd.ModifyAmount(choiceContext, this, remainingFrostbite - Amount, freezeApplier, cardSource, false);

        if (Owner.IsMonster)
            await PowerCmd.Apply<ClassicFreezePower>(choiceContext, Owner, freezeStacks, freezeApplier, cardSource, false);
        else
        {
            await PowerCmd.Apply<WaterFrozenPower>(choiceContext, Owner, freezeStacks, freezeApplier, cardSource, false);
            await CreatureCmd.GainBlock(Owner, PlayerFrozenBlock, SakuraPowerValueProps.Block, null, false);
        }
    }
}

// Native combat state retains the count when Frostbite and Freeze expire or are removed.
public class SakuraFreezeResistancePower : SakuraPowerModel
{
    protected override string IconFileName => "frostbite.png";
    protected override bool IsVisibleInternal => false;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
}
