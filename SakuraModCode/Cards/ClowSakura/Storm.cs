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

public class ClowStorm() : ClowExtraEffectCard(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    private const int Hits = 5;

    public override SakuraElementSet Elements => SakuraElementSet.Wind;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new SakuraSourceDamageVar(4, ValueProp.Move), new DynamicVar("Magic", Hits)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await SakuraMagicCharge.AddVoidToDiscardPile(choiceContext, Owner);
        var target = RequiredTarget(play);
        await DealDamage(choiceContext, target, ReleasedDamage(), hitCount: ReleasedMagic());
    }

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await SakuraMagicCharge.AddVoidToDiscardPile(choiceContext, Owner);
        await DealDamageToEnemies(choiceContext, CombatState!.HittableEnemies, ReleasedDamage(), hitCount: ReleasedMagic());
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(2);
}

public class SakuraStorm() : SakuraFormCard(1, CardType.Attack, TargetType.None)
{
    private const int MaxDamageOffset = 8;

    public override SakuraElementSet Elements => SakuraElementSet.Wind;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new SakuraSourceDamageVar(1, ValueProp.Move), new DynamicVar("MaxDamage", 9), new DynamicVar("Magic", 7)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await using var attack = await AttackCommand.CreateContextAsync(CombatState!, choiceContext, this);
        for (var i = 0; i < ReleasedMagic(); i++)
        {
            var target = Owner.RunState.Rng.CombatCardSelection.NextItem(CombatState!.HittableEnemies.ToList());
            if (target is null)
                return;

            var amount = Owner.RunState.Rng.CombatCardSelection.NextInt(ReleasedDamage(), ReleasedDamage() + MaxDamageOffset + 1);
            await DealDamageHit(attack, choiceContext, target, amount);
        }
    }
}

