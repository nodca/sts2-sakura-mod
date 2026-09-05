using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.FourthAct.Fire.Cards;
using SakuraMod.SakuraModCode.FourthAct.Fire.Models;
using SakuraMod.SakuraModCode.FourthAct.Fire.Visuals;
using SakuraMod.SakuraModCode.Powers;

namespace SakuraMod.SakuraModCode.FourthAct.Fire.Powers;

internal enum LibraPresentationCause
{
    FacingRecorded,
    FacingVote,
    Imbalance,
    Recenter,
    TierResolved,
    PanLost
}

internal readonly record struct LibraPresentationEvent(
    LibraPresentationCause Cause,
    int OldLeft,
    int OldRight,
    int Left,
    int Right,
    string? Side = null,
    bool Strong = false);

public sealed class SwordBlockFeedingPower : SakuraPowerModel
{
    private readonly Dictionary<Creature, int> _bonuses = [];
    protected override string IconFileName => "fourth_act/sword_edge.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override int DisplayAmount => _bonuses.Values.DefaultIfEmpty().Max();
    public override Task AfterBlockGained(Creature creature, decimal amount, ValueProp props, CardModel? cardSource)
    {
        if (creature.IsPlayer && amount > 0) { _bonuses[creature] = _bonuses.GetValueOrDefault(creature) + 5; InvokeDisplayAmountChanged(); }
        return Task.CompletedTask;
    }
    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource) =>
        dealer == Owner && target is not null ? _bonuses.GetValueOrDefault(target) : 0;
    internal void Reset() { _bonuses.Clear(); InvokeDisplayAmountChanged(); }
}

public sealed class FireSovereigntyPower : SakuraPowerModel
{
    protected override string IconFileName => "fourth_act/fire_sovereignty.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override int DisplayAmount => -1;
    public override bool TryModifyPowerAmountReceived(PowerModel power, Creature target, decimal amount, Creature? applier, out decimal modified)
    {
        if (target.IsPlayer && amount > 0 && power is ClassicFireyPower or ClassicFireyPermanentPower) { modified = 0; Flash(); SakuraElementStateHud.NotifyPrevented(target.Player, SakuraElementSet.Fire); return true; }
        modified = amount; return false;
    }
}

public sealed class LightBattlePower : SakuraPowerModel
{
    protected override string IconFileName => "fourth_act/light_trial.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override int DisplayAmount => -1;
    public override bool TryModifyKeywordsInCombat(CardModel card, ISet<CardKeyword> keywords) =>
        Owner.IsAlive && card.Owner?.Creature.IsPlayer == true && !card.IsTemporary() && keywords.Add(CardKeyword.Retain);
    public override bool TryModifyPowerAmountReceived(PowerModel power, Creature target, decimal amount, Creature? applier, out decimal modified)
    {
        if (target.IsPlayer && amount > 0 && power is ClassicFireyPower or ClassicFireyPermanentPower or ClassicEarthyPower or ClassicEarthyPermanentPower) { modified = 0; Flash(); SakuraElementStateHud.NotifyPrevented(target.Player, SakuraElementSet.Fire | SakuraElementSet.Earth); return true; }
        modified = amount; return false;
    }
}

