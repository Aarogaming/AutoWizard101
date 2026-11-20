namespace ProjectMaelstrom.Utilities;

internal sealed record HeadingDecision(string State, string Reason, string? Key, int PulseMs,
    double HeadingError, double RadiansPerSecond);

/// <summary>Pure bounded turn controller. Proposals are distinct from dispatched
/// attempts; rates learn only from a completed turn and a fresh subsequent pose.
/// Coordinate evidence is not route, zone or obstacle qualification.</summary>
internal sealed class HeadingFeedback(double targetHeading, int maximumPulseMs)
{
    private WaypointPose? _origin, _previous;
    private HeadingDecision? _pending;
    private int _moves, _arrivals, _lastPulse, _lastDirection, _stalls;
    private double _rate = 2.5;
    private string? _stopped;
    internal int Moves => _moves;
    internal static double Wrap(double radians) => Math.IEEERemainder(radians, Math.PI * 2);

    internal HeadingDecision Observe(WaypointPose pose, long nowTicks)
    {
        var error = Wrap(targetHeading - pose.Heading);
        HeadingDecision Decision(string state, string reason, string? key = null, int pulse = 0) =>
            new(state, reason, key, pulse, error, _rate);
        HeadingDecision Stop(string reason) { _stopped = reason; return Decision("stopped", reason); }
        if (_stopped is not null) return Decision("stopped", _stopped);
        if (_pending is not null) return Stop("turn-receipt-required");
        if (maximumPulseMs is < 20 or > 300 || !double.IsFinite(targetHeading) || targetHeading < 0 || targetHeading > Math.PI * 2 ||
            !new[] { pose.X, pose.Y, pose.Z, pose.Heading, error }.All(double.IsFinite) ||
            pose.Timestamp <= 0 || pose.Timestamp > nowTicks || nowTicks - pose.Timestamp > TimeSpan.FromSeconds(2).Ticks)
            return Stop("invalid-or-stale-heading-pose");
        if (_previous is { } previous && pose.Timestamp <= previous.Timestamp) return Stop("pose-not-newer");
        _origin ??= pose;
        if (Distance(_origin, pose) > 20) return Stop("unexpected-translation-during-turn");
        if (_previous is { } prior && _lastPulse > 0)
        {
            var turned = Wrap(pose.Heading - prior.Heading);
            if (Math.Abs(turned) > 1.8) return Stop("implausible-heading-jump");
            if (turned * _lastDirection < -0.02) return Stop("turn-direction-mismatch");
            _stalls = Math.Abs(turned) < 0.01 ? _stalls + 1 : 0;
            if (_stalls >= 2) return Stop("turn-stalled");
            if (Math.Abs(error) > Math.Abs(Wrap(targetHeading - prior.Heading)) + 0.06)
                return Stop("turn-diverged-from-target");
            if (Math.Abs(turned) >= 0.01) _rate = Math.Clamp(Math.Abs(turned) * 1000 / _lastPulse, 0.5, 6);
        }
        _previous = pose; _lastPulse = 0;
        if (Math.Abs(error) <= 0.06)
            return Decision(++_arrivals >= 2 ? "arrived" : "confirm", "heading-coordinate-aligned");
        _arrivals = 0;
        if (_moves >= 4) return Stop("turn-budget-exhausted");
        var milliseconds = Math.Clamp((int)Math.Floor(Math.Abs(error) / _rate * 850), 20, maximumPulseMs);
        _pending = Decision("turn", "bounded-heading-correction", error < 0 ? "A" : "D", milliseconds);
        return _pending;
    }

    /// <summary>Call exactly once after a dispatch attempt. An interrupted but
    /// released pulse consumes an attempt but never calibrates the turn rate.</summary>
    internal void RecordTurn(HeadingDecision proposal, bool completed)
    {
        if (_pending is null || !ReferenceEquals(_pending, proposal))
            throw new InvalidOperationException("Record the exact outstanding heading proposal.");
        _moves++;
        _lastPulse = completed ? proposal.PulseMs : 0;
        _lastDirection = proposal.Key == "A" ? -1 : 1;
        _pending = null;
    }

    internal void DiscardProposal(HeadingDecision proposal)
    {
        if (_pending is null || !ReferenceEquals(_pending, proposal))
            throw new InvalidOperationException("Discard the exact outstanding heading proposal.");
        _pending = null;
    }

    private static double Distance(WaypointPose a, WaypointPose b) =>
        Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2) + Math.Pow(a.Z - b.Z, 2));
}
