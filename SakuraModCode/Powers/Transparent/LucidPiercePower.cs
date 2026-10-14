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

public class LucidPiercePower : SakuraPowerModel
{
    private readonly List<CardModel> _cards = [];

    protected override string IconFileName => "lucid_pierce.png";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public static bool ShouldPierce(Creature? dealer, CardModel? cardSource) =>
        dealer is not null
        && cardSource is { Type: CardType.Attack }
        && cardSource.Owner?.Creature == dealer
        && (dealer.GetPower<LucidPierceAllPower>() is not null
            || dealer.GetPower<LucidPiercePower>()?._cards.Contains(cardSource) == true);

    public static async Task Mark(PlayerChoiceContext choiceContext, Creature owner, CardModel card, CardModel source)
    {
        if (owner.GetPower<LucidPiercePower>() is null)
            await PowerCmd.Apply<LucidPiercePower>(choiceContext, owner, 1, owner, source, false);
        owner.GetPower<LucidPiercePower>()?._cards.Add(card);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner.Side == side && participants.Contains(Owner))
            await PowerCmd.Remove(this);
    }
}

public class LucidPierceAllPower : SakuraPowerModel
{
    protected override string IconFileName => "lucid_pierce.png";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner.Side == side && participants.Contains(Owner))
            await PowerCmd.Remove(this);
    }
}
