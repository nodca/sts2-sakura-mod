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

    // The family orchestration lives here because this is the common prefix of both
    // paths. The activated effect gets the same swing without changing action order;
    // its unblockable follow-up lands during the fade as the same cut deepening.
    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = RequiredTarget(play);
        await SakuraSwordBladeVfx.PlayOrResolveAsync(
            this,
            Owner.Creature,
            [target],
            SwordMode.Single,
            async cues =>
            {
                // Before the damage: the number belongs on the beat the cut opens,
                // which this card lands two stepped frames after the blade passes.
                cues.Impact(target);
                await DealDamage(choiceContext, target, CurrentDamage());
            });
    }

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
            // Enumerated once, and the loop walks that same copy. The visuals need the
            // list the hits walk, and ToList() is an immediate snapshot taken before any
            // damage resolves, so hoisting it cannot change who is struck. Enumerating
            // twice could: the second pass would drop targets killed by the first.
            var targets = SakuraThroughResolution.TargetsFor(play).ToList();
            // Shares the Clow version's presentation verbatim: same session, same
            // parameters. The only difference is how many targets gameplay hands over,
            // which is a gameplay difference, so "shared" needs no branch here.
            await SakuraSwordBladeVfx.PlayOrResolveAsync(
                this,
                Owner.Creature,
                targets,
                SwordMode.Single,
                async cues =>
                {
                    foreach (var target in targets)
                    {
                        cues.Impact(target);
                        await DealDamage(choiceContext, target, ReleasedDamage());
                        await DealDamage(
                            choiceContext,
                            target,
                            target.CurrentHp * ReleasedMagic() / 100,
                            ValueProp.Unblockable);
                    }
                });
        });
    }
}
