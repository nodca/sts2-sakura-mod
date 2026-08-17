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

public class ClowSweet() : ClowExtraEffectCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.None)
{
    public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;
    public override SakuraElementSet Elements => SakuraElementSet.Fire;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new HealVar(5)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var targets = CombatState!.Players
            .Where(static player => player.Creature.IsAlive)
            .Select(static player => player.Creature)
            .ToList();
        foreach (var target in targets)
            await CreatureCmd.Heal(target, ReleasedValue("Heal"));
    }

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await PowerCmd.Apply<RegenPower>(
            choiceContext,
            CombatState!.Players
                .Where(static player => player.Creature.IsAlive)
                .Select(static player => player.Creature)
                .ToList(),
            ReleasedValue("Heal"),
            Owner.Creature,
            this,
            silent: false);

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class SakuraSweet() : SakuraFormCard(2, CardType.Power, TargetType.None)
{
    public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;
    public override SakuraElementSet Elements => SakuraElementSet.Fire;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Magic", 10)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await PowerCmd.Apply<ClassicSweetPower>(
            choiceContext,
            CombatState!.Players
                .Where(static player => player.Creature.IsAlive)
                .Select(static player => player.Creature)
                .ToList(),
            ReleasedMagic(),
            Owner.Creature,
            this,
            silent: false);
}
