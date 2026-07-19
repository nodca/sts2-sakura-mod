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

public class ClowSleep() : ClowExtraEffectCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override SakuraElementSet Elements => SakuraElementSet.Wind;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Magic", 2)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await ApplySleep(choiceContext, RequiredTarget(play));

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await SakuraThroughResolution.WithPropagationSuppressed(async () =>
        {
            foreach (var enemy in CombatState!.HittableEnemies.ToList())
                await ApplySleep(choiceContext, enemy);
        });
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);

    private async Task ApplySleep(PlayerChoiceContext choiceContext, Creature target)
    {
        await ApplyPower<ClassicSleepPower>(choiceContext, target, ReleasedMagic());
    }
}

public class SakuraSleep() : SakuraFormCard(1, CardType.Skill, TargetType.AnyEnemy)
{
    public override SakuraElementSet Elements => SakuraElementSet.Wind;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Magic", 4)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = RequiredTarget(play);
        await ApplyPower<ClassicSleepPower>(choiceContext, target, ReleasedMagic());
    }
}
