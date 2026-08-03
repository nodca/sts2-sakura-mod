using MegaCrit.Sts2.Core.Entities.Creatures;
using SakuraMod.SakuraModCode.FourthAct.Visuals;

namespace SakuraMod.SakuraModCode.FourthAct.Wind.Visuals;

internal static class FlyVisualController
{
    private const double FrameSeconds = 0.055;

    internal static Task PlayLandingAsync(Creature fly) => PlayAsync(fly, landing: true);
    internal static Task PlayTakeoffAsync(Creature fly) => PlayAsync(fly, landing: false);

    private static Task PlayAsync(Creature fly, bool landing)
    {
        if (SakuraStandeeActionController.TryGet(fly) is not { } controller)
            return Task.CompletedTask;
        var frames = landing
            ? WindEnemyAssets.FlyTransitionFrames
            : WindEnemyAssets.FlyTransitionFrames.Reverse().ToArray();
        return controller.PlayTextureSequenceAsync(
            frames,
            landing ? WindEnemyAssets.FlyGrounded : WindEnemyAssets.FlyAirborne,
            FrameSeconds);
    }
}
