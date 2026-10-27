using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.Relics;
using SakuraMod.SakuraModCode.Extensions;
using STS2RitsuLib.Utils;

namespace SakuraMod.SakuraModCode.Cards;

public class ClowFlower() : ClowExtraEffectCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.None)
{
    internal const int ExtraEnergy = 2;

    public override SakuraElementSet Elements => SakuraElementSet.Earth;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new EnergyVar(2)];

    private int? _keptOnTurn;

    protected override PileType GetResultPileTypeForCardPlay()
    {
        var ordinaryResultPile = base.GetResultPileTypeForCardPlay();
        var keepsPlay = FlowerRules.KeepsThisTurn(this, _keptOnTurn);
        if (keepsPlay)
            _keptOnTurn = FlowerRules.CurrentTurn(this);
        return FlowerRules.ResultPileForPlay(keepsPlay, ordinaryResultPile);
    }

    public override bool TryModifyKeywordsInCombat(CardModel card, ISet<CardKeyword> keywords) =>
        FlowerRules.TryRemoveExhaust(this, card, FlowerRules.KeepsThisTurn(this, _keptOnTurn), keywords);

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await PlayerCmd.GainEnergy(ReleasedValue("Energy"), Owner);

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PlayCard(choiceContext, play);
        await PlayerCmd.GainEnergy(ExtraEnergy, Owner);
    }

    protected override void OnUpgrade() => DynamicVars.Energy.UpgradeValueBy(1);
}

public class SakuraFlower() : SakuraFormCard(0, CardType.Skill, TargetType.None)
{
    public override SakuraElementSet Elements => SakuraElementSet.Earth;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new EnergyVar(5)];

    private int? _keptOnTurn;

    protected override PileType GetResultPileTypeForCardPlay()
    {
        var ordinaryResultPile = base.GetResultPileTypeForCardPlay();
        var keepsPlay = FlowerRules.KeepsThisTurn(this, _keptOnTurn);
        if (keepsPlay)
            _keptOnTurn = FlowerRules.CurrentTurn(this);
        return FlowerRules.ResultPileForPlay(keepsPlay, ordinaryResultPile);
    }

    public override bool TryModifyKeywordsInCombat(CardModel card, ISet<CardKeyword> keywords) =>
        FlowerRules.TryRemoveExhaust(this, card, FlowerRules.KeepsThisTurn(this, _keptOnTurn), keywords);

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await PlayerCmd.GainEnergy(ReleasedValue("Energy"), Owner);
}

internal static class FlowerRules
{
    internal static bool IsInEarthyState(CardModel card) =>
        card.Owner?.Creature.GetPower<ClassicEarthyPower>()?.Amount > 0;

    internal static int? CurrentTurn(CardModel card) =>
        card.Owner?.PlayerCombatState?.TurnNumber;

    // Earthy state spares a Flower from Exhaust only on its first play each turn,
    // so returning it to hand (Gravitation, draw loops) cannot farm Energy.
    internal static bool KeepsPlay(bool isInEarthyState, int? keptOnTurn, int? currentTurn) =>
        isInEarthyState && (currentTurn is null || keptOnTurn != currentTurn);

    internal static bool KeepsThisTurn(CardModel card, int? keptOnTurn) =>
        KeepsPlay(IsInEarthyState(card), keptOnTurn, CurrentTurn(card));

    internal static bool TryRemoveExhaust(
        CardModel source,
        CardModel card,
        bool keepsPlay,
        ISet<CardKeyword> keywords) =>
        ReferenceEquals(source, card)
        && keepsPlay
        && keywords.Remove(CardKeyword.Exhaust);

    internal static PileType ResultPileForPlay(
        bool keepsPlay,
        PileType ordinaryResultPile) =>
        keepsPlay && ordinaryResultPile == PileType.Exhaust
            ? PileType.Discard
            : ordinaryResultPile;
}
