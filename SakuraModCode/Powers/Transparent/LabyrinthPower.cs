using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Cards.Visuals;
using SakuraMod.SakuraModCode.Character;
using STS2RitsuLib.Utils;

namespace SakuraMod.SakuraModCode.Powers;

public class LabyrinthPower : SakuraPowerModel
{
    private static readonly SavedAttachedState<LabyrinthPower, bool> EarthCardPlayed =
        new("SakuraMod_LabyrinthEarthCardPlayed", () => false);

    protected override string IconFileName => "labyrinth.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        foreach (var creature in Owner.CombatState!.Creatures.ToList())
            await ProtectCreature(creature);
        RefreshCardCosts();
        LabyrinthFieldBackgroundVisuals.Refresh(Owner.CombatState!);
    }

    public override Task AfterCreatureAddedToCombat(Creature creature) => ProtectCreature(creature);

    private async Task ProtectCreature(Creature creature)
    {
        if (creature.IsAlive && creature.GetPower<LabyrinthLostPower>() is null)
            await PowerCmd.Apply<LabyrinthLostPower>(new ThrowingPlayerChoiceContext(), creature, 1, Owner, null, false);
    }

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal currentCost, out decimal newCost)
    {
        newCost = currentCost;
        if (Amount <= 0 || EarthCardPlayed[this] || !IsOwnedEarthCard(card) || card.EnergyCost.CostsX || currentCost <= 0)
            return false;

        newCost = Math.Max(0, currentCost - 1);
        return true;
    }

    // Resources have already been paid here. Commit before resolution so nested plays cannot reuse the discount.
    public override Task BeforeCardPlayed(CardPlay play)
    {
        if (Amount > 0 && !EarthCardPlayed[this] && IsOwnedEarthCard(play.Card))
        {
            EarthCardPlayed[this] = true;
            RefreshCardCosts();
        }
        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == Owner.Side && participants.Contains(Owner))
        {
            EarthCardPlayed[this] = false;
            RefreshCardCosts();
        }
        return Task.CompletedTask;
    }

    public override async Task AfterRemoved(Creature oldOwner)
    {
        RefreshCardCosts();
        if (oldOwner.CombatState is not { } combat)
            return;
        LabyrinthFieldBackgroundVisuals.Refresh(combat);
        if (combat.Creatures.Any(c => c.GetPower<LabyrinthPower>() is not null))
            return;
        foreach (var creature in combat.Creatures.ToList())
            if (creature.GetPower<LabyrinthLostPower>() is { } lost)
                await PowerCmd.Remove(lost);
    }

    private bool IsOwnedEarthCard(CardModel card) =>
        card.IsMutable && card.Owner?.Creature == Owner && SakuraActions.HasElement(card, SakuraElement.Earth);

    private void RefreshCardCosts()
    {
        if (Owner.Player is not { } player)
            return;
        foreach (var card in CardPile.GetCards(player, PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust))
            if (IsOwnedEarthCard(card))
                card.InvokeEnergyCostChanged();
    }
}
