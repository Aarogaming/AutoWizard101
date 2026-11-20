using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace ProjectMaelstrom.Utilities;

/// <summary>Bounded readiness diagnostics contain identities, sizes and activity
/// counters only; no window title, captured text or user key identities.</summary>
internal sealed record W101ControlReadiness(string? AdmissionFailure, bool TargetCurrent, bool Minimized,
    long TargetHandle, long ForegroundHandle, Size ExpectedGeometry, Size? ActualGeometry,
    bool MonitorInstalled, bool Cancelled, bool Stopping, bool EscapeRequested,
    long? ActivityRevision, long Baseline, double NowMs, double HeartbeatMs, string? InspectionFailure = null, bool OperatorPaused = false, bool OperatorStopped = false)
{
    internal string Code => OperatorStopped ? "operator-stopped" : Cancelled ? "cancelled" : Stopping ? "scope-stopping" :
        AdmissionFailure ?? (InspectionFailure is not null ? "target-inspection-failed" :
        !TargetCurrent ? "target-instance-changed" : Minimized ? "target-minimized" :
        !MonitorInstalled ? "activity-monitor-unavailable" : EscapeRequested ? "escape-takeover" :
        ActivityRevision != Baseline ? "desktop-activity-takeover" : ForegroundHandle != TargetHandle ? "target-not-foreground" :
        ActualGeometry != ExpectedGeometry ? "client-geometry-changed" :
        !double.IsFinite(NowMs) || !double.IsFinite(HeartbeatMs) ? "invalid-heartbeat-clock" :
        NowMs < HeartbeatMs ? "future-heartbeat" : NowMs - HeartbeatMs > 1500 ? "message-loop-stalled" : OperatorPaused ? "operator-paused" : "ready");
    internal bool Ready => Code == "ready";
    internal string Describe()
    {
        var detail = FormattableString.Invariant(
            $"{Code}; owner={AdmissionFailure ?? "valid"}; target={TargetHandle}; current={TargetCurrent}; foreground={ForegroundHandle}; minimized={Minimized}; expected={ExpectedGeometry.Width}x{ExpectedGeometry.Height}; actual={(ActualGeometry is { } size ? size.Width + "x" + size.Height : "unknown")}; monitor={MonitorInstalled}; revision={ActivityRevision}; baseline={Baseline}; cancelled={Cancelled}; stopping={Stopping}; escape={EscapeRequested}; heartbeatAgeMs={NowMs - HeartbeatMs:F3}; inspection={InspectionFailure ?? "none"}");
        return detail.Length <= 512 ? detail : detail[..512];
    }
}

/// <summary>A short-lived foreground control scope for an already admitted W101
/// operation. The private input tag exempts only this run's injected input from
/// takeover detection. It never acquires ownership. Explicit initial quiet
/// admission may request foreground once; ongoing takeover is never overridden.
/// Explicit quiet recovery can rearm activity only between released stages.
/// Hooks live on one hidden STA message-loop thread.</summary>
internal sealed class W101ControlScope : IAsyncDisposable
{
    private const uint Quit = 0x0012;
    private readonly OperationContext _context;
    private readonly GameWindow _target;
    private readonly Size _geometry;
    private readonly CancellationToken _token;
    private readonly bool _keyboardOnly;
    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Thread _thread;
    private CancellationTokenRegistration _cancel;
    private DesktopActivityMonitor? _monitor;
    private W101RunningNotice? _runningNotice;
    private readonly W101RoutineControl _operator = new();
    private long _baseline;
    private double _heartbeat;
    private uint _threadId;
    private int _stopRequested;
    internal nuint Tag { get; }

    private W101ControlScope(OperationContext context, GameWindow target, Size geometry, CancellationToken token, bool keyboardOnly)
    {
        _keyboardOnly = keyboardOnly;
        _context = context; _target = target; _geometry = geometry; _token = token;
        Tag = (nuint)RandomNumberGenerator.GetInt32(1, int.MaxValue);
        _thread = new Thread(Pump) { IsBackground = true, Name = "Maelstrom W101 control scope" };
        _thread.SetApartmentState(ApartmentState.STA);
    }

