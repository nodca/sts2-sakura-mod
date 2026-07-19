using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Relics;
using SakuraMod.SakuraModCode.Extensions;
using SakuraMod.SakuraModCode.Powers;
using STS2RitsuLib.Combat.HandSize;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;
using STS2RitsuLib.Utils;

namespace SakuraMod.SakuraModCode.Powers;

public class ClassicLightSakuraPower : SakuraLightPowerBase
{
    protected override string IconFileName => "light_power.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool TryModifyKeywordsInCombat(CardModel card, ISet<CardKeyword> keywords)
    {
        if (!IsOwnedStatusOrCurse(card))
            return false;

        var changed = keywords.Remove(CardKeyword.Unplayable);
        changed |= keywords.Add(CardKeyword.Exhaust);
        return changed;
    }

    public override (PileType, CardPilePosition) ModifyCardPlayResultPileTypeAndPosition(
        CardModel card,
        bool isAutoPlay,
        ResourceInfo resources,
        PileType pileType,
        CardPilePosition position) =>
        IsOwnedStatusOrCurse(card)
            ? (PileType.Exhaust, position)
            : (pileType, position);

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (!IsOwnedStatusOrCurse(play.Card) || Owner.Player is not { } player)
            return;

        await CardPileCmd.Draw(choiceContext, 1, player, false);
        await CreatureCmd.Heal(Owner, 1);
    }

    internal static bool IsStatusOrCurse(CardType type) =>
        type is CardType.Status or CardType.Curse;

    private bool IsOwnedStatusOrCurse(CardModel? card) =>
        card?.Owner?.Creature == Owner && IsStatusOrCurse(card.Type);
}

