using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Runs;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraSpiralCard = SakuraMod.SakuraModCode.Cards.Spiral;
using VanillaEvents = MegaCrit.Sts2.Core.Models.Events;

namespace SakuraMod.SakuraModCode.Events;

internal static class SakuraStarterEventReplacements
{
    public static void RemoveVanillaEventsFromAct(IRunState? runState, int actIndex)
    {
        if (runState is null || !SakuraStarterCards.IsSakuraRun(runState) || actIndex < 0 || actIndex >= runState.Acts.Count)
            return;

        var act = runState.Acts[actIndex];
        act.RemoveEventFromSet(ModelDb.Event<VanillaEvents.Amalgamator>());
        act.RemoveEventFromSet(ModelDb.Event<VanillaEvents.WoodCarvings>());
        act.RemoveEventFromSet(ModelDb.Event<VanillaEvents.SpiralingWhirlpool>());
    }
}

public class SakuraAmalgamator : CustomEventModel
{
    public override ActModel[] Acts => [ModelDb.Act<Hive>()];

    public SakuraAmalgamator()
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new StringVar("GaleReward", ModelDb.Card<SakuraSpiralCard>().Title),
        new StringVar("SiegeReward", ModelDb.Card<Labyrinth>().Title)
    ];

    public override bool IsAllowed(IRunState runState) =>
        SakuraStarterCards.IsSakuraRun(runState)
        && runState.Players.All(SakuraStarterCards.CanReplaceStrikeOrDefendPair);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        SakuraStarterCards.CountRemovable<Gale>(Owner!) >= 2
            ? new EventOption(this, CombineGales, SakuraInitialOptionKey("COMBINE_GALES"), HoverTipFactory.FromCardWithCardHoverTips<SakuraSpiralCard>())
            : new EventOption(this, null, SakuraInitialOptionKey("COMBINE_GALES_LOCKED"), HoverTipFactory.FromCardWithCardHoverTips<SakuraSpiralCard>()),
        SakuraStarterCards.CountRemovable<Siege>(Owner!) >= 2
            ? new EventOption(this, CombineSieges, SakuraInitialOptionKey("COMBINE_SIEGES"), HoverTipFactory.FromCardWithCardHoverTips<Labyrinth>())
            : new EventOption(this, null, SakuraInitialOptionKey("COMBINE_SIEGES_LOCKED"), HoverTipFactory.FromCardWithCardHoverTips<Labyrinth>())
    ];

    private async Task CombineGales()
    {
        await RemoveTwo<Gale>();
        await AddReward<SakuraSpiralCard>();
        SetEventFinished(L10NLookup($"{Id.Entry}.pages.COMBINE_GALES.description"));
    }

    private async Task CombineSieges()
    {
        await RemoveTwo<Siege>();
        await AddReward<Labyrinth>();
        SetEventFinished(L10NLookup($"{Id.Entry}.pages.COMBINE_SIEGES.description"));
    }

    private async Task RemoveTwo<T>() where T : CardModel
    {
        var cards = (await CardSelectCmd.FromDeckForRemoval(
            Owner!,
            new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt, 2),
            card => SakuraStarterCards.IsStarterCard<T>(card) && card.IsRemovable)).ToList();

        await CardPileCmd.RemoveFromDeck(cards);
    }

    private async Task AddReward<T>() where T : CardModel
    {
        var card = Owner!.RunState.CreateCard<T>(Owner);
        CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(card, PileType.Deck), 2f);
    }

    private string SakuraInitialOptionKey(string optionName) =>
        $"{Id.Entry}.pages.INITIAL.options.{optionName}";
}

public class SakuraWoodCarvings : CustomEventModel
{
    public override ActModel[] Acts => [ModelDb.Act<Overgrowth>()];

    public SakuraWoodCarvings()
    {
    }

