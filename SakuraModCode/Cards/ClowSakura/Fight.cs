using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
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

public class ClowFight() : ClowExtraEffectCard(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
{
    private const int ExtraStrength = 2;

    public override SakuraElementSet Elements => SakuraElementSet.Earth;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new SakuraSourceDamageVar(9, ValueProp.Move), new DynamicVar("Magic", 1)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await DealDamage(choiceContext, RequiredTarget(play), ReleasedDamage());
        await SakuraMagicCharge.GainMagic(choiceContext, Owner, 1, this);
        var temporaryStrength = ReleasedMagic() + (Owner.Creature.GetPower<SakuraFightPower>()?.Amount ?? 0);
        await ApplyPower<ClassicTemporaryStrengthPower>(choiceContext, Owner.Creature, temporaryStrength);
        await GenerateFight(choiceContext);
    }

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PlayCard(choiceContext, play);
        await ApplyPower<StrengthPower>(choiceContext, Owner.Creature, ExtraStrength);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2);
        DynamicVars["Magic"].UpgradeValueBy(1);
    }

    private async Task GenerateFight(PlayerChoiceContext choiceContext)
    {
        var combatState = CombatState
            ?? throw new InvalidOperationException("Fight generation requires an active combat.");
        var generatedFight = combatState.CreateCard<ClowFight>(Owner);
        while (generatedFight.CurrentUpgradeLevel < CurrentUpgradeLevel && generatedFight.IsUpgradable)
            generatedFight.UpgradeInternal();

        await CardPileCmd.AddGeneratedCardToCombat(
            generatedFight,
            PileType.Hand,
            Owner,
            CardPilePosition.Random);
    }
}

public class SakuraFight() : SakuraFormCard(1, CardType.Power, TargetType.None)
{
    public override SakuraElementSet Elements => SakuraElementSet.Earth;
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromCard<ClowFight>()];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await SakuraFightPower.AddTemporaryUpgradedFight(choiceContext, Owner);
        await ApplyPower<SakuraFightPower>(choiceContext, Owner.Creature, 1);
    }
}
