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

public class Gravitation() : TransparentExtraEffectCard(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Earth];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        SakuraCardPlayVfx.PlayGravitation(CombatState!.HittableEnemies);
        var power = await PowerCmd.Apply<GravitationHoldPower>(
            choiceContext,
            Owner.Creature,
            1,
            Owner.Creature,
            this,
            false);
        power?.ExcludeSource(this);
        if (activation.IsActive)
            await ApplyExtraEffect(choiceContext, play);
    }

    private async Task ApplyExtraEffect(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await ChooseFromPileToHand(choiceContext, PileType.Discard);
        await ChooseFromPileToHand(choiceContext, PileType.Draw);
    }

    protected override void OnUpgrade() => AddKeywordIfMissing(CardKeyword.Retain);

    private async Task ChooseFromPileToHand(PlayerChoiceContext choiceContext, PileType pileType)
    {
        var card = await SakuraActions.SelectFromCards(
            this,
            choiceContext,
            CardPile.Get(pileType, Owner)!.Cards,
            cancelable: false);
        if (card is not null)
            await SakuraActions.MoveExistingCardToHand(this, card);
    }
}

