using BaseLib.Extensions;
using BaseLib.Patches.Saves;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Rooms;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Extensions;

namespace SakuraMod.SakuraModCode.Relics;

public class DreamKey : SakuraModRelic
{
    private const int ReleaseProgressThreshold = 3;
    private const char ReleaseProgressSeparator = ':';
    private const string ReleaseProgressVar = "ReleaseProgress";

    private static readonly SavedSpireField<DreamKey, bool> OpeningManifestCompleted =
        new(() => false, "SakuraMod_OpeningManifestCompleted");

    private static readonly SavedSpireField<DreamKey, List<string>> ReleaseProgress =
        new(() => new List<string>(), "SakuraMod_DreamKeyReleaseProgress")
        {
            Serializer = SerializeProgress,
            Deserializer = DeserializeProgress
        };

    public override RelicRarity Rarity => RelicRarity.Starter;
    public override string PackedIconPath => "dream_key.png".RelicImagePath();
    protected override string PackedIconOutlinePath => "dream_key_outline.png".RelicImagePath();
    protected override string BigIconPath => "dream_key.png".BigRelicImagePath();

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(1),
        new DreamKeyReleaseProgressVar()
    ];

    public override RelicModel GetUpgradeReplacement() =>
        ModelDb.Relic<DreamKeyTrueForm>();

    internal static int OpeningRareAtlasChoices(ICombatState combatState) =>
        combatState.Encounter?.RoomType is RoomType.Elite or RoomType.Boss ? 1 : 0;

    private bool _openingManifestInProgress;
    private bool _releaseProgressRecordedThisCombat;

    public static void Register()
    {
        ExtendedSaveTypes.RegisterListSaveType<string>();
        _ = ReleaseProgress;
    }

    public static void RecordReleasedCard(CardModel card)
    {
        if (card.Owner?.GetRelic<DreamKey>() is not { } dreamKey)
            return;

        dreamKey.RecordReleaseProgress(card);
    }

    public override Task AfterMapGenerated(ActMap map, int actIndex)
    {
        SakuraStarterCompatibility.RemoveIncompatibleVanillaStarterEvents(Owner?.RunState, actIndex);
        return Task.CompletedTask;
    }

    public override Task BeforeCombatStart()
    {
        OpeningManifestCompleted[this] = false;
        _openingManifestInProgress = false;
        _releaseProgressRecordedThisCombat = false;
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
                    1,
                    rareAtlasChoices: OpeningRareAtlasChoices(combatState));

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
        _releaseProgressRecordedThisCombat = false;
        return Task.CompletedTask;
    }

    private void RecordReleaseProgress(CardModel releasedCard)
    {
        if (_releaseProgressRecordedThisCombat)
            return;

        if (releasedCard.Owner != Owner || !SakuraCardCatalog.IsTransparentCard(releasedCard))
            return;

        var deckCard = DirectDeckCardSourceOf(releasedCard);
        if (deckCard is null || deckCard.Owner != Owner || !SakuraCardCatalog.IsTransparentCard(deckCard))
            return;

        var clearCardType = deckCard.GetType();
        if (UnreleasedDeckCards(clearCardType).Count == 0)
            return;

        _releaseProgressRecordedThisCombat = true;
        var progress = ProgressByCardId(this);
        var cardId = deckCard.Id.Entry;
        progress.TryGetValue(cardId, out var currentProgress);
        currentProgress++;
        if (currentProgress < ReleaseProgressThreshold)
        {
            SaveProgress(this, progress, cardId, currentProgress);
            return;
        }

        currentProgress -= ReleaseProgressThreshold;
        PermanentlyReleaseNextDeckCard(clearCardType);
        SaveProgress(this, progress, cardId, currentProgress);
    }

    private static CardModel? DirectDeckCardSourceOf(CardModel card)
    {
        if (card.Pile?.Type == PileType.Deck)
            return card;

        return card.DeckVersion;
    }

    private List<CardModel> UnreleasedDeckCards(Type clearCardType) =>
        Owner.Deck.Cards
            .Where(card => card.GetType() == clearCardType && SakuraCardCatalog.IsTransparentCard(card) && !card.IsReleased())
            .ToList();

    private void PermanentlyReleaseNextDeckCard(Type clearCardType)
    {
        var target = UnreleasedDeckCards(clearCardType)
            .OrderByDescending(card => card.CurrentUpgradeLevel)
            .FirstOrDefault();
        target?.MakeReleasePermanent();
    }

    private static Dictionary<string, int> ProgressByCardId(DreamKey dreamKey)
    {
        Dictionary<string, int> progressByCardId = [];
        foreach (var entry in ReleaseProgressEntries(dreamKey))
        {
            var separatorIndex = entry.LastIndexOf(ReleaseProgressSeparator);
            if (separatorIndex <= 0 || separatorIndex == entry.Length - 1)
                continue;

            var cardId = entry[..separatorIndex];
            if (!int.TryParse(entry[(separatorIndex + 1)..], out var progress))
                continue;

            if (progress > 0)
                progressByCardId[cardId] = progress;
        }

        return progressByCardId;
    }

    private static void SaveProgress(
        DreamKey dreamKey,
        Dictionary<string, int> progressByCardId,
        string cardId,
        int progress)
    {
        if (progress > 0)
            progressByCardId[cardId] = progress;
        else
            progressByCardId.Remove(cardId);

        ReleaseProgress[dreamKey] = progressByCardId
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => $"{entry.Key}{ReleaseProgressSeparator}{entry.Value}")
            .ToList();
    }

    private static List<string> ReleaseProgressEntries(DreamKey dreamKey)
    {
        var progress = ReleaseProgress[dreamKey];
        if (progress is not null)
            return progress;

        progress = [];
        ReleaseProgress[dreamKey] = progress;
        return progress;
    }

    private string ReleaseProgressLine()
    {
        var progress = ProgressByCardId(this);
        if (progress.Count == 0)
            return "";

        var separator = L10NLookup($"{Id.Entry}.progressSeparator").GetFormattedText();
        var entries = progress
            .Select(entry => $"{CardTitleFor(entry.Key)} {entry.Value}/{ReleaseProgressThreshold}");

        var line = L10NLookup($"{Id.Entry}.progress");
        line.Add("Progress", string.Join(separator, entries));
        return line.GetFormattedText();
    }

    private static string CardTitleFor(string cardId)
    {
        foreach (var type in SakuraCardCatalog.TransparentCardTypes)
        {
            var template = ModelDb.GetById<CardModel>(ModelDb.GetId(type));
            if (template.Id.Entry == cardId)
                return template.Title;
        }

        return cardId;
    }

    private static void SerializeProgress(List<string> progress, PacketWriter writer)
    {
        writer.WriteInt(progress.Count);
        foreach (var entry in progress)
            writer.WriteString(entry);
    }

    private static List<string> DeserializeProgress(PacketReader reader)
    {
        var count = reader.ReadInt();
        List<string> progress = [];
        for (var i = 0; i < count; i++)
            progress.Add(reader.ReadString());
        return progress;
    }

    private sealed class DreamKeyReleaseProgressVar() : DynamicVar(ReleaseProgressVar, 0)
    {
        public override string ToString() =>
            _owner is DreamKey dreamKey ? dreamKey.ReleaseProgressLine() : "";
    }
}
