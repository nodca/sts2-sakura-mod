using SakuraMod.TestProtocol;
using System.Text.Json;

namespace SakuraMod.TestRunner;

public static class ProtocolSelfTestCommand
{
    public static async Task<int> RunAsync(string repoRoot)
    {
        var runId = $"{DateTimeOffset.UtcNow:yyyyMMddTHHmmssZ}-protocol-{Guid.NewGuid():N}";
        var root = Path.Combine(repoRoot, "artifacts", "tests", runId);
        Directory.CreateDirectory(root);
        try
        {
            var requestPath = Path.Combine(root, "request.json");
            var request = new SakuraTestRequest(
                SakuraTestProtocol.CurrentSchemaVersion,
                runId,
                "runtime",
                "protocol-self-test",
                "single",
                RuntimePreflight.ExpectedGameVersion,
                RuntimePreflight.ExpectedRitsuVersion,
                "1.1.0",
                1,
                "eng",
                5,
                root,
                Path.Combine(root, "result.json"),
                Path.Combine(root, "checkpoints.jsonl"));
            SakuraTestProtocol.ValidateRequest(request);
            await SakuraTestProtocol.WriteAtomicAsync(requestPath, request);
            var roundTrip = await SakuraTestProtocol.ReadAsync<SakuraTestRequest>(requestPath);
            if (roundTrip != request)
            {
                throw new InvalidDataException("Protocol request did not survive a typed round trip.");
            }

            await SakuraTestProtocol.AppendCheckpointAsync(
                request.CheckpointPath,
                new SakuraTestCheckpoint(1, runId, request.ScenarioId, "round_trip", DateTimeOffset.UtcNow, "PASS"));
            var malformedPath = Path.Combine(root, "malformed.json");
            await File.WriteAllTextAsync(malformedPath, "{\"schema_version\":");
            try
            {
                await SakuraTestProtocol.ReadAsync<SakuraTestRequest>(malformedPath);
                throw new InvalidOperationException("Malformed protocol JSON was accepted.");
            }
            catch (JsonException)
            {
                // Expected: partial JSON can never be interpreted as success.
            }

            if (Directory.EnumerateFiles(root, "*.tmp", SearchOption.AllDirectories).Any())
            {
                throw new IOException("Atomic protocol write left a temporary file behind.");
            }

            Console.WriteLine($"[protocol] PASS: {root}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"[protocol] FAIL: {exception.Message}");
            Console.Error.WriteLine($"[protocol] artifacts retained: {root}");
            return 1;
        }
    }
}
