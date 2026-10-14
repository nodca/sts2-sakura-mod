using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using MegaCrit.Sts2.Core.TestSupport;
using SakuraMod.SakuraModCode.FourthAct.Fire.Visuals;

namespace SakuraMod.SakuraModCode.FourthAct.Visuals.Presentation;

// The one entry every fourth-act move resolves through. Presentation is looked up in the
// catalog and always fails open: gameplay runs exactly once, and a presentation that could
// not be shown adds no wait.
internal static class FourthActMoveFx
{
    // Long enough for the rim to read as a wind-up before the first hit lands.
    private const float WindupFast = 0.1f;
    private const float WindupStandard = 0.2f;

    // `variant` picks the first ImpactCycle entry (see FourthActMovePresentation); `aim` is
    // the creature a TravelFx flies at when the command's targets are not known up front.
    internal static Task AttackAsync(MonsterModel monster, string id, AttackCommand command, int variant = 0, Creature? aim = null)
    {
        var presentation = FourthActMovePresentationCatalog.Get(monster.GetType(), id);
        Dress(monster, presentation, command, variant);
        return PerformCoreAsync(monster, presentation, () => command.Execute(null), aim is null ? [] : [aim], attack: true);
    }

    // Gives a native attack the move's per-hit effect and sound without wrapping it, for a
    // move that runs several attacks inside one performance.
    internal static AttackCommand Dress(MonsterModel monster, string id, AttackCommand command, int variant = 0) =>
        Dress(monster, FourthActMovePresentationCatalog.Get(monster.GetType(), id), command, variant);

    private static AttackCommand Dress(MonsterModel monster, FourthActMovePresentation presentation, AttackCommand command, int variant)
    {
        var attacker = monster.Creature;
        var hit = 0;
        command
            .WithNoAttackerAnim()
            .OnlyPlayAnimOnce()
            .WithHitVfxNode(target =>
            {
                Shake(presentation.Shake);
                return FourthActFxPlayer.CreateHit(presentation.ImpactFor(variant, hit++), attacker, target, presentation.ImpactAnchor);
            })
            .WithWaitBeforeHit(0.02f, 0.05f);
        if (presentation.ImpactCue is { } cue)
        {
            var (sfx, plainSound) = FourthActEnemyAudio.HitArguments(cue);
            command.WithHitFx(null, sfx, plainSound);
        }

        return command;
    }

    internal static Task PerformAsync(
        MonsterModel monster,
        string id,
        Func<Task> resolve,
        IEnumerable<Creature>? impactTargets = null) =>
        PerformCoreAsync(
            monster,
            FourthActMovePresentationCatalog.Get(monster.GetType(), id),
            resolve,
            impactTargets ?? [],
            attack: false);

    private static async Task PerformCoreAsync(
        MonsterModel monster,
        FourthActMovePresentation presentation,
        Func<Task> resolve,
        IEnumerable<Creature> impactTargets,
        bool attack)
    {
        if (TestMode.IsOn)
        {
            await resolve();
            return;
        }

        var actor = monster.Creature;
        var standee = FourthActStandeeMaterial.TryGet(actor);
        var controller = SakuraStandeeActionController.TryGet(actor);
        var shown = false;
        try
        {
            if (presentation.CastCue is { } castCue)
                FourthActEnemyAudio.Play(castCue);
            if (presentation.Charge && standee is not null)
            {
                standee.SetCharge(true);
                shown = true;
            }
            if (presentation.Pose && controller is { HasActionPose: true })
            {
                controller.SetActionPose(true);
                shown = true;
            }

            var contact = presentation.CastFx is { } castFx
                ? FourthActFxPlayer.SpawnAt(castFx, actor, presentation.CastAnchor)
                : null;
            if (contact is { } seconds)
                await Cmd.CustomScaledWait(seconds * 0.5f, seconds);
            else if (shown)
                await Cmd.CustomScaledWait(WindupFast, WindupStandard);

            if (presentation.TravelFx is { } travelFx && AimOf(monster, impactTargets) is { } aim
                && FourthActFxPlayer.SpawnToward(travelFx, actor, presentation.CastAnchor, aim, presentation.ImpactAnchor) is { } arrival)
                await Cmd.CustomScaledWait(arrival * 0.5f, arrival);
        }
        catch (Exception exception)
        {
            MainFile.Logger.Warn($"Fourth-act move wind-up failed open: {exception.Message}");
        }

        try
        {
            if (LibraVisualController.TryGet(actor) is { } libra)
                await libra.PlayAttackAsync(actor, resolve);
            else
                await resolve();
        }
        finally
        {
            standee?.SetCharge(false);
            controller?.SetActionPose(false);
        }

        // An attack's impact rides each native hit; anything else lands once it resolved.
        if (attack)
            return;
        if (presentation.ImpactCue is { } impactCue)
            FourthActEnemyAudio.Play(impactCue);
        if (presentation.ImpactFx is { } impact)
        {
            foreach (var target in impactTargets)
                FourthActFxPlayer.SpawnAt(impact, target, presentation.ImpactAnchor);
        }
    }

    // Flies the move's TravelFx at one more target inside a running performance (a follow-up
    // hit), and returns once it arrives. Fails open with no wait.
    internal static async Task TravelAsync(MonsterModel monster, string id, Creature target)
    {
        if (TestMode.IsOn)
            return;

        try
        {
            var presentation = FourthActMovePresentationCatalog.Get(monster.GetType(), id);
            if (presentation.TravelFx is { } travelFx
                && FourthActFxPlayer.SpawnToward(travelFx, monster.Creature, presentation.CastAnchor, target, presentation.ImpactAnchor) is { } arrival)
                await Cmd.CustomScaledWait(arrival * 0.5f, arrival);
        }
        catch (Exception exception)
        {
            MainFile.Logger.Warn($"Fourth-act travel failed open: {exception.Message}");
        }
    }

    // The native camera shake (it honours the player's screen-shake setting), as vanilla
    // CeremonialBeast plays it on its stomp.
    private static void Shake(ShakeStrength strength)
    {
        if (strength != ShakeStrength.None)
            NGame.Instance?.ScreenShake(strength, strength >= ShakeStrength.Strong ? ShakeDuration.Normal : ShakeDuration.Short);
    }

    private static Creature? AimOf(MonsterModel monster, IEnumerable<Creature> targets) =>
        targets.FirstOrDefault(static target => target.IsAlive)
        ?? monster.CombatState?.Players.Select(static player => player.Creature).FirstOrDefault(static creature => creature.IsAlive);
}
