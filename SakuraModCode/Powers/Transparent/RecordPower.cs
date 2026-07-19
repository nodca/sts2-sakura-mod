using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.Extensions;
using STS2RitsuLib.Combat.HandSize;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace SakuraMod.SakuraModCode.Powers;

public class RecordPower : SakuraPowerModel
{
    private const string RecordedHpKey = "RecordedHp";
    private const string RecordedBlockKey = "RecordedBlock";

    protected override string IconFileName => "record.png";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override int DisplayAmount => RecordedHp;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(RecordedHpKey, 0),
        new DynamicVar(RecordedBlockKey, 0)
    ];

    private int RecordedHp => DynamicVars[RecordedHpKey].IntValue;
    private int RecordedBlock => DynamicVars[RecordedBlockKey].IntValue;

    public static async Task<RecordResult> RecordOrRestore(PlayerChoiceContext choiceContext, Creature owner, CardModel source)
    {
        if (owner.GetPower<RecordPower>() is { } record)
        {
            await record.RestoreRecordedValues();
            return RecordResult.Restored;
        }

        var power = await PowerCmd.Apply<RecordPower>(choiceContext, owner, 1, owner, source, false);
        power?.StoreCurrentValues(owner);
        return RecordResult.Recorded;
    }

    private void StoreCurrentValues(Creature creature)
    {
        DynamicVars[RecordedHpKey].BaseValue = Math.Max(0, creature.CurrentHp);
        DynamicVars[RecordedBlockKey].BaseValue = Math.Max(0, creature.Block);
        InvokeDisplayAmountChanged();
    }

    private async Task RestoreRecordedValues()
    {
        await SakuraCreatureState.RestoreHp(Owner, RecordedHp);
        SakuraCreatureState.RestoreBlock(Owner, RecordedBlock);
        await PowerCmd.Remove(this);
    }
}

