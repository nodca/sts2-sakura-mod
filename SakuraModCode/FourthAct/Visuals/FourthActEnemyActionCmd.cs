using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace SakuraMod.SakuraModCode.FourthAct.Visuals;

internal enum FourthActAttackStyle
{
    Wind,
    HeavyWind,
    Illusion,
    Dark
}

internal static class FourthActEnemyActionCmd
{
    internal static Task AttackAsync(
        Creature attacker,
        AttackCommand command,
        FourthActAttackStyle style = FourthActAttackStyle.Wind)
    {
        var (attackerSfx, hitVfx) = style switch
        {
            FourthActAttackStyle.HeavyWind =>
                (FourthActEnemyAudio.PathFor(FourthActAudioCue.HeavyWindAttack), "vfx/vfx_flying_slash"),
            FourthActAttackStyle.Illusion =>
                (FourthActEnemyAudio.PathFor(FourthActAudioCue.IllusionAttack), "vfx/vfx_starry_impact"),
            FourthActAttackStyle.Dark =>
                (FourthActEnemyAudio.PathFor(FourthActAudioCue.DarkAttack), "vfx/vfx_attack_slash"),
            _ =>
                (FourthActEnemyAudio.PathFor(FourthActAudioCue.WindAttack), "vfx/vfx_attack_slash")
        };
        command
            .WithNoAttackerAnim()
            .OnlyPlayAnimOnce()
            .WithAttackerFx(null, attackerSfx)
            .WithHitFx(hitVfx)
            .WithWaitBeforeHit(0.02f, 0.05f);
        return PerformAsync(attacker, SakuraStandeeClip.Attack, () => command.Execute(null));
    }

    internal static async Task PerformAsync(
        Creature actor,
        SakuraStandeeClip clip,
        Func<Task> resolveAtContact)
    {
        if (SakuraStandeeActionController.TryGet(actor) is not { } controller)
        {
            await resolveAtContact();
            return;
        }

        await controller.PlayActionAsync(clip, resolveAtContact);
    }
}
