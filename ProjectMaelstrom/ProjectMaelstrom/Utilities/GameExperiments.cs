using System.Security.Cryptography;
using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

internal sealed record ExperimentPack(string Role, string Id, string Version, string File, string Sha256);
internal sealed record ExperimentTrial(int Number, string Stage, string Role, int Seed);
internal sealed record ExperimentPlan(int SchemaVersion, string Id, string Title, string Question, string GameId,
    string CreatedUtc, string Workspace, double Speed, int Target, int DiskLimitMb,
    List<ExperimentPack> Packs, List<ExperimentTrial> Trials, Dictionary<string, string> Environment, string Kind = "dodge-comparison");
internal sealed record ExperimentResult(int Number, string Status, int? Score, string StopReason,
    string RecordedUtc, Dictionary<string, string> Evidence, int? OwnerPid = null, string? OwnerStartedUtc = null);
internal sealed record ExperimentStatistics(int Expected, int Completed, double? Mean, double? Median, int? Minimum, int? AtTarget, int[] Scores);

/// <summary>Local, versioned experiment plans and evidence. The first execution adapter is private Dodge.</summary>
internal sealed class GameExperiments
{
    internal static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    public string Root { get; }
    public GameExperiments(string dataRoot) => Root = Path.Combine(Path.GetFullPath(dataRoot), "experiments");

