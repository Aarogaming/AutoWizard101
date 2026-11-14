using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>A single sequential producer, shared cancellation and durable batch receipts.
/// The recording owns its lifetime. Producers must finish cleanup before returning.</summary>
internal static class TeachingTelemetryLoop
{
    public static async Task<string[]> CollectAsync(Func<CancellationToken, Task<string?>> read,
        CancellationToken stop, int maximumBatches = 240)
    {
        if (maximumBatches is < 1 or > 240) throw new ArgumentOutOfRangeException(nameof(maximumBatches));
        var batches = new List<string>();
        for (var index = 0; index < maximumBatches && !stop.IsCancellationRequested; index++)
        {
            var path = await read(stop);
            if (path != null) batches.Add(path);
            if (!stop.IsCancellationRequested) await Task.Delay(250, stop).ContinueWith(_ => { }, TaskScheduler.Default);
        }
        if (!stop.IsCancellationRequested) throw new InvalidOperationException("Telemetry batch budget exhausted; stop recording rather than silently lose state coverage.");
        return batches.ToArray();
    }

    public static async Task<string[]> CollectW101Async(string workspace, string dataRoot, GameWindow window, CancellationToken stop)
    {
        var target = WizwalkerTarget.FromWindow(window);
        var launch = WizwalkerLaunch.FromWorkspace(workspace) with { Worker = Path.Combine(workspace, "tools", "w101_ui_tree.py") };
        await using var scheduler = new OperationScheduler(dataRoot);
        return await CollectAsync(async token =>
        {
            token.ThrowIfCancellationRequested();
            var ticket = W101UiTreeOperation.Submit(scheduler, new(target, true), launch, true, 15, includeQuestContext: true);
            using var cancellation = token.Register(ticket.Cancel);
            var receipt = await ticket.Completion;
            // Cancellation is only accepted after the worker recorded successful cleanup.
            var request = receipt.Artifacts.FirstOrDefault(p => Path.GetFileName(p) == "ui-request.json");
            var resultPath = receipt.Artifacts.FirstOrDefault(p => Path.GetFileName(p) == "ui-result.json");
            if (receipt.State is not (OperationState.Completed or OperationState.Cancelled) || resultPath == null || request == null)
                throw new InvalidOperationException("Telemetry stopped: " + receipt.Message);
            if (new FileInfo(resultPath).Length > W101UiTreeOperation.MaximumResultBytes) throw new InvalidDataException("Telemetry result exceeds budget.");
            var result = JsonSerializer.Deserialize<W101UiTreeResult>(File.ReadAllText(resultPath));
            if (result is not { CleanupConfirmed: true, HookRestored: true }) throw new InvalidOperationException("Observer disconnect is unconfirmed.");
            return request;
        }, stop);
    }

    public static string? MergeW101(string[] requests, string sessionPath, TeachingSession session)
    {
        if (requests.Length == 0) return null;
        var output = Path.Combine(Path.GetDirectoryName(sessionPath)!, "telemetry-merged-" + Guid.NewGuid().ToString("N") + ".jsonl");
        long bytes = 0; var count = 0; double last = -1;
        try
        {
            using (var writer = new StreamWriter(new FileStream(output, FileMode.CreateNew, FileAccess.Write)))
                for (var batch = 0; batch < requests.Length; batch++)
                {
                    var imported = W101TeachingStateImport.Import(requests[batch], sessionPath, session);
                    foreach (var sample in TeachingStateTrack.Read(imported, session))
                    {
                        if (sample.TimeMs < last) throw new InvalidDataException("Observer clock moved backwards; alignment requires review.");
                        last = sample.TimeMs;
                        // A reconnect is an explicit continuity boundary, never assumed same visit.
                        var line = JsonSerializer.Serialize(sample with { Context = $"batch-{batch}." + sample.Context });
                        bytes += System.Text.Encoding.UTF8.GetByteCount(line) + 2;
                        if (bytes > TeachingStateTrack.MaximumBytes || ++count > TeachingStateTrack.MaximumSamples)
                            throw new InvalidDataException("Telemetry review budget exceeded; raw observer batches remain available.");
                        writer.WriteLine(line);
                    }
                }
            return TeachingStateTrack.Attach(output, sessionPath, session);
        }
        finally { if (File.Exists(output)) File.Delete(output); }
    }
}
