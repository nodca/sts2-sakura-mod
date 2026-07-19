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

public class ClowLibra() : ClowExtraEffectCard(0, CardType.Skill, CardRarity.Common, TargetType.None)
{
    private const int ExtraBlock = 12;

    public override bool GainsBlock => true;
    public override SakuraElementSet Elements => SakuraElementSet.Fire;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new SakuraSourceBlockVar(4, ValueProp.Move), new DynamicVar("Magic", 3)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (Owner.Creature.Block == 0)
        {
            await SakuraMagicCharge.SpendUpToMagic(choiceContext, Owner, ReleasedMagic());
            await GainBlock(play, ReleasedBlock());
        }

        await GainBlock(play, ReleasedBlock());
    }

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PlayCard(choiceContext, play);
        await GainBlock(play, ExtraBlock);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2);
        DynamicVars["Magic"].UpgradeValueBy(-1);
    }
}

public class SakuraLibra() : SakuraFormCard(0, CardType.Skill, TargetType.None)
{
    private const int ChargePerEnergy = 3;

    public override bool GainsBlock => true;
    public override SakuraElementSet Elements => SakuraElementSet.Fire;
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new SakuraSourceBlockVar(3, ValueProp.Move),
        new DynamicVar("Magic", ChargePerEnergy),
        new EnergyVar(1)
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var charge = await SakuraMagicCharge.SpendAllMagic(choiceContext, Owner);
        for (var i = 0; i < charge; i++)
            await GainBlock(play, ReleasedBlock());

        var energy = EnergyFromCharge(charge) * ReleasedValue("Energy");
        if (energy > 0)
            await PlayerCmd.GainEnergy(energy, Owner);
    }

    internal static int EnergyFromCharge(int charge) =>
        Math.Max(0, charge) / ChargePerEnergy;
}
