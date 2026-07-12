using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Models;

namespace SakuraMod.SakuraModCode.Cards;

public sealed class SakuraDrawCountHook : HookedSingletonModel
{
    private static readonly ConditionalWeakTable<ICombatState, Dictionary<Player, int>> DrawCountsByCombat = new();

    public SakuraDrawCountHook() : base(HookType.Combat)
    {
    }

    public override Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        DrawCountsByCombat.GetValue(combatState, static _ => [])[player] = 0;
        return Task.CompletedTask;
    }

    public override Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (card.Owner is not { } owner || owner.Creature.CombatState is not { } combatState)
            return Task.CompletedTask;

        var drawCounts = DrawCountsByCombat.GetValue(combatState, static _ => []);
        drawCounts[owner] = DrawCountThisTurn(combatState, owner) + 1;
        return Task.CompletedTask;
    }

    public static int DrawCountThisTurn(CardModel card) =>
        card.Owner?.Creature.CombatState is { } combatState && card.Owner is { } owner
            ? DrawCountThisTurn(combatState, owner)
            : 0;

    private static int DrawCountThisTurn(ICombatState combatState, Player owner) =>
        DrawCountsByCombat.TryGetValue(combatState, out var drawCounts)
        && drawCounts.TryGetValue(owner, out var count)
            ? count
            : 0;
}
