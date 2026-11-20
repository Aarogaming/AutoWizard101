using System.Diagnostics;

namespace ProjectMaelstrom.Utilities;

internal sealed class WindowCaptureCleanupException(Exception cause)
    : Exception("Window capture cleanup could not be confirmed; restart Maelstrom before using game controls again.", cause);

/// <summary>Owns one short capture session. A stalled read is abandoned once, its late
/// image is disposed, and cleanup has a separate finite budget. A usable image is
/// transferred only after cleanup succeeds; cancellation never extends either budget.</summary>
internal static class BoundedWindowCapture
{
    internal const int CaptureTimeoutMs = 2000;
    internal const int CleanupTimeoutMs = 3000;

    internal static async Task<CapturedWindowFrame?> ReadAsync(Func<Task<CapturedWindowFrame?>> read,
        Func<Task> dispose, Func<CapturedWindowFrame, bool> accept, CancellationToken token,
        int captureTimeoutMs = CaptureTimeoutMs, int cleanupTimeoutMs = CleanupTimeoutMs)
    {
        if (captureTimeoutMs is < 1 or > CaptureTimeoutMs) throw new ArgumentOutOfRangeException(nameof(captureTimeoutMs));
        if (cleanupTimeoutMs is < 1 or > CleanupTimeoutMs) throw new ArgumentOutOfRangeException(nameof(cleanupTimeoutMs));
        CapturedWindowFrame? selected = null;
        var clock = Stopwatch.StartNew();
        try
        {
            while (clock.ElapsedMilliseconds < captureTimeoutMs)
            {
                token.ThrowIfCancellationRequested();
                var remaining = Math.Max(1, captureTimeoutMs - (int)clock.ElapsedMilliseconds);
                var frame = await PetDanceObservationRead.ReadAsync(read, token, remaining).ConfigureAwait(false);
                try
                {
                    token.ThrowIfCancellationRequested();
                    if (frame != null && accept(frame)) { selected = frame; frame = null; break; }
                }
                finally { frame?.Dispose(); }
                remaining = captureTimeoutMs - (int)clock.ElapsedMilliseconds;
                if (remaining > 0) await Task.Delay(Math.Min(25, remaining), token).ConfigureAwait(false);
            }
        }
        catch (TimeoutException) { /* No replacement read after a stall. Cleanup still owns its gate. */ }
        finally
        {
            Task? cleanup = null;
            try
            {
                // Cleanup deliberately ignores cancellation, but can never wait indefinitely.
                cleanup = dispose();
                await cleanup.WaitAsync(TimeSpan.FromMilliseconds(cleanupTimeoutMs)).ConfigureAwait(false);
            }
            catch (Exception error)
            {
                selected?.Dispose(); selected = null;
                if (cleanup != null)
                    _ = cleanup.ContinueWith(completed => { _ = completed.Exception; }, CancellationToken.None,
                        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                throw new WindowCaptureCleanupException(error);
            }
        }
        if (token.IsCancellationRequested) { selected?.Dispose(); token.ThrowIfCancellationRequested(); }
        return selected;
    }
}
