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

public class Appear() : TransparentExtraEffectCard(1, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Wind, SakuraKeywords.Manifest, CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new CardsVar("ManifestCards", 1)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        var manifested = await SakuraManifestLoop.Manifest(
            this,
            choiceContext,
            DynamicVars["ManifestCards"].IntValue,
            stabilize: true);

        foreach (var copy in manifested)
        {
            if (copy.EnergyCost.CostsX)
                continue;

            if (activation.IsActive)
                copy.SetToFreeThisTurn();
            else
                copy.EnergyCost.AddThisTurnOrUntilPlayed(-1, true);
        }
    }

    protected override void OnUpgrade() => RemoveKeyword(CardKeyword.Exhaust);
}
