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
    internal const int ExtraHpLoss = 15;

    // Vanilla strike sound: the Ironclad's character attack sfx, played by the game
    // when a Strike's attack animation fires (CharacterModel.AttackSfx convention,
    // no override in the Ironclad character model). Sakura's attacks skip the attacker
    // animation, so the sound is carried on the damage instead.
    internal const string StrikeSfx = "event:/sfx/characters/ironclad/ironclad_attack";

    public override SakuraElementSet Elements => SakuraElementSet.Fire;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Loner];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new SakuraSourceDamageVar(6, ValueProp.Move, SourceCardIdentity.Sword)];
    protected override HashSet<CardTag> CanonicalTags => [CardTag.Strike];

    private int CurrentDamage() => SakuraSourceCardValues.EffectiveValue(this, DynamicVars.Damage);

    protected override Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play) =>
        Strike(choiceContext, RequiredTarget(play));

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var target = RequiredTarget(play);
        await Strike(choiceContext, target);
        await DealDamage(choiceContext, target, ExtraHpLoss, ValueProp.Unblockable);
    }

    // Vanilla Strike's hit: the same slash the game's own Strikes spawn.
    private Task Strike(PlayerChoiceContext choiceContext, Creature target) =>
        DealDamage(choiceContext, target, CurrentDamage(), hitSfx: StrikeSfx, hitVfx: SakuraNativeHitFx.Slash);

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
}

public class SakuraSword() : SakuraFormCard(1, CardType.Attack, TargetType.AnyEnemy)
{
    public override SakuraElementSet Elements => SakuraElementSet.Fire;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new SakuraSourceDamageVar(16, ValueProp.Move), new DynamicVar("Magic", 10)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        // Enumerated once, before any damage resolves: enumerating inside the loop
        // could drop targets killed by an earlier hit.
        var targets = PlayTargets(play).ToList();
        foreach (var target in targets)
        {
            await DealDamage(
                choiceContext,
                target,
                ReleasedDamage(),
                hitSfx: ClowSword.StrikeSfx,
                hitVfx: SakuraNativeHitFx.Slash);
            await DealDamage(
                choiceContext,
                target,
                target.CurrentHp * ReleasedMagic() / 100,
                ValueProp.Unblockable);
        }
    }
}
