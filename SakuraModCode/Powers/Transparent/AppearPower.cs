using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Cards;

namespace SakuraMod.SakuraModCode.Powers;

public class AppearPower : SakuraPowerModel
{
    protected override string IconFileName => "appear.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    // Like Infinite Blades, the turn-start card arrives before the hand is drawn.
    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        if (player.Creature != Owner)
            return;

        Flash();
        await SakuraManifestLoop.Manifest(player, choiceContext, Amount);
    }

    // Every Manifested card brings the Spell of its own element, so a Manifest
    // that only offers more Manifests still pays off.
    internal static async Task AfterManifested(Player owner, CardModel manifested)
    {
        if (owner.Creature.GetPower<AppearPower>() is not { } power)
            return;

        var combatState = owner.Creature.CombatState
            ?? throw new InvalidOperationException("Appear Spells require an active combat.");
        foreach (var element in SakuraActions.ElementsOf(manifested))
        {
            for (var i = 0; i < power.Amount; i++)
            {
                var spell = combatState.CreateCard(ModelDb.GetById<CardModel>(ModelDb.GetId(SpellTypeFor(element))), owner);
                await CardPileCmd.AddGeneratedCardToCombat(spell, PileType.Hand, owner, CardPilePosition.Random);
            }
        }
    }

    internal static Type SpellTypeFor(SakuraElement element) =>
        element switch
        {
            SakuraElement.Fire => typeof(SpellHuoShen),
            SakuraElement.Earth => typeof(SpellLeiDi),
            SakuraElement.Wind => typeof(SpellFengHua),
            SakuraElement.Water => typeof(SpellShuiLong),
            _ => throw new ArgumentOutOfRangeException(nameof(element), element, "Appear only maps the four elements.")
        };
}
