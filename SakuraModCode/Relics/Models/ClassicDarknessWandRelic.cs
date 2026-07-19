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

public class ClassicDarknessWandRelic : SakuraRelicModel
{
    public const int WandChargeGain = 2;

    private const int MaxEnergyGain = 1;
    private const int EnemyStrength = 1;

    protected override string IconFileName => "darkness_wand.png";
    public override RelicRarity Rarity => RelicRarity.Ancient;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(MaxEnergyGain),
        new PowerVar<StrengthPower>(EnemyStrength),
        new DynamicVar("WandChargeGain", WandChargeGain)
    ];

    public override decimal ModifyMaxEnergy(Player player, decimal amount) =>
        player == Owner ? amount + DynamicVars.Energy.IntValue : amount;

    public override async Task BeforeCombatStart()
    {
        var enemies = Owner.Creature.CombatState?.GetOpponentsOf(Owner.Creature)
            .Where(static creature => creature.IsAlive)
            .ToList() ?? [];
        if (enemies.Count == 0)
            return;

        Flash();
        await PowerCmd.Apply<StrengthPower>(
            new ThrowingPlayerChoiceContext(),
            enemies,
            DynamicVars["StrengthPower"].IntValue,
            Owner.Creature,
            null,
            false);
    }
}

