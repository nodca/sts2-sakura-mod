using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.FourthAct.Wind.CardState;
using SakuraMod.SakuraModCode.FourthAct.Visuals;
using SakuraMod.SakuraModCode.Powers;

namespace SakuraMod.SakuraModCode.FourthAct.Wind.Powers;

public sealed class IllusionIdentityPower : SakuraPowerModel
{
    private bool _revealAfterPowerApplication;

    protected override string IconFileName => "fourth_act/illusion_identity.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override int DisplayAmount => -1;
    public bool IsRealBodyRevealed { get; private set; }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Owner.Monster is Models.IllusionMonster illusion && player.Creature.IsAlive)
        {
            IsRealBodyRevealed = false;
            await illusion.ReshufflePresentationAsync();
        }
    }

    public override Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target == Owner && IsPlayerEffect(dealer, cardSource))
            RevealRealBody();
        return Task.CompletedTask;
    }

    public override bool TryModifyPowerAmountReceived(
        PowerModel canonicalPower,
        Creature target,
        decimal amount,
        Creature? applier,
        out decimal modifiedAmount)
    {
        modifiedAmount = amount;
        if (Owner.Monster is Models.IllusionMonster && target == Owner && applier?.IsPlayer == true)
        {
            _revealAfterPowerApplication = true;
            return true;
        }

        return false;
    }

    public override Task AfterModifyingPowerAmountReceived(PowerModel power)
    {
        if (_revealAfterPowerApplication)
        {
            _revealAfterPowerApplication = false;
            RevealRealBody();
        }

        return Task.CompletedTask;
    }

    private void RevealRealBody()
    {
        if (Owner.Monster is not Models.IllusionMonster || IsRealBodyRevealed)
            return;

        IsRealBodyRevealed = true;
        MegaCrit.Sts2.Core.Helpers.TaskHelper.RunSafely(
            Visuals.IllusionVisualController.RevealRealBodyAsync(Owner));
    }

    private static bool IsPlayerEffect(Creature? dealer, CardModel? cardSource) =>
        dealer?.IsPlayer == true || cardSource?.Owner.Creature.IsPlayer == true;
}

public sealed class IllusionProjectionPower : SakuraPowerModel
{
    private bool _absorbedStatus;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    protected override bool IsVisibleInternal => false;

    public override decimal ModifyDamageCap(Creature? target, ValueProp props, Creature? dealer, CardModel? cardSource) =>
        target == Owner && (dealer?.IsPlayer == true || cardSource?.Owner.Creature.IsPlayer == true)
            ? 0m
            : base.ModifyDamageCap(target, props, dealer, cardSource);

    public override bool TryModifyPowerAmountReceived(PowerModel canonicalPower, Creature target, decimal amount, Creature? applier, out decimal modifiedAmount)
    {
        if (target == Owner
            && applier?.IsPlayer == true
            && canonicalPower is not IllusionIdentityPower
            && canonicalPower is not IllusionProjectionPower
            && canonicalPower is not MegaCrit.Sts2.Core.Models.Powers.MinionPower)
        {
            _absorbedStatus = true;
            modifiedAmount = 0;
            return true;
        }

        modifiedAmount = amount;
        return false;
    }

    public override async Task AfterModifyingPowerAmountReceived(PowerModel power)
    {
        if (_absorbedStatus && Owner.IsAlive)
        {
            _absorbedStatus = false;
            await Visuals.IllusionVisualController.DissolveProjectionAsync(
                Owner,
                Visuals.IllusionVisualController.StatusColor(power));
            await CreatureCmd.Kill(Owner);
        }
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target == Owner && Owner.IsAlive && (dealer?.IsPlayer == true || cardSource?.Owner.Creature.IsPlayer == true))
        {
            await Visuals.IllusionVisualController.DissolveProjectionAsync(
                Owner,
                new Godot.Color(0.54f, 0.88f, 0.98f, 0.94f));
            await CreatureCmd.Kill(Owner);
        }
    }
}

public sealed class WindSovereigntyPower : SakuraPowerModel
{
    protected override string IconFileName => "fourth_act/wind_sovereignty.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override int DisplayAmount => -1;

    public override bool TryModifyPowerAmountReceived(PowerModel canonicalPower, Creature target, decimal amount, Creature? applier, out decimal modifiedAmount)
    {
        if (target.IsPlayer && canonicalPower is ClassicWindyPower or ClassicWindyPermanentPower && amount > 0)
        {
            modifiedAmount = 0;
            Flash();
            SakuraElementStateHud.NotifyPrevented(target.Player, SakuraLockedElementStates.Wind);
            return true;
        }

        modifiedAmount = amount;
        return false;
    }
}

public sealed class WindBindPower : SakuraPowerModel
{
    protected override string IconFileName => "fourth_act/wind_bind.png";
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (Amount > 0 && play.Card.Owner.Creature == Owner)
            await PowerCmd.ModifyAmount(choiceContext, this, -1, Owner, null, true);
    }
}

public sealed class WindWallPower : SakuraPowerModel
{
    protected override string IconFileName => "fourth_act/wind_wall.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyDamageCap(Creature? target, ValueProp props, Creature? dealer, CardModel? cardSource) =>
        Amount > 0 && target == Owner && dealer?.IsPlayer == true && props.IsPoweredAttack()
            ? 0m
            : base.ModifyDamageCap(target, props, dealer, cardSource);

