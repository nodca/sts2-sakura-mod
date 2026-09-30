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
        var hits = await ExhaustSongCards(choiceContext);
        await Sing(choiceContext, SongBeatSchedule.EchoBeats(hits ?? []));
    }

    protected override async Task PlayActivatedCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var hits = await ExhaustSongCards(choiceContext);
        if (hits is null)
            return;

        await Sing(choiceContext, SongBeatSchedule.EchoBeats(hits, ExtraHits));
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(2);

    /// <summary>Exhausts the chosen cards and returns each one's hit count, in exhaust order.</summary>
    private async Task<IReadOnlyList<int>?> ExhaustSongCards(PlayerChoiceContext choiceContext)
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

        var hits = selected.Select(SongCount).ToList();
        foreach (var card in selected)
            await CardCmd.Exhaust(choiceContext, card);

        return hits;
    }

    /// <summary>Hits one exhausted card sings: the Voice card's Echo adds <c>Magic</c> more.</summary>
    internal static int SongCount(CardModel card) =>
        card is SakuraSourceCard { Identity: SourceCardIdentity.Voice } voice
        && voice.DynamicVars.TryGetValue("Magic", out var magic)
            ? 1 + magic.IntValue
            : 1;

    private Task Sing(PlayerChoiceContext choiceContext, bool[] echoes)
    {
        var count = echoes.Length;
        if (count <= 0)
            return Task.CompletedTask;

        // The staff opens only after the exhaust selection: the selection is an
        // unbounded player interaction and the session is bounded by a wall clock.
        return SongStaffVfx.PlayOrResolveAsync(
            this,
            Owner.Creature,
            CombatState!.HittableEnemies.ToList(),
            echoes,
            async cues =>
            {
                for (var i = 0; i < count; i++)
                {
                    var enemies = CombatState!.HittableEnemies.ToList();
                    await cues.Beat(i, enemies);
                    await DealDamageToEnemies(choiceContext, enemies, ReleasedDamage(), hitVfxNode: SongHitFallback(cues));
                }
                cues.Finale();
            });
    }

    /// <summary>The vanilla line burst, only when the staff is not being drawn.</summary>
    internal static Func<Creature, Godot.Node2D?>? SongHitFallback(SongStaffVfx.Cues cues) =>
        cues.IsLive ? null : SakuraNativeHitFx.LineBurst;
}

public class SakuraSong() : SakuraFormCard(1, CardType.Attack, TargetType.AllEnemies)
{
    public override bool GainsBlock => true;
    public override SakuraElementSet Elements => SakuraElementSet.Wind;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new SakuraSourceDamageVar(6, ValueProp.Move), new SakuraSourceBlockVar(3, ValueProp.Move)];

    protected override async Task PlayCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var echoes = SongBeatSchedule.EchoBeats(await ExhaustSongCards(choiceContext));
        var count = echoes.Length;
        if (count <= 0)
            return;

        await SongStaffVfx.PlayOrResolveAsync(
            this,
            Owner.Creature,
            CombatState!.HittableEnemies.ToList(),
            echoes,
            async cues =>
            {
                for (var i = 0; i < count; i++)
                {
                    var enemies = CombatState!.HittableEnemies.ToList();
                    await cues.Beat(i, enemies);
                    await DealDamageToEnemies(choiceContext, enemies, ReleasedDamage(), hitVfxNode: ClowSong.SongHitFallback(cues));
                    await GainBlock(play, ReleasedBlock());
                }
                cues.Finale();
            });
    }

    /// <summary>Exhausts the chosen cards and returns each one's hit count, in exhaust order.</summary>
    private async Task<IReadOnlyList<int>> ExhaustSongCards(PlayerChoiceContext choiceContext)
    {
        var hand = CardPile.GetCards(Owner, PileType.Hand).Where(card => card != this).ToList();
        if (hand.Count == 0)
            return [];

        var selected = (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 0, hand.Count)
            {
                Cancelable = true
            },
            card => hand.Contains(card),
            this)).ToList();

        var hits = selected.Select(ClowSong.SongCount).ToList();
        foreach (var card in selected)
            await CardCmd.Exhaust(choiceContext, card);

        return hits;
    }
}
