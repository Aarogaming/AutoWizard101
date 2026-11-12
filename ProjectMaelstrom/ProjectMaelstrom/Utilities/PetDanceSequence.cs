namespace ProjectMaelstrom.Utilities;

internal enum DancePhase { Unknown, Ready, Watch, Go, Success, Failure, Resolving, Loading }
internal enum DanceCue { Unknown, Neutral, Up, Down, Left, Right, Fading }
internal sealed record DanceDecision(string[] Cues, string[] Proposals, string Reason, bool Rejected);

/// <summary>Observation-only teacher. Time and visible phase boundaries replace the legacy sleep/replay loop.</summary>
internal sealed class PetDanceSequence
{
    internal const double StableMs = 70, MaximumGapMs = 150;
    private readonly List<string> _cues = [];
    private int _expected;
    private double _lastTime = -1, _since = -1;
    private DanceCue _candidate = DanceCue.Unknown;
    private DancePhase _phase;
    private bool _active, _armed, _emitted;
    private bool _boundarySeen, _outcomeApplied;
    private double _fadeSince = -1, _lowSince = -1, _lastOpacity = 1;
    private string? _fault;
    private Size? _size;
    public PetDanceSequence(int expected)
    {
        if (expected is < 3 or > 7) throw new ArgumentOutOfRangeException(nameof(expected));
        _expected = expected;
    }

    public static DancePhase ParsePhase(string? label) => label switch
    {
        "dance-ready" => DancePhase.Ready,
        "dance-watch" => DancePhase.Watch,
        "dance-go" => DancePhase.Go,
        "dance-success" => DancePhase.Success,
        "dance-failure" => DancePhase.Failure,
        "dance-resolving" => DancePhase.Resolving,
        "game-loading" => DancePhase.Loading,
        _ => DancePhase.Unknown
    };

