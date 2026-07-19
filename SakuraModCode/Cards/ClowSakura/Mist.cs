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

public class ClowMist() : ClowExtraEffectCard(1, CardType.Skill, CardRarity.Common, TargetType.None)
{
    public override SakuraElementSet Elements => SakuraElementSet.Earth;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<PoisonPower>(4)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await ApplyPowerToEnemies<PoisonPower>(choiceContext, ReleasedValue("PoisonPower"));

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        foreach (var enemy in CombatState!.HittableEnemies)
        {
            await CreatureCmd.LoseBlock(enemy, enemy.Block);
            foreach (var power in enemy.Powers.Where(static power => power is ArtifactPower or ThornsPower or BufferPower or IntangiblePower or BarricadePower).ToList())
                await PowerCmd.Remove(power);
        }

        await PlayCard(choiceContext, play);
    }

    protected override void OnUpgrade() => DynamicVars.Poison.UpgradeValueBy(2);
}

public class SakuraMist() : SakuraFormCard(1, CardType.Power, TargetType.None)
{
    public override SakuraElementSet Elements => SakuraElementSet.Earth;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<NoxiousFumesPower>(6)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        foreach (var enemy in CombatState!.HittableEnemies)
            await CreatureCmd.LoseBlock(enemy, enemy.Block);

        await ApplyPower<NoxiousFumesPower>(choiceContext, Owner.Creature, ReleasedValue("NoxiousFumesPower"));
    }
}

