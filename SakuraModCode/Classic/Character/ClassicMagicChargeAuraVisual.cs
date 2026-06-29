using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.TestSupport;
using SakuraMod.SakuraModCode.Classic.Cards;
using SakuraMod.SakuraModCode.Extensions;

namespace SakuraMod.SakuraModCode.Classic.Character;

[HarmonyPatch(typeof(NCreature), nameof(NCreature._Ready))]
internal static class ClassicMagicChargeAuraVisualPatch
{
    [HarmonyPostfix]
    private static void ReadyPostfix(NCreature __instance) =>
        ClassicMagicChargeAuraVisual.Mount(__instance);
}

internal static class ClassicMagicChargeAuraVisual
{
    private const string AuraNodeName = "ClassicMagicChargeAura";
    private const string BlurRingPath = "vfx/classic_magic_charge_blur_ring.png";
    private const string ImportPathPrefix = "path=\"";
    private const float MaxAlpha = 0.6f;
    private const float FadeDuration = 0.3f;
    private const float PulseAmplitude = 0.035f;
    private const float PulseSpeed = 2.2f;

    private static readonly Vector2 AuraOffset = new(-10f, -8f);
    private static readonly CanvasItemMaterial AdditiveMaterial = new()
    {
        BlendMode = CanvasItemMaterial.BlendModeEnum.Add
    };

    public static void Mount(NCreature creatureNode)
    {
        if (TestMode.IsOn
            || creatureNode.Entity.Player is not { Character: ClassicSakura } player
            || player.Creature.CombatState is not { } combatState
            || creatureNode.GetNodeOrNull<Node2D>(AuraNodeName) is not null)
        {
            return;
        }

        var aura = new Node2D
        {
            Name = AuraNodeName,
            ZAsRelative = true,
            Modulate = Colors.Transparent,
            Visible = false
        };
        BuildGlow(aura);
        creatureNode.AddChildSafely(aura);
        creatureNode.MoveChildSafely(aura, 0);
        TaskHelper.RunSafely(AnimateAura(aura, player, creatureNode, combatState));
    }

    private static async Task AnimateAura(Node2D aura, Player player, NCreature creatureNode, ICombatState combatState)
    {
        var alpha = 0f;
        var time = 0f;
        var hasSeenLiveCombat = combatState.IsLiveCombat();
        while (IsCurrentCreatureAura(aura, player, creatureNode, combatState))
        {
            var isLiveCombat = combatState.IsLiveCombat();
            hasSeenLiveCombat |= isLiveCombat;
            if (hasSeenLiveCombat && !isLiveCombat)
                break;

            var step = await aura.AwaitProcessFrame(CancellationToken.None);
            time += step;
            aura.GlobalPosition = creatureNode.VfxSpawnPosition + AuraOffset;

            var targetAlpha = ClassicSakuraMagic.CanUseExtraEffect(player) ? MaxAlpha : 0f;
            alpha = MoveToward(alpha, targetAlpha, step / FadeDuration);

            if (alpha <= 0.001f && targetAlpha <= 0f)
            {
                aura.Visible = false;
                continue;
            }

            aura.Visible = true;
            var pulse = 3.5f + Mathf.Sin(time * PulseSpeed) * PulseAmplitude;
            aura.Scale = creatureNode.Visuals.Scale * pulse;
            aura.Modulate = new Color(1f, 1f, 1f, alpha);
        }

        aura.QueueFreeSafely();
    }

    private static bool IsCurrentCreatureAura(Node2D aura, Player player, NCreature creatureNode, ICombatState combatState) =>
        GodotObject.IsInstanceValid(aura)
        && aura.IsInsideTree()
        && GodotObject.IsInstanceValid(creatureNode)
        && creatureNode.IsInsideTree()
        && ReferenceEquals(creatureNode.Entity.Player, player)
        && ReferenceEquals(player.Creature.CombatState, combatState)
        && player.Creature.IsAlive;

    private static void BuildGlow(Node2D aura)
    {
        var texture = ResourceLoader.Load<Texture2D>(ImportedTexturePath(BlurRingPath.ImagePath()), null, ResourceLoader.CacheMode.Reuse);
        aura.AddChild(BuildLayer(texture, new Color(0.93f, 0.41f, 0.46f), 1.15f));
        aura.AddChild(BuildLayer(texture, new Color(1f, 0.57f, 0.64f), 1f));
        aura.AddChild(BuildLayer(texture, new Color(1f, 0.75f, 0.79f), 0.85f));
        aura.AddChild(BuildLayer(texture, new Color(1f, 0.57f, 0.64f), 0.7f));
    }

    private static Sprite2D BuildLayer(Texture2D texture, Color color, float scale) =>
        new()
        {
            Texture = texture,
            Centered = true,
            Scale = Vector2.One * scale,
            Modulate = color,
            Material = AdditiveMaterial
        };

    private static string ImportedTexturePath(string sourcePath)
    {
        var importPath = sourcePath + ".import";
        if (!Godot.FileAccess.FileExists(importPath))
            return sourcePath;

        using var file = Godot.FileAccess.Open(importPath, Godot.FileAccess.ModeFlags.Read);
        if (file is null)
            return sourcePath;

        while (!file.EofReached())
        {
            var line = file.GetLine().Trim();
            if (line.StartsWith(ImportPathPrefix) && line.EndsWith('"'))
                return line[ImportPathPrefix.Length..^1];
        }

        return sourcePath;
    }

    private static float MoveToward(float from, float to, float delta)
    {
        if (Mathf.Abs(to - from) <= delta)
            return to;

        return from + Mathf.Sign(to - from) * delta;
    }
}
