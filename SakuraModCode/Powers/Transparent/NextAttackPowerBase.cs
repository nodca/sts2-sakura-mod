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

public abstract class NextAttackPowerBase : SakuraPowerModel
{
    private CardModel? _activeAttack;

    protected bool IsActiveAttack(CardModel? card) =>
        Amount > 0 && card is not null && _activeAttack == card;

    public override Task BeforeCardPlayed(CardPlay play)
    {
        if (Amount > 0
            && _activeAttack is null
            && play.Card?.Owner?.Creature == Owner
            && play.Card.Type == CardType.Attack)
        {
            _activeAttack = play.Card;
            AfterActiveAttackSet(play.Card);
        }

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (_activeAttack == play.Card)
            await PowerCmd.Remove(this);
    }

    protected virtual void AfterActiveAttackSet(CardModel card)
    {
    }
}

