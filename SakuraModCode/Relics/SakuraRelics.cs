using BaseLib.Utils;
using MegaCrit.Sts2.Core;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Events;
using SakuraMod.SakuraModCode.Extensions;

namespace SakuraMod.SakuraModCode.Relics;

public class DreamBookPage : SakuraModRelic
{
    public override RelicRarity Rarity => RelicRarity.Common;

    private bool _usedThisTurn;

    public override Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, CombatState combatState)
    {
        if (player == Owner)
            _usedThisTurn = false;

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (_usedThisTurn || play.Card?.Owner != Owner || play.Card.IsReleased() != true)
            return;

        _usedThisTurn = true;
        await CardPileCmd.Draw(choiceContext, 1, Owner, false);
    }
}

public class DreamRibbon : SakuraModRelic
{
    public override RelicRarity Rarity => RelicRarity.Common;

    private bool _usedThisCombat;

    public override Task BeforeCombatStart()
    {
        _usedThisCombat = false;
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (_usedThisCombat || play.Card?.Owner != Owner || play.Card.IsTemporary() != true)
            return;

        _usedThisCombat = true;
        await PlayerCmd.GainEnergy(1, Owner);
        await CardPileCmd.Draw(choiceContext, 1, Owner, false);
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _usedThisCombat = false;
        return Task.CompletedTask;
    }
}

public class StorageRibbon : SakuraModRelic
{
    public override RelicRarity Rarity => RelicRarity.Common;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(1),
        new CardsVar(1)
    ];

    private bool _usedThisCombat;

    public override Task BeforeCombatStart()
    {
        _usedThisCombat = false;
        return Task.CompletedTask;
    }

    public async Task AfterTemporaryStabilized(PlayerChoiceContext choiceContext, CardModel card)
    {
        if (_usedThisCombat || card.Owner != Owner)
            return;

        _usedThisCombat = true;
        await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner, false);
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _usedThisCombat = false;
        return Task.CompletedTask;
    }
}

public class CatalogNewPage : SakuraModRelic
{
    public override RelicRarity Rarity => RelicRarity.Common;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(2, ValueProp.Move)];

    public async Task AfterCatalogedClearCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (play.Card?.Owner != Owner)
            return;

        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block.IntValue, ValueProp.Move, play, false);
    }
}

public class KeroCharm : SakuraModRelic
{
    public override RelicRarity Rarity => RelicRarity.Common;

    private bool _usedThisCombat;

    public override Task BeforeCombatStart()
    {
        _usedThisCombat = false;
        return Task.CompletedTask;
    }

    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, CombatState combatState)
    {
        if (_usedThisCombat || player != Owner)
            return;

        _usedThisCombat = true;
        var advice = combatState.CreateCard<KeroAdvice>(Owner);
        await SakuraActions.AddGeneratedCardToCombat(
            advice,
            new SakuraActions.GeneratedCardOptions
            {
                AddTemporary = true
            },
            choiceContext);
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _usedThisCombat = false;
        return Task.CompletedTask;
    }
}

public class YueMagicCrystal : SakuraModRelic
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    private int _clearCardsPlayed;

    public override Task BeforeCombatStart()
    {
        _clearCardsPlayed = 0;
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (play.Card?.Owner != Owner || !SakuraActions.IsManifestableClearCard(play.Card))
            return;

        _clearCardsPlayed++;
        if (_clearCardsPlayed % 3 == 0)
            await PlayerCmd.GainEnergy(1, Owner);
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _clearCardsPlayed = 0;
        return Task.CompletedTask;
    }
}

public class BaguaCompass : SakuraModRelic
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    private bool _usedThisTurn;

    public override Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, CombatState combatState)
    {
        if (player == Owner)
        {
            _usedThisTurn = false;
            SakuraActions.ClearPlayedElements(player);
        }

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (_usedThisTurn || play.Card?.Owner != Owner || !SakuraActions.IsClearCard(play.Card))
            return;

        _usedThisTurn = true;
        var element = SakuraActions.RandomElement(Owner);
        SakuraActions.GrantElementsThisTurn(play.Card, element.ToSet());
        await SakuraActions.RememberPlayedElements(play);
    }
}

public class TomoyoSewingKit : SakuraModRelic
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(4, ValueProp.Move)];

    private bool _usedThisTurn;

    public override Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, CombatState combatState)
    {
        if (player == Owner)
            _usedThisTurn = false;

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (_usedThisTurn || play.Card?.Owner != Owner || !SakuraActions.IsPartner(play.Card))
            return;

        _usedThisTurn = true;
        await CreatureCmd.GainBlock(Owner.Creature, 4, ValueProp.Move, play, false);
        CardPile.Get(PileType.Hand, Owner)!.Cards
            .FirstOrDefault(card => card.IsTemporary())
            ?.SetToFreeThisTurn();
    }
}

