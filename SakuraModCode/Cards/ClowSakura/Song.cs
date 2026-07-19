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

public class ClowSong() : ClowExtraEffectCard(0, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
{
    private const int ExtraHits = 2;

    public override SakuraElementSet Elements => SakuraElementSet.Wind;
    protected override bool HasEnergyCostX => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new SakuraSourceDamageVar(4, ValueProp.Move), new DynamicVar("ExtraHits", ExtraHits)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var count = await ExhaustSongCards(choiceContext) ?? 0;
        await Sing(choiceContext, count);
    }

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var exhausted = await ExhaustSongCards(choiceContext);
        if (exhausted is null)
            return;

        var count = exhausted.Value + ExtraHits;
        await Sing(choiceContext, count);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(2);

    private async Task<int?> ExhaustSongCards(PlayerChoiceContext choiceContext)
    {
        var hand = CardPile.GetCards(Owner, PileType.Hand).Where(card => card != this).ToList();
        if (hand.Count == 0)
            return null;

        var maxSelect = Math.Min(hand.Count, ResolveEnergyXValue() + 1);
        if (maxSelect <= 0)
            return null;

        var selected = (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 0, maxSelect)
            {
                Cancelable = true
            },
            card => hand.Contains(card),
            this)).ToList();

        var count = selected.Sum(SongCount);
        foreach (var card in selected)
            await CardCmd.Exhaust(choiceContext, card);

        return count;
    }

    private int SongCount(CardModel card) =>
        card is SakuraSourceCard { Identity: SourceCardIdentity.Voice } voice
        && voice.DynamicVars.TryGetValue("Magic", out var magic)
            ? 1 + magic.IntValue
            : 1;

    private async Task Sing(PlayerChoiceContext choiceContext, int count)
    {
        for (var i = 0; i < count; i++)
            await DealDamageToEnemies(choiceContext, CombatState!.HittableEnemies.ToList(), ReleasedDamage());
    }
}

public class SakuraSong() : SakuraFormCard(1, CardType.Attack, TargetType.AllEnemies)
{
    public override bool GainsBlock => true;
    public override SakuraElementSet Elements => SakuraElementSet.Wind;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new SakuraSourceDamageVar(6, ValueProp.Move), new SakuraSourceBlockVar(3, ValueProp.Move)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var count = await ExhaustSongCards(choiceContext);
        for (var i = 0; i < count; i++)
        {
            await DealDamageToEnemies(choiceContext, CombatState!.HittableEnemies.ToList(), ReleasedDamage());
            await GainBlock(play, ReleasedBlock());
        }
    }

    private async Task<int> ExhaustSongCards(PlayerChoiceContext choiceContext)
    {
        var hand = CardPile.GetCards(Owner, PileType.Hand).Where(card => card != this).ToList();
        if (hand.Count == 0)
            return 0;

        var selected = (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 0, hand.Count)
            {
                Cancelable = true
            },
            card => hand.Contains(card),
            this)).ToList();

        var count = selected.Sum(SongCount);
        foreach (var card in selected)
            await CardCmd.Exhaust(choiceContext, card);

        return count;
    }

    private int SongCount(CardModel card) =>
        card is SakuraSourceCard { Identity: SourceCardIdentity.Voice } voice
        && voice.DynamicVars.TryGetValue("Magic", out var magic)
            ? 1 + magic.IntValue
            : 1;
}
