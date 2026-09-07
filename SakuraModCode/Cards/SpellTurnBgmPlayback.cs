using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Audio;
using SakuraMod.SakuraModCode;
using STS2RitsuLib.Audio;

namespace SakuraMod.SakuraModCode.Cards;

internal static class SpellTurnBgmPlayback
{
    internal const string ResourcePath = $"{MainFile.ResPath}/music/platinum.ogg";
    internal const string RelativePath = "music/platinum.ogg";
    internal const string MusicChannel = $"{MainFile.ModId}.SpellTurnBgm";
    internal const float MusicVolume = 0.3f;
    internal const float FadeInSeconds = CardBgmPlayback.FadeInSeconds;
    internal const float FadeOutSeconds = CardBgmPlayback.FadeOutSeconds;

    private static readonly CardBgmPlayback Track = CardBgmPlayback.CreateTrack(
        new CardBgmPlayback.Config(ResourcePath, RelativePath, MusicChannel, MusicVolume, "Spell Turn"));

    public static void TryPlay(CardModel card) => Track.TryPlay(card);

    internal static AudioPlaybackOptions CreatePlaybackOptions() => Track.CreatePlaybackOptions();
}
