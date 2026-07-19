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

public class ClowReturn() : ClowCard(1, CardType.Power, CardRarity.Rare, TargetType.None)
{
    private const int Duration = 2;
    private const int VoidCount = 2;

    public override SakuraElementSet Elements => SakuraElementSet.Water;
    public override IEnumerable<CardKeyword> CanonicalKeywords => IsUpgraded ? [CardKeyword.Retain] : [];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Magic", Duration), new DynamicVar("Voids", VoidCount)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (Owner.Creature.GetPower<ClassicReturnPower>() is { } existing)
        {
            existing.SetAmount(ReleasedMagic());
        }
        else
        {
            await ApplyPower<ClassicReturnPower>(choiceContext, Owner.Creature, ReleasedMagic());
        }

        if (!IsUpgraded)
        {
            for (var i = 0; i < DynamicVars["Voids"].IntValue; i++)
                await SakuraMagicCharge.AddVoidToDiscardPile(choiceContext, Owner);
        }
    }

    protected override void OnUpgrade() => AddKeywordIfMissing(CardKeyword.Retain);
}

public class SakuraReturn() : SakuraFormCard(0, CardType.Power, TargetType.None)
{
    public override SakuraElementSet Elements => SakuraElementSet.Water;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new SakuraReturnRechargeVar()];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var deckCard = Owner.Deck.Cards.OfType<SakuraReturn>().FirstOrDefault();
        if (deckCard is null)
            return;

        var missingHp = Math.Max(0, Owner.Creature.MaxHp - Owner.Creature.CurrentHp);
        if (missingHp > 0)
            await CreatureCmd.Heal(Owner.Creature, missingHp);

        Owner.GetRelic<ClassicSealedWandRelic>()?.AddReturnRecharge();
        await CardPileCmd.RemoveFromDeck(deckCard, showPreview: false);
    }
}

