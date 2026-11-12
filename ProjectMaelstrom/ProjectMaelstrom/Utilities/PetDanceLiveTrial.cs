using System.Diagnostics;
using System.Text.Json;

namespace ProjectMaelstrom.Utilities;

/// <summary>Only the optional sound trial defers its input baseline until a fresh,
/// quiet Watch. Once armed, the baseline never follows subsequent desktop activity.</summary>
internal sealed class PetDanceTrialArming(bool deferUntilWatch, long initialRevision)
{
    public bool Armed { get; private set; } = !deferUntilWatch;
    public long Baseline { get; private set; } = initialRevision;

    public bool TryArm(DancePhase phase, bool fresh, bool targetReady, bool noKeysHeld,
        double quietMs, long revision)
    {
        if (Armed || phase != DancePhase.Watch || !fresh || !targetReady || !noKeysHeld ||
            !double.IsFinite(quietMs) || quietMs < 500) return false;
        Baseline = revision;
        Armed = true;
        return true;
    }
}

internal enum PetDanceFrameAdmission { Fresh, Wait, Expired }
internal enum PetDanceInputContinuation { Wait, Dispatch, DiscardForWatch }

/// <summary>Discard old/future/buffered frames without consuming a pending key.
/// Fresh source evidence remains mandatory; an armed wait never exceeds five seconds.</summary>
internal static class PetDanceTrialFrameGate
{
    public const double MaximumAgeMs = 150;
    public const double MaximumWaitMs = 5000;

    public static PetDanceFrameAdmission Inspect(double? capturedMs, double lastAcceptedMs, double nowMs,
        double sinceFreshMs, bool armed)
    {
        if (!double.IsFinite(nowMs) || !double.IsFinite(sinceFreshMs) || sinceFreshMs < 0 ||
            !double.IsFinite(lastAcceptedMs) || armed && sinceFreshMs >= MaximumWaitMs)
            return PetDanceFrameAdmission.Expired;
        return capturedMs is { } captured && captured > lastAcceptedMs &&
            DodgeLiveRun.IsFreshCapture(captured, nowMs, MaximumAgeMs)
            ? PetDanceFrameAdmission.Fresh : PetDanceFrameAdmission.Wait;
    }

    public static bool MayDispatch(double capturedMs, double nowMs)
        => DodgeLiveRun.IsFreshCapture(capturedMs, nowMs, MaximumAgeMs);

    public static PetDanceInputContinuation Pending(DancePhase phase, bool fresh, bool gameplayAllowed)
        => !fresh ? PetDanceInputContinuation.Wait : phase == DancePhase.Watch ? PetDanceInputContinuation.DiscardForWatch :
            phase == DancePhase.Go && gameplayAllowed ? PetDanceInputContinuation.Dispatch : PetDanceInputContinuation.Wait;
}

/// <summary>Release evidence is independent of the later supervised Idle check.
/// Uncertain or malformed pulse outcomes latch uncertainty for this visit.</summary>
internal sealed class PetDanceTrialReleaseEvidence
{
    public bool Confirmed { get; private set; } = true;

    public void Record(DodgePulseResult receipt)
    {
        var knownNoInput = receipt.DownMs == null && receipt.UpMs == null &&
            receipt.Status is "rejected" or "busy" or "invalid-action";
        var knownReleased = receipt.DownMs is { } down && receipt.UpMs is { } up &&
            double.IsFinite(down) && double.IsFinite(up) && down >= 0 && up >= down &&
            receipt.Status is "dispatched" or "interrupted" or "release-retried";
        Confirmed &= knownNoInput || knownReleased;
    }

    public void MarkUncertain() => Confirmed = false;
}

internal static class PetDanceTrialCleanupPolicy
{
    /// <summary>A missing game-state confirmation is a failed requirement, not
    /// evidence of a held key when input and capture cleanup are independently known.</summary>
    public static OperationOutcome MissingIdle(bool workerCleanupConfirmed, bool inputReleaseConfirmed,
        string detail, params string[] artifacts)
    {
        var released = workerCleanupConfirmed && inputReleaseConfirmed;
        return new(released ? OperationState.Failed : OperationState.CleanupFailed,
            released ? "Native Idle unconfirmed; input released and capture stopped. " + detail :
                "Native Idle unconfirmed and input/worker cleanup remains uncertain. " + detail, artifacts);
    }
}