    public override async Task AfterModifyingDamageAmount(CardModel? cardSource)
    {
        // Unlike ModifyDamageCap, this callback is not used by damage previews.
        if (Amount > 0)
        {
            FourthActCombatFeedbackVisuals.BeginWindWallInterception(Owner);
            await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), this, -1, Owner, cardSource, true);
        }
    }
}

public sealed class WindyNextActionDamagePower : SakuraPowerModel
{
    protected override string IconFileName => "fourth_act/gathered_wind.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource) =>
        Amount > 0 && dealer == Owner && target?.IsPlayer == true && props.IsPoweredAttack()
            ? Amount
            : 0;

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Enemy && participants.Contains(Owner))
            await PowerCmd.Remove(this);
    }
}

public sealed class WindyBattlePower : SakuraPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override int DisplayAmount => -1;
    protected override bool IsVisibleInternal => false;

    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player || Owner.IsDead)
            return;

        var activePlayers = CombatState.Players.Where(static player => player.Creature.IsAlive).ToList();
        var unresolved = new List<int>(activePlayers.Count);
        foreach (var player in activePlayers)
        {
            var bind = player.Creature.GetPower<WindBindPower>();
            var amount = Math.Max(0, bind?.Amount ?? 0);
            unresolved.Add(amount);
            for (var index = 0; index < amount; index++)
            {
                var dazed = CombatState.CreateCard<Dazed>(player);
                await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
                    dazed,
                    PileType.Draw,
                    player,
                    CardPilePosition.Random);
            }

            await PowerCmd.Remove(bind);
        }

        var attackBonus = WindEnemyRules.FailedBindAttackBonus(unresolved);
        if (attackBonus > 0)
            await PowerCmd.Apply<WindyNextActionDamagePower>(choiceContext, Owner, attackBonus, Owner, null, true);

        var wall = Owner.GetPower<WindWallPower>();
        var existingWall = wall?.Amount ?? 0;
        var totalWall = WindEnemyRules.AggregateWall(existingWall, unresolved, CombatState.Players.Count);
        FourthActCombatFeedbackVisuals.PlayWindBindConversion(
            Owner,
            unresolved.Sum(),
            attackBonus,
            Math.Max(0, totalWall - existingWall));
        if (totalWall > existingWall)
            await PowerCmd.Apply<WindWallPower>(choiceContext, Owner, totalWall - existingWall, Owner, null, true);
    }
}

public sealed class FloatDrawCounterPower : SakuraPowerModel
{
    private int _drawCount;
    protected override string IconFileName => "fourth_act/updraft.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override int DisplayAmount => _drawCount;
    public int DrawCount => _drawCount;

    public override Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (card.Owner.Creature.IsAlive)
        {
            _drawCount++;
            InvokeDisplayAmountChanged();
        }
        return Task.CompletedTask;
    }

    public void Reset()
    {
        _drawCount = 0;
        InvokeDisplayAmountChanged();
    }
}

public sealed class WindSleepSelectionPower : SakuraPowerModel
{
    protected override string IconFileName => "fourth_act/drowsy.png";
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner || Amount <= 0)
            return;

        var eligible = CardPile.GetCards(player, PileType.Hand)
            .Where(static candidate => candidate.CanPlay() && !WindSleepingCards.IsSleeping(candidate))
            .ToList();
        if (eligible.Count > 0)
        {
            var selected = player.RunState.Rng.CombatCardSelection.NextItem(eligible)
                ?? throw new InvalidOperationException("Sleep selection unexpectedly returned no eligible card.");
            if (Applier is { } source)
            {
                await FourthActCombatFeedbackVisuals.PlayTransferAsync(
                    source,
                    Owner,
                    new Godot.Color(0.49f, 0.4f, 0.72f, 0.92f));
            }
            if (await WindSleepingCards.MarkSleeping(selected))
                await PowerCmd.Apply<WindSleepWakePower>(choiceContext, Owner, 1, Applier, null, true);
        }

        await PowerCmd.ModifyAmount(choiceContext, this, -1, Applier, null, true);
    }

    public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature, bool wasRemovalPrevented, float deathAnimLength)
    {
        if (!wasRemovalPrevented && creature.Monster is Models.SleepMonster)
        {
            WindSleepingCards.WakeAll(Owner.Player!);
            await PowerCmd.Remove(this);
        }
    }
}

public sealed class WindSleepWakePower : SakuraPowerModel
{
    protected override string IconFileName => "fourth_act/sleeping_cards.png";
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override int DisplayAmount => -1;

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == Owner && result.UnblockedDamage > 0 && dealer?.IsEnemy == true && props.IsPoweredAttack())
        {
            foreach (var player in CombatState.Players)
            {
                WindSleepingCards.WakeAll(player);
                await PowerCmd.Remove(player.Creature.GetPower<WindSleepWakePower>());
            }
        }
    }

    public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature, bool wasRemovalPrevented, float deathAnimLength)
    {
        if (!wasRemovalPrevented && creature.Monster is Models.SleepMonster)
        {
            WindSleepingCards.WakeAll(Owner.Player!);
            await PowerCmd.Remove(this);
        }
    }
}
