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
using SakuraMod.SakuraModCode.Classic.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Classic.Character;
using SakuraMod.SakuraModCode.Classic.Powers;
using SakuraMod.SakuraModCode.Extensions;
using SakuraMod.SakuraModCode.Relics;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;
using CoreVoid = MegaCrit.Sts2.Core.Models.Cards.Void;

namespace SakuraMod.SakuraModCode.Classic.Relics;

public abstract class ClassicSakuraRelic : ModRelicTemplate
{
    protected abstract string IconFileName { get; }
    protected virtual string IconOutlineFileName => $"{Path.GetFileNameWithoutExtension(IconFileName)}_outline.png";

    public override string CustomIconPath => IconFileName.RelicImagePath();
    public override string CustomIconOutlinePath => IconOutlineFileName.RelicImagePath();
    public override string CustomBigIconPath => IconFileName.BigRelicImagePath();
}

public class ClassicSealedBookRelic : ClassicSakuraRelic
{
    protected override string IconFileName => "sealed_book.png";
    public override RelicRarity Rarity => RelicRarity.Starter;
}

public class ClassicSealedWandRelic : ClassicSakuraRelic, ISakuraUpgradeableStarterRelic
{
    private const string ChargeGainVar = "ChargeGain";
    private const string EliteBossExtraGainVar = "EliteBossExtraGain";
    private const string SealExtraGainVar = "SealExtraGain";
    private const string TriggerThresholdVar = "TriggerThreshold";
    private const string TriggerIncreaseVar = "TriggerIncrease";
    private const string RemainingChargeVar = "RemainingCharge";

    private const int DefaultBaseTrigger = 40;
    private const int DefaultTriggerIncrease = 20;
    private const int DefaultBaseChargeGain = 3;
    private const int DefaultEliteBossExtraGain = 2;
    private const int DefaultSealExtraGain = 2;

    private static readonly SavedAttachedState<ClassicSealedWandRelic, int> Charge =
        new("SakuraMod_ClassicSealedWandCharge", () => 0);

    private readonly HashSet<uint> _chargedDeathsThisCombat = [];
    private readonly HashSet<Creature> _sealKillsThisCombat = new(ReferenceEqualityComparer.Instance);

