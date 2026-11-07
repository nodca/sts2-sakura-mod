using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;

namespace SakuraMod.SakuraModCode.Cards;

public class Aqua() : TransparentExtraEffectCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Water];
    internal override IEnumerable<CardKeyword> ReferencedKeywords => [SakuraKeywords.Frostbite];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(6, ValueProp.Move),
        new DynamicVar("FrostbiteBonus", 4),
        new EnergyVar(2)
    ];

    protected override async Task PlayCard(
        PlayerChoiceContext choiceContext,
        CardPlay play,
        SakuraExtraEffectActivation activation)
    {
        var targets = CombatState!.HittableEnemies.ToList();
        IReadOnlyList<Creature> frostbitten = [];
        await AquaPhoenixVfx.PlayOrResolveAsync(this, Owner.Creature, targets, activation.IsActive, async cues =>
        {
            await using (var attack = await AttackCommand.CreateContextAsync(CombatState!, choiceContext, play))
            {
                foreach (var enemy in targets)
                {
                    await cues.ReachAsync(enemy);
                    if (!enemy.IsAlive)
                        continue;

                    var damage = AquaRules.DamageAgainst(
                        enemy, DynamicVars.Damage.IntValue, DynamicVars["FrostbiteBonus"].IntValue);
                    attack.AddHit(await CreatureCmd.Damage(
                        choiceContext, enemy, damage, ValueProp.Move, Owner.Creature, this, play));
                }
            }

            // Read Frostbite after the whole attack resolves: damage can kill the
            // enemy holding Frostbite, and the rules filter on IsAlive.
            frostbitten = AquaRules.FrostbiteEnemies(targets);
            cues.Return(frostbitten);
        });

        if (activation.IsActive)
            await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
}

internal static class AquaRules
{
    /// <summary>Live enemies still holding Frostbite, in gameplay order.</summary>
    internal static IReadOnlyList<Creature> FrostbiteEnemies(IEnumerable<Creature> enemies) =>
        enemies.Where(HasFrostbite).ToList();

    internal static int FrostbiteEnemyCount(IEnumerable<Creature> enemies) =>
        FrostbiteEnemies(enemies).Count;

    // Frostbite is read just before each hit, so earlier hits cannot change a later enemy's bonus.
    internal static int DamageAgainst(Creature enemy, int baseDamage, int bonusPerFrostbite) =>
        DamageFor(baseDamage, enemy.GetPower<SakuraFrostbitePower>()?.Amount ?? 0, bonusPerFrostbite);

    internal static int DamageFor(int baseDamage, int frostbite, int bonusPerFrostbite) =>
        Math.Max(0, baseDamage) + Math.Max(0, frostbite) * Math.Max(0, bonusPerFrostbite);

    private static bool HasFrostbite(Creature enemy) =>
        enemy.IsAlive && (enemy.GetPower<SakuraFrostbitePower>()?.Amount ?? 0) > 0;
}
