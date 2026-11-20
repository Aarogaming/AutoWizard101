using System.Diagnostics;

namespace ProjectMaelstrom.Utilities;

/// <summary>Finite, passive audio/visual study. No activation or input APIs.
/// One selected-window stream and one selected-process audio source; no model.</summary>
internal static class PetDanceSoundObserver
{
    private sealed class Host : Form
    {
        protected override bool ShowWithoutActivation => true;
        protected override CreateParams CreateParams { get { var p = base.CreateParams; p.ExStyle |= 0x08000000; return p; } }
    }

    public static void Run(nint handle, string profilePath, string comparisonPath, string output, int seconds)
    {
        W101DemonstrationRecorder.ValidateSeconds(seconds);
        if (Directory.Exists(output) || File.Exists(output)) throw new IOException("Choose a new observation folder.");
        var target = GameWindows.Read(handle) ?? throw new InvalidOperationException("Wizard101 is unavailable.");
        if (!HomeMovementGate.IsWizard101(target)) throw new InvalidOperationException("Select Wizard101.");
        using var profileCheck = new PetDancePanelVision(profilePath);
        _ = PetDanceAudioDetector.LoadFrozenComparison(comparisonPath);
        Directory.CreateDirectory(output);
        using var host = new Host { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new(-30000, -30000) };
        host.Shown += async (_, _) =>
        {
            using var cancel = new CancellationTokenSource();
            using var timer = new System.Windows.Forms.Timer { Interval = 100 };
            timer.Tick += (_, _) => { if (File.Exists(Path.Combine(output, "stop.request"))) cancel.Cancel(); };
            timer.Start();
            try
            {
                GameExperiments.WriteAtomic(Path.Combine(output, "standby.json"), new
                {
                    state = "waiting-for-start",
                    target.ProcessId,
                    target.StartedUtcTicks,
                    maximum_seconds = seconds,
                    maximum_standby_seconds = 300,
                    inputs_sent = 0,
                    audio_scope = "selected-process-tree",
                    changes_volume = false,
                    geometry_qualified = false,
                    live_qualified = false
                });
                var wait = Stopwatch.StartNew();
                while (!File.Exists(Path.Combine(output, "start.request")) && wait.Elapsed.TotalSeconds < 300)
                    await Task.Delay(100, cancel.Token);
                if (!File.Exists(Path.Combine(output, "start.request"))) throw new TimeoutException("Observation start was not requested.");
                await Task.Run(() => ObserveAsync(target, profilePath, comparisonPath, output, seconds, cancel.Token));
            }
            catch (Exception error)
            {
                Environment.ExitCode = 1;
                GameExperiments.WriteAtomic(Path.Combine(output, "failed.json"), new { error = error.Message, inputs_sent = 0 });
            }
            finally { timer.Stop(); host.Close(); }
        };
        Application.Run(host);
    }

