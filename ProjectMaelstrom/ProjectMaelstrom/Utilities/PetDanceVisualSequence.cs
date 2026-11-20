namespace ProjectMaelstrom.Utilities;

internal sealed record PetDanceVisualSample(double QpcMs, DancePhase Phase, DanceCue Cue,
    DanceCue FadeOf = DanceCue.Unknown, double Opacity = double.NaN);
/// <summary>SupportingFrames counts full arrows only; fade support and the last
/// full-arrow timestamp remain separate when a fading shape confirms persistence.</summary>
internal sealed record PetDanceVisualCueEvidence(string Direction, double OnsetQpcMs,
    double ConfirmedQpcMs, int SupportingFrames, string Transition,
    int FadeSupportingFrames = 0, double? LastFullQpcMs = null);
internal sealed record PetDanceVisualSequenceResult(string Status, string[] Cues, bool Accepted,
    string Reason, PetDanceVisualCueEvidence[] Evidence)
{
    public string Source => "visual";
}

/// <summary>Counts qualified arrow appearances, not frames. The caller supplies
/// direction/phase observations from its qualified layout and shape reader.
/// Brief fades/Unknowns are transitions; gaps or unresolved repeats retain explicit
/// uncertainty for a separate fallback selector. This class sends no inputs.</summary>
internal sealed class PetDanceVisualSequence
{
    private const double StableMs = 50, TroughMs = 16, MinimumSeparationMs = 250, MaximumGapMs = 150;
    private readonly List<PetDanceVisualCueEvidence> _appearances = [];
    private readonly List<string> _uncertainties = [];
    private double _lastTime = -1, _candidateSince, _troughSince = -1, _unknownSince = -1;
    private double _lastLow = -1, _candidateLastFull, _candidateLastSupport, _candidateLastOpacity = double.NaN;
    private int _candidateSamples, _troughSamples, _observedLength, _minimumAtGo, _maximumAtGo;
    private int _candidateFadeSamples;
    private DanceCue _candidate = DanceCue.Unknown, _held = DanceCue.Unknown;
    private bool _candidateHandled, _rearmed, _transitionSeen, _responseObserved, _goHandled, _outcomeUnconfirmed;
    private int? _selectedLength;
    private bool _awaitBoundary;
    public int Expected { get; private set; }
    public bool NewPreview { get; private set; }
    public double? WatchStartMs { get; private set; }
    public bool NeedsGoAssessment => WatchStartMs.HasValue && !_goHandled;
    public string[] CurrentCues => _appearances.Select(item => item.Direction).ToArray();
    public IReadOnlyList<PetDanceVisualCueEvidence> Appearances => _appearances.AsReadOnly();

    public PetDanceVisualSequence(int expected = 3)
    {
        if (expected is < 3 or > 7) throw new ArgumentOutOfRangeException(nameof(expected));
        Expected = _observedLength = expected;
    }

    /// <summary>Discard an interrupted preview without inventing feedback or
    /// resetting the timestamp watermark. A known non-Watch phase must be seen
    /// before a later Watch can begin another sequence. Sends no input.</summary>
    public void InterruptPreview()
    {
        ClearPreview(); WatchStartMs = null; _selectedLength = null;
        _responseObserved = _goHandled = _outcomeUnconfirmed = false;
        _observedLength = Expected; NewPreview = false; _awaitBoundary = true;
    }