    protected override string IconFileName => "sealed_wand.png";
    public override RelicRarity Rarity => RelicRarity.Starter;
    public override bool ShowCounter => true;
    public override int DisplayAmount => Charge[this];
    public int ChargeAmount => Charge[this];
    public int ReturnRechargeAmount => ReturnRechargeAmountForThreshold(TriggerThreshold());
    protected virtual int BaseTriggerAmount => DefaultBaseTrigger;
    protected virtual int TriggerIncreaseAmount => DefaultTriggerIncrease;
    protected virtual int BaseChargeGainAmount => DefaultBaseChargeGain;
    protected virtual int EliteBossExtraGainAmount => DefaultEliteBossExtraGain;
    protected virtual int SealExtraGainAmount => DefaultSealExtraGain;
    protected virtual string GeneratedTurnSourceName => "Sealed Wand";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(ChargeGainVar, BaseChargeGainAmount),
        new DynamicVar(EliteBossExtraGainVar, EliteBossExtraGainAmount),
        new DynamicVar(SealExtraGainVar, SealExtraGainAmount),
        new SealedWandTriggerThresholdVar(BaseTriggerAmount),
        new DynamicVar(TriggerIncreaseVar, TriggerIncreaseAmount),
        new SealedWandRemainingChargeVar(BaseTriggerAmount)
    ];

    public virtual RelicModel? GetUpgradeReplacement() =>
        ModelDb.Relic<ClassicStarWandRelic>();

    public override async Task BeforeCombatStart()
    {
        _chargedDeathsThisCombat.Clear();
        _sealKillsThisCombat.Clear();
        await DowngradeDuplicateSakuraCards();
    }

    public override Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (cardSource is SpellSeal
            && result.WasTargetKilled
            && target.Side == CombatSide.Enemy
            && !target.IsSecondaryEnemy)
        {
            _sealKillsThisCombat.Add(target);
        }

        return Task.CompletedTask;
    }

    public override Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature, bool wasRemovalPrevented, float deathAnimLength)
    {
        TryGainChargeForEnemyDeath(creature, wasRemovalPrevented);
        return Task.CompletedTask;
    }

    internal bool TryGainChargeForEnemyDeath(Creature creature, bool wasRemovalPrevented)
    {
        var wasKilledBySeal = _sealKillsThisCombat.Remove(creature);
        if (wasRemovalPrevented
            || creature.CombatState is null
            || creature.Side != CombatSide.Enemy
            || creature.IsSecondaryEnemy)
            return false;

        var combatId = creature.CombatId ?? 0;
        if (!_chargedDeathsThisCombat.Add(combatId))
            return false;

        var gain = BaseChargeGainAmount;
        if (creature.CombatState.Encounter?.RoomType is RoomType.Elite or RoomType.Boss)
            gain += EliteBossExtraGainAmount;
        if (wasKilledBySeal)
            gain += SealExtraGainAmount;

        AddCharge(gain);
        return true;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner)
            return;

        var threshold = TriggerThreshold();
        if (Charge[this] < threshold)
            return;

        AddCharge(-threshold);
        if (Charge[this] < 0)
            Charge[this] = 0;

        Flash();
        await AddGeneratedTurnCard();
    }

    protected virtual async Task AddGeneratedTurnCard()
    {
        var combatState = Owner.Creature.CombatState
            ?? throw new InvalidOperationException($"{GeneratedTurnSourceName} generated Turn requires an active combat.");
        var turn = combatState.CreateCard<SpellTurn>(Owner);
        await CardPileCmd.AddGeneratedCardToCombat(turn, PileType.Hand, Owner, CardPilePosition.Random);
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _chargedDeathsThisCombat.Clear();
        _sealKillsThisCombat.Clear();
        return Task.CompletedTask;
    }

    private int TriggerThreshold() =>
        TriggerThreshold(Owner, BaseTriggerAmount, TriggerIncreaseAmount);

    private static int TriggerThreshold(Player owner, int baseTrigger, int triggerIncrease) =>
        baseTrigger + triggerIncrease * ClassicSakuraCardCatalog.ConvertedSakuraCount(owner);

    private int DisplayTriggerThreshold()
    {
        if (!IsMutable || Owner is null)
            return BaseTriggerAmount;

        return TriggerThreshold(Owner, BaseTriggerAmount, TriggerIncreaseAmount);
    }

    private int DisplayRemainingCharge()
    {
        if (!IsMutable || Owner is null)
            return BaseTriggerAmount;

        return Math.Max(0, TriggerThreshold(Owner, BaseTriggerAmount, TriggerIncreaseAmount) - Charge[this]);
    }

    public void AddReturnRecharge() =>
        AddCharge(ReturnRechargeAmount);

    public static int ReturnRechargeAmountFor(Player owner) =>
        owner.GetRelic<ClassicSealedWandRelic>() is { } wand
            ? wand.ReturnRechargeAmount
            : ReturnRechargeAmountForThreshold(TriggerThreshold(owner, DefaultBaseTrigger, DefaultTriggerIncrease));

    private static int ReturnRechargeAmountForThreshold(int threshold) =>
        Math.Max(0, (threshold - DefaultTriggerIncrease) * 3 / 4);

    private async Task DowngradeDuplicateSakuraCards()
    {
        if (Owner is null)
            return;

        HashSet<SourceCardIdentity> seen = [];
        List<CardTransformation> transformations = [];

        foreach (var card in Owner.Deck.Cards.OfType<ClassicSakuraConversionCard>())
        {
            if (card.Identity is not { } identity || seen.Add(identity))
                continue;

            var clowType = ClassicSakuraCardCatalog.ClowTypeFor(identity);
            if (clowType is null)
                continue;

            var canonicalClow = ModelDb.GetById<CardModel>(ModelDb.GetId(clowType));
            transformations.Add(new CardTransformation(card, Owner.RunState.CreateCard(canonicalClow, Owner)));
        }

        if (transformations.Count > 0)
            await CardCmd.Transform(transformations, null, CardPreviewStyle.None);
    }

    public void CopyChargeFrom(ClassicSealedWandRelic source) =>
        SetCharge(source.ChargeAmount);

    private void AddCharge(int amount) =>
        SetCharge(Charge[this] + amount);

    private void SetCharge(int amount)
    {
        Charge[this] = Math.Max(0, amount);
        InvokeDisplayAmountChanged();
    }

    private sealed class SealedWandTriggerThresholdVar(int fallbackValue) : DynamicVar(TriggerThresholdVar, fallbackValue)
    {
        public override string ToString() =>
            CurrentValue().ToString();

        protected override decimal GetBaseValueForIConvertible() =>
            CurrentValue();

        private int CurrentValue() =>
            _owner is ClassicSealedWandRelic relic ? relic.DisplayTriggerThreshold() : fallbackValue;
    }

    private sealed class SealedWandRemainingChargeVar(int fallbackValue) : DynamicVar(RemainingChargeVar, fallbackValue)
    {
        public override string ToString() =>
            CurrentValue().ToString();

        protected override decimal GetBaseValueForIConvertible() =>
            CurrentValue();

        private int CurrentValue() =>
            _owner is ClassicSealedWandRelic relic ? relic.DisplayRemainingCharge() : fallbackValue;
    }
}

