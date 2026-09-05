using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.FourthAct.Fire.Powers;
using STS2RitsuLib.Scaffolding.Content;

namespace SakuraMod.SakuraModCode.FourthAct.Fire.Cards;

public sealed class Balance() : ModCardTemplate(1, CardType.Status, CardRarity.Status, TargetType.Self, false), ISakuraClearLayoutCard
{
    public CardType DescriptionShapeCardType => CardType.Skill;
    public override CardPoolModel Pool => ModelDb.CardPool<ClassicSakuraCardPool>();
    public override bool CanBeGeneratedInCombat => false;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    public override string CustomPortraitPath => CardModel.MissingPortraitPath;
    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play) => LibraPendulumPower.Recenter(choiceContext, Owner.Creature.CombatState);
}
