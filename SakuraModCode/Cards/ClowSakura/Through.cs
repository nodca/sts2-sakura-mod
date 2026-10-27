using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;

namespace SakuraMod.SakuraModCode.Cards;

public class ClowThrough() : ClowCard(2, CardType.Power, CardRarity.Rare, TargetType.None)
{
    private const int PoisonPerCard = 2;

    public override SakuraElementSet Elements => SakuraElementSet.Earth;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<PoisonPower>(PoisonPerCard)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await ApplyPower<ClassicThroughPower>(choiceContext, Owner.Creature, DynamicVars["PoisonPower"].IntValue);

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public class SakuraThrough() : SakuraFormCard(1, CardType.Power, TargetType.None)
{
    private const int PoisonPerCard = 3;

    public override SakuraElementSet Elements => SakuraElementSet.Earth;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<PoisonPower>(PoisonPerCard)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await ApplyPower<ClassicThroughSakuraPower>(choiceContext, Owner.Creature, DynamicVars["PoisonPower"].IntValue);
}