public class ClassicStarWandRelic : ClassicSealedWandRelic
{
    protected override string IconFileName => "star_wand.png";
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override bool IsAllowedInShops => false;
    protected override int BaseTriggerAmount => 35;
    protected override int BaseChargeGainAmount => 4;
    protected override int EliteBossExtraGainAmount => 3;
    protected override string GeneratedTurnSourceName => "Star Wand";

    public override bool IsAllowed(IRunState runState) =>
        runState.Players.Any(static player => ClassicUltimateWandRecipe.FindExactSealedWand(player) is not null);

    public override RelicModel? GetUpgradeReplacement() =>
        null;

    public override async Task AfterObtained()
    {
        var sealedWand = ClassicUltimateWandRecipe.FindExactSealedWand(Owner);
        if (sealedWand is not null && !ReferenceEquals(sealedWand, this))
        {
            var replacement = ClassicUltimateWandRecipe.CreateStarWandReplacement(sealedWand);
            await RelicCmd.Replace(sealedWand, replacement);
            if (!HasBeenRemovedFromState)
                await RelicCmd.Remove(this);
            return;
        }

        await ClassicUltimateWandRecipe.TryCreateUltimateWand(Owner);
    }

    protected override async Task AddGeneratedTurnCard()
    {
        await base.AddGeneratedTurnCard();

        var deckCard = Owner.RunState.CreateCard<SpellTurn>(Owner);
        await CardPileCmd.Add(deckCard, PileType.Deck, CardPilePosition.Bottom, this, skipVisuals: true);
    }
}

public class ClassicUltimateWandRelic : ClassicSealedWandRelic
{
    private const string BattleStartMagicChargeVar = "BattleStartMagicCharge";
    private const string ElementTriggerVar = "ElementTrigger";
    private const string ElementMagicChargeVar = "ElementMagicCharge";
    private const string MarkDamageIncreaseVar = "MarkDamageIncrease";
    private const string HandUpgradesVar = "HandUpgrades";

    private const int BattleStartMagicCharge = 6;
    private const int ElementTrigger = 2;
    private const int ElementMagicCharge = 1;
    private const int MarkAmount = 2;
    private const int MarkDamageIncreasePercent = 50;
    private const int HandUpgrades = 2;

    private int _elementCardsPlayed;