    public DanceDecision Observe(double timeMs, Size size, DancePhase phase, DanceCue cue,
        DanceCue fadeOf = DanceCue.Unknown, double opacity = double.NaN)
    {
        var timeValid = double.IsFinite(timeMs) && timeMs >= 0 && (_lastTime < 0 || timeMs > _lastTime);
        if (!timeValid) return Reject("Missing or non-increasing timestamp; round discarded.");
        var gap = _lastTime >= 0 && timeMs - _lastTime > MaximumGapMs;
        _lastTime = timeMs;
        if (size.Width < 1 || size.Height < 1) return Reject("Invalid frame dimensions.");
        if (_size.HasValue && _size.Value != size) return Reject("Window geometry changed; recalibrate and restart the teacher.");
        _size = size;
        if (!Enum.IsDefined(phase) || !Enum.IsDefined(cue)) return Reject("Invalid observation.");
        if (phase == DancePhase.Loading)
        {
            // A scene boundary invalidates queued cues, not the game result/expected length.
            // Resume requires a fresh, complete Watch sequence rather than replaying old input.
            _active = _armed = _emitted = _boundarySeen = _outcomeApplied = false;
            _candidate = DanceCue.Unknown; _since = _fadeSince = _lowSince = -1;
            _lastOpacity = 1; _fault = null; _cues.Clear(); _phase = phase;
            return Decision("Loading: gameplay paused; waiting for a fresh Watch sequence.");
        }
        if (phase is DancePhase.Ready or DancePhase.Success or DancePhase.Failure)
        {
            if (!_outcomeApplied)
            {
                if (phase == DancePhase.Success && _emitted) _expected = Math.Min(7, _expected + 1);
                _active = false; _emitted = false; _fault = null; _cues.Clear();
                _candidate = DanceCue.Unknown; _since = -1; _armed = false;
                _boundarySeen = true; _outcomeApplied = true;
                _fadeSince = _lowSince = -1; _lastOpacity = 1;
            }
            _phase = phase;
            return Decision("Waiting for a fresh Watch sequence.");
        }
        if (phase == DancePhase.Resolving)
        {
            _phase = phase;
            return Decision("Inputs finished; waiting for independently recognized success or failure.");
        }
        if (phase == DancePhase.Unknown) { _phase = phase; return _active ? Reject("Unrecognized phase during the round.") : Decision("Waiting for a recognized Watch phase."); }
        if (phase == DancePhase.Watch)
        {
            if (_emitted || _phase is DancePhase.Go or DancePhase.Resolving) return Reject("A result or Ready boundary is required before the next round.");
            if (!_active)
            {
                _active = true; _armed = _boundarySeen; _candidate = DanceCue.Unknown; _since = -1;
                _boundarySeen = false; _outcomeApplied = false;
            }
            else if (gap) _fault ??= "Observation gap exceeds 150 ms; a cue may have been missed.";
            _phase = phase;
            if (_fault != null) return Decision(_fault);
            if (cue == DanceCue.Unknown) return Reject("Arrow area is unclear; this round needs review.");
            if (cue == DanceCue.Fading)
            {
                if (_cues.Count == 0 || fadeOf is not (DanceCue.Up or DanceCue.Down or DanceCue.Left or DanceCue.Right) ||
                    fadeOf.ToString().ToLowerInvariant() != _cues[^1] || !double.IsFinite(opacity) || opacity is < 0 or >= .7)
                    return Reject("Fade is not tied to the last confirmed arrow.");
                if (_fadeSince < 0) { _fadeSince = timeMs; _lastOpacity = opacity; }
                if (timeMs - _fadeSince > 350 || opacity > _lastOpacity + .12)
                    return Reject("Fade lasted too long or brightened without a new clear cue.");
                _lastOpacity = Math.Min(_lastOpacity, opacity); _candidate = DanceCue.Fading; _since = timeMs;
                if (opacity <= .25)
                {
                    if (_lowSince < 0) _lowSince = timeMs;
                    if (timeMs - _lowSince >= 16) _armed = true;
                }
                else { _lowSince = -1; _armed = false; }
                return Decision(_armed ? "Known arrow faded to a confirmed trough." : "Following the confirmed arrow fade; no new cue counted.");
            }
            // The last low-opacity sample and the following explicit neutral sample
            // belong to one trough, even if the game displays neutral for one frame.
            if (cue == DanceCue.Neutral && _fadeSince >= 0)
            {
                if (_lowSince < 0) _lowSince = timeMs;
                if (timeMs - _lowSince >= 16) _armed = true;
            }
            if (cue != DanceCue.Neutral) { _fadeSince = _lowSince = -1; _lastOpacity = 1; }
            if (cue != _candidate) { _candidate = cue; _since = timeMs; }
            if (timeMs - _since < StableMs) return Decision("Waiting for a stable cue or neutral gap.");
            if (cue == DanceCue.Neutral) { _armed = true; return Decision("Neutral gap confirmed."); }
            var direction = cue.ToString().ToLowerInvariant();
            if (!_armed)
            {
                if (_cues.Count == 0) return Reject("Recording began inside a cue; its beginning is missing.");
                if (_cues[^1] != direction) return Reject("Cue changed without a confirmed neutral gap.");
                return Decision("Held cue already counted.");
            }
            _armed = false; _cues.Add(direction);
            return _cues.Count > _expected ? Reject("Too many cues for the selected round.") : Decision($"Remembered {_cues.Count}/{_expected} cues.");
        }
        _phase = phase;
        if (_emitted) return Decision("Go already handled; no duplicate proposal.");
        if (!_active) return Decision("Go without a complete observed Watch sequence.");
        if (gap) _fault ??= "Observation gap before Go; round discarded.";
        if (_fault != null) return Decision(_fault);
        if (_cues.Count != _expected || !_armed || _candidate is not (DanceCue.Neutral or DanceCue.Fading))
            return Reject("Go arrived without a complete, separated cue sequence.");
        _emitted = true;
        return new(_cues.ToArray(), _cues.Select(c => "dance-" + c).ToArray(), "Verified sequence proposal at Go; no input sent.", false);
    }

    private DanceDecision Reject(string reason) { _fault ??= reason; return Decision(_fault); }
    private DanceDecision Decision(string reason) => new(_cues.ToArray(), [], reason, _fault != null);
}
