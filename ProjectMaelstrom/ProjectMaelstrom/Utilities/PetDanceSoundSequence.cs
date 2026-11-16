using System.Security.Cryptography;
using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

internal sealed record PetDanceSoundVisualSample(double QpcMs, DancePhase Phase, DanceCue Cue);
internal sealed record PetDanceSoundCueEvidence(string Direction, double CandidateQpcMs, double WindowStartMs,
    double WindowEndMs, int MatchingFrames, double? FirstMatchMs, double? LastMatchMs, double Similarity, double Margin,
    string VisualStatus, int ConflictingFrames);
internal sealed record PetDanceSoundSequenceResult(string Status, string[] Cues, bool Accepted, string Reason,
    PetDanceSoundCueEvidence[] Evidence, double? ObservedWatchStartMs = null, double? AudioOnsetBoundaryMs = null);

/// <summary>Audio discovers occurrences; independently classified full Watch arrows
/// corroborate each one when available. With complete qualified audio, unclear or
/// conflicting arrows remain explicit uncertainty rather than erasing the round.
/// Observed Watch and fresh Go remain required; no screen-blind or input APIs.</summary>
internal sealed class PetDanceSoundSequence
{
    internal const double MaximumOnsetRecoveryMs = 750;
    private readonly List<PetDanceSoundVisualSample> _visual = [];
    private double _lastTime = -1;
    private double? _watchStart;
    private PetDanceSoundVisualSample? _previousSample;
    private double? _audioOnsetBoundary;
    private bool _goHandled, _acceptedProposal, _responseObserved, _outcomeUnconfirmed;
    private int _observedLength, _minimumAtGo, _maximumAtGo;
    /// <summary>Length from the starting configuration or explicit feedback. A recovered preview may
    /// establish its own length without inventing the previous round's outcome.</summary>
    private bool _awaitBoundary;
    public int Expected { get; private set; }
    public bool NewPreview { get; private set; }
    public double? WatchStartMs => _watchStart;
    public bool NeedsGoAssessment => _watchStart.HasValue && !_goHandled;

    public PetDanceSoundSequence(int expected = 3)
    {
        if (expected is < 3 or > 7) throw new ArgumentOutOfRangeException(nameof(expected));
        Expected = _observedLength = expected;
    }

    /// <summary>One assessment per observed Go. Loading/result boundaries discard old
    /// evidence. Fresh Watch after a response also starts a new window when feedback
    /// was missed; qualified audio may have the last observed length or one more.
    /// Only explicit feedback updates Expected, once, from the accepted preview length.</summary>
    /// <summary>Discard an interrupted preview without inventing feedback or
    /// resetting the timestamp watermark. A known non-Watch phase must be seen
    /// before a later Watch can begin another sequence. Sends no input.</summary>
    public void InterruptPreview()
    {
        _visual.Clear(); _watchStart = null; _audioOnsetBoundary = null; _previousSample = null;
        _goHandled = _acceptedProposal = _responseObserved = _outcomeUnconfirmed = false;
        _minimumAtGo = _maximumAtGo = 0;
        _observedLength = Expected; NewPreview = false; _awaitBoundary = true;
    }

