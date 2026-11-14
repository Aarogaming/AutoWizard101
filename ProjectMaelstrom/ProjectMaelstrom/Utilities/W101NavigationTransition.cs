namespace ProjectMaelstrom.Utilities;

/// <summary>After key release, record a bounded loading edge without replaying movement.
/// A cleared transition is not proof of destination identity.</summary>
internal static class W101NavigationTransition
{
    internal sealed record Result(bool Ready, bool SawLoading, int Samples, string Reason);
    internal sealed record WalkResult(RoutineReceipt Receipt, bool SawLoading, int Samples);

    /// <summary>One bounded hold, with serial observations and a freshness watchdog.
    /// The input owner still independently checks takeover and releases the key.</summary>
    internal static async Task<WalkResult> WalkAsync(Func<CancellationToken, Task<bool?>> read,
        Func<Func<bool>, Task<RoutineReceipt>> move, Func<bool> owned, Func<double> clock, CancellationToken token, bool initialClear = false)
    {
        bool? loading = initialClear ? false : await read(token).ConfigureAwait(false);
        if (loading != false || !owned()) throw new InvalidOperationException("Walking needs a fresh clear loading observation.");
        var lastClear = clock();
        var keepMoving = true;
        var sawLoading = false;
        var samples = initialClear ? 0 : 1;
        var motion = move(() => keepMoving && owned() && clock() - lastClear <= 2500);
        try
        {
            while (!motion.IsCompleted && owned())
            {
                loading = await read(token).ConfigureAwait(false);
                samples++;
                if (loading != false)
                {
                    sawLoading |= loading == true;
                    keepMoving = false;
                    break;
                }
                lastClear = clock();
                await Task.Delay(100, token).ConfigureAwait(false);
            }
        }
        finally
        {
            keepMoving = false;
            await motion.ConfigureAwait(false);
        }
        return new(await motion.ConfigureAwait(false), sawLoading, samples);
    }

    internal static async Task<Result> WaitAsync(Func<CancellationToken, Task<bool?>> read,
        Func<bool> owned, Func<double> clock, CancellationToken token, bool requireLoading = false)
    {
        var start = clock();
        var gate = new LoadingTransitionGate(start, 20000);
        var sawLoading = false;
        for (var i = 0; i < 24; i++)
        {
            token.ThrowIfCancellationRequested();
            if (!owned()) return new(false, sawLoading, i, "control-changed");
            var loading = await read(token).ConfigureAwait(false);
            sawLoading |= loading == true;
            if (!owned()) return new(false, sawLoading, i + 1, "control-changed");
            var state = gate.Observe(clock(), requireLoading && !sawLoading ? null : loading, loading == false, true);
            if (state == "ready") return new(true, sawLoading, i + 1, sawLoading ? "loading-cleared-destination-unverified" : "no-loading-observed");
            if (state is "timed-out" or "stopped") return new(false, sawLoading, i + 1, state);
        }
        return new(false, sawLoading, 24, "sample-budget-exhausted");
    }
}
