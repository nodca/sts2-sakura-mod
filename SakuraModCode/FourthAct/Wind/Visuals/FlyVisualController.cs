using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace SakuraMod.SakuraModCode.FourthAct.Wind.Visuals;

internal static class FlyVisualController
{
    private const double FrameSeconds = 0.055;

    internal static void PlayLanding(Creature fly) => Play(fly, landing: true);
    internal static void PlayTakeoff(Creature fly) => Play(fly, landing: false);

    private static void Play(Creature fly, bool landing)
    {
        if (NCombatRoom.Instance?.GetCreatureNode(fly) is not { } node)
            return;
        TaskHelper.RunSafely(PlayFrames(node, landing));
    }

    private static async Task PlayFrames(NCreature node, bool landing)
    {
        var sprite = FindSprite(node.Visuals);
        if (sprite is null)
            return;

        var frames = landing
            ? WindEnemyAssets.FlyTransitionFrames
            : WindEnemyAssets.FlyTransitionFrames.Reverse().ToArray();
        foreach (var frame in frames)
        {
            if (!GodotObject.IsInstanceValid(sprite) || !sprite.IsInsideTree())
                return;
            sprite.Texture = ResourceLoader.Load<Texture2D>(frame);
            await sprite.ToSignal(sprite.GetTree().CreateTimer(FrameSeconds), SceneTreeTimer.SignalName.Timeout);
        }

        if (GodotObject.IsInstanceValid(sprite))
            sprite.Texture = ResourceLoader.Load<Texture2D>(landing ? WindEnemyAssets.FlyGrounded : WindEnemyAssets.FlyAirborne);
    }

    private static Sprite2D? FindSprite(Node root)
    {
        if (root is Sprite2D sprite)
            return sprite;
        foreach (var child in root.GetChildren())
        {
            if (FindSprite(child) is { } nested)
                return nested;
        }
        return null;
    }
}
