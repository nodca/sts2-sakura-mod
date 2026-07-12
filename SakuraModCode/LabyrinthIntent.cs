using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Combat;
using SakuraMod.SakuraModCode.Extensions;

namespace SakuraMod.SakuraModCode;

public class LabyrinthIntent : AbstractIntent
{
    private const string IconFileName = "labyrinth.png";

    private static Texture2D? _texture;

    public override IntentType IntentType => IntentType.Stun;
    protected override string IntentPrefix => "SAKURA_MOD_LABYRINTH";
    protected override string? SpritePath => null;
    public override IEnumerable<string> AssetPaths => [IconPath];

    internal static Texture2D Texture =>
        _texture ??= ResourceLoader.Load<Texture2D>(IconPath, null, ResourceLoader.CacheMode.Reuse);

    public override Texture2D GetTexture(IEnumerable<Creature> targets, Creature owner) => Texture;

    // NIntent requires a registered vanilla animation name before the visual patch supplies our static frame.
    public override string GetAnimation(IEnumerable<Creature> targets, Creature owner) => "hidden";

    private static string IconPath => IconFileName.IntentImagePath();
}

public sealed class LabyrinthReleaseWarningIntent : LabyrinthIntent
{
    protected override string IntentPrefix => "SAKURA_MOD_LABYRINTH_RELEASE_WARNING";
}

[HarmonyPatch(typeof(NIntent), "UpdateVisuals")]
internal static class LabyrinthIntentVisualPatch
{
    private const float PulseCyclesPerSecond = 1.25f;
    private const float MinimumPulseAlpha = 0.35f;
    private static readonly ConditionalWeakTable<NIntent, object> PulsingIntents = new();

    [HarmonyPostfix]
    private static void UpdateVisualsPostfix(
        NIntent __instance,
        AbstractIntent ____intent,
        List<Texture2D> ____animationFrames)
    {
        var intentSprite = __instance.GetNode<Sprite2D>("%Intent");
        if (____intent is not LabyrinthIntent)
        {
            if (PulsingIntents.Remove(__instance))
                intentSprite.SelfModulate = Colors.White;
            return;
        }

        ____animationFrames.Clear();
        intentSprite.Texture = LabyrinthIntent.Texture;
        if (____intent is LabyrinthReleaseWarningIntent)
            PulsingIntents.GetOrCreateValue(__instance);
        else if (PulsingIntents.Remove(__instance))
            intentSprite.SelfModulate = Colors.White;
    }

    internal static void ApplyPulse(NIntent intent)
    {
        if (!PulsingIntents.TryGetValue(intent, out _))
            return;

        var phase = Time.GetTicksMsec() / 1000f * Mathf.Tau * PulseCyclesPerSecond;
        var alpha = Mathf.Lerp(MinimumPulseAlpha, 1f, (Mathf.Sin(phase) + 1f) * 0.5f);
        intent.GetNode<Sprite2D>("%Intent").SelfModulate = new Color(1f, 1f, 1f, alpha);
    }
}

[HarmonyPatch(typeof(NIntent), "_Process")]
internal static class LabyrinthIntentPulsePatch
{
    [HarmonyPostfix]
    private static void ProcessPostfix(NIntent __instance) =>
        LabyrinthIntentVisualPatch.ApplyPulse(__instance);
}
