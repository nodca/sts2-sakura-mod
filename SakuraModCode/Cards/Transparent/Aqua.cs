using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;

namespace SakuraMod.SakuraModCode.Cards;

public class Aqua() : TransparentExtraEffectCard(0, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Water];
    internal override IEnumerable<CardKeyword> ReferencedKeywords => [SakuraKeywords.Frostbite];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(4, ValueProp.Move),
        new EnergyVar(1)
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
            foreach (var enemy in targets)
            {
                await cues.ReachAsync(enemy);
                await SakuraActions.Attack(choiceContext, this, enemy, DynamicVars.Damage.IntValue);
            }

            // Read Frostbite after every attack resolves: attacks can kill the
            // enemy holding Frostbite, and the rules filter on IsAlive.
            frostbitten = AquaRules.FrostbiteEnemies(targets);
            cues.Return(frostbitten);
        });

        var frostbiteEnemies = frostbitten.Count;
        if (frostbiteEnemies <= 0)
            return;

        var drawCount = AquaRules.DrawCount(frostbiteEnemies);
        if (drawCount > 0)
            await CardPileCmd.Draw(choiceContext, drawCount, Owner, false);

        if (activation.IsActive)
        {
            var energy = frostbiteEnemies * DynamicVars.Energy.IntValue;
            if (energy > 0)
                await PlayerCmd.GainEnergy(energy, Owner);
        }
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

    internal static int DrawCount(int frostbiteEnemyCount) =>
        Math.Max(0, frostbiteEnemyCount);

    private static bool HasFrostbite(Creature enemy) =>
        enemy.IsAlive && (enemy.GetPower<SakuraFrostbitePower>()?.Amount ?? 0) > 0;
}
