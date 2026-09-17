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

public class ClassicWindyPower : SakuraElementStatePower
{
    private const int DrawTrigger = 2;

    // The trigger count gates CardPileCmd.Draw, so it must be part of the
    // synced model state; a plain field desyncs silently and one machine draws
    // while the other does not (multiplayer checksum divergence).
    private static readonly SavedAttachedState<ClassicWindyPower, int> TriggerCounter =
        new("SakuraMod_ClassicWindyTriggerCounter", () => 0);

    protected override string IconFileName => "windy_power.png";
    internal override SakuraElement Element => SakuraElement.Wind;
    protected override Type PermanentPowerType => typeof(ClassicWindyPermanentPower);

    protected override async Task TriggerElement(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var counter = TriggerCounter[this] + 1;
        if (counter < DrawTrigger)
        {
            TriggerCounter[this] = counter;
            return;
        }

        TriggerCounter[this] = counter - DrawTrigger;
        SakuraElementStateVisuals.NotifyWindTriggered(Owner);
        await CardPileCmd.Draw(choiceContext, 1, Owner.Player!, false);
    }
}
