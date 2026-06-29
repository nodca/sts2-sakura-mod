using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Runs;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Relics;
using VanillaEvents = MegaCrit.Sts2.Core.Models.Events;

namespace SakuraMod.SakuraModCode.Character;

public static class SakuraStarterCompatibility
{
    private const string NeowCursedDonePage = "NEOW.pages.DONE.CURSED.description";

    private static readonly Type[] IncompatibleVanillaStarterEventTypes =
    [
        typeof(VanillaEvents.Amalgamator),
        typeof(VanillaEvents.WoodCarvings),
        typeof(VanillaEvents.SpiralingWhirlpool)
    ];

    public static bool IsSakura(Player player) =>
        player.Character.Id == ModelDb.Character<SakuraMod>().Id;

    public static bool IsSakuraRun(IRunState runState) =>
        runState.Players.All(IsSakura);

    public static bool IsStarterCard(CardModel card) =>
        SakuraCardCatalog.IsStarterCard(card);

    public static bool IsStarterCard<T>(CardModel card) where T : CardModel =>
        SakuraCardCatalog.IsStarterCard<T>(card);

    public static bool IsStrikeEquivalentStarterCard(CardModel card) =>
        SakuraCardCatalog.IsStrikeEquivalentStarterCard(card);

    public static bool IsDefendEquivalentStarterCard(CardModel card) =>
        SakuraCardCatalog.IsDefendEquivalentStarterCard(card);

    public static bool IsBasicStrikeOrDefendEquivalent(CardModel card) =>
        SakuraCardCatalog.IsBasicStrikeOrDefendEquivalent(card);

    public static bool IsRemovableStarterCard(CardModel card) =>
        SakuraCardCatalog.IsRemovableStarterCard(card);

    public static bool IsTransformableStarterCard(CardModel card) =>
        SakuraCardCatalog.IsTransformableStarterCard(card);

    public static bool CanReplaceStrikeOrDefendPair(Player player) =>
        SakuraCardCatalog.CanReplaceStrikeOrDefendPair(player);

    public static int CountRemovable<T>(Player player) where T : CardModel =>
        SakuraCardCatalog.CountRemovable<T>(player);

    public static bool TryDisallowIncompatibleVanillaStarterEvent(
        IRunState runState,
        Type vanillaEventType,
        ref bool allowed)
    {
        if (!IsIncompatibleVanillaStarterEvent(vanillaEventType) || !IsSakuraRun(runState))
            return true;

        allowed = false;
        return false;
    }

    public static void RemoveIncompatibleVanillaStarterEvents(IRunState? runState, int actIndex)
    {
        if (runState is null || !IsSakuraRun(runState) || actIndex < 0 || actIndex >= runState.Acts.Count)
            return;

        var act = runState.Acts[actIndex];
        foreach (var eventType in IncompatibleVanillaStarterEventTypes)
            act.RemoveEventFromSet(EventModel(eventType));
    }

    public static IReadOnlyList<EventOption> ReplaceNeowRelicOptions(
        Neow neow,
        IReadOnlyList<EventOption> options,
        Func<Neow, RelicModel, EventOption> createRelicOption)
    {
        if (neow.Owner is null || !IsSakura(neow.Owner))
            return options;

        var replaced = options.ToList();
        for (var i = 0; i < replaced.Count; i++)
        {
            var replacement = NeowRelicReplacement(replaced[i].Relic);
            if (replacement is not null)
                replaced[i] = createRelicOption(neow, replacement);
        }

        return replaced;
    }

    public static bool TryTransformSakuraBasicStrikeOrDefendCards(
        PandorasBox pandorasBox,
        ref Task result)
    {
        if (!IsSakura(pandorasBox.Owner))
            return true;

        result = TransformSakuraBasicStrikeOrDefendCards(pandorasBox.Owner);
        return false;
    }

    public static bool TryUpgradeSakuraBasicStrikeAndDefendCards(
        NeowsTalisman neowsTalisman,
        ref Task result)
    {
        if (!IsSakura(neowsTalisman.Owner))
            return true;

        result = UpgradeSakuraBasicStrikeAndDefendCards(neowsTalisman.Owner);
        return false;
    }

    public static bool TryApplyKeroSnackBoxForSakura(LargeCapsule largeCapsule, ref Task result)
    {
        if (!IsSakura(largeCapsule.Owner))
            return true;

        result = SakuraStarterRelicEffects.ApplyKeroSnackBox(
            largeCapsule.Owner,
            largeCapsule.DynamicVars["Relics"].IntValue);
        return false;
    }

    public static bool TryApplyBrokenClockGearForSakura(LeafyPoultice leafyPoultice, ref Task result)
    {
        if (!IsSakura(leafyPoultice.Owner))
            return true;

        result = SakuraStarterRelicEffects.ApplyBrokenClockGear(
            leafyPoultice.Owner,
            leafyPoultice.DynamicVars.MaxHp.BaseValue);
        return false;
    }

    public static bool TryUseCustomStarterRelicUpgrade(RelicModel starterRelic, ref RelicModel result)
    {
        if (starterRelic is not CustomRelicModel customStarter)
            return true;

        var replacement = customStarter.GetUpgradeReplacement();
        if (replacement is null || replacement.Id == starterRelic.Id)
            return true;

        result = replacement;
        return false;
    }

    public static async Task ObtainNeowReplacementRelic(
        Neow neow,
        RelicModel relic,
        Action<AncientEventModel, string?> setDonePage,
        Action<AncientEventModel> finishAncient)
    {
        await RelicCmd.Obtain(relic, neow.Owner!);
        setDonePage(neow, NeowCursedDonePage);
        finishAncient(neow);
    }

    private static bool IsIncompatibleVanillaStarterEvent(Type eventType) =>
        IncompatibleVanillaStarterEventTypes.Contains(eventType);

    private static EventModel EventModel(Type eventType) =>
        ModelDb.GetById<EventModel>(ModelDb.GetId(eventType));

    private static RelicModel? NeowRelicReplacement(RelicModel? relic) =>
        relic switch
        {
            LargeCapsule => ModelDb.Relic<KeroSnackBox>(),
            _ => null
        };

    private static async Task TransformSakuraBasicStrikeOrDefendCards(Player owner)
    {
        var transformations = owner.Deck.Cards
            .Where(card => IsBasicStrikeOrDefendEquivalent(card) && card.IsTransformable)
            .Select(card => SakuraStarterRelicEffects.CreateSupportTransformation(owner, card))
            .ToList();

        var results = (await CardCmd.Transform(transformations, null, CardPreviewStyle.None)).ToList();
        if (results.Count > 0 && LocalContext.IsMe(owner))
            NSimpleCardsViewScreen.ShowScreen(results, new LocString("relics", "PANDORAS_BOX.infoText"));
    }

    private static Task UpgradeSakuraBasicStrikeAndDefendCards(Player owner)
    {
        var basicCards = owner.Deck.Cards
            .Where(card => card.Rarity == CardRarity.Basic)
            .ToList();
        var cards = new[]
        {
            basicCards.LastOrDefault(IsStrikeEquivalentStarterCard),
            basicCards.LastOrDefault(IsDefendEquivalentStarterCard)
        };

        CardCmd.Upgrade(cards.OfType<CardModel>(), CardPreviewStyle.HorizontalLayout);
        return Task.CompletedTask;
    }
}