    public PetDanceVisualSequenceResult? Observe(PetDanceVisualSample sample)
    {
        NewPreview = false;
        if (!double.IsFinite(sample.QpcMs) || sample.QpcMs < 0 || sample.QpcMs <= _lastTime ||
            !Enum.IsDefined(sample.Phase) || !Enum.IsDefined(sample.Cue) || !Enum.IsDefined(sample.FadeOf))
            throw new InvalidDataException("Invalid or non-increasing visual observation.");
        var gap = _lastTime >= 0 && sample.QpcMs - _lastTime > MaximumGapMs;
        _lastTime = sample.QpcMs;
        if (_awaitBoundary)
        {
            if (sample.Phase is DancePhase.Ready or DancePhase.Loading or DancePhase.Go or
                DancePhase.Resolving or DancePhase.Success or DancePhase.Failure)
                _awaitBoundary = false;
            return null;
        }
        if (sample.Phase is DancePhase.Success or DancePhase.Failure or DancePhase.Loading or DancePhase.Ready)
        {
            if (_selectedLength is { } selected && sample.Phase is DancePhase.Success or DancePhase.Failure)
                Expected = Math.Min(7, selected + (sample.Phase == DancePhase.Success ? 1 : 0));
            ClearPreview(); WatchStartMs = null;
            _responseObserved = _goHandled = _outcomeUnconfirmed = false;
            _selectedLength = null; _observedLength = Expected;
            return null;
        }
        if (sample.Phase == DancePhase.Watch && (WatchStartMs == null || _responseObserved))
        {
            _outcomeUnconfirmed = _responseObserved;
            ClearPreview(); WatchStartMs = sample.QpcMs; NewPreview = true;
            _responseObserved = _goHandled = false; _selectedLength = null;
            gap = false; // A gap between rounds does not erase the new preview.
        }
        if (sample.Phase is DancePhase.Go or DancePhase.Resolving) _responseObserved = true;
        if (WatchStartMs == null || _goHandled) return null;
        if (gap) Uncertain("Observation gap exceeds 150 ms; an appearance may be missing.");
        if (sample.Phase == DancePhase.Unknown) { Unknown(sample); return null; }
        if (sample.Phase == DancePhase.Watch) { ObserveCue(sample); return null; }
        if (sample.Phase != DancePhase.Go) return null;
        FinishUnknown(sample.QpcMs);
        _goHandled = true;
        _minimumAtGo = _outcomeUnconfirmed ? _observedLength : Expected;
        _maximumAtGo = _outcomeUnconfirmed ? Math.Min(7, _minimumAtGo + 1) : _minimumAtGo;
        var complete = _appearances.Count >= _minimumAtGo && _appearances.Count <= _maximumAtGo;
        var accepted = complete && _uncertainties.Count == 0;
        if (accepted) NotifyResponseSelected(_appearances.Count);
        var reason = accepted ? "Complete sequence of independently confirmed visual appearances." :
            string.Join(" ", _uncertainties.Concat(complete ? [] : new[] { "Visual appearance count does not match this round; a qualified fallback is needed." }));
        if (_outcomeUnconfirmed) reason = "Previous round outcome unconfirmed; this fresh preview is assessed independently. " + reason;
        return new(accepted ? (_outcomeUnconfirmed ? "outcome-unconfirmed" : "visual-complete") : "visual-uncertain",
            CurrentCues, accepted, reason, _appearances.ToArray());
    }

    /// <summary>Called after both modalities assess Go and the coordinator selects a
    /// qualified response. Updates feedback accounting without claiming a game outcome.</summary>
    public void NotifyResponseSelected(int count)
    {
        if (!_goHandled || WatchStartMs == null || count < _minimumAtGo || count > _maximumAtGo)
            throw new InvalidOperationException("Select only a qualified response within the assessed round length bounds.");
        _selectedLength = _observedLength = count;
    }

    private void ObserveCue(PetDanceVisualSample sample)
    {
        if (sample.Cue == DanceCue.Unknown) { Unknown(sample); return; }
        FinishUnknown(sample.QpcMs);
        if (sample.Cue is DanceCue.Neutral or DanceCue.Fading)
        {
            // A qualified falling shape can extend an already seen pair of full
            // arrows. It never supplies either of the two required full samples.
            var sameFade = sample.Cue == DanceCue.Fading && !_candidateHandled && _candidateSamples >= 2 &&
                sample.FadeOf == _candidate && double.IsFinite(sample.Opacity) && sample.Opacity is >= 0 and < .7 &&
                double.IsFinite(_candidateLastOpacity) && sample.Opacity <= _candidateLastOpacity &&
                sample.QpcMs - _candidateLastSupport <= MaximumGapMs;
            if (sameFade)
            {
                _candidateFadeSamples++; _candidateLastSupport = sample.QpcMs; _candidateLastOpacity = sample.Opacity;
                ConfirmCandidate(sample.QpcMs);
            }
            else ResetCandidate();
            _transitionSeen |= _held != DanceCue.Unknown;
            var low = sample.Cue == DanceCue.Neutral ||
                sample.FadeOf is DanceCue.Up or DanceCue.Down or DanceCue.Left or DanceCue.Right &&
                double.IsFinite(sample.Opacity) && sample.Opacity is >= 0 and <= .25;
            if (low)
            {
                if (_troughSince < 0 || sample.QpcMs - _lastLow > MaximumGapMs)
                { _troughSince = sample.QpcMs; _troughSamples = 0; }
                _lastLow = sample.QpcMs;
                _troughSamples++;
                if (_troughSamples >= 2 && sample.QpcMs - _troughSince >= TroughMs) _rearmed = true;
            }
            else ResetTrough();
            return; // Rising fades do not revoke a previously confirmed trough.
        }
        ResetTrough();
        if (_candidate != sample.Cue)
        {
            _candidate = sample.Cue; _candidateSince = sample.QpcMs;
            _candidateSamples = _candidateFadeSamples = 0; _candidateHandled = false;
        }
        _candidateSamples++;
        _candidateLastFull = _candidateLastSupport = sample.QpcMs; _candidateLastOpacity = sample.Opacity;
        ConfirmCandidate(sample.QpcMs);
    }

