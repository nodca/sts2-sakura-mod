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

public class ClassicLightPower : SakuraLightPowerBase, IMaxHandSizeModifier
{
    private const int ExtraHandSize = 2;
    private bool _upgraded;
    private CardModel? _playedVoid;

    protected override string IconFileName => "light_power.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public bool IsUpgraded => _upgraded;

    public void MarkUpgraded()
    {
        if (_upgraded)
            return;

        _upgraded = true;
        InvokeDisplayAmountChanged();
    }

    public int ModifyMaxHandSize(Player player, int currentMaxHandSize) =>
        player.Creature == Owner ? currentMaxHandSize + ExtraHandSize : currentMaxHandSize;

    public int ModifyMaxHandSizeLate(Player player, int currentMaxHandSize) =>
        currentMaxHandSize;

    public override bool TryModifyKeywordsInCombat(CardModel card, ISet<CardKeyword> keywords)
    {
        if (!_upgraded || !IsOwnedVoid(card))
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
        _upgraded && IsOwnedVoid(card)
            ? (PileType.Exhaust, position)
            : (pileType, position);

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (_upgraded && IsOwnedVoid(play.Card))
            _playedVoid = play.Card;

        return Task.CompletedTask;
    }

    public override async Task AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)
    {
        if (card == _playedVoid)
        {
            _playedVoid = null;
            await CardPileCmd.Draw(choiceContext, 1, Owner.Player!, false);
        }
    }

    private bool IsOwnedVoid(CardModel? card) =>
        card is MegaCrit.Sts2.Core.Models.Cards.Void && card.Owner?.Creature == Owner;
}

