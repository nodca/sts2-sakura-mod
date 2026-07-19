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

public class ClowSword() : ClowExtraEffectCard(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
{
    public override SakuraElementSet Elements => SakuraElementSet.Fire;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Loner];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new SakuraSourceDamageVar(6, ValueProp.Move, SourceCardIdentity.Sword)];
    protected override HashSet<CardTag> CanonicalTags => [CardTag.Strike];

    private int CurrentDamage() => SakuraSourceCardValues.EffectiveValue(this, DynamicVars.Damage);

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        await DealDamage(choiceContext, RequiredTarget(play), CurrentDamage());

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PlayCard(choiceContext, play);
        await DealDamage(choiceContext, RequiredTarget(play), SakuraMagicCharge.SwordExtraHpLoss, ValueProp.Unblockable);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
}

public class SakuraSword() : SakuraFormCard(1, CardType.Attack, TargetType.AnyEnemy)
{
    public override SakuraElementSet Elements => SakuraElementSet.Fire;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new SakuraSourceDamageVar(16, ValueProp.Move), new DynamicVar("Magic", 25)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await SakuraThroughResolution.WithPropagationSuppressed(async () =>
        {
            foreach (var target in SakuraThroughResolution.TargetsFor(play))
            {
                await DealDamage(choiceContext, target, ReleasedDamage());
                await DealDamage(choiceContext, target, target.CurrentHp * ReleasedMagic() / 100, ValueProp.Unblockable);
            }
        });
    }
}
