using Godot;
using SakuraMod.SakuraModCode.FourthAct.Visuals;
using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

// Every fourth-act audio cue must resolve against the loaded FMOD banks. A missing event
// fails silently in game, so the bank, not a string literal in decompiled code, is the
// authority.
internal static class FourthActAudioScenario
{
    public static Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions)
    {
        var server = Engine.GetSingleton("FmodServer")
            ?? throw new InvalidOperationException("FmodServer singleton is not registered.");
        var events = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var bank in server.Call("get_all_banks").AsGodotArray())
        {
            foreach (var description in bank.AsGodotObject().Call("get_description_list").AsGodotArray())
                events.Add(description.AsGodotObject().Call("get_path").AsString());
        }

        assertions.True("fmod_banks_listed", events.Count > 0, $"{events.Count} events");
        var missing = new List<string>();
        foreach (var cue in Enum.GetValues<FourthActAudioCue>())
        {
            var path = FourthActEnemyAudio.PathFor(cue);
            if (!path.StartsWith("event:/", StringComparison.Ordinal))
                continue;
            if (!events.Contains(path))
                missing.Add($"{cue}={path}");
        }
        assertions.True("fourth_act_cues_exist", missing.Count == 0, string.Join(", ", missing));

        // The full list is the menu for choosing cues; keep it beside the run artifacts.
        var listing = Path.Combine(request.ArtifactRoot, "fmod-events.txt");
        Directory.CreateDirectory(request.ArtifactRoot);
        File.WriteAllLines(listing, events);

        return Task.FromResult(new Dictionary<string, object?>
        {
            ["event_count"] = events.Count,
            ["event_listing"] = listing
        });
    }
}