    public string CreateFootageStudy(string title, string question, string gameId)
    {
        Text(title, 120); Text(question, 2000); TeachingRecording.ValidateId(gameId);
        var id = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..12];
        var directory = DirectoryPath(id); Directory.CreateDirectory(directory);
        WriteNew(Path.Combine(directory, "plan.json"), new ExperimentPlan(1, id, title.Trim(), question.Trim(), gameId,
            DateTime.UtcNow.ToString("O"), "", 0, 0, 2048, [], [], new(), "footage-study"));
        File.WriteAllText(Path.Combine(directory, "plan.sha256"), Digest(Path.Combine(directory, "plan.json")));
        return id;
    }

    public string CreateDodge(string title, string question, string workspace, string baseline, string candidate,
        Dictionary<string, string> environment)
    {
        Text(title, 120); Text(question, 2000);
        var inputs = new[] { DodgeLivePack.Load(baseline), DodgeLivePack.Load(candidate) };
        if (inputs.Any(pack => pack.Context == null)) throw new InvalidDataException("Choose two context-aware Dodge packs.");
        if (inputs[0].Sha256 == inputs[1].Sha256) throw new InvalidDataException("Choose a candidate that differs from the baseline.");
        // Different observation clocks cannot be ranked as a policy comparison.
        if (inputs.Any(pack => pack.Context!.AvoidancePolicy is "center-continuous-v16" or "center-continuous-track-v17"))
            throw new InvalidDataException("This comparison adapter requires advance-before-decision timing.");
        using var studyLock = DodgeExperimentRunner.AcquireStudy(workspace);
        var id = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..12];
        var directory = DirectoryPath(id);
        Directory.CreateDirectory(directory);
        var packs = new List<ExperimentPack>();
        foreach (var (source, role, pack) in new[] { (baseline, "baseline", inputs[0]), (candidate, "candidate", inputs[1]) })
        {
            var file = role + ".json";
            File.Copy(source, Path.Combine(directory, file));
            if (Digest(Path.Combine(directory, file)) != pack.Sha256.ToLowerInvariant()) throw new IOException("Pack changed while freezing the experiment.");
            packs.Add(new(role, pack.Id, pack.Version, file, pack.Sha256.ToLowerInvariant()));
        }
        var registryPath = Path.Combine(workspace, ".local", "dodge-holdouts", "registry.json");
        var seeds = new HashSet<int>();
        if (File.Exists(registryPath))
        {
            using var registry = JsonDocument.Parse(BoundedBytes(registryPath, 2_000_000));
            if (registry.RootElement.GetProperty("schema_version").GetInt32() != 1) throw new InvalidDataException("Invalid seed registry.");
            foreach (var seed in registry.RootElement.GetProperty("used_seeds").EnumerateArray())
            {
                var value = seed.GetInt32();
                if (value is < 1 or >= int.MaxValue) throw new InvalidDataException("Invalid reserved seed.");
                seeds.Add(value);
            }
        }
        var fixtures = Path.Combine(workspace, "tools", "fixtures");
        if (Directory.Exists(fixtures))
            foreach (var path in Directory.GetFiles(fixtures, "dodge-benchmark-*.json"))
            {
                using var manifest = JsonDocument.Parse(BoundedBytes(path, 1_000_000));
                foreach (var seed in manifest.RootElement.GetProperty("seeds").EnumerateArray()) seeds.Add(seed.GetInt32());
            }
        if (seeds.Count > 99_976) throw new InvalidDataException("Seed registry is full; archive and review before starting another study.");
        int NextSeed() { int value; do { value = RandomNumberGenerator.GetInt32(1, int.MaxValue); } while (!seeds.Add(value)); return value; }
        var trials = new List<ExperimentTrial>();
        for (var i = 0; i < 4; i++)
        {
            var seed = NextSeed();
            // Alternate the order to reduce systematic machine-load/order effects.
            foreach (var role in i % 2 == 0 ? new[] { "baseline", "candidate" } : new[] { "candidate", "baseline" })
                trials.Add(new(trials.Count, "development", role, seed));
        }
        foreach (var stage in new[] { "validation-1", "validation-2" })
            for (var i = 0; i < 10; i++) trials.Add(new(trials.Count, stage, "candidate", NextSeed()));
        Directory.CreateDirectory(Path.GetDirectoryName(registryPath)!);
        // Reserve before publishing the plan. A failed creation may waste seeds, but never releases them for reuse.
        WriteAtomic(registryPath, new { schema_version = 1, used_seeds = seeds.Order().ToArray() });
        var plan = new ExperimentPlan(1, id, title.Trim(), question.Trim(), "godot-dodge-the-creeps", DateTime.UtcNow.ToString("O"),
            Path.GetFullPath(workspace), .25, 50, 2048, packs, trials, new(environment));
        WriteNew(Path.Combine(directory, "plan.json"), plan);
        File.WriteAllText(Path.Combine(directory, "plan.sha256"), Digest(Path.Combine(directory, "plan.json")));
        return id;
    }

    public string DirectoryPath(string id)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(id, @"\A\d{8}-\d{6}-[a-f0-9]{12}\z")) throw new InvalidDataException("Invalid experiment identifier.");
        return Path.Combine(Root, id);
    }

    public IEnumerable<string> ListIds() => Directory.Exists(Root)
        ? Directory.GetDirectories(Root).Select(Path.GetFileName).OfType<string>().Where(id => File.Exists(Path.Combine(Root, id, "plan.json"))).OrderDescending()
        : [];

    public ExperimentPlan Load(string id)
    {
        var directory = DirectoryPath(id);
        var path = Path.Combine(directory, "plan.json");
        if (Digest(path) != File.ReadAllText(Path.Combine(directory, "plan.sha256"))) throw new InvalidDataException("Experiment plan changed. Create a new experiment.");
        var plan = Read<ExperimentPlan>(path);
        if (plan.Kind == "footage-study")
        {
            TeachingRecording.ValidateId(plan.GameId);
            if (plan.SchemaVersion != 1 || plan.Id != id || plan.Packs == null || plan.Packs.Count != 0 || plan.Trials == null || plan.Trials.Count != 0 ||
                plan.Environment == null || plan.Environment.Count != 0 || plan.Speed != 0 || plan.Target != 0) throw new InvalidDataException("Invalid footage study.");
            return plan;
        }
        if (plan.Kind != "dodge-comparison" || plan.SchemaVersion != 1 || plan.Id != id || plan.GameId != "godot-dodge-the-creeps" || plan.Speed != .25 || plan.Target != 50 || plan.DiskLimitMb != 2048 ||
            plan.Packs == null || plan.Packs.Count != 2 || plan.Trials == null || plan.Trials.Count != 28 || plan.Environment == null)
            throw new InvalidDataException("Unsupported experiment plan.");
        foreach (var role in new[] { "baseline", "candidate" })
        {
            var pack = plan.Packs.Single(p => p.Role == role);
            if (pack.File != role + ".json" || Digest(Path.Combine(directory, pack.File)) != pack.Sha256)
                throw new InvalidDataException("Frozen pack changed. Create a new experiment.");
        }
        return plan;
    }

    public List<ExperimentResult> Results(ExperimentPlan plan)
    {
        var rows = new List<ExperimentResult>();
        foreach (var trial in plan.Trials)
        {
            var path = ResultPath(plan, trial);
            if (!File.Exists(path)) continue;
            var row = Read<ExperimentResult>(path);
            if (row.Number != trial.Number || row.Evidence == null) throw new InvalidDataException("Invalid experiment result.");
            foreach (var (file, hash) in row.Evidence)
                if (Digest(Child(Path.GetDirectoryName(path)!, file)) != hash) throw new InvalidDataException("Experiment evidence changed.");
            if (row.Status == "complete")
            {
                var verified = InspectReport(Path.Combine(Path.GetDirectoryName(path)!, "report.json"), plan, trial);
                if (row.Score != verified.Score || row.StopReason != verified.StopReason || verified.Files.Any(file => !row.Evidence.ContainsKey(file))) throw new InvalidDataException("Result disagrees with its report or evidence list.");
            }
            else if (row.Score != null) throw new InvalidDataException("An incomplete game cannot have a ranked score.");
            rows.Add(row.Status == "started" ? row with { Status = OwnerStatus(row) } : row);
        }
        return rows;
    }

    public static ExperimentStatistics Statistics(ExperimentPlan plan, IReadOnlyList<ExperimentResult> results, string stage, string role)
    {
        var trials = plan.Trials.Where(t => t.Stage == stage && t.Role == role).ToArray();
        var scores = trials.Select(t => results.SingleOrDefault(r => r.Number == t.Number)).Where(r => r?.Status == "complete").Select(r => r!.Score!.Value).ToArray();
        var complete = scores.Length == trials.Length && scores.Length > 0;
        var sorted = scores.Order().ToArray();
        return new(trials.Length, scores.Length, complete ? scores.Average() : null,
            complete ? (sorted[(sorted.Length - 1) / 2] + sorted[sorted.Length / 2]) / 2.0 : null,
            complete ? scores.Min() : null, complete ? scores.Count(s => s >= plan.Target) : null, scores);
    }

    internal static bool DevelopmentPassed(ExperimentPlan plan, IReadOnlyList<ExperimentResult> results)
    {
        var baseline = Statistics(plan, results, "development", "baseline");
        var candidate = Statistics(plan, results, "development", "candidate");
        return candidate.Mean >= plan.Target && candidate.Mean > baseline.Mean;
    }

    public static string Verdict(ExperimentPlan plan, IReadOnlyList<ExperimentResult> results)
    {
        if (plan.Kind == "footage-study") return "Visual teaching study: review footage and export labeled examples to a game pack. Gameplay performance has not been tested.";
        if (results.Any(r => r.Status == "running")) return "A trial is running. Results remain provisional until the complete set finishes.";
        if (results.Any(r => r.Status is "started" or "unfinished")) return "An unfinished trial needs review; its owner is unknown. No complete comparison is available.";
        if (results.Any(r => r.Status != "complete")) return "Incomplete attempt — preserve it and start a new experiment; no ranking.";
        if (Statistics(plan, results, "development", "candidate").Mean == null || Statistics(plan, results, "development", "baseline").Mean == null)
            return "Development comparison pending.";
        if (!DevelopmentPassed(plan, results)) return "Candidate did not pass development. Validation remains unused.";
        var first = Statistics(plan, results, "validation-1", "candidate");
        var second = Statistics(plan, results, "validation-2", "candidate");
        if (first.Mean == null || second.Mean == null) return "Development improved; independent validation pending.";
        return first.Mean >= plan.Target && second.Mean >= plan.Target
            ? "Both fresh sets averaged at least 50. Review lower scores and variability; this supports only private 25% stepped play."
            : "Independent validation below target. Keep as research; continue improving.";
    }

    public ExperimentTrial? Next(ExperimentPlan plan, IReadOnlyList<ExperimentResult> results)
    {
        if (plan.Kind != "dodge-comparison") return null;
        if (results.Any(r => r.Status != "complete")) return null;
        var next = plan.Trials.FirstOrDefault(t => results.All(r => r.Number != t.Number));
        return next?.Stage == "development" || DevelopmentPassed(plan, results) ? next : null;
    }

    public void Begin(ExperimentPlan plan, ExperimentTrial trial)
    {
        var path = ResultPath(plan, trial);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        WriteNew(path, new ExperimentResult(trial.Number, "started", null, "unfinished", DateTime.UtcNow.ToString("O"), new(), process.Id, process.StartTime.ToUniversalTime().ToString("O")));
    }

    internal static string OwnerStatus(ExperimentResult result)
    {
        if (result.OwnerPid == null || result.OwnerStartedUtc == null) return "unfinished";
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(result.OwnerPid.Value);
            return !process.HasExited && process.StartTime.ToUniversalTime().ToString("O") == result.OwnerStartedUtc ? "running" : "interrupted";
        }
        catch (ArgumentException) { return "interrupted"; }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { return "unfinished"; }
    }

    public void Fail(ExperimentPlan plan, ExperimentTrial trial, string reason) =>
        WriteAtomic(ResultPath(plan, trial), new ExperimentResult(trial.Number, "incomplete", null, reason[..Math.Min(1000, reason.Length)], DateTime.UtcNow.ToString("O"), new()));

    public ExperimentResult Finish(ExperimentPlan plan, ExperimentTrial trial, string source)
    {
        var folder = Path.GetDirectoryName(ResultPath(plan, trial))!;
        var inspected = InspectReport(source, plan, trial);
        if (inspected.Files.Sum(name => new FileInfo(Child(Path.GetDirectoryName(source)!, name)).Length) > 128L * 1024 * 1024)
            throw new InvalidDataException("Trial evidence exceeds its 128 MiB preservation limit.");
        var evidence = new Dictionary<string, string>();
        // Preserve the report, sealed audit, and terminal frame with the experiment.
        foreach (var name in inspected.Files)
        {
            var path = Child(Path.GetDirectoryName(source)!, name);
            var copy = Path.Combine(folder, name);
            File.Copy(path, copy, overwrite: false);
            evidence[name] = Digest(copy);
        }
        var check = InspectReport(Path.Combine(folder, "report.json"), plan, trial);
        if (check.Score != inspected.Score || check.StopReason != inspected.StopReason) throw new IOException("Evidence changed during import.");
        var origin = Path.Combine(folder, "origin.json");
        WriteNew(origin, new { source_report = Path.GetFullPath(source), captured_at_import_utc = DateTime.UtcNow.ToString("O") });
        evidence["origin.json"] = Digest(origin);
        var complete = check.StopReason == "game-over" && check.Score != null;
        var row = new ExperimentResult(trial.Number, complete ? "complete" : "incomplete", complete ? check.Score : null, check.StopReason, DateTime.UtcNow.ToString("O"), evidence);
        WriteAtomic(ResultPath(plan, trial), row);
        return row;
    }

    private static (int? Score, string StopReason, string[] Files) InspectReport(string path, ExperimentPlan plan, ExperimentTrial trial)
    {
        if (Path.GetFileName(path) != "report.json") throw new InvalidDataException("Choose a game report.json file.");
        using var json = JsonDocument.Parse(BoundedBytes(path, 20_000_000));
        var root = json.RootElement; var training = root.GetProperty("training"); var evaluation = root.GetProperty("evaluation");
        var pack = root.GetProperty("pack"); var expected = plan.Packs.Single(p => p.Role == trial.Role);
        if (root.GetProperty("schema_version").GetInt32() != 1 || root.GetProperty("mode").GetString() != "wsl-private-endurance" ||
            root.GetProperty("seed").GetInt32() != trial.Seed || training.GetProperty("mode").GetString() != "stepped" || training.GetProperty("speed").GetDouble() != plan.Speed ||
            !training.GetProperty("benchmark").GetBoolean() || training.GetProperty("fixed_fps").GetInt32() != 30 || training.GetProperty("clock").GetString() != "simulated" ||
            training.GetProperty("action_limit").GetInt32() != 2400 || training.GetProperty("request_limit").GetInt32() != 9600 || training.GetProperty("time_limit_seconds").GetInt32() != 1200 ||
            training.GetProperty("observation_cadence").GetString() != "advance-before-decision" ||
            pack.GetProperty("Id").GetString() != expected.Id || pack.GetProperty("Version").GetString() != expected.Version ||
            !string.Equals(pack.GetProperty("Sha256").GetString(), expected.Sha256, StringComparison.OrdinalIgnoreCase) || evaluation.GetProperty("source").GetString() != "private-game-evaluator-not-policy")
            throw new InvalidDataException("Game report does not match the frozen experiment.");
        var files = new List<string> { "report.json" };
        var stop = root.GetProperty("stop_reason").GetString() ?? "unknown";
        if (stop != "game-over") return (null, stop, files.ToArray());
        var score = evaluation.GetProperty("score").GetInt32();
        if (score is < 0 or > 1000 || evaluation.GetProperty("alive").ValueKind != JsonValueKind.False || root.GetProperty("survival_observation").GetString() != "game-over-observed")
            throw new InvalidDataException("Game-over score has no matching terminal observation.");
        var folder = Path.GetDirectoryName(path)!;
        var audit = Child(folder, "evaluation-audit.jsonl");
        if (new FileInfo(audit).Length > 100L * 1024 * 1024) throw new InvalidDataException("Game audit exceeds the preservation limit.");
        if (!string.Equals(root.GetProperty("evaluation_audit_sha256").GetString(), Digest(audit), StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Captured game audit changed or is unsealed.");
        files.Add("evaluation-audit.jsonl");
        var frame = root.TryGetProperty("last_unrecognized_frame", out var last) && last.ValueKind == JsonValueKind.String ? last.GetString() : null;
        if (frame == null)
        {
            var actions = root.GetProperty("actions");
            if (actions.GetArrayLength() > 0) frame = actions[actions.GetArrayLength() - 1].GetProperty("after_file").GetString();
        }
        if (frame == null) throw new InvalidDataException("Terminal image is missing.");
        var image = BoundedBytes(Child(folder, frame), 54 + 480 * 720 * 4);
        if (image.Length != 54 + 480 * 720 * 4 || image[0] != 'B' || image[1] != 'M') throw new InvalidDataException("Invalid terminal image.");
        files.Add(frame);
        return (score, stop, files.Distinct().ToArray());
    }

    public string Preserve(string id, string note)
    {
        Text(note, 2000);
        var plan = Load(id); var results = Results(plan);
        var footage = new Dictionary<string, string>();
        var footageRoot = Path.Combine(DirectoryPath(id), "footage");
        if (Directory.Exists(footageRoot))
            foreach (var assessment in Directory.EnumerateFiles(footageRoot, "assessment.json", SearchOption.AllDirectories))
            {
                FootageAssessment.Verify(assessment);
                footage[Path.GetRelativePath(DirectoryPath(id), assessment)] = Digest(assessment);
            }
        if (results.Count == 0 && footage.Count == 0) throw new InvalidOperationException("Record a trial or attach footage before preserving a result.");
        var path = Path.Combine(DirectoryPath(id), "decisions", Guid.NewGuid().ToString("N") + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        WriteNew(path, new
        {
            schema_version = 1,
            recorded_utc = DateTime.UtcNow.ToString("O"),
            note,
            verdict = Verdict(plan, results),
            plan_sha256 = Digest(Path.Combine(DirectoryPath(id), "plan.json")),
            packs = plan.Packs,
            results,
            footage,
            statistics = new[] { ("development", "baseline"), ("development", "candidate"), ("validation-1", "candidate"), ("validation-2", "candidate") }
                .Select(pair => new { stage = pair.Item1, role = pair.Item2, summary = Statistics(plan, results, pair.Item1, pair.Item2) }),
            deployment = "Preserved research snapshot. No installed or active pack was changed."
        });
        return path;
    }

    internal string ResultPath(ExperimentPlan plan, ExperimentTrial trial) => Path.Combine(DirectoryPath(plan.Id), "trials", trial.Number.ToString("D3"), "result.json");
    internal static string Child(string folder, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || Path.GetFileName(name) != name || name.IndexOfAny(['/', '\\', ':']) >= 0 || name is "." or "..") throw new InvalidDataException("Invalid evidence filename.");
        return Path.Combine(folder, name);
    }
    internal static byte[] BoundedBytes(string path, long maximum)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length > maximum || (info.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Evidence is missing, linked, or too large.");
        var bytes = File.ReadAllBytes(path);
        if (bytes.LongLength > maximum) throw new InvalidDataException("Evidence grew while being read.");
        return bytes;
    }
    internal static string Digest(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists || (info.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Evidence is missing or linked.");
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
    internal static T Read<T>(string path) => JsonSerializer.Deserialize<T>(BoundedBytes(path, 2_000_000), Json) ?? throw new InvalidDataException("Invalid experiment file.");
    internal static void WriteNew<T>(string path, T data) { using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None); JsonSerializer.Serialize(stream, data, Json); }
    internal static void WriteAtomic<T>(string path, T data)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { WriteNew(temporary, data); File.Move(temporary, path, true); } finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private static void Text(string value, int maximum) { if (string.IsNullOrWhiteSpace(value) || value.Length > maximum) throw new InvalidDataException($"Enter text of 1–{maximum} characters."); }
}
