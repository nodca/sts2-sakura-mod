using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.TestSupport;
using SakuraMod.SakuraModCode;

namespace SakuraMod.SakuraModCode.Cards;

public enum SakuraVoiceTrigger
{
    Stabilize
}

public static class SakuraVoicePlayback
{
    private const string PlayerName = "SakuraVoicePlayer";

    private static readonly Dictionary<SakuraVoiceTrigger, string> VoicePaths = new()
    {
        [SakuraVoiceTrigger.Stabilize] = $"{MainFile.ResPath}/voices/stabilize.ogg"
    };

    private static ICombatState? _currentCombat;
    private static readonly HashSet<SakuraVoiceTrigger> PlayedThisCombat = [];
    private static AudioStreamPlayer? _player;

    public static void TryPlay(SakuraVoiceTrigger trigger, ICombatState? combatState)
    {
        if (!SakuraModConfig.EnableSakuraVoice || TestMode.IsOn)
            return;

        ResetIfCombatChanged(combatState);
        if (!PlayedThisCombat.Add(trigger))
            return;

        if (!VoicePaths.TryGetValue(trigger, out var path) || !ResourceLoader.Exists(path))
            return;

        var player = GetOrCreatePlayer();
        if (player is null || player.Playing)
            return;

        var stream = ResourceLoader.Load<AudioStream>(path, null, ResourceLoader.CacheMode.Reuse);
        if (stream is null)
            return;

        player.Stream = stream;
        player.Play();
    }

    private static void ResetIfCombatChanged(ICombatState? combatState)
    {
        if (ReferenceEquals(_currentCombat, combatState))
            return;

        _currentCombat = combatState;
        PlayedThisCombat.Clear();
    }

    private static AudioStreamPlayer? GetOrCreatePlayer()
    {
        if (IsUsable(_player))
            return _player;

        var tree = Engine.GetMainLoop() as SceneTree;
        var parent = tree?.Root;
        if (parent is null)
            return null;

        _player = new AudioStreamPlayer { Name = PlayerName };
        parent.AddChild(_player);
        return _player;
    }

    private static bool IsUsable(AudioStreamPlayer? player) =>
        player is not null
        && GodotObject.IsInstanceValid(player)
        && !player.IsQueuedForDeletion()
        && player.IsInsideTree();
}
