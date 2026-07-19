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

public class ClowLock() : ClowExtraEffectCard(1, CardType.Skill, CardRarity.Common, TargetType.None)
{
    public override SakuraElementSet Elements => SakuraElementSet.Fire;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Magic", 2), new EnergyVar(2)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var hand = CardPile.GetCards(Owner, PileType.Hand).Where(card => card != this).ToList();
        if (hand.Count == 0)
            return;

        var selected = (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 0, Math.Min(ReleasedMagic(), hand.Count))
            {
                Cancelable = true
            },
            card => hand.Contains(card),
            this)).ToList();
        if (selected.Count == 0)
            return;

        foreach (var card in selected)
            await CardCmd.Exhaust(choiceContext, card, false);

        var copy = CreateClone();
        await SakuraGeneratedCardLifecycle.AddGeneratedCardToCombat(
            copy,
            PileType.Discard,
            Owner,
            CardPilePosition.Bottom);

        await SakuraMagicCharge.GainMagic(choiceContext, Owner, selected.Count, this);
    }

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PlayCard(choiceContext, play);
        await PlayerCmd.GainEnergy(ReleasedValue("Energy"), Owner);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class SakuraLock() : SakuraFormCard(1, CardType.Skill, TargetType.None)
{
    public override SakuraElementSet Elements => SakuraElementSet.Fire;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await ApplyPower<ClassicLockSakuraPower>(choiceContext, Owner.Creature, 1);
}

