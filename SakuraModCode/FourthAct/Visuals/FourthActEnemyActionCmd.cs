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
    private const string WindAttackSfx =
        "event:/sfx/enemy/enemy_attacks/thieving_hopper/thieving_hopper_attack_hover";
    private const string IllusionAttackSfx =
        "event:/sfx/enemy/enemy_attacks/obscura/obscura_attack";
    private const string DarkAttackSfx =
        "event:/sfx/enemy/enemy_attacks/spectral_knight/spectral_knight_soul_slash";

    internal static Task AttackAsync(
        Creature attacker,
        AttackCommand command,
        FourthActAttackStyle style = FourthActAttackStyle.Wind)
    {
        var (attackerSfx, hitVfx) = style switch
        {
            FourthActAttackStyle.HeavyWind => (WindAttackSfx, "vfx/vfx_flying_slash"),
            FourthActAttackStyle.Illusion => (IllusionAttackSfx, "vfx/vfx_starry_impact"),
            FourthActAttackStyle.Dark => (DarkAttackSfx, "vfx/vfx_attack_slash"),
            _ => (WindAttackSfx, "vfx/vfx_attack_slash")
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
