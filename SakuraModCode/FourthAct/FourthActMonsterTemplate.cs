using Godot;
using MegaCrit.Sts2.Core.Audio;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using SakuraMod.SakuraModCode.FourthAct.Visuals;
using SakuraMod.SakuraModCode.FourthAct.Visuals.Presentation;
using STS2RitsuLib.Scaffolding.Content;

namespace SakuraMod.SakuraModCode.FourthAct;

// Shared presentation contract for every fourth-act monster: its element decides the hit
// material sound, death cue and standee shader, its standee never moves on its own, and its
// moves resolve through FourthActMoveFx with the catalog's effects preloaded.
public abstract class FourthActMonsterTemplate : ModMonsterTemplate
{
    internal FourthActElement Element => FourthActMovePresentationCatalog.ElementOf(GetType());

    private FourthActElementProfile Profile => FourthActElements.Profile(Element);

    public override bool HasDeathSfx => true;
    public override string DeathSfx => FourthActEnemyAudio.PathFor(Profile.Death);
    public override DamageSfxType TakeDamageSfxType => Profile.DamageSfx;
    public override float DeathAnimLengthOverride => DissolveDuration;

    // The standee dissolves itself; the native fade would only replay over an empty body.
    public override bool ShouldFadeAfterDeath => false;

    public sealed override IEnumerable<string> AssetPaths =>
        MonsterAssetPaths
            .Concat(FourthActMovePresentationCatalog.AssetPathsFor(GetType()))
            .Distinct();

    protected abstract IEnumerable<string> MonsterAssetPaths { get; }

    protected abstract NCreatureVisuals? CreateStandee();

    protected sealed override NCreatureVisuals? TryCreateCreatureVisuals()
    {
        var visuals = CreateStandee();
        if (visuals is not null)
            FourthActStandeeMaterial.Attach(visuals, this);
        return visuals;
    }

    // Read every frame by the standee material. StateGlow (0..1) lights a lasting rim in
    // StateColor for a buff worth watching; AmbientLevel fades the catalog's ambient layer.
    internal virtual float StandeeStateGlow => 0f;
    internal virtual Color StandeeStateColor => Profile.Accent;
    internal virtual Color StandeeAccent => Profile.Accent;
    internal virtual float AmbientLevel => 1f;

    // Death: how long the standee takes to dissolve, and an effect played at its centre as it
    // starts (The Nothing's sphere coming apart).
    internal virtual float DissolveDuration => FourthActStandeeMaterial.DissolveDuration;
    internal virtual FourthActFxId? DeathFx => null;

    // A hit normally flashes the body; a monster that shows hits another way (The Nothing's
    // sphere ripples where it was struck) plays that here and returns true.
    internal virtual bool TryPlayStandeeHit(NCreature node) => false;

    // NextMove is the move being performed for the whole of MonsterModel.PerformMove, so a
    // method shared by several moves resolves under whichever one is running.
    protected Task AttackAsync(AttackCommand command, int variant = 0, Creature? aim = null) =>
        AttackAsync(NextMove.Id, command, variant, aim);

    protected Task PerformAsync(Func<Task> resolve, IEnumerable<Creature>? impactTargets = null) =>
        PerformAsync(NextMove.Id, resolve, impactTargets);

    protected AttackCommand WithMoveFx(AttackCommand command) =>
        FourthActMoveFx.Dress(this, NextMove.Id, command);

    protected AttackCommand WithMoveFx(string moveId, AttackCommand command) =>
        FourthActMoveFx.Dress(this, moveId, command);

    protected Task TravelAsync(Creature target) =>
        FourthActMoveFx.TravelAsync(this, NextMove.Id, target);

    protected Task AttackAsync(string moveId, AttackCommand command, int variant = 0, Creature? aim = null) =>
        FourthActMoveFx.AttackAsync(this, moveId, command, variant, aim);

    protected Task PerformAsync(string moveId, Func<Task> resolve, IEnumerable<Creature>? impactTargets = null) =>
        FourthActMoveFx.PerformAsync(this, moveId, resolve, impactTargets);
}
