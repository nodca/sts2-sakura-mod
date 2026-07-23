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

public class ClassicSealedWandRelic : SakuraRelicModel
{
    private const string ChargeGainVar = "ChargeGain";
    private const string EliteBossExtraGainVar = "EliteBossExtraGain";
    private const string SealExtraGainVar = "SealExtraGain";
    private const string TriggerThresholdVar = "TriggerThreshold";
    private const string TriggerIncreaseVar = "TriggerIncrease";
    private const string RemainingChargeVar = "RemainingCharge";

    private const int DefaultBaseTrigger = 40;
    private const int DefaultTriggerIncrease = 10;
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
        new CardsVar(1),
        new SealedWandTriggerThresholdVar(BaseTriggerAmount),
        new DynamicVar(TriggerIncreaseVar, TriggerIncreaseAmount),
        new SealedWandRemainingChargeVar(BaseTriggerAmount)
    ];

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
        if (SakuraSealKillPolicy.IsSealCard(cardSource)
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

        var darknessWand = Owner.GetRelic<ClassicDarknessWandRelic>();
        var gain = ChargeGainForDeath(
            BaseChargeGainAmount,
            EliteBossExtraGainAmount,
            SealExtraGainAmount,
            ClassicDarknessWandRelic.WandChargeGain,
            creature.CombatState.Encounter?.RoomType is RoomType.Elite or RoomType.Boss,
            wasKilledBySeal,
            darknessWand is not null);

        darknessWand?.Flash();

        AddCharge(gain);
        return true;
    }

    internal static int ChargeGainForDeath(
        int baseGain,
        int eliteBossExtraGain,
        int sealExtraGain,
        int darknessWandExtraGain,
        bool isEliteOrBossRoom,
        bool wasKilledBySeal,
        bool hasDarknessWand) =>
        baseGain
        + (isEliteOrBossRoom ? eliteBossExtraGain : 0)
        + (wasKilledBySeal ? sealExtraGain : 0)
        + (hasDarknessWand ? darknessWandExtraGain : 0);

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
        TriggerThresholdFor(
            SakuraSourceCardRules.ConvertedSakuraCount(owner),
            baseTrigger,
            triggerIncrease);

    internal static int TriggerThresholdFor(
        int convertedSakuraCount,
        int baseTrigger = DefaultBaseTrigger,
        int triggerIncrease = DefaultTriggerIncrease) =>
        baseTrigger + triggerIncrease * Math.Max(0, convertedSakuraCount);

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

        foreach (var card in Owner.Deck.Cards.OfType<SakuraFormCard>())
        {
            if (card.Identity is not { } identity || seen.Add(identity))
                continue;

            var clowType = SakuraSourceCardRules.ClowTypeFor(identity);
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
