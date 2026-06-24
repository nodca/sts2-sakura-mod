using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Classic.Character;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Relics;

namespace SakuraMod.SakuraModCode;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "SakuraMod"; //Used for resource filepath
    public const string ResPath = $"res://{ModId}";

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } = new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize()
    {
        //If you want to use scripts defined in your mod for Godot scenes, uncomment the following line.
        //Godot.Bridge.ScriptManagerBridge.LookupScriptsInAssembly(Assembly.GetExecutingAssembly());

        SealedBookMemory.Register();
        SakuraManifestLoop.Register();
        DreamKey.Register();

        Harmony harmony = new(ModId);

        harmony.PatchAll();
        ClassicSakuraRunHooks.Register();
        SakuraCaptureRunHooks.Register();
        ClearCardLayout.PreloadVisualResources();
    }
}
