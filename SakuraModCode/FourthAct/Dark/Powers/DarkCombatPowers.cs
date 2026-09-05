using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.FourthAct;
using SakuraMod.SakuraModCode.FourthAct.Dark.Cards;
using SakuraMod.SakuraModCode.FourthAct.Dark.Models;
using SakuraMod.SakuraModCode.Powers;
using STS2RitsuLib.Combat.HandSize;

namespace SakuraMod.SakuraModCode.FourthAct.Dark.Powers;

public sealed class DarknessPower : SakuraPowerModel
{
    protected override string IconFileName => "fourth_act/dark_veil.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || dealer?.IsPlayer != true && cardSource?.Owner?.Creature.IsPlayer != true)
            return 1m;
        return DarkEnemyRules.DarknessDamageMultiplier(Amount);
    }
}

public sealed class DarkSovereigntyPower : SakuraPowerModel
{
    protected override string IconFileName => "fourth_act/dark_sovereignty.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override int DisplayAmount => -1;

    public override bool TryModifyPowerAmountReceived(PowerModel canonicalPower, Creature target, decimal amount, Creature? applier, out decimal modifiedAmount)
    {
        if (target.IsPlayer && amount > 0 && canonicalPower is ClassicWindyPower or ClassicWindyPermanentPower or ClassicWateryPower or ClassicWateryPermanentPower)
        {
            modifiedAmount = 0;
            Flash();
            SakuraElementStateHud.NotifyPrevented(target.Player, SakuraElementState.LocksForPower(canonicalPower));
            return true;
        }
        modifiedAmount = amount;
        return false;
    }
}

public sealed class DarkBattlePower : SakuraPowerModel, IMaxHandSizeModifier
{
    protected override string IconFileName => "fourth_act/dark_trial.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override int DisplayAmount => -1;

    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        if (combatState != CombatState || !Owner.IsAlive || !player.Creature.IsAlive)
            return;
        for (var index = 0; index < DarkEnemyRules.MicroLightsPerDraw; index++)
            await SakuraGeneratedCardLifecycle.AddGeneratedCardToHand(combatState.CreateCard<MicroLight>(player), choiceContext);
    }

    public int ModifyMaxHandSize(Player player, int currentMaxHandSize) =>
        Owner.IsAlive && player.Creature.IsAlive && player.Creature.CombatState == CombatState
            ? DarkEnemyRules.ModifyMaxHandSize(currentMaxHandSize)
            : currentMaxHandSize;

    public int ModifyMaxHandSizeLate(Player player, int currentMaxHandSize) => currentMaxHandSize;

    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player || !Owner.IsAlive || CombatState is not { } combatState)
            return;
        var participantSet = participants.ToHashSet();
        foreach (var player in combatState.Players.Where(player => player.Creature.IsAlive && participantSet.Contains(player.Creature)))
            await SakuraFadeCardLifecycle.RemoveEligibleCardsFromHand(player, combatState);
    }

    public override Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player && FourthActCombatRules.IsCompletePlayerSide(CombatState, participants))
            return Task.CompletedTask;
        return Task.CompletedTask;
    }

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == Owner && result.UnblockedDamage > 0 && Owner.IsAlive
            && Owner.Monster is DarkMonster dark
            && !dark.TransitionTriggered
            && Owner.CurrentHp <= Owner.MaxHp * DarkEnemyRules.TransitionHpRatio)
            await dark.BeginTransition();
    }
}

public sealed class DarkConfinementSelectionPower : SakuraPowerModel
{
    private static readonly LocString SelectionPrompt = new("powers", "SAKURA_MOD_POWER_DARK_CONFINEMENT_SELECTION_POWER.selectionPrompt");
    protected override string IconFileName => "fourth_act/confinement.png";
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner || Amount <= 0 || !Owner.IsAlive)
            return;
        var eligible = CardPile.GetCards(player, PileType.Hand).Where(IsEligible).ToList();
        if (eligible.Count == 0)
            Flash();
        else
        {
            var selected = (await CardSelectCmd.FromHand(choiceContext, player,
                new CardSelectorPrefs(SelectionPrompt, 1) { Cancelable = false, RequireManualConfirmation = false },
                IsEligible, this)).FirstOrDefault();
            if (selected is not null)
                await ApplyConfinement(choiceContext, selected);
        }
        await PowerCmd.ModifyAmount(choiceContext, this, -1, Applier, null, true);
    }

    internal static bool IsEligible(CardModel card) => card is not MicroLight && !card.IsTemporary();

    internal static async Task ApplyConfinement(PlayerChoiceContext choiceContext, CardModel card)
    {
        if (IsEligible(card) && await SakuraForgotten.GrantTemporary(choiceContext, card))
            CardCmd.Preview(card);
    }
}

public static class DarkMicroLightCoordinator
{
    public static async Task ApplyMicroLight(PlayerChoiceContext choiceContext, Player owner, int amount)
    {
        var dark = FindDark(owner.Creature.CombatState);
        if (dark is null || amount <= 0)
            return;
        var darkness = dark.Creature.GetPower<DarknessPower>()
            ?? throw new InvalidOperationException("The Dark has no Darkness power.");
        await PowerCmd.ModifyAmount(choiceContext, darkness,
            DarkEnemyRules.ChangeDarkness(darkness.Amount, -amount) - darkness.Amount,
            owner.Creature, null, false);
    }

    private static DarkMonster? FindDark(ICombatState? combatState) =>
        combatState?.Enemies.Select(static enemy => enemy.Monster).OfType<DarkMonster>()
            .FirstOrDefault(static dark => dark.Creature.IsAlive);
}
