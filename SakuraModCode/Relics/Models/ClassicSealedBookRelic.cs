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

public class ClassicSealedBookRelic : SakuraRelicModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("ChargeGain", SakuraMagicCharge.NormalMagicChargeGain),
        new DynamicVar("PowerChargeGain", SakuraMagicCharge.PowerMagicChargeGain),
        new DynamicVar("ExtraEffectCost", SakuraMagicCharge.ExtraEffectCost)
    ];

    protected override string IconFileName => "sealed_book.png";
    public override RelicRarity Rarity => RelicRarity.Starter;
}

