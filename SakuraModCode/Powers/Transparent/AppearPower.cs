using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using SakuraMod.SakuraModCode.Cards;
using STS2RitsuLib.Utils;

namespace SakuraMod.SakuraModCode.Powers;

public class AppearPower : SakuraPowerModel
{
    internal const int WideManifestChoiceCount = 5;

    // Gates how many choices every Manifest rolls, so it must be checksummed.
    private static readonly SavedAttachedState<AppearPower, bool> WidensManifest =
        new("SakuraMod_AppearPowerWidensManifest", () => false);

    protected override string IconFileName => "appear.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override string SmartDescriptionLocKey =>
        WidensManifest[this] ? Id.Entry + ".wideSmartDescription" : base.SmartDescriptionLocKey;

    internal void MarkUpgraded()
    {
        if (WidensManifest[this])
            return;

        WidensManifest[this] = true;
        InvokeDisplayAmountChanged();
    }

    internal static int ManifestChoiceCount(Player owner, int choiceCount) =>
        owner.Creature.GetPower<AppearPower>() is { } power && WidensManifest[power]
            ? Math.Max(choiceCount, WideManifestChoiceCount)
            : choiceCount;

    // Like Infinite Blades, the turn-start card arrives before the hand is drawn.
    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        if (player.Creature != Owner)
            return;

        Flash();
        await SakuraManifestLoop.Manifest(player, choiceContext, Amount);
    }
}
