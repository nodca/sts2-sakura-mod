using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
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

    public static bool TryApplyStrikeDummyDamageForSakura(
        RelicModel relic,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        ref decimal result)
    {
        if (!IsSakura(relic.Owner))
            return true;

        result = 0m;
        if (!props.IsPoweredAttack()
            || cardSource is null
            || !IsStarterCard<Gale>(cardSource)
            || (dealer != relic.Owner.Creature && cardSource.Owner != relic.Owner))
            return false;

        result = relic.DynamicVars["ExtraDamage"].BaseValue;
        return false;
    }

    public static bool TryApplyGhostSeedAfterCardEnteredCombatForSakura(
        GhostSeed ghostSeed,
        CardModel card,
        ref Task result)
    {
        if (!IsSakura(ghostSeed.Owner))
            return true;

        ApplyGhostSeedToSakuraStarter(card);
        result = Task.CompletedTask;
        return false;
    }

    public static bool TryApplyGhostSeedAfterRoomEnteredForSakura(
        GhostSeed ghostSeed,
        AbstractRoom room,
        ref Task result)
    {
        if (!IsSakura(ghostSeed.Owner))
            return true;

        if (room is CombatRoom && ghostSeed.Owner.PlayerCombatState is { } combatState)
        {
            foreach (var card in combatState.AllCards)
                ApplyGhostSeedToSakuraStarter(card);
        }

        result = Task.CompletedTask;
        return false;
    }

    public static bool TryApplyTezcatarasEmberForSakura(NutritiousSoup nutritiousSoup, ref Task result)
    {
        if (!IsSakura(nutritiousSoup.Owner))
            return true;

        result = ApplyTezcatarasEmberToSakuraGales(nutritiousSoup.Owner);
        return false;
    }

    public static bool TryApplySakuraGoopyForSakura(PaelsClaw paelsClaw, ref Task result)
    {
        if (!IsSakura(paelsClaw.Owner))
            return true;

        result = ApplySakuraGoopyToSakuraSieges(paelsClaw.Owner);
        return false;
    }

    public static bool TrySetupArchaicToothForSakura(
        ArchaicTooth archaicTooth,
        Player player,
        ref bool result)
    {
        if (!IsSakura(player))
            return true;

        var starter = FindArchaicToothStarter(player);
        if (starter is null)
        {
            result = false;
            return false;
        }

        archaicTooth.SetupForTests(
            starter.ToSerializable(),
            CreateArchaicToothReplacement(starter).ToSerializable());
        result = true;
        return false;
    }

    public static bool TryApplyArchaicToothForSakura(ArchaicTooth archaicTooth, ref Task result)
    {
        if (!IsSakura(archaicTooth.Owner))
            return true;

        result = ApplyArchaicToothForSakura(archaicTooth.Owner);
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

    private static void ApplyGhostSeedToSakuraStarter(CardModel card)
    {
        if (card.Rarity == CardRarity.Basic
            && IsBasicStrikeOrDefendEquivalent(card)
            && !card.GetKeywordsWithSources(KeywordSources.Local).Contains(CardKeyword.Ethereal))
        {
            CardCmd.ApplyKeyword(card, CardKeyword.Ethereal);
        }
    }

    private static Task ApplyTezcatarasEmberToSakuraGales(Player owner)
    {
        foreach (var card in PileType.Deck.GetPile(owner).Cards.ToList())
        {
            if (card.Rarity == CardRarity.Basic
                && IsStarterCard<Gale>(card)
                && ModelDb.Enchantment<TezcatarasEmber>().CanEnchant(card))
            {
                CardCmd.Enchant<TezcatarasEmber>(card, 1m);
                NRun.Instance?.GlobalUi.CardPreviewContainer.AddChildSafely(NCardEnchantVfx.Create(card));
            }
        }

        return Task.CompletedTask;
    }

    private static Task ApplySakuraGoopyToSakuraSieges(Player owner)
    {
        foreach (var card in PileType.Deck.GetPile(owner).Cards.ToList())
        {
            if (card.Rarity == CardRarity.Basic
                && IsStarterCard<Siege>(card)
                && ModelDb.Enchantment<SakuraGoopy>().CanEnchant(card))
            {
                CardCmd.Enchant<SakuraGoopy>(card, 1m);
                NRun.Instance?.GlobalUi.CardPreviewContainer.AddChildSafely(NCardEnchantVfx.Create(card));
            }
        }

        return Task.CompletedTask;
    }

    private static async Task ApplyArchaicToothForSakura(Player owner)
    {
        var starter = FindArchaicToothStarter(owner);
        if (starter is null)
            return;

        await CardCmd.Transform(starter, CreateArchaicToothReplacement(starter));
    }

    private static CardModel? FindArchaicToothStarter(Player owner) =>
        owner.Deck.Cards.FirstOrDefault(card =>
            card.IsTransformable
            && (IsStarterCard<Gale>(card) || IsStarterCard<Siege>(card)));

    private static CardModel CreateArchaicToothReplacement(CardModel starter)
    {
        CardModel replacementTemplate = IsStarterCard<Gale>(starter)
            ? ModelDb.Card<RollerbladeDash>()
            : ModelDb.Card<MagicBarrier>();
        var replacement = starter.Owner.RunState.CreateCard(replacementTemplate, starter.Owner);

        if (starter.IsUpgraded)
            CardCmd.Upgrade(replacement);
        if (starter.Enchantment is not null)
        {
            var enchantment = (EnchantmentModel)starter.Enchantment.MutableClone();
            if (enchantment.CanEnchant(replacement))
                CardCmd.Enchant(enchantment, replacement, enchantment.Amount);
        }

        return replacement;
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
