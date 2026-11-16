namespace ProjectMaelstrom.Utilities;

internal sealed record PathPoint(double X, double Y, double Z, double Clearance);
/// <summary>Heading is canonical counterclockwise radians from +X, not client yaw.
/// Context binds target lifetime, zone/instance epoch, route/assets and movement profile.
/// TimeMs is acquisition time on the same monotonic clock passed to Observe.</summary>
internal sealed record PathObservation(double X, double Y, double Z, double Heading, double TimeMs,
    string Zone, string Context, bool Fresh = true, bool Loading = false, bool Focused = true, bool Stopped = false);
/// <summary>Turn is canonical angular direction (-1, 0, +1), never a key binding.
/// ValidUntilMs is an absolute expiry; the input owner must enforce it independently.</summary>
internal sealed record PathMotion(string State, string Reason, bool Forward, int Turn = 0,
    double CrossTrack = 0, double Remaining = 0, double HeadingError = 0, double ValidUntilMs = 0,
    long SequenceId = 0, double IssuedMs = 0);
/// <summary>Acceptance, observed motion and release are separate evidence. A missing
/// release keeps the follower blocked even after focus loss or an operator stop.</summary>
internal sealed record PathMotionReceipt(long SequenceId, bool DispatchAccepted, double ReportedMs,
    double? DispatchedMs = null, double? MotionObservedMs = null, double? ReleasedMs = null, bool? ForwardApplied = null);
internal sealed record PathFollowerOptions(string Zone, string Context, double Lookahead = 100,
    double ArrivalRadius = 25, double VerticalTolerance = 25, double TurnDeadband = 0.04,
    double ForwardAngleLimit = 0.65, double StuckMs = 1500, double MinimumProgress = 5,
    double MaximumActiveMs = 60000, double MaximumSampleGapMs = 250, double IntentLifetimeMs = 100,
    double MaximumSampleAgeMs = 150, double MaximumWaitMs = 2000, double MaximumWallMs = 360000,
    double ReleaseGraceMs = 250);

/// <summary>Pure polyline decisions over an externally qualified corridor. No input,
/// focus, clock, release watchdog or Idle side effects. Defaults are fixture/prototype
/// limits, not measured W101 safety bounds. The owner supplies qualified timing limits
/// and clearance, validates named arrival and performs cleanup. Reverse is unsupported.</summary>
internal sealed class PathFollower
{
    private readonly PathPoint[] _path;
    private readonly PathFollowerOptions _options;
    private readonly double[] _lengths;
    private readonly double _totalLength;
    private int _segment, _arrivals;
    private long _sequence;
    private double? _started, _lastNow, _lastSample, _waitingSince, _lastReceiptTime, _latestSeenNow;
    private double _activeMs, _stalledMs, _bestProgress, _releasedEffort;
    private bool _paused, _alignAfterCorner;
    private string? _terminalState, _terminalReason;
    private PathMotion? _pending;
    private PathMotionReceipt? _receipt;

