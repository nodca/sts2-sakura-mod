using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using SakuraMod.SakuraModCode.Extensions;
using STS2RitsuLib.Scaffolding.Content;

namespace SakuraMod.SakuraModCode.Relics;

public sealed class ClassicMonsterRelic : SakuraRelicModel
{
    private const int MaxHpGain = 6;
    private const int StrengthGain = 2;
    private const int DexterityLoss = 1;

    protected override string IconFileName => "monster.png";
    public override RelicRarity Rarity => RelicRarity.Rare;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("MaxHpGain", MaxHpGain),
        new PowerVar<StrengthPower>(StrengthGain),
        new PowerVar<DexterityPower>(DexterityLoss)
    ];

    public override async Task AfterObtained() =>
        await CreatureCmd.GainMaxHp(Owner.Creature, DynamicVars["MaxHpGain"].IntValue);

    public override async Task BeforeCombatStart()
    {
        var player = Owner ?? throw new InvalidOperationException("Monster relic has no owner.");
        var creature = player.Creature;
        var context = new MegaCrit.Sts2.Core.GameActions.Multiplayer.ThrowingPlayerChoiceContext();

        Flash();
        await PowerCmd.Apply<StrengthPower>(
            context,
            creature,
            DynamicVars["StrengthPower"].IntValue,
            creature,
            null,
            false);
        await PowerCmd.Apply<DexterityPower>(
            context,
            creature,
            -DynamicVars["DexterityPower"].IntValue,
            creature,
            null,
            false);
    }
}
