namespace ProjectMaelstrom.Utilities;

internal enum NavigationArrivalKind { Coordinate, InteractionPrompt, ZoneTransition }
internal enum NavigationArrivalState { Waiting, Confirming, Verified, Failed, TimedOut }

/// <summary>Trusted expected arrival, bound to the target lifetime and source localization
/// epoch captured before dispatch. A portal always requires a new localization epoch,
/// even when it returns to the same zone name. This contract does not authorize input.</summary>
internal sealed record NavigationArrivalContract(NavigationArrivalKind Kind, string TargetIdentity,
    string SourceZone, string SourceEpoch, string DestinationZone, string? ExpectedPrompt = null,
    NavigationPoint? DestinationPoint = null, double Radius = 25, double VerticalTolerance = 25);

/// <summary>TimeMs is acquisition time on the SAME host monotonic clock as dispatch
/// and Observe(nowMs), never raw Python monotonic seconds or UI UTC ticks. InputReleased
/// comes from the current input owner's receipt, not inferred from a still image.</summary>
internal sealed record NavigationArrivalObservation(double TimeMs, string TargetIdentity,
    string? Zone, string? Epoch, bool? Loading, bool Fresh, bool InputReleased,
    string? Prompt = null, NavigationPoint? Position = null, double? InputReleasedMs = null);

internal sealed record NavigationArrivalResult(NavigationArrivalState State, string Reason,
    bool SawLoading, double? LoadingObservedMs, double? ArrivalObservedMs, string? ArrivalEpoch);

/// <summary>Pure bounded post-dispatch verifier. Reuses the shared two-observation
/// transition gate; it never navigates, releases input, restores Idle or mutates a route.</summary>
internal sealed class NavigationArrivalGate
{
    private readonly NavigationArrivalContract _contract;
    private readonly double _dispatchedMs, _maximumAgeMs;
    private readonly LoadingTransitionGate _gate;
    private double _lastNow, _lastSample = double.NegativeInfinity;
    private double? _loadingObservedMs;
    private string? _confirmingEpoch;
    private NavigationArrivalResult? _terminal;

    internal NavigationArrivalGate(NavigationArrivalContract contract, double dispatchedMs,
        double timeoutMs, double maximumAgeMs)
    {
        ArgumentNullException.ThrowIfNull(contract);
        if (!Enum.IsDefined(contract.Kind) ||
            new[] { contract.TargetIdentity, contract.SourceZone, contract.SourceEpoch, contract.DestinationZone }.Any(string.IsNullOrWhiteSpace) ||
            !double.IsFinite(dispatchedMs) || dispatchedMs < 0 ||
            !double.IsFinite(maximumAgeMs) || maximumAgeMs <= 0 || maximumAgeMs > timeoutMs ||
            contract.Kind != NavigationArrivalKind.ZoneTransition && contract.DestinationZone != contract.SourceZone ||
            contract.Kind == NavigationArrivalKind.InteractionPrompt && string.IsNullOrWhiteSpace(contract.ExpectedPrompt) ||
            contract.Kind == NavigationArrivalKind.Coordinate && (contract.DestinationPoint is not { } p || !Finite(p) ||
                !double.IsFinite(contract.Radius) || contract.Radius <= 0 ||
                !double.IsFinite(contract.VerticalTolerance) || contract.VerticalTolerance <= 0))
            throw new InvalidDataException("Invalid bounded arrival contract.");
        _contract = contract;
        _dispatchedMs = dispatchedMs;
        _maximumAgeMs = maximumAgeMs;
        _lastNow = dispatchedMs;
        _gate = new(dispatchedMs, timeoutMs);
    }