    internal PathFollower(IReadOnlyList<PathPoint> path, PathFollowerOptions options)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(options);
        if (path.Count is < 2 or > 4096 || string.IsNullOrWhiteSpace(options.Zone) || string.IsNullOrWhiteSpace(options.Context) ||
            !new[] { options.Lookahead, options.ArrivalRadius, options.VerticalTolerance, options.TurnDeadband,
                options.ForwardAngleLimit, options.StuckMs, options.MinimumProgress, options.MaximumActiveMs,
                options.MaximumSampleGapMs, options.IntentLifetimeMs, options.MaximumSampleAgeMs,
                options.MaximumWaitMs, options.MaximumWallMs, options.ReleaseGraceMs }.All(v => double.IsFinite(v) && v > 0) ||
            options.TurnDeadband >= options.ForwardAngleLimit || options.ForwardAngleLimit > Math.PI / 2 ||
            options.MaximumActiveMs > 600000 || options.MaximumWallMs > 3600000 ||
            options.MaximumWallMs < options.MaximumActiveMs || options.IntentLifetimeMs > options.MaximumSampleGapMs)
            throw new InvalidDataException("Invalid path or bounded follower options.");
        _path = path.ToArray();
        _options = options;
        _lengths = new double[path.Count - 1];
        for (var i = 0; i < _path.Length; i++)
        {
            var p = _path[i];
            if (p is null || !new[] { p.X, p.Y, p.Z, p.Clearance }.All(double.IsFinite) || p.Clearance <= 0)
                throw new InvalidDataException("A finite, positive qualified corridor clearance is required.");
            if (i == 0) continue;
            _lengths[i - 1] = Distance(_path[i - 1].X, _path[i - 1].Y, p.X, p.Y);
            if (!double.IsFinite(_lengths[i - 1]) || _lengths[i - 1] < 0.001)
                throw new InvalidDataException("Duplicate, overflowing or vertical-only path segment.");
        }
        _totalLength = _lengths.Sum();
        if (!double.IsFinite(_totalLength)) throw new InvalidDataException("Path length overflow.");
    }

    private static PathMotion Hold(string state, string reason) => new(state, reason, false);
    private void Invalidate(string state, string reason)
    {
        _terminalState ??= state;
        _terminalReason ??= reason;
        _releasedEffort = 0;
        _arrivals = 0;
    }

    internal PathMotion Observe(PathObservation sample, double nowMs)
    {
        ArgumentNullException.ThrowIfNull(sample);
        if (!double.IsFinite(nowMs) || nowMs < 0 || _latestSeenNow is { } lastNow && nowMs < lastNow ||
            _lastReceiptTime is { } receiptTime && nowMs < receiptTime)
            Invalidate("invalidated", "invalid-monotonic-clock");
        else _latestSeenNow = nowMs;
        if (sample.Stopped) Invalidate("stopped", "operator-stop");
        if (sample.Loading) Invalidate("invalidated", "loading-requires-new-route-context");
        if (sample.Zone != _options.Zone || sample.Context != _options.Context)
            Invalidate("invalidated", "localization-context-changed");
        if (!sample.Focused)
        {
            _paused = true;
            _releasedEffort = 0;
            _arrivals = 0;
        }

        // Never erase pending input evidence on a stop/pause/transition. Only an
        // explicit rejection or a release receipt can close the outstanding intent.
        if (_pending is not null)
        {
            if (double.IsFinite(nowMs) && nowMs > _pending.ValidUntilMs + _options.ReleaseGraceMs)
                Invalidate("invalidated", "release-confirmation-overdue");
            return Hold("awaiting-release", _terminalReason ?? (_receipt is null ? "dispatch-receipt-required" : "release-receipt-required"));
        }
        if (_terminalState is not null) return Hold(_terminalState, _terminalReason!);

        _started ??= nowMs;
        var delta = _lastNow is { } previous ? nowMs - previous : 0;
        _lastNow = nowMs;
        if (nowMs - _started > _options.MaximumWallMs)
        {
            Invalidate("invalidated", "route-wall-budget-exhausted");
            return Hold(_terminalState!, _terminalReason!);
        }
        if (!sample.Focused)
        {
            _paused = true;
            _releasedEffort = 0;
            _arrivals = 0;
            _waitingSince = null;
            return Hold("paused", "focus-paused");
        }
        if (!new[] { sample.X, sample.Y, sample.Z, sample.Heading, sample.TimeMs }.All(double.IsFinite) ||
            sample.TimeMs < 0 || sample.TimeMs > nowMs)
        {
            Invalidate("invalidated", "invalid-pose");
            return Hold(_terminalState!, _terminalReason!);
        }
        if (!sample.Fresh || nowMs - sample.TimeMs > _options.MaximumSampleAgeMs ||
            _lastSample is { } prior && sample.TimeMs <= prior)
        {
            _waitingSince ??= nowMs;
            _releasedEffort = 0;
            _arrivals = 0;
            if (nowMs - _waitingSince > _options.MaximumWaitMs)
            {
                Invalidate("invalidated", "fresh-pose-wait-exhausted");
                return Hold(_terminalState!, _terminalReason!);
            }
            return Hold("waiting", "fresh-pose-required");
        }
        if (!_paused && _lastSample is { } priorSample && sample.TimeMs - priorSample > _options.MaximumSampleGapMs)
        {
            Invalidate("invalidated", "pose-stream-gap");
            return Hold(_terminalState!, _terminalReason!);
        }
        if (_paused) delta = 0;
        _paused = false;
        _waitingSince = null;
        _lastSample = sample.TimeMs;
        _activeMs += delta;
        if (_activeMs > _options.MaximumActiveMs)
        {
            Invalidate("invalidated", "route-active-budget-exhausted");
            return Hold(_terminalState!, _terminalReason!);
        }

        var a = _path[_segment];
        var b = _path[_segment + 1];
        var length = _lengths[_segment];
        var t = Math.Clamp(((sample.X - a.X) * (b.X - a.X) + (sample.Y - a.Y) * (b.Y - a.Y)) / (length * length), 0, 1);
        var px = a.X + (b.X - a.X) * t;
        var py = a.Y + (b.Y - a.Y) * t;
        var cross = Distance(sample.X, sample.Y, px, py);
        var clearance = Math.Min(a.Clearance, b.Clearance);
        if (!double.IsFinite(cross) || cross > clearance || Math.Abs(sample.Z - (a.Z + (b.Z - a.Z) * t)) > _options.VerticalTolerance)
        {
            Invalidate("invalidated", "outside-qualified-corridor-or-elevation");
            return Hold(_terminalState!, _terminalReason!);
        }
        var progress = _lengths.Take(_segment).Sum() + length * t;
        var remaining = _totalLength - progress;
        if (progress >= _bestProgress + _options.MinimumProgress)
        {
            _bestProgress = progress;
            _stalledMs = 0;
        }
        else _stalledMs += _releasedEffort;
        _releasedEffort = 0;
        if (_stalledMs >= _options.StuckMs)
        {
            Invalidate("blocked", "forward-progress-stalled");
            return Hold(_terminalState!, _terminalReason!);
        }

        var atEnd = Distance(sample.X, sample.Y, b.X, b.Y) <= Math.Min(_options.ArrivalRadius, clearance) &&
            Math.Abs(sample.Z - b.Z) <= _options.VerticalTolerance;
        if (atEnd)
        {
            if (_segment == _lengths.Length - 1)
                return new(++_arrivals >= 2 ? "arrived" : "confirm", "coordinate-arrival-only", false, CrossTrack: cross, Remaining: remaining);
            // Stop at a corner before advancing one adjacent segment. The next
            // observation must validate the next corridor and align in place.
            _segment++;
            _alignAfterCorner = true;
            _arrivals = 0;
            return Hold("corner", "corner-stop-before-next-segment");
        }
        _arrivals = 0;
        var ahead = Math.Min(1, t + _options.Lookahead / length);
        var x = a.X + (b.X - a.X) * ahead;
        var y = a.Y + (b.Y - a.Y) * ahead;
        var desired = Math.Atan2(y - sample.Y, x - sample.X);
        var error = Math.IEEERemainder(desired - sample.Heading, Math.Tau);
        var turn = Math.Abs(error) <= _options.TurnDeadband ? 0 : Math.Sign(error);
        if (turn == 0) _alignAfterCorner = false;
        _pending = new("follow", "bounded-corridor-intent", !_alignAfterCorner && Math.Abs(error) < _options.ForwardAngleLimit,
            turn, cross, remaining, error, nowMs + _options.IntentLifetimeMs, ++_sequence, nowMs);
        return _pending;
    }

    internal void RecordApplied(PathMotion motion, PathMotionReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        if (_pending is null || !ReferenceEquals(_pending, motion) || receipt.SequenceId != motion.SequenceId)
            throw new InvalidOperationException("Acknowledge the exact current motion intention.");
        bool ValidTime(double? value) => value is null || double.IsFinite(value.Value) && value >= motion.IssuedMs && value <= receipt.ReportedMs;
        if (!double.IsFinite(receipt.ReportedMs) || receipt.ReportedMs < motion.IssuedMs ||
            _latestSeenNow is { } observationTime && receipt.ReportedMs < observationTime ||
            !ValidTime(receipt.DispatchedMs) || !ValidTime(receipt.MotionObservedMs) || !ValidTime(receipt.ReleasedMs) ||
            _receipt is { } previous && (receipt.ReportedMs < previous.ReportedMs || receipt.DispatchedMs != previous.DispatchedMs ||
                receipt.DispatchAccepted != previous.DispatchAccepted || previous.MotionObservedMs is not null && receipt.MotionObservedMs != previous.MotionObservedMs) ||
            receipt.DispatchAccepted && (receipt.DispatchedMs is null || receipt.DispatchedMs > motion.ValidUntilMs) ||
            !receipt.DispatchAccepted && (receipt.DispatchedMs is not null || receipt.MotionObservedMs is not null || receipt.ReleasedMs is not null) ||
            receipt.MotionObservedMs is { } onset && onset < receipt.DispatchedMs ||
            receipt.ReleasedMs is { } release && (release < receipt.DispatchedMs || receipt.MotionObservedMs > release))
            throw new InvalidDataException("Invalid motion receipt chronology or acceptance.");
        _receipt = receipt;
        _lastReceiptTime = receipt.ReportedMs;
        if (receipt.DispatchAccepted && receipt.ReleasedMs is null) return;
        if (receipt.DispatchAccepted)
        {
            // Accepted effort is not proof of displacement. Actual progress is
            // evaluated from the subsequent pose; motion onset is retained separately.
            if (motion.Forward && receipt.ForwardApplied != false && _terminalState is null && !_paused)
                _releasedEffort += receipt.ReleasedMs!.Value - receipt.DispatchedMs!.Value;
            if (receipt.ReleasedMs > motion.ValidUntilMs + _options.ReleaseGraceMs)
                Invalidate("invalidated", "release-confirmation-overdue");
        }
        else _releasedEffort = 0;
        _pending = null;
        _receipt = null;
    }

    private static double Distance(double ax, double ay, double bx, double by) => Math.Sqrt(Math.Pow(bx - ax, 2) + Math.Pow(by - ay, 2));
}