    protected override string IconFileName => "ultimate_wand.png";
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override bool IsAllowedInShops => false;
    protected override int BaseTriggerAmount => 35;
    protected override int BaseChargeGainAmount => 5;
    protected override int EliteBossExtraGainAmount => 5;
    protected override string GeneratedTurnSourceName => "Ultimate Wand";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ..base.CanonicalVars,
        new DynamicVar(BattleStartMagicChargeVar, BattleStartMagicCharge),
        new DynamicVar(ElementTriggerVar, ElementTrigger),
        new DynamicVar(ElementMagicChargeVar, ElementMagicCharge),
        new DynamicVar(MarkDamageIncreaseVar, MarkDamageIncreasePercent),
        new DynamicVar(HandUpgradesVar, HandUpgrades)
    ];

    public override bool IsAllowed(IRunState runState) =>
        false;

    public override RelicModel? GetUpgradeReplacement() =>
        null;

    public override async Task BeforeCombatStart()
    {
        await base.BeforeCombatStart();
        await ClassicRelicCombatActions.ApplyMagicChargeIfSealedBook(
            this,
            new ThrowingPlayerChoiceContext(),
            BattleStartMagicCharge);
    }

    public override Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        if (player == Owner)
            _elementCardsPlayed = 0;

        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner)
            return;

        ClassicRelicCombatActions.UpgradeRandomHandCards(this, HandUpgrades);
        await ClassicRelicCombatActions.MarkRandomEnemy(this, choiceContext, MarkAmount);
        await base.AfterPlayerTurnStart(choiceContext, player);
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (play.Card?.Owner != Owner
            || play.Card is not ClassicSakuraCard card
            || card.Element == ClassicElement.None)
            return;

        _elementCardsPlayed++;
        if (_elementCardsPlayed < ElementTrigger)
            return;

        _elementCardsPlayed -= ElementTrigger;
        await ClassicRelicCombatActions.ApplyMagicChargeIfSealedBook(this, choiceContext, ElementMagicCharge);
    }
}

public class ClassicSwordJadeRelic : ClassicSakuraRelic
{
    public const float ReleaseRate = 1f;

    protected override string IconFileName => "sword_jade.png";
    public override RelicRarity Rarity => RelicRarity.Common;
}

public class ClassicTeddyBearRelic : ClassicSakuraRelic
{
    private const int Block = 3;

    protected override string IconFileName => "teddy_bear.png";
    public override RelicRarity Rarity => RelicRarity.Common;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (play.Card?.Owner != Owner || play.Card is not ClassicSpellCard || play.Card is SpellEmptySpell)
            return;

        Flash();
        await CreatureCmd.GainBlock(Owner.Creature, Block, ValueProp.Move, play, false);
    }
}

public class ClassicRollerSkatesRelic : ClassicSakuraRelic
{
    private const int DexterityGain = 1;
    private const int MaxDexterity = 3;

    protected override string IconFileName => "roller_skates.png";
    public override RelicRarity Rarity => RelicRarity.Common;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner || Owner.Creature.GetPower<DexterityPower>()?.Amount >= MaxDexterity)
            return;

        Flash();
        await PowerCmd.Apply<DexterityPower>(choiceContext, Owner.Creature, DexterityGain, Owner.Creature, null, false);
    }
}

public class ClassicTouyasBicycleRelic : ClassicSakuraRelic
{
    private const int MagicCharge = 1;

    protected override string IconFileName => "touyas_bicycle.png";
    public override RelicRarity Rarity => RelicRarity.Common;

    public override async Task AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)
    {
        if (card.Owner != Owner || card is not CoreVoid || Owner.GetRelic<ClassicSealedBookRelic>() is null)
            return;

        Flash();
        await PowerCmd.Apply<ClassicMagicChargePower>(choiceContext, Owner.Creature, MagicCharge, Owner.Creature, null, false);
    }
}

public class ClassicTaoistSuitRelic : ClassicSakuraRelic
{
    private const int Trigger = 4;
    private int _cardsPlayed;

