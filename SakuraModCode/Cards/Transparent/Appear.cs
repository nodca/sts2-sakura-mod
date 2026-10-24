using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Extensions;
using SakuraMod.SakuraModCode.Powers;
using STS2RitsuLib.Cards.DynamicVars;

namespace SakuraMod.SakuraModCode.Cards;

public class Appear() : TransparentExtraEffectCard(0, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Wind, SakuraKeywords.Manifest, CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new CardsVar("ManifestCards", 1), new DynamicVar("ManifestChoices", SakuraManifestLoop.BaseManifestChoiceCount)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        // Appear keeps what it reveals, so a wider reveal on upgrade matters
        // more than a second card would.
        var manifested = await SakuraManifestLoop.Manifest(
            this,
            choiceContext,
            DynamicVars["ManifestCards"].IntValue,
            stabilize: true,
            choiceCount: DynamicVars["ManifestChoices"].IntValue);
        if (!activation.IsActive)
            return;

        foreach (var copy in manifested)
        {
            if (!copy.EnergyCost.CostsX)
                copy.EnergyCost.AddThisTurnOrUntilPlayed(-1, true);
        }
    }

    protected override void OnUpgrade() => DynamicVars["ManifestChoices"].UpgradeValueBy(2);
}
