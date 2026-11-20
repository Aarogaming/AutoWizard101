using System.Collections.Concurrent;
using System.Text.Json;
using System.Linq;
using System.Drawing;
using ProjectMaelstrom.Models;
using ProjectMaelstrom.Modules;
using ProjectMaelstrom.Modules.ImageRecognition;
using ProjectMaelstrom.Properties;

namespace ProjectMaelstrom.Utilities;

/// <summary>
/// Coordinator for smart play: consumes snapshots + navigation knowledge to emit input sequences through the bridge.
/// </summary>
internal sealed class SmartPlayManager : IDisposable
{
    private readonly object _lock = new();
    private readonly ConcurrentQueue<SmartTask> _taskQueue = new();
    private readonly PlayerController _controller;
    private InputBridge? _inputBridge;
    private GameStateSnapshot? _latestSnapshot;
    private readonly WizWikiDataService _wiki = WizWikiDataService.Instance;
    private System.Threading.Timer? _timer;
    private bool _disposing;
    private SmartTask? _activeTask;
    private OperationTicket? _operation;
    private readonly string _taskPath;
    private readonly TemplateLibrary _templateLibrary = TemplateLibrary.Instance;
    private readonly string _tuningPath;
    private SmartTelemetry _telemetry = new();
    private DateTime _lastHeartbeat = DateTime.UtcNow;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly List<GameAudioCue> _recentAudioCues = new();
    private readonly TimeSpan _audioHorizon = TimeSpan.FromSeconds(20);
    private Func<string>? _designCaptureHandler;

    public SmartPlayManager(PlayerController controller)
    {
        _controller = controller;
        _taskPath = Path.Combine(StorageUtils.GetCacheDirectory(), "smartplay_tasks.json");
        _tuningPath = Path.Combine(StorageUtils.GetCacheDirectory(), "smartplay_tuning.json");
        LoadTelemetry();
    }

    public void AttachInputBridge(InputBridge bridge)
    {
        _inputBridge = bridge;
    }

    public void Start(TimeSpan? tickInterval = null)
    {
        Stop();
        _timer = new System.Threading.Timer(_ => Pump(), null, TimeSpan.Zero, tickInterval ?? TimeSpan.FromMilliseconds(750));
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
        lock (_lock) { _operation?.Cancel(); _taskQueue.Clear(); }
        _controller.ReleaseAll();
    }

    public int QueueLength
    {
        get
        {
            return _taskQueue.Count + (_activeTask != null ? 1 : 0);
        }
    }

    public void UpdateSnapshot(GameStateSnapshot snapshot)
    {
        _latestSnapshot = snapshot;
    }

    public void UpdateSensors(GameSensors sensors)
    {
        if (sensors.Snapshot != null)
        {
            _latestSnapshot = sensors.Snapshot;
        }

        if (sensors.AudioCues != null && sensors.AudioCues.Count > 0)
        {
            lock (_recentAudioCues)
            {
                _recentAudioCues.AddRange(sensors.AudioCues);
                PruneOldAudio();
            }
        }
    }

    public bool HasRecentAudioCue(string cueType, TimeSpan window, double minConfidence = 0.5)
    {
        lock (_recentAudioCues)
        {
            PruneOldAudio();
            var since = DateTime.UtcNow - window;
            return _recentAudioCues.Any(c =>
                c.CapturedUtc >= since &&
                c.Confidence >= minConfidence &&
                string.Equals(cueType, c.Type, StringComparison.OrdinalIgnoreCase));
        }
    }

