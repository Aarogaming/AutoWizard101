using System.Text.Json;
using System.Text.Json.Nodes;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public class ModuleReplayTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "maelstrom-module-replay-" + Guid.NewGuid().ToString("N"));
    public ModuleReplayTests() => Directory.CreateDirectory(_directory);
    public void Dispose() => Directory.Delete(_directory, recursive: true);
    private string Report(string name) => Path.Combine(_directory, name + ".json");

    private (string Bundle, string Manifest, string Events) Prepare()
    {
        var bundle = ModuleRunnerTests.LabBundle(); var binding = ModuleRunnerTests.Binding(bundle);
        var bundlePath = Report("bundle"); ModuleCatalog.Save(bundle, bundlePath);
        var manifest = Report("session"); var events = Path.Combine(_directory, "events.jsonl");
        File.WriteAllText(manifest, JsonSerializer.Serialize(new
        {
            schema_version = 1,
            session_id = "replay",
            clock_origin = "synthetic-test",
            events_file = "events.jsonl",
            routine_binding = binding
        }, OperationModules.Json));
        var compact = new JsonSerializerOptions(OperationModules.Json) { WriteIndented = false };
        var lines = new List<string>(); var sequence = 0;
        void Event(double time, string kind, object payload) => lines.Add(JsonSerializer.Serialize(new
        { event_id = "e" + sequence, sequence = sequence++, time_ms = time, kind, payload }, compact));
        void Observe(double time, string scene, string? proposal) => Event(time, "observation",
            new { module_observation = ModuleRunnerTests.Frame(binding, 999999, scene, proposal) });
        Observe(0, "Spawn", null);
        string[] actions = ["lab.walk", "lab.interact", "lab.confirm", "lab.pause", "lab.rest"];
        string[] proposalIds = ["replay.m0/approach/1", "replay.m1/interact/1", "replay.m2/confirm/1", "replay.m3/rest/1", "replay.m3/rest/2"];
        string[] scenes = ["Pad", "Station", "Ready", "PauseMenu", "Resting"];
        for (var i = 0; i < actions.Length; i++)
        {
            Event(i * 100 + 1, "action_receipt", new { routine_receipt = new RoutineReceipt(proposalIds[i], actions[i], 999999, true, true) });
            Observe((i + 1) * 100, scenes[i], proposalIds[i]);
        }
        Event(501, "marker", new { note = "synthetic fixture; no live dispatch" });
        File.WriteAllLines(events, lines);
        return (bundlePath, manifest, events);
    }

    [Fact]
    public void ReplayIsDeterministicUsesEnvelopeClockAndReportsZeroDispatchWithSourceHashes()
    {
        var files = Prepare();
        Assert.True(ModuleReplay.Run(files.Bundle, files.Manifest, Report("first")));
        Assert.True(ModuleReplay.Run(files.Bundle, files.Manifest, Report("second")));
        Assert.Equal(File.ReadAllBytes(Report("first")), File.ReadAllBytes(Report("second")));
        using var report = JsonDocument.Parse(File.ReadAllText(Report("first"))); var root = report.RootElement;
        Assert.Equal(5, root.GetProperty("proposal_count").GetInt32()); Assert.Equal(5, root.GetProperty("receipt_count").GetInt32());
        Assert.Equal(4, root.GetProperty("completed_modules").GetInt32()); Assert.Equal(1, root.GetProperty("ignored_events").GetInt32());
        Assert.Equal(0, root.GetProperty("desktop_dispatch_count").GetInt32()); Assert.False(root.GetProperty("live_qualified").GetBoolean());
        Assert.Equal(64, root.GetProperty("sha256_bundle").GetString()!.Length);
        Assert.Equal(64, root.GetProperty("sha256_manifest").GetString()!.Length);
        Assert.Equal(64, root.GetProperty("sha256_events").GetString()!.Length);
        Assert.Throws<IOException>(() => ModuleReplay.Run(files.Bundle, files.Manifest, Report("first")));
    }

    [Fact]
    public void MissingReceiptsNeverInventCompletion()
    {
        var files = Prepare();
        File.WriteAllLines(files.Events, File.ReadAllLines(files.Events).Where(line => !line.Contains("action_receipt")));
        Assert.False(ModuleReplay.Run(files.Bundle, files.Manifest, Report("incomplete")));
        using var report = JsonDocument.Parse(File.ReadAllText(Report("incomplete")));
        Assert.Equal(0, report.RootElement.GetProperty("completed_modules").GetInt32());
        Assert.Equal(1, report.RootElement.GetProperty("proposal_count").GetInt32());
    }

    [Theory]
    [InlineData("accepted")]
    [InlineData("input_released")]
    public void UnconfirmedReceiptStopsReportEvenAtEndOfFile(string missing)
    {
        var files = Prepare(); var lines = File.ReadAllLines(files.Events).Take(2).ToArray();
        lines[^1] = lines[^1].Replace("\"" + missing + "\":true", "\"" + missing + "\":false"); File.WriteAllLines(files.Events, lines);
        Assert.False(ModuleReplay.Run(files.Bundle, files.Manifest, Report("rejected")));
        using var report = JsonDocument.Parse(File.ReadAllText(Report("rejected")));
        Assert.True(report.RootElement.GetProperty("stopped").GetBoolean());
        Assert.Equal("dispatch-unconfirmed", report.RootElement.GetProperty("reason").GetString());
    }

    [Fact]
    public void RemovingOutcomeBindingsCannotAdvanceDespiteCorrectReceipts()
    {
        var files = Prepare();
        File.WriteAllLines(files.Events, File.ReadAllLines(files.Events).Select(line =>
        {
            var item = JsonNode.Parse(line)!;
            if (item["payload"]!["module_observation"] is { } frame)
                frame["observation"]!.AsObject().Remove("outcome_for_proposal_id");
            return item.ToJsonString();
        }).ToArray());
        Assert.False(ModuleReplay.Run(files.Bundle, files.Manifest, Report("unbound")));
        using var report = JsonDocument.Parse(File.ReadAllText(Report("unbound")));
        Assert.Equal(0, report.RootElement.GetProperty("completed_modules").GetInt32());
        Assert.Equal(1, report.RootElement.GetProperty("proposal_count").GetInt32());
    }

    [Fact]
    public void PreviousModuleReceiptStopsReplayAfterHandoff()
    {
        var files = Prepare(); var lines = File.ReadAllLines(files.Events);
        var old = JsonNode.Parse(lines[1])!["payload"]!.DeepClone();
        var next = JsonNode.Parse(lines[3])!; next["payload"] = old; lines[3] = next.ToJsonString();
        File.WriteAllLines(files.Events, lines);
        Assert.False(ModuleReplay.Run(files.Bundle, files.Manifest, Report("stale")));
        using var report = JsonDocument.Parse(File.ReadAllText(Report("stale")));
        Assert.True(report.RootElement.GetProperty("stopped").GetBoolean());
        Assert.Equal(2, report.RootElement.GetProperty("proposal_count").GetInt32());
        Assert.Equal(1, report.RootElement.GetProperty("completed_modules").GetInt32());
    }

    [Theory]
    [InlineData("duplicate-event")]
    [InlineData("bad-clock")]
    [InlineData("duplicate-property")]
    [InlineData("unknown-observation")]
    [InlineData("missing-observation")]
    public void MalformedEventsFailWithoutProducingSuccessReport(string change)
    {
        var files = Prepare(); var lines = File.ReadAllLines(files.Events);
        if (change == "duplicate-event") lines = [.. lines, lines[0]];
        else if (change == "duplicate-property") lines[0] = lines[0].Replace("\"sequence\":0", "\"sequence\":0,\"sequence\":0");
        else
        {
            var item = JsonNode.Parse(lines[0])!;
            if (change == "bad-clock") item["time_ms"] = -1;
            if (change == "unknown-observation") item["payload"]!["module_observation"]!["execute"] = "run";
            if (change == "missing-observation") item["payload"]!["module_observation"]!["observation"] = null;
            lines[0] = item.ToJsonString();
        }
        File.WriteAllLines(files.Events, lines);
        Assert.ThrowsAny<Exception>(() => ModuleReplay.Run(files.Bundle, files.Manifest, Report("invalid")));
        Assert.False(File.Exists(Report("invalid")));
    }

    [Theory]
    [InlineData("../events.jsonl")]
    [InlineData("sub/events.jsonl")]
    [InlineData("events.jsonl:stream")]
    public void EventPathsCannotEscapeSessionDirectory(string filename)
    {
        var files = Prepare(); var data = JsonNode.Parse(File.ReadAllText(files.Manifest))!;
        data["events_file"] = filename; File.WriteAllText(files.Manifest, data.ToJsonString());
        Assert.Throws<InvalidDataException>(() => ModuleReplay.Run(files.Bundle, files.Manifest, Report("escaped")));
        Assert.False(File.Exists(Report("escaped")));
    }
}
