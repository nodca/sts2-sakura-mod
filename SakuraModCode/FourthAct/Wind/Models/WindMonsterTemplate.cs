using MegaCrit.Sts2.Core.Audio;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Combat;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.FourthAct.Visuals;
using STS2RitsuLib.Scaffolding.Content;

namespace SakuraMod.SakuraModCode.FourthAct.Wind.Models;

public abstract class WindMonsterTemplate : ModMonsterTemplate
{
    protected abstract string StandeePath { get; }
    protected abstract string StandeeLabel { get; }
    protected virtual IEnumerable<string> AdditionalAssetPaths => [];
    protected abstract IEnumerable<AbstractIntent> DeclaredIntents { get; }

    public sealed override string? CustomVisualsPath => StandeePath;
    public override bool HasDeathSfx => true;
    public override string DeathSfx =>
        "event:/sfx/enemy/enemy_attacks/living_fog/living_fog_die";
    public override string? HurtSfx =>
        "event:/sfx/enemy/enemy_attacks/soul_fysh/soul_fysh_hurt";
    public override DamageSfxType TakeDamageSfxType => DamageSfxType.Magic;
    public override float DeathAnimLengthOverride => SakuraStandeeActionController.DeathDuration;

    public override IEnumerable<string> AssetPaths =>
        DeclaredIntents
            .SelectMany(static intent => intent.AssetPaths)
            .Concat(WindEnemyAssets.ActionFramesFor(StandeePath))
            .Concat(AdditionalAssetPaths)
            .Prepend(StandeePath)
            .Distinct();

    protected override NCreatureVisuals? TryCreateCreatureVisuals() =>
        SakuraStandeeVisuals.Create(StandeePath, StandeeLabel);
}
