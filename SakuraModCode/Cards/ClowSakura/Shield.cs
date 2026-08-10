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

public class ClowShield() : ClowExtraEffectCard(1, CardType.Skill, CardRarity.Basic, TargetType.None)
{
    public override bool GainsBlock => true;
    public override SakuraElementSet Elements => SakuraElementSet.Fire;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Loner];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new SakuraSourceBlockVar(5, ValueProp.Move, SourceCardIdentity.Shield),
        new PowerVar<ClassicShieldWardPower>(SakuraMagicCharge.ShieldMetallicizeBlock)
    ];
    protected override HashSet<CardTag> CanonicalTags => [CardTag.Defend];

    private int CurrentBlock() => SakuraSourceCardValues.EffectiveValue(this, DynamicVars.Block);

    // The session lives here rather than in PlayActivatedCard because this is the
    // common prefix of both paths, so the extra effect gets the plate without the
    // activated path's action order changing. The Metallicize that follows reads as
    // the same forming plate settling, not as a second shield.
    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var shieldVfx = SakuraShieldPlateVfx.TryCreate(Owner.Creature);
        try
        {
            if (shieldVfx is not null)
                await shieldVfx.PlayPrelude(this, Owner.Creature);
            // Before the block: the number belongs on the beat the plate snaps to a
            // stop, matching the other cards in this family.
            shieldVfx?.Impact();
            await GainBlock(play, CurrentBlock());
        }
        finally
        {
            shieldVfx?.FadeAndDispose();
        }
    }

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PlayCard(choiceContext, play);
        await ApplyPower<ClassicShieldWardPower>(choiceContext, Owner.Creature, DynamicVars["ClassicShieldWardPower"].IntValue);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3);
}

public class SakuraShield() : SakuraFormCard(1, CardType.Skill, TargetType.None)
{
    public override bool GainsBlock => true;
    public override SakuraElementSet Elements => SakuraElementSet.Fire;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new SakuraSourceBlockVar(14, ValueProp.Move), new DynamicVar("Magic", 25)];

    internal static int CurrentHpBlock(int currentHp, int rate) =>
        Math.Max(0, currentHp) * Math.Max(0, rate) / 100;

    // Shares the Clow version's presentation verbatim: same session, same parameters.
    // Era reaches the visuals only through the shared prelude's circle colour, so
    // "shared" needs no branch here.
    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var shieldVfx = SakuraShieldPlateVfx.TryCreate(Owner.Creature);
        try
        {
            if (shieldVfx is not null)
                await shieldVfx.PlayPrelude(this, Owner.Creature);
            shieldVfx?.Impact();
            await GainBlock(play, ReleasedBlock());
            await GainBlock(play, CurrentHpBlock(Owner.Creature.CurrentHp, ReleasedMagic()));
        }
        finally
        {
            shieldVfx?.FadeAndDispose();
        }
    }
}
