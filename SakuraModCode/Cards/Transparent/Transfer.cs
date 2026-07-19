using MegaCrit.Sts2.Core;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.Extensions;
using STS2RitsuLib.Combat.HandSize;

namespace SakuraMod.SakuraModCode.Cards;

public class Transfer() : TransparentExtraEffectCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Wind, CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("EnemyStrengthLoss", 2),
        new DynamicVar("StrengthGain", 1),
        new DynamicVar("DexterityGain", 1)
    ];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        ApplyExtraEffectExhaustChange(activation);
        await SakuraThroughResolution.WithPropagationSuppressed(async () =>
        {
            foreach (var target in SakuraThroughResolution.TargetsFor(play))
            {
                await PowerCmd.Apply<StrengthPower>(choiceContext, target, -DynamicVars["EnemyStrengthLoss"].IntValue, Owner.Creature, this, false);
                await PowerCmd.Apply<StrengthPower>(choiceContext, Owner.Creature, DynamicVars["StrengthGain"].IntValue, Owner.Creature, this, false);
                await PowerCmd.Apply<DexterityPower>(choiceContext, Owner.Creature, DynamicVars["DexterityGain"].IntValue, Owner.Creature, this, false);
            }
        });
    }

    protected override PileType GetResultPileTypeForCardPlay() =>
        SakuraCardModel.UsesMagicChargeExtraEffect(this)
            ? PileType.Discard
            : base.GetResultPileTypeForCardPlay();

    private void ApplyExtraEffectExhaustChange(SakuraExtraEffectActivation activation)
    {
        if (activation.IsActive)
            RemoveKeywordIfPresent(CardKeyword.Exhaust);
        else
            AddKeywordIfMissing(CardKeyword.Exhaust);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["EnemyStrengthLoss"].UpgradeValueBy(1);
        DynamicVars["StrengthGain"].UpgradeValueBy(1);
    }
}



