/// <summary>One-game trial with optional verified postgame UI cleanup. Shared input
/// ownership is held through automatic or supervised native Idle confirmation.</summary>
internal static class PetDanceLiveTrial
{
    private sealed record TrialReport(string Path, bool CleanupConfirmed, bool InputReleaseConfirmed,
        long ActivityBaseline, bool MayContinuePostGame);
    private sealed class Host(bool interactive) : Form
    {
        protected override bool ShowWithoutActivation => !interactive;
        protected override CreateParams CreateParams { get { var p = base.CreateParams; if (!interactive) p.ExStyle |= 0x08000000; return p; } }
    }
    public static void Run(nint handle, string profile, string output, string? audioComparisonPath = null, bool interactive = false,
        bool requireControlSetting = false, PetDanceTargetIdentity? expectedTarget = null,
        bool returnToNativeIdle = true, PetDanceArena arena = PetDanceArena.WizardCity, string? clientLoadingWorkspace = null)
    {
        if (!Enum.IsDefined(arena) || audioComparisonPath != null && arena != PetDanceArena.WizardCity)
            throw new InvalidOperationException("Select a supported arena; audio backup is qualified only for Wizard City.");
        if (Directory.Exists(output) || File.Exists(output)) throw new IOException("Choose a new live-trial folder.");
        var target = GameWindows.Read(handle) ?? throw new InvalidOperationException("Wizard101 is unavailable.");
        if (requireControlSetting && expectedTarget == null || expectedTarget != null && !expectedTarget.Matches(target))
            throw new InvalidOperationException("The selected Wizard101 process changed. Refresh and select its window again.");
        if (!HomeMovementGate.IsWizard101(target)) throw new InvalidOperationException("Select Wizard101.");
        using var inspection = new PetDancePanelVision(profile);
        var controlPermission = requireControlSetting ? new PetDanceControlPermission(ExecutionPolicyManager.PolicyPath) : null;
        var pins = new Dictionary<string, string>(inspection.Pins);
        WizwalkerSession? clientSession = null; WizwalkerLaunch? clientLaunch = null;
        if (clientLoadingWorkspace is not null)
        {
            if (!returnToNativeIdle || expectedTarget is null || !Path.IsPathFullyQualified(clientLoadingWorkspace))
                throw new InvalidOperationException("Opt-in client loading requires a selected target and native-Idle continuation.");
            clientSession = new(WizwalkerTarget.FromWindow(target), Enabled: true);
            clientLaunch = WizwalkerLaunch.FromWorkspace(clientLoadingWorkspace);
            foreach (var pin in WizwalkerOperation.Prepare(clientSession, clientLaunch, null, WizwalkerOperation.Id, WizwalkerOperation.Version).Pins)
                pins.Add(pin.Key, pin.Value);
        }
        if (audioComparisonPath != null)
        {
            audioComparisonPath = Path.GetFullPath(audioComparisonPath);
            var audioInspection = PetDanceAudioDetector.LoadFrozenComparison(audioComparisonPath);
            pins[audioComparisonPath] = audioInspection.TemplateSourceSha256!;
        }
        Directory.CreateDirectory(output);
        GameExperiments.WriteAtomic(Path.Combine(output, "routine-settings.json"), new
        { arena = arena.ToString(), arena_selection = "user-selected-in-game-not-automatically-verified", return_to_native_idle = returnToNativeIdle,
            feeding_enabled = false, crown_shop_locked = true, resolution_qualification = "1280x720-client",
            optional_client_loading = clientSession is not null, client_loading_scope = "shared-postgame-modules-only" });
        using var host = new Host(interactive)
        {
            Text = "Maelstrom · Dance trial",
            ShowInTaskbar = interactive,
            StartPosition = interactive ? FormStartPosition.CenterScreen : FormStartPosition.Manual,
            ClientSize = new(480, 240),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false
        };
        if (!interactive) host.Location = new(-30000, -30000);
        var startButton = new Button { Text = "Start", Bounds = new(20, 182, 110, 34) };
        var stopButton = new Button { Text = "Stop", Bounds = new(140, 182, 110, 34), Enabled = false };
        var idleButton = new Button { Text = "Confirm native Idle", Bounds = new(260, 182, 200, 34), Enabled = false };
        var statusLabel = new Label { Bounds = new(20, 20, 440, 150), Text = "Ready. Start, then use Wizard101's menu to begin Dance.\nThe teacher waits for Watch and a quiet desktop before arming.\nAny desktop input after arming stops it. Volume stays unchanged." };
        if (interactive) host.Controls.AddRange([statusLabel, startButton, stopButton, idleButton]);
        startButton.Click += (_, _) =>
        {
            if (controlPermission != null && !controlPermission.IsAllowed())
            {
                statusLabel.Text = "Game controls are off. Enable them in Maelstrom Tools before starting this player.";
                return;
            }
            File.WriteAllText(Path.Combine(output, "start.request"), "Explicit visible Start button.");
            startButton.Enabled = false; stopButton.Enabled = true;
            statusLabel.Text = "Starting observation. Switch to Wizard101 and start Dance.\nLeave keyboard and mouse quiet during Watch.\nThe app will not change focus for you.";
        };
        idleButton.Click += (_, _) =>
        {
            File.WriteAllText(Path.Combine(output, "idle-confirmed.request"), "Supervisor confirmed the visible native Idle banner.");
            idleButton.Enabled = false;
        };
        var allowClose = false;
        host.Shown += async (_, _) =>
        {
            using var cancel = new CancellationTokenSource();
            using var timer = new System.Windows.Forms.Timer { Interval = 50 };
            var heartbeat = FrameTimeline.TimestampMs();
            double idleConfirmationStarted = double.NaN;
            double nextUi = 0;
            stopButton.Click += (_, _) => { File.WriteAllText(Path.Combine(output, "stop.request"), "Explicit visible Stop button."); cancel.Cancel(); stopButton.Enabled = false; };
            host.FormClosing += (_, args) => { if (!allowClose) { args.Cancel = true; cancel.Cancel(); } };
            timer.Tick += (_, _) =>
            {
                var now = FrameTimeline.TimestampMs(); Volatile.Write(ref heartbeat, now);
                if (File.Exists(Path.Combine(output, "stop.request"))) cancel.Cancel();
                if (!interactive || now < nextUi) return;
                nextUi = now + 500;
                if (File.Exists(Path.Combine(output, "teacher-complete.json")))
                {
                    idleButton.Enabled = !File.Exists(Path.Combine(output, "idle-confirmed.request"));
                    var remaining = PetDanceIdleConfirmation.RemainingSeconds(Volatile.Read(ref idleConfirmationStarted), now);
                    statusLabel.Text = $"Teacher stopped. Return Wizard101 to native Idle.\nAfter seeing the Idle banner, click Confirm native Idle.\n{remaining} seconds remain for loading and menu navigation.\nGame input and capture are stopped; this does not claim a win.";
                    return;
                }
                if (File.Exists(Path.Combine(output, "native-idle-started.json")))
                {
                    statusLabel.Text = "Game ended. Returning through the reward and snack menus to native Idle.\nSnacks will not be fed.\nKeep the game focused; any desktop activity stops menu input.";
                    return;
                }
                var progress = Path.Combine(output, "progress.json");
                if (!File.Exists(progress)) return;
                try
                {
                    using var json = JsonDocument.Parse(File.ReadAllBytes(progress));
                    var state = json.RootElement.GetProperty("state").GetString();
                    statusLabel.Text = $"{state}\nGame: {json.RootElement.GetProperty("phase").GetString()}\nArrow attempts: {json.RootElement.GetProperty("keys").GetInt32()}\nDesktop input after arming stops the teacher.";
                    if (json.RootElement.TryGetProperty("sound_sequence_reason", out var soundReason))
                        statusLabel.Text += "\n" + soundReason.GetString();
                }
                catch (Exception error) when (error is IOException or JsonException or KeyNotFoundException) { }
            };
            timer.Start();
            DesktopActivityMonitor? activity = null;
            try
            {
                GameExperiments.WriteAtomic(Path.Combine(output, "standby.json"), new { state = "waiting-for-start", target.ProcessId, maximum_game_seconds = 75, maximum_loading_seconds = 45, maximum_keys = 32, cleanup = "supervised-native-idle", feeding_enabled = false, audio_required = audioComparisonPath != null, visual_sequence_enabled = inspection.UsesShapeSequence, interactive, arming = audioComparisonPath == null && !inspection.UsesShapeSequence ? "immediate-existing-baseline" : "first-fresh-Watch-500ms-quiet-no-held-keys" });
                var waiting = Stopwatch.StartNew();
                while (!File.Exists(Path.Combine(output, "start.request")) && waiting.Elapsed.TotalSeconds < 300) await Task.Delay(100, cancel.Token);
                if (!File.Exists(Path.Combine(output, "start.request"))) throw new TimeoutException("No trial start request.");
                var tag = (nuint)Random.Shared.NextInt64(1, long.MaxValue);
                activity = DesktopActivityMonitor.Install(tag);
                var monitor = activity; var baseline = monitor.Revision; var client = GameWindows.ClientSize(target);
                await using var scheduler = new OperationScheduler(output);
                var request = new OperationRequest(Guid.NewGuid().ToString("N"), "w101.pet-dance-live-trial", "postgame-modules-v7", PetDanceIdleConfirmation.OperationTimeoutSeconds, true, ModuleOwnedInput.TargetId(target), pins);
                var ticket = scheduler.Submit(request, async (context, token) =>
                {
                    var trial = await PlayAsync(target, client, profile, output, monitor, baseline, tag,
                        () => FrameTimeline.TimestampMs() - Volatile.Read(ref heartbeat) < 1500, token, audioComparisonPath,
                        controlPermission == null ? null : controlPermission.IsAllowed).ConfigureAwait(false);
                    var report = trial.Path;
                    if (returnToNativeIdle && trial.MayContinuePostGame && trial.CleanupConfirmed && trial.InputReleaseConfirmed && !token.IsCancellationRequested)
                    {
                        GameExperiments.WriteAtomic(Path.Combine(output, "native-idle-started.json"), new { operation = "w101.return-native-idle", feeding_enabled = false });
                        var idleResult = await PetDanceNativeIdle.RunWithFailsafeAsync(context, target, client, output, monitor, trial.ActivityBaseline, tag,
                            () => FrameTimeline.TimestampMs() - Volatile.Read(ref heartbeat) < 1500,
                            controlPermission == null ? null : controlPermission.IsAllowed, token, clientSession, clientLaunch).ConfigureAwait(false);
                        if (idleResult.Confirmed && idleResult.InputReleased && idleResult.CaptureStopped)
                            return OperationOutcome.Completed("Game ended and native Idle was visually verified by the player operation; inspect gameplay results separately.", report, idleResult.Report);
                        if (!idleResult.InputReleased || !idleResult.CaptureStopped)
                            return new OperationOutcome(OperationState.CleanupFailed, "Postgame input release needs review.", [report, idleResult.Report]);
                    }
                    var cleanupStarted = FrameTimeline.TimestampMs();
                    Volatile.Write(ref idleConfirmationStarted, cleanupStarted);
                    GameExperiments.WriteAtomic(Path.Combine(output, "teacher-complete.json"), new { state = "stopped-awaiting-native-idle", report, inputs_enabled = false, worker_cleanup_confirmed = trial.CleanupConfirmed, input_release_confirmed = trial.InputReleaseConfirmed, idle_confirmation_seconds = PetDanceIdleConfirmation.TimeoutSeconds, idle_confirmation_started_ms = cleanupStarted });
                    while (!File.Exists(Path.Combine(output, "idle-confirmed.request")) && PetDanceIdleConfirmation.RemainingSeconds(cleanupStarted, FrameTimeline.TimestampMs()) > 0 && !token.IsCancellationRequested)
                        await Task.Delay(100).ConfigureAwait(false);
                    if (!File.Exists(Path.Combine(output, "idle-confirmed.request")))
                        return PetDanceTrialCleanupPolicy.MissingIdle(trial.CleanupConfirmed, trial.InputReleaseConfirmed,
                            "The bounded supervised confirmation window ended without an Idle confirmation.", report);
                    HomeSample? idleSample;
                    try { idleSample = await WindowsHomeMovementRuntime.CaptureAsync(target, null, token).ConfigureAwait(false); }
                    catch (WindowCaptureCleanupException error)
                    {
                        return new OperationOutcome(OperationState.CleanupFailed, error.Message, [report]);
                    }
                    catch (OperationCanceledException)
                    {
                        return PetDanceTrialCleanupPolicy.MissingIdle(trial.CleanupConfirmed, trial.InputReleaseConfirmed,
                            "Native Idle confirmation was cancelled; its capture session stopped.", report);
                    }
                    using var idle = idleSample;
                    if (idle == null) return PetDanceTrialCleanupPolicy.MissingIdle(trial.CleanupConfirmed, trial.InputReleaseConfirmed,
                        "Native Idle was reported but fresh confirmation evidence could not be captured.", report);
                    idle.Image.Save(Path.Combine(output, "idle-final.png"));
                    GameExperiments.WriteAtomic(Path.Combine(output, "idle-return.json"), new { confirmed_by = "supervised visual menu check", captured_ms = idle.CapturedMs, sha256 = GameExperiments.Digest(Path.Combine(output, "idle-final.png")), autonomous_idle_recognition = false });
                    if (!trial.CleanupConfirmed || !trial.InputReleaseConfirmed)
                        return new OperationOutcome(OperationState.CleanupFailed, "Native Idle was confirmed but input release or worker cleanup was not; inspect the trial receipt.", [report, Path.Combine(output, "idle-return.json")]);
                    return OperationOutcome.Completed("Trial stopped and supervised native Idle return confirmed; inspect gameplay outcomes separately.", report, Path.Combine(output, "idle-return.json"));
                });
                using var registration = cancel.Token.Register(ticket.Cancel);
                var receipt = await ticket.Completion;
                GameExperiments.WriteAtomic(Path.Combine(output, "finished.json"), receipt);
                Environment.ExitCode = PetDanceIdleConfirmation.ExitCode(receipt.State);
            }
            catch (Exception error)
            {
                Environment.ExitCode = 1;
                GameExperiments.WriteAtomic(Path.Combine(output, "failed.json"), new { error = error.Message, native_idle = "not-confirmed-by-this-run" });
            }
            finally { timer.Stop(); activity?.Dispose(); allowClose = true; host.Close(); }
        };
        Application.Run(host);
    }

