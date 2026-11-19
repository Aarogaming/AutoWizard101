using System.Diagnostics;
using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

internal sealed record WizwalkerTarget(long Handle, int ProcessId, long StartedUtcTicks, string ProcessName, string ImagePath, string ImageSha256)
{
    internal GameWindow Window => new((nint)Handle, ProcessId, StartedUtcTicks, ProcessName, "Wizard101");
    internal static WizwalkerTarget FromWindow(GameWindow window)
    {
        if (!GameWindows.IsCurrent(window) || window.ProcessName != "WizardGraphicalClient")
            throw new InvalidOperationException("Select a current Wizard101 window.");
        using var process = Process.GetProcessById(window.ProcessId);
        var image = process.MainModule?.FileName ?? throw new InvalidOperationException("The client image is unavailable.");
        if (new FileInfo(image).Length > 512L * 1024 * 1024) throw new InvalidOperationException("The client image exceeds this adapter's verification budget.");
        return new(window.Handle.ToInt64(), window.ProcessId, window.StartedUtcTicks, window.ProcessName, image, GameExperiments.Digest(image));
    }
    internal bool IsCurrent() => GameWindows.IsCurrent(Window);
}

internal sealed record WizwalkerSession(WizwalkerTarget Target, bool Enabled = false);
internal sealed record WizwalkerLaunch(string Python, string Worker, string SourceRoot)
{
    internal static WizwalkerLaunch FromWorkspace(string workspace) => new(
        Path.Combine(workspace, ".local", "envs", "deimos", "Scripts", "python.exe"),
        Path.Combine(workspace, "tools", "wizwalker_adapter.py"),
        Path.Combine(workspace, "third_party", "wizwalker", "wizwalker-master", "wizwalker"));
}

internal sealed record WizwalkerRequest(int SchemaVersion, string RunId, string Operation, bool Enabled,
    bool NetworkAllowed, WizwalkerTarget Target, string SourceRoot, string SourceSha256, int TimeoutSeconds);
internal sealed record WizwalkerResult(int SchemaVersion, string RunId, string Operation, string SourceSha256,
    WizwalkerTarget Target, string Status, string Code, bool? Loading, long ObservedUtcTicks, int DurationMs, bool CleanupConfirmed);
internal sealed record WizwalkerRuntime(Func<WizwalkerTarget, bool> IsCurrent,
    Func<WizwalkerLaunch, WizwalkerRequest, OperationContext, CancellationToken, Task<WizwalkerResult>> Execute);

/// <summary>Explicit one-shot observation. Uses the scheduler's exclusive control
/// lane even though this first adapter sends no inputs or memory writes. No global
/// legacy-policy switch, auto-discovery/attachment, retry or background loop.</summary>
internal static class WizwalkerOperation
{
    internal const string Id = "w101.wizwalker-observe";
    internal const string Version = "wizwalker-loading-v1";
    internal const string SourceSha256 = "99f0eec7e15da862893bcf6aa3d89c3d1538c0a022c04f198d490cf383cdb2d1";
    internal const string WorkerSha256 = "70f38c0f875bce43d78f2b94e7933cf27a9051eaadad2a73f7743a3dd23f2d19";
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly WizwalkerRuntime Runtime = new(target => target.IsCurrent(), WizwalkerWorker.ExecuteAsync);

    internal static OperationTicket Submit(OperationScheduler scheduler, WizwalkerSession session, WizwalkerLaunch launch,
        WizwalkerRuntime? runtime = null)
    {
        var request = Prepare(session, launch, runtime, Id, Version);
        return scheduler.Submit(request, (context, token) => ReadOwnedAsync(context, session.Target, launch, runtime, token));
    }

