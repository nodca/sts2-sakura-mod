using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;

namespace SakuraMod.SakuraModCode.Cards;

public class MemoryFracture() : SakuraModCard(-1, CardType.Curse, CardRarity.Curse, TargetType.None)
{
    private const int HpLoss = 2;

    public override int MaxUpgradeLevel => 0;
    public override bool CanBeGeneratedByModifiers => false;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Unplayable];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("HpLoss", HpLoss)];

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        await base.AfterSideTurnEnd(choiceContext, side, participants);

        if (Pile?.Type != PileType.Hand
            || Owner.Creature.Side != side
            || !participants.Contains(Owner.Creature))
            return;

        var partners = SakuraActions.Hand(this)
            .Where(card => card != this && SakuraCardCatalog.IsPartnerCard(card))
            .ToList();
        var partner = Owner.RunState.Rng.CombatCardSelection.NextItem(partners);
        if (partner is null)
        {
            await CreatureCmd.Damage(
                choiceContext,
                Owner.Creature,
                DynamicVars["HpLoss"].IntValue,
                ValueProp.Unblockable | ValueProp.Unpowered,
                null,
                null);
            return;
        }

        await CardCmd.Exhaust(choiceContext, partner);
        if (Pile?.Type == PileType.Hand)
            await CardCmd.Exhaust(choiceContext, this);
    }
}

public class VoidBond() : SakuraModCard(2, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    private const int FallbackDraw = 3;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(FallbackDraw)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var exhaustedPartners = CardPile.Get(PileType.Exhaust, Owner)!.Cards
            .Where(SakuraCardCatalog.IsPartnerCard)
            .ToList();
        if (exhaustedPartners.Count == 0)
        {
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner, false);
            return;
        }

        foreach (var card in exhaustedPartners)
        {
            card.EnergyCost.SetThisTurnOrUntilPlayed(0, true);
            await SakuraActions.MoveExistingCardToHand(this, card);
        }
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class TsubasaAnotherMe() : SakuraModCard(1, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => IsUpgraded ? [CardKeyword.Innate] : [];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play) =>
        await PowerCmd.Apply<AnotherMePower>(choiceContext, Owner.Creature, 1, Owner.Creature, this, false);

    protected override void OnUpgrade() => AddKeywordIfMissing(CardKeyword.Innate);
}

public class EquivalentExchange() : SakuraModCard(2, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    private static readonly LocString SelectionPrompt = new("cards", "SAKURAMOD-EQUIVALENT_EXCHANGE.selectionPrompt");

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var candidates = SakuraActions.Hand(this)
            .Where(card => card != this && SakuraCardCatalog.IsPartnerCard(card))
            .ToList();
        var selected = await SakuraActions.SelectUpToFromCards(
            this,
            choiceContext,
            candidates,
            candidates.Count,
            prompt: SelectionPrompt,
            minSelect: 0);

        foreach (var card in selected)
            await CardCmd.Exhaust(choiceContext, card);

        for (var i = 0; i < selected.Count; i++)
            await AddRandomReleasedTransparentCard(choiceContext);
    }

    private async Task AddRandomReleasedTransparentCard(PlayerChoiceContext choiceContext)
    {
        var template = Owner.RunState.Rng.CombatCardSelection.NextItem(
            SakuraCardCatalog.TransparentCardTypes.Select(SakuraCardCatalog.CardTemplate).ToList());
        if (template is null || Owner.Creature.CombatState is not { } combatState)
            return;

        var card = combatState.CreateCard(template, Owner);
        await SakuraManifestLoop.AddGeneratedCardToCombat(
            card,
            new GeneratedCardOptions
            {
                AddRelease = true,
                RemoveTemporary = true,
                Pile = PileType.Hand,
                Position = CardPilePosition.Random
            },
            choiceContext);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class CopiedSoul() : SakuraModCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    private static readonly LocString SelectionPrompt = new("cards", "SAKURAMOD-COPIED_SOUL.selectionPrompt");

    public override IEnumerable<CardKeyword> CanonicalKeywords => IsUpgraded ? [] : [CardKeyword.Exhaust];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var selected = (await CardSelectCmd.FromHand(
                choiceContext,
                Owner,
                new CardSelectorPrefs(SelectionPrompt, 1)
                {
                    Cancelable = false,
                    RequireManualConfirmation = false
                },
                card => SakuraCardCatalog.IsPartnerCard(card),
                this))
            .FirstOrDefault();
        if (selected is null)
            return;

        await SakuraGeneratedCardLifecycle.AddGeneratedCopyToHand(
            selected,
            new GeneratedCardOptions
            {
                AddTemporary = true,
                FreeThisTurn = IsUpgraded
            },
            choiceContext);
    }

    protected override void OnUpgrade() => RemoveKeywordIfPresent(CardKeyword.Exhaust);
}

public class SleepingWings() : SakuraModCard(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<SleepingWingsPower>(1)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play) =>
        await PowerCmd.Apply<SleepingWingsPower>(
            choiceContext,
            Owner.Creature,
            IsUpgraded ? 2 : DynamicVars["SleepingWingsPower"].IntValue,
            Owner.Creature,
            this,
            false);

    protected override void OnUpgrade() => DynamicVars["SleepingWingsPower"].UpgradeValueBy(1);
}

public class MemoryFeather() : SakuraModCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    private static readonly LocString SelectionPrompt = new("cards", "SAKURAMOD-MEMORY_FEATHER.selectionPrompt");

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var selected = (await CardSelectCmd.FromHand(
                choiceContext,
                Owner,
                new CardSelectorPrefs(SelectionPrompt, 1)
                {
                    Cancelable = false,
                    RequireManualConfirmation = false
                },
                card => card != this && !SakuraCardCatalog.IsTsubasaCard(card),
                this))
            .FirstOrDefault();
        if (selected is null)
            return;

        await SakuraGeneratedCardLifecycle.GrantTemporary(choiceContext, selected);
        var power = await PowerCmd.Apply<MemoryFeatherPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this, true);
        power?.AddTarget(selected);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class DimensionalDrift() : SakuraModCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(2)];
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var templates = SakuraCardCatalog.PartnerTemplates();
        for (var i = 0; i < DynamicVars.Cards.IntValue; i++)
        {
            var template = Owner.RunState.Rng.CombatCardSelection.NextItem(templates);
            if (template is null || Owner.Creature.CombatState is not { } combatState)
                continue;

            var card = combatState.CreateCard(template, Owner);
            await SakuraManifestLoop.AddGeneratedCardToCombat(
                card,
                new GeneratedCardOptions
                {
                    AddTemporary = true,
                    Pile = PileType.Hand,
                    Position = CardPilePosition.Random
                },
                choiceContext);
        }
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1);
}
