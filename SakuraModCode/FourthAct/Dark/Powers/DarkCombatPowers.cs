using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.FourthAct.Dark.Afflictions;
using SakuraMod.SakuraModCode.FourthAct.Dark.Cards;
using SakuraMod.SakuraModCode.FourthAct.Dark.Models;
using SakuraMod.SakuraModCode.Powers;

namespace SakuraMod.SakuraModCode.FourthAct.Dark.Powers;

public sealed class DarkLightPower : SakuraPowerModel
{
    protected override string IconFileName => "light_power.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    internal void SetLight(int amount) => SetAmount(Math.Max(0, amount), silent: true);
}

public sealed class DarkNightPower : SakuraPowerModel
{
    protected override string IconFileName => "dark_power.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
}

public sealed class DarkVeilPower : SakuraPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override int DisplayAmount => -1;

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource) =>
        target == Owner && (dealer?.IsPlayer == true || cardSource?.Owner?.Creature.IsPlayer == true)
            ? DarkEnemyRules.VeilDamageMultiplier
            : 1m;
}

public sealed class DarkSovereigntyPower : SakuraPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override int DisplayAmount => -1;

    public override bool TryModifyPowerAmountReceived(PowerModel canonicalPower, Creature target, decimal amount, Creature? applier, out decimal modifiedAmount)
    {
        if (target.IsPlayer && amount > 0 && canonicalPower is ClassicWindyPower or ClassicWindyPermanentPower or ClassicWateryPower or ClassicWateryPermanentPower)
        {
            modifiedAmount = 0;
            return true;
        }

        modifiedAmount = amount;
        return false;
    }
}

public sealed class DarkBattlePower : SakuraPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override int DisplayAmount => -1;

    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        if (combatState != CombatState || !player.Creature.IsAlive || Owner.Monster is not DarkMonster { CanGenerateMicroLight: true })
            return;

        for (var index = 0; index < DarkEnemyRules.MicroLightsPerDraw; index++)
        {
            var light = combatState.CreateCard<MicroLight>(player);
            await SakuraGeneratedCardLifecycle.AddGeneratedCardToHand(light, choiceContext);
        }
    }

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == Owner && result.UnblockedDamage > 0 && Owner.IsAlive
            && Owner.Monster is DarkMonster { Phase: DarkPhase.Veiled } dark
            && Owner.CurrentHp < Owner.MaxHp * DarkEnemyRules.TransitionHpRatio)
            await dark.BeginTransition();
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player && Owner.Monster is DarkMonster dark)
            await dark.AdvanceVeilWindow(choiceContext);
    }
}

public sealed class DarkConfinementSelectionPower : SakuraPowerModel
{
    private static readonly LocString SelectionPrompt = new("powers", "SAKURA_MOD_POWER_DARK_CONFINEMENT_SELECTION_POWER.selectionPrompt");

    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner || Amount <= 0 || !Owner.IsAlive)
            return;

        var eligible = CardPile.GetCards(player, PileType.Hand).Where(IsEligible).ToList();
        if (eligible.Count == 0)
        {
            Flash();
        }
        else
        {
            var selected = (await CardSelectCmd.FromHand(
                    choiceContext,
                    player,
                    new CardSelectorPrefs(SelectionPrompt, 1) { Cancelable = false, RequireManualConfirmation = false },
                    IsEligible,
                    this)).FirstOrDefault();
            if (selected is not null)
                await ApplyConfinement(choiceContext, selected);
        }

        await PowerCmd.ModifyAmount(choiceContext, this, -1, Applier, null, true);
    }

    internal static bool IsEligible(CardModel card) => card is not MicroLight && !card.IsTemporary() && card.Affliction is null;

    internal static async Task ApplyConfinement(PlayerChoiceContext choiceContext, CardModel card)
    {
        if (!IsEligible(card))
            return;

        if (await CardCmd.Afflict<DarkConfinementAffliction>(card, 1) is null)
            return;
        if (!await SakuraGeneratedCardLifecycle.GrantTemporary(choiceContext, card))
        {
            CardCmd.ClearAffliction(card);
            return;
        }

        CardCmd.Preview(card);
    }
}

public static class DarkLightCoordinator
{
    public static async Task TryGainLight(PlayerChoiceContext choiceContext, Player owner, int amount)
    {
        var dark = FindDark(owner.Creature.CombatState);
        if (dark is null || amount <= 0 || dark.Phase == DarkPhase.Veiled && !dark.IsVeilActive)
            return;

        await PowerCmd.Apply<DarkLightPower>(choiceContext, owner.Creature, amount, dark.Creature, null, true);
        await ResolveThresholds(choiceContext, dark);
    }

    public static async Task ResolveThresholds(PlayerChoiceContext choiceContext, DarkMonster dark)
    {
        var threshold = DarkEnemyRules.LightThreshold(dark.CombatStartPlayerCount);
        while (AggregateLight(dark) >= threshold)
        {
            if (dark.Phase == DarkPhase.Veiled)
            {
                if (!dark.IsVeilActive)
                    return;
                ConsumeThreshold(dark, threshold);
                await dark.BreakVeil(choiceContext);
                return;
            }

            if (dark.Phase != DarkPhase.EternalNight || dark.Night <= 1)
                return;

            ConsumeThreshold(dark, threshold);
            var night = dark.Creature.GetPower<DarkNightPower>()
                ?? throw new InvalidOperationException("The Dark has no Night power during Eternal Night.");
            await PowerCmd.ModifyAmount(choiceContext, night, -1, dark.Creature, null, true);
            dark.RefreshPhaseTwoIntent();
        }
    }

    public static async Task OnTemporaryStabilized(PlayerChoiceContext choiceContext, CardModel card)
    {
        if (card.Affliction is not DarkConfinementAffliction || card.Owner is not { } owner)
            return;

        CardCmd.ClearAffliction(card);
        await TryGainLight(choiceContext, owner, 1);
    }

    public static void ClearSourceMarker(CardModel card)
    {
        if (card.Affliction is DarkConfinementAffliction)
            CardCmd.ClearAffliction(card);
    }

    private static DarkMonster? FindDark(ICombatState? combatState) =>
        combatState?.Enemies.Select(static enemy => enemy.Monster).OfType<DarkMonster>().FirstOrDefault(static dark => dark.Creature.IsAlive);

    private static int AggregateLight(DarkMonster dark) =>
        dark.CombatState.Players.Sum(static player => Math.Max(0, player.Creature.GetPower<DarkLightPower>()?.Amount ?? 0));

    private static void ConsumeThreshold(DarkMonster dark, int threshold)
    {
        var remaining = threshold;
        foreach (var player in dark.CombatState.Players)
        {
            var light = player.Creature.GetPower<DarkLightPower>();
            var consumed = Math.Min(remaining, Math.Max(0, light?.Amount ?? 0));
            if (light is not null && consumed > 0)
                light.SetLight(light.Amount - consumed);
            remaining -= consumed;
            if (remaining == 0)
                return;
        }

        throw new InvalidOperationException("Aggregate Light changed during threshold consumption.");
    }
}
