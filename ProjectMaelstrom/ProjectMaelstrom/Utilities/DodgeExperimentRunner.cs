namespace ProjectMaelstrom.Utilities;

/// <summary>One private game at a time; explicit budgets, cancellation and resumable completed trials.</summary>
internal static class DodgeExperimentRunner
{
    internal static Dictionary<string, string> Fingerprints(string workspace)
    {
        workspace = Path.GetFullPath(workspace);
        var files = new List<string> { typeof(DodgeExperimentRunner).Assembly.Location };
        files.AddRange(new[] { "wsl_dodge_launch.sh", "wsl_dodge_session.sh", "wsl_dodge_guest.py" }.Select(name => Path.Combine(workspace, "tools", name)));
        files.AddRange(new[] { "godot/Godot_v4.7.2-stable_linux.x86_64", "root-current/usr/bin/Xvfb", "root-current/usr/bin/xdotool" }
            .Select(name => Path.Combine(workspace, ".local", "wsl-dodge-tools", name)));
        var game = Path.Combine(workspace, ".local", "wsl-dodge-step-game");
        if (!Directory.Exists(game)) throw new DirectoryNotFoundException("Prepare the isolated Dodge game in the development folder first.");
        files.AddRange(Directory.GetFiles(game, "*", SearchOption.TopDirectoryOnly).Where(p => Path.GetExtension(p) is ".gd" or ".tscn" or ".godot"));
        foreach (var folder in new[] { "art", "fonts" })
            if (Directory.Exists(Path.Combine(game, folder))) files.AddRange(Directory.GetFiles(Path.Combine(game, folder), "*", SearchOption.AllDirectories));
        return files.Order(StringComparer.Ordinal).ToDictionary(Path.GetFullPath, GameExperiments.Digest, StringComparer.OrdinalIgnoreCase);
    }

    internal static void VerifyEnvironment(ExperimentPlan plan)
    {
        var current = Fingerprints(plan.Workspace);
        if (current.Count != plan.Environment.Count || current.Any(pair => !plan.Environment.TryGetValue(pair.Key, out var hash) || hash != pair.Value))
            throw new InvalidDataException("Application or game changed since this experiment was created. Preserve it and start a new comparison.");
    }

    // Same Windows byte-range lock as tools/train_dodge_offline.py.
    internal static FileStream AcquireStudy(string workspace)
    {
        Directory.CreateDirectory(Path.Combine(workspace, ".local"));
        var stream = new FileStream(Path.Combine(workspace, ".local", "dodge-training.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        try
        {
            if (stream.Length == 0) { stream.WriteByte(0); stream.Flush(); }
            stream.Lock(0, 1);
            return stream;
        }
        catch { stream.Dispose(); throw new IOException("Another isolated Dodge study is using the game. Try after it finishes."); }
    }

    public static async Task RunAsync(GameExperiments store, string id, int maximumGames, int minutes, IProgress<string>? progress, CancellationToken token,
        Func<ExperimentPlan, ExperimentTrial, string, CancellationToken, Task<string>>? execute = null)
    {
        if (maximumGames is < 1 or > 28 || minutes is < 1 or > 120) throw new ArgumentOutOfRangeException(nameof(maximumGames));
        var plan = store.Load(id);
        if (plan.Kind != "dodge-comparison") throw new InvalidOperationException("This footage study has no gameplay execution adapter. Review its visual examples instead.");
        var reserveWholeGame = execute == null;
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        execute ??= (p, t, pack, cancellation) => DodgeWslVerification.RunAsync(p.Workspace, pack, cancellation,
            t.Seed, stepped: true, speed: .25, endurance: true, benchmark: true);
        using var studyLock = AcquireStudy(plan.Workspace);
        VerifyEnvironment(plan);
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(token);
        budget.CancelAfter(TimeSpan.FromMinutes(minutes));
        var runRoot = Path.Combine(plan.Workspace, ".local", "wsl-dodge-runs");
        Directory.CreateDirectory(runRoot);
        var existing = Directory.GetDirectories(runRoot).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var priorRuns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var origin in Directory.EnumerateFiles(store.DirectoryPath(id), "origin.json", SearchOption.AllDirectories))
        {
            using var saved = System.Text.Json.JsonDocument.Parse(GameExperiments.BoundedBytes(origin, 8192));
            var report = saved.RootElement.GetProperty("source_report").GetString();
            if (report != null && Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(report))) == runRoot)
                priorRuns.Add(Path.GetDirectoryName(report)!);
        }
        string? resourceStop = null;
        using var monitorStop = new CancellationTokenSource();
        var monitor = MonitorAsync();
        try
        {
            for (var i = 0; i < maximumGames; i++)
            {
                budget.Token.ThrowIfCancellationRequested();
                plan = store.Load(id);
                var trial = store.Next(plan, store.Results(plan));
                if (trial == null) break;
                if (reserveWholeGame && TimeSpan.FromMinutes(minutes) - elapsed.Elapsed < TimeSpan.FromSeconds(1245))
                { progress?.Report("Time budget preserved. Resume the remaining trials later; no partial game was started."); break; }
                VerifyEnvironment(plan);
                CheckDisk();
                budget.Token.ThrowIfCancellationRequested();
                progress?.Report($"{trial.Stage} · {trial.Role} · trial {trial.Number + 1}/28 · private 25% stepped game");
                store.Begin(plan, trial);
                try
                {
                    var pack = plan.Packs.Single(p => p.Role == trial.Role);
                    var report = await execute(plan, trial, Path.Combine(store.DirectoryPath(id), pack.File), budget.Token);
                    VerifyEnvironment(plan);
                    var result = store.Finish(plan, trial, report);
                    progress?.Report($"Trial {trial.Number + 1}: {result.Status} · score {result.Score?.ToString() ?? "unranked"} · {result.StopReason}");
                    if (result.Status != "complete") break;
                }
                catch (Exception ex)
                {
                    store.Fail(plan, trial, resourceStop ?? (budget.IsCancellationRequested ? "cancelled-or-time-budget" : ex.Message));
                    throw;
                }
            }
            progress?.Report(GameExperiments.Verdict(plan, store.Results(plan)));
        }
        finally
        {
            monitorStop.Cancel();
            try { await monitor; } catch (OperationCanceledException) { }
        }

        void CheckDisk()
        {
            var folders = Directory.GetDirectories(runRoot).Where(p => !existing.Contains(p) || priorRuns.Contains(p)).Append(store.DirectoryPath(id));
            long bytes = 0;
            foreach (var folder in folders)
                foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
                {
                    try { bytes += new FileInfo(file).Length; } catch (FileNotFoundException) { }
                    if (bytes > plan.DiskLimitMb * 1024L * 1024) { resourceStop = "experiment-disk-budget"; budget.Cancel(); return; }
                }
            if (new DriveInfo(Path.GetPathRoot(plan.Workspace)!).AvailableFreeSpace < 2L * 1024 * 1024 * 1024)
            { resourceStop = "less-than-2-GiB-free"; budget.Cancel(); }
        }
        async Task MonitorAsync()
        {
            while (!monitorStop.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), monitorStop.Token);
                try { CheckDisk(); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { resourceStop = "resource-monitor-failed"; budget.Cancel(); return; }
            }
        }
    }
}
