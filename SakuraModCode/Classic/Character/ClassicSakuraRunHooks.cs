using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Runs;
using SakuraMod.SakuraModCode.Classic.Cards;

namespace SakuraMod.SakuraModCode.Classic.Character;

internal static class ClassicSakuraRunHooks
{
    private const string RunHookId = "SakuraMod.ClassicSakura.Run";
    private static readonly ClassicSakuraHook Hook = new();
    private static bool _registered;

    public static void Register()
    {
        if (_registered)
            return;

        ModHelper.SubscribeForRunStateHooks(RunHookId, runState =>
        {
            Hook.SetRunState(runState);
            return [Hook];
        });
        _registered = true;
    }
}

internal sealed class ClassicSakuraHook : AbstractModel
{
    private RunState? _runState;

    public override bool ShouldReceiveCombatHooks => false;

    public void SetRunState(RunState runState)
    {
        _runState = runState;
    }

    public override Task BeforeCombatStart()
    {
        if (_runState is not { } runState)
            return Task.CompletedTask;

        foreach (var player in runState.Players.Where(player => player.Character is ClassicSakura))
            ClowCreate.ReduceCostAtCombatStart(player);

        return Task.CompletedTask;
    }
}
