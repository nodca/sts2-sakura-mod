using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using SakuraMod.SakuraModCode.Cards;
using STS2RitsuLib.Utils;

namespace SakuraMod.SakuraModCode.Relics;

public class ClassicFrogRaincoatRelic : SakuraRelicModel
{
    private const int MemoryTrigger = 3;
    private static readonly SavedAttachedState<ClassicFrogRaincoatRelic, int> MemoryRemainder =
        new("SakuraMod_FrogRaincoatMemoryRemainder", () => 0);
    private static readonly SavedAttachedState<ClassicFrogRaincoatRelic, int> PendingReminds =
        new("SakuraMod_FrogRaincoatPendingReminds", () => 0);

    protected override string IconFileName => "frog_raincoat.png";

    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override bool IsAllowedInShops => false;
    public override bool ShowCounter => true;
    public override int DisplayAmount => MemoryRemainder[this];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Trigger", MemoryTrigger),
        new CardsVar(1)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [SakuraCardHoverTips.StaticTip(SakuraCardHoverTips.RemindTipKey)];

    public override bool IsAllowed(IRunState runState) => false;

    public override Task BeforeCombatStart()
    {
        SetMemoryState(0, 0);
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner || PendingReminds[this] <= 0)
            return;

        var triggers = PendingReminds[this];
        PendingReminds[this] = 0;
        for (var i = 0; i < triggers; i++)
        {
            var memory = SakuraMemoryPile.Get(Owner)?.Cards.ToList() ?? [];
            if (memory.Count == 0)
                break;

            Flash();
            var selected = memory.Count == 1
                ? memory[0]
                : (await CardSelectCmd.FromSimpleGrid(
                    choiceContext,
                    memory,
                    Owner,
                    new CardSelectorPrefs(SelectionScreenPrompt, 1))).Single();
            await RememberOne(choiceContext, selected);
        }
    }

    internal void RecordMemoryEntries(int count)
    {
        if (count <= 0)
            return;

        var (remainder, addedTriggers) = Accumulate(MemoryRemainder[this], count, MemoryTrigger);
        SetMemoryState(remainder, PendingReminds[this] + addedTriggers);
        if (addedTriggers > 0)
            Flash();
    }

    internal static (int Remainder, int AddedTriggers) Accumulate(int remainder, int entries, int trigger)
    {
        if (remainder < 0)
            throw new ArgumentOutOfRangeException(nameof(remainder));
        if (entries < 0)
            throw new ArgumentOutOfRangeException(nameof(entries));
        if (trigger <= 0)
            throw new ArgumentOutOfRangeException(nameof(trigger));

        var total = remainder + entries;
        return (total % trigger, total / trigger);
    }

    private async Task RememberOne(PlayerChoiceContext choiceContext, CardModel selected)
    {
        var copies = await SakuraMemoryPile.Consume(Owner, [selected]);
        try
        {
            await SakuraGeneratedCardLifecycle.AddRememberedCardToHand(
                copies.Single(),
                freeThisTurn: true,
                context: choiceContext);
        }
        finally
        {
            SakuraGeneratedCardLifecycle.RemoveDetachedGeneratedChoices(copies);
        }
    }

    private void SetMemoryState(int remainder, int pendingReminds)
    {
        MemoryRemainder[this] = remainder;
        PendingReminds[this] = pendingReminds;
        InvokeDisplayAmountChanged();
    }
}
