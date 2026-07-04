using BaseLib.Utils;
using MegaCrit.Sts2.Core;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
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
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Extensions;

namespace SakuraMod.SakuraModCode.Relics;

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

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (play.Card?.Owner != Owner || !SakuraCardCatalog.IsTransparentCard(play.Card) || !play.Card.IsTemporary())
            return;

        await GainPageBlock(choiceContext, play);
    }

    public async Task AfterCatalogedClearCard(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (play.Card?.Owner != Owner)
            return;

        await GainPageBlock(choiceContext, play);
    }

    private async Task GainPageBlock(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block.IntValue, ValueProp.Move, play, false);
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
        if (play.Card?.Owner != Owner || !SakuraCardCatalog.IsTransparentCard(play.Card))
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
    public override string PackedIconPath => "bagua_compass.png".RelicImagePath();
    protected override string PackedIconOutlinePath => "bagua_compass_outline.png".RelicImagePath();
    protected override string BigIconPath => "bagua_compass.png".BigRelicImagePath();

    private bool _usedThisTurn;

    public override Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
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
        if (_usedThisTurn || play.Card?.Owner != Owner || !SakuraCardCatalog.IsTransparentCard(play.Card))
            return;

        _usedThisTurn = true;
        var element = SakuraActions.RandomElement(Owner);
        SakuraActions.GrantElementsThisTurn(play.Card, element.ToSet());
        await SakuraActions.RememberPlayedElements(choiceContext, play);
    }
}

public class TomoyoSewingKit : SakuraModRelic
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(4, ValueProp.Move)];

    private bool _usedThisTurn;

    public override Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        if (player == Owner)
            _usedThisTurn = false;

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (_usedThisTurn || play.Card?.Owner != Owner || !SakuraCardCatalog.IsPartnerCard(play.Card))
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

    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];

    private bool _usedThisTurn;

    public override Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
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
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner, false);
        await SakuraManifestLoop.Manifest(Owner, choiceContext, 1);
    }
}

public class SakuraIntuition : SakuraModRelic
{
    public override RelicRarity Rarity => RelicRarity.Rare;
    public override string PackedIconPath => "sakura_intuition.png".RelicImagePath();
    protected override string PackedIconOutlinePath => "sakura_intuition_outline.png".RelicImagePath();
    protected override string BigIconPath => "sakura_intuition.png".BigRelicImagePath();

    protected override IEnumerable<DynamicVar> CanonicalVars => [new IntVar("Choices", 1)];

    public int AdditionalManifestChoices => DynamicVars["Choices"].IntValue;
}

public class DreamKeyTrueForm : SakuraModRelic
{
    private static readonly SavedSpireField<DreamKeyTrueForm, bool> OpeningManifestCompleted =
        new(() => false, "SakuraMod_OpeningManifestCompleted");

    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override bool IsAllowedInShops => false;
    public override string PackedIconPath => "dream_key.png".RelicImagePath();
    protected override string PackedIconOutlinePath => "dream_key_outline.png".RelicImagePath();
    protected override string BigIconPath => "dream_key.png".BigRelicImagePath();

    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(2)];

    private bool _openingManifestInProgress;

    public override Task AfterMapGenerated(ActMap map, int actIndex)
    {
        SakuraStarterCompatibility.RemoveIncompatibleVanillaStarterEvents(Owner?.RunState, actIndex);
        return Task.CompletedTask;
    }

    public override Task BeforeCombatStart()
    {
        OpeningManifestCompleted[this] = false;
        _openingManifestInProgress = false;
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (OpeningManifestCompleted[this] || _openingManifestInProgress || player != Owner)
            return;

        _openingManifestInProgress = true;
        try
        {
            var combatState = player.Creature.CombatState;
            if (combatState is null)
                return;

            var source = Owner.Deck.Cards.OfType<SakuraModCard>().FirstOrDefault();
            if (source is not null)
                await SakuraManifestLoop.Manifest(
                    source,
                    choiceContext,
                    2,
                    rareAtlasChoices: DreamKey.OpeningRareAtlasChoices(combatState));

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
    public override string PackedIconPath => "kaito_pocket_watch.png".RelicImagePath();
    protected override string PackedIconOutlinePath => "kaito_pocket_watch_outline.png".RelicImagePath();
    protected override string BigIconPath => "kaito_pocket_watch.png".BigRelicImagePath();

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

    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        if (player == Owner)
            _releasedThisTurn = false;
        if (_dazedAddedThisCombat || player != Owner)
            return;

        _dazedAddedThisCombat = true;
        var dazed = Enumerable.Range(0, 3)
            .Select(_ => combatState.CreateCard<Dazed>(Owner))
            .ToList();
        CardCmd.PreviewCardPileAdd(
            await SakuraGeneratedCardLifecycle.AddGeneratedCardsToCombatWithResults(dazed, PileType.Draw, Owner, CardPilePosition.Random));
        await CardPileCmd.Shuffle(choiceContext, Owner);
    }

    public override async Task BeforeCardPlayed(CardPlay play)
    {
        if (_releasedThisTurn
            || play.Card?.Owner != Owner
            || !SakuraCardCatalog.IsTransparentCard(play.Card))
            return;

        _releasedThisTurn = true;
        await SakuraActions.ReleaseThisTurnAndRecord(new ThrowingPlayerChoiceContext(), play.Card);
        SakuraActions.RememberPlayedReleasedCard(play);
    }

    public override Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner.Creature.Side == side && participants.Contains(Owner.Creature))
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
    public override string PackedIconPath => "nameless_book_truth.png".RelicImagePath();
    protected override string PackedIconOutlinePath => "nameless_book_truth_outline.png".RelicImagePath();
    protected override string BigIconPath => "nameless_book_truth.png".BigRelicImagePath();
}
