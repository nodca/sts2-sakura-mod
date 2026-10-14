using Godot;
using MegaCrit.Sts2.Core.Audio;

namespace SakuraMod.SakuraModCode.FourthAct.Visuals.Presentation;

internal enum FourthActElement
{
    Wind,
    Water,
    Ice,
    Fire,
    Light,
    Blade,
    Earth,
    Wood,
    Dark
}

// What an element contributes when a move or monster names nothing more specific: its
// floor hit effect, the native per-hit material sound, its death cue and the standee
// shader tints (charge rim, hit flash, dissolve edge).
internal sealed record FourthActElementProfile(
    FourthActFxId Impact,
    DamageSfxType DamageSfx,
    FourthActAudioCue Death,
    Color Accent,
    Color Flash,
    Color DissolveEdge);

internal static class FourthActElements
{
    private const string ShaderRoot = MainFile.ResPath + "/shaders/fourth_act";

    internal static FourthActElementProfile Profile(FourthActElement element) => element switch
    {
        FourthActElement.Wind => new(FourthActFxId.WindImpact, DamageSfxType.Magic, FourthActAudioCue.WindDeath,
            new Color(0.55f, 0.95f, 0.84f), new Color(0.88f, 1f, 0.96f), new Color(0.62f, 1f, 0.88f)),
        FourthActElement.Water => new(FourthActFxId.WaterImpact, DamageSfxType.Slime, FourthActAudioCue.WaterDeath,
            new Color(0.32f, 0.68f, 1f), new Color(0.82f, 0.93f, 1f), new Color(0.4f, 0.78f, 1f)),
        FourthActElement.Ice => new(FourthActFxId.IceImpact, DamageSfxType.Stone, FourthActAudioCue.IceDeath,
            new Color(0.7f, 0.92f, 1f), new Color(0.94f, 0.99f, 1f), new Color(0.78f, 0.96f, 1f)),
        FourthActElement.Fire => new(FourthActFxId.FireImpact, DamageSfxType.Magic, FourthActAudioCue.FireDeath,
            new Color(1f, 0.5f, 0.16f), new Color(1f, 0.86f, 0.62f), new Color(1f, 0.42f, 0.1f)),
        FourthActElement.Light => new(FourthActFxId.LightImpact, DamageSfxType.Magic, FourthActAudioCue.LightDeath,
            new Color(1f, 0.86f, 0.46f), new Color(1f, 0.97f, 0.84f), new Color(1f, 0.9f, 0.55f)),
        FourthActElement.Blade => new(FourthActFxId.BladeImpact, DamageSfxType.Armor, FourthActAudioCue.BladeDeath,
            new Color(0.8f, 0.86f, 0.98f), new Color(0.96f, 0.98f, 1f), new Color(0.84f, 0.9f, 1f)),
        FourthActElement.Earth => new(FourthActFxId.EarthImpact, DamageSfxType.Stone, FourthActAudioCue.EarthDeath,
            new Color(0.86f, 0.62f, 0.34f), new Color(1f, 0.9f, 0.76f), new Color(0.74f, 0.52f, 0.3f)),
        FourthActElement.Wood => new(FourthActFxId.WoodImpact, DamageSfxType.Plant, FourthActAudioCue.WoodDeath,
            new Color(0.5f, 0.86f, 0.36f), new Color(0.9f, 1f, 0.82f), new Color(0.56f, 0.9f, 0.4f)),
        FourthActElement.Dark => new(FourthActFxId.DarkImpact, DamageSfxType.Magic, FourthActAudioCue.DarkDeath,
            new Color(0.62f, 0.42f, 1f), new Color(0.86f, 0.8f, 1f), new Color(0.56f, 0.32f, 0.94f)),
        _ => throw new ArgumentOutOfRangeException(nameof(element), element, null)
    };

    internal static string StandeeShaderPath(FourthActElement element) =>
        $"{ShaderRoot}/standee_{element.ToString().ToLowerInvariant()}.gdshader";
}
