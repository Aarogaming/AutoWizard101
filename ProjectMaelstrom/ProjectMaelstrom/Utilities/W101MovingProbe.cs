using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>One bounded overlap under an existing owner. The observer alone owns
/// its hook; movement still uses the normal target-bound dispatcher. No new lease.</summary>
internal static class W101MovingProbe
{
    internal static bool WindowReady(string trace, long nowTicks)
    {
        if (trace.Length > 4 * 1024 * 1024) return false;
        var end = trace.LastIndexOf('\n');
        if (end < 0 || end != trace.Length - 1) return false;
        try
        {
            foreach (var line in trace[..end].Split('\n', StringSplitOptions.RemoveEmptyEntries).Reverse())
            {
                using var doc = JsonDocument.Parse(line);
                var r = doc.RootElement;
                if (r.ValueKind != JsonValueKind.Object || !r.TryGetProperty("kind", out var kind) || kind.ValueKind != JsonValueKind.String)
                    return false;
                var eventKind = kind.GetString();
                // A completed or invalidated stream revokes its earlier ready event,
                // even when both timestamps fit the readiness freshness window.
                if (eventKind is "moving-pose-ended" or "moving-pose-invalidated") return false;
                if (eventKind != "pose-window-ready") continue;
                if (!r.TryGetProperty("observedUtcTicks", out var time) || time.ValueKind != JsonValueKind.Number || !time.TryGetInt64(out var stamp))
                    return false;
                return r.TryGetProperty("safe", out var safe) && safe.ValueKind == JsonValueKind.True &&
                    stamp > 0 && stamp <= nowTicks && nowTicks - stamp <= TimeSpan.FromMilliseconds(100).Ticks;
            }
        }
        catch (JsonException) { return false; }
        return false;
    }

    internal static async Task<RoutineReceipt> RunAsync(Func<CancellationToken, Task> observe,
        Func<bool> ready, Func<bool> owned, Func<Task<RoutineReceipt>> move,
        RoutineReceipt rejected, CancellationToken token)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(token);
        stop.CancelAfter(TimeSpan.FromSeconds(8));
        var reading = observe(stop.Token);
        try
        {
            while (!reading.IsCompleted && owned() && !stop.IsCancellationRequested)
            {
                if (ready()) return await move().ConfigureAwait(false);
                await Task.Delay(10, stop.Token).ConfigureAwait(false);
            }
            return rejected;
        }
        finally
        {
            // Keep the lease until the observer has restored its hook; no detached task.
            if (!owned() || token.IsCancellationRequested) stop.Cancel();
            await reading.ConfigureAwait(false);
        }
    }
}
