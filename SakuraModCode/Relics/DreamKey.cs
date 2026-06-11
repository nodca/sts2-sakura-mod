using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Events;
using SakuraMod.SakuraModCode.Extensions;

namespace SakuraMod.SakuraModCode.Relics;

public class DreamKey : SakuraModRelic
{
    private static readonly SavedSpireField<DreamKey, bool> OpeningManifestCompleted =
        new(() => false, "SakuraMod_OpeningManifestCompleted");

    public override RelicRarity Rarity => RelicRarity.Starter;
    public override string PackedIconPath => "dream_key.png".RelicImagePath();
    protected override string PackedIconOutlinePath => "dream_key_outline.png".RelicImagePath();
    protected override string BigIconPath => "dream_key.png".BigRelicImagePath();

    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];

    public override RelicModel GetUpgradeReplacement() =>
        ModelDb.Relic<DreamKeyTrueForm>();

    private bool _openingManifestInProgress;

    public override Task AfterMapGenerated(ActMap map, int actIndex)
    {
        SakuraStarterEventReplacements.RemoveVanillaEventsFromAct(Owner?.RunState, actIndex);
        return Task.CompletedTask;
    }

    public override Task BeforeCombatStart()
    {
        OpeningManifestCompleted[this] = false;
        _openingManifestInProgress = false;
        return Task.CompletedTask;
    }

    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, CombatState combatState)
    {
        if (OpeningManifestCompleted[this] || _openingManifestInProgress || player != Owner)
            return;

        _openingManifestInProgress = true;
        try
        {
            var source = Owner.Deck.Cards.OfType<SakuraModCard>().FirstOrDefault();
            if (source is not null)
                await SakuraActions.Manifest(source, choiceContext, 1);

            OpeningManifestCompleted[this] = true;
        }
        finally
        {
            _openingManifestInProgress = false;
        }
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        OpeningManifestCompleted[this] = false;
        _openingManifestInProgress = false;
        return Task.CompletedTask;
    }
}
