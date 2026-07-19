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

public static class SakuraRelicCatalog
{
    private static readonly Type[] RewardableTypes =
    [
        typeof(ClassicSwordJadeRelic),
        typeof(ClassicTeddyBearRelic),
        typeof(ClassicRollerSkatesRelic),
        typeof(ClassicTouyasBicycleRelic),
        typeof(ClassicTaoistSuitRelic),
        typeof(ClassicYukitosBentoBoxRelic),
        typeof(ClassicCompassRelic),
        typeof(ClassicTomoyosHeartRelic),
        typeof(ClassicCerberusRelic),
        typeof(ClassicYueRelic),
        typeof(ClassicMoonBellRelic)
    ];

    internal static IReadOnlyList<Type> RewardableRelicTypes => RewardableTypes;

    public static IReadOnlyList<RelicModel> RewardableTemplates() =>
        RewardableTypes.Select(TypeToRelic).ToList();

    public static IReadOnlyList<RelicModel> AvailableForCreate(Player owner, RelicRarity rarity) =>
        IsCreateRarity(rarity)
            ? RewardableTemplates()
                .Where(relic => relic.Rarity == rarity && owner.RelicGrabBag.Contains(relic))
                .ToList()
            : [];

    public static IReadOnlyList<RelicModel> AvailableForCreate(Player owner) =>
        RewardableTemplates()
            .Where(relic => IsCreateRarity(relic.Rarity) && owner.RelicGrabBag.Contains(relic))
            .ToList();

    public static RelicModel? TryPullAvailableForCreate(Player owner, RelicRarity rarity)
    {
        if (AvailableForCreate(owner, rarity).Count == 0)
            return null;

        return owner.RelicGrabBag.PullFromFront(rarity, IsRewardableExclusive, owner.RunState);
    }

    public static RelicModel? TryPullRandomAvailableForCreate(Player owner)
    {
        var available = AvailableForCreate(owner);
        var relic = owner.PlayerRng.Rewards.NextItem(available);
        if (relic is null)
            return null;

        owner.RelicGrabBag.Remove(relic);
        owner.RunState.SharedRelicGrabBag.Remove(relic);
        return relic;
    }

    public static bool IsRewardableExclusive(RelicModel relic) =>
        RewardableTypes.Contains(relic.GetType());

    public static Type[] AllRelicTypes() =>
    [
        typeof(ClassicSealedBookRelic),
        typeof(ClassicSealedWandRelic),
        typeof(ClassicStarWandRelic),
        typeof(ClassicUltimateWandRelic),
        typeof(ClassicDarknessWandRelic),
        typeof(ClassicGemBroochRelic),
        ..RewardableTypes
    ];

    public static RelicModel[] AllClassicRelics() =>
    [
        ModelDb.Relic<ClassicSealedBookRelic>(),
        ModelDb.Relic<ClassicSealedWandRelic>(),
        ModelDb.Relic<ClassicStarWandRelic>(),
        ModelDb.Relic<ClassicUltimateWandRelic>(),
        ModelDb.Relic<ClassicDarknessWandRelic>(),
        ModelDb.Relic<ClassicGemBroochRelic>(),
        ..RewardableTemplates()
    ];

    private static bool IsCreateRarity(RelicRarity rarity) =>
        rarity is RelicRarity.Common or RelicRarity.Uncommon or RelicRarity.Rare;

    private static RelicModel TypeToRelic(Type type) =>
        ModelDb.GetById<RelicModel>(ModelDb.GetId(type));
}
