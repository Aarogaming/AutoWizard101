using System.Text.Json;
using System.Text.Json.Nodes;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

[Collection("Windows OCR")]
public sealed class ModuleScreenReviewTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "module-screen-review-" + Guid.NewGuid().ToString("N"));
    public ModuleScreenReviewTests() => Directory.CreateDirectory(_root);
    private string Output(string name) => Path.Combine(_root, name + ".json");
    private string Prepare()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "TestData", "Modules", "W101Menus");
        foreach (var name in new[] { "reward", "snack", "outside", "graphics-menu", "native-idle", "loading" })
            File.Copy(Path.Combine(source, name + ".png"), Path.Combine(_root, name + ".png"));
        var bundle = W101ModulePrograms.Postgame(); ModuleCatalog.Save(bundle, Output("bundle"));
        var binding = ModuleScreenSessionTests.Binding(bundle);
        File.WriteAllText(Output("session"), JsonSerializer.Serialize(new
        {
            schema_version = 1,
            session_id = "review",
            clock_origin = "synthetic-milliseconds-not-original-capture-times",
            evidence_mode = "saved-images-synthetic-rehearsal",
            bundle_file = "bundle.json",
            bundle_sha256 = GameExperiments.Digest(Output("bundle")),
            events_file = "events.jsonl",
            routine_binding = binding,
            geometry = new { width = 1282, height = 760 }
        }, OperationModules.Json));
        var lines = new List<string>(); var sequence = 0; var compact = new JsonSerializerOptions(OperationModules.Json) { WriteIndented = false };
        void Event(double time, string kind, object payload) => lines.Add(JsonSerializer.Serialize(new
        { event_id = "event" + sequence, sequence = sequence++, time_ms = time, kind, payload }, compact));
        void Image(string name, double time) => Event(time + 20, "observation", new
        { module_image = new { file = name + ".png", sha256 = GameExperiments.Digest(Path.Combine(_root, name + ".png")), captured_ms = time } });
        string[] images = ["reward", "snack", "outside", "graphics-menu", "native-idle"];
        string[] ids = ["review.m0/w101-postgame-menus/1", "review.m0/w101-postgame-menus/2", "review.m1/w101-native-idle/1", "review.m1/w101-native-idle/2"];
        string[] actions = ["pet-dance.next", "pet-dance.finish-without-snack", "game.open-menu", "wizard101.native-idle"];
        for (var i = 0; i < images.Length; i++)
        {
            Image(images[i], i * 1000); Image(images[i], i * 1000 + 500);
            if (i < actions.Length) Event(i * 1000 + 530, "action_receipt", new { routine_receipt = new RoutineReceipt(ids[i], actions[i], 999999, true, true) });
            if (i == 1) Image("loading", 1750);
        }
        File.WriteAllLines(Path.Combine(_root, "events.jsonl"), lines);
        return Output("session");
    }

    private static Task<ModuleVisualRecognition> SavedOcr(byte[] bytes, ModuleVisualFrame frame, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); Assert.Equal(frame.ImageSha256.ToLowerInvariant(), RoutineReplay.Digest(bytes));
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "Modules", "W101Menus", "recognition-cases.json")));
        var item = document.RootElement.GetProperty("cases").EnumerateArray().Single(item =>
            item.GetProperty("source_image_sha256").GetString()!.Equals(frame.ImageSha256, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(W101MenuRecognition.Classify(frame with { TextOrigin = "saved-ocr" },
            item.GetProperty("text").Deserialize<PetDanceMenuText>(OperationModules.Json)!));
    }

    [Fact]
    public async Task ActualPngsAndFreshLocalOcrCompletePostgameModuleReviewWithoutDesktopInput()
    {
        var snapshot = ModuleScreenReviewSnapshot.Load(Prepare());
        var result = await ModuleScreenReview.RunAsync(snapshot, Output("actual-ocr"), CancellationToken.None);
        Assert.True(result.Completed, $"{result.Reason} ({WindowsOcrWarmup.Summary})"); Assert.Equal(4, result.Proposals); Assert.Equal(4, result.Receipts); Assert.Equal(11, result.Frames);
        using var report = JsonDocument.Parse(File.ReadAllText(result.Report));
        Assert.Equal(0, report.RootElement.GetProperty("desktop_dispatch_count").GetInt32());
        Assert.False(report.RootElement.GetProperty("live_qualified").GetBoolean());
        Assert.False(report.RootElement.GetProperty("actual_input_alignment_verified").GetBoolean());
        Assert.All(report.RootElement.GetProperty("trace").EnumerateArray().Where(item => item.TryGetProperty("recognition", out _)),
            item => Assert.Equal("local-windows-ocr", item.GetProperty("recognition").GetProperty("frame").GetProperty("text_origin").GetString()));
    }

    [Fact]
    public async Task FrozenSourcesAreUsedAndOutputCannotOverwriteEvidence()
    {
        var manifest = Prepare(); var snapshot = ModuleScreenReviewSnapshot.Load(manifest);
        File.WriteAllText(Path.Combine(_root, "reward.png"), "changed after freezing");
        File.WriteAllText(Output("bundle"), "changed bundle");
        Assert.True((await ModuleScreenReview.RunAsync(snapshot, Output("frozen"), CancellationToken.None, SavedOcr)).Completed);
        await Assert.ThrowsAsync<IOException>(() => ModuleScreenReview.RunAsync(snapshot, Output("frozen"), CancellationToken.None, SavedOcr));
    }

    [Fact]
    public async Task MissingReceiptsKeepActualScreenChangesIncomplete()
    {
        var manifest = Prepare(); var events = Path.Combine(_root, "events.jsonl");
        File.WriteAllLines(events, File.ReadAllLines(events).Where(line => !line.Contains("action_receipt")).ToArray());
        var result = await ModuleScreenReview.RunAsync(ModuleScreenReviewSnapshot.Load(manifest), Output("missing"), CancellationToken.None, SavedOcr);
        Assert.False(result.Completed); Assert.Equal(1, result.Proposals); Assert.Equal(0, result.Receipts);
    }

    [Fact]
    public async Task CancellationPreservesPartialReportWithoutSuccess()
    {
        var snapshot = ModuleScreenReviewSnapshot.Load(Prepare()); using var stop = new CancellationTokenSource(); stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ModuleScreenReview.RunAsync(snapshot, Output("cancelled"), stop.Token, SavedOcr));
        using var report = JsonDocument.Parse(File.ReadAllText(Output("cancelled")));
        Assert.False(report.RootElement.GetProperty("completed").GetBoolean()); Assert.Equal(0, report.RootElement.GetProperty("frame_count").GetInt32());
    }

    [Theory]
    [InlineData("image-hash")]
    [InlineData("bundle-hash")]
    [InlineData("path")]
    [InlineData("geometry")]
    [InlineData("duplicate-event")]
    [InlineData("invented-live-proof")]
    public void InvalidEvidenceCannotEnterTheReview(string error)
    {
        var manifest = Prepare(); var data = JsonNode.Parse(File.ReadAllText(manifest))!;
        if (error == "image-hash") File.WriteAllBytes(Path.Combine(_root, "reward.png"), File.ReadAllBytes(Path.Combine(_root, "snack.png")));
        if (error == "bundle-hash") data["bundle_sha256"] = new string('0', 64);
        if (error == "path") data["events_file"] = "../events.jsonl";
        if (error == "geometry") data["geometry"]!["width"] = 1280;
        if (error == "invented-live-proof") data["evidence_mode"] = "live-verified";
        if (error == "duplicate-event") File.AppendAllText(Path.Combine(_root, "events.jsonl"), File.ReadLines(Path.Combine(_root, "events.jsonl")).First());
        File.WriteAllText(manifest, data.ToJsonString());
        Assert.ThrowsAny<Exception>(() => ModuleScreenReviewSnapshot.Load(manifest));
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
