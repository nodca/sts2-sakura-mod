using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using STS2RitsuLib;
using STS2RitsuLib.RunData;

namespace SakuraMod.SakuraModCode.Character;

public sealed class SakuraCombatArtState
{
    public bool UseChibi { get; set; }
}

internal static class SakuraCombatArtPreference
{
    internal const string RunSavedDataKey = "combat_art_v1";

    private static PlayerRunSavedData<SakuraCombatArtState>? _runData;
    private static bool _registrationAttempted;

    public static void Register()
    {
        if (_registrationAttempted)
            return;

        _registrationAttempted = true;
        try
        {
            _runData = RitsuLibFramework.GetRunSavedDataStore(MainFile.ModId).RegisterPerPlayer(
                RunSavedDataKey,
                static () => new SakuraCombatArtState(),
                CreateOptions());
        }
        catch (Exception exception)
        {
            MainFile.Logger.Error($"Sakura combat-art run-data registration failed: {exception}");
        }
    }

    internal static RunSavedDataOptions CreateOptions() =>
        new()
        {
            SchemaVersion = 1,
            WritePolicy = RunSavedDataWritePolicy.WhenSet,
            SyncLobbyOnChange = true
        };

    internal static bool GetOrInitializeLocalLobbyPreference(StartRunLobby lobby)
    {
        ArgumentNullException.ThrowIfNull(lobby);

        if (_runData is not null
            && _runData.Lobby.TryGet(lobby, lobby.LocalPlayer.id, out var existing))
        {
            return IsChibi(existing);
        }

        if (_runData is null)
            return false;

        var useChibi = SakuraModConfig.IsChibiCombatArtEnabled();
        SetLocalLobbyPreference(lobby, useChibi, persistGlobalDefault: false);
        return useChibi;
    }

    internal static void SetLocalLobbyPreference(
        StartRunLobby lobby,
        bool useChibi,
        bool persistGlobalDefault = true)
    {
        ArgumentNullException.ThrowIfNull(lobby);

        if (_runData is null)
            return;

        if (persistGlobalDefault)
            SakuraModConfig.UseChibiCombatArtBinding.Write(useChibi);

        _runData?.Lobby.Set(
            lobby,
            lobby.LocalPlayer.id,
            new SakuraCombatArtState { UseChibi = useChibi });
    }

    internal static bool IsChibi(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);

        return player.RunState is RunState runState
            && _runData is not null
            && _runData.TryGet(runState, player.NetId, out var state)
            && IsChibi(state);
    }

    internal static bool IsChibi(SakuraCombatArtState? state) =>
        SakuraCombatArtFeature.IsEnabled && state?.UseChibi == true;
}

[HarmonyPatch(typeof(Creature), nameof(Creature.CreateVisuals))]
internal static class SakuraPlayerCombatVisualPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Creature __instance, ref NCreatureVisuals? __result)
    {
        var player = __instance.Player;
        if (!ShouldOverride(TestMode.IsOn, player?.Character is ClassicSakura))
            return true;

        var sakura = (ClassicSakura)player!.Character;
        __result = SakuraCombatVisuals.CreateSelected(
            sakura.CustomVisualsPath,
            SakuraCombatArtPreference.IsChibi(player));
        return false;
    }

    internal static bool ShouldOverride(bool isTestMode, bool isSakuraPlayer) =>
        !isTestMode && isSakuraPlayer;
}
