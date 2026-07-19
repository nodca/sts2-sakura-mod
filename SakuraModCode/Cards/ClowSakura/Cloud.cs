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

public class ClowCloud() : ClowExtraEffectCard(1, CardType.Skill, CardRarity.Common, TargetType.None)
{
    public override bool GainsBlock => true;
    public override SakuraElementSet Elements => SakuraElementSet.Water;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new SakuraSourceBlockVar(5, ValueProp.Move)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await GainBlock(play, ReleasedBlock());

        var wateryCards = SakuraCloudEffects.CountWateryCards(CardPile.GetCards(Owner, PileType.Hand));
        for (var i = 0; i < wateryCards; i++)
            await GainBlock(play, ReleasedBlock());

        if (Owner.Creature.GetPower<ClassicWateryPower>()?.Amount > 0)
            await SakuraCloudEffects.AddRainToHand(Owner, choiceContext, freeForCombat: false);
    }

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PlayCard(choiceContext, play);
        await GainBlock(play, SakuraMagicCharge.CloudExtraBlock);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(2);
}

public class SakuraCloud() : SakuraFormCard(1, CardType.Skill, TargetType.None)
{
    public override bool GainsBlock => true;
    public override SakuraElementSet Elements => SakuraElementSet.Water;
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new SakuraSourceBlockVar(7, ValueProp.Move),
        new BlockVar("ExtraBlock", 3, ValueProp.Move)
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await GainBlock(play, ReleasedBlock());

        var wateryCards = SakuraCloudEffects.CountWateryCards(Owner.Deck.Cards);
        for (var i = 0; i < wateryCards; i++)
            await GainBlock(play, ReleasedValue("ExtraBlock"));

        await SakuraCloudEffects.AddRainToHand(Owner, choiceContext, freeForCombat: true);
    }
}

internal static class SakuraCloudEffects
{
    internal static int CountWateryCards(IEnumerable<CardModel> cards) =>
        cards.Count(static card => SakuraActions.HasElement(card, SakuraElement.Water));

    public static async Task AddRainToHand(Player owner, PlayerChoiceContext choiceContext, bool freeForCombat)
    {
        var combatState = owner.Creature.CombatState
            ?? throw new InvalidOperationException("Cloud-generated Rain requires an active combat.");
        var rain = combatState.CreateCard<ClowRain>(owner);
        if (freeForCombat)
        {
            rain.EnergyCost.SetThisCombat(0, reduceOnly: true);
            await SakuraGeneratedCardLifecycle.AddGeneratedCardToHand(rain, choiceContext);
            return;
        }

        await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
            rain,
            new GeneratedCardOptions { FreeThisTurn = true },
            choiceContext);
    }
}
