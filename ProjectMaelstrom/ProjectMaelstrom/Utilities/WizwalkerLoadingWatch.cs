using System.Diagnostics;
using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>A finite observation series, under one existing scheduler/control lease.
/// Reuses the reviewed one-shot worker and its disconnect check; never injects input.</summary>
internal static class WizwalkerLoadingWatch
{
    internal const string Id = "w101.wizwalker-loading-watch";
    internal const string Version = "wizwalker-loading-watch-v1";
    internal const int MaximumSamples = 32;
    internal const int WatchMilliseconds = 15000;
    internal const int MaximumWatchSeconds = 60;

    internal static OperationTicket Submit(OperationScheduler scheduler, WizwalkerSession session, WizwalkerLaunch launch,
        WizwalkerRuntime? runtime = null, int durationSeconds = 15)
    {
        if (durationSeconds is < 1 or > MaximumWatchSeconds) throw new ArgumentOutOfRangeException(nameof(durationSeconds));
        var request = WizwalkerOperation.Prepare(session, launch, runtime, Id, Version) with { TimeoutSeconds = durationSeconds + 25 };
        return scheduler.Submit(request, (context, token) => RunOwnedAsync(context, session.Target, launch, token, runtime,
            (int)Math.Ceiling(MaximumSamples * durationSeconds / 15d), durationSeconds * 1000));
    }

    internal static async Task<OperationOutcome> RunOwnedAsync(OperationContext context, WizwalkerTarget target,
        WizwalkerLaunch launch, CancellationToken token, WizwalkerRuntime? runtime = null,
        int maximumSamples = MaximumSamples, int watchMilliseconds = WatchMilliseconds)
    {
        if (maximumSamples is < 1 or > 128 || watchMilliseconds is < 1 or > MaximumWatchSeconds * 1000)
            throw new ArgumentOutOfRangeException(nameof(maximumSamples));
        var trace = Path.Combine(context.Directory, "loading-observations.jsonl");
        var summary = Path.Combine(context.Directory, "loading-watch.json");
        var watch = Stopwatch.StartNew();
        int count = 0, loading = 0; bool sawClear = false, sawLoadingAfterClear = false, sawReturn = false;
        var state = OperationState.Completed; var reason = "bounded-watch-finished";
        using var stream = new FileStream(trace, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(stream);
        try
        {
            while (count < maximumSamples && watch.ElapsedMilliseconds < watchMilliseconds)
            {
                token.ThrowIfCancellationRequested();
                var folder = Path.Combine(context.Directory, "sample-" + count.ToString("D3"));
                Directory.CreateDirectory(folder);
                var outcome = await WizwalkerOperation.ReadOwnedAsync(context with { Directory = folder }, target, launch, runtime, token).ConfigureAwait(false);
                if (outcome.State != OperationState.Completed) { state = outcome.State; reason = outcome.Message; break; }
                var result = JsonSerializer.Deserialize<WizwalkerResult>(GameExperiments.BoundedBytes(Path.Combine(folder, "adapter-result.json"), 16384))!;
                await writer.WriteLineAsync(JsonSerializer.Serialize(new { index = count, elapsedMs = watch.ElapsedMilliseconds, result }, WizwalkerOperation.Json)).ConfigureAwait(false);
                await writer.FlushAsync().ConfigureAwait(false);
                count++;
                if (result.Loading == true) { loading++; if (sawClear) sawLoadingAfterClear = true; }
                else { sawClear = true; if (sawLoadingAfterClear) sawReturn = true; }
                if (count < maximumSamples) await Task.Delay(100, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { state = OperationState.Cancelled; reason = "watch-cancelled"; }
        catch
        {
            state = context.Input?.RecoveryRequired == true ? OperationState.CleanupFailed : OperationState.Failed;
            reason = "watch-observation-failed";
            throw;
        }
        finally
        {
            if (state == OperationState.Completed && count == 0) { state = OperationState.Unknown; reason = "no-observations-within-budget"; }
            GameExperiments.WriteAtomic(summary, new { schemaVersion = 1, samples = count, loadingSamples = loading,
                observedClearLoadingClear = sawReturn, durationMs = watch.ElapsedMilliseconds, state, reason,
                desktopDispatchCount = 0, qualification = "Native flag sequence only; compare independently observed game screens." });
        }
        return new(state, $"{reason}: {count} observations; {loading} loading. No game inputs sent.", [trace, summary]);
    }
}
