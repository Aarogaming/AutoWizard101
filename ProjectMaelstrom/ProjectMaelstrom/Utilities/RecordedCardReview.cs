using System.Security.Cryptography;
using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

internal sealed record RecordedCardDecision(double TimeMs, string Status, string Reason, string? Source, string? Context);
internal sealed record RecordedCardReport(string CardHash, string SessionId, string TrackHash,
    RecordedCardDecision[] Decisions, bool LiveQualified = false, int DesktopDispatchCount = 0,
    string Qualification = "Custom · requirements replay only; action outcomes unverified");

/// <summary>Uses the live condition evaluator against recorded, bound snapshots. Never dispatches or promotes cards.</summary>
internal static class RecordedCardReview
{
    internal static string Hash(JobCardSpec card) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(card, JobDeckRules.Json)));

    internal static RecordedCardDecision Evaluate(JobCardSpec card, TeachingSession session,
        IReadOnlyList<TeachingStateSample> samples, double timeMs, IReadOnlyList<JobFactDefinition> definitions)
    {
        if (!double.IsFinite(timeMs) || timeMs < 0 || timeMs > (session.Chunks.LastOrDefault()?.EndMs ?? 0))
            throw new InvalidDataException("Test time is outside the recorded session.");
        foreach (var c in card.Conditions ?? []) JobFactCatalog.ValidateCondition(c, definitions);
        foreach (var f in card.Filters ?? []) JobFactCatalog.ValidateFilter(f, definitions);
        var view = TeachingStateTrack.At(samples, timeMs);
        if (view.Status != "observed" || view.Sample is not { } sample)
            return new(timeMs, "Unknown", "Recorded state is " + view.Status + ".", view.Sample?.Source, view.Sample?.Context);
        TeachingStateTrack.Validate(sample, session);
        var now = DateTime.UnixEpoch.AddMilliseconds(timeMs);
        var facts = new JobFacts(session.Game, $"{sample.Target.ProcessId}/{sample.Target.StartedUtcTicks}",
            DateTime.UnixEpoch.AddMilliseconds(sample.TimeMs), sample.Facts, sample.Source);
        var reason = JobDeckRules.FactBlock(card, session.Game, facts, now, true, definitions);
        return new(timeMs, reason == null ? "Eligible" : reason.StartsWith("Unknown", StringComparison.Ordinal) ? "Unknown" : "Blocked",
            reason ?? "Recorded requirements matched; action outcome is not established.", sample.Source, sample.Context);
    }

    internal static RecordedCardReport Run(JobCardSpec card, string sessionPath, IReadOnlyList<JobFactDefinition> definitions, string gameId = "*")
    {
        var session = LongTeachingSession.Read(sessionPath);
        if (gameId != "*" && session.Game != gameId) throw new InvalidDataException("Recording belongs to a different game.");
        if (session.Status == "recording") throw new InvalidDataException("Stop recording before reviewing it.");
        _ = TeachingReviewCli.Review(sessionPath);
        var track = TeachingReviewCli.TrackPath(sessionPath, session)!;
        var samples = TeachingStateTrack.Read(track, session);
        var end = session.Chunks.LastOrDefault()?.EndMs ?? 0;
        var times = samples.Select(s => s.TimeMs).Concat(samples.Select(s => s.TimeMs + TeachingStateTrack.FreshnessMs + 1).Where(t => t <= end))
            .Concat(session.Activities.Select(a => a.StartMs)).Append(0).Append(end).Distinct().Order().ToArray();
        var decisions = times.Select(t => Evaluate(card, session, samples, t, definitions)).ToArray();
        return new(Hash(card), session.Id, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(track))), decisions);
    }
}
