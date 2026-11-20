using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Production candidate telemetry boundary. Host supplies stream/run/target;
/// the first zone is a candidate label, never independently verified identity.
/// This observer exposes no input and cannot qualify navigation.</summary>
internal sealed class W101PoseStreamObservation(string runId, string streamId, WizwalkerTarget target)
{
    private W101PoseStreamConsumer? consumer;
    private string? wizardKey;
    internal int Accepted { get; private set; }
    internal string? CandidateZone { get; private set; }
    internal bool Ended { get; private set; }
    internal bool SawLoading { get; private set; }
    internal string? Reason { get; private set; }
    internal void End(string reason)
    {
        if (Ended) return;
        Ended = true; Reason = reason; consumer?.Invalidate(reason);
    }
    internal void Receive(string line, long utcTicks, double hostMs, bool owned)
    {
        if (Ended) return;
        if (!owned) { End("owner-lost"); return; }
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            var kind = root.GetProperty("kind").GetString();
            if (kind == "loading")
            {
                if (root.GetProperty("loading").ValueKind != JsonValueKind.False)
                {
                    SawLoading = root.GetProperty("loading").ValueKind == JsonValueKind.True;
                    End("loading-context-invalidated");
                }
                return;
            }
            if (kind is "moving-pose-invalidated" or "moving-pose-ended") { End(kind); return; }
            if (kind != "moving-pose-candidate") return;
            string? subject = root.GetProperty("wizardKey").GetString();
            if (root.GetProperty("wizardOwnerChecked").ValueKind != JsonValueKind.True ||
                subject is null || subject.Length != 64 || !subject.All(Uri.IsHexDigit) ||
                wizardKey is not null && subject != wizardKey)
            { End("wizard-identity-changed-or-unverified"); return; }
            string zone = root.GetProperty("zone").GetString() ?? "";
            consumer ??= new(runId, streamId, target, zone, streamId, 250, 10);
            if (!consumer.Accept(line, utcTicks, hostMs, owned)) { End(consumer.Reason ?? "sample-rejected"); return; }
            wizardKey = subject;
            CandidateZone = zone; Accepted++;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException)
        { End("malformed-trace"); }
    }
}
