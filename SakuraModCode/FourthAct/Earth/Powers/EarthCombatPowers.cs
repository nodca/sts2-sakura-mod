using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;

namespace SakuraMod.SakuraModCode.FourthAct.Earth.Powers;

public static class EarthCombatRules
{
    private static readonly HashSet<CardModel> BuriedCards = [];

    public static bool IsBuried(CardModel card) => BuriedCards.Contains(card);
    public static void MarkBuried(CardModel card) => BuriedCards.Add(card);
    public static void UnmarkBuried(CardModel card) => BuriedCards.Remove(card);
    public static void ClearBuried() => BuriedCards.Clear();
    public static bool HasAnyBuried(Player player)
    {
        var discard = CardPile.Get(PileType.Discard, player);
        return discard?.Cards.Any(IsBuried) == true;
    }
}

[HarmonyPatch(typeof(CardPileCmd), nameof(CardPileCmd.Shuffle))]
public static class EarthBuriedCardShufflePatch
{
    public static void Prefix(Player player, out List<CardModel> __state)
    {
        __state = [];
        var discard = CardPile.Get(PileType.Discard, player);
        if (discard is null) return;

        var buried = discard.Cards.Where(EarthCombatRules.IsBuried).ToList();
        foreach (var card in buried)
        {
            discard.RemoveInternal(card, true);
            __state.Add(card);
        }
    }

    public static void Postfix(Player player, List<CardModel> __state)
    {
        if (__state.Count == 0) return;
        var discard = CardPile.Get(PileType.Discard, player);
        if (discard is null) return;

        foreach (var card in __state)
        {
            discard.AddInternal(card, discard.Cards.Count, true);
            EarthCombatRules.UnmarkBuried(card);
        }
        discard.InvokeContentsChanged();
    }
}

public sealed class EarthSovereigntyPower : SakuraPowerModel
{
    protected override string IconFileName => "fourth_act/earth_sovereignty.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override int DisplayAmount => -1;

    public override bool TryModifyPowerAmountReceived(PowerModel power, Creature target, decimal amount, Creature? applier, out decimal modified)
    {
        if (target.IsPlayer && amount > 0 && power is ClassicEarthyPower or ClassicEarthyPermanentPower)
        {
            modified = 0;
            Flash();
            SakuraElementStateHud.NotifyPrevented(target.Player, SakuraElementSet.Earth);
            return true;
        }
        modified = amount;
        return false;
    }
}

public sealed class ShadowEchoPower : SakuraPowerModel
{
    private readonly Dictionary<Creature, CardType> _currentTurnCard = [];
    private readonly Dictionary<Creature, CardType?> _lastTurnCard = [];

    protected override string IconFileName => "fourth_act/shadow_echo.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override int DisplayAmount => -1;

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (play.Card.Owner?.Creature is { } playerCreature)
        {
            _currentTurnCard[playerCreature] = play.Card.Type;
        }
        return Task.CompletedTask;
    }

    public override Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player)
        {
            foreach (var participant in participants.Where(c => c.IsPlayer))
            {
                if (_currentTurnCard.TryGetValue(participant, out var cardType))
                {
                    _lastTurnCard[participant] = cardType;
                }
                else
                {
                    _lastTurnCard[participant] = null;
                }
            }
            _currentTurnCard.Clear();
        }
        return Task.CompletedTask;
    }

    public CardType? GetLastCardType(Creature player) =>
        _lastTurnCard.GetValueOrDefault(player);
}

public sealed class WoodRootedPower : SakuraPowerModel
{
    protected override string IconFileName => "fourth_act/wood_rooted.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override int DisplayAmount
    {
        get
        {
            if (Owner.CombatState is null) return 0;
            var players = Owner.CombatState.Players.Where(p => p.Creature.IsAlive).ToList();
            return players.Count == 0 ? 0 : players.Max(p => CardPile.GetCards(p, PileType.Exhaust).Count());
        }
    }

    public static int GetRootedCount(Player player) =>
        CardPile.GetCards(player, PileType.Exhaust).Count();

    public override Task AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)
    {
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }

    public override Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }
}

public sealed class EarthySedimentPower : SakuraPowerModel
{
    private readonly Dictionary<Creature, int> _currentTurnSediment = [];
    private readonly Dictionary<Creature, int> _lastTurnSediment = [];

    protected override string IconFileName => "fourth_act/earthy_sediment.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override int DisplayAmount => _lastTurnSediment.Values.DefaultIfEmpty(0).Max();

    public int GetSediment(Creature player) => _lastTurnSediment.GetValueOrDefault(player, 0);

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (play.Card.Owner?.Creature is { } playerCreature)
        {
            if (!play.Card.Keywords.Contains(CardKeyword.Exhaust) && !play.Card.IsTemporary())
            {
                _currentTurnSediment[playerCreature] = _currentTurnSediment.GetValueOrDefault(playerCreature) + 1;
            }
        }
        return Task.CompletedTask;
    }

    public override Task AfterCardDiscarded(PlayerChoiceContext choiceContext, CardModel card)
    {
        if (card.Owner?.Creature is { } playerCreature)
        {
            _currentTurnSediment[playerCreature] = _currentTurnSediment.GetValueOrDefault(playerCreature) + 1;
        }
        return Task.CompletedTask;
    }

    public override Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? clonedBy)
    {
        if (oldPileType == PileType.Discard && EarthCombatRules.IsBuried(card))
        {
            EarthCombatRules.UnmarkBuried(card);
        }
        return Task.CompletedTask;
    }

    public override Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature, bool wasRemovalPrevented, float deathAnimLength)
    {
        if (creature == Owner)
        {
            EarthCombatRules.ClearBuried();
        }
        return Task.CompletedTask;
    }

    public override Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player)
        {
            foreach (var p in participants.Where(c => c.IsPlayer))
            {
                _lastTurnSediment[p] = _currentTurnSediment.GetValueOrDefault(p, 0);
            }
            _currentTurnSediment.Clear();
            InvokeDisplayAmountChanged();
        }
        return Task.CompletedTask;
    }
}
