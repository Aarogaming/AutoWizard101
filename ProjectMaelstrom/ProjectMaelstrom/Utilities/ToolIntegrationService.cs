using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectMaelstrom.Models;

namespace ProjectMaelstrom.Utilities;

internal sealed class ToolReadiness
{
    [JsonPropertyName("tool")] public string Tool { get; set; } = "";
    [JsonPropertyName("source_present")] public bool SourcePresent { get; set; }
    [JsonPropertyName("entry_present")] public bool? EntryPresent { get; set; }
    [JsonPropertyName("probe_error")] public string? ProbeError { get; set; }
    [JsonPropertyName("missing_packages")] public string[] MissingPackages { get; set; } = Array.Empty<string>();
    public bool Installed => SourcePresent && EntryPresent != false && ProbeError == null && MissingPackages.Length == 0;
    public string DisplayStatus => Installed ? "Installed · game compatibility unverified" : "Needs setup";
}

/// <summary>Owns tool processes for one Tools window; all arguments are passed without a shell.</summary>
internal sealed class ToolIntegrationService : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Process> _running = new();
    private readonly Dictionary<string, OperationTicket> _scheduled = new();
    private readonly Dictionary<string, Task> _exits = new();
    private readonly Dictionary<string, OwnedProcessJob> _jobs = new();
    private readonly Dictionary<string, int> _exitCodes = new();
    private readonly HashSet<string> _stopping = new();
    private readonly List<string> _messages = new();
    private CancellationTokenSource _operations = new();
    private bool _disposed;
    public string? Workspace { get; private set; }
    public GuidedActivitySession? GuidedActivity { get; private set; }
    public string Messages { get { lock (_gate) return string.Join(Environment.NewLine, _messages); } }
    public bool IsRunning(string tool) { lock (_gate) return _running.ContainsKey(tool) || _scheduled.ContainsKey(tool); }
    public bool HasGameplaySession => IsRunning("dance") || IsRunning("deimos");
    public bool HasOpenTools { get { lock (_gate) return _running.Count > 0 || _scheduled.Count > 0; } }
    public int? LastExitCode(string tool) { lock (_gate) return _exitCodes.TryGetValue(tool, out var code) ? code : null; }

    public ToolIntegrationService(string? workspace = null)
    {
        Workspace = workspace ?? FindWorkspace(AppContext.BaseDirectory);
        var saved = Path.Combine(StorageUtils.GetCacheDirectory(), "tool-workspace.txt");
        if (Workspace == null && File.Exists(saved))
        {
            var candidate = File.ReadAllText(saved).Trim();
            if (IsWorkspace(candidate)) Workspace = candidate;
        }
    }

    public static bool IsWorkspace(string path) => File.Exists(Path.Combine(path, "tools", "wizard101_hub.py"));

    internal static string? FindWorkspace(string start)
    {
        for (var directory = new DirectoryInfo(start); directory != null; directory = directory.Parent)
            if (IsWorkspace(directory.FullName)) return directory.FullName;
        return null;
    }

    public void SelectWorkspace(string path)
    {
        if (!IsWorkspace(path)) throw new InvalidOperationException("Choose the AutoWizard101 folder containing the installed tools.");
        lock (_gate)
        {
            if (_running.Count > 0 || _scheduled.Count > 0) throw new InvalidOperationException("Stop the open tools before changing folders.");
            Workspace = Path.GetFullPath(path);
        }
        File.WriteAllText(Path.Combine(StorageUtils.GetCacheDirectory(), "tool-workspace.txt"), Workspace);
    }

    private string HubPython()
    {
        if (Workspace == null) throw new InvalidOperationException("Choose your AutoWizard101 folder first.");
        var path = Path.Combine(Workspace, ".local", "envs", "companion", "Scripts", "python.exe");
        if (!File.Exists(path)) throw new InvalidOperationException("Tool setup is missing. Choose the AutoWizard101 folder where the tools were installed.");
        return path;
    }

    internal static ProcessStartInfo BuildStartInfo(string python, string workspace, string script, IEnumerable<string> arguments)
    {
        var info = new ProcessStartInfo(python)
        {
            WorkingDirectory = workspace,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        info.ArgumentList.Add(script);
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        info.Environment["PYTHONUTF8"] = "1";
        info.Environment["PYTHONNOUSERSITE"] = "1";
        info.Environment.Remove("PYTHONPATH");
        return info;
    }

    private ProcessStartInfo HubInfo(params string[] arguments) => BuildStartInfo(
        HubPython(), Workspace!, Path.Combine(Workspace!, "tools", "wizard101_hub.py"), arguments);

    public async Task<IReadOnlyList<ToolReadiness>> CheckAsync(CancellationToken cancellationToken = default)
    {
        var output = await RunOperationAsync(HubInfo("status"), cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<List<ToolReadiness>>(output) ?? new List<ToolReadiness>();
    }

    private async Task<string> RunOperationAsync(ProcessStartInfo info, CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _operations.Token);
        return await RunOnceAsync(info, linked.Token).ConfigureAwait(false);
    }

    internal static async Task<string> RunOnceAsync(ProcessStartInfo info, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(120));
        using var process = new Process { StartInfo = info };
        using var job = new OwnedProcessJob();
        process.Start();
        try { job.Attach(process); }
        catch { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
        var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            var result = await stdout.ConfigureAwait(false);
            var error = await stderr.ConfigureAwait(false);
            if (process.ExitCode != 0) throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? "The tool could not complete this action." : error.Trim());
            return result;
        }
        catch
        {
            job.Dispose();
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            throw;
        }
    }

    internal static string? LaunchBlockReason(string tool, bool installed, bool allowLive, bool experimental, bool gameOpen, bool inputBusy)
    {
        if (!installed) return "This tool needs setup. Check the tool folder and try Refresh.";
        if (tool is "wizsdk" or "wizwalker" or "wizdiff") return "This is a supporting library. Use the available tasks instead.";
        if (W101InteractionPolicy.ToolBlockReason(tool) is { } boundary) return boundary;
        if (tool is "dance" or "deimos")
        {
            if (!allowLive || !experimental) return "Turn on Allow game controls before starting a gameplay tool.";
            if (!gameOpen) return "Open Wizard101 and sign in before starting this tool.";
            if (inputBusy) return "Stop the current gameplay task first.";
        }
        if (tool == "wiztype" && !gameOpen) return "Open Wizard101 before checking client types.";
        return null;
    }

    public async Task LaunchAsync(string tool, ToolReadiness readiness, string? nativeBlockReason = null, string? output = null, GuidedActivitySession? guided = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        W101InteractionPolicy.RequireTool(tool);
        if (tool is not ("dance" or "deimos"))
        { await LaunchCoreAsync(tool, readiness, nativeBlockReason, output, guided, cancellationToken).ConfigureAwait(false); return; }
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (HasGameplaySession) throw new InvalidOperationException("Stop the queued or running gameplay routine first.");
            if (nativeBlockReason != null) throw new InvalidOperationException(nativeBlockReason);
            var pins = new Dictionary<string, string>();
            var hub = Path.Combine(Workspace ?? throw new InvalidOperationException("Select the tool workspace first."), "tools", "wizard101_hub.py");
            pins[hub] = GameExperiments.Digest(hub);
            if (guided != null) pins[guided.ProfilePath] = GameExperiments.Digest(guided.ProfilePath);
            var request = new OperationRequest(Guid.NewGuid().ToString("N"), "legacy." + tool, "hub-shared-owner-v1", 1800, true,
                "Wizard101 (validated by guided adapter)", pins, DelegatedInput: true);
            var ticket = ApplicationOperations.For(WorkshopHost.DataRoot(AppContext.BaseDirectory)).Submit(request, async (_, token) =>
            {
                try
                {
                    await LaunchCoreAsync(tool, readiness, nativeBlockReason, output, guided, token).ConfigureAwait(false);
                    Task exit; lock (_gate) exit = _exits[tool];
                    using var registration = token.Register(() => StopProcess(tool));
                    await exit.ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    return new(LastExitCode(tool) == 0 ? OperationState.Unknown : OperationState.Failed,
                        "Legacy worker exited; gameplay outcome requires separate evidence.", guided == null ? [] : [guided.EventsPath]);
                }
                finally { StopProcess(tool); }
            });
            _scheduled.Add(tool, ticket);
            _ = ObserveScheduledAsync(tool, ticket);
            AddMessage(tool + " queued as a bounded routine; the hub shares native input ownership.");
        }
    }
    private async Task ObserveScheduledAsync(string tool, OperationTicket ticket)
    {
        try { var result = await ticket.Completion.ConfigureAwait(false); AddMessage(tool + ": " + result.State + " · " + result.Message); }
        catch (Exception error) { AddMessage(tool + ": receipt failed · " + error.Message); }
        finally { lock (_gate) _scheduled.Remove(tool); }
    }
    private async Task LaunchCoreAsync(string tool, ToolReadiness readiness, string? nativeBlockReason = null, string? output = null, GuidedActivitySession? guided = null, CancellationToken cancellationToken = default)
    {
        var policy = ExecutionPolicyManager.Current;
        var processes = Process.GetProcessesByName("WizardGraphicalClient");
        var gameOpen = processes.Length > 0;
        foreach (var process in processes) process.Dispose();
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var reason = LaunchBlockReason(tool, readiness.Installed, policy.AllowLiveAutomation,
                policy.Profile == ExecutionProfile.ExperimentalSimulation, gameOpen, _running.ContainsKey("dance") || _running.ContainsKey("deimos") || nativeBlockReason != null);
            if (reason != null) throw new InvalidOperationException(nativeBlockReason ?? reason);
            if (guided != null && processes.Length != 1) throw new InvalidOperationException("Open exactly one Wizard101 client for guided activities.");
            if (_running.ContainsKey(tool)) throw new InvalidOperationException("This tool is already open.");
            var arguments = new List<string> { "launch", tool, "--execute", "--policy", ExecutionPolicyManager.PolicyPath,
                "--snapshot", Path.Combine(StorageUtils.GetCacheDirectory(), "snapshot.json") };
            if (output != null) arguments.AddRange(new[] { "--output", output });
            if (guided != null) arguments.AddRange(new[] { "--profile", guided.ProfilePath, "--events", guided.EventsPath, "--control", guided.ControlPath });
            var process = new Process { StartInfo = HubInfo(arguments.ToArray()) };
            var job = new OwnedProcessJob();
            process.OutputDataReceived += (_, e) => { if (e.Data != null) AddMessage(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) AddMessage(e.Data); };
            try { process.Start(); job.Attach(process); }
            catch
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                process.Dispose();
                job.Dispose();
                throw;
            }
            process.Exited += (_, _) => job.Dispose();
            process.EnableRaisingEvents = true;
            _exitCodes.Remove(tool);
            _stopping.Remove(tool);
            _running.Add(tool, process);
            _jobs.Add(tool, job);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            _exits[tool] = ObserveExitAsync(tool, process);
        }
        await Task.CompletedTask;
    }

    private async Task ObserveExitAsync(string tool, Process process)
    {
        await process.WaitForExitAsync().ConfigureAwait(false);
        AddMessage($"{tool} closed (code {process.ExitCode}).");
        lock (_gate)
        {
            _exitCodes[tool] = _stopping.Remove(tool) ? 0 : process.ExitCode;
            if (_running.TryGetValue(tool, out var current) && ReferenceEquals(current, process)) _running.Remove(tool);
            if (_jobs.Remove(tool, out var job)) job.Dispose();
        }
        process.Dispose();
    }

    private void AddMessage(string message)
    {
        lock (_gate)
        {
            _messages.Add(message.Length > 2000 ? message[..2000] : message);
            if (_messages.Count > 100) _messages.RemoveAt(0);
        }
    }

    public void Stop(string tool)
    {
        OperationTicket? ticket; lock (_gate) _scheduled.TryGetValue(tool, out ticket);
        ticket?.Cancel(); StopProcess(tool);
    }
    private void StopProcess(string tool)
    {
        lock (_gate)
        {
            if (_running.ContainsKey(tool))
            {
                _stopping.Add(tool);
                if (_jobs.TryGetValue(tool, out var job)) job.Dispose();
            }
        }
    }

    public void StopGameplay() { Stop("dance"); Stop("deimos"); }

    public Task LaunchObserverAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (Workspace == null) throw new InvalidOperationException("Choose your AutoWizard101 folder first.");
            if (_running.ContainsKey("diego-observer")) throw new InvalidOperationException("The Diego observer is already open.");
            var python = Path.Combine(Workspace, ".local", "envs", "dance", "Scripts", "python.exe");
            var script = Path.Combine(Workspace, "tools", "diego_observer.py");
            if (!File.Exists(python) || !File.Exists(script)) throw new InvalidOperationException("The observer or isolated vision environment is missing. Restore the tools setup first.");
            var process = new Process { StartInfo = BuildStartInfo(python, Workspace, script, Array.Empty<string>()) };
            var job = new OwnedProcessJob();
            process.OutputDataReceived += (_, e) => { if (e.Data != null) AddMessage(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) AddMessage(e.Data); };
            var started = false;
            try { started = process.Start(); job.Attach(process); }
            catch
            {
                if (started && !process.HasExited) process.Kill(entireProcessTree: true);
                process.Dispose();
                job.Dispose();
                throw;
            }
            process.Exited += (_, _) => job.Dispose();
            process.EnableRaisingEvents = true;
            _exitCodes.Remove("diego-observer");
            _stopping.Remove("diego-observer");
            _running.Add("diego-observer", process);
            _jobs.Add("diego-observer", job);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            _ = ObserveExitAsync("diego-observer", process);
        }
        return Task.CompletedTask;
    }

    public async Task CheckActivityAsync(ActivityProfile profile, CancellationToken cancellationToken = default, bool probe = false)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (probe) W101InteractionPolicy.RequireTool(profile.Tool);
        if (probe && HasGameplaySession) throw new InvalidOperationException("Stop the current gameplay helper before checking another connection.");
        var folder = Path.Combine(StorageUtils.GetCacheDirectory(), "activities");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "check-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            profile.Save(path);
            var arguments = new List<string> { "activity-check", "--profile", path };
            if (probe) arguments.Add("--probe");
            await RunOperationAsync(HubInfo(arguments.ToArray()), cancellationToken).ConfigureAwait(false);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    public async Task LaunchGuidedAsync(ActivityProfile profile, ToolReadiness readiness, string? nativeBlockReason, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        W101InteractionPolicy.RequireTool(profile.Tool);
        await CheckActivityAsync(profile, cancellationToken).ConfigureAwait(false);
        var folder = Path.Combine(StorageUtils.GetCacheDirectory(), "activities", "runs", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "profile.json");
        profile.Save(path);
        var session = new GuidedActivitySession(profile.Tool, path, Path.Combine(folder, "progress.json"), Path.Combine(folder, "stop.request"), profile);
        cancellationToken.ThrowIfCancellationRequested();
        GuidedActivity = session;
        await LaunchAsync(profile.Tool, readiness, nativeBlockReason, guided: session, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public ActivityProgress? ReadActivityProgress()
    {
        var session = GuidedActivity;
        if (session == null) return null;
        ActivityProgress progress = new();
        try
        {
            if (File.Exists(session.EventsPath))
            {
                using var stream = new FileStream(session.EventsPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                progress = JsonSerializer.Deserialize<ActivityProgress>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? progress;
            }
        }
        catch (IOException) { }
        catch (JsonException) { }
        if (!IsRunning(session.Tool) && progress.Phase is not ("completed" or "stopped" or "failed"))
        {
            progress.Phase = LastExitCode(session.Tool) is int code && code != 0 ? "failed" : "stopped";
            progress.Message = progress.Phase == "failed" ? "The backend closed with an error. See Setup & details." : "The backend closed; no activity completion is assumed.";
        }
        return progress;
    }

    public async Task StopGuidedAsync()
    {
        var session = GuidedActivity;
        if (session == null || !IsRunning(session.Tool)) return;
        try { File.WriteAllText(session.ControlPath, "stop"); }
        catch (IOException) { Stop(session.Tool); return; }
        catch (UnauthorizedAccessException) { Stop(session.Tool); return; }
        for (var attempt = 0; attempt < 80 && IsRunning(session.Tool); attempt++) await Task.Delay(100).ConfigureAwait(false);
        if (IsRunning(session.Tool))
        {
            AddMessage("The activity did not stop in time; closing its owned process group.");
            Stop(session.Tool);
        }
    }

    public static int GameClientCount()
    {
        var processes = Process.GetProcessesByName("WizardGraphicalClient");
        var count = processes.Length;
        foreach (var process in processes) process.Dispose();
        return count;
    }

    public async Task<int> UpdateBossLibraryAsync()
    {
        HubPython();
        var source = Path.Combine(Workspace!, ".local", "upstream", "wizard101-companion");
        var database = Path.Combine(source, "boss_wiki.db");
        if (!File.Exists(database)) database = Path.Combine(source, "templates", "boss_wiki.db");
        var destination = Path.Combine(StorageUtils.GetScriptLibraryPath(), "WizWikiAPI-main", "wizwiki_companion.json");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await RunOperationAsync(HubInfo("export-companion", database, "--output", temporary)).ConfigureAwait(false);
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(temporary).ConfigureAwait(false));
            var count = document.RootElement.GetArrayLength();
            File.Move(temporary, destination, overwrite: true);
            WizWikiDataService.Instance.Refresh();
            return count;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public Task<string> CompareAsync(bool patches, string before, string after, string output)
    {
        if (!patches) return RunOperationAsync(HubInfo("compare-types", before, after, "--output", output));
        HubPython();
        var python = Path.Combine(Workspace!, ".local", "envs", "wizdiff", "Scripts", "python.exe");
        return RunOperationAsync(BuildStartInfo(python, Workspace!, Path.Combine(Workspace!, "tools", "wizdiff_adapter.py"), new[] { before, after, "--output", output }));
    }

    public Task<string> DecodeAsync(string definitions, string capture, string output) => RunOperationAsync(
        HubInfo("launch", "moonlight", "--definitions", definitions, "--capture", capture, "--output", output, "--execute"));

    public void StopAll()
    {
        if (_disposed) return;
        _operations.Cancel();
        _operations.Dispose();
        _operations = new CancellationTokenSource();
        OperationTicket[] scheduled; lock (_gate) scheduled = _scheduled.Values.ToArray();
        foreach (var ticket in scheduled) ticket.Cancel();
        lock (_gate)
        {
            foreach (var tool in _running.Keys)
            {
                _stopping.Add(tool);
                if (_jobs.TryGetValue(tool, out var job)) job.Dispose();
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        StopAll();
        _disposed = true;
        _operations.Dispose();
    }
}