    internal static OperationRequest Prepare(WizwalkerSession session, WizwalkerLaunch launch, WizwalkerRuntime? runtime,
        string operationId, string operationVersion, string workerSha256 = WorkerSha256)
    {
        if (!session.Enabled) throw new InvalidOperationException("Enable the optional client observation for this check first.");
        if (runtime is null && workerSha256 == W101UiTreeOperation.WorkerSha256 &&
            !W101UiTreeOperation.SupportsClientImage(session.Target.ImageSha256))
            throw new InvalidOperationException("The restarted W101 build needs UI-adapter compatibility review. No hook, focus change or input was attempted. Visual-only operation remains separate.");
        if (runtime is null && !GameExperiments.Digest(launch.Worker).Equals(workerSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The reviewed client adapter changed. Use the adapter files matching this Maelstrom build.");
        runtime ??= Runtime;
        var target = session.Target;
        if (target.Handle <= 0 || target.ProcessId <= 0 || target.StartedUtcTicks <= 0 || target.ProcessName != "WizardGraphicalClient" ||
            !Path.IsPathFullyQualified(target.ImagePath) || !DigestValid(target.ImageSha256) || !runtime.IsCurrent(target))
            throw new InvalidOperationException("Select the current Wizard101 process instance.");
        foreach (var path in new[] { launch.Python, launch.Worker, launch.SourceRoot })
            if (!Path.IsPathFullyQualified(path)) throw new InvalidDataException("Use the configured local adapter paths.");
        if (!Directory.Exists(launch.SourceRoot)) throw new InvalidDataException("The reviewed wizwalker source is missing.");
        var pins = new Dictionary<string, string>();
        foreach (var path in new[] { launch.Python, launch.Worker })
            pins[Path.GetFullPath(path)] = GameExperiments.Digest(path);
        pins[Path.GetFullPath(target.ImagePath)] = target.ImageSha256;
        return new OperationRequest(Guid.NewGuid().ToString("N"), operationId, operationVersion, 25, true,
            $"Wizard101 PID {target.ProcessId}; started {target.StartedUtcTicks}; HWND {target.Handle}", pins);
    }

    /// <summary>Observe under an existing scheduler lease. A caller must use a new
    /// evidence directory for each read. No second scheduler or lease is acquired.</summary>
    internal static async Task<OperationOutcome> ReadOwnedAsync(OperationContext context, WizwalkerTarget target,
        WizwalkerLaunch launch, WizwalkerRuntime? runtime, CancellationToken token)
    {
        if (!context.Request.WritesGame || context.Request.DelegatedInput || context.Input is not { CanDispatch: true } ||
            !ReferenceEquals(context.Input, GameInputLease.Current))
            throw new InvalidOperationException("Client observation requires the current scheduler control owner.");
        if (runtime is null && !GameExperiments.Digest(launch.Worker).Equals(WorkerSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The reviewed client adapter changed.");
        var services = runtime ?? Runtime;
        var frozen = new WizwalkerRequest(1, context.Request.Id, Version, true, false, target, launch.SourceRoot, SourceSha256, 8);
        token.ThrowIfCancellationRequested();
        if (context.Input?.CanDispatch != true || !services.IsCurrent(target))
            throw new InvalidOperationException("The selected client or control ownership changed before connection.");
        var evidence = Path.Combine(context.Directory, "adapter-request.json");
        GameExperiments.WriteAtomic(evidence, frozen);
        WizwalkerResult result;
        try { result = await services.Execute(launch, frozen, context, token).ConfigureAwait(false); }
        catch
        {
            // A worker exception must not silently certify child shutdown.
            context.Input.RequireRecovery("Wizwalker worker did not return verified disconnect evidence.");
            throw;
        }
        if (!result.CleanupConfirmed || result.RunId != context.Request.Id || result.Target != target ||
            result.Operation != Version || result.SourceSha256 != SourceSha256 || result.SchemaVersion != 1 ||
            result.Status is not ("observed" or "unavailable" or "cancelled"))
        {
            // Latch before optional report I/O; a failed evidence save cannot
            // turn an uncertain child disconnect into a clean control guard.
            context.Input.RequireRecovery("Wizwalker disconnect or result identity is unconfirmed.");
            return new(OperationState.CleanupFailed, "Client disconnect or result identity is unconfirmed. Review recovery before another run.", [evidence]);
        }
        var report = Path.Combine(context.Directory, "adapter-result.json");
        GameExperiments.WriteAtomic(report, result);
        if (result.Status == "cancelled") return new(OperationState.Cancelled, "Client check stopped; nothing was replayed.", [evidence, report]);
        if (result.Status == "unavailable" && result.Code == "dependency-unavailable")
            return new(OperationState.Failed, "The optional adapter's Python dependencies are unavailable. Repair its isolated environment before retrying. No client was opened.", [evidence, report]);
        if (result.Status != "observed" || result.Loading is null || !services.IsCurrent(target) ||
            result.ObservedUtcTicks > DateTime.UtcNow.Ticks || DateTime.UtcNow.Ticks - result.ObservedUtcTicks > TimeSpan.FromSeconds(5).Ticks ||
            result.DurationMs is < 0 or > 15000)
            return new(OperationState.Failed, "No current loading observation was verified. Visual observation remains available.", [evidence, report]);
        return OperationOutcome.Completed(result.Loading.Value ? "Client reports loading. This is a single observation, not a gameplay outcome." :
            "Client reports not loading. This does not prove the wizard is ready to play.", evidence, report);
    }

    private static bool DigestValid(string value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
}
