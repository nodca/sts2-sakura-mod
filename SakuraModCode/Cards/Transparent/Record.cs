using MegaCrit.Sts2.Core;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.Extensions;
using STS2RitsuLib.Combat.HandSize;

namespace SakuraMod.SakuraModCode.Cards;

public class Record() : TransparentExtraEffectCard(2, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    private bool _restoredDuringCurrentPlay;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Water];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1), new EnergyVar(1)];

    public override (PileType, CardPilePosition) ModifyCardPlayResultPileTypeAndPosition(
        CardModel card,
        bool isAutoPlay,
        ResourceInfo resources,
        PileType pileType,
        CardPilePosition position)
    {
        if (card == this)
            _restoredDuringCurrentPlay = false;

        if (card == this && Owner.Creature.GetPower<RecordPower>() is not null)
            return (PileType.Exhaust, CardPilePosition.Bottom);

        return (pileType, position);
    }

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        if (_restoredDuringCurrentPlay)
            return;

        var result = await RecordPower.RecordOrRestore(choiceContext, Owner.Creature, this);
        if (result == RecordResult.Restored)
            _restoredDuringCurrentPlay = true;

        if (!activation.IsActive)
            return;

        if (result == RecordResult.Recorded)
        {
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner, false);
            return;
        }

        await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner);
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await base.AfterCardPlayed(choiceContext, play);

        if (play.Card != this || play.PlayIndex < play.PlayCount - 1)
            return;

        var shouldExhaust = _restoredDuringCurrentPlay && play.ResultPile != PileType.Exhaust;
        _restoredDuringCurrentPlay = false;
        if (shouldExhaust && Pile?.Type == PileType.Play)
            await CardCmd.Exhaust(choiceContext, this);
    }

    protected override void OnUpgrade() => AddKeywordIfMissing(CardKeyword.Retain);
}





