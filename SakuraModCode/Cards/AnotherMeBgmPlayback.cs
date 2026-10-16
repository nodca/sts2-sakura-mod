using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Audio;
using SakuraMod.SakuraModCode;
using STS2RitsuLib.Audio;

namespace SakuraMod.SakuraModCode.Cards;

internal static class AnotherMeBgmPlayback
{
    internal const string RelativePath = "music/another_me.ogg";
    internal const string MusicChannel = $"{MainFile.ModId}.AnotherMeBgm";
    internal const float MusicVolume = 0.3f;
    internal const float FadeInSeconds = CardBgmPlayback.FadeInSeconds;
    internal const float FadeOutSeconds = CardBgmPlayback.FadeOutSeconds;

    private static readonly CardBgmPlayback Track = CardBgmPlayback.CreateTrack(
        new CardBgmPlayback.Config(RelativePath, MusicChannel, MusicVolume, "Another Me"));

    public static void Register() => CardBgmPlayback.RegisterSharedLifecycle();

    internal static bool LifecycleCleanupRegistered => CardBgmPlayback.LifecycleCleanupRegistered;

    public static void TryPlay(CardModel card) => Track.TryPlay(card);

    internal static AudioPlaybackOptions CreatePlaybackOptions() => Track.CreatePlaybackOptions();
}
