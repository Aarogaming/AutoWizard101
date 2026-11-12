using System.Diagnostics;

namespace ProjectMaelstrom.Utilities;

/// <summary>Passive, finite video/input collection. No activation, mouse or key dispatch.
/// Start/stop request files coordinate a human demonstration without missing the ready window.</summary>
internal static class W101DemonstrationRecorder
{
    internal static void ValidateSeconds(int seconds)
    {
        if (seconds is < 2 or > 180) throw new ArgumentOutOfRangeException(nameof(seconds), "Use 2–180 seconds.");
    }

    private sealed class PassiveHost : Form
    {
        protected override bool ShowWithoutActivation => true;
        protected override CreateParams CreateParams
        {
            get { var parameters = base.CreateParams; parameters.ExStyle |= 0x08000000; return parameters; }
        }
    }

    public static void Run(nint handle, string directory, int seconds, bool supervisedUi = false, bool includeGameAudio = false)
    {
        ValidateSeconds(seconds);
        if (Directory.Exists(directory) || File.Exists(directory)) throw new IOException("Choose a new demonstration directory.");
        var target = GameWindows.Read(handle) ?? throw new InvalidOperationException("The selected game window is unavailable.");
        if (!HomeMovementGate.IsWizard101(target)) throw new InvalidOperationException("Select the Wizard101 client.");
        Directory.CreateDirectory(directory);
        using var host = new PassiveHost { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new(-30000, -30000) };
        host.Shown += async (_, _) =>
        {
            using var stop = new CancellationTokenSource();
            using var timer = new System.Windows.Forms.Timer { Interval = 250 };
            GameplayInputRecorder? inputs = null;
            var video = Path.Combine(directory, "video");
            var clock = Stopwatch.StartNew();
            using var process = Process.GetCurrentProcess(); var cpu = process.TotalProcessorTime;
            string reason = "standby-timeout"; var verified = false;
            timer.Tick += (_, _) => { if (File.Exists(Path.Combine(directory, "stop.request"))) stop.Cancel(); };
            timer.Start();
            try
            {
                GameExperiments.WriteAtomic(Path.Combine(directory, "standby.json"), new
                {
                    state = "waiting-for-start",
                    target.ProcessId,
                    target.StartedUtcTicks,
                    maximum_seconds = seconds,
                    input_profile = "Wizard101Navigation",
                    actor = supervisedUi ? "supervised-ui" : "human-demonstration",
                    keys = GameplayInputRecorder.KeyNamesFor(GameplayInputProfile.Wizard101Navigation),
                    sends_input = false,
                    changes_focus = false,
                    audio = includeGameAudio,
                    audio_scope = includeGameAudio ? "selected-game-process-tree; no microphone or desktop mix" : "off",
                    maximum_standby_seconds = 300
                });
                while (!File.Exists(Path.Combine(directory, "start.request")) && clock.Elapsed.TotalSeconds < 300)
                    await Task.Delay(200, stop.Token);
                if (!File.Exists(Path.Combine(directory, "start.request"))) throw new TimeoutException("No start request arrived within five minutes.");
                stop.Token.ThrowIfCancellationRequested();
                var client = GameWindows.ClientSize(target);
                inputs = GameplayInputRecorder.Install(target, GameplayInputProfile.Wizard101Navigation);
                await Task.Run(() => GpuVideoRecording.RunAsync(target, video, TimeSpan.FromSeconds(seconds), stop.Token,
                    () => GameExperiments.WriteAtomic(Path.Combine(directory, "recording-ready.json"), new { state = "recording", started_utc = DateTime.UtcNow, maximum_seconds = seconds }),
                    inputs.Start, inputs.Stop, allowRateConvertedOutput: true, includeGameAudio: includeGameAudio));
                verified = true; reason = stop.IsCancellationRequested ? "stopped" : "duration-limit";
                inputs.Stop(FrameTimeline.TimestampMs()); inputs.Save(video, videoVerified: true);
                inputs.Dispose(); inputs = null;
                using var audioReport = includeGameAudio ? System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(video, "audio-timing.json"))) : null;
                var audioStatus = audioReport?.RootElement.GetProperty("status").GetString();
                GameExperiments.WriteAtomic(Path.Combine(directory, "demonstration.json"), new
                {
                    schema_version = 1,
                    kind = supervisedUi ? "supervised-ui-lifecycle-observation" : "human-route-and-pet-demonstration",
                    actor = supervisedUi ? "supervised-ui" : "human-demonstration",
                    video_verified = true,
                    target.ProcessId,
                    target.StartedUtcTicks,
                    client_width = client.Width,
                    client_height = client.Height,
                    video = "video/capture.mp4",
                    timing = "video/timing.json",
                    observed_inputs = "video/input-events.json",
                    input_coordinate_space = "client pixels; video includes window borders and any reported encoder scale",
                    exact_input_video_alignment_verified = false,
                    note = "Decoded video and input log share a capture clock. Inspect timing.json for rate conversion; do not infer unique observations from encoded FPS.",
                    route_qualified = false,
                    labels_verified = false,
                    automatic_training_eligible = false,
                    supervision_note = supervisedUi ? "Continuous observation from before menu interaction through return. Observed or injected inputs are not verified correct-action labels; review all stages and outcomes." : "Human actions and outcomes require independent review before training.",
                    inputs_sent = 0,
                    audio_requested = includeGameAudio,
                    audio = audioStatus == "complete",
                    audio_status = audioStatus ?? "off",
                    audio_timing = includeGameAudio ? "video/audio-timing.json" : null,
                    audio_note = includeGameAudio ? "Separate game-audio.wav and source-clock packet mapping. Read audio-timing.json for actual completion and gaps; MP4 has no embedded audio." : "Audio off."
                });
            }
            catch (OperationCanceledException) { reason = "cancelled"; }
            catch (Exception error) { reason = "failed: " + error.Message; Environment.ExitCode = 1; }
            finally
            {
                timer.Stop();
                if (inputs != null)
                {
                    inputs.Stop(FrameTimeline.TimestampMs()); inputs.Dispose();
                    try { if (Directory.Exists(video) && !File.Exists(Path.Combine(video, "input-events.json"))) inputs.Save(video, videoVerified: false); }
                    catch (Exception error) { reason += "; input-save: " + error.Message; }
                }
                try
                {
                    GameExperiments.WriteAtomic(Path.Combine(directory, "finished.json"), new
                    {
                        reason,
                        video_verified = verified,
                        elapsed_ms = clock.Elapsed.TotalMilliseconds,
                        process_cpu_ms = (process.TotalProcessorTime - cpu).TotalMilliseconds,
                        cpu_scope = "This process only; excludes encoder helper and driver work",
                        inputs_sent = 0,
                        hooks_released = true
                    });
                }
                finally { host.Close(); }
            }
        };
        Application.Run(host);
    }
}
