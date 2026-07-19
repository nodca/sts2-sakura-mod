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

public class ClowDream() : ClowExtraEffectCard(1, CardType.Skill, CardRarity.Common, TargetType.None)
{
    private const int Cards = 1;
    private const int ExtraCards = 1;

    public override SakuraElementSet Elements => SakuraElementSet.Wind;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(Cards), new DynamicVar("ExtraCards", ExtraCards)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await AddDreamCards(ReleasedValue("Cards"));

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await AddDreamCards(ReleasedValue("Cards") + DynamicVars["ExtraCards"].IntValue);

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);

    private async Task AddDreamCards(int count)
    {
        for (var i = 0; i < count; i++)
        {
            var card = SakuraSourceCardRules.CreateRandomDreamClowCard(Owner);
            SakuraMagicCharge.SetFreeForRestOfTurn(card);
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner, CardPilePosition.Random);
        }
    }
}

public class SakuraDream() : SakuraFormCard(0, CardType.Skill, TargetType.None)
{
    public override SakuraElementSet Elements => SakuraElementSet.Wind;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (Owner.Creature.GetPower<ClassicDreamPower>() is { } existing)
        {
            await existing.ConvertCurrentHand();
            return;
        }

        await ApplyPower<ClassicDreamPower>(choiceContext, Owner.Creature, 1);
    }
}