    private static async Task ObserveAsync(GameWindow target, string profilePath, string comparisonPath,
        string output, int seconds, CancellationToken token)
    {
        using var vision = new PetDancePanelVision(profilePath);
        var detector = PetDanceAudioDetector.LoadFrozenComparison(comparisonPath);
        var audioEvents = new List<PetDanceAudioObservation>(); var audioGate = new object();
        var visualEvents = new List<object>(); var proposals = new List<object>(); var outcomes = new List<object>();
        var sequence = new PetDanceSequence(3);
        var clock = new GameplayPauseClock(45000, Math.Max(1000, seconds * 1000));
        using var process = Process.GetCurrentProcess(); var initialCpu = process.TotalProcessorTime;
        var wall = Stopwatch.StartNew(); var sourceSize = Size.Empty; var client = GameWindows.ClientSize(target);
        WindowObserver? observer = null; GameAudioRecording? audio = null;
        double last = -1, lastFrameWall = 0, watchStart = -1, nextProgress = 0, nextDiagnostic = 0, maxAnalysisMs = 0, sumAnalysisMs = 0;
        var previousPhase = DancePhase.Unknown; var phase = DancePhase.Unknown;
        var frames = 0; var pictures = 0; var cueCount = 0; var staleFrames = 0; string reason = "duration-limit";
        var audioCleanup = false; var videoCleanup = false;
        string? cleanupError = null; PetDanceAudioVisualResult? latestAgreement = null; string[] currentCues = [];
        var audioDirectory = Path.Combine(output, "audio"); Directory.CreateDirectory(audioDirectory);
        var snapshots = new PetDanceSnapshotWriter(); var snapshotsComplete = false;
        void SaveFrame(Bitmap image, string label)
        {
            if (pictures >= 64) return;
            snapshots.TrySave(image, Path.Combine(output, $"frame-{pictures++:00}-{label}.png"));
        }
        try
        {
            observer = new WindowObserver(target); sourceSize = new(observer.Width, observer.Height);
            audio = await GameAudioRecording.StartAsync(target.ProcessId, target.StartedUtcTicks, audioDirectory,
                () => GameWindows.IsCurrent(target), token, packet =>
                {
                    lock (audioGate)
                    {
                        var observations = detector.Push(packet);
                        if (audioEvents.Count + observations.Count > 5000) throw new InvalidDataException("Audio analysis event limit.");
                        var processed = FrameTimeline.TimestampMs();
                        audioEvents.AddRange(observations.Select(o => o with { ProcessedQpcMs = processed }));
                    }
                }).ConfigureAwait(false);
            GameExperiments.WriteAtomic(Path.Combine(output, "recording-ready.json"), new
            { state = "passive-listening", started_utc = DateTime.UtcNow, inputs_sent = 0, changes_volume = false });
            while (wall.Elapsed.TotalSeconds < seconds)
            {
                token.ThrowIfCancellationRequested();
                if (!GameWindows.IsCurrent(target) || GameWindows.ClientSize(target) != client)
                { reason = "target-or-geometry-changed"; break; }
                if (!audio.IsCapturing) { reason = "audio-stopped-or-analysis-backlog"; break; }
                using var frame = await PetDanceObservationRead.ReadAsync(() => observer.ReadTimestampedAsync(latestOnly: true), token).ConfigureAwait(false);
                if (frame?.CaptureTime is not { } stamp || stamp.TotalMilliseconds <= last)
                {
                    clock.Advance(wall.Elapsed.TotalMilliseconds, phase == DancePhase.Loading, false);
                    if (clock.StopReason != null) { reason = clock.StopReason; break; }
                    if (wall.Elapsed.TotalMilliseconds - lastFrameWall > 5000) { reason = "capture-not-progressing"; break; }
                    await Task.Delay(15, token).ConfigureAwait(false); continue;
                }
                var time = stamp.TotalMilliseconds;
                var ahead = time - FrameTimeline.TimestampMs();
                if (ahead is > 0 and <= 30) await Task.Delay((int)Math.Ceiling(ahead), token).ConfigureAwait(false);
                if (!DodgeLiveRun.IsFreshCapture(time, FrameTimeline.TimestampMs(), 150))
                {
                    staleFrames++;
                    clock.Advance(wall.Elapsed.TotalMilliseconds, phase == DancePhase.Loading, false);
                    // This observer owns no input. Withhold visual decisions while
                    // listening continues; silence/static Idle is not gameplay.
                    latestAgreement = null;
                    GameExperiments.WriteAtomic(Path.Combine(output, "progress.json"), new
                    { state = "waiting-for-fresh-frame", phase = "Unknown", frames, staleFrames, inputs_sent = 0, agreement = (object?)null });
                    if (clock.StopReason != null) { reason = clock.StopReason; break; }
                    if (wall.Elapsed.TotalMilliseconds - lastFrameWall >= 5000) { reason = "capture-not-progressing"; break; }
                    await Task.Delay(25, token).ConfigureAwait(false); continue;
                }
                var compute = Stopwatch.StartNew();
                last = time; lastFrameWall = wall.Elapsed.TotalMilliseconds;
                using var panel = vision.Extract(frame.Image);
                var seen = vision.Read(panel, time); phase = seen.Phase;
                var gameplay = phase is DancePhase.Watch or DancePhase.Go or DancePhase.Resolving or DancePhase.Success or DancePhase.Failure;
                clock.Advance(wall.Elapsed.TotalMilliseconds, phase == DancePhase.Loading, gameplay);
                if (frames == 0 || phase != previousPhase) SaveFrame(frame.Image, phase.ToString());
                if (phase == DancePhase.Unknown && wall.Elapsed.TotalMilliseconds >= nextDiagnostic)
                {
                    SaveFrame(frame.Image, "unknown-diagnostic");
                    nextDiagnostic = wall.Elapsed.TotalMilliseconds + 2000;
                }
                if (phase == DancePhase.Watch && previousPhase != DancePhase.Watch)
                { watchStart = time; cueCount = 0; latestAgreement = null; }
                var decision = sequence.Observe(time, vision.CanonicalSize, phase, seen.Cue, seen.FadeOf, seen.Opacity);
                currentCues = decision.Cues;
                if (currentCues.Length > cueCount)
                { SaveFrame(frame.Image, $"cue-{currentCues.Length}-{seen.Cue}"); cueCount = currentCues.Length; }
                if (decision.Proposals.Length > 0)
                {
                    PetDanceAudioObservation[] snapshot; lock (audioGate) snapshot = audioEvents.ToArray();
                    latestAgreement = PetDanceAudioVisualAgreement.Compare(currentCues, snapshot, watchStart, time);
                    proposals.Add(new { qpc_ms = time, decision.Proposals, agreement = latestAgreement, dispatched = false });
                    SaveFrame(frame.Image, "Go-proposal");
                }
                if (phase is DancePhase.Success or DancePhase.Failure && phase != previousPhase)
                    outcomes.Add(new { qpc_ms = time, outcome = phase.ToString() });
                visualEvents.Add(new
                {
                    qpc_ms = time,
                    phase = phase.ToString(),
                    cue = seen.Cue.ToString(),
                    fade_of = seen.FadeOf.ToString(),
                    seen.Opacity,
                    seen.Error,
                    seen.Margin,
                    seen.PhaseError,
                    decision.Cues,
                    decision.Rejected,
                    decision.Reason
                });
                frames++; previousPhase = phase;
                sumAnalysisMs += compute.Elapsed.TotalMilliseconds; maxAnalysisMs = Math.Max(maxAnalysisMs, compute.Elapsed.TotalMilliseconds);
                if (wall.Elapsed.TotalMilliseconds >= nextProgress)
                {
                    PetDanceAudioObservation[] recent; string status; long analyzed;
                    lock (audioGate) { recent = audioEvents.Where(a => a.Emitted).TakeLast(10).ToArray(); status = detector.LastStatus; analyzed = detector.AnalyzedWindows; }
                    GameExperiments.WriteAtomic(Path.Combine(output, "progress.json"), new
                    {
                        state = clock.IsLoading ? "paused-loading" : "passive-observation",
                        qpc_ms = time,
                        phase = phase.ToString(),
                        cue = seen.Cue.ToString(),
                        cues = currentCues,
                        decision.Rejected,
                        decision.Reason,
                        agreement = phase == DancePhase.Go ? latestAgreement : null,
                        proposals = proposals.TakeLast(1).ToArray(),
                        outcomes,
                        audio_status = status,
                        analyzed_windows = analyzed,
                        recent_audio = recent,
                        frames,
                        clock.ActiveMs,
                        clock.PausedMs,
                        capture_age_ms = FrameTimeline.TimestampMs() - time,
                        inputs_sent = 0,
                        geometry_qualified = false
                    });
                    nextProgress = wall.Elapsed.TotalMilliseconds + 100;
                }
                if (clock.StopReason != null) { reason = clock.StopReason; break; }
                if (frames >= 6000) { reason = "visual-event-limit"; break; }
                await Task.Delay(25, token).ConfigureAwait(false);
            }
            vision.VerifyInputsUnchanged();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { reason = "stopped"; }
        catch (Exception error) { reason = "failed: " + error.Message; Environment.ExitCode = 1; }
        finally
        {
            if (audio != null)
                try { await audio.StopAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false); audioCleanup = true; }
                catch (Exception error) { cleanupError = error.Message; }
            if (observer != null)
                try { await observer.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false); videoCleanup = true; }
                catch (Exception error) { cleanupError = error.Message; }
            try { await snapshots.CompleteAsync().WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false); snapshotsComplete = true; }
            catch (Exception error) { cleanupError = error.Message; }
            PetDanceAudioObservation[] audioSnapshot = [];
            long? analyzed = null, emitted = null, discontinuities = null;
            var audioSnapshotComplete = Monitor.TryEnter(audioGate, 100);
            if (audioSnapshotComplete)
            {
                try { audioSnapshot = audioEvents.ToArray(); analyzed = detector.AnalyzedWindows; emitted = detector.EmittedCount; discontinuities = detector.Discontinuities; }
                finally { Monitor.Exit(audioGate); }
            }
            GameExperiments.WriteAtomic(Path.Combine(output, "report.json"), new
            {
                schema_version = 1,
                kind = "passive-pet-dance-audio-visual-study",
                reason,
                target.ProcessId,
                target.StartedUtcTicks,
                elapsed_ms = wall.Elapsed.TotalMilliseconds,
                process_cpu_ms = (process.TotalProcessorTime - initialCpu).TotalMilliseconds,
                cpu_scope = "Observer process only; excludes GPU/driver work",
                frames,
                stale_frames = staleFrames,
                source_geometry = sourceSize,
                geometry_mapping = vision.CaptureGeometry,
                geometry_qualified = false,
                profile_pins = vision.Pins,
                audio_template_sha256 = detector.TemplateSourceSha256,
                visual_events = visualEvents,
                audio_events = audioSnapshot,
                audio_snapshot_complete = audioSnapshotComplete,
                proposals,
                outcomes,
                mean_visual_analysis_ms = frames > 0 ? sumAnalysisMs / frames : 0,
                max_visual_analysis_ms = maxAnalysisMs,
                audio_windows = analyzed,
                audio_emitted = emitted,
                audio_discontinuities = discontinuities,
                inputs_sent = 0,
                audio_cleanup_awaited = audioCleanup,
                window_cleanup_confirmed = videoCleanup,
                input_hooks_installed = false,
                cleanup_error = cleanupError,
                snapshot_writer_complete = snapshotsComplete,
                snapshots_saved = snapshots.Saved,
                snapshots_skipped = snapshots.Skipped,
                snapshot_error = snapshots.Error,
                native_idle = "requires-independent-supervised-menu-confirmation",
                live_qualified = false,
                volume_changed = false,
                model_loaded = false,
                automatic_training_eligible = false
            });
            GameExperiments.WriteAtomic(Path.Combine(output, "finished.json"), new
            { reason, inputs_sent = 0, audio_cleanup_awaited = audioCleanup, window_cleanup_confirmed = videoCleanup, input_hooks_installed = false, cleanup_error = cleanupError });
        }
    }
}
