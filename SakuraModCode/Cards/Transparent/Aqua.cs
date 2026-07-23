using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Extensions;
using SakuraMod.SakuraModCode.Powers;
using STS2RitsuLib.Cards.DynamicVars;

namespace SakuraMod.SakuraModCode.Cards;

public class Aqua() : TransparentExtraEffectCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Water];
    internal override IEnumerable<CardKeyword> ReferencedKeywords => [SakuraKeywords.Frostbite];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(6, ValueProp.Move),
        new EnergyVar(1)
    ];

    protected override async Task PlayCard(
        PlayerChoiceContext choiceContext,
        CardPlay play,
        SakuraExtraEffectActivation activation)
    {
        var targets = CombatState!.HittableEnemies.ToList();
        SakuraCardPlayVfx.PlayAqua(targets);
        foreach (var enemy in targets)
            await SakuraActions.Attack(choiceContext, this, enemy, DynamicVars.Damage.IntValue);

        var highestFrostbite = AquaRules.HighestFrostbite(targets);
        if (highestFrostbite <= 0)
            return;

        var extraAmount = activation.IsActive ? 1 : 0;
        await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue + extraAmount, Owner);

        var drawCount = AquaRules.DrawCount(highestFrostbite) + extraAmount;
        if (drawCount > 0)
            await CardPileCmd.Draw(choiceContext, drawCount, Owner, false);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
}

internal static class AquaRules
{
    internal static int HighestFrostbite(IEnumerable<Creature> enemies) =>
        enemies
            .Where(static enemy => enemy.IsAlive)
            .Select(static enemy => enemy.GetPower<SakuraFrostbitePower>()?.Amount ?? 0)
            .DefaultIfEmpty()
            .Max();

    internal static int DrawCount(int highestFrostbite) =>
        Math.Max(0, highestFrostbite);
}
