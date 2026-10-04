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

public class ClowSnow() : ClowExtraEffectCard(2, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
{
    private const int ExtraDamage = 10;

    public override SakuraElementSet Elements => SakuraElementSet.Water;
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new SakuraCombatHistoryDamageVar(4, ValueProp.Move, SakuraSnowRules.PlayedWateryCards),
        new SakuraSourceDamageVar(SakuraSnowRules.PerCardDamageVar, 4, ValueProp.Move),
        new SakuraCombatHistoryCountVar(SakuraSnowRules.PlayedWateryCards),
        new PowerVar<SakuraFrostbitePower>(2),
        new DynamicVar("ExtraDamage", ExtraDamage)
    ];

    protected override Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        SnowBlizzardVfx.PlayOrResolveAsync(
            this,
            Owner.Creature,
            CombatState!.HittableEnemies.ToList(),
            async cues => await SakuraSnowRules.ApplyFrostbite(
                choiceContext,
                this,
                await ResolveSnowMechanics(choiceContext, cues)));

    protected override Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        SnowBlizzardVfx.PlayOrResolveAsync(
            this,
            Owner.Creature,
            CombatState!.HittableEnemies.ToList(),
            async cues =>
            {
                var receivers = await ResolveSnowMechanics(choiceContext, cues);
                cues.Finale();
                receivers.AddRange(SakuraSnowRules.FrostbiteReceivers(
                    await DealDamageToEnemies(choiceContext, CombatState!.HittableEnemies.ToList(), ExtraDamage)));
                await SakuraSnowRules.ApplyFrostbite(choiceContext, this, receivers);
            });

    private async Task<List<Creature>> ResolveSnowMechanics(
        PlayerChoiceContext choiceContext,
        SnowBlizzardVfx.Cues cues)
    {
        var receivers = new List<Creature>();
        var count = SakuraSnowRules.PlayedWateryCards(this);
        for (var i = 0; i < count; i++)
        {
            var target = Owner.RunState.Rng.CombatCardSelection.NextItem(CombatState!.HittableEnemies.ToList());
            if (target is null)
                break;

            cues.Impact(target);
            receivers.AddRange(SakuraSnowRules.FrostbiteReceivers(
                await DealDamage(choiceContext, target, SnowDamage())));
        }
        return receivers;
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2);
        DynamicVars[SakuraSnowRules.PerCardDamageVar].UpgradeValueBy(2);
    }

    private int SnowDamage() => ReleasedValue(SakuraSnowRules.PerCardDamageVar);
}

public class SakuraSnow() : SakuraFormCard(1, CardType.Attack, TargetType.AllEnemies)
{
    public override SakuraElementSet Elements => SakuraElementSet.Water;
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new SakuraCombatHistoryDamageVar(5, ValueProp.Move, SakuraSnowRules.PlayedWateryCards),
        new SakuraSourceDamageVar(SakuraSnowRules.PerCardDamageVar, 5, ValueProp.Move),
        new SakuraCombatHistoryCountVar(SakuraSnowRules.PlayedWateryCards),
        new PowerVar<SakuraFrostbitePower>(2)
    ];

    protected override Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        SnowBlizzardVfx.PlayOrResolveAsync(
            this,
            Owner.Creature,
            CombatState!.HittableEnemies.ToList(),
            async cues =>
            {
                var receivers = new List<Creature>();
                var count = SakuraSnowRules.PlayedWateryCards(this);
                for (var i = 0; i < count; i++)
                {
                    // One beat strikes the whole enemy line at once, so every
                    // snapshot target takes a dart before the shared attack lands.
                    var targets = CombatState!.HittableEnemies.ToList();
                    foreach (var target in targets)
                        cues.Impact(target);
                    receivers.AddRange(SakuraSnowRules.FrostbiteReceivers(
                        await DealDamageToEnemies(choiceContext, targets, SnowDamage())));
                }
                await SakuraSnowRules.ApplyFrostbite(choiceContext, this, receivers);
            });

    private int SnowDamage() => ReleasedValue(SakuraSnowRules.PerCardDamageVar);
}