    private static async Task<TrialReport> PlayAsync(GameWindow target, Size client, string profile, string output,
        DesktopActivityMonitor monitor, long baseline, nuint tag, Func<bool> heartbeat, CancellationToken token, string? audioComparisonPath,
        Func<bool>? controlPermission = null)
    {
        using var vision = new PetDancePanelVision(profile);
        var visualSequenceEnabled = vision.UsesShapeSequence;
        var audioRequired = audioComparisonPath != null;
        var audioAvailability = new PetDanceAudioAvailability(audioRequired, visualSequenceEnabled);
        double? audioUnavailableAt = null;
        var modernSequence = visualSequenceEnabled || audioRequired;
        var detector = audioRequired ? PetDanceAudioDetector.LoadFrozenComparison(audioComparisonPath!) : null;
        GameAudioRecording? audio = null;
        var audioEvents = new List<PetDanceAudioObservation>(); var audioGate = new object();
        var arming = new PetDanceTrialArming(modernSequence, baseline);
        WindowObserver? observer = null;
        var sourceGeometry = Size.Empty;
        var sink = new WindowsDodgeKeySink(tag);
        PetDanceSelectedResponse? selectedResponse = null;
        bool ObserveAllowed() => monitor.Installed && !monitor.EscapeRequested && heartbeat() &&
            GameWindows.IsCurrent(target) && GameWindows.ClientSize(target) == client && !GameWindows.Minimized(target);
        bool Permitted() => arming.Armed && ObserveAllowed() && monitor.Revision == arming.Baseline && sink.Ready(target, client) &&
            (!audioAvailability.Required || audio?.IsCapturing == true) &&
            (selectedResponse?.Source != "sound-fallback" || audioAvailability.Available && audio?.IsCapturing == true) &&
            (controlPermission?.Invoke() ?? true);
        var releases = new PetDanceTrialReleaseEvidence();
        var sequence = new PetDanceSequence(3); var soundSequence = new PetDanceSoundSequence();
        var visualSequence = new PetDanceVisualSequence();
        var responseWindow = new PetDanceResponseWindow();
        var responseTiming = new List<object>();
        PetDanceVisualSequenceResult? latestVisual = null;
        var pulse = new DodgeInputPulse(sink, Permitted);
        var timer = Stopwatch.StartNew(); var events = new List<object>();
        var receipts = new List<object>(); var outcomes = new List<object>(); var previousOutcome = DancePhase.Unknown;
        var proposals = new List<object>(); var rejectedRounds = new List<object>();
        var frameWaits = new List<object>();
        var pending = new Queue<string>(); double last = -1, nextKey = 0; var keys = 0; var reason = "duration-limit";
        var keyDowns = 0; var deferredInputs = 0;
        var frames = 0; double? first = null; var latencies = new List<double>();
        var clock = new GameplayPauseClock(); var scene = DancePhase.Unknown;
        double lastFrameSeen = 0, nextProgress = 0; var sceneChanges = 0;
        ulong? lastContent = null; var contentChanges = 0; double contentSince = 0;
        double watchStart = -1; var cueCount = 0; var roundRejected = false; var failures = 0; var staleFrames = 0;
        PetDanceSoundSequenceResult? latestAgreement = null;
        string[] currentSoundCues = []; var soundStatus = "waiting-for-Watch"; var soundReason = "No sound sequence has been assessed.";
        var savedVisualRejection = false; var previousSnapshotCue = DanceCue.Unknown;
        var snapshots = new PetDanceSnapshotWriter(); var snapshotsComplete = false;
        var captureCleanup = false; var audioCleanup = !audioRequired; var audioCleanupConfirmed = !audioRequired; string? cleanupError = null;
        void Save(Bitmap image, string name) => snapshots.TrySave(image, Path.Combine(output, name));
        void RejectRound(double time, string why)
        {
            pending.Clear();
            if (!roundRejected) rejectedRounds.Add(new { qpc_ms = time, reason = why, inputs_withheld = true });
            roundRejected = true;
        }
        try
        {
            observer = new WindowObserver(target);
            sourceGeometry = new(observer.Width, observer.Height);
            if (audioRequired)
            {
                var audioDirectory = Path.Combine(output, "audio"); Directory.CreateDirectory(audioDirectory);
                audio = await GameAudioRecording.StartAsync(target.ProcessId, target.StartedUtcTicks, audioDirectory,
                    () => GameWindows.IsCurrent(target), token, packet =>
                    {
                        // The detector has a single writer. Only publishing its
                        // small result list shares a lock with visual observation.
                        var detected = detector!.Push(packet);
                        var processed = FrameTimeline.TimestampMs();
                        lock (audioGate)
                        {
                            if (audioEvents.Count + detected.Count > 5000) throw new InvalidDataException("Audio analysis event budget exhausted.");
                            audioEvents.AddRange(detected.Select(item => item with { ProcessedQpcMs = processed }));
                        }
                    }).ConfigureAwait(false);
            }
            GameExperiments.WriteAtomic(Path.Combine(output, "running.json"), new { state = arming.Armed ? "observing" : "waiting-for-Watch-to-arm", started_utc = DateTime.UtcNow, focus_changes = false, audio_required = audioRequired });
            while (true)
            {
                token.ThrowIfCancellationRequested();
                if (controlPermission != null && !controlPermission()) { reason = "game-controls-disabled"; break; }
                if (audioAvailability.Observe(audio?.IsCapturing == true))
                {
                    audioUnavailableAt = FrameTimeline.TimestampMs();
                    audio?.RequestStop();
                    latestAgreement = null;
                    if (!audioAvailability.MayContinue) { reason = "required-audio-stopped-or-analysis-backlog"; break; }
                    if (selectedResponse?.Source == "sound-fallback")
                    {
                        RejectRound(audioUnavailableAt.Value, "Audio backup stopped; unfinished sound response discarded.");
                        selectedResponse = null;
                    }
                    soundStatus = "audio-unavailable-visual-only";
                    soundReason = "Optional audio stopped; only independently complete visual sequences may respond.";
                    nextProgress = 0;
                }
                if (!ObserveAllowed() || arming.Armed && !Permitted()) { reason = "desktop-activity-or-target-change"; break; }
                using var frame = await PetDanceObservationRead.ReadAsync(() => observer.ReadTimestampedAsync(latestOnly: true), token).ConfigureAwait(false);
                var capturedMs = frame?.CaptureTime?.TotalMilliseconds;
                var ahead = capturedMs.HasValue ? capturedMs.Value - FrameTimeline.TimestampMs() : 0;
                if (ahead is > 0 and <= 30) await Task.Delay((int)Math.Ceiling(ahead), token).ConfigureAwait(false);
                var now = FrameTimeline.TimestampMs();
                if (pending.Count > 0 && responseWindow.Estimate(now, pending.Count, nextKey).Expired)
                    RejectRound(now, "Five-second response window expired; unfinished keys discarded.");
                var frameAdmission = PetDanceTrialFrameGate.Inspect(capturedMs, last, now,
                    timer.Elapsed.TotalMilliseconds - lastFrameSeen, arming.Armed);
                if (frameAdmission != PetDanceFrameAdmission.Fresh)
                {
                    if (capturedMs.HasValue) staleFrames++;
                    if (frameWaits.Count < 128 && capturedMs.HasValue)
                        frameWaits.Add(new { captured_ms = capturedMs, observed_ms = now, pending_keys = pending.Count, armed = arming.Armed, disposition = frameAdmission.ToString(), no_input_sent = true });
                    clock.Advance(timer.Elapsed.TotalMilliseconds, scene == DancePhase.Loading, false);
                    if (clock.StopReason != null) { reason = clock.StopReason; break; }
                    if (frameAdmission == PetDanceFrameAdmission.Expired) { reason = "capture-not-progressing"; break; }
                    if (timer.Elapsed.TotalMilliseconds >= nextProgress)
                    {
                        GameExperiments.WriteAtomic(Path.Combine(output, "progress.json"), new
                        {
                            state = arming.Armed ? "waiting-for-fresh-frame-before-next-input" : "waiting-for-fresh-frame-before-arming",
                            phase = "Unknown",
                            frames,
                            keys,
                            staleFrames,
                            armed = arming.Armed,
                            pending_keys = pending.Count,
                            sound_sequence_status = soundStatus,
                            sound_sequence_reason = soundReason,
                            current_cues = currentSoundCues,
                            no_input_sent_from_discarded_frame = true
                        });
                        nextProgress = timer.Elapsed.TotalMilliseconds + 1000;
                    }
                    await Task.Delay(10, token).ConfigureAwait(false); continue;
                }
                var time = capturedMs!.Value;
                if (frame == null) throw new InvalidDataException("Fresh-frame admission requires an actual frame.");
                last = time;
                if (!first.HasValue) { first = time; audio?.SetVideoOrigin(time, FrameTimeline.TimestampMs()); }
                var compute = Stopwatch.StartNew();
                lastFrameSeen = timer.Elapsed.TotalMilliseconds;
                using var panel = vision.Extract(frame.Image);
                var content = Fingerprint(panel);
                if (lastContent != content) { if (lastContent.HasValue) contentChanges++; lastContent = content; contentSince = timer.Elapsed.TotalMilliseconds; }
                var observation = vision.Read(panel, time);
                responseWindow.Observe(time, observation.Phase);
                var armedNow = !arming.Armed && arming.TryArm(observation.Phase, true, sink.Ready(target, client), !DesktopActivityMonitor.AnyKeyHeld(),
                    FrameTimeline.TimestampMs() - monitor.LastActivityMs, monitor.Revision);
                if (armedNow)
                    GameExperiments.WriteAtomic(Path.Combine(output, "armed.json"), new { armed_qpc_ms = FrameTimeline.TimestampMs(), captured_ms = time, monitor_revision = arming.Baseline, quiet_required_ms = 500, no_focus_change = true });
                clock.Advance(timer.Elapsed.TotalMilliseconds, observation.Phase == DancePhase.Loading,
                    arming.Armed && observation.Phase is (DancePhase.Watch or DancePhase.Go or DancePhase.Resolving or DancePhase.Success or DancePhase.Failure));
                var priorScene = scene;
                if (observation.Phase != scene)
                {
                    scene = observation.Phase;
                    if (sceneChanges < 64) Save(frame.Image, $"scene-{sceneChanges++:00}-{scene}-raw.png");
                }
                if (scene == DancePhase.Watch && arming.Armed && (priorScene != DancePhase.Watch || watchStart < 0))
                { watchStart = time; cueCount = 0; latestAgreement = null; }
                if (timer.Elapsed.TotalMilliseconds >= nextProgress)
                {
                    GameExperiments.WriteAtomic(Path.Combine(output, "progress.json"), new
                    {
                        state = !arming.Armed ? "waiting-for-Watch-and-quiet-desktop" : clock.IsLoading ? "paused-loading" : clock.Started ? "armed-observing-gameplay" : "waiting-for-game",
                        phase = scene.ToString(),
                        clock.ActiveMs,
                        clock.PausedMs,
                        frames,
                        keys,
                        dispatched_keys = keyDowns,
                        deferred_inputs = deferredInputs,
                        armed = arming.Armed,
                        audio_required = audioRequired,
                        audio_required_for_input = audioAvailability.Required,
                        audio_available = audioAvailability.Available,
                        audio_unavailable_at_qpc_ms = audioUnavailableAt,
                        audio_agreement = latestAgreement,
                        visual_sequence = latestVisual,
                        selected_response = selectedResponse,
                        response_deadline_qpc_ms = responseWindow.DeadlineQpcMs,
                        sound_sequence_status = soundStatus,
                        sound_sequence_reason = soundReason,
                        current_cues = currentSoundCues,
                        layout = observation.Layout,
                        arrow_shape = observation.ArrowShape,
                        capture_age_ms = FrameTimeline.TimestampMs() - time,
                        content_changes = contentChanges,
                        unchanged_content_ms = timer.Elapsed.TotalMilliseconds - contentSince,
                        note = "Fresh capture timestamps do not prove that static image content is updating."
                    });
                    nextProgress = timer.Elapsed.TotalMilliseconds + 1000;
                }
                if (clock.StopReason != null) { reason = clock.StopReason; Save(panel, "stopped-panel.png"); Save(frame.Image, "stopped-frame.png"); break; }
                var decision = arming.Armed || observation.Phase is DancePhase.Ready or DancePhase.Loading
                    ? sequence.Observe(time, vision.CanonicalSize, observation.Phase, observation.Cue, observation.FadeOf, observation.Opacity)
                    : new DanceDecision([], [], "Input remains disabled until a fresh Watch and a quiet desktop.", false);
                frames++; latencies.Add(compute.Elapsed.TotalMilliseconds);
                events.Add(new { time_ms = time - first.Value, qpc_ms = time, phase = observation.Phase.ToString(), cue = observation.Cue.ToString(), fade_of = observation.FadeOf.ToString(), observation.Opacity, observation.Error, observation.Margin, observation.PhaseError, observation.CueOffsetX, observation.CueOffsetY, observation.CueAlignmentQualified, observation.Layout, observation.ArrowShape, decision.Cues, decision.Rejected, decision.Reason, armed = arming.Armed, old_visual_sequence_diagnostic_only = modernSequence });
                if (frames == 1) { Save(panel, "first-panel.png"); Save(frame.Image, "first-frame.png"); }
                if (decision.Cues.Length > cueCount) { Save(frame.Image, $"cue-{outcomes.Count + 1}-{decision.Cues.Length}-{observation.Cue}-raw.png"); cueCount = decision.Cues.Length; }
                if (modernSequence && observation.Phase == DancePhase.Watch && observation.Cue != previousSnapshotCue &&
                    observation.Cue is DanceCue.Up or DanceCue.Down or DanceCue.Left or DanceCue.Right)
                    Save(frame.Image, $"sound-visual-{frames:0000}-{observation.Cue}-raw.png");
                previousSnapshotCue = observation.Cue;
                if (decision.Rejected)
                {
                    if (!savedVisualRejection) { Save(panel, $"rejected-{outcomes.Count + 1}-panel.png"); Save(frame.Image, $"rejected-{outcomes.Count + 1}-raw.png"); savedVisualRejection = true; }
                    if (!modernSequence) { reason = decision.Reason; break; }
                    // The legacy parser's frame-by-frame fault remains diagnostic.
                    // It cannot erase the independent sound-led sequence or queued keys.
                }
                if (modernSequence && arming.Armed)
                {
                    PetDanceAudioObservation[] snapshot = [];
                    if (audioAvailability.Available && observation.Phase == DancePhase.Go && soundSequence.NeedsGoAssessment)
                    {
                        // Never block visual observation behind spectral analysis.
                        if (Monitor.TryEnter(audioGate))
                            try { snapshot = audioEvents.ToArray(); }
                            finally { Monitor.Exit(audioGate); }
                    }
                    var soundDecision = audioAvailability.Available ? soundSequence.Observe(new(time, observation.Phase, observation.Cue), snapshot) : null;
                    var visualDecision = visualSequenceEnabled ? visualSequence.Observe(new(time, observation.Phase,
                        observation.Cue, observation.FadeOf, observation.Opacity)) : null;
                    if (visualSequenceEnabled ? visualSequence.NewPreview : soundSequence.NewPreview)
                    {
                        if (pending.Count > 0) RejectRound(time, "New preview discarded the previous round's unfinished response.");
                        pending.Clear(); roundRejected = false; latestAgreement = null; latestVisual = null; selectedResponse = null;
                        currentSoundCues = []; savedVisualRejection = false; nextKey = 0;
                    }
                    if (visualDecision != null || soundDecision != null)
                    {
                        latestAgreement = soundDecision; latestVisual = visualDecision;
                        selectedResponse = audioAvailability.Select(visualDecision, audio?.IsCapturing == true ? soundDecision : null);
                        soundStatus = selectedResponse.Source; soundReason = selectedResponse.Reason;
                        currentSoundCues = selectedResponse.Cues;
                        if (!selectedResponse.Accepted) RejectRound(time, selectedResponse.Reason);
                        var queued = selectedResponse.Accepted && !roundRejected;
                        var actions = selectedResponse.Cues.Select(cue => "dance-" + cue).ToArray();
                        proposals.Add(new
                        {
                            qpc_ms = time,
                            Proposals = actions,
                            audio_required = audioRequired,
                            agreement = soundDecision,
                            visual_sequence = visualDecision,
                            selection = selectedResponse,
                            queued,
                            dispatched = false,
                            expected = visualSequenceEnabled ? visualSequence.Expected : soundSequence.Expected
                        });
                        if (queued)
                        {
                            if (visualSequenceEnabled) visualSequence.NotifyResponseSelected(selectedResponse.Cues.Length);
                            if (audioAvailability.Available) soundSequence.NotifyResponseSelected(selectedResponse.Cues.Length);
                            foreach (var cue in selectedResponse.Cues) pending.Enqueue(cue.ToUpperInvariant());
                        }
                        Save(frame.Image, $"go-{outcomes.Count + 1}-raw.png");
                        nextProgress = 0;
                    }
                }
                else if (!modernSequence && decision.Proposals.Length > 0)
                {
                    proposals.Add(new { qpc_ms = time, decision.Proposals, audio_required = false, queued = true, dispatched = false });
                    foreach (var action in decision.Proposals) pending.Enqueue(action[6..].ToUpperInvariant());
                    Save(frame.Image, $"go-{outcomes.Count + 1}-raw.png");
                }
                if (observation.Phase == DancePhase.Loading)
                {
                    pending.Clear(); previousOutcome = DancePhase.Unknown; watchStart = -1; cueCount = 0; roundRejected = false; latestAgreement = null;
                    currentSoundCues = []; soundStatus = "paused-loading"; soundReason = "Loading invalidated the previous round."; savedVisualRejection = false;
                    await Task.Delay(25, token).ConfigureAwait(false);
                    continue;
                }
                if (observation.Phase == DancePhase.Watch) previousOutcome = DancePhase.Unknown;
                if (observation.Phase is DancePhase.Success or DancePhase.Failure && previousOutcome == DancePhase.Unknown)
                {
                    outcomes.Add(new { time_ms = time - first.Value, outcome = observation.Phase.ToString(), uncompleted_proposed_keys = pending.Count, round_withheld = roundRejected }); previousOutcome = observation.Phase;
                    Save(frame.Image, $"outcome-{outcomes.Count}.png");
                    if (observation.Phase == DancePhase.Failure) failures++;
                    if (pending.Count != 0)
                    {
                        if (!modernSequence) { reason = "result-before-all-proposed-inputs"; break; }
                        RejectRound(time, "Result appeared before all proposed inputs; remaining keys discarded.");
                    }
                    pending.Clear(); roundRejected = false; watchStart = -1; cueCount = 0; latestAgreement = null;
                    currentSoundCues = []; soundStatus = "waiting-for-next-Watch"; soundReason = "Round feedback recorded; waiting for a fresh preview."; savedVisualRejection = false;
                    if (outcomes.Count >= 5) { reason = "five-results-observed"; break; }
                    if (modernSequence && failures >= 3) { reason = "three-failures-observed"; break; }
                }
                var inputContinuation = PetDanceTrialFrameGate.Pending(observation.Phase,
                    PetDanceTrialFrameGate.MayDispatch(time, FrameTimeline.TimestampMs()), clock.GameplayAllowed);
                if (pending.Count > 0 && inputContinuation == PetDanceInputContinuation.DiscardForWatch)
                {
                    RejectRound(time, "Fresh Watch appeared before feedback; old response keys discarded instead of crossing the round boundary.");
                    soundStatus = "old-response-discarded"; soundReason = "A fresh Watch cannot receive the previous round's keys."; nextProgress = 0;
                }
                if (pending.Count > 0 && FrameTimeline.TimestampMs() >= nextKey)
                {
                    var responseEstimate = responseWindow.Estimate(FrameTimeline.TimestampMs(), pending.Count, nextKey);
                    if (!responseEstimate.CanFinish)
                    {
                        responseTiming.Add(new
                        {
                            qpc_ms = FrameTimeline.TimestampMs(),
                            responseWindow.GoQpcMs,
                            responseWindow.DeadlineQpcMs,
                            estimate = responseEstimate,
                            unfinished_keys = pending.Count,
                            status = "remaining-response-cannot-fit"
                        });
                        RejectRound(time, "The remaining response no longer fits within five seconds; wait for the next preview.");
                        continue;
                    }
                    if (inputContinuation != PetDanceInputContinuation.Dispatch || !PetDanceTrialFrameGate.MayDispatch(time, FrameTimeline.TimestampMs()))
                    {
                        if (!PetDanceTrialFrameGate.MayDispatch(time, FrameTimeline.TimestampMs())) staleFrames++;
                        if (frameWaits.Count < 128) frameWaits.Add(new { captured_ms = time, observed_ms = FrameTimeline.TimestampMs(), pending_keys = pending.Count, armed = true, disposition = "waiting-for-fresh-Go-before-input", no_input_sent = true });
                        soundStatus = "waiting-for-fresh-Go"; soundReason = "Response keys retained; waiting for fresh Go or actual feedback.";
                        await Task.Delay(25, token).ConfigureAwait(false);
                        continue;
                    }
                    if (keyDowns >= 32) { reason = "input-budget"; break; }
                    soundStatus = selectedResponse?.Source ?? latestAgreement?.Status ?? "responding";
                    soundReason = selectedResponse?.Reason ?? latestAgreement?.Reason ?? "Sending a verified response during fresh Go.";
                    var key = pending.Peek();
                    DodgePulseResult receipt;
                    try
                    {
                        receipt = await pulse.RunAsync(target, client, key, (int)PetDanceResponseWindow.KeyHoldMs, time, token,
                            maximumCaptureAgeMs: PetDanceTrialFrameGate.MaximumAgeMs, responseDeadlineMs: responseWindow.DeadlineQpcMs).ConfigureAwait(false);
                    }
                    catch { releases.MarkUncertain(); throw; }
                    releases.Record(receipt);
                    var deferred = receipt.Status == "rejected" && receipt.Reason == "stale-or-invalid-frame" && receipt.DownMs == null && receipt.UpMs == null;
                    receipts.Add(new { key, capture_ms = time, receipt, deferred }); keys++;
                    if (receipt.Status == "rejected" && receipt.Reason == "response-window-expired" && receipt.DownMs == null && receipt.UpMs == null)
                    {
                        responseTiming.Add(new
                        {
                            qpc_ms = FrameTimeline.TimestampMs(),
                            responseWindow.GoQpcMs,
                            responseWindow.DeadlineQpcMs,
                            unfinished_keys = pending.Count,
                            status = "final-input-check-expired"
                        });
                        RejectRound(time, "Response deadline reached before key-down; waiting for the next preview.");
                        continue;
                    }
                    if (deferred)
                    {
                        deferredInputs++;
                        soundStatus = "waiting-for-fresh-Go"; soundReason = "The pulse rejected an aged frame before any key was sent; response retained.";
                        await Task.Delay(25, token).ConfigureAwait(false);
                        continue;
                    }
                    if (receipt.DownMs != null) { pending.Dequeue(); keyDowns++; }
                    if (receipt.Status != "dispatched" || receipt.UpMs == null) { reason = "input-rejected: " + (receipt.Reason ?? receipt.Status); break; }
                    nextKey = receipt.UpMs.Value + PetDanceResponseWindow.KeyGapMs;
                    if (pending.Count == 0)
                    {
                        responseWindow.Complete(receipt.UpMs.Value);
                        responseTiming.Add(new
                        {
                            responseWindow.GoQpcMs,
                            responseWindow.DeadlineQpcMs,
                            responseWindow.CompletedQpcMs,
                            responseWindow.CompletionDurationMs,
                            responseWindow.CompletedWithinBudget,
                            status = "all-keys-released"
                        });
                    }
                }
                if (frames >= 6000) { reason = "visual-event-budget"; break; }
                await Task.Delay(25, token).ConfigureAwait(false);
            }
            vision.VerifyInputsUnchanged();
        }
        catch (OperationCanceledException) { reason = "cancelled"; }
        catch (Exception error) { reason = "error: " + error.Message; }
        finally
        {
            if (audio != null)
                try
                {
                    audio.SetVideoEnd(FrameTimeline.TimestampMs());
                    await audio.StopAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                    audioCleanup = true;
                    using var audioReceipt = JsonDocument.Parse(GameExperiments.BoundedBytes(Path.Combine(output, "audio", "audio-timing.json"), 24 * 1024 * 1024));
                    audioCleanupConfirmed = audioReceipt.RootElement.GetProperty("capture_cleanup_confirmed").GetBoolean();
                }
                catch (Exception error) { cleanupError = "Audio cleanup: " + error.Message; }
            if (observer != null)
                try { await observer.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false); captureCleanup = true; }
                catch (Exception error) { cleanupError = (cleanupError == null ? "" : cleanupError + "; ") + "Capture cleanup: " + error.Message; }
            try { await snapshots.CompleteAsync().WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false); snapshotsComplete = true; }
            catch (Exception error) { cleanupError = (cleanupError == null ? "" : cleanupError + "; ") + "Snapshot cleanup: " + error.Message; }
        }
        PetDanceAudioObservation[] audioSnapshot = [];
        var audioSnapshotComplete = Monitor.TryEnter(audioGate, 100);
        if (audioSnapshotComplete) { try { audioSnapshot = audioEvents.ToArray(); } finally { Monitor.Exit(audioGate); } }
        var report = Path.Combine(output, "live-report.json");
        GameExperiments.WriteAtomic(report, new
        {
            schema_version = 1,
            reason,
            frames,
            receipts,
            outcomes,
            proposals,
            rejected_rounds = rejectedRounds,
            events,
            audio_required = audioRequired,
            audio_required_for_input = audioAvailability.Required,
            audio_available = audioAvailability.Available,
            audio_unavailable_at_qpc_ms = audioUnavailableAt,
            control_setting_required = controlPermission != null,
            visual_sequence_enabled = visualSequenceEnabled,
            sequence_policy = visualSequenceEnabled ? "visual-first-independent-sound-fallback" : audioRequired ? "sound-led" : "legacy-visual",
            response_budget_ms = PetDanceResponseWindow.BudgetMs,
            response_timing = responseTiming,
            audio_template_sha256 = detector?.TemplateSourceSha256,
            sound_sequence_status = soundStatus,
            sound_sequence_reason = soundReason,
            current_sound_cues = currentSoundCues,
            expected_sound_sequence_length = soundSequence.Expected,
            audio_events = audioSnapshot,
            audio_snapshot_complete = audioSnapshotComplete,
            audio_cleanup_awaited = audioCleanup,
            audio_capture_cleanup_confirmed = audioCleanupConfirmed,
            capture_cleanup_awaited = captureCleanup,
            cleanup_error = cleanupError,
            snapshots_complete = snapshotsComplete,
            snapshots_saved = snapshots.Saved,
            snapshots_skipped = snapshots.Skipped,
            snapshot_error = snapshots.Error,
            armed = arming.Armed,
            stale_frames = staleFrames,
            frame_waits = frameWaits,
            maximum_input_capture_age_ms = PetDanceTrialFrameGate.MaximumAgeMs,
            maximum_armed_fresh_frame_wait_ms = PetDanceTrialFrameGate.MaximumWaitMs,
            arrow_attempts = keys,
            dispatched_keys = keyDowns,
            deferred_inputs = deferredInputs,
            input_release_confirmed = releases.Confirmed,
            game_victory_verified = false,
            volume_changed = false,
            elapsed_ms = timer.Elapsed.TotalMilliseconds,
            gameplay_ms = clock.ActiveMs,
            paused_ms = clock.PausedMs,
            last_scene = scene.ToString(),
            content_changes = contentChanges,
            mean_analysis_ms = latencies.Count > 0 ? latencies.Average() : 0,
            pins = vision.Pins,
            source_geometry = sourceGeometry,
            normalized_geometry = vision.CanonicalSize,
            capture_mapping = vision.CaptureGeometry,
            geometry_adjustment = "Exact dimensions or the single mapping declared in the pinned profile; evidence remains separate from live qualification.",
            native_idle = "pending-supervised-menu-return",
            feeding_enabled = false,
            focus_changes = false
        });
        return new(report, captureCleanup && audioCleanup && audioCleanupConfirmed && cleanupError == null, releases.Confirmed,
            arming.Baseline, arming.Armed && reason is "five-results-observed" or "three-failures-observed");
    }

    private static ulong Fingerprint(Bitmap panel)
    {
        // Diagnostic only: unchanged content can be a legitimate static loading scene.
        var hash = 14695981039346656037UL;
        for (var y = 0; y < panel.Height; y += 16)
            for (var x = 0; x < panel.Width; x += 16)
                hash = unchecked((hash ^ (uint)panel.GetPixel(x, y).ToArgb()) * 1099511628211UL);
        return hash;
    }
}
