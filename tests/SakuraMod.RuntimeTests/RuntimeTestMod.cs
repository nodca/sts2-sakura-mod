using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using STS2RitsuLib;

namespace SakuraMod.RuntimeTests;

[ModInitializer(nameof(Initialize))]
public static class RuntimeTestMod
{
    public const string ModId = "SakuraMod.RuntimeTests";
    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } = new(ModId, LogType.Generic);

    private static int _scheduled;
    private static IDisposable? _subscription;

    public static void Initialize()
    {
        _subscription = RitsuLibFramework.SubscribeLifecycle<MainMenuReadyEvent>(
            (_, subscription) =>
            {
                subscription.Dispose();
                if (Interlocked.Exchange(ref _scheduled, 1) != 0)
                {
                    return;
                }

                Callable.From(RuntimeTestHost.ExecuteRequestedScenario).CallDeferred();
            },
            replayCurrentState: true);
    }
}
