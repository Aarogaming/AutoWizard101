using System.Security.Cryptography;

namespace ProjectMaelstrom.Utilities;

internal sealed record HomeSample(Bitmap Image, double CapturedMs) : IDisposable
{
    public void Dispose() => Image.Dispose();
}
internal interface IHomeMovementRuntime
{
    HomeMovementSignal Signal { get; }
    bool Permitted(HomeMovementGate gate);
    Task<HomeSample?> CaptureAsync(double? afterMs, CancellationToken token);
    Task<DodgePulseResult> PulseAsync(string key, int milliseconds, double capturedMs, HomeMovementGate gate, CancellationToken token);
    Task DelayAsync(int milliseconds, CancellationToken token);
}
internal sealed record HomeMovementRunResult(HomeMovementState State, string Reason, int CompletedCycles, string Report,
    long? ActivityBaseline = null);

/// <summary>Optional bounded home steps. Evidence distinguishes accepted input from a visual scene change.</summary>
internal static class HomeMovementRun
{
    public static async Task<HomeMovementRunResult> RunAsync(IHomeMovementRuntime runtime, GameWindow target,
        HomeReference reference, HomeMovementOptions options, string dataRoot, bool homeConfirmed, IProgress<string>? progress, CancellationToken token)
    {
        options.Validate();
        if (options.Mode == HomeIdleMode.NativeIdle) throw new InvalidOperationException("Native Idle is controlled by W101's Escape → Idle button; Maelstrom sends no recurring input in that mode.");
        if (!homeConfirmed || !HomeMovementGate.IsWizard101(target) || !reference.Usable) throw new InvalidOperationException("Select Wizard101, capture a usable reference, and confirm the clear test area first.");
        var folder = Path.Combine(dataRoot, "home-movement", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(folder);
        var gate = new HomeMovementGate(options, runtime.Signal.NowMs);
        var events = new List<object>();
        var checks = new List<object>();
        var expectedView = reference.View;
        var started = DateTime.UtcNow;
        var imageHashes = new Dictionary<string, string>();
        void Save(Bitmap image, string name)
        {
            var path = Path.Combine(folder, name); image.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            imageHashes[name] = GameExperiments.Digest(path);
        }
        void Persist()
        {
            GameExperiments.WriteAtomic(Path.Combine(folder, "report.json"), new
            {
                schema_version = 1,
                mode = "wizard101-home-movement",
                motion = options.Mode.ToString(),
                started_utc = started,
                updated_utc = DateTime.UtcNow,
                target = new { target.ProcessId, target.StartedUtcTicks, handle = target.Handle.ToInt64(), target.ProcessName },
                options,
                status = gate.State.ToString(),
                reason = gate.Reason,
                completed_cycles = gate.Cycles,
                actions = events,
                visual_checks = checks,
                images = imageHashes,
                reference = new { capture_size = reference.View.Size, client_size = reference.ClientSize, sha256 = Convert.ToHexString(SHA256.HashData(reference.View.Pixels)), user_confirmed_test_area = true, home_required = options.RequiresHome },
                note = "Test area is user-confirmed. Mode and keys are explicit; custom keybinds need independent verification. Single-direction turns keep the original HUD anchors and compare scene changes against the previous verified view. Similarity checks are not semantic home/menu detection. Scene changes are not proof of character motion or prevention of logout. Desktop input contents are not recorded."
            });
        }
        File.WriteAllBytes(Path.Combine(folder, "reference-view.rgb"), reference.View.Pixels);
        Persist(); progress?.Report(gate.Reason);
        try
        {
            while (gate.State is HomeMovementState.Arming or HomeMovementState.Running)
            {
                token.ThrowIfCancellationRequested();
                if (!gate.Evaluate(runtime.Signal))
                {
                    if (gate.State is HomeMovementState.Paused or HomeMovementState.Stopped) break;
                    await runtime.DelayAsync(250, token).ConfigureAwait(false); continue;
                }
                progress?.Report($"Running · {options.Mode} · checking the view before cycle {gate.Cycles + 1}.");
                foreach (var key in options.Keys)
                {
                    using var before = await runtime.CaptureAsync(null, token).ConfigureAwait(false);
                    if (before == null || !DodgeLiveRun.IsFreshCapture(before.CapturedMs, runtime.Signal.NowMs, 200)) { gate.Pause("fresh-game-frame-unavailable"); break; }
                    var view = HomeView.From(before.Image);
                    var sceneReference = options.UsesRollingScene ? new HomeReference(expectedView, reference.ClientSize) : reference;
                    var rejected = reference.RejectHud(view) ?? sceneReference.Reject(view);
                    checks.Add(new { cycle = gate.Cycles + 1, key, phase = "before", capture_ms = before.CapturedMs, initial_reference = reference.Measurements(view), comparison = sceneReference.Measurements(view) });
                    if (rejected != null) { Save(before.Image, "last-changed-view.png"); gate.Pause(rejected); break; }
                    if (!runtime.Permitted(gate)) { gate.Pause("desktop-activity-or-target-change"); break; }
                    // Compare first, then dispatch while fresh; save evidence only after release.
                    if (!DodgeLiveRun.IsFreshCapture(before.CapturedMs, runtime.Signal.NowMs, 200)) { gate.Pause("frame-aged-before-input"); break; }
                    var receipt = await runtime.PulseAsync(key, options.PulseMs, before.CapturedMs, gate, token).ConfigureAwait(false);
                    events.Add(new { cycle = gate.Cycles + 1, key, capture_ms = before.CapturedMs, receipt });
                    Save(before.Image, "last-before.png");
                    if (receipt.Status != "dispatched" || receipt.UpMs == null) { gate.Pause(receipt.Reason ?? receipt.Status); break; }
                    // Observe after each individual key; the second step never assumes the first was safe.
                    using var after = await runtime.CaptureAsync(receipt.UpMs, token).ConfigureAwait(false);
                    if (after == null || after.CapturedMs < receipt.UpMs || !DodgeLiveRun.IsFreshCapture(after.CapturedMs, runtime.Signal.NowMs, 200))
                    { gate.Pause("post-input-frame-unavailable"); break; }
                    var afterView = HomeView.From(after.Image);
                    sceneReference = options.UsesRollingScene ? new HomeReference(view, reference.ClientSize) : reference;
                    rejected = reference.RejectHud(afterView) ?? sceneReference.Reject(afterView);
                    checks.Add(new { cycle = gate.Cycles + 1, key, phase = "after", capture_ms = after.CapturedMs, initial_reference = reference.Measurements(afterView), comparison = sceneReference.Measurements(afterView) });
                    Save(after.Image, "last-after.png");
                    if (!runtime.Permitted(gate)) { gate.Pause("desktop-activity-or-target-change"); break; }
                    if (rejected != null) { gate.Pause(rejected); break; }
                    var difference = view.Difference(afterView, HomeReference.Scene);
                    events.Add(new { cycle = gate.Cycles + 1, key, outcome = difference >= .002 ? "scene-change-observed" : "movement-unconfirmed", scene_difference = difference });
                    if (difference < .002) { gate.Pause("movement-unconfirmed"); break; }
                    expectedView = afterView;
                    File.WriteAllBytes(Path.Combine(folder, "last-expected-view.rgb"), expectedView.Pixels);
                }
                if (gate.State != HomeMovementState.Running) break;
                gate.CompletedPair(runtime.Signal.NowMs); Persist();
                progress?.Report($"Running · {gate.Cycles} cycles · next check in {options.IntervalSeconds} seconds. Desktop activity pauses the routine; Escape pauses too.");
                await runtime.DelayAsync(250, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { gate.Stop(); }
        catch (Exception ex) { gate.Pause("error: " + ex.Message); }
        finally { Persist(); }
        progress?.Report($"{gate.State}: {HomeMovementMessages.Explain(gate.Reason)}");
        return new(gate.State, gate.Reason, gate.Cycles, Path.Combine(folder, "report.json"), gate.ActivityBaseline);
    }
}

internal sealed class WindowsHomeMovementRuntime(GameWindow target, Size clientSize, DesktopActivityMonitor activity, nuint tag,
    Func<bool> enabled, Func<double> messageLoopHeartbeat) : IHomeMovementRuntime
{
    private readonly WindowsDodgeKeySink _sink = new(tag);
    public HomeMovementSignal Signal
    {
        get
        {
            var ready = _sink.Ready(target, clientSize);
            var held = DesktopActivityMonitor.AnyKeyHeld();
            var revision = activity.Revision;
            var lastActivity = activity.LastActivityMs;
            // Sample now after the activity timestamp: input arriving during the OS checks
            // must not look like a broken clock while the user switches to the game.
            var now = FrameTimeline.TimestampMs();
            return new(now, ready, held, revision, lastActivity, activity.EscapeRequested,
                enabled() && activity.Installed && now - messageLoopHeartbeat() < 1500);
        }
    }
    public bool Permitted(HomeMovementGate gate) => gate.PermitsPulse(activity.Revision, _sink.Ready(target, clientSize),
        enabled() && activity.Installed && FrameTimeline.TimestampMs() - messageLoopHeartbeat() < 1500, activity.EscapeRequested);
    public Task<DodgePulseResult> PulseAsync(string key, int milliseconds, double capturedMs, HomeMovementGate gate, CancellationToken token) =>
        new DodgeInputPulse(_sink, () => Permitted(gate)).RunAsync(target, clientSize, key, milliseconds, capturedMs, token);
    public Task DelayAsync(int milliseconds, CancellationToken token) => Task.Delay(milliseconds, token);

    public Task<HomeSample?> CaptureAsync(double? afterMs, CancellationToken token) => CaptureAsync(target, afterMs, token);
    internal static async Task<HomeSample?> CaptureAsync(GameWindow target, double? afterMs, CancellationToken token)
    {
        // Short capture sessions avoid a continuous capture/model workload between movements.
        token.ThrowIfCancellationRequested();
        var observer = new WindowObserver(target);
        try
        {
            var frame = await BoundedWindowCapture.ReadAsync(() => observer.ReadTimestampedAsync(),
                () => observer.DisposeAsync().AsTask(), frame => frame.CaptureTime is { } captured &&
                    (afterMs == null || captured.TotalMilliseconds >= afterMs) &&
                    DodgeLiveRun.IsFreshCapture(captured.TotalMilliseconds, FrameTimeline.TimestampMs(), 150), token).ConfigureAwait(false);
            return frame == null ? null : new(frame.Image, frame.CaptureTime!.Value.TotalMilliseconds);
        }
        catch (WindowCaptureCleanupException error)
        {
            // Home movement also uses this helper outside the Dance scheduler closure.
            try { GameInputLease.Current?.RequireRecovery(error.Message); }
            catch (Exception recoveryError) { throw new WindowCaptureCleanupException(new AggregateException(error, recoveryError)); }
            throw;
        }
    }
}
