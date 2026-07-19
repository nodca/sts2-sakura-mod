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

public class Blank() : TransparentExtraEffectCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    private static readonly PileType[] ForgottenTargetPileTypes =
    [
        PileType.Hand,
        PileType.Draw,
        PileType.Discard
    ];

    public override bool GainsBlock => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Earth, CardKeyword.Exhaust];
    internal override IEnumerable<string> ReferencedStaticHoverTipKeys =>
        [SakuraCardHoverTips.TemporaryTipKey];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(4, ValueProp.Move)];
    internal static IReadOnlyList<PileType> TargetPileTypes => ForgottenTargetPileTypes;

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play, SakuraExtraEffectActivation activation)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block.IntValue, ValueProp.Move, play, false);

        var cards = CardPile.GetCards(Owner, ForgottenTargetPileTypes)
            .Where(card => CanGainForgotten(card.Type, card.IsTemporary()))
            .ToList();

        var forgottenCards = 0;
        foreach (var card in cards)
        {
            if (await SakuraGeneratedCardLifecycle.GrantTemporary(choiceContext, card))
                forgottenCards++;
        }

        if (forgottenCards > 0)
        {
            await PowerCmd.Apply<DrawCardsNextTurnPower>(
                choiceContext,
                Owner.Creature,
                forgottenCards,
                Owner.Creature,
                this,
                false);
        }

        if (activation.IsActive)
            await ApplyExtraEffect();
    }

    internal static bool CanGainForgotten(CardType type, bool isForgotten) =>
        !isForgotten && type is CardType.Status or CardType.Curse;

    private async Task ApplyExtraEffect()
    {
        foreach (var power in Owner.Creature.Powers.Where(IsOwnNegativePower).ToList())
            await PowerCmd.Remove(power);

        foreach (var enemy in CombatState!.Enemies)
        {
            foreach (var power in enemy.Powers
                         .Where(IsEnemyPositivePower)
                         .ToList())
                await PowerCmd.Remove(power);
        }
    }

    private static bool IsOwnNegativePower(PowerModel power) =>
        power.Type == PowerType.Debuff
        || IsNegativeStatPower(power);

    private static bool IsEnemyPositivePower(PowerModel power) =>
        IsPositiveStatPower(power)
        || power is ArtifactPower { Amount: > 0 };

    private static bool IsNegativeStatPower(PowerModel power) =>
        power.Amount < 0 && power is StrengthPower or DexterityPower;

    private static bool IsPositiveStatPower(PowerModel power) =>
        power.Amount > 0 && power is StrengthPower or DexterityPower;

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3);
}
