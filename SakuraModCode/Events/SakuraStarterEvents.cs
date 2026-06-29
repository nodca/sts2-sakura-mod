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

namespace SakuraMod.SakuraModCode.Events;

internal static class SakuraStarterEventPortraits
{
    public const string Amalgamator = "res://images/events/amalgamator.png";
    public const string WoodCarvings = "res://images/events/wood_carvings.png";
    public const string SpiralingWhirlpool = "res://images/events/spiraling_whirlpool.png";
}

public class SakuraAmalgamator : CustomEventModel
{
    public override ActModel[] Acts => [ModelDb.Act<Hive>()];
    public override string? CustomInitialPortraitPath => SakuraStarterEventPortraits.Amalgamator;

    public SakuraAmalgamator()
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new StringVar("GaleReward", ModelDb.Card<RollerbladeDash>().Title),
        new StringVar("SiegeReward", ModelDb.Card<MagicBarrier>().Title)
    ];

    public override bool IsAllowed(IRunState runState) =>
        SakuraStarterCompatibility.IsSakuraRun(runState)
        && runState.Players.All(SakuraStarterCompatibility.CanReplaceStrikeOrDefendPair);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        SakuraStarterCompatibility.CountRemovable<Gale>(Owner!) >= 2
            ? new EventOption(this, CombineGales, SakuraInitialOptionKey("COMBINE_GALES"), HoverTipFactory.FromCardWithCardHoverTips<RollerbladeDash>())
            : new EventOption(this, null, SakuraInitialOptionKey("COMBINE_GALES_LOCKED"), HoverTipFactory.FromCardWithCardHoverTips<RollerbladeDash>()),
        SakuraStarterCompatibility.CountRemovable<Siege>(Owner!) >= 2
            ? new EventOption(this, CombineSieges, SakuraInitialOptionKey("COMBINE_SIEGES"), HoverTipFactory.FromCardWithCardHoverTips<MagicBarrier>())
            : new EventOption(this, null, SakuraInitialOptionKey("COMBINE_SIEGES_LOCKED"), HoverTipFactory.FromCardWithCardHoverTips<MagicBarrier>())
    ];

    private async Task CombineGales()
    {
        await RemoveTwo<Gale>();
        await AddReward<RollerbladeDash>();
        SetEventFinished(L10NLookup($"{Id.Entry}.pages.COMBINE_GALES.description"));
    }

    private async Task CombineSieges()
    {
        await RemoveTwo<Siege>();
        await AddReward<MagicBarrier>();
        SetEventFinished(L10NLookup($"{Id.Entry}.pages.COMBINE_SIEGES.description"));
    }

    private async Task RemoveTwo<T>() where T : CardModel
    {
        var cards = (await CardSelectCmd.FromDeckForRemoval(
            Owner!,
            new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt, 2),
            card => SakuraStarterCompatibility.IsStarterCard<T>(card) && card.IsRemovable)).ToList();

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
    public override string? CustomInitialPortraitPath => SakuraStarterEventPortraits.WoodCarvings;

    public SakuraWoodCarvings()
    {
    }

    public override bool IsAllowed(IRunState runState) =>
        SakuraStarterCompatibility.IsSakuraRun(runState)
        && runState.Players.All(player => player.Deck.Cards.Any(SakuraStarterCompatibility.IsTransformableStarterCard));

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new EventOption(this, Bird, SakuraInitialOptionKey("BIRD"), HoverTipFactory.FromCardWithCardHoverTips<SilverMoonWing>()),
        Owner!.Deck.Cards.Any(CanRelease)
            ? new EventOption(this, Snake, SakuraInitialOptionKey("SNAKE"))
            : new EventOption(this, null, SakuraInitialOptionKey("SNAKE_LOCKED")),
        new EventOption(this, Torus, SakuraInitialOptionKey("TORUS"), HoverTipFactory.FromCardWithCardHoverTips<SealedBook>())
    ];

    private async Task Bird()
    {
        var card = (await SelectStarter(CardSelectorPrefs.TransformSelectionPrompt, SakuraStarterCompatibility.IsTransformableStarterCard)).FirstOrDefault();
        if (card is not null)
            await CardCmd.TransformTo<SilverMoonWing>(card, CardPreviewStyle.EventLayout);

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
        var card = (await SelectStarter(CardSelectorPrefs.TransformSelectionPrompt, SakuraStarterCompatibility.IsTransformableStarterCard)).FirstOrDefault();
        if (card is not null)
            await CardCmd.TransformTo<SealedBook>(card, CardPreviewStyle.EventLayout);

        SetEventFinished(L10NLookup($"{Id.Entry}.pages.TORUS.description"));
    }

    private Task<IEnumerable<CardModel>> SelectStarter(LocString prompt, Func<CardModel, bool> filter) =>
        CardSelectCmd.FromDeckGeneric(
            Owner!,
            new CardSelectorPrefs(prompt, 1),
            filter);

    private static bool CanRelease(CardModel card) =>
        SakuraStarterCompatibility.IsStarterCard(card) && !card.IsReleased();

    private string SakuraInitialOptionKey(string optionName) =>
        $"{Id.Entry}.pages.INITIAL.options.{optionName}";
}

public class SakuraSpiralingWhirlpool : CustomEventModel
{
    public override ActModel[] Acts => [ModelDb.Act<Underdocks>()];
    public override string? CustomInitialPortraitPath => SakuraStarterEventPortraits.SpiralingWhirlpool;

    public SakuraSpiralingWhirlpool()
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => [new HealVar(0)];

    public override void CalculateVars()
    {
        DynamicVars.Heal.BaseValue = Owner is null ? 0 : Owner.Creature.MaxHp * 0.33m;
    }

    public override bool IsAllowed(IRunState runState) =>
        SakuraStarterCompatibility.IsSakuraRun(runState)
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
