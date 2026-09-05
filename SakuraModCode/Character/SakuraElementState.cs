using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.FourthAct.Dark.Powers;
using SakuraMod.SakuraModCode.FourthAct.Wind.Powers;
using SakuraMod.SakuraModCode.FourthAct.Water.Powers;
using SakuraMod.SakuraModCode.FourthAct.Fire.Powers;
using SakuraMod.SakuraModCode.FourthAct.Earth.Powers;
using SakuraMod.SakuraModCode.Powers;

namespace SakuraMod.SakuraModCode.Character;

internal static class SakuraElementState
{
    public static async Task<bool> ApplyMissing(PlayerChoiceContext choiceContext, CardModel card)
    {
        if (!card.IsMutable)
            return false;

        var applied = false;
        foreach (var element in SakuraActions.ElementSetOf(card).AsElements())
            applied |= await ApplyMissing(choiceContext, card.Owner, element);
        return applied;
    }

    public static bool Has(Player player, SakuraElement element) =>
        TriggerAmount(player, element) > 0;

    public static SakuraElementSet ReadActive(Player player)
    {
        var states = SakuraElementSet.None;
        foreach (var element in SakuraElementSets.AllElements)
        {
            if (Has(player, element))
                states |= element.ToSet();
        }

        return states;
    }

    public static SakuraElementSet NewlyActive(SakuraElementSet previous, SakuraElementSet current) =>
        current & ~previous;

    public static SakuraElementSet LocksFromSovereignty(bool wind, bool dark, bool water = false, bool fire = false, bool light = false, bool earth = false)
    {
        var locks = SakuraElementSet.None;
        if (wind || dark)
            locks |= SakuraElementSet.Wind;
        if (dark || water)
            locks |= SakuraElementSet.Water;
        if (fire || light)
            locks |= SakuraElementSet.Fire;
        if (light || earth)
            locks |= SakuraElementSet.Earth;
        return locks;
    }

    public static SakuraElementSet ReadLocks(ICombatState combatState) =>
        LocksFromSovereignty(
            combatState.Enemies.Any(static enemy => enemy.IsAlive && enemy.HasPower<WindSovereigntyPower>()),
            combatState.Enemies.Any(static enemy => enemy.IsAlive && enemy.HasPower<DarkSovereigntyPower>()),
            combatState.Enemies.Any(static enemy => enemy.IsAlive && enemy.HasPower<WaterSovereigntyPower>()),
            combatState.Enemies.Any(static enemy => enemy.IsAlive && enemy.HasPower<FireSovereigntyPower>()),
            combatState.Enemies.Any(static enemy => enemy.IsAlive && enemy.HasPower<LightBattlePower>()),
            combatState.Enemies.Any(static enemy => enemy.IsAlive && enemy.HasPower<EarthSovereigntyPower>()));

    public static SakuraElementSet LocksForPower(PowerModel power) => power switch
    {
        ClassicWindyPower or ClassicWindyPermanentPower => SakuraElementSet.Wind,
        ClassicWateryPower or ClassicWateryPermanentPower => SakuraElementSet.Water,
        ClassicFireyPower or ClassicFireyPermanentPower => SakuraElementSet.Fire,
        ClassicEarthyPower or ClassicEarthyPermanentPower => SakuraElementSet.Earth,
        _ => SakuraElementSet.None
    };

    public static bool IsTriggerPower(PowerModel power) =>
        power is ClassicEarthyPower
            or ClassicFireyPower
            or ClassicWateryPower
            or ClassicWindyPower;

    private static async Task<bool> ApplyMissing(
        PlayerChoiceContext choiceContext,
        Player owner,
        SakuraElement element)
    {
        switch (element)
        {
            case SakuraElement.Earth when owner.Creature.GetPower<ClassicEarthyPower>() is null:
                await PowerCmd.Apply<ClassicEarthyPower>(choiceContext, owner.Creature, 1, owner.Creature, null, false);
                return true;
            case SakuraElement.Fire when owner.Creature.GetPower<ClassicFireyPower>() is null:
                await PowerCmd.Apply<ClassicFireyPower>(choiceContext, owner.Creature, 1, owner.Creature, null, false);
                return true;
            case SakuraElement.Water when owner.Creature.GetPower<ClassicWateryPower>() is null:
                await PowerCmd.Apply<ClassicWateryPower>(choiceContext, owner.Creature, 1, owner.Creature, null, false);
                return true;
            case SakuraElement.Wind when owner.Creature.GetPower<ClassicWindyPower>() is null:
                await PowerCmd.Apply<ClassicWindyPower>(choiceContext, owner.Creature, 1, owner.Creature, null, false);
                return true;
            default:
                return false;
        }
    }

    private static int TriggerAmount(Player player, SakuraElement element) => element switch
    {
        SakuraElement.Earth => player.Creature.GetPower<ClassicEarthyPower>()?.Amount ?? 0,
        SakuraElement.Fire => player.Creature.GetPower<ClassicFireyPower>()?.Amount ?? 0,
        SakuraElement.Water => player.Creature.GetPower<ClassicWateryPower>()?.Amount ?? 0,
        SakuraElement.Wind => player.Creature.GetPower<ClassicWindyPower>()?.Amount ?? 0,
        _ => 0
    };
}