    public PetDanceSoundSequenceResult? Observe(PetDanceSoundVisualSample sample, IReadOnlyList<PetDanceAudioObservation> audio)
    {
        NewPreview = false;
        if (!double.IsFinite(sample.QpcMs) || sample.QpcMs < 0 || sample.QpcMs <= _lastTime ||
            !Enum.IsDefined(sample.Phase) || !Enum.IsDefined(sample.Cue))
            throw new InvalidDataException("Invalid or non-increasing visual observation.");
        var previousSample = _previousSample;
        _previousSample = sample;
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
            if (_acceptedProposal && sample.Phase is DancePhase.Success or DancePhase.Failure)
                Expected = Math.Min(7, _observedLength + (sample.Phase == DancePhase.Success ? 1 : 0));
            _visual.Clear(); _watchStart = null; _audioOnsetBoundary = null;
            _goHandled = _acceptedProposal = _responseObserved = _outcomeUnconfirmed = false;
            _minimumAtGo = _maximumAtGo = 0;
            _observedLength = Expected;
            return null;
        }
        if (sample.Phase == DancePhase.Watch && _responseObserved)
        {
            // This is a new preview, not permission to replay the old response.
            // Discard old visual tokens and select audio against the new Watch clock.
            _visual.Clear(); _watchStart = sample.QpcMs;
            _goHandled = _acceptedProposal = _responseObserved = false;
            _outcomeUnconfirmed = true;
            NewPreview = true;
        }
        if (sample.Phase == DancePhase.Watch && _watchStart == null && !_goHandled)
        { _watchStart = sample.QpcMs; NewPreview = true; }
        if (NewPreview)
        {
            // A capture stall can hide the beginning of Watch while audio keeps
            // recording. Retain only the immediately preceding observed result
            // boundary; never invent a Watch frame or include an older response.
            var gap = previousSample == null ? 0 : sample.QpcMs - previousSample.QpcMs;
            _audioOnsetBoundary = previousSample?.Phase is DancePhase.Success or DancePhase.Failure or DancePhase.Resolving &&
                gap > 220 && gap <= MaximumOnsetRecoveryMs ? previousSample.QpcMs : null;
        }
        if (sample.Phase is DancePhase.Go or DancePhase.Resolving) _responseObserved = true;
        if (_watchStart == null) return null;
        if (_visual.Count >= 6000) throw new InvalidDataException("Visual evidence budget exceeded.");
        _visual.Add(sample);
        if (sample.Phase != DancePhase.Go || _goHandled) return null;
        _goHandled = true;
        _minimumAtGo = _outcomeUnconfirmed ? _observedLength : Expected;
        _maximumAtGo = _outcomeUnconfirmed ? Math.Min(7, _minimumAtGo + 1) : _minimumAtGo;
        var result = AssessRange(_visual, audio, _watchStart.Value, sample.QpcMs, _minimumAtGo, _maximumAtGo, _audioOnsetBoundary);
        _acceptedProposal = result.Accepted;
        if (result.Accepted) _observedLength = result.Cues.Length;
        if (_outcomeUnconfirmed)
            result = result with
            {
                Status = result.Accepted ? "outcome-unconfirmed" : result.Status,
                Reason = "Previous round outcome unconfirmed; this fresh Watch is assessed independently. " + result.Reason
            };
        return result;
    }

    /// <summary>Called after both modalities assess Go, including when this audio
    /// assessment withheld and the coordinator selected qualified visual evidence.</summary>
    public void NotifyResponseSelected(int count)
    {
        if (!_goHandled || _watchStart == null || count < _minimumAtGo || count > _maximumAtGo)
            throw new InvalidOperationException("Select only a qualified response within the assessed round length bounds.");
        _acceptedProposal = true; _observedLength = count;
    }

    public static PetDanceSoundSequenceResult Assess(IReadOnlyList<PetDanceSoundVisualSample> visual,
        IReadOnlyList<PetDanceAudioObservation> audio, double watchStartMs, double goMs, int expected)
        => AssessRange(visual, audio, watchStartMs, goMs, expected, expected);

    private static PetDanceSoundSequenceResult AssessRange(IReadOnlyList<PetDanceSoundVisualSample> visual,
        IReadOnlyList<PetDanceAudioObservation> audio, double watchStartMs, double goMs, int minimumExpected, int maximumExpected,
        double? audioOnsetBoundary = null)
    {
        ArgumentNullException.ThrowIfNull(visual); ArgumentNullException.ThrowIfNull(audio);
        PetDanceSoundSequenceResult Reject(string why, List<PetDanceSoundCueEvidence>? evidence = null)
            => new("withheld", [], false, why, evidence?.ToArray() ?? [], watchStartMs, audioOnsetBoundary);
        if (minimumExpected is < 3 or > 7 || maximumExpected < minimumExpected || maximumExpected > Math.Min(7, minimumExpected + 1) ||
            visual.Count is < 1 or > 6000 || audio.Count > 5000 ||
            !double.IsFinite(watchStartMs) || !double.IsFinite(goMs) || watchStartMs < 0 || goMs <= watchStartMs)
            return Reject("Invalid sequence bounds or expected length.");
        if (audioOnsetBoundary is { } boundary && (!double.IsFinite(boundary) || boundary < 0 ||
            watchStartMs - boundary <= 220 || watchStartMs - boundary > MaximumOnsetRecoveryMs))
            return Reject("Invalid observed pre-Watch boundary.");
        double previous = -1;
        foreach (var sample in visual)
        {
            if (!double.IsFinite(sample.QpcMs) || sample.QpcMs < 0 || sample.QpcMs <= previous ||
                !Enum.IsDefined(sample.Phase) || !Enum.IsDefined(sample.Cue))
                return Reject("Visual samples are invalid or duplicated.");
            previous = sample.QpcMs;
        }
        if (!visual.Any(sample => sample.Phase == DancePhase.Go && sample.QpcMs == goMs))
            return Reject("A fresh observed Go frame is required.");
        if (!visual.Any(sample => sample.Phase == DancePhase.Watch && sample.QpcMs == watchStartMs))
            return Reject("An observed Watch starting frame is required; sound alone cannot identify game state.");
        var relevant = new List<PetDanceAudioObservation>();
        var continuityStart = audioOnsetBoundary ?? watchStartMs;
        foreach (var observation in audio)
        {
            if (!double.IsFinite(observation.CandidateQpcMs) || !double.IsFinite(observation.AvailableQpcMs))
                return Reject("Audio evidence contains an invalid source clock.");
            if (observation.AvailableQpcMs < continuityStart || observation.CandidateQpcMs > goMs) continue;
            if (observation.AvailableQpcMs <= goMs && IsReset(observation.Status))
                return Reject("Audio continuity was lost during Watch.");
            if (observation.Emitted && (audioOnsetBoundary is { } onset
                ? observation.CandidateQpcMs > onset : observation.CandidateQpcMs >= watchStartMs - 220))
                relevant.Add(observation);
        }
        if (relevant.Count < minimumExpected || relevant.Count > maximumExpected)
            return Reject("Independent audio cue count does not match the expected round length.");
        relevant.Sort((a, b) => a.CandidateQpcMs.CompareTo(b.CandidateQpcMs));
        for (var index = 0; index < relevant.Count; index++)
        {
            var cue = relevant[index];
            if (cue.Direction is not ("Up" or "Down" or "Left" or "Right") ||
                !double.IsFinite(cue.Similarity) || !double.IsFinite(cue.Margin) || cue.Similarity < .8 || cue.Margin < .08 ||
                cue.AvailableQpcMs < cue.CandidateQpcMs + 220 || cue.AvailableQpcMs > goMs ||
                cue.ProcessedQpcMs is not { } processed || !double.IsFinite(processed) || processed < cue.AvailableQpcMs || processed > goMs ||
                index > 0 && cue.CandidateQpcMs - relevant[index - 1].CandidateQpcMs < 250)
                return Reject("Audio confidence, occurrence separation or causal availability is insufficient.");
        }
        var evidence = new List<PetDanceSoundCueEvidence>();
        var usedVisual = new HashSet<int>();
        for (var index = 0; index < relevant.Count; index++)
        {
            var cue = relevant[index];
            var start = Math.Max(continuityStart, cue.CandidateQpcMs - 100);
            var end = cue.CandidateQpcMs + 250;
            if (index > 0) start = Math.Max(start, (relevant[index - 1].CandidateQpcMs + cue.CandidateQpcMs) / 2);
            var endExclusive = false;
            if (index + 1 < relevant.Count)
            {
                var midpoint = (cue.CandidateQpcMs + relevant[index + 1].CandidateQpcMs) / 2;
                if (midpoint <= end) { end = midpoint; endExclusive = true; }
            }
            var completeWindow = end <= goMs;
            end = Math.Min(end, goMs);
            var matches = new List<int>();
            var conflicts = 0;
            for (var frame = 0; frame < visual.Count; frame++)
            {
                var sample = visual[frame];
                if (sample.QpcMs < start || sample.QpcMs > end || endExclusive && sample.QpcMs == end) continue;
                if (sample.Cue is not (DanceCue.Up or DanceCue.Down or DanceCue.Left or DanceCue.Right)) continue;
                if (sample.Cue.ToString() != cue.Direction)
                    conflicts++;
                else if (sample.Phase == DancePhase.Watch && !usedVisual.Contains(frame)) matches.Add(frame);
            }
            var first = matches.Count > 0 ? visual[matches[0]].QpcMs : (double?)null;
            var last = matches.Count > 0 ? visual[matches[^1]].QpcMs : (double?)null;
            var visualStatus = conflicts > 0 ? "visual-conflict-audio-preferred" :
                completeWindow && matches.Count >= 2 && last - first >= 50 ? "corroborated" :
                matches.Count > 0 ? "partial-visual-support-audio-preferred" : "visual-unclear-audio-preferred";
            evidence.Add(new(cue.Direction!, cue.CandidateQpcMs, start, end, matches.Count, first, last, cue.Similarity, cue.Margin, visualStatus, conflicts));
            foreach (var frame in matches) usedVisual.Add(frame);
        }
        var corroborated = evidence.All(item => item.VisualStatus == "corroborated");
        var result = new PetDanceSoundSequenceResult(corroborated ? "corroborated" : "audio-led-visual-uncertain", relevant.Select(cue => cue.Direction!.ToLowerInvariant()).ToArray(), true,
            corroborated ? "Every independent sound occurrence has stable, nonconflicting full Watch-arrow evidence." :
                "Complete qualified sound sequence selected within observed Watch/Go; individual arrow visuals remain uncertain or conflicting.", evidence.ToArray(), watchStartMs, audioOnsetBoundary);
        if (audioOnsetBoundary.HasValue && relevant.Any(cue => cue.CandidateQpcMs < watchStartMs - 220))
            result = result with { Reason = "Continuous audio recovered a cue after the preceding observed result boundary and before the first captured Watch frame. " + result.Reason };
        return result;
    }

    private static bool IsReset(string status) => status.Contains("gap", StringComparison.OrdinalIgnoreCase) ||
        status.Contains("discontinu", StringComparison.OrdinalIgnoreCase) || status.Contains("timestamp", StringComparison.OrdinalIgnoreCase) ||
        status is "clock-overlap" or "nonmonotonic-packet-time" or "invalid-pcm-packet" or "unsupported-packet-flags" or "analysis-duration-limit" or "reset";

    /// <summary>Counterfactual replay only. Never dispatches or turns recorded failures into wins.</summary>
    public static object Review(string liveReportPath, string newOutputPath)
    {
        if (!Path.IsPathFullyQualified(liveReportPath) || !Path.IsPathFullyQualified(newOutputPath))
            throw new ArgumentException("Review paths must be absolute.");
        if (File.Exists(newOutputPath) || Directory.Exists(newOutputPath)) throw new IOException("Choose a new sequence review output.");
        using var stream = new FileStream(liveReportPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is <= 0 or > 24 * 1024 * 1024) throw new InvalidDataException("Live report exceeds the bounded review size.");
        var bytes = new byte[(int)stream.Length]; stream.ReadExactly(bytes);
        using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 40 });
        var root = document.RootElement;
        if (root.GetProperty("schema_version").GetInt32() != 1 || !root.GetProperty("audio_required").GetBoolean() ||
            !root.GetProperty("audio_snapshot_complete").GetBoolean()) throw new InvalidDataException("Complete recorded sound-trial evidence is required.");
        var visualRows = root.GetProperty("events");
        var audioRows = root.GetProperty("audio_events");
        if (visualRows.GetArrayLength() is < 1 or > 6000 || audioRows.GetArrayLength() > 5000)
            throw new InvalidDataException("Recorded event budget exceeded.");
        var audio = JsonSerializer.Deserialize<PetDanceAudioObservation[]>(audioRows.GetRawText()) ?? throw new InvalidDataException("Missing audio events.");
        var tracker = new PetDanceSoundSequence(); var assessments = new List<object>(); var proposedKeys = 0;
        foreach (var row in visualRows.EnumerateArray())
        {
            var sample = new PetDanceSoundVisualSample(row.GetProperty("qpc_ms").GetDouble(),
                Enum.Parse<DancePhase>(row.GetProperty("phase").GetString()!, false),
                Enum.Parse<DanceCue>(row.GetProperty("cue").GetString()!, false));
            var expected = tracker.Expected;
            var result = tracker.Observe(sample, audio);
            if (result == null) continue;
            if (result.Accepted) proposedKeys += result.Cues.Length;
            assessments.Add(new { go_qpc_ms = sample.QpcMs, expected, result, dispatched = false });
        }
        var report = new
        {
            schema_version = 1,
            kind = "counterfactual-pet-dance-sound-sequence-review",
            source_sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            assessments,
            proposed_keys = proposedKeys,
            inputs_sent = 0,
            counterfactual = true,
            live_gameplay_qualified = false,
            note = "Re-evaluates recorded evidence with frozen gates. Proposed keys were not sent; original failures remain failures."
        };
        var directory = Path.GetDirectoryName(newOutputPath)!; Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, ".sound-sequence-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                JsonSerializer.Serialize(output, report, new JsonSerializerOptions { WriteIndented = true });
            File.Move(temporary, newOutputPath, false);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        return report;
    }
}
