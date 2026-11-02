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

public class Mirror() : TransparentExtraEffectCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Water];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new RepeatVar(1), new RepeatVar("ExtraRepeat", 1)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        var candidates = CardPile.GetCards(Owner, PileType.Hand).Where(CanGainReplay).ToList();
        if (candidates.Count == 0)
            return;

        var card = Owner.RunState.Rng.CombatCardSelection.NextItem(candidates);
        if (card is null)
            return;

        var amount = DynamicVars["Repeat"].IntValue;
        if (activation.IsActive)
            amount += DynamicVars["ExtraRepeat"].IntValue;
        card.BaseReplayCount += amount;
        CardCmd.Preview(card);
    }

    // Mirrors native Hidden Gem's pool, but from Hand; energy is deliberately not checked.
    internal bool CanGainReplay(CardModel card) =>
        card != this
        && card.Type is not (CardType.Status or CardType.Curse)
        && !card.Keywords.Contains(CardKeyword.Unplayable)
        && card.GetEnchantedReplayCount() < 1
        && SakuraSourceCardRules.CanBeTargetedByClearCardEffects(card);

    protected override void OnUpgrade() => DynamicVars["Repeat"].UpgradeValueBy(1);
}
