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

public class ClassicReturnPower : SakuraPowerModel
{
    private readonly Dictionary<PowerModel, int> _recordedPowers = [];
    private int _recordedHp;
    private int _recordedBlock;

    protected override string IconFileName => "return.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        CaptureSnapshot();
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Owner.Player != player)
            return;

        if (Amount > 1)
        {
            await PowerCmd.ModifyAmount(choiceContext, this, -1, Owner, null, false);
            return;
        }

        await PowerCmd.Remove(this);
    }

    public override async Task AfterRemoved(Creature oldOwner)
    {
        await RemovePowersAddedAfterSnapshot(oldOwner);
        RestoreRecordedPowerAmounts(oldOwner);

        await SakuraCreatureState.RestoreHp(oldOwner, _recordedHp);
        SakuraCreatureState.RestoreBlock(oldOwner, _recordedBlock);
    }

    private void CaptureSnapshot()
    {
        _recordedPowers.Clear();
        foreach (var power in Owner.Powers.Where(power => power != this))
            _recordedPowers[power] = power.Amount;

        _recordedHp = Owner.CurrentHp;
        _recordedBlock = Owner.Block;
    }

    private async Task RemovePowersAddedAfterSnapshot(Creature creature)
    {
        while (creature.Powers.FirstOrDefault(power => !_recordedPowers.ContainsKey(power)) is { } power)
            await PowerCmd.Remove(power);
    }

    private void RestoreRecordedPowerAmounts(Creature creature)
    {
        var currentPowers = creature.Powers.ToHashSet();
        foreach (var (power, amount) in _recordedPowers)
        {
            if (currentPowers.Contains(power))
                power.SetAmount(amount);
        }
    }

}

