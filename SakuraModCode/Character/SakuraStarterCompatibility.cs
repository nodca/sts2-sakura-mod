using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Runs;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Relics;

namespace SakuraMod.SakuraModCode.Character;

public static class SakuraStarterCompatibility
{
    public static bool IsKinomotoSakuraCharacter(CharacterModel character) =>
        character is ClassicSakura;

    public static bool IsKinomotoSakura(Player player) =>
        IsKinomotoSakuraCharacter(player.Character);

    public static bool IsKinomotoSakuraRun(IRunState runState) =>
        runState.Players.All(IsKinomotoSakura);

    public static bool TrySetupClassicSealedWandStarterUpgrade(
        TouchOfOrobas touchOfOrobas,
        Player player,
        ref bool result)
    {
        var sealedWand = SakuraUltimateWandRecipe.FindExactSealedWand(player);
        if (sealedWand is null)
            return true;

        touchOfOrobas.StarterRelic = sealedWand.Id;
        touchOfOrobas.UpgradedRelic = ModelDb.Relic<ClassicStarWandRelic>().Id;
        result = true;
        return false;
    }

    public static bool TryApplyClassicSealedWandStarterUpgrade(
        TouchOfOrobas touchOfOrobas,
        ref Task result)
    {
        var owner = touchOfOrobas.Owner;
        var starterRelic = touchOfOrobas.StarterRelic is { } starterRelicId
            ? owner.GetRelicById(starterRelicId)
            : SakuraUltimateWandRecipe.FindExactSealedWand(owner);
        if (starterRelic is not ClassicSealedWandRelic sealedWand
            || starterRelic.GetType() != typeof(ClassicSealedWandRelic))
        {
            return true;
        }

        var upgradedRelicId = touchOfOrobas.UpgradedRelic
            ?? touchOfOrobas.GetUpgradedStarterRelic(starterRelic).Id;
        if (upgradedRelicId != ModelDb.Relic<ClassicStarWandRelic>().Id)
            return true;

        result = RelicCmd.Replace(
            sealedWand,
            SakuraUltimateWandRecipe.CreateStarWandReplacement(sealedWand));
        return false;
    }
}
