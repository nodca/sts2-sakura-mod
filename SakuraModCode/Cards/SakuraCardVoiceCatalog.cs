using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Character;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
/// Single owner of per-card voice lines. Lines bind to a Source Card identity, so every era of the same
/// card (for example ClowSword and SakuraSword) shares one line. Adding a card = one row + one OGG and
/// its tracked .import under SakuraMod/voices/cards/.
/// </summary>
internal static class SakuraCardVoiceCatalog
{
    private static readonly IReadOnlyDictionary<SourceCardIdentity, SakuraVoiceLine> Lines =
        new Dictionary<SourceCardIdentity, SakuraVoiceLine>
        {
            [SourceCardIdentity.Sword] = new("card:Sword", "voices/cards/sword.ogg", Gain: 0.75f),
        };

    internal static IEnumerable<SakuraVoiceLine> All => Lines.Values;

    internal static SakuraVoiceLine? For(CardModel card) =>
        SakuraCardCatalog.TryGetMetadata(card, out var metadata)
        && metadata.Identity is { } identity
        && Lines.TryGetValue(identity, out var line)
            ? line
            : null;
}
