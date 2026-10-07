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

public class ClowTime() : ClowExtraEffectCard(0, CardType.Skill, CardRarity.Rare, TargetType.None)
{
    public override SakuraElementSet Elements => SakuraElementSet.Water;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(2), new PowerVar<ClassicTimeStasisPower>(1)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await PowerCmd.Apply<ClassicTimeStasisPower>(choiceContext, Owner.Creature, ReleasedValue("ClassicTimeStasisPower"), Owner.Creature, this, false);

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PlayCard(choiceContext, play);
        await CardPileCmd.Draw(choiceContext, ReleasedValue("Cards"), Owner, false);
    }

    protected override void OnUpgrade() => DynamicVars["ClassicTimeStasisPower"].UpgradeValueBy(1);
}

public class SakuraTime() : SakuraFormCard(1, CardType.Skill, TargetType.AllEnemies)
{
    public override SakuraElementSet Elements => SakuraElementSet.Water;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust, CardKeyword.Innate];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<ClassicTimePower>(1)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        foreach (var enemy in CombatState!.HittableEnemies.ToList())
            await SakuraPowerRules.ApplyBypassingArtifact<ClassicTimePower>(choiceContext, enemy, ReleasedValue("ClassicTimePower"), Owner.Creature, this);
        await SakuraMagicCharge.AddVoidToDiscardPile(choiceContext, Owner);
    }
}
