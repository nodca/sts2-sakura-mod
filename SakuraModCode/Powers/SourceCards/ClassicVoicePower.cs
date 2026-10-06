using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
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
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Relics;
using SakuraMod.SakuraModCode.Extensions;
using SakuraMod.SakuraModCode.Powers;
using STS2RitsuLib.Combat.HandSize;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;
using STS2RitsuLib.Utils;

namespace SakuraMod.SakuraModCode.Powers;

public class ClassicVoicePower : SakuraPowerModel
{
    public const int BlockBonusPerStack = 3;

    protected override string IconFileName => "voice_power_sakuracard.png";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("BlockBonus", BlockBonusPerStack)];

    internal static int BlockBonus(Creature owner) =>
        (owner.GetPower<ClassicVoicePower>()?.Amount ?? 0) * BlockBonusPerStack;

    // Card previews pass the Voice as cardSource; the turn-end gain itself has no card play,
    // so ClowVoice adds BlockBonus explicitly and this hook never double counts it.
    public override decimal ModifyBlockAdditive(Creature target, decimal block, ValueProp props, CardModel? cardSource, CardPlay? cardPlay) =>
        cardSource is ClowVoice && cardSource.Owner.Creature == Owner && Amount > 0
            ? Amount * BlockBonusPerStack
            : 0m;

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        SyncBlockBonus();
        return Task.CompletedTask;
    }

    public override Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (power == this)
            SyncBlockBonus();
        return Task.CompletedTask;
    }

    private void SyncBlockBonus() =>
        DynamicVars["BlockBonus"].BaseValue = Amount * BlockBonusPerStack;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Owner.Player != player || Amount <= 0)
            return;

        await AddVoicesToHand(player, Amount);
    }

    internal static async Task AddVoicesToHand(Player player, int count)
    {
        var combatState = player.Creature.CombatState
            ?? throw new InvalidOperationException("Sakura Voice generated cards require an active combat.");
        for (var i = 0; i < count; i++)
        {
            var card = combatState.CreateCard<ClowVoice>(player);
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player, CardPilePosition.Random);
        }
    }
}
