namespace ProjectMaelstrom.Utilities;

/// <summary>Bounded, offline-testable contract for a resident memory-only W101 reader.
///
/// Runs under the single Maelstrom owner, publishes into the shared fact store, and
/// deliberately has NO foreground/window-focus gate: observation does not require the
/// game to be frontmost. Interaction remains a separate foreground-only path.
///
/// Every cycle re-validates exact process identity (HWND/PID/start ticks/image hash).
/// Any mismatch, fault, cancellation or recovery hold invalidates all volatile facts
/// and stops the reader; it never auto-retargets or guesses a replacement process.
/// No input, capture, network or game-file access lives here.</summary>
internal sealed class W101BackgroundReader
{
    internal const int DefaultIntervalMs = 500;
    internal const int MaximumIntervalMs = 5000;
    internal const int MaximumCycles = 7200; // 1 hour at 500 ms

    private readonly Func<(bool Valid, string Reason)> _validateTarget;
    private readonly Func<CancellationToken, Task<IReadOnlyList<W101Fact>>> _readCycle;
    private readonly Action<string> _invalidate;
    private readonly Func<long> _utcTicks;
    private readonly Func<int, CancellationToken, Task> _delay;
    private readonly int _intervalMs;
    private readonly int _maximumCycles;
    private readonly Func<bool> _recoveryRequired;

    internal W101BackgroundReader(Func<(bool, string)> validateTarget,
        Func<CancellationToken, Task<IReadOnlyList<W101Fact>>> readCycle,
        Action<string> invalidate,
        Func<bool>? recoveryRequired = null,
        Func<long>? utcTicks = null,
        Func<int, CancellationToken, Task>? delay = null,
        int intervalMs = DefaultIntervalMs,
        int maximumCycles = MaximumCycles)
    {
        ArgumentNullException.ThrowIfNull(validateTarget);
        ArgumentNullException.ThrowIfNull(readCycle);
        ArgumentNullException.ThrowIfNull(invalidate);
        if (intervalMs is < 1 or > MaximumIntervalMs) throw new ArgumentOutOfRangeException(nameof(intervalMs));
        if (maximumCycles is < 1 or > MaximumCycles) throw new ArgumentOutOfRangeException(nameof(maximumCycles));
        _validateTarget = validateTarget;
        _readCycle = readCycle;
        _invalidate = invalidate;
        _recoveryRequired = recoveryRequired ?? (() => false);
        _utcTicks = utcTicks ?? (() => DateTime.UtcNow.Ticks);
        _delay = delay ?? ((ms, token) => Task.Delay(ms, token));
        _intervalMs = intervalMs;
        _maximumCycles = maximumCycles;
    }

    /// <summary>Runs until cancelled, budget exhausted, or identity/fault fails.
    /// Returns a bounded reason code; a failed identity is never silently recovered.</summary>
    internal async Task<string> RunAsync(Action<W101Fact> publish, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(publish);
        for (var cycle = 0; cycle < _maximumCycles; cycle++)
        {
            if (token.IsCancellationRequested) return "cancelled";
            if (_recoveryRequired())
            {
                _invalidate("recovery-hold");
                return "recovery-hold";
            }
            var target = _validateTarget();
            if (!target.Valid)
            {
                _invalidate(string.IsNullOrWhiteSpace(target.Reason) ? "target-changed" : target.Reason);
                return "target-changed";
            }
            try
            {
                var facts = await _readCycle(token).ConfigureAwait(false);
                if (facts is null) throw new InvalidDataException("reader returned no result");
                var now = _utcTicks();
                foreach (var fact in facts) publish(fact with { ObservedUtcTicks = now });
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                _invalidate("cancelled");
                return "cancelled";
            }
            catch
            {
                _invalidate("reader-fault");
                return "reader-fault";
            }
            if (cycle + 1 < _maximumCycles) await _delay(_intervalMs, token).ConfigureAwait(false);
        }
        return "cycle-budget";
    }
}
