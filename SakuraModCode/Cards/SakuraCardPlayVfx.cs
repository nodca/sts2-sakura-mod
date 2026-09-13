using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.TestSupport;

namespace SakuraMod.SakuraModCode.Cards;

public static class SakuraCardPlayVfx
{
    internal const string TimeScenePath = MainFile.ResPath + "/scenes/combat/card_vfx/time_shift_vfx.tscn";
    internal static IReadOnlyList<string> TimeAssetPaths { get; } = [TimeScenePath];
    private static bool _timeFailureLogged;

    public static void PlayTime()
    {
        if (!SakuraModConfig.IsCardVfxEnabled()
            || TestMode.IsOn
            || NCombatRoom.Instance is not { } room)
            return;

        Node2D? root = null;
        try
        {
            root = PreloadManager.Cache.GetScene(TimeScenePath).Instantiate<Node2D>();
            root.Name = "SakuraTimeVfx";
            root.ZAsRelative = true;
            root.ZIndex = 0;
            room.CombatVfxContainer.AddChildSafely(root);
            root.GlobalPosition = room.SceneContainer.GetGlobalRect().GetCenter();

            var animation = root.GetNode<AnimationPlayer>("AnimationPlayer");
            var effect = root;
            // Native animation owns only the drawing. It never changes the game
            // clock, turn flow, or creature pose, and introduces no gameplay await.
            void OnCombatEnded(CombatRoom _) => effect.QueueFreeSafely();
            CombatManager.Instance.CombatEnded += OnCombatEnded;
            effect.TreeExiting += () => CombatManager.Instance.CombatEnded -= OnCombatEnded;
            animation.AnimationFinished += _ => effect.QueueFreeSafely();
            animation.Play("time_shift");
        }
        catch (Exception exception)
        {
            root?.QueueFreeSafely();
            if (_timeFailureLogged)
                return;
            _timeFailureLogged = true;
            MainFile.Logger.Error($"Could not play Time VFX from {TimeScenePath}: {exception}");
        }
    }
}
