namespace ProjectMaelstrom.Utilities;

/// <summary>Shared bounded transition wait. Loading and missing observations invalidate
/// prior destination evidence; neither authorizes retrying an already dispatched action.
/// Two fresh destination observations resume the next step. The deadline never resets.</summary>
internal sealed class LoadingTransitionGate
{
    private readonly double _started;
    private readonly double _timeout;
    private double _last = double.NegativeInfinity;
    private int _readySamples;
    internal string State { get; private set; } = "waiting";

    internal LoadingTransitionGate(double startedMs, double timeoutMs)
    {
        if (!double.IsFinite(startedMs) || !double.IsFinite(timeoutMs) || timeoutMs is < 1 or > 120000)
            throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        _started = startedMs; _timeout = timeoutMs;
    }

    internal string Observe(double nowMs, bool? loading, bool destinationVerified, bool fresh, bool cancelled = false)
    {
        if (State is "ready" or "stopped" or "timed-out") return State;
        if (cancelled || !double.IsFinite(nowMs) || nowMs < _started) return State = "stopped";
        if (nowMs - _started >= _timeout) return State = "timed-out";
        if (!fresh || nowMs <= _last) { _readySamples = 0; return State = "waiting-for-evidence"; }
        _last = nowMs;
        if (loading != false || !destinationVerified)
        {
            _readySamples = 0;
            return State = loading == true ? "paused-for-loading" : "waiting-for-evidence";
        }
        return State = ++_readySamples >= 2 ? "ready" : "confirming-destination";
    }
}