    protected override string IconFileName => "taoist_suit.png";
    public override RelicRarity Rarity => RelicRarity.Uncommon;
    public override bool ShowCounter => true;
    public override int DisplayAmount => _cardsPlayed;

    public override Task BeforeCombatStart()
    {
        SetCardsPlayed(0);
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (play.Card?.Owner != Owner
            || play.Card is not ClassicSakuraCard { IsClassicSourceCard: true })
            return;

        SetCardsPlayed(_cardsPlayed + 1);
        if (_cardsPlayed < Trigger)
            return;

        SetCardsPlayed(_cardsPlayed - Trigger);
        Flash();
        var combatState = Owner.Creature.CombatState
            ?? throw new InvalidOperationException("Taoist Suit generated Empty Spell requires an active combat.");
        var card = combatState.CreateCard<SpellEmptySpell>(Owner);
        await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner, CardPilePosition.Random);
    }

    private void SetCardsPlayed(int amount)
    {
        _cardsPlayed = amount;
        InvokeDisplayAmountChanged();
    }
}

public class ClassicYukitosBentoBoxRelic : ClassicSakuraRelic
{
    private const int EnergyGain = 1;
    private bool _wasEnergyEmpty;

    protected override string IconFileName => "yukitos_bento_box.png";
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner is { } owner && owner.Creature.Side == side && participants.Contains(owner.Creature))
            _wasEnergyEmpty = owner.PlayerCombatState?.Energy == 0;

        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner || !_wasEnergyEmpty)
            return;

        _wasEnergyEmpty = false;
        Flash();
        await PlayerCmd.GainEnergy(EnergyGain, Owner);
    }
}

public class ClassicCompassRelic : ClassicSakuraRelic
{
    private static readonly LocString Prompt = new("cards", "SAKURAMOD-CLASSIC_COMPASS.selectionPrompt");
    private const int ChoiceCount = 3;
    private const int VoidCount = 3;
    private bool _usedThisCombat;

    protected override string IconFileName => "compass.png";
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override Task BeforeCombatStart()
    {
        _usedThisCombat = false;
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (_usedThisCombat || player != Owner)
            return;

        _usedThisCombat = true;
        var combatState = Owner.Creature.CombatState
            ?? throw new InvalidOperationException("Compass card choice requires an active combat.");
        var templates = ClassicSakuraCardCatalog.RewardableClowTemplates().ToList();
        Owner.RunState.Rng.CombatCardSelection.Shuffle(templates);
        var choices = templates
            .Take(ChoiceCount)
            .Select(template => combatState.CreateCard(template, Owner))
            .ToList();
        if (choices.Count == 0)
            return;

        CardModel? selected = null;
        try
        {
            selected = choices.Count == 1
                ? choices[0]
                : (await CardSelectCmd.FromSimpleGrid(
                    choiceContext,
                    choices,
                    Owner,
                    new CardSelectorPrefs(Prompt, 1)
                    {
                        Cancelable = false,
                        RequireManualConfirmation = false
                    })).FirstOrDefault();

            if (selected is null)
                return;

            Flash();
            selected.EnergyCost.SetThisTurnOrUntilPlayed(0, true);
            await CardPileCmd.AddGeneratedCardToCombat(selected, PileType.Hand, Owner, CardPilePosition.Random);

            var deckCard = Owner.RunState.CreateCard(ModelDb.GetById<CardModel>(ModelDb.GetId(selected.GetType())), Owner);
            await CardPileCmd.Add(deckCard, PileType.Deck, CardPilePosition.Bottom, this, skipVisuals: true);

            for (var i = 0; i < VoidCount; i++)
                await ClassicSakuraMagic.AddVoidToDiscardPile(choiceContext, Owner);
        }
        finally
        {
            foreach (var choice in choices)
            {
                if (choice != selected && choice.Pile is null)
                    choice.CardScope?.RemoveCard(choice);
            }
        }
    }
}

