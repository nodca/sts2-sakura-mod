using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.Extensions;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;
using CoreVoid = MegaCrit.Sts2.Core.Models.Cards.Void;

namespace SakuraMod.SakuraModCode.Relics;

public class ClassicTomoyosHeartRelic : SakuraRelicModel
{
    private const int Trigger = 4;
    private int _exhaustedCards;

    protected override string IconFileName => "tomoyos_heart.png";
    public override RelicRarity Rarity => RelicRarity.Uncommon;
    public override bool ShowCounter => true;
    public override int DisplayAmount => _exhaustedCards;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Trigger", Trigger),
        new CardsVar(1)
    ];

    public override async Task AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)
    {
        if (card.Owner != Owner)
            return;

        Flash();
        SetExhaustedCards(_exhaustedCards + 1);
        if (_exhaustedCards < DynamicVars["Trigger"].IntValue)
            return;

        SetExhaustedCards(_exhaustedCards - DynamicVars["Trigger"].IntValue);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner, false);
    }

    private void SetExhaustedCards(int amount)
    {
        _exhaustedCards = amount;
        InvokeDisplayAmountChanged();
    }
}