    public GameAudioCue? GetLatestAudioCue(string? cueType = null)
    {
        lock (_recentAudioCues)
        {
            PruneOldAudio();
            if (cueType == null)
            {
                return _recentAudioCues.LastOrDefault();
            }

            return _recentAudioCues
                .Where(c => string.Equals(cueType, c.Type, StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c.CapturedUtc)
                .LastOrDefault();
        }
    }

    public void EnqueueNavigationToBazaar()
    {
        QueueTask(SmartTask.FromPlan("Go to Bazaar", NavigationKnowledge.GetBazaarPlanFromAnywhere(), RouteScaler));
    }

    public void EnqueueNavigationToMiniGames()
    {
        QueueTask(SmartTask.FromPlan("Go to Mini Games", NavigationKnowledge.GetMiniGamePlanFromAnywhere(), RouteScaler));
    }

    public void EnqueueNavigationToPetPavilion()
    {
        QueueTask(SmartTask.FromPlan("Go to Pet Pavilion", NavigationKnowledge.GetPetPavilionPlanFromAnywhere(), RouteScaler));
    }

    public void EnqueuePotionRefillRun()
    {
        var plan = NavigationKnowledge.GetMiniGamePlanFromAnywhere();
        QueueTask(SmartTask.FromPlan("Potion refill via mini-game", plan, RouteScaler, includeMiniGameLoop: true));
    }

    public void EnqueueScriptRun(string scriptName, int repeatCount = 1)
    {
        if (repeatCount is < 1 or > 20) throw new InvalidOperationException("Choose a finite repeat count from 1 to 20. Unlimited legacy runs are no longer accepted.");
        var script = ScriptLibraryService.Instance.Scripts
            .FirstOrDefault(s => string.Equals(s.Manifest.Name, scriptName, StringComparison.OrdinalIgnoreCase));

        if (script == null)
        {
            Logger.LogError($"[SmartPlay] Script not found for smart run: {scriptName}");
            return;
        }

        QueueTask(SmartTask.FromScript(script, repeatCount));
    }

    public void EnqueueDesignCapture()
    {
        if (_designCaptureHandler == null) return;
        QueueTask(SmartTask.DesignCapture());
    }

    internal void EnqueueExternalTask(SmartTask task)
    {
        if (task == null) return;
        QueueTask(task);
    }
    private void QueueTask(SmartTask task)
    {
        lock (_lock)
        {
            if (QueueLength >= OperationScheduler.Capacity) throw new InvalidOperationException("Legacy task queue is full (32 tasks).");
            _taskQueue.Enqueue(task);
        }
    }

    /// <summary>
    /// Attempts to find a template in a given screenshot and enqueue a click at its center.
    /// </summary>
    public void EnqueueTemplateClick(string sourceImagePath, string templateKey, double threshold = 0.85, System.Drawing.Rectangle? region = null)
    {
        if (string.IsNullOrWhiteSpace(sourceImagePath) || string.IsNullOrWhiteSpace(templateKey))
        {
            Logger.LogError("[SmartPlay] Template click skipped: missing source or template key.");
            return;
        }

        var templatePath = _templateLibrary.FindTemplatePath(templateKey);
        if (templatePath == null)
        {
            Logger.LogError($"[SmartPlay] Template click skipped: template '{templateKey}' not found.");
            return;
        }

        var match = TemplateMatcher.FindBestMatch(sourceImagePath, templatePath, threshold, region);
        if (!match.Found)
        {
            Logger.LogBotAction("SmartPlay", $"Template '{templateKey}' not found (score {match.Score:0.00}).");
            return;
        }

        var clickCmd = new InputCommand
        {
            Type = "click",
            X = match.Center.X,
            Y = match.Center.Y
        };

        // A saved screenshot cannot establish fresh live target coordinates.
        Logger.LogBotAction("SmartPlay", $"Template '{templateKey}' matched at {match.Center} (score {match.Score:0.00}). Saved-image clicks remain simulation-only.");
        new SimulationExecutor().Execute(new[] { clickCmd }, new ExecutionContext { Source = "SmartPlay.Template", Bridge = _inputBridge });
    }

    public void SetDesignCaptureHandler(Func<string> captureHandler)
    {
        _designCaptureHandler = captureHandler;
    }

    private void MaybeEnqueueZoneHints()
    {
        // Placeholder: zone detection is not yet in snapshots.
        // Once snapshot includes zone/coords, use _wiki.FindZone(zoneName) to enqueue contextual routes.
        return;
    }

    public void AddAudioCue(GameAudioCue cue)
    {
        if (cue == null || string.IsNullOrWhiteSpace(cue.Type)) return;
        lock (_recentAudioCues)
        {
            _recentAudioCues.Add(cue);
            PruneOldAudio();
        }
    }

    private void Pump()
    {
        lock (_lock)
        {
            if (_disposing) return;
            try
            {
                if (_operation != null)
                {
                    if (!_operation.Completion.IsCompleted) return;
                    var receipt = _operation.Completion.GetAwaiter().GetResult();
                    Logger.LogBotAction("SmartPlay", $"{receipt.State}: {receipt.Message}");
                    _operation = null; _activeTask = null;
                    if (receipt.State != OperationState.Completed) _taskQueue.Clear();
                }
                LoadExternalTasks();
                if (!_taskQueue.TryDequeue(out var next)) return;
                _activeTask = next;
                var policy = ExecutionPolicyManager.Current;
                bool live = policy.AllowLiveAutomation && next.Kind != SmartTaskKind.DesignCapture && !ScriptLibraryService.Instance.DryRun;
                var target = live ? GameWindows.List().SingleOrDefault(w => HomeMovementGate.IsWizard101(w) && w.Handle == WinAPI.GetForegroundWindow()) : null;
                if (live && target == null) throw new InvalidOperationException("Live legacy task paused: Wizard101 must already be foreground.");
                var pins = new Dictionary<string, string>();
                if (next.Script != null)
                {
                    var path = Path.GetFullPath(Path.Combine(next.Script.RootPath, next.Script.Manifest.EntryPoint)); pins[path] = GameExperiments.Digest(path);
                }
                var request = new OperationRequest(Guid.NewGuid().ToString("N"), "legacy." + next.Kind, "legacy-bounded-v1", 600, live,
                    target == null ? "simulation" : $"{target.ProcessId}:{target.StartedUtcTicks}:{target.Handle}", pins);
                _operation = ApplicationOperations.For(WorkshopHost.DataRoot(AppContext.BaseDirectory)).Submit(request, async (_, token) =>
                {
                    try
                    {
                        if (next.Kind == SmartTaskKind.DesignCapture)
                        {
                            if (_designCaptureHandler == null) return new(OperationState.Failed, "No capture handler is registered.", []);
                            return OperationOutcome.Completed("Design capture saved.", _designCaptureHandler());
                        }
                        if (next.Kind == SmartTaskKind.ScriptRun)
                        {
                            for (int repeat = 0; repeat < next.RemainingRuns; repeat++)
                            {
                                token.ThrowIfCancellationRequested();
                                if (!live) return new(OperationState.Completed, "Script simulation: no executable launched.", []);
                                if (!GameWindows.IsCurrent(target!)) throw new InvalidOperationException("Selected target changed.");
                                var session = ScriptLibraryService.Instance.StartScript(next.Script!);
                                try { await session.Process.WaitForExitAsync(token).ConfigureAwait(false); }
                                finally { if (ScriptLibraryService.Instance.CurrentSession == session) ScriptLibraryService.Instance.StopCurrentScript(); }
                                if (session.Process.ExitCode != 0) return new(OperationState.Failed, "Legacy script exited with an error; gameplay result is unverified.", []);
                            }
                            return new(OperationState.Unknown, "Legacy process exited; game outcome remains unverified. Dependent legacy work paused.", []);
                        }
                        var executor = live ? ExecutorFactory.FromPolicy(ExecutionPolicyManager.Current) : new SimulationExecutor();
                        foreach (var command in next.GetPendingCommands())
                        {
                            token.ThrowIfCancellationRequested();
                            if (live && (!ExecutionPolicyManager.Current.AllowLiveAutomation || !GameWindows.IsCurrent(target!) || WinAPI.GetForegroundWindow() != target!.Handle))
                                throw new InvalidOperationException("Game controls paused: policy, target or foreground changed.");
                            executor.Execute([command], new ExecutionContext { Source = "SmartPlay.Operation", TaskName = next.Name, Bridge = _inputBridge });
                        }
                        return new(live ? OperationState.Unknown : OperationState.Completed, live ? "Commands finished; arrival/game outcome unverified. Dependent legacy work paused." : "Simulation finished; no game inputs sent.", []);
                    }
                    finally { _controller.ReleaseAll(); }
                });
            }
            catch (Exception error) { Logger.LogError("[SmartPlay] Operation paused", error); _operation = null; _activeTask = null; _taskQueue.Clear(); }
        }
    }

    private void EmitHeartbeat()
    {
        // Emit a lightweight periodic snapshot so we can debug queue state in dev mode.
        var now = DateTime.UtcNow;
        if ((now - _lastHeartbeat).TotalSeconds < 5) return;
        _lastHeartbeat = now;

        var active = _activeTask?.Name ?? "none";
        var queueDepth = _taskQueue.Count;
        var audioCue = GetLatestAudioCue()?.Type ?? "none";
        var bridgeIdle = _inputBridge?.IsIdle ?? false;
        Logger.Log($"[DevMode][SmartPlay] heartbeat active={active} queue={queueDepth} audioCue={audioCue} bridgeIdle={bridgeIdle}");
        DevTelemetry.Log("SmartPlay", $"heartbeat active={active} queue={queueDepth} audioCue={audioCue} bridgeIdle={bridgeIdle}");
    }

    private void HandleDesignCapture(SmartTask task)
    {
        if (_designCaptureHandler == null)
        {
            _activeTask = null;
            return;
        }

        try
        {
            var path = _designCaptureHandler.Invoke();
            Logger.Log($"[SmartPlay] Design capture saved to {path}");
        }
        catch (Exception ex)
        {
            Logger.LogError("[SmartPlay] Design capture failed", ex);
        }
        finally
        {
            _activeTask = null;
        }
    }

    private void LoadExternalTasks()
    {
        if (!File.Exists(_taskPath)) return;
        var history = Path.Combine(Path.GetDirectoryName(_taskPath)!, "legacy-task-inbox");
        Directory.CreateDirectory(history);
        var claimed = Path.Combine(history, Guid.NewGuid().ToString("N") + ".received.json");
        try
        {
            File.Move(_taskPath, claimed);
            if (new FileInfo(claimed).Length > 1048576) throw new InvalidDataException("Legacy task request exceeds 1 MiB.");
            var json = File.ReadAllText(claimed);
            var defs = JsonSerializer.Deserialize<List<SmartPlayTaskDefinition>>(json, _jsonOptions);
            ValidateInbox(defs, OperationScheduler.Capacity - QueueLength);
            if (defs!.Any(d => string.Equals(d.Type?.Trim(), "script_run", StringComparison.OrdinalIgnoreCase) && !ScriptLibraryService.Instance.Scripts.Any(s => s.Manifest.Name.Equals(d.ScriptName, StringComparison.OrdinalIgnoreCase))))
                throw new InvalidDataException("A requested script is not installed.");

            foreach (var def in defs!)
            {
                if (def == null || string.IsNullOrWhiteSpace(def.Type)) continue;
                switch (def.Type.Trim().ToLowerInvariant())
                {
                    case "nav":
                        if (string.Equals(def.Target, "bazaar", StringComparison.OrdinalIgnoreCase))
                            EnqueueNavigationToBazaar();
                        else if (string.Equals(def.Target, "minigames", StringComparison.OrdinalIgnoreCase))
                            EnqueueNavigationToMiniGames();
                        else if (string.Equals(def.Target, "pet", StringComparison.OrdinalIgnoreCase))
                            EnqueueNavigationToPetPavilion();
                        break;
                    case "potion_refill":
                        EnqueuePotionRefillRun();
                        break;
                    case "script_run":
                        if (!string.IsNullOrWhiteSpace(def.ScriptName))
                        {
                            EnqueueScriptRun(def.ScriptName!, def.RepeatCount ?? 1);
                        }
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("[SmartPlay] Failed to load smartplay_tasks.json", ex);
            if (File.Exists(claimed)) File.WriteAllText(claimed + ".error.txt", ex.Message);
        }
        // Preserve even invalid input for inspection; never delete it or retry it implicitly.
    }
    internal static void ValidateInbox(List<SmartPlayTaskDefinition>? definitions, int available)
    {
        if (definitions == null || definitions.Count is < 1 or > 32 || definitions.Count > available) throw new InvalidDataException("Legacy task batch must fit within the bounded queue.");
        foreach (var definition in definitions)
        {
            if (definition == null) throw new InvalidDataException("Missing legacy task.");
            var type = definition.Type?.Trim().ToLowerInvariant();
            if (type == "script_run" && !string.IsNullOrWhiteSpace(definition.ScriptName) && (definition.RepeatCount ?? 1) is >= 1 and <= 20) continue;
            if (type == "nav" && definition.Target?.ToLowerInvariant() is "bazaar" or "minigames" or "pet") continue;
            if (type == "potion_refill") continue;
            throw new InvalidDataException("Unknown task or unbounded script repetition; no part of the batch was admitted.");
        }
    }

    private void RecordRouteTiming(IEnumerable<string> routeNames, TimeSpan duration)
    {
        if (duration.TotalMilliseconds <= 0) return;
        foreach (var name in routeNames)
        {
            if (string.IsNullOrWhiteSpace(name)) continue;
            var key = name.Trim();
            if (!_telemetry.Routes.TryGetValue(key, out var stats))
            {
                stats = new RouteTimingStats { AverageMs = duration.TotalMilliseconds, Samples = 1 };
            }
            else
            {
                var total = stats.AverageMs * stats.Samples + duration.TotalMilliseconds;
                stats.Samples += 1;
                stats.AverageMs = total / stats.Samples;
            }
            _telemetry.Routes[key] = stats;
        }
        SaveTelemetry();
    }

    private double GetRouteScale(string routeName, int defaultTotalMs)
    {
        if (!Settings.Default.ENABLE_SELF_TUNING) return 1.0;
        if (defaultTotalMs <= 0) return 1.0;
        if (!_telemetry.Routes.TryGetValue(routeName, out var stats) || stats.Samples == 0) return 1.0;
        var factor = stats.AverageMs / defaultTotalMs;
        return Math.Clamp(factor, 0.5, 1.5);
    }

    private double RouteScaler(string routeName, int defaultTotalMs) => GetRouteScale(routeName, defaultTotalMs);

    private void LoadTelemetry()
    {
        try
        {
            if (File.Exists(_tuningPath))
            {
                var json = File.ReadAllText(_tuningPath);
                _telemetry = JsonSerializer.Deserialize<SmartTelemetry>(json, _jsonOptions) ?? new SmartTelemetry();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("[SmartPlay] Failed to load tuning data", ex);
            _telemetry = new SmartTelemetry();
        }
    }

    private void SaveTelemetry()
    {
        try
        {
            var json = JsonSerializer.Serialize(_telemetry, _jsonOptions);
            File.WriteAllText(_tuningPath, json);
        }
        catch (Exception ex)
        {
            Logger.LogError("[SmartPlay] Failed to save tuning data", ex);
        }
    }

    public void Dispose()
    {
        _disposing = true;
        Stop();
    }

    public string DescribeState()
    {
        if (_activeTask != null)
        {
            return $"Running: {_activeTask.Name}";
        }

        if (!_taskQueue.IsEmpty)
        {
            return "Queued tasks waiting";
        }

        return "Idle";
    }

    public void ClearAllTasks()
    {
        while (_taskQueue.TryDequeue(out _)) { }
        _activeTask = null;
    }

    public void ResetTuning()
    {
        _telemetry = new SmartTelemetry();
        try
        {
            if (File.Exists(_tuningPath))
            {
                File.Delete(_tuningPath);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("[SmartPlay] Failed to reset tuning cache", ex);
        }
    }

    private void PruneOldAudio()
    {
        var cutoff = DateTime.UtcNow - _audioHorizon;
        if (_recentAudioCues.Count == 0) return;
        _recentAudioCues.RemoveAll(c => c.CapturedUtc < cutoff);
    }

    internal sealed class SmartTask
    {
        private readonly Queue<InputCommand> _commands;
        public string Name { get; }
        public SmartTaskKind Kind { get; }
        public ScriptDefinition? Script { get; }
        public int RemainingRuns { get; }
        public bool Started { get; set; }
        public IReadOnlyList<string> RouteNames { get; }

        private SmartTask(string name, IEnumerable<InputCommand> commands, IReadOnlyList<string>? routeNames = null)
        {
            Name = name;
            Kind = SmartTaskKind.Inputs;
            _commands = new Queue<InputCommand>(commands);
            RouteNames = routeNames ?? Array.Empty<string>();
        }

        private SmartTask(string name, ScriptDefinition script, int remainingRuns)
        {
            Name = name;
            Kind = SmartTaskKind.ScriptRun;
            Script = script;
            RemainingRuns = remainingRuns;
            _commands = new Queue<InputCommand>();
            RouteNames = Array.Empty<string>();
        }

        private SmartTask(string name, SmartTaskKind kind)
        {
            Name = name;
            Kind = kind;
            Script = null;
            RemainingRuns = 0;
            _commands = new Queue<InputCommand>();
            RouteNames = Array.Empty<string>();
        }

        public static SmartTask FromPlan(string name, TravelPlan plan, Func<string, int, double> routeScaler, bool includeMiniGameLoop = false)
        {
            var cmds = new List<InputCommand>();
            var routeNames = new List<string>();

            foreach (var route in plan.Routes)
            {
                var routeCmds = ToCommands(route);
                var tuned = ApplyRouteTuning(route.Name, routeCmds, routeScaler);
                cmds.AddRange(tuned);
                routeNames.Add(route.Name);
            }

            if (includeMiniGameLoop)
            {
                var loop = MiniGameLoop();
                var tunedLoop = ApplyRouteTuning("MiniGameLoop", loop, routeScaler);
                cmds.AddRange(tunedLoop);
                routeNames.Add("MiniGameLoop");
            }

            return new SmartTask(name, cmds, routeNames);
        }

        public IEnumerable<InputCommand> GetPendingCommands()
        {
            while (_commands.Count > 0)
            {
                yield return _commands.Dequeue();
            }
        }

        public static SmartTask FromScript(ScriptDefinition script, int repeat)
        {
            return new SmartTask($"Run script: {script.Manifest.Name}", script, repeat);
        }

        public SmartTask CloneForNextRun(int? remainingRunsOverride = null)
        {
            int next = remainingRunsOverride ?? RemainingRuns;
            return new SmartTask(Name, Script!, next);
        }

        public static SmartTask DesignCapture()
        {
            return new SmartTask("Design Capture", SmartTaskKind.DesignCapture);
        }

        private static IEnumerable<InputCommand> ToCommands(TravelRoute route)
        {
            // Rough heuristics; meant to be refined per profile/coords.
            switch (route.Name)
            {
                case "Teleport home chain":
                    return new[]
                    {
                        new InputCommand { Type = "key_press", Key = "HOME", DelayMs = 4500 },
                        new InputCommand { Type = "key_press", Key = "HOME", DelayMs = 4500 }
                    };
                case "Dorm to Wizard City commons":
                    return new[]
                    {
                        new InputCommand { Type = "key_down", Key = "UP", DelayMs = 2000 },
                        new InputCommand { Type = "key_up", Key = "UP", DelayMs = 1500 }
                    };
                case "Commons to Bazaar":
                    return new[]
                    {
                        new InputCommand { Type = "key_press", Key = "LEFT", DelayMs = 500 },
                        new InputCommand { Type = "key_down", Key = "UP", DelayMs = 6000 },
                        new InputCommand { Type = "key_up", Key = "UP", DelayMs = 500 },
                        new InputCommand { Type = "key_press", Key = "X", DelayMs = 2500 } // enter Bazaar
                    };
                case "Commons to Mini Games":
                    return new[]
                    {
                        new InputCommand { Type = "key_press", Key = "RIGHT", DelayMs = 500 },
                        new InputCommand { Type = "key_down", Key = "UP", DelayMs = 5000 },
                        new InputCommand { Type = "key_up", Key = "UP", DelayMs = 500 },
                        new InputCommand { Type = "key_press", Key = "X", DelayMs = 2500 } // interact kiosk
                    };
                case "Commons to Pet Pavilion":
                    return new[]
                    {
                        new InputCommand { Type = "key_press", Key = "LEFT", DelayMs = 500 },
                        new InputCommand { Type = "key_down", Key = "UP", DelayMs = 7000 },
                        new InputCommand { Type = "key_up", Key = "UP", DelayMs = 500 }
                    };
                default:
                    Logger.LogBotAction("SmartPlay", $"No mapped commands for route: {route.Name}");
                    return Array.Empty<InputCommand>();
            }
        }

        private static IEnumerable<InputCommand> MiniGameLoop()
        {
            // Placeholder: interact, small move, repeat.
            return new[]
            {
                new InputCommand { Type = "key_press", Key = "X", DelayMs = 2000 },
                new InputCommand { Type = "key_press", Key = "UP", DelayMs = 250 },
                new InputCommand { Type = "key_press", Key = "SPACE", DelayMs = 250 },
                new InputCommand { Type = "delay", DelayMs = 5000 }
            };
        }

        private static List<InputCommand> ApplyRouteTuning(string routeName, IEnumerable<InputCommand> commands, Func<string, int, double> routeScaler)
        {
            var list = commands.Select(cmd => new InputCommand
            {
                Type = cmd.Type,
                X = cmd.X,
                Y = cmd.Y,
                Key = cmd.Key,
                DelayMs = cmd.DelayMs
            }).ToList();

            int defaultTotal = list.Sum(c => c.DelayMs);
            if (defaultTotal <= 0) return list;

            double scale = Math.Clamp(routeScaler(routeName, defaultTotal), 0.5, 1.5);

            for (int i = 0; i < list.Count; i++)
            {
                var d = list[i].DelayMs;
                if (d <= 0) continue;
                int tuned = (int)Math.Clamp(d * scale, 50, 15000);
                list[i].DelayMs = tuned;
            }
            return list;
        }
    }

    private sealed class SmartTelemetry
    {
        public Dictionary<string, RouteTimingStats> Routes { get; set; } = new();
    }

    private sealed class RouteTimingStats
    {
        public double AverageMs { get; set; }
        public int Samples { get; set; }
    }
}

internal enum SmartTaskKind
{
    Inputs,
    ScriptRun,
    DesignCapture
}