public class ClassicTomoyosHeartRelic : ClassicSakuraRelic
{
    private const int Trigger = 3;
    private int _exhaustedCards;

    protected override string IconFileName => "tomoyos_heart.png";
    public override RelicRarity Rarity => RelicRarity.Uncommon;
    public override bool ShowCounter => true;
    public override int DisplayAmount => _exhaustedCards;

    public override async Task AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)
    {
        if (card.Owner != Owner)
            return;

        Flash();
        if (_exhaustedCards >= Trigger)
        {
            SetExhaustedCards(_exhaustedCards - Trigger);
            await CardPileCmd.Draw(choiceContext, 1, Owner, false);
            return;
        }

        SetExhaustedCards(_exhaustedCards + 1);
    }

    private void SetExhaustedCards(int amount)
    {
        _exhaustedCards = amount;
        InvokeDisplayAmountChanged();
    }
}

public class ClassicCerberusRelic : ClassicSakuraRelic
{
    private const int BattleStartMagicCharge = 6;
    private const int MarkAmount = 1;

    protected override string IconFileName => "cerberus.png";
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override async Task AfterObtained() =>
        await ClassicUltimateWandRecipe.TryCreateUltimateWand(Owner);

    public override async Task BeforeCombatStart() =>
        await ClassicRelicCombatActions.ApplyMagicChargeIfSealedBook(
            this,
            new ThrowingPlayerChoiceContext(),
            BattleStartMagicCharge);

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner)
            return;

        await ClassicRelicCombatActions.MarkRandomEnemy(this, choiceContext, MarkAmount);
    }
}

public class ClassicYueRelic : ClassicSakuraRelic
{
    private const int Trigger = 3;
    private const int MagicCharge = 1;
    private const int Upgrades = 1;
    private int _elementCardsPlayed;

    protected override string IconFileName => "yue.png";
    public override RelicRarity Rarity => RelicRarity.Rare;
    public override bool ShowCounter => true;
    public override int DisplayAmount => _elementCardsPlayed;

    public override async Task AfterObtained() =>
        await ClassicUltimateWandRecipe.TryCreateUltimateWand(Owner);

    public override Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        if (player == Owner)
            SetElementCardsPlayed(0);

        return Task.CompletedTask;
    }

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner)
            ClassicRelicCombatActions.UpgradeRandomHandCards(this, Upgrades);

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (play.Card?.Owner != Owner
            || play.Card is not ClassicSakuraCard card
            || card.Element == ClassicElement.None)
            return;

        SetElementCardsPlayed(_elementCardsPlayed + 1);
        if (_elementCardsPlayed < Trigger)
            return;

        SetElementCardsPlayed(_elementCardsPlayed - Trigger);
        await ClassicRelicCombatActions.ApplyMagicChargeIfSealedBook(this, choiceContext, MagicCharge);
    }

    private void SetElementCardsPlayed(int amount)
    {
        _elementCardsPlayed = amount;
        InvokeDisplayAmountChanged();
    }
}

public class ClassicMoonBellRelic : ClassicSakuraRelic
{
    private const int TriggerCost = 2;
    private const int Heal = 1;
    private const int DeathPreventHealPercent = 30;

    private static readonly SavedAttachedState<ClassicMoonBellRelic, bool> Used =
        new("SakuraMod_ClassicMoonBellUsed", () => false);

    protected override string IconFileName => Used[this] ? "moon_bell_invalid.png" : "moon_bell.png";
    protected override string IconOutlineFileName => "moon_bell_outline.png";
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (Used[this] || play.Card?.Owner != Owner || play.Card.EnergyCost.Canonical < TriggerCost)
            return;

