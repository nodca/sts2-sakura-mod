using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Cards;

namespace SakuraMod.SakuraModCode.Powers;

public class ClassicThroughPower : SakuraPowerModel
{
    private int _baseClowSources;
    private int _upgradedClowSources;
    private int _sakuraSources;
    private CardModel? _selectedCard;
    private ThroughPlayScope? _scope;
    private bool _usedThisTurn;

    protected override string IconFileName => "lucid_pierce.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    internal ThroughPlayScope? ActiveScope => _scope;

    internal void RegisterClowSource(bool upgraded)
    {
        if (upgraded)
            _upgradedClowSources++;
        else
            _baseClowSources++;
    }

    internal void RegisterSakuraSource() => _sakuraSources++;

    internal int CalculateBonusDamage(int magicCharge) =>
        _baseClowSources * (magicCharge / 2)
        + _upgradedClowSources * magicCharge
        + _sakuraSources * 10;

    public override Task BeforeCardPlayed(CardPlay play)
    {
        if (_selectedCard == play.Card && play.PlayIndex > 0)
        {
            _scope = _scope?.WithActivePlay(play);
            return Task.CompletedTask;
        }

        if (_usedThisTurn
            || play.PlayIndex != 0
            || play.Card.Owner?.Creature != Owner
            || !SakuraThroughResolution.IsEligibleCard(play.Card)
            || play.Target is null)
            return Task.CompletedTask;

        var charge = Owner.GetPower<ClassicMagicChargePower>()?.Amount ?? 0;
        var scope = SakuraThroughResolution.TryCreate(play, charge, CalculateBonusDamage(charge));
        if (scope is null)
            return Task.CompletedTask;

        _usedThisTurn = true;
        _selectedCard = play.Card;
        _scope = scope;
        return Task.CompletedTask;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (_selectedCard == play.Card)
            _scope = play.IsLastInSeries ? null : _scope?.WithoutActivePlay();

        return Task.CompletedTask;
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        var scope = _scope;
        if (scope is null
            || cardSource != scope.Card
            || !scope.Contains(target))
            return;

        if (scope.BonusDamage <= 0)
            return;

        await SakuraThroughResolution.WithPropagationSuppressed(() =>
            CreatureCmd.Damage(
                choiceContext,
                target,
                scope.BonusDamage,
                SakuraPowerValueProps.HpLoss,
                Owner,
                null));
    }

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner.Player)
            ResetTurn();

        return Task.CompletedTask;
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        ResetTurn();
        return Task.CompletedTask;
    }

    private void ResetTurn()
    {
        _usedThisTurn = false;
        _selectedCard = null;
        _scope = null;
    }
}
