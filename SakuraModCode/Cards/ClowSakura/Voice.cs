using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
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
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.Relics;
using SakuraMod.SakuraModCode.Extensions;
using STS2RitsuLib.Utils;

namespace SakuraMod.SakuraModCode.Cards;

public class ClowVoice() : ClowExtraEffectCard(0, CardType.Skill, CardRarity.Common, TargetType.None)
{
    public override SakuraElementSet Elements => SakuraElementSet.Wind;
    public override bool HasTurnEndInHandEffect => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Ethereal, SakuraKeywords.Invisible];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new SakuraSourceBlockVar(4, ValueProp.Move), new DynamicVar("Magic", 1), new CardsVar(1)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await AddVoiceCopies(choiceContext, 1, PileType.Discard);

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await AddVoiceCopies(choiceContext, 3, PileType.Discard);

    protected override async Task OnTurnEndInHand(PlayerChoiceContext choiceContext) =>
        await CreatureCmd.GainBlock(Owner.Creature, ReleasedBlock(), ValueProp.Move, null, false);

    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (card == this)
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner, false);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2);
        DynamicVars["Magic"].UpgradeValueBy(1);
    }

    private async Task AddVoiceCopies(PlayerChoiceContext choiceContext, int count, PileType pile)
    {
        for (var i = 0; i < count; i++)
        {
            var copy = CreateClone();
            await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
                copy,
                pile,
                Owner,
                pile == PileType.Hand ? CardPilePosition.Random : CardPilePosition.Bottom);
        }
    }
}

public class SakuraVoice() : SakuraFormCard(1, CardType.Power, TargetType.None)
{
    public override SakuraElementSet Elements => SakuraElementSet.Wind;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Magic", 1)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await ApplyPower<ClassicVoicePower>(choiceContext, Owner.Creature, ReleasedMagic());
}

