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

public class SakuraFrostbitePower : SakuraPowerModel
{
    private const int FreezeThreshold = 6;

    protected override string IconFileName => "frostbite.png";

    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    internal static (int FreezeStacks, int RemainingFrostbite) ConvertToFreeze(int frostbite)
    {
        var amount = Math.Max(0, frostbite);
        return (amount / FreezeThreshold, amount % FreezeThreshold);
    }

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource) =>
        target == Owner && Amount > 0 && props.IsPoweredAttack()
            ? 1m + Amount / 10m
            : 1m;

    public override async Task AfterApplied(Creature? applier, CardModel? cardSource) =>
        await ResolveFreeze(new ThrowingPlayerChoiceContext(), applier, cardSource);

    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (power == this)
            await ResolveFreeze(choiceContext, applier, cardSource);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (!Owner.IsMonster || side != Owner.Side || !participants.Contains(Owner))
            return;

        if (Amount <= 1)
        {
            await PowerCmd.Remove(this);
            return;
        }

        await PowerCmd.Decrement(this);
    }

    private async Task ResolveFreeze(PlayerChoiceContext choiceContext, Creature? applier, CardModel? cardSource)
    {
        var (freezeStacks, remainingFrostbite) = ConvertToFreeze(Amount);
        if (freezeStacks <= 0)
            return;

        var freezeApplier = applier ?? Applier ?? Owner;
        if (remainingFrostbite == 0)
            await PowerCmd.Remove(this);
        else
            await PowerCmd.ModifyAmount(choiceContext, this, remainingFrostbite - Amount, freezeApplier, cardSource, false);

        await PowerCmd.Apply<ClassicFreezePower>(
            choiceContext,
            Owner,
            freezeStacks,
            freezeApplier,
            cardSource,
            false);
    }
}

