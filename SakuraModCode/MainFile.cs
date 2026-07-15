using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Classic.Cards;
using SakuraMod.SakuraModCode.Classic.Character;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Events;
using SakuraMod.SakuraModCode.Telemetry;
using STS2RitsuLib.Interop;

namespace SakuraMod.SakuraModCode;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "SakuraMod"; //Used for resource filepath
    public const string ResPath = $"res://{ModId}";

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } = new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize()
    {
        ModTypeDiscoveryHub.RegisterModAssembly(ModId, Assembly.GetExecutingAssembly());

        SakuraKeywords.Register();
        SakuraCardStates.Register();
        SakuraContentRegistration.Register();
        SakuraEventRegistration.Register();
        SakuraTelemetry.Register();

        Harmony harmony = new(ModId);

        harmony.PatchAll();
        SakuraCardVisualPatchRegistration.Register();
        SakuraCombatResourceHudPatchRegistration.Register();
        ClassicSakuraRunHooks.Register();
        ClearCardLayout.PreloadVisualResources();
    }
}
