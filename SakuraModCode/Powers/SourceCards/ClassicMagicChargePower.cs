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

public class ClassicMagicChargePower : SakuraPowerModel
{
    private static readonly SavedAttachedState<ClassicMagicChargePower, int> OpportunityToken =
        new("SakuraMod_ClassicMagicChargeOpportunityToken", () => 0);

    protected override string IconFileName => "magick_charge_power.png";
    protected override bool IsVisibleInternal => false;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    internal event System.Action? ProjectionChanged;

    internal int ArmedOpportunityGeneration => Math.Max(0, OpportunityToken[this]);

    internal void ArmNextOpportunity()
    {
        var current = OpportunityToken[this];
        var magnitude = current == int.MinValue ? int.MaxValue : Math.Abs(current);
        OpportunityToken[this] = magnitude == int.MaxValue ? 1 : magnitude + 1;
    }

    internal void ExpireOpportunity()
    {
        var current = OpportunityToken[this];
        if (current > 0)
            OpportunityToken[this] = -current;
    }

    internal bool TryConsumeOpportunity(int generation)
    {
        if (generation <= 0 || OpportunityToken[this] != generation)
            return false;

        OpportunityToken[this] = -generation;
        ProjectionChanged?.Invoke();
        return true;
    }

    internal void NotifyProjectionChanged() => ProjectionChanged?.Invoke();
}