    private void ConfirmCandidate(double time)
    {
        if (_candidateHandled || _candidateSamples < 2 || time - _candidateSince < StableMs) return;
        _candidateHandled = true;
        if (_held == _candidate && !_rearmed)
        {
            if (_transitionSeen && _appearances.Count > 0 && _candidateSince - _appearances[^1].OnsetQpcMs >= MinimumSeparationMs)
                Uncertain("Same direction returned without a confirmed low/neutral trough; repeat count is uncertain.");
            return;
        }
        if (_appearances.Count > 0 && _candidateSince - _appearances[^1].OnsetQpcMs < MinimumSeparationMs)
        { Uncertain("Distinct appearances are less than 250 ms apart."); return; }
        if (_appearances.Count >= 8) { Uncertain("Visual appearance budget exceeded."); return; }
        var transition = _appearances.Count == 0 ? "first-appearance" : _held == _candidate ? "confirmed-trough-repeat" : "direction-change";
        _appearances.Add(new(_candidate.ToString().ToLowerInvariant(), _candidateSince, time, _candidateSamples, transition,
            _candidateFadeSamples, _candidateLastFull));
        _held = _candidate; _rearmed = _transitionSeen = false;
    }

    private void Unknown(PetDanceVisualSample sample)
    {
        var time = sample.QpcMs;
        if (_unknownSince < 0) _unknownSince = time;
        if (time - _unknownSince > MaximumGapMs) Uncertain("Unclear observations exceed 150 ms; occurrence count is uncertain.");
        _transitionSeen |= _held != DanceCue.Unknown;
        ResetCandidate();
        // Unknown is not a positive trough sample. Retain a recent positive only
        // until a second qualified low/neutral sample corroborates it; never bridge
        // a full/high icon, a long unknown interval or a source gap over 150 ms.
        if (_lastLow < 0 || time - _lastLow > MaximumGapMs ||
            sample.Cue is DanceCue.Up or DanceCue.Down or DanceCue.Left or DanceCue.Right ||
            double.IsFinite(sample.Opacity) && sample.Opacity > .25)
            ResetTrough();
    }

    private void FinishUnknown(double time)
    {
        if (_unknownSince >= 0 && time - _unknownSince > MaximumGapMs)
            Uncertain("Unclear observations exceed 150 ms; occurrence count is uncertain.");
        _unknownSince = -1;
    }

    private void Uncertain(string reason)
    {
        if (_uncertainties.Count < 8 && !_uncertainties.Contains(reason)) _uncertainties.Add(reason);
    }

    private void ClearPreview()
    {
        _appearances.Clear(); _uncertainties.Clear();
        _candidate = _held = DanceCue.Unknown;
        _candidateSamples = _candidateFadeSamples = _troughSamples = _minimumAtGo = _maximumAtGo = 0;
        _candidateHandled = _rearmed = _transitionSeen = false;
        _troughSince = _lastLow = _unknownSince = -1;
        _candidateLastOpacity = double.NaN;
    }

    private void ResetCandidate()
    { _candidate = DanceCue.Unknown; _candidateSamples = _candidateFadeSamples = 0; _candidateHandled = false; _candidateLastOpacity = double.NaN; }

    private void ResetTrough()
    { _troughSince = _lastLow = -1; _troughSamples = 0; }
}
