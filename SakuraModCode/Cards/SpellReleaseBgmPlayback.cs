using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using SakuraMod.SakuraModCode;
using STS2RitsuLib;
using STS2RitsuLib.Audio;

namespace SakuraMod.SakuraModCode.Cards;

internal static class SpellReleaseBgmPlayback
{
    internal static readonly IReadOnlyList<string> RelativePaths = Array.AsReadOnly(new[]
    {
        "music/release.ogg",
        "music/release_2.ogg",
        "music/release_3.ogg",
        "music/release_4.ogg",
        "music/release_5.ogg"
    });
    internal const string MusicChannel = $"{MainFile.ModId}.SpellReleaseBgm";
    internal const float MusicVolume = 0.32f;
    internal const float FadeInSeconds = CardBgmPlayback.FadeInSeconds;
    internal const float FadeOutSeconds = CardBgmPlayback.FadeOutSeconds;

    private static readonly HashSet<ICombatState> TriggeredCombats = [];
    private static bool _lifecycleRegistered;

    private static readonly CardBgmPlayback[] Tracks = RelativePaths.Select(path =>
        CardBgmPlayback.CreateTrack(new CardBgmPlayback.Config(
            $"{MainFile.ResPath}/{path}", path, MusicChannel, MusicVolume, $"Spell Release ({path})"))).ToArray();

    public static void Register()
    {
        CardBgmPlayback.RegisterSharedLifecycle();
        if (_lifecycleRegistered)
            return;

        RitsuLibFramework.SubscribeLifecycle<CombatEndedEvent>(
            _ => ResetPerCombat(),
            replayCurrentState: false);
        RitsuLibFramework.SubscribeLifecycle<RunEndedEvent>(
            _ => ResetPerCombat(),
            replayCurrentState: false);
        _lifecycleRegistered = true;
    }

    internal static bool LifecycleCleanupRegistered => _lifecycleRegistered;

    public static void TryPlay(CardModel card)
    {
        if (!ShouldPlay(card))
            return;

        TriggeredCombats.Add(card.CombatState!);
        // Presentation-only randomness must not advance the run's gameplay RNG.
        Tracks[Random.Shared.Next(Tracks.Length)].TryPlay(card);
    }

    public static bool ShouldPlay(CardModel card)
    {
        if (card.CombatState is not { } combatState)
            return false;

        if (!IsEligibleRoom(combatState))
            return false;

        if (TriggeredCombats.Contains(combatState))
            return false;

        return true;
    }

    internal static bool IsEligibleRoom(ICombatState? combatState) =>
        combatState?.Encounter?.RoomType is RoomType.Elite or RoomType.Boss;

    internal static bool HasTriggeredInCombat(ICombatState? combatState) =>
        combatState is not null && TriggeredCombats.Contains(combatState);

    internal static void ResetPerCombat() => TriggeredCombats.Clear();

    internal static AudioPlaybackOptions CreatePlaybackOptions() => Tracks[0].CreatePlaybackOptions();
}
