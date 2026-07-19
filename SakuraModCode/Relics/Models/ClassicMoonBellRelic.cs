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

public class ClassicMoonBellRelic : SakuraRelicModel
{
    private const int TriggerCost = 2;
    private const int Heal = 1;
    private const int DeathPreventHealPercent = 30;

    private static readonly SavedAttachedState<ClassicMoonBellRelic, bool> Used =
        new("SakuraMod_ClassicMoonBellUsed", () => false);

    protected override string IconFileName => Used[this] ? "moon_bell_invalid.png" : "moon_bell.png";
    protected override string IconOutlineFileName => "moon_bell_outline.png";
    public override RelicRarity Rarity => RelicRarity.Rare;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("TriggerCost", TriggerCost),
        new HealVar(Heal),
        new DynamicVar("DeathPreventHealPercent", DeathPreventHealPercent)
    ];

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (Used[this] || play.Card?.Owner != Owner || play.Card.EnergyCost.Canonical < DynamicVars["TriggerCost"].IntValue)
            return;

        Flash();
        await CreatureCmd.Heal(Owner.Creature, DynamicVars.Heal.IntValue);
    }

    public override bool ShouldDie(Creature creature) =>
        creature != Owner?.Creature || Used[this];

    public override async Task AfterPreventingDeath(Creature creature)
    {
        if (creature != Owner?.Creature || Used[this])
            return;

        Used[this] = true;
        ApplyUsedPresentation();
        Flash();

        // Heal is the revive path; SetCurrentHp(0) would re-enter Kill while death is being prevented.
        await CreatureCmd.Heal(
            Owner.Creature,
            Math.Max(1, Owner.Creature.MaxHp * DynamicVars["DeathPreventHealPercent"].IntValue / 100));

        var combatState = Owner.Creature.CombatState
            ?? throw new InvalidOperationException("Moon Bell generated Turn requires an active combat.");
        var handCard = combatState.CreateCard<SpellTurn>(Owner);
        await CardPileCmd.AddGeneratedCardToCombat(handCard, PileType.Hand, Owner, CardPilePosition.Random);

        var deckCard = Owner.RunState.CreateCard<SpellTurn>(Owner);
        await CardPileCmd.Add(deckCard, PileType.Deck, CardPilePosition.Bottom, this, skipVisuals: true);
    }

    internal void RestoreSavedPresentation()
    {
        if (Used[this])
            ApplyUsedPresentation();
    }

    private void ApplyUsedPresentation()
    {
        Status = RelicStatus.Disabled;
        RelicIconChanged();
    }
}

