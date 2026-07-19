using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.Extensions;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;
using CoreVoid = MegaCrit.Sts2.Core.Models.Cards.Void;

namespace SakuraMod.SakuraModCode.Relics;

internal static class SakuraRelicCombatActions
{
    public static async Task ApplyMagicChargeIfSealedBook(
        SakuraRelicModel relic,
        PlayerChoiceContext choiceContext,
        int amount)
    {
        if (relic.Owner.GetRelic<ClassicSealedBookRelic>() is null)
            return;

        relic.Flash();
        await SakuraMagicCharge.GainMagic(choiceContext, relic.Owner, amount);
    }

    public static async Task MarkRandomEnemy(
        SakuraRelicModel relic,
        PlayerChoiceContext choiceContext,
        int amount)
    {
        var target = relic.Owner.Creature.CombatState?.HittableEnemies.ToList() is { Count: > 0 } enemies
            ? relic.Owner.RunState.Rng.CombatCardSelection.NextItem(enemies)
            : null;
        if (target is null)
            return;

        relic.Flash();
        await PowerCmd.Apply<ClassicCerberusMarkPower>(
            choiceContext,
            target,
            amount,
            relic.Owner.Creature,
            null,
            false);
    }

    public static void UpgradeRandomHandCards(SakuraRelicModel relic, int upgrades)
    {
        var upgradable = CardPile.GetCards(relic.Owner, PileType.Hand)
            .Where(static card => !card.IsUpgraded && card.MaxUpgradeLevel > card.CurrentUpgradeLevel)
            .ToList();
        if (upgradable.Count == 0)
            return;

        relic.Flash();
        for (var i = 0; i < upgrades && upgradable.Count > 0; i++)
        {
            var card = relic.Owner.RunState.Rng.CombatCardSelection.NextItem(upgradable);
            if (card is null)
                return;

            upgradable.Remove(card);
            CardCmd.Upgrade(card, CardPreviewStyle.None);
        }
    }
}

