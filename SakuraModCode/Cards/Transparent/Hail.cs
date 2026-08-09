using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Extensions;
using SakuraMod.SakuraModCode.Powers;
using STS2RitsuLib.Cards.DynamicVars;

namespace SakuraMod.SakuraModCode.Cards;

public class Hail() : TransparentExtraEffectCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
{
    private const int BaseHits = 2;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Water];
    internal override IEnumerable<CardKeyword> ReferencedKeywords => [SakuraKeywords.Frostbite];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(3, ValueProp.Move),
        new PowerVar<SakuraFrostbitePower>(1)
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        await using var attack = await AttackCommand.CreateContextAsync(CombatState!, choiceContext, this);
        var frostbite = DynamicVars["SakuraFrostbitePower"].IntValue + (activation.IsActive ? 1 : 0);
        // Snapshot the hittable set once. The VFX session needs the same list the
        // hit loop walks, and ToList() is an immediate copy taken before any damage
        // resolves, so hoisting it cannot change which enemies are struck.
        var targets = CombatState!.HittableEnemies.ToList();
        var iceVfx = HailIceShardVfx.TryCreate(targets, Owner.Creature);
        try
        {
            if (iceVfx is not null)
                await iceVfx.PlayPrelude(this, Owner.Creature);
            foreach (var target in targets)
            {
                for (var i = 0; i < BaseHits && target.IsAlive; i++)
                    await Hit(choiceContext, attack, target, iceVfx);

                if (target.IsAlive)
                    await PowerCmd.Apply<SakuraFrostbitePower>(choiceContext, target, frostbite, Owner.Creature, this, false);
            }
        }
        finally
        {
            iceVfx?.FadeAndDispose();
        }
    }

    private async Task Hit(
        PlayerChoiceContext choiceContext,
        AttackContext attack,
        Creature target,
        HailIceShardVfx? iceVfx)
    {
        iceVfx?.Impact(target);
        attack.AddHit(await CreatureCmd.Damage(
            choiceContext,
            target,
            DynamicVars.Damage.IntValue,
            SakuraActions.AttackProps(this, DynamicVars.Damage.Props),
            Owner.Creature,
            this));
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(1);
}

