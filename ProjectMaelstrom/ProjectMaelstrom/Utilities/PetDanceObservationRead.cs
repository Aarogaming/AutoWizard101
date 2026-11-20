namespace ProjectMaelstrom.Utilities;

/// <summary>Abandon one pending read on timeout; never start a replacement read.
/// The capture owner still has to await or report its own pending cleanup.</summary>
internal static class PetDanceObservationRead
{
    internal static async Task<CapturedWindowFrame?> ReadAsync(Func<Task<CapturedWindowFrame?>> read,
        CancellationToken token, int timeoutMs = 3000)
    {
        var pending = read();
        try { return await pending.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), token).ConfigureAwait(false); }
        catch
        {
            _ = pending.ContinueWith(completed =>
            {
                if (completed.IsCompletedSuccessfully) completed.Result?.Dispose();
                else _ = completed.Exception;
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            throw;
        }
    }
}
