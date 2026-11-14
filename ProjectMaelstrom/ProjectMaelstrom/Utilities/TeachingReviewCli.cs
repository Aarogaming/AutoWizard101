using System.Security.Cryptography;
using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

internal static class TeachingReviewCli
{
    public static string? TrackPath(string sessionPath, TeachingSession session) => session.StateTrack == null ? null :
        Path.Combine(Path.GetDirectoryName(Path.GetFullPath(sessionPath))!, session.StateTrack);

    public static TeachingCardProposal[] Review(string sessionPath)
    {
        var session = LongTeachingSession.Read(sessionPath);
        var track = TrackPath(sessionPath, session) ?? throw new InvalidDataException("Attach a state track before reviewing evidence.");
        var samples = TeachingStateTrack.Read(track, session);
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(track)));
        if (Path.GetFileName(track) != "state-" + hash + ".jsonl") throw new InvalidDataException("State evidence changed after attachment.");
        return TeachingStateTrack.Propose(session, samples, hash);
    }

    public static void Attach(string sessionPath, string input, bool w101)
    {
        var session = LongTeachingSession.Read(sessionPath);
        if (session.Status == "recording") throw new InvalidOperationException("Stop recording before attaching offline evidence.");
        var track = w101 ? W101TeachingStateImport.Import(input, sessionPath, session) : TeachingStateTrack.Attach(input, sessionPath, session);
        LongTeachingSession.Save(sessionPath, session with { StateTrack = Path.GetFileName(track) });
    }

    public static void Run(string[] args)
    {
        if (args.Length == 4 && args[0] == "--teaching-export-card")
        {
            var session = LongTeachingSession.Read(args[1]);
            var graph = LongTeachingSession.Compile(session.Activities.Single(a => a.Id == args[2]), session.Game);
            using var output = new FileStream(args[3], FileMode.CreateNew, FileAccess.Write);
            JsonSerializer.Serialize(output, graph, RoutineGraphs.Json);
            return;
        }
        if (args.Length != 3) throw new ArgumentException("--teaching-review <session> <new-report> OR --teaching-attach-state/--teaching-import-w101 <session> <state-jsonl/observer-request>");
        if (args[0] == "--teaching-merge-w101")
        {
            var session = LongTeachingSession.Read(args[1]);
            if (session.Status == "recording" || new FileInfo(args[2]).Length > 262144) throw new InvalidDataException("Stop recording before merging bounded telemetry.");
            using var ledger = JsonDocument.Parse(File.ReadAllText(args[2]));
            var requests = ledger.RootElement.GetProperty("requests").EnumerateArray().Select(v => v.GetString() ?? throw new InvalidDataException("Missing request path.")).ToArray();
            if (requests.Length > 240) throw new InvalidDataException("Telemetry batch budget exceeded.");
            var track = TeachingTelemetryLoop.MergeW101(requests, args[1], session);
            if (track != null) LongTeachingSession.Save(args[1], session with { StateTrack = Path.GetFileName(track) });
            return;
        }
        if (args[0] == "--teaching-annotate")
        {
            var session = LongTeachingSession.Read(args[1]);
            if (session.Status == "recording" || new FileInfo(args[2]).Length > 16384) throw new InvalidDataException("Stop recording and use a bounded annotation.");
            var activity = JsonSerializer.Deserialize<TeachingActivity>(File.ReadAllText(args[2])) ?? throw new InvalidDataException("Missing annotation.");
            _ = LongTeachingSession.Compile(activity, session.Game);
            LongTeachingSession.Save(args[1], session with { Activities = session.Activities.Where(a => a.Id != activity.Id).Append(activity).ToArray() });
            return;
        }
        if (args[0] is "--teaching-attach-state" or "--teaching-import-w101") { Attach(args[1], args[2], args[0] == "--teaching-import-w101"); return; }
        var proposals = Review(args[1]);
        using var file = new FileStream(args[2], FileMode.CreateNew, FileAccess.Write);
        JsonSerializer.Serialize(file, new { version = 1, kind = "teaching-card-proposals", model_required = false,
            live_qualified = false, proposals }, LongTeachingSession.Json);
    }
}
