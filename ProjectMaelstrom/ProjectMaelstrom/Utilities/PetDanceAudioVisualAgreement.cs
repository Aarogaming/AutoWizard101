namespace ProjectMaelstrom.Utilities;

internal sealed record PetDanceAudioVisualResult(string Status, string[] Visual, string[] Audio,
    bool Agrees, string Reason);

/// <summary>Corroborates an already complete visual sequence; never authorizes input.
/// Continuous audio discovery is independent of the visual Watch/Go boundaries.</summary>
internal static class PetDanceAudioVisualAgreement
{
    public static PetDanceAudioVisualResult Compare(string[] visual, IEnumerable<PetDanceAudioObservation> observations,
        double watchStartMs, double goMs)
    {
        if (visual.Length is < 3 or > 7 || visual.Any(c => c is not ("up" or "down" or "left" or "right")) ||
            !double.IsFinite(watchStartMs) || !double.IsFinite(goMs) || watchStartMs < 0 || goMs <= watchStartMs)
            return new("invalid", visual, [], false, "A complete visual sequence and valid Watch/Go clocks are required.");
        // An anchor can precede a tone because its analysis window looks ahead.
        // Availability remains causal; no future event may support a past decision.
        var events = observations.Where(o => o.AvailableQpcMs >= watchStartMs && o.AvailableQpcMs <= goMs &&
            (o.ProcessedQpcMs == null || double.IsFinite(o.ProcessedQpcMs.Value) && o.ProcessedQpcMs.Value <= goMs)).ToArray();
        if (events.Any(o => o.Status.Contains("gap", StringComparison.OrdinalIgnoreCase) ||
            o.Status.Contains("discontinu", StringComparison.OrdinalIgnoreCase) ||
            o.Status.Contains("timestamp", StringComparison.OrdinalIgnoreCase) ||
            o.Status is "clock-overlap" or "nonmonotonic-packet-time" or "invalid-pcm-packet" or
                "unsupported-packet-flags" or "analysis-duration-limit" or "reset"))
            return new("unknown", visual, [], false, "Audio continuity was lost during Watch.");
        var cues = events.Where(o => o.Emitted && o.CandidateQpcMs >= watchStartMs - 220 &&
            o.CandidateQpcMs <= goMs && o.Direction != null).OrderBy(o => o.CandidateQpcMs).ToArray();
        if (cues.Any(o => !double.IsFinite(o.CandidateQpcMs) || !double.IsFinite(o.AvailableQpcMs) ||
            o.AvailableQpcMs < o.CandidateQpcMs + 220 || !double.IsFinite(o.Similarity) || !double.IsFinite(o.Margin) ||
            o.Similarity < .8 || o.Margin < .08) ||
            cues.Zip(cues.Skip(1)).Any(pair => pair.Second.CandidateQpcMs <= pair.First.CandidateQpcMs))
            return new("invalid", visual, [], false, "Invalid, duplicated or unqualified audio event.");
        var labels = cues.Select(o => o.Direction!.ToLowerInvariant()).ToArray();
        if (labels.Length != visual.Length)
            return new("unknown", visual, labels, false, "Audio and visual cue counts differ; do not fill missing arrows.");
        var agrees = labels.SequenceEqual(visual);
        return new(agrees ? "agrees" : "disagrees", visual, labels, agrees,
            agrees ? "Independent audio events agree with the visual order; supervision and fresh Go are still required." :
            "Audio and visual directions disagree; this observation cannot authorize play.");
    }
}