    internal NavigationArrivalResult Observe(NavigationArrivalObservation sample, double nowMs, bool cancelled = false)
    {
        ArgumentNullException.ThrowIfNull(sample);
        if (_terminal is not null) return _terminal;
        if (cancelled) return Fail("arrival-cancelled");
        if (!double.IsFinite(nowMs) || nowMs < _lastNow) return Fail("invalid-host-clock");
        _lastNow = nowMs;
        if (sample.TargetIdentity != _contract.TargetIdentity) return Fail("target-lifetime-changed");

        bool fresh = sample.Fresh && double.IsFinite(sample.TimeMs) && sample.TimeMs > _dispatchedMs &&
            sample.TimeMs > _lastSample && sample.TimeMs <= nowMs && nowMs - sample.TimeMs <= _maximumAgeMs;
        // Retain the chronology watermark even when an observation is too old; a later
        // replay of that observation cannot supply a new loading edge or confirmation.
        if (double.IsFinite(sample.TimeMs) && sample.TimeMs <= nowMs) _lastSample = Math.Max(_lastSample, sample.TimeMs);
        if (fresh && sample.Loading == true) _loadingObservedMs = sample.TimeMs;

        bool destination = fresh && sample.Loading == false && sample.InputReleased &&
            sample.InputReleasedMs is { } released && double.IsFinite(released) && released >= _dispatchedMs &&
            released < sample.TimeMs &&
            sample.Zone == _contract.DestinationZone && !string.IsNullOrWhiteSpace(sample.Epoch);
        string reason = "fresh-released-arrival-required";
        if (_contract.Kind == NavigationArrivalKind.ZoneTransition)
        {
            destination &= _loadingObservedMs is { } loading && sample.TimeMs > loading && sample.Epoch != _contract.SourceEpoch;
            reason = "loading-and-new-destination-epoch-required";
        }
        else
        {
            destination &= sample.Epoch == _contract.SourceEpoch;
            if (_contract.Kind == NavigationArrivalKind.InteractionPrompt)
            {
                destination &= string.Equals(sample.Prompt?.Trim(), _contract.ExpectedPrompt!.Trim(), StringComparison.OrdinalIgnoreCase);
                reason = "exact-interaction-prompt-required";
            }
            else
            {
                destination &= sample.Position is { } pose && Finite(pose) && Within(pose, _contract.DestinationPoint!.Value);
                reason = "coordinate-arrival-required";
            }
        }

        // Two different epochs cannot jointly confirm arrival. Reset the shared gate
        // before counting the first observation belonging to a new destination epoch.
        if (destination && _confirmingEpoch is not null && _confirmingEpoch != sample.Epoch)
            _gate.Observe(nowMs, null, false, false);
        _confirmingEpoch = destination ? sample.Epoch : null;
        var state = _gate.Observe(nowMs, sample.Loading, destination, fresh);
        if (state == "timed-out") return _terminal = Result(NavigationArrivalState.TimedOut, "arrival-deadline-exhausted");
        if (state == "stopped") return Fail("arrival-gate-stopped");
        if (state == "ready") return _terminal = Result(NavigationArrivalState.Verified, "typed-arrival-verified", sample.TimeMs, sample.Epoch);
        return Result(state == "confirming-destination" ? NavigationArrivalState.Confirming : NavigationArrivalState.Waiting,
            state == "confirming-destination" ? "second-stable-arrival-required" : reason);
    }

    private NavigationArrivalResult Fail(string reason) => _terminal = Result(NavigationArrivalState.Failed, reason);
    private NavigationArrivalResult Result(NavigationArrivalState state, string reason, double? arrival = null, string? epoch = null) =>
        new(state, reason, _loadingObservedMs is not null, _loadingObservedMs, arrival, epoch);
    private static bool Finite(NavigationPoint p) => new[] { p.X, p.Y, p.Z }.All(double.IsFinite);
    private bool Within(NavigationPoint p, NavigationPoint target) =>
        Math.Sqrt(Math.Pow(p.X - target.X, 2) + Math.Pow(p.Y - target.Y, 2)) <= _contract.Radius &&
        Math.Abs(p.Z - target.Z) <= _contract.VerticalTolerance;
}
