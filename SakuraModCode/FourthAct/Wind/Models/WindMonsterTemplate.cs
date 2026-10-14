using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Combat;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.FourthAct.Visuals;

namespace SakuraMod.SakuraModCode.FourthAct.Wind.Models;

public abstract class WindMonsterTemplate : FourthActMonsterTemplate
{
    protected abstract string StandeePath { get; }
    protected abstract string StandeeLabel { get; }
    protected virtual IEnumerable<string> AdditionalAssetPaths => [];
    protected abstract IEnumerable<AbstractIntent> DeclaredIntents { get; }

    public sealed override string? CustomVisualsPath => StandeePath;
    public override string? HurtSfx =>
        "event:/sfx/enemy/enemy_attacks/soul_fysh/soul_fysh_hurt";

    protected override IEnumerable<string> MonsterAssetPaths =>
        DeclaredIntents
            .SelectMany(static intent => intent.AssetPaths)
            .Concat(WindEnemyAssets.ActionFramesFor(StandeePath))
            .Concat(AdditionalAssetPaths)
            .Prepend(StandeePath)
            .Distinct();

    protected override NCreatureVisuals? CreateStandee() =>
        SakuraStandeeVisuals.CreateFourthActEnemy(StandeePath, StandeeLabel);
}
