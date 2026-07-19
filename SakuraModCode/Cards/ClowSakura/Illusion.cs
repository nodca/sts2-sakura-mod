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

public class ClowIllusion() : ClowExtraEffectCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.None)
{
    public override bool GainsBlock => true;
    public override SakuraElementSet Elements => SakuraElementSet.Wind;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new SakuraSourceBlockVar(7, ValueProp.Move), new DynamicVar("Magic", 2)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await GainBlock(play, ReleasedBlock());
        await GainBlock(play, ReleasedMagic() * (CardPile.Get(PileType.Exhaust, Owner)?.Cards.Count ?? 0) / 3);
    }

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await GainBlock(play, ReleasedBlock());
        await GainBlock(play, ReleasedMagic() * (CardPile.Get(PileType.Exhaust, Owner)?.Cards.Count ?? 0));
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3);
        DynamicVars["Magic"].UpgradeValueBy(1);
    }
}

public class SakuraIllusion() : SakuraFormCard(0, CardType.Skill, TargetType.AnyEnemy)
{
    public override SakuraElementSet Elements => SakuraElementSet.Wind;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust, CardKeyword.Innate];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<WeakPower>(99), new PowerVar<VulnerablePower>(99)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = RequiredTarget(play);
        await ApplyPower<WeakPower>(choiceContext, target, DynamicVars.Weak.IntValue);
        await ApplyPower<VulnerablePower>(choiceContext, target, DynamicVars.Vulnerable.IntValue);
    }
}
