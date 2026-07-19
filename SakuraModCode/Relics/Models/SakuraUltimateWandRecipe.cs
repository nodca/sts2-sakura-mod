using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.Extensions;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;
using CoreVoid = MegaCrit.Sts2.Core.Models.Cards.Void;

namespace SakuraMod.SakuraModCode.Relics;

internal static class SakuraUltimateWandRecipe
{
    public static ClassicSealedWandRelic? FindExactSealedWand(Player owner) =>
        owner.Relics.FirstOrDefault(static relic => relic.GetType() == typeof(ClassicSealedWandRelic))
            as ClassicSealedWandRelic;

    public static RelicModel CreateStarWandReplacement(ClassicSealedWandRelic sealedWand)
    {
        var replacement = ModelDb.Relic<ClassicStarWandRelic>().ToMutable();
        ((ClassicStarWandRelic)replacement).CopyChargeFrom(sealedWand);
        return replacement;
    }

    public static async Task TryCreateUltimateWand(Player owner)
    {
        if (owner.GetRelic<ClassicUltimateWandRelic>() is not null)
            return;

        var starWand = owner.GetRelic<ClassicStarWandRelic>();
        var cerberus = owner.GetRelic<ClassicCerberusRelic>();
        var yue = owner.GetRelic<ClassicYueRelic>();
        if (starWand is null || cerberus is null || yue is null)
            return;

        var ultimateWand = ModelDb.Relic<ClassicUltimateWandRelic>().ToMutable();
        ((ClassicUltimateWandRelic)ultimateWand).CopyChargeFrom(starWand);

        await RelicCmd.Replace(starWand, ultimateWand);
        if (!cerberus.HasBeenRemovedFromState)
            await RelicCmd.Remove(cerberus);
        if (!yue.HasBeenRemovedFromState)
            await RelicCmd.Remove(yue);
    }
}

