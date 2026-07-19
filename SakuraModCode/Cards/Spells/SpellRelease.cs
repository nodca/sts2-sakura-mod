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

public class SpellRelease() : SpellCard(1, CardType.Skill, CardRarity.Basic, TargetType.None)
{
    private static LocString Prompt => CardLoc<SpellRelease>("selectionPrompt");
    private const float ReleaseRate = 0.5f;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<VulnerablePower>(1)];
    public override int MaxUpgradeLevel => 0;

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        SakuraVoicePlayback.TryPlay(this);
        var choices = CardPile.GetCards(Owner, PileType.Hand)
            .Where(CanRelease)
            .ToList();
        if (choices.Count > 0)
        {
            var selected = (await CardSelectCmd.FromHand(
                choiceContext,
                Owner,
                new CardSelectorPrefs(Prompt, 1)
                {
                    Cancelable = false,
                    RequireManualConfirmation = false
                },
                card => choices.Contains(card),
                this)).FirstOrDefault();

            if (selected is not null)
                ApplyRelease(selected);
        }

        await PowerCmd.Apply<VulnerablePower>(
            choiceContext,
            CombatState!.HittableEnemies.ToList(),
            DynamicVars.Vulnerable.IntValue,
            Owner.Creature,
            this,
            false);
    }

    private void ApplyRelease(CardModel selected)
    {
        var swordJade = Owner.GetRelic<ClassicSwordJadeRelic>();
        if (swordJade is not null)
        {
            swordJade.Flash();
            SakuraReleaseState.Apply(selected, ClassicSwordJadeRelic.ReleaseRate);
            return;
        }

        SakuraReleaseState.Apply(selected, ReleaseRate);
    }

    internal static bool CanRelease(CardModel card) =>
        SakuraCardCatalog.TryGetMetadata(card, out var metadata)
        && metadata.Era is SourceEraClass.Clow or SourceEraClass.Sakura or SourceEraClass.Clear;
}

