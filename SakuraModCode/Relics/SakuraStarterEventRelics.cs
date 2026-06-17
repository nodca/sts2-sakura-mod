using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Extensions;

namespace SakuraMod.SakuraModCode.Relics;

public class KeroSnackBox : SakuraModRelic
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override string PackedIconPath => "kero_snack_box.png".RelicImagePath();
    protected override string PackedIconOutlinePath => "kero_snack_box_outline.png".RelicImagePath();
    protected override string BigIconPath => "kero_snack_box.png".BigRelicImagePath();

    protected override IEnumerable<DynamicVar> CanonicalVars => [new IntVar("Relics", 2)];

    public override bool IsAllowed(IRunState runState) =>
        SakuraStarterCards.IsSakuraRun(runState);

    public override async Task AfterObtained() =>
        await SakuraStarterRelicEffects.ApplyKeroSnackBox(Owner, DynamicVars["Relics"].IntValue);
}

public class BrokenClockGear : SakuraModRelic
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new MaxHpVar(12)];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [HoverTipFactory.Static(StaticHoverTip.Transform)];

    public override bool IsAllowed(IRunState runState) =>
        SakuraStarterCards.IsSakuraRun(runState);

    public override async Task AfterObtained() =>
        await SakuraStarterRelicEffects.ApplyBrokenClockGear(Owner, DynamicVars.MaxHp.BaseValue);
}

public class TomoyoGalePlushie : SakuraModRelic
{
    private const string ExtraDamageKey = "ExtraDamage";

    public override RelicRarity Rarity => RelicRarity.Event;
    public override int MerchantCost => 50;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar(ExtraDamageKey, 1)];

    public override bool IsAllowed(IRunState runState) =>
        SakuraStarterCards.IsSakuraRun(runState);

    public override decimal ModifyDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (!props.IsPoweredAttack() || cardSource is null)
            return 0;

        if (!SakuraStarterCards.IsStarterCard<Gale>(cardSource))
            return 0;

        if (dealer != Owner.Creature && cardSource.Owner != Owner)
            return 0;

        return DynamicVars[ExtraDamageKey].BaseValue;
    }
}

internal static class SakuraStarterRelicEffects
{
    public static async Task ApplyKeroSnackBox(Player owner, int relicCount)
    {
        for (var i = 0; i < relicCount; i++)
        {
            var relic = RelicFactory.PullNextRelicFromFront(owner).ToMutable();
            await RelicCmd.Obtain(relic, owner);
        }

        var cards = new CardModel[]
        {
            owner.RunState.CreateCard<KeroAdvice>(owner),
            owner.RunState.CreateCard<KeroSnackBreak>(owner)
        };
        var results = await CardPileCmd.Add(cards, PileType.Deck);
        CardCmd.PreviewCardPileAdd(results, 2f);
    }

    public static async Task ApplyBrokenClockGear(Player owner, decimal maxHpLoss)
    {
        await CreatureCmd.LoseMaxHp(new ThrowingPlayerChoiceContext(), owner.Creature, maxHpLoss, isFromCard: false);

        var transformations = new List<CardTransformation>();
        AddFirstStarterTransformation<Gale>(owner, transformations);
        AddFirstStarterTransformation<Siege>(owner, transformations);

        if (transformations.Count > 0)
            await CardCmd.Transform(transformations, owner.PlayerRng.Transformations);
    }

    public static CardTransformation CreateSupportTransformation(Player owner, CardModel source) =>
        new(source, CreateRandomSupportCard(owner));

    private static void AddFirstStarterTransformation<T>(Player owner, ICollection<CardTransformation> transformations)
        where T : CardModel
    {
        var card = owner.Deck.Cards.FirstOrDefault(card =>
            SakuraStarterCards.IsStarterCard<T>(card) && card.IsTransformable);
        if (card is not null)
            transformations.Add(CreateSupportTransformation(owner, card));
    }

    private static CardModel CreateRandomSupportCard(Player owner)
    {
        var supportTemplates = SakuraActions.RewardableSupportCardTemplates(owner);
        if (supportTemplates.Count == 0)
            throw new InvalidOperationException("Cannot transform Sakura starter card: support card pool is empty.");

        var template = owner.PlayerRng.Transformations.NextItem(supportTemplates)
            ?? throw new InvalidOperationException("Cannot transform Sakura starter card: support card selection failed.");
        return owner.RunState.CreateCard(template, owner);
    }
}
