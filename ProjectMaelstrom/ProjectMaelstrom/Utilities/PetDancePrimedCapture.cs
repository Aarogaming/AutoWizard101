namespace ProjectMaelstrom.Utilities;

internal sealed record PetDanceStartReceipt(bool Accepted, bool InputReleased);

/// <summary>Prime the sole game capture before Play and keep observing while its
/// bounded dispatch finishes. No buffered old frame ever grants input permission.</summary>
internal sealed class PetDancePrimedCapture : IDisposable
{
    private readonly CancellationTokenSource _stop;
    private readonly Task<PetDanceStartReceipt> _play;
    internal double ReadyCaptureMs { get; }
    internal bool Pending => !_play.IsCompleted;
    internal bool Accepted => _play.IsCompletedSuccessfully && _play.Result is { Accepted: true, InputReleased: true };
    private PetDancePrimedCapture(double captured, Func<bool> ready, Func<CancellationToken, Task<PetDanceStartReceipt>> play, CancellationToken token)
    {
        ReadyCaptureMs = captured;
        _stop = CancellationTokenSource.CreateLinkedTokenSource(token);
        _stop.CancelAfter(TimeSpan.FromSeconds(3));
        // The scheduler's execution context/lease flows to this one bounded
        // dispatch. The calling specialist continues observing concurrently.
        _play = Task.Run(async () =>
        {
            _stop.Token.ThrowIfCancellationRequested();
            if (!ready()) return new PetDanceStartReceipt(false, true);
            return await play(_stop.Token).ConfigureAwait(false);
        }, _stop.Token);
    }

    internal static async Task<PetDancePrimedCapture> StartAsync(Func<Task<CapturedWindowFrame?>> read,
        Func<bool> ready, Func<CancellationToken, Task<PetDanceStartReceipt>> play, Func<double> now, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(read); ArgumentNullException.ThrowIfNull(ready);
        ArgumentNullException.ThrowIfNull(play); ArgumentNullException.ThrowIfNull(now);
        var started = now();
        bool WithinBudget(double current) => double.IsFinite(started) && double.IsFinite(current) &&
            current >= started && current - started <= 2000;
        for (var attempt = 0; attempt < 200; attempt++)
        {
            token.ThrowIfCancellationRequested();
            if (!ready() || !WithinBudget(now()))
                throw new InvalidOperationException("Capture was not ready before Play; no entry dispatched.");
            using var frame = await PetDanceObservationRead.ReadAsync(read, token, 1000).ConfigureAwait(false);
            var observed = now();
            if (!WithinBudget(observed))
                throw new InvalidOperationException("Capture preparation expired while awaiting a frame; no entry dispatched.");
            if (frame?.CaptureTime is { } captured &&
                PetDanceTrialFrameGate.MayDispatch(captured.TotalMilliseconds, observed) && ready())
                return new(captured.TotalMilliseconds, () =>
                {
                    var dispatchTime = now();
                    return WithinBudget(dispatchTime) &&
                        PetDanceTrialFrameGate.MayDispatch(captured.TotalMilliseconds, dispatchTime) && ready();
                }, play, token);
            await Task.Delay(10, token).ConfigureAwait(false);
        }
        throw new InvalidOperationException("Capture priming budget exhausted; no entry dispatched.");
    }

    internal void CheckDispatch()
    {
        if (_stop.IsCancellationRequested && !Accepted || _play.IsCompleted && !Accepted)
            throw new InvalidOperationException("Play dispatch failed or exceeded its bounded capture-start window.");
    }

    internal async Task<bool> FinishAsync()
    {
        _stop.Cancel();
        try { return (await _play.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false)).InputReleased; }
        catch { return false; } // Task completion is not input-release evidence.
    }
    public void Dispose() => _stop.Dispose();
}