public class DreamJournal : SakuraModRelic
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    private bool _usedThisTurn;

    public override Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, CombatState combatState)
    {
        if (player == Owner)
            _usedThisTurn = false;

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (_usedThisTurn || play.Card?.Owner != Owner || play.Card.IsReleased() != true)
            return;

        _usedThisTurn = true;
        await SakuraActions.Manifest(Owner, choiceContext, 1);
    }
}

public class ClearCardCase : SakuraModRelic
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (play.Card?.Owner != Owner || !SakuraActions.IsManifestableClearCard(play.Card) || !play.Card.IsTemporary())
            return;

        await CreatureCmd.GainBlock(Owner.Creature, 2, MegaCrit.Sts2.Core.ValueProps.ValueProp.Move, play, false);
    }
}

public class SakuraIntuition : SakuraModRelic
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new IntVar("Choices", 1)];

    public int AdditionalManifestChoices => DynamicVars["Choices"].IntValue;
}

public class DreamKeyTrueForm : SakuraModRelic
{
    private static readonly SavedSpireField<DreamKeyTrueForm, bool> OpeningManifestCompleted =
        new(() => false, "SakuraMod_OpeningManifestCompleted");

    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override bool IsAllowedInShops => false;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(2)];

    private bool _openingManifestInProgress;

    public override Task AfterMapGenerated(ActMap map, int actIndex)
    {
        SakuraStarterEventReplacements.RemoveVanillaEventsFromAct(Owner?.RunState, actIndex);
        return Task.CompletedTask;
    }

    public override Task BeforeCombatStart()
    {
        OpeningManifestCompleted[this] = false;
        _openingManifestInProgress = false;
        return Task.CompletedTask;
    }

    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, CombatState combatState)
    {
        if (OpeningManifestCompleted[this] || _openingManifestInProgress || player != Owner)
            return;

        _openingManifestInProgress = true;
        try
        {
            var source = Owner.Deck.Cards.OfType<SakuraModCard>().FirstOrDefault();
            if (source is not null)
                await SakuraActions.Manifest(source, choiceContext, 2);

            OpeningManifestCompleted[this] = true;
        }
        finally
        {
            _openingManifestInProgress = false;
        }
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        OpeningManifestCompleted[this] = false;
        _openingManifestInProgress = false;
        return Task.CompletedTask;
    }
}

public class KaitoPocketWatch : SakuraModRelic
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override bool IsAllowedInShops => false;

    public override Task AfterObtained()
    {
        Owner.MaxEnergy += 1;
        return Task.CompletedTask;
    }

    public override Task AfterRemoved()
    {
        Owner.MaxEnergy -= 1;
        return Task.CompletedTask;
    }
}

public class AkihoAliceBook : SakuraModRelic
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override bool IsAllowedInShops => false;
    public override string PackedIconPath => "alice_in_clockland.png".RelicImagePath();
    protected override string PackedIconOutlinePath => "alice_in_clockland_outline.png".RelicImagePath();
    protected override string BigIconPath => "alice_in_clockland.png".BigRelicImagePath();

    private bool _dazedAddedThisCombat;
    private bool _releasedThisTurn;

    public override Task BeforeCombatStart()
    {
        _dazedAddedThisCombat = false;
        _releasedThisTurn = false;
        return Task.CompletedTask;
    }

    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, CombatState combatState)
    {
        if (player == Owner)
            _releasedThisTurn = false;
        if (_dazedAddedThisCombat || player != Owner)
            return;

        _dazedAddedThisCombat = true;
        var dazed = Enumerable.Range(0, 3)
            .Select(_ => ModelDb.Card<Dazed>().CreateClone())
            .ToList();
        await CardPileCmd.AddGeneratedCardsToCombat(dazed, PileType.Draw, true, CardPilePosition.Random);
        await CardPileCmd.Shuffle(choiceContext, Owner);
    }

    public override async Task BeforeCardPlayed(CardPlay play)
    {
        if (_releasedThisTurn
            || play.Card?.Owner != Owner
            || !SakuraActions.IsManifestableClearCard(play.Card))
            return;

        _releasedThisTurn = true;
        await SakuraActions.ReleaseThisTurnAndRecord(play.Card);
        SakuraActions.RememberPlayedReleasedCard(play);
    }

    public override Task AfterTurnEnd(PlayerChoiceContext choiceContext, CombatSide side)
    {
        if (Owner.Creature.Side == side)
            _releasedThisTurn = false;

        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _dazedAddedThisCombat = false;
        _releasedThisTurn = false;
        return Task.CompletedTask;
    }
}

public class NamelessBookTruth : SakuraModRelic
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override bool IsAllowedInShops => false;
}
