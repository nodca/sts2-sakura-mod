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

public abstract class SakuraTrackedCostReductionPower : SakuraPowerModel
{
    private readonly HashSet<CardModel> _targets = [];

    protected override bool IsVisibleInternal => false;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public void AddTarget(CardModel card)
    {
        _targets.Add(card);
    }

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal currentCost, out decimal newCost)
    {
        if (_targets.Contains(card) && currentCost > 0)
        {
            newCost = Math.Max(0, currentCost - Math.Max(1, Amount));
            return true;
        }

        newCost = currentCost;
        return false;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (play.Card is not null)
            _targets.Remove(play.Card);

        await RemoveIfEmpty();
    }

    protected void PruneDetachedTargets()
    {
        _targets.RemoveWhere(card => card.Pile?.IsCombatPile != true);
    }

    protected async Task RemoveIfEmpty()
    {
        if (_targets.Count == 0)
            await PowerCmd.Remove(this);
    }
}