    internal static async Task<W101ControlScope> CreateAsync(OperationContext context, GameWindow target,
        Size clientGeometry, CancellationToken token, bool activateWhenQuiet = false, bool resumable = false)
    {
        if (AdmissionFailure(context, target, clientGeometry) is { } rejected)
            throw new InvalidOperationException("W101 control admission rejected: " + rejected);
        token.ThrowIfCancellationRequested();
        var dataRoot = Directory.GetParent(context.Directory)?.Parent?.FullName
            ?? throw new InvalidOperationException("Operation settings directory unavailable.");
        var preferences = AppPreferences.Load(dataRoot);
        var scope = new W101ControlScope(context, target, clientGeometry, token, true);
        scope._thread.Start();
        scope._cancel = token.Register(scope.RequestStop);
        try
        {
            await scope._started.Task.WaitAsync(TimeSpan.FromSeconds(3), token).ConfigureAwait(false);
            if (activateWhenQuiet && scope.InspectReadiness().Code == "target-not-foreground")
                await scope.ActivateAfterQuietAsync(token).ConfigureAwait(false);
            if (scope.InspectReadiness().Code == "target-not-foreground")
                await scope.WaitForFocusAsync(token).ConfigureAwait(false);
            var readiness = scope.InspectReadiness();
            if (!readiness.Ready) throw new InvalidOperationException("W101 control not ready: " + readiness.Describe());
            GameExperiments.WriteAtomic(Path.Combine(context.Directory, "control-policy.json"),
                new { keyboardOnlyTakeover = true, keyboardTakeoverRequiresTargetFocus = true, escapeRequiresTargetFocus = true, foregroundRequired = true });
            await ActionStartNotice.ShowAsync(target, scope.Ready,
                preferences.ActionCountdownSound, token, context.Directory, () => FocusWaitAllowed(scope.InspectReadiness()), () => context.FocusBudget?.Pause()).ConfigureAwait(false);
            if (!scope.Ready()) throw new InvalidOperationException("W101 changed during the start countdown.");
            if (preferences.W101RunningCaption) scope._runningNotice = await W101RunningNotice.StartAsync(target, () => scope._operator.Stopped ? "Stopping" : scope.Ready() ? "Running" : "Paused", token,
                resumable ? scope._operator.Resume : null, resumable ? scope._operator.Pause : null, scope._operator.Stop).ConfigureAwait(false);
            return scope;
        }
        catch
        {
            await scope.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static string? AdmissionFailure(OperationContext context, GameWindow target, Size geometry) =>
        !context.Request.WritesGame ? "request-does-not-own-control" : context.Request.DelegatedInput ? "delegated-input-not-supported" :
        context.Request.Target != "pid-" + target.ProcessId + "-start-" + target.StartedUtcTicks ? "request-target-mismatch" :
        target.ProcessName != "WizardGraphicalClient" || target.Handle == 0 || target.ProcessId <= 0 || target.StartedUtcTicks <= 0 ? "invalid-target-identity" :
        !ModuleRecognition.ValidGeometry(geometry) ? "invalid-client-geometry" :
        context.Input is not { CanDispatch: true } ? "input-owner-unavailable" :
        !ReferenceEquals(context.Input, GameInputLease.Current) ? "input-owner-scope-mismatch" : null;

    internal bool Ready() => InspectReadiness().Ready;
    internal string ReadinessReason => InspectReadiness().Describe();

    internal static bool FocusWaitAllowed(W101ControlReadiness state) => state.Code is "ready" or "target-not-foreground" or "operator-paused";

    // Only call after input release; never replace the immediate Ready input guard.
    internal async Task<bool> WaitForFocusAsync(CancellationToken token)
    {
        var initial = InspectReadiness();
        if (!FocusWaitAllowed(initial)) return false;
        IDisposable? pause = null;
        var timer = System.Diagnostics.Stopwatch.StartNew();
        double? stableSince = null;
        try
        {
            while (timer.Elapsed.TotalSeconds < 300)
            {
                token.ThrowIfCancellationRequested();
                var state = InspectReadiness();
                if (!FocusWaitAllowed(state)) return false;
                if (!state.Ready) pause ??= _context.FocusBudget?.Pause();
                if (state.Ready && !DesktopActivityMonitor.AnyKeyHeld(true))
                {
                    stableSince ??= timer.Elapsed.TotalMilliseconds;
                    if (timer.Elapsed.TotalMilliseconds - stableSince >= 500) return Ready();
                }
                else stableSince = null;
                await Task.Delay(100, token).ConfigureAwait(false);
            }
            return false;
        }
        finally { pause?.Dispose(); }
    }

    private async Task ActivateAfterQuietAsync(CancellationToken token)
    {
        var gate = new W101ForegroundQuietGate();
        var started = System.Diagnostics.Stopwatch.StartNew();
        bool attempted = false, accepted = false;
        while (started.Elapsed < TimeSpan.FromSeconds(7))
        {
            token.ThrowIfCancellationRequested();
            var state = InspectReadiness();
            if (!W101ForegroundQuietGate.Eligible(state) || DesktopActivityMonitor.AnyKeyHeld(_keyboardOnly)) break;
            if (gate.Observe(state, false))
            {
                // Recheck immediately before the single OS activation request.
                if (!W101ForegroundQuietGate.Eligible(InspectReadiness()) || DesktopActivityMonitor.AnyKeyHeld(_keyboardOnly)) break;
                attempted = true;
                _ = SetForegroundWindow(_target.Handle);
                for (var i = 0; i < 10; i++)
                {
                    await Task.Delay(100, token).ConfigureAwait(false);
                    if (Ready()) { accepted = true; break; }
                    if (!W101ForegroundQuietGate.Eligible(InspectReadiness())) break;
                }
                break;
            }
            await Task.Delay(100, token).ConfigureAwait(false);
        }
        GameExperiments.WriteAtomic(Path.Combine(_context.Directory, "foreground-admission.json"),
            new { attempted, accepted, quietSeconds = 5, readiness = ReadinessReason });
    }

    /// <summary>Explicit opt-in only, called between fully released stages.
    /// Never activates a window, clears Escape, or resets a recovery latch.</summary>
    internal async Task<bool> ResumeAfterQuietAsync(int quietSeconds, CancellationToken token)
    {
        var quiet = new W101QuietPeriod();
        var started = System.Diagnostics.Stopwatch.StartNew();
        while (started.Elapsed < TimeSpan.FromSeconds(12))
        {
            token.ThrowIfCancellationRequested();
            var state = InspectReadiness();
            if (state.Code is not ("ready" or "desktop-activity-takeover" or "target-not-foreground")) return false;
            if (quiet.Observe(state, DesktopActivityMonitor.AnyKeyHeld(_keyboardOnly), quietSeconds))
            {
                var current = InspectReadiness();
                if (current.ActivityRevision != state.ActivityRevision || DesktopActivityMonitor.AnyKeyHeld(_keyboardOnly) ||
                    !(current with { Baseline = current.ActivityRevision ?? -1 }).Ready) return false;
                Interlocked.Exchange(ref _baseline, current.ActivityRevision!.Value);
                return Ready();
            }
            await Task.Delay(200, token).ConfigureAwait(false);
        }
        return false;
    }

    internal W101ControlReadiness InspectReadiness()
    {
        var monitor = Volatile.Read(ref _monitor);
        bool current = false, minimized = false;
        long foreground = 0;
        Size? actual = null;
        string? failure = null;
        try
        {
            foreground = GetForegroundWindow().ToInt64();
            current = GameWindows.IsCurrent(_target);
            if (current)
            {
                minimized = GameWindows.Minimized(_target);
                actual = GameWindows.ClientSize(_target);
            }
        }
        catch (Exception error) { failure = error.GetType().Name; }
        // Read the cross-thread heartbeat BEFORE sampling our clock. Sampling
        // now first can misclassify a concurrent heartbeat update as future time.
        var heartbeat = Volatile.Read(ref _heartbeat);
        var now = FrameTimeline.TimestampMs();
        return new(AdmissionFailure(_context, _target, _geometry), current, minimized,
            _target.Handle.ToInt64(), foreground, _geometry, actual, monitor?.Installed == true,
            _token.IsCancellationRequested, Volatile.Read(ref _stopRequested) != 0, monitor?.EscapeRequested == true,
            monitor?.Revision, _baseline, now, heartbeat, failure, _operator.Paused, _operator.Stopped);
    }

    /// <summary>Pure scope policy: changes to activity can never be acknowledged
    /// away, and the message loop must still be servicing the hooks.</summary>
    internal static bool Allows(bool cancelled, bool stopping, bool installed, bool escape,
        long activity, long baseline, bool targetReady, double now, double heartbeat) =>
        !cancelled && !stopping && installed && !escape && activity == baseline && targetReady &&
        double.IsFinite(now) && double.IsFinite(heartbeat) && now >= heartbeat && now - heartbeat <= 1500;

    private void Pump()
    {
        nuint timer = 0;
        try
        {
            // Establish the queue before publishing the thread ID for shutdown.
            _ = PeekMessageW(out _, 0, 0, 0, 0);
            Volatile.Write(ref _threadId, GetCurrentThreadId());
            if (Volatile.Read(ref _stopRequested) != 0 || _token.IsCancellationRequested)
                throw new OperationCanceledException(_token);
            using var monitor = DesktopActivityMonitor.Install(Tag, _keyboardOnly, _target.Handle);
            _baseline = monitor.Revision;
            Volatile.Write(ref _monitor, monitor);
            if (DesktopActivityMonitor.AnyKeyHeld(_keyboardOnly))
                throw new InvalidOperationException("Release existing desktop keys before control begins.");
            timer = SetTimer(0, 0, 200, 0);
            if (timer == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), "Control heartbeat could not start.");
            Volatile.Write(ref _heartbeat, FrameTimeline.TimestampMs());
            _started.TrySetResult();
            while (Volatile.Read(ref _stopRequested) == 0)
            {
                var result = GetMessageW(out var message, 0, 0, 0);
                if (result == 0) break;
                if (result < 0) throw new Win32Exception(Marshal.GetLastWin32Error(), "Control message loop failed.");
                Volatile.Write(ref _heartbeat, FrameTimeline.TimestampMs());
                _ = TranslateMessage(ref message);
                _ = DispatchMessageW(ref message);
            }
        }
        catch (Exception error)
        {
            _started.TrySetException(error);
            // If startup already completed, the vanished monitor/heartbeat blocks
            // further dispatch; Dispose still waits for thread-side hook removal.
        }
        finally
        {
            if (timer != 0) _ = KillTimer(0, timer);
            Volatile.Write(ref _monitor, null);
            Volatile.Write(ref _threadId, 0);
            _finished.TrySetResult();
        }
    }

    private void RequestStop()
    {
        Interlocked.Exchange(ref _stopRequested, 1);
        var id = Volatile.Read(ref _threadId);
        if (id != 0) _ = PostThreadMessageW(id, Quit, 0, 0);
    }

    public async ValueTask DisposeAsync()
    {
        RequestStop(); _cancel.Dispose();
        try
        {
            try { if (_runningNotice is not null) await _runningNotice.DisposeAsync().ConfigureAwait(false); }
            finally { await _finished.Task.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false); }
        }
        catch
        {
            _context.Input?.RequireRecovery("W101 control resources did not confirm bounded shutdown.");
            throw;
        }
    }

    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Message
    {
        public nint Window; public uint Id; public nuint WParam; public nint LParam;
        public uint Time; public NativePoint Point; public uint Private;
    }
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")] private static extern bool PeekMessageW(out Message message, nint window, uint first, uint last, uint remove);
    [DllImport("user32.dll", SetLastError = true)] private static extern int GetMessageW(out Message message, nint window, uint first, uint last);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref Message message);
    [DllImport("user32.dll")] private static extern nint DispatchMessageW(ref Message message);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool PostThreadMessageW(uint thread, uint message, nuint wParam, nint lParam);
    [DllImport("user32.dll", SetLastError = true)] private static extern nuint SetTimer(nint window, nuint id, uint milliseconds, nint callback);
    [DllImport("user32.dll")] private static extern bool KillTimer(nint window, nuint id);
}