public sealed class LibraPendulumPower : SakuraPowerModel
{
    private readonly Dictionary<Creature, string> _facing = [];
    private (int Left, int Right) _lastEffectPosition = (5, 5);
    public int Left { get; private set; } = 5;
    public int Right { get; private set; } = 5;
    protected override string IconFileName => "fourth_act/libra_pendulum.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override int DisplayAmount => Owner.SlotName == "LEFT" ? Left : Right;
    internal event Action<LibraPresentationEvent>? PresentationChanged;

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (play.Card.Type == CardType.Attack && play.Target?.Monster is LibraPanMonster && play.Card.Owner?.Creature is { } player)
        {
            var side = play.Target.SlotName ?? "RIGHT";
            _facing[player] = side;
            SakuraStandeeVisuals.SetFacing(player, side == "LEFT");
            if (IsCoordinator())
                Notify(LibraPresentationCause.FacingRecorded, Left, Right, side);
        }
        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player || !FourthActCombatRules.IsCompletePlayerSide(CombatState, participants)) return;
        if (!IsCoordinator()) return;
        var vote = CombatState.Players.Where(p => p.Creature.IsAlive).Sum(p => _facing.GetValueOrDefault(p.Creature, "RIGHT") == "LEFT" ? -1 : 1);
        var survivor = CombatState.Enemies.FirstOrDefault(c => c.IsAlive
            && c.Monster is LibraPanMonster
            && c.GetPower<LibraImbalancePower>() is not null);
        var resolution = FireEnemyRules.ResolveLibraTurn(Left, Right, vote, survivor?.SlotName);
        var beforeVote = (Left, Right);
        SetAll(resolution.Vote.Left, resolution.Vote.Right);
        Notify(LibraPresentationCause.FacingVote, beforeVote.Left, beforeVote.Right);
        await LibraVisualController.WaitForPendingAsync(CombatState);

        if (resolution.Final != resolution.Vote)
        {
            SetAll(resolution.Final.Left, resolution.Final.Right);
            Notify(
                LibraPresentationCause.Imbalance,
                resolution.Vote.Left,
                resolution.Vote.Right,
                survivor?.SlotName);
            await LibraVisualController.WaitForPendingAsync(CombatState);
        }
        await KillPlayersAtExtreme(CombatState);
    }

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != CombatSide.Player || !FourthActCombatRules.IsCompletePlayerSide(CombatState, participants) || !IsCoordinator()) return;

        var currentPosition = (Left, Right);
        var isEntry = currentPosition != _lastEffectPosition;
        SetLastEffectPositionAll(currentPosition);

        var survivor = CombatState.Enemies.Where(c => c.IsAlive && c.Monster is LibraPanMonster).ToList();
        var deviation = Math.Abs(Right - 5);
        if (deviation == 1 && Left > Right)
            foreach (var pan in survivor)
                await CreatureCmd.GainBlock(pan, FireEnemyRules.LibraBlock, ValueProp.Move, null, false);
        if (deviation == 2)
            foreach (var player in CombatState.Players.Where(p => p.Creature.IsAlive))
                if (Right > Left)
                    await PowerCmd.Apply<WeakPower>(new ThrowingPlayerChoiceContext(), player.Creature, 1, Owner, null, false);
                else
                    await PowerCmd.Apply<VulnerablePower>(new ThrowingPlayerChoiceContext(), player.Creature, 1, Owner, null, false);
        if (deviation == 3)
            foreach (var player in CombatState.Players.Where(p => p.Creature.IsAlive))
                foreach (var pile in new[] { PileType.Draw, PileType.Hand, PileType.Discard })
                    await CardPileCmd.AddGeneratedCardToCombat(CombatState.CreateCard<Balance>(player), pile, player);

        Notify(LibraPresentationCause.TierResolved, Left, Right, strong: isEntry);
        await LibraVisualController.WaitForPendingAsync(CombatState);
    }

    public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature, bool wasRemovalPrevented, float deathAnimLength)
    {
        if (wasRemovalPrevented || creature.Monster is not LibraPanMonster || !IsCoordinator())
            return;

        Notify(LibraPresentationCause.PanLost, Left, Right, creature.SlotName);
        await LibraVisualController.WaitForPendingAsync(CombatState);
        var survivor = CombatState.Enemies.FirstOrDefault(c => c.IsAlive && c.Monster is LibraPanMonster);
        if (survivor is null)
            return;

        var survivorSide = survivor.SlotName == "LEFT" ? "LEFT" : "RIGHT";
        SetFacingAll(survivorSide);
        if (survivor.GetPower<LibraImbalancePower>() is null)
            await PowerCmd.Apply<LibraImbalancePower>(choiceContext, survivor, 1, Owner, null, true);
    }

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource) =>
        dealer == Owner && Left == 4 && Right == 6 ? FireEnemyRules.LibraAttackBonus : 0;

    public static async Task Recenter(PlayerChoiceContext context, ICombatState? combatState)
    {
        var power = combatState?.Enemies.Select(c => c.GetPower<LibraPendulumPower>())
            .FirstOrDefault(p => p?.IsCoordinator() == true);
        if (power is null)
            return;

        var before = (power.Left, power.Right);
        var next = FireEnemyRules.Recenter(before.Left, before.Right);
        power.SetAll(next.Left, next.Right);
        power.Notify(LibraPresentationCause.Recenter, before.Left, before.Right);
        await LibraVisualController.WaitForPendingAsync(combatState!);
    }
    private bool IsCoordinator() => Owner.SlotName == "RIGHT" && Owner.IsAlive || Owner.SlotName == "LEFT" && !CombatState.Enemies.Any(c => c.IsAlive && c.SlotName == "RIGHT" && c.Monster is LibraPanMonster);
    private void SetAll(int left, int right) { foreach (var power in CombatState.Enemies.Select(c => c.GetPower<LibraPendulumPower>()).Where(p => p is not null)) power!.Set(left, right); }
    private void SetLastEffectPositionAll((int Left, int Right) position)
    {
        foreach (var power in CombatState.Enemies.Select(c => c.GetPower<LibraPendulumPower>()).Where(p => p is not null))
            power!._lastEffectPosition = position;
    }
    private void SetFacingAll(string side)
    {
        foreach (var power in CombatState.Enemies.Select(c => c.GetPower<LibraPendulumPower>()).Where(p => p is not null))
            foreach (var player in CombatState.Players)
                power!._facing[player.Creature] = side;
    }
    internal static async Task KillPlayersAtExtreme(ICombatState combatState)
    {
        var power = combatState.Enemies.Select(c => c.GetPower<LibraPendulumPower>()).FirstOrDefault(p => p is not null);
        if (power is null || power.Left is not (0 or 10) && power.Right is not (0 or 10)) return;
        await LibraVisualController.PlayExtremeConfirmationAsync(combatState);
        foreach (var player in combatState.Players.Where(p => p.Creature.IsAlive))
            await CreatureCmd.Kill(player.Creature, true);
    }
    private void Set(int left, int right) { Left = Math.Clamp(left, 0, 10); Right = Math.Clamp(right, 0, 10); InvokeDisplayAmountChanged(); }
    private void Notify(
        LibraPresentationCause cause,
        int oldLeft,
        int oldRight,
        string? side = null,
        bool strong = false) =>
        PresentationChanged?.Invoke(new LibraPresentationEvent(
            cause,
            oldLeft,
            oldRight,
            Left,
            Right,
            side,
            strong));
}

public sealed class LibraImbalancePower : SakuraPowerModel
{
    protected override string IconFileName => "fourth_act/libra_imbalance.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override int DisplayAmount => -1;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Owner.IsAlive && player.Creature.IsAlive)
            await CardPileCmd.AddGeneratedCardToCombat(
                CombatState.CreateCard<Balance>(player), PileType.Hand, player, CardPilePosition.Random);
    }

}