    public override bool IsAllowed(IRunState runState) =>
        SakuraStarterCards.IsSakuraRun(runState)
        && runState.Players.All(player => player.Deck.Cards.Any(SakuraStarterCards.IsTransformableStarterCard));

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new EventOption(this, Bird, SakuraInitialOptionKey("BIRD"), HoverTipFactory.FromCardWithCardHoverTips<Reflect>()),
        Owner!.Deck.Cards.Any(CanRelease)
            ? new EventOption(this, Snake, SakuraInitialOptionKey("SNAKE"))
            : new EventOption(this, null, SakuraInitialOptionKey("SNAKE_LOCKED")),
        new EventOption(this, Torus, SakuraInitialOptionKey("TORUS"), HoverTipFactory.FromCardWithCardHoverTips<Labyrinth>())
    ];

    private async Task Bird()
    {
        var card = (await SelectStarter(CardSelectorPrefs.TransformSelectionPrompt, SakuraStarterCards.IsTransformableStarterCard)).FirstOrDefault();
        if (card is not null)
            await CardCmd.TransformTo<Reflect>(card, CardPreviewStyle.EventLayout);

        SetEventFinished(L10NLookup($"{Id.Entry}.pages.BIRD.description"));
    }

    private async Task Snake()
    {
        var card = (await SelectStarter(CardSelectorPrefs.EnchantSelectionPrompt, CanRelease)).FirstOrDefault();
        card?.Release();

        SetEventFinished(L10NLookup($"{Id.Entry}.pages.SNAKE.description"));
    }

    private async Task Torus()
    {
        var card = (await SelectStarter(CardSelectorPrefs.TransformSelectionPrompt, SakuraStarterCards.IsTransformableStarterCard)).FirstOrDefault();
        if (card is not null)
            await CardCmd.TransformTo<Labyrinth>(card, CardPreviewStyle.EventLayout);

        SetEventFinished(L10NLookup($"{Id.Entry}.pages.TORUS.description"));
    }

    private Task<IEnumerable<CardModel>> SelectStarter(LocString prompt, Func<CardModel, bool> filter) =>
        CardSelectCmd.FromDeckGeneric(
            Owner!,
            new CardSelectorPrefs(prompt, 1),
            filter);

    private static bool CanRelease(CardModel card) =>
        SakuraStarterCards.IsStarterCard(card) && !card.IsReleased();

    private string SakuraInitialOptionKey(string optionName) =>
        $"{Id.Entry}.pages.INITIAL.options.{optionName}";
}

public class SakuraSpiralingWhirlpool : CustomEventModel
{
    public override ActModel[] Acts => [ModelDb.Act<Underdocks>()];

    public SakuraSpiralingWhirlpool()
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => [new HealVar(0)];

    public override void CalculateVars()
    {
        DynamicVars.Heal.BaseValue = Owner is null ? 0 : Owner.Creature.MaxHp * 0.33m;
    }

    public override bool IsAllowed(IRunState runState) =>
        SakuraStarterCards.IsSakuraRun(runState)
        && runState.Players.All(player => player.Deck.Cards.Any(ModelDb.Enchantment<SakuraSpiral>().CanEnchant));

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        var canObserve = Owner!.Deck.Cards.Any(ModelDb.Enchantment<SakuraSpiral>().CanEnchant);
        return
        [
            canObserve
                ? new EventOption(this, ObserveTheSpiral, SakuraInitialOptionKey("OBSERVE"), HoverTipFactory.FromEnchantment<SakuraSpiral>())
                : new EventOption(this, null, SakuraInitialOptionKey("OBSERVE_LOCKED"), HoverTipFactory.FromEnchantment<SakuraSpiral>()),
            new EventOption(this, Drink, SakuraInitialOptionKey("DRINK"))
        ];
    }

    private async Task ObserveTheSpiral()
    {
        var enchantment = ModelDb.Enchantment<SakuraSpiral>();
        var card = (await CardSelectCmd.FromDeckForEnchantment(
            Owner!,
            enchantment,
            1,
            new CardSelectorPrefs(CardSelectorPrefs.EnchantSelectionPrompt, 1))).FirstOrDefault();

        if (card is not null)
        {
            CardCmd.Enchant(enchantment.ToMutable(), card, 1m);
            var vfx = NCardEnchantVfx.Create(card);
            if (vfx is not null)
                NRun.Instance?.GlobalUi.CardPreviewContainer.AddChild(vfx);
        }

        SetEventFinished(L10NLookup($"{Id.Entry}.pages.OBSERVE.description"));
    }

    private async Task Drink()
    {
        await CreatureCmd.Heal(Owner!.Creature, DynamicVars.Heal.IntValue);
        SetEventFinished(L10NLookup($"{Id.Entry}.pages.DRINK.description"));
    }

    private string SakuraInitialOptionKey(string optionName) =>
        $"{Id.Entry}.pages.INITIAL.options.{optionName}";
}