        Flash();
        await CreatureCmd.Heal(Owner.Creature, Heal);
    }

    public override bool ShouldDie(Creature creature) =>
        creature != Owner?.Creature || Used[this];

    public override async Task AfterPreventingDeath(Creature creature)
    {
        if (creature != Owner?.Creature || Used[this])
            return;

        Used[this] = true;
        Status = RelicStatus.Disabled;
        RelicIconChanged();
        Flash();

        // Heal is the revive path; SetCurrentHp(0) would re-enter Kill while death is being prevented.
        await CreatureCmd.Heal(Owner.Creature, Math.Max(1, Owner.Creature.MaxHp * DeathPreventHealPercent / 100));

        var combatState = Owner.Creature.CombatState
            ?? throw new InvalidOperationException("Moon Bell generated Turn requires an active combat.");
        var handCard = combatState.CreateCard<SpellTurn>(Owner);
        await CardPileCmd.AddGeneratedCardToCombat(handCard, PileType.Hand, Owner, CardPilePosition.Random);

        var deckCard = Owner.RunState.CreateCard<SpellTurn>(Owner);
        await CardPileCmd.Add(deckCard, PileType.Deck, CardPilePosition.Bottom, this, skipVisuals: true);
    }
}

internal static class ClassicUltimateWandRecipe
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

internal static class ClassicRelicCombatActions
{
    public static async Task ApplyMagicChargeIfSealedBook(
        ClassicSakuraRelic relic,
        PlayerChoiceContext choiceContext,
        int amount)
    {
        if (relic.Owner.GetRelic<ClassicSealedBookRelic>() is null)
            return;

        relic.Flash();
        await PowerCmd.Apply<ClassicMagicChargePower>(
            choiceContext,
            relic.Owner.Creature,
            amount,
            relic.Owner.Creature,
            null,
            false);
    }

    public static async Task MarkRandomEnemy(
        ClassicSakuraRelic relic,
        PlayerChoiceContext choiceContext,
        int amount)
    {
        var target = relic.Owner.Creature.CombatState?.HittableEnemies.ToList() is { Count: > 0 } enemies
            ? relic.Owner.RunState.Rng.CombatCardSelection.NextItem(enemies)
            : null;
        if (target is null)
            return;

        relic.Flash();
        await PowerCmd.Apply<ClassicCerberusMarkPower>(
            choiceContext,
            target,
            amount,
            relic.Owner.Creature,
            null,
            false);
    }

    public static void UpgradeRandomHandCards(ClassicSakuraRelic relic, int upgrades)
    {
        var upgradable = CardPile.GetCards(relic.Owner, PileType.Hand)
            .Where(static card => !card.IsUpgraded && card.MaxUpgradeLevel > card.CurrentUpgradeLevel)
            .ToList();
        if (upgradable.Count == 0)
            return;

        relic.Flash();
        for (var i = 0; i < upgrades && upgradable.Count > 0; i++)
        {
            var card = relic.Owner.RunState.Rng.CombatCardSelection.NextItem(upgradable);
            if (card is null)
                return;

            upgradable.Remove(card);
            CardCmd.Upgrade(card, CardPreviewStyle.None);
        }
    }
}

public static class ClassicSakuraExclusiveRelics
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

    public static Type[] AllClassicRelicTypes() =>
    [
        typeof(ClassicSealedBookRelic),
        typeof(ClassicSealedWandRelic),
        typeof(ClassicStarWandRelic),
        typeof(ClassicUltimateWandRelic),
        ..RewardableTypes
    ];

    public static RelicModel[] AllClassicRelics() =>
    [
        ModelDb.Relic<ClassicSealedBookRelic>(),
        ModelDb.Relic<ClassicSealedWandRelic>(),
        ModelDb.Relic<ClassicStarWandRelic>(),
        ModelDb.Relic<ClassicUltimateWandRelic>(),
        ..RewardableTemplates()
    ];

    private static bool IsCreateRarity(RelicRarity rarity) =>
        rarity is RelicRarity.Common or RelicRarity.Uncommon or RelicRarity.Rare;

    private static RelicModel TypeToRelic(Type type) =>
        ModelDb.GetById<RelicModel>(ModelDb.GetId(type));
}
